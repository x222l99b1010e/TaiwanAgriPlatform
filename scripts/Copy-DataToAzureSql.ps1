<#
.SYNOPSIS
    把本機 SQL Server 的資料表逐張搬到 Azure SQL Database。

.DESCRIPTION
    免費方案的 Azure SQL Database「不能」用還原備份或複製既有資料庫建出來
    （官方明文不支援），所以搬資料只有一條路：
    建空的資料庫 → 跑 EF Core migration 把表建出來 → 用 bcp 逐張灌。

    這支腳本做的是最後一步。設計上有三個刻意的地方：

    1. 逐張搬、逐張報告耗時與列數。
       Azure SQL 免費方案的額度單位是「vCore 秒」（用了幾顆 CPU × 幾秒），
       每月 100,000 秒。一次把 790 萬列推上去而中途失敗，等於白燒一次額度。
       所以預設先搬小表、把實測數字印出來，再決定要不要繼續推大表。

    2. 可以重跑。已經灌完（雲端列數 ≥ 本機列數）的表會被跳過，
       中斷之後直接再跑一次即可，不必從頭來。

    3. 用 bcp 的原生格式（-n）。它保留型別、不做文字轉換，也是最快的一種；
       前提是兩邊的資料表結構完全一致——由 EF migration 保證。

    ⚠ bcp 匯入預設不檢查外來鍵、也不觸發 trigger，所以資料表的順序不影響結果。

.PARAMETER SourceServer
    本機 SQL Server（預設 docker-compose 起的那個）。

.PARAMETER TargetServer
    Azure SQL 的伺服器位址，例如 your-server.database.windows.net。

.PARAMETER Tables
    要搬的資料表（schema.table）。不指定就用內建的預設清單（由小到大排序）。

.PARAMETER MaxRowsPerTable
    只搬每張表的前 N 列。用來先量成本，不是正式搬遷。0 代表全部。

