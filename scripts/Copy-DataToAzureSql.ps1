<#
.SYNOPSIS
    把本機 SQL Server 的資料表逐張搬到 Azure SQL Database。

.DESCRIPTION
    免費方案的 Azure SQL Database「不能」用還原備份或複製既有資料庫建出來
    （官方明文不支援），所以搬資料只有一條路：
    建空的資料庫 → 跑 EF Core migration 把表建出來 → 用 bcp 逐張灌。

    這支腳本做的是最後一步。設計上有三個刻意的地方：

    1. 逐張搬、逐張報告耗時與列數。
       Azure SQL 免費方案的額度單位是「vCore 秒」，每月 100,000 秒，資料庫醒著的每一秒都算
       （最低 0.5 vCore）。一次把近 800 萬列推上去而中途失敗，等於白燒一次額度。
       所以預設先搬小表、把實測數字印出來，再決定要不要繼續推大表。

    2. 以「整張表」為單位可以重跑：雲端列數已達本機列數的表會被跳過。
       ⚠ 搬到一半中斷的表（雲端有資料、但比本機少）不會自動接續——bcp 重送會從第一列開始，
       撞上已經存在的主鍵。腳本遇到這種表會略過它、在最後印出清空指令，清掉之後再重跑。

    3. 資料庫正在自動暫停時，第一次連線要等它恢復（約 1 分鐘），sqlcmd 預設的登入逾時等不到。
       所以開始之前先把雲端資料庫叫醒、確認連得上，才進入逐表搬遷。

    4. 用 bcp 的原生格式（-n）。它保留型別、不做文字轉換，也是最快的一種；
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
    [int]      $LoginTimeoutSeconds = 30,
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
    $output = & sqlcmd -S $Server -d $Database -U $User -P $Password -C -l $LoginTimeoutSeconds -Q $query -h -1 -W
    if ($LASTEXITCODE -ne 0) { throw "查列數失敗（$Table）：$output" }

    $digits = $output | Where-Object { $_ -match '^\d+$' } | Select-Object -First 1
    if (-not $digits) { throw "查列數的輸出裡沒有數字（$Table）：$output" }
    return [int64] $digits
}

# serverless 從自動暫停恢復約需 1 分鐘，期間登入可能逾時、也可能直接回 40613，
# 兩種都只能等。所以開始搬之前先反覆試連，連上了才往下走
function Wait-TargetOnline {
    param([string] $Server, [string] $Database, [string] $User, [string] $Password)

    # 函式內改成 Continue：Windows PowerShell 5.1 在 Stop 之下用 2>&1 收原生程式的 stderr，
    # 第一行錯誤訊息就會中斷整支腳本，重試迴圈等於沒有作用
    $ErrorActionPreference = 'Continue'

    $maxAttempts = 6
    for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
        $output = (& sqlcmd -S $Server -d $Database -U $User -P $Password -C -l $LoginTimeoutSeconds -Q 'SET NOCOUNT ON; SELECT 1;' -h -1 -W 2>&1 | ForEach-Object { "$_" }) -join ' '
        if ($LASTEXITCODE -eq 0) { return }

        if ($attempt -lt $maxAttempts) {
            Write-Host "雲端資料庫還連不上（可能正從自動暫停中恢復），15 秒後重試（$attempt/$maxAttempts）…"
            Start-Sleep -Seconds 15
        }
    }
    throw "雲端資料庫連不上，已重試 $maxAttempts 次。最後一次的訊息：$output"
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

Wait-TargetOnline $TargetServer $TargetDatabase $TargetUser $dstPwd

$summary = @()
$partialTables = @()
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

    # 雲端有資料卻比應有的少＝上次搬到一半中斷。bcp 重送會從第一列開始、撞上已經存在的主鍵，
    # 所以不猜要從哪一列接，留給操作者清空後重跑
    if ($targetRows -gt 0) {
        Write-Warning ("{0} 雲端已有 {1:N0} 列、少於應有的 {2:N0} 列（上次搬到一半中斷），這次略過" -f $table, $targetRows, $expected)
        $summary += [pscustomobject]@{ 資料表 = $table; 本機 = $sourceRows; 雲端 = $targetRows; 秒 = 0; 結果 = '不完整，未搬' }
        $partialTables += $table
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
    # -b 分批送出、每批各自 commit：中途斷掉時雲端會留下已送出的批次，就是上面判斷的「不完整」
    $inArgs = @($table, 'in', $dataFile, '-S', $TargetServer, '-d', $TargetDatabase,
                '-U', $TargetUser, '-P', $dstPwd, '-n', '-q', '-E', '-b', $BatchSize,
                '-l', $LoginTimeoutSeconds)
    & bcp @inArgs | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "bcp in 失敗：$table。雲端可能已留下部分資料，重跑時這張表會被判為不完整、並印出清空指令" }

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

if ($partialTables.Count -gt 0) {
    Write-Host '⚠ 以下資料表在雲端只有一部分資料，這次沒有搬。清空雲端那一份之後再重跑本腳本'
    Write-Host '  （清空指令只對雲端執行；省略 -P，sqlcmd 會自己詢問密碼）：'
    foreach ($t in $partialTables) {
        $name = '[' + $t.Split('.')[0] + '].[' + $t.Split('.')[1] + ']'
        Write-Host ('  sqlcmd -S {0} -d {1} -U {2} -C -Q "TRUNCATE TABLE {3};"' -f $TargetServer, $TargetDatabase, $TargetUser, $name)
    }
    Write-Host '  若回報這張表被外來鍵參照而無法 TRUNCATE，把指令改成 DELETE FROM（較慢）。'
    Write-Host ''
}

Write-Host '⚠ 額度換算：Azure SQL 免費方案每月 100,000 vCore 秒，資料庫醒著的每一秒都算。'
Write-Host '   量實際消耗看資料庫「計量」的 App CPU billed（每分鐘計費的 vCore 秒）；'
Write-Host '   「剩餘可用量」有延遲、刻度粗，不適合量單次搬遷。'
Write-Host '   先量一張中型表，再決定要不要推大表。'