.EXAMPLE
    # 先量成本：只搬一張中型表
    .\Copy-DataToAzureSql.ps1 -TargetServer x.database.windows.net -TargetUser sqladmin `
        -Tables 'pet.OfficialLostPetPosts'

.EXAMPLE
    # 正式全量搬遷
    .\Copy-DataToAzureSql.ps1 -TargetServer x.database.windows.net -TargetUser sqladmin

.NOTES
    需要 bcp 與 sqlcmd（隨 SQL Server Client SDK 一起安裝，通常已在 PATH 上）。

    密碼依序從三個地方取，先有先用：
      1. -SourcePassword / -TargetPassword 參數（SecureString）
      2. 環境變數 TAIWANAGRI_SOURCE_PASSWORD / TAIWANAGRI_TARGET_PASSWORD
      3. 互動輸入（Read-Host -AsSecureString）
    ⚠ 不要把密碼當明文字串打在指令列上——那一整行會留在 PowerShell 的歷史紀錄檔裡。
    ⚠ bcp 與 sqlcmd 只接受 -P 明文參數，所以密碼在這兩支程式執行的那幾秒內會出現在
      作業系統的行程清單上（工作管理員的「命令列」欄位看得到）。單人使用的機器可以接受；
      多人共用的主機請改用 Azure AD 驗證的 bcp -G，不要用這支腳本。
#>
[CmdletBinding()]
param(
    [string]   $SourceServer   = 'localhost,1433',
    [string]   $SourceDatabase = 'TaiwanAgriPlatform',
    [string]   $SourceUser     = 'sa',
    [SecureString] $SourcePassword,

    [Parameter(Mandatory = $true)]
    [string]   $TargetServer,
    [string]   $TargetDatabase = 'TaiwanAgriPlatform',
    [Parameter(Mandatory = $true)]
    [string]   $TargetUser,
    [SecureString] $TargetPassword,

    [string[]] $Tables,
    [int]      $MaxRowsPerTable = 0,
    [int]      $BatchSize       = 10000,
    [string]   $WorkDirectory   = (Join-Path $env:TEMP 'taiwanagri-bcp')
)

$ErrorActionPreference = 'Stop'

# 只搬「從政府開放資料同步下來的資料」。全部的資料表分成三類，另外兩類都不搬：
#
#   ① 政府開放資料 → 搬（就是下面這張清單）
#   ② 啟動時自己種的  → 不搬。core.NavModules、core.RoleModulePermissions（DbInitializer）
#                       與 pet.Shelters（PetDbInitializer）在服務啟動時會自己建，
#                       搬過去只會跟種子資料撞在一起
#   ③ 使用者資料      → 不搬。AspNet* 帳號表、dbo.UserFarmProfiles / UserFarmCrops /
#                       UserWatchlists、weather.UserNotifications / PestRuleConfigs、
#                       pet.LostPetPosts。本機那些是開發用的假帳號，把密碼雜湊搬上雲端
#                       沒有意義也不該做；雲端的測試帳號在網站上重新註冊一個就好
#
# ⚠ core.SyncStates 要搬。它記的是「每個資料源同步到哪一天了」——不搬的話，
#   雲端第一次跑 Worker 會以為什麼都還沒同步，從頭回補一次。
#
# 清單由小到大排序：先搬小的，量到的耗時可以外推到大的，中途發現不對還來得及停。
$defaultTables = @(
    'core.SyncStates',
    'market.CropInfos',
    'market.MarketInfos',
    'market.MarketRestDays',
    'weather.RainfallStations',
    'pet.LegalSpecificPets',
    'weather.PestAlertCities',
    'weather.PestAlertCrops',
    'weather.PestAlerts',
    'weather.PestDecadeSummaries',
    'foodsafety.OrganicCertifications',
    'foodsafety.PesticideViolations',
    'market.DebrisAlertRecords',
    'weather.WeatherObservations',
    'weather.RainfallObservations',
    'pet.ShelterAnimals',
    'market.PoultryTrans',
    'market.PorkTrans',
    'pet.OfficialLostPetPosts',
    'market.AgriProductsTrans'      # 736 萬列，最後一張
)

if (-not $Tables) { $Tables = $defaultTables }

function Read-PasswordIfMissing {
    param([SecureString] $Value, [string] $EnvName, [string] $Prompt)
    if ($Value) { return $Value }
    $fromEnv = [Environment]::GetEnvironmentVariable($EnvName)
    if ($fromEnv) { return (ConvertTo-SecureString $fromEnv -AsPlainText -Force) }
    return (Read-Host -Prompt $Prompt -AsSecureString)
}

function ConvertTo-PlainText {
    param([SecureString] $Secure)
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Secure)
    try { return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
}

function Get-RowCount {
    param([string] $Server, [string] $Database, [string] $User, [string] $Password, [string] $Table)

    $schemaName = $Table.Split('.')[0]
    $tableName  = $Table.Split('.')[1]
    $query = 'SET NOCOUNT ON; SELECT COUNT_BIG(*) FROM [' + $schemaName + '].[' + $tableName + '];'

    # -h -1 去掉表頭與底線，-W 去掉尾端空白，剩下就只有那個數字
    $output = & sqlcmd -S $Server -d $Database -U $User -P $Password -C -Q $query -h -1 -W
    if ($LASTEXITCODE -ne 0) { throw "查列數失敗（$Table）：$output" }

    $digits = $output | Where-Object { $_ -match '^\d+$' } | Select-Object -First 1
    if (-not $digits) { throw "查列數的輸出裡沒有數字（$Table）：$output" }
    return [int64] $digits
}

$SourcePassword = Read-PasswordIfMissing $SourcePassword 'TAIWANAGRI_SOURCE_PASSWORD' "本機 SQL Server（$SourceUser）的密碼"
$TargetPassword = Read-PasswordIfMissing $TargetPassword 'TAIWANAGRI_TARGET_PASSWORD' "Azure SQL（$TargetUser）的密碼"
$srcPwd = ConvertTo-PlainText $SourcePassword
$dstPwd = ConvertTo-PlainText $TargetPassword

if (-not (Test-Path $WorkDirectory)) { New-Item -ItemType Directory -Path $WorkDirectory | Out-Null }

Write-Host ''
Write-Host "來源：$SourceServer / $SourceDatabase"
Write-Host "目標：$TargetServer / $TargetDatabase"
Write-Host "暫存：$WorkDirectory"
if ($MaxRowsPerTable -gt 0) { Write-Host "⚠ 成本量測模式：每張表只搬前 $MaxRowsPerTable 列" }
Write-Host ''

$summary = @()
$totalStopwatch = [Diagnostics.Stopwatch]::StartNew()

foreach ($table in $Tables) {
    $dataFile = Join-Path $WorkDirectory "$($table -replace '\.', '_').bcp"
    $stopwatch = [Diagnostics.Stopwatch]::StartNew()

    try {
        $sourceRows = Get-RowCount $SourceServer $SourceDatabase $SourceUser $srcPwd $table
        $targetRows = Get-RowCount $TargetServer $TargetDatabase $TargetUser $dstPwd $table
    }
    catch {
        Write-Warning "$table 略過：$_"
        $summary += [pscustomobject]@{ 資料表 = $table; 本機 = '-'; 雲端 = '-'; 秒 = '-'; 結果 = '查不到' }
        continue
    }

    $expected = if ($MaxRowsPerTable -gt 0) { [Math]::Min($sourceRows, $MaxRowsPerTable) } else { $sourceRows }

    # 已經灌完的就跳過，讓整支腳本可以重跑
    if ($targetRows -ge $expected -and $expected -gt 0) {
        Write-Host ("{0,-40} 已有 {1,10:N0} 列，跳過" -f $table, $targetRows)
        $summary += [pscustomobject]@{ 資料表 = $table; 本機 = $sourceRows; 雲端 = $targetRows; 秒 = 0; 結果 = '跳過' }
        continue
    }

    if ($sourceRows -eq 0) {
        Write-Host ("{0,-40} 本機是空的，跳過" -f $table)
        $summary += [pscustomobject]@{ 資料表 = $table; 本機 = 0; 雲端 = $targetRows; 秒 = 0; 結果 = '空表' }
        continue
    }

    Write-Host ("{0,-40} {1,10:N0} 列 → 匯出…" -f $table, $expected) -NoNewline

    $outArgs = @($table, 'out', $dataFile, '-S', $SourceServer, '-d', $SourceDatabase,
                 '-U', $SourceUser, '-P', $srcPwd, '-n', '-q')
    if ($MaxRowsPerTable -gt 0) { $outArgs += @('-L', $MaxRowsPerTable) }
    & bcp @outArgs | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "bcp out 失敗：$table" }

    Write-Host ' 匯入…' -NoNewline

    # -E 保留來源的識別欄位值（不加的話 Azure 會重新編號，外來鍵就對不上了）
    # -b 分批送出，中途失敗時已送出的批次會留著，重跑可以接續
    $inArgs = @($table, 'in', $dataFile, '-S', $TargetServer, '-d', $TargetDatabase,
                '-U', $TargetUser, '-P', $dstPwd, '-n', '-q', '-E', '-b', $BatchSize)
    & bcp @inArgs | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "bcp in 失敗：$table（暫存檔留在 $dataFile，可重跑）" }

    $stopwatch.Stop()
    $after = Get-RowCount $TargetServer $TargetDatabase $TargetUser $dstPwd $table
    $ok = $after -ge $expected

    Write-Host (" {0,6:N1} 秒，雲端現在 {1:N0} 列 {2}" -f $stopwatch.Elapsed.TotalSeconds, $after, $(if ($ok) { '✔' } else { '✘ 列數不符' }))
    $summary += [pscustomobject]@{
        資料表 = $table; 本機 = $sourceRows; 雲端 = $after
        秒 = [Math]::Round($stopwatch.Elapsed.TotalSeconds, 1)
        結果 = $(if ($ok) { 'OK' } else { '列數不符' })
    }

    Remove-Item $dataFile -ErrorAction SilentlyContinue
}

$totalStopwatch.Stop()

Write-Host ''
$summary | Format-Table -AutoSize
Write-Host ("總耗時 {0:N1} 分鐘" -f $totalStopwatch.Elapsed.TotalMinutes)
Write-Host ''
Write-Host '⚠ 額度換算：Azure SQL 免費方案每月 100,000 vCore 秒。'
Write-Host '   實際消耗要看 Portal 的「剩餘可用量」指標，不能只用上面的牆鐘時間推算'
Write-Host '   （匯入期間 vCore 會往上跑，不是最低的 0.5）。'
Write-Host '   先量一張中型表、看 Portal 掉了多少，再決定要不要推大表。'
