# TaiwanAgriPlatform — PR Description 合集

這份文件收錄 W1 到 W6 已完成工作的 PR Description，依照實際提交時間排序。
每一篇都是「假設當時有即時寫下來」的版本，供你閱讀、對照記憶、並作為未來寫法的參考模板。

---

## PR #001 — 基礎建設：Docker Compose + Solution 架構

**標題**：`feat: 建立 Docker Compose 基礎設施與 8-Project Solution 架構`

**背景與動機**

這是整個專案的地基。在寫任何一行業務程式碼之前，必須先把開發環境的基礎設施架好，確保「一行指令可以啟動所有依賴服務」。這一步做好了，後面每個人（包括未來的自己）都能在十分鐘內把環境跑起來，而不是在環境問題上浪費一天。

**關鍵設計決策：Docker 只跑基礎設施，.NET 應用跑在本機**

這是這個 PR 最重要的架構決策，也是最容易做錯的地方。原本可以把整個系統全部容器化，但如果 Worker 跑在 Docker 容器裡，Visual Studio 的 F5 中斷點除錯就會失效，每次修改都要重新 build image，再等容器重啟。對一個 Side Project 而言，這個代價太高。

最終決定：`docker-compose.yml` 只定義三個服務：

```
sqlserver  → 主要資料庫（SQL Server 2022）
redis      → 快取服務（Redis 7-alpine）
rabbitmq   → 訊息佇列（RabbitMQ 3-management）
```

.NET 應用程式（`TaiwanAgri.Worker`、`TaiwanAgri.Web`）在 Windows 本機直接執行，透過 `localhost` 連接這三個服務。這個設定保留了完整的 F5 除錯體驗，是開發效率和架構整潔之間刻意選擇的取捨。

**關鍵設計決策：8-Project Solution 邊界劃分**

採用 Modular Monolith 而非微服務。微服務的網路開銷、分散式事務、跨服務 Debug 複雜度，在這個規模的 Side Project 中是不必要的負擔。但在程式碼層級從第一天就劃清模組邊界——每個模組有自己的 Project，不直接存取彼此的 DbContext。

`TaiwanAgri.Core` 定位為零依賴的共用層，只放 Interface、DTO、Enum、Exception 和常數。這確保各模組 Project 都能依賴 Core，但 Core 不依賴任何人，避免循環依賴。

**驗收標準**

`docker-compose up -d` 執行後，`docker-compose ps` 顯示三個服務全部 `healthy`。
SQL Server 物件總管可用 `localhost,1433 / sa` 連線成功。

---

## PR #002 — 機密資訊防護：.gitignore、.env.example、README 清理

**標題**：`chore: 加強機密資訊防護 — .gitignore 規則、.env.example 範本、README 清理`

**背景與動機**

初始 commit 把 `docker-compose.yml` 和 `README.md` 裡的明文密碼一起推上了 repo（`SA_PASSWORD=YourStrong!Password123`、連線字串完整含帳密）。雖然這是 Side Project，但密碼進版控是不可逆的安全風險——即使之後刪掉，git history 裡仍然看得到。趁著 repo 還是私有、提交數還少，立刻清理，建立正確的習慣。

**關鍵設計決策：.gitignore 加入 .env 和 appsettings.json**

C# 專案的慣例是把機密設定放在 `appsettings.json` 或 `.env`，但這兩種檔案都不應該進版控。在 `.gitignore` 加入：

```
appsettings.json
appsettings.*.json
.env
.env.*
!.env.example
```

`!.env.example` 例外規則讓範本檔案可以進 repo，這樣任何人 clone 後都知道需要哪些環境變數。

**關鍵設計決策：.env.example 作為文件而非設定**

新增 `.env.example`，裡面只有變數名稱，沒有實際值：

```
DB_PASSWORD=
DB_CONNECTION_STRING=
WEATHER_API_KEY=
```

這個檔案的用途是「環境設定的目錄」——讓人知道要準備什麼，但沒有洩漏任何機密。

**關鍵設計決策：README 的版本標記更新**

README 的技術棧標記順手從 `.NET 8` 更新成 `.NET 10`，與實際的 `TaiwanAgri.Worker.csproj` 裡 `<TargetFramework>net10.0</TargetFramework>` 保持一致，避免讓閱讀 README 的人對框架版本產生誤解。

**驗收標準**

執行 `git status` 後，`appsettings.json` 和 `.env` 不出現在追蹤清單。`.env.example` 存在且只有變數名稱無實際值。README 裡的所有密碼佔位文字為 `你的密碼`、`你的伺服器`，不含任何明文機密。

---

## PR #003 — WeatherObservations 資料表設計與第一個 Migration

**標題**：`feat: 設計 WeatherObservations Entity 與執行 CreateWeatherObservations Migration`

**背景與動機**

在寫 Worker 之前，先把資料要存到哪裡設計清楚。這個 PR 解決的是「Schema 問題」，而不是「如何拉資料的問題」。把這兩件事分開，是 SA/SD 優先開發哲學的具體體現。

**關鍵設計決策：先打一次真實 API，再設計 Entity**

在設計 Entity 之前，先用 HTTP Client 直接打了一次 `AutoWeatherStationType` API，確認真實的 JSON 回傳結構：

```json
{ "RS": "OK", "Data": [ { "STNO": "...", "CITY": "南投縣", "ObsTime": "...", "D_TX": "18.5", "D_RH": "87", "H_FX": "儀器校驗中" } ] }
```

這個步驟揭露了一個關鍵問題：部分數值欄位在儀器校驗期間會回傳中文字串（例如 `"儀器校驗中"`）而非數字。如果 Entity 設計成 `decimal` 而非 `decimal?`，解析時會在這類資料上拋出例外，Worker 整個崩潰。

因此所有感測器數值欄位（溫度、濕度、雨量、風速）全部設計為 `nullable`（`decimal?`），並在 Mapping 方法中使用 `decimal.TryParse()` 容錯處理，TryParse 失敗時寫入 `null` 而非拋出例外。

**關鍵設計決策：複合索引設計**

`WeatherObservations` 最常見的查詢模式是「依縣市 + 時間範圍」篩選，例如「南投縣過去 72 小時的溫濕度」。因此在 `(CityCode, ObservedAt)` 上建立複合索引，而非只在 `ObservedAt` 上建單一索引。複合索引讓 SQL Server 可以先用 CityCode 縮小範圍，再用時間做範圍查詢，避免全表掃描。

**注意：Migration 命名的一次錯誤**

第一次 Migration 產生了 `WeatherObsevations`（少一個 r），資料表名稱出現錯字。後續補了一個 Migration 把表名改正為 `WeatherObservations`。這個錯誤提醒了一件事：Entity 類別名稱要在第一個 Migration 執行前就仔細確認，改名代價遠高於事前多看一眼。

**驗收標準**

`Update-Database` 執行完成，SQL Server 物件總管可以看到 `WeatherObservations` 資料表與 `IX_WeatherObservations_CityCode_ObservedAt` 複合索引。

---

## PR #004 — WeatherSyncWorker：能打 API + 資料寫入資料庫

**標題**：`feat: 實作 WeatherSyncWorker — BackgroundService + EF Core 寫入`

**背景與動機**

這是整個系統的第一個「真的能動的東西」。Worker 每小時打一次農業部 API，把回傳資料清洗後寫入 `WeatherObservations`。這條路走通之後，後面所有模組的 Sync Worker 都是同一個模式的延伸。

**關鍵設計決策：IServiceScopeFactory 解決 Singleton/Scoped 生命週期衝突**

這是這個 PR 遇到的第一個真實架構問題。`BackgroundService` 預設被 DI 容器以 Singleton 生命週期管理，但 `WeatherDbContext` 是 Scoped。在建構子裡直接注入 DbContext 會在啟動時拋出例外：

```
Cannot consume scoped service 'WeatherDbContext' from singleton 'WeatherSyncWorker'
```

DI 容器這樣設計是有道理的：DbContext 設計上預期每個 Scope 用完就釋放（Change Tracker 清空、Connection 歸還 Pool），如果讓它活在 Singleton 裡，Change Tracker 會持續累積所有被追蹤的 Entity，最終造成記憶體問題或奇怪的資料行為。

解法是不在建構子注入 DbContext，改為注入 `IServiceScopeFactory`，在每次 `SyncWeatherAsync()` 執行時建立一個新的 Scope，從 Scope 取得 DbContext，執行完後 Scope 自動釋放：

```csharp
using var scope = _scopeFactory.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<WeatherDbContext>();
// ... 執行同步邏輯 ...
// scope 的 using 結束時，DbContext 跟著釋放
```

**關鍵設計決策：`ExecuteDeleteAsync` 取代 `RemoveRange` 清除舊資料**

Worker 需要定期清除超過 30 天的舊資料。有兩種做法可以選：第一種是 `RemoveRange`，先把要刪的資料全部載入記憶體，標記為 Deleted，再 `SaveChanges`；第二種是 `ExecuteDeleteAsync`，直接讓 EF Core 在資料庫端產生一條 `DELETE SQL` 執行，完全不載入資料。當舊資料量龐大時，`RemoveRange` 會把幾萬筆資料載入記憶體，是不必要的浪費。`ExecuteDeleteAsync` 直接在 DB 端執行，效率高很多，這裡選用後者。

**驗收標準**

Worker 啟動後，`WeatherObservations` 資料表在一小時內有新資料寫入。Hangfire Dashboard 顯示排程執行記錄，狀態為 Succeeded。

---

## PR #005 — WeatherSyncWorker：修正防重複邏輯 + 處理分頁商業限制

**標題**：`fix: 重寫防重複邏輯（HashSet 取代 latestTime）+ 處理 MOA API 分頁限制`

**背景與動機**

PR #004 的版本能動，但防重複邏輯有根本缺陷，且發現農業部 API 的分頁功能有商業限制，需要正確處理。這個 PR 是在功能能動之後、做程式碼審查時發現問題並修正的。

**關鍵設計決策：用 HashSet 組合比對取代單一 latestTime 比較**

第一版的防重複邏輯是：從 DB 取得最新一筆記錄的 `ObservedAt` 時間，然後只寫入 `ObservedAt` 大於這個時間的資料。這個邏輯有一個根本缺陷：農業部 API 同一批回傳的資料，包含全台幾百個不同測站，但它們的觀測時間是同一個整點（例如全部都是 14:00）。如果 DB 裡已有 14:00 的某個測站資料，`latestTime` 就是 14:00，然後同批其他測站的 14:00 資料也會因為「不大於 latestTime」而被跳過，造成資料遺漏。

修正版改用 HashSet 組合比對：先從 DB 查出所有已存在的 `(StationId, ObservedAt)` 組合，存入 `HashSet<string>`，然後對 API 回傳的每筆資料組成同樣的 Key，先查 HashSet，只有不存在的組合才寫入：

```csharp
var existingKeys = await db.WeatherObservations
    .Select(w => w.StationId + "_" + w.ObservedAt.ToString("yyyyMMddHHmm"))
    .ToHashSetAsync();

var toInsert = dtos
    .Where(d => !existingKeys.Contains(d.STNO + "_" + d.ParsedObsTime))
    .Select(d => MapToEntity(d))
    .ToList();
```

這個方式在任何情況下都能正確過濾，不依賴時間順序假設。

**農業部 API 分頁商業限制的處理**

API 文件說明支援 `Page` 參數分頁，回傳物件包含 `Next: true` 代表還有下一頁，因此在 Worker 裡實作了分頁迴圈。但實際測試發現，當請求第二頁時，API 回傳：

```json
{ "RS": "ERROR", "MSG": "非會員只限回傳第一頁資料" }
```

這是商業鎖定的功能，免費帳號只能取得第一頁（最多 1000 筆）。這不是程式問題，也不是農業部 API 壞掉。

處理決策：分頁迴圈保留，不移除。當 `RS != "OK"` 時，迴圈優雅地 `break`。更重要的是修改 Log 訊息，區分「真正的技術錯誤（Page 1 就失敗）」和「商業限制（Page 2+ 被拒絕）」：

```csharp
if (response?.RS != "OK")
{
    if (page == 1)
        _logger.LogWarning("[WeatherSync] 第一頁 API 回傳異常，請確認服務狀態");
    else
        _logger.LogInformation("[WeatherSync] 第 {Page} 頁無資料或無分頁權限，停止抓取", page);
    break;
}
```

這樣 Log 看起來就清楚了，不會把正常的商業限制誤判為需要調查的錯誤。

**驗收標準**

重複執行 Worker 兩次，第二次執行後 `WeatherObservations` 的資料筆數不增加（防重複生效）。Log 顯示 `Information` 等級的分頁限制訊息，而非 `Warning`。

---

## PR #006 — Identity Migration 提前執行：讓 AspNetUsers 表存在

**標題**：`feat: 提前執行 Identity Migration — 解除 UserId FK 的技術債問題`

**背景與動機**

原始 Sprint 計畫把 Identity 設定排在 W15-16，但在設計 `UserNotifications` 和 `UserFarmProfiles` 的資料表時，發現這個安排製造了一個持續十週的技術債：這些表都需要 `UserId FK → AspNetUsers.Id`，如果 `AspNetUsers` 在 W15 才存在，那 W5-W14 期間這些 FK 欄位都必須設為 `nullable`，到 W15 要改成 `NOT NULL` 時，還需要對已有的歷史資料補值，Migration 會變得複雜。

這個問題的解法成本極低但效果顯著：只需要三行設定，就能讓 `AspNetUsers` 等六個 Identity 資料表提前存在，後續所有 B 類資料表的 FK 從一開始就是正確的 `NOT NULL`。

**關鍵設計決策：把「Migration 存在」和「Login UI 完成」分開**

這兩件事在時間上不需要綁在一起。這個 PR 做的只是：

1. `ApplicationUser` 繼承 `IdentityUser`，加入業務欄位（`DisplayName`、`PreferredCity`、`UserType` 等）
2. `WeatherDbContext` 繼承 `IdentityDbContext<ApplicationUser>`
3. 執行 `Add-Migration IdentitySchema` + `Update-Database`

這個 PR 完成後，`AspNetUsers`、`AspNetRoles`、`AspNetUserRoles` 等六個表在資料庫裡存在，但沒有任何 Login API、沒有 JWT、沒有任何前台登入頁。那些在 W15-16 再做。

這個分拆讓架構從一開始就乾淨：後續新增的 `UserNotifications`、`UserFarmProfiles`、`LostPetReports` 的 Migration，`UserId` 欄位直接定義為 `NOT NULL + FK`，不需要任何補救措施。

**驗收標準**

SQL Server 物件總管中，`TaiwanAgriPlatform` 資料庫出現 `AspNetUsers`（含擴充的業務欄位）、`AspNetRoles`、`AspNetUserRoles`、`AspNetUserClaims`、`AspNetUserLogins`、`AspNetUserTokens` 共六個資料表。

---

## PR #007 — PestAlerts 資料表設計與 Migration

**標題**：`feat: 設計 PestAlerts + PestAlertCities + PestAlertCrops 資料表並執行 Migration`

**背景與動機**

模組 2 的病蟲害警報牆需要把農業部 `PlantEpidemicType` API 的公告資料落地資料庫。原因是 API 資料需要「依縣市 + 作物」的複合篩選，直打 API 無法做到這種聚合查詢，必須先落地才能用 SQL 查詢。

**關鍵設計決策：主表 + 兩張關聯表，取代 JSON 欄位**

一筆病蟲害公告可能涉及多個縣市、多種作物（例如「南投縣、嘉義縣的高麗菜、青椒」）。設計上有兩個選項：一是把縣市列表和作物列表存成 JSON 欄位（`string`），二是建立獨立的關聯表 `PestAlertCities` 和 `PestAlertCrops`。

選擇關聯表而非 JSON 欄位，原因是查詢性質：「找出所有涉及南投縣的警報」這個查詢，在 JSON 欄位上需要 LIKE 或 JSON 函數，效能差且難以建立索引；在關聯表上只需要 `JOIN + WHERE CityName = '南投縣'`，可以在 `CityName` 欄位建立索引，效能好得多。

**關鍵設計決策：SourceHash 防重複的 Schema 設計**

`PlantEpidemicType` API 每日全量回傳所有公告，每次同步都要判斷哪些是新的、哪些是已存在的。用公告內容的核心欄位計算 SHA256 Hash，存入 `SourceHash` 欄位並設為 UNIQUE 索引，是這個問題的標準解法。具體要用哪幾個欄位組合計算 Hash，是 Worker 實作時的業務判斷，在 PR #008 裡詳細說明。Schema 層面只需要確保 `SourceHash` 是 `nvarchar(64)`（SHA256 hex 字串固定 64 字元）並設為 UNIQUE。

**關鍵設計決策：Transaction 確保寫入原子性**

`PestAlerts` 和它的兩張關聯表有順序依賴：必須先存 `PestAlert` 拿到 `Id`，才能存 `PestAlertCity`（FK 指向 `PestAlertId`）和 `PestAlertCrop`。如果 `PestAlert` 存進去了，但解析縣市時程式崩潰，就會有一筆沒有任何縣市記錄的孤立警示。

解法是把三張表的 `Add` 放在同一次 `SaveChangesAsync()` 呼叫中，EF Core 會把它們包進一個隱含的 Transaction，要嘛三張表全部成功，要嘛全部 Rollback，不需要手動 `BeginTransaction()`：

```csharp
db.PestAlerts.Add(alert);
db.PestAlertCities.AddRange(cities);
db.PestAlertCrops.AddRange(crops);
await db.SaveChangesAsync();  // 一個 Transaction，全部成功或全部失敗
```

**驗收標準**

執行 `Update-Database` 後，SQL Server 物件總管出現 `PestAlerts`、`PestAlertCities`、`PestAlertCrops` 三張資料表，FK 關係正確，`PestAlerts.SourceHash` 欄位有 UNIQUE 索引。

---

## PR #008 — PestAlertSyncWorker：病蟲害警報同步 Worker 完整實作

**標題**：`feat: 實作 PestAlertSyncWorker — PlantEpidemicType API 同步 + SHA256 防重複 + Navigation Property 三表寫入`

**背景與動機**

模組 2 的病蟲害警報牆（F03）需要把農業部 `PlantEpidemicType` API 的公告資料持續同步到資料庫。這個 Worker 是 WeatherSyncWorker 模式的第二次應用，但有幾個本質不同的地方：寫入涉及三張有 FK 依賴的資料表、去重邏輯基於 Hash 而非時間戳、更新頻率是每天一次而非每小時。

**關鍵設計決策：SourceHash Key 的業務判斷 — SHA256(PubDate + "|" + Subject)**

PR #007 在 Schema 層面預留了 `SourceHash` 欄位，但具體用哪幾個欄位組合計算 Hash 是業務決策，必須從「什麼叫重複」出發推導。

實際看過 API 資料後，出現了一個複雜的情況：同一則病蟲害事件，有時會被兩個不同單位各自發布，例如桃園市政府發一篇、農業部防檢署再發一篇。Subject（標題）幾乎相同但措辭略有不同，`Issue`（發布單位）欄位不同。

三個候選 Key 方案：

第一個是 `PubDate + Issue + PlantName`。能區分同一天同一單位對不同作物的公告，但「不同單位轉發同一則警示」會被視為兩筆不同資料，農民會看到重複內容。

第二個是 `Subject` 單獨。最簡單，但如果同一件事真的有兩篇不同的公告（例如初報和更詳細的複報），Subject 完全相同時兩筆只有一筆進得去。

第三個是 `PubDate + Subject`。同一天相同標題的公告，不論哪個單位發布，視為同一則資訊。加入 PubDate 是為了讓「不同事件但標題措辭相同」的情況不會互相碰撞。

最終選擇第三個方案，這是一個業務判斷：這個系統是給農民看警示的儀表板，不是政府公文存檔系統。同一天同樣標題的兩篇公告，資訊價值對農民而言完全相同，存兩筆只是噪音。加入 `|` 分隔符防止字串拼接碰撞（例如 `"A" + "B2025"` 和 `"AB" + "2025"` 拼接後相同，加分隔符後就不同了）。

**關鍵設計決策：Insert-Only 而非 Insert+Update**

確定 Hash Key 後，還需要決定「Hash 已存在時怎麼辦」。

最初的設計傾向 Insert+Update：Hash 存在時比對 Body 和 Prescription 的內容，如果有差異就更新資料庫裡的舊資料。這個方案看起來更完整，能跟上公告內容的修改。

但仔細想了一下資料來源的性質：農業部的病蟲害警報是改良場和防檢署發布的官方公告，屬於公文型資料。政府單位不修改已發布的公告——有補充資訊時，他們的做法是發布一篇新公告，而不是修改舊公告。Insert+Update 的假設前提在這個資料來源上根本不成立。

最終採用 Insert-Only：Hash 存在直接跳過，不存在才寫入。程式碼更簡單，DB 查詢也少一次（不需要拉完整實體來比對欄位，只需要查 SourceHash 是否存在），也更符合這個資料來源的實際行為：

```csharp
var existingHashes = (await db.PestAlerts
    .Where(p => targetHashes.Contains(p.SourceHash))
    .Select(p => p.SourceHash)
    .ToListAsync(stoppingToken))
    .ToHashSet();

var newPestAlerts = incoming
    .Where(p => !existingHashes.Contains(p.SourceHash))
    .ToList();
```

**關鍵設計決策：Navigation Property 讓 EF Core 自動管理 FK**

MapToEntity 方法在回傳 PestAlert 物件時，同時建立 Cities 和 Crops 清單，透過 navigation property 附加在同一個物件上：

```csharp
return new PestAlert
{
    Subject = dto.Subject,
    // ...其他欄位...
    Cities = dto.City.Split(',')
               .Select(c => new PestAlertCity { CityName = c.Trim() })
               .ToList(),
    Crops = string.IsNullOrWhiteSpace(dto.PlantName)
              ? new List<PestAlertCrop>()
              : dto.PlantName.Split(',')
                   .Select(p => new PestAlertCrop { CropName = p.Trim() })
                   .ToList()
};
```

EF Core 的 Change Tracker 在 `SaveChangesAsync()` 時自動：先 INSERT PestAlert 拿到 Id，再把 Id 填入所有 PestAlertCity.AlertId 和 PestAlertCrop.AlertId，最後 INSERT 關聯表，全部包在一個 Transaction 裡。不需要手動管 FK，也不需要分多次 SaveChanges。

空作物的處理：部分公告的 PlantName 是空字串（代表全作物適用），這種情況給空 List，EF Core 不寫入任何 PestAlertCrop 資料列，保留語意——「沒有 Crop 記錄」代表「全作物適用」，前端查詢時需要同時撈有對應作物的警示和沒有任何作物記錄的警示。

**關鍵設計決策：導入 Polly Retry 機制**

農業部 API 是外部服務，偶發的網路抖動或 timeout 會導致整批資料同步失敗。在 HttpClient 上加入 Polly 的 `WaitAndRetryAsync(3, _ => TimeSpan.FromSeconds(2))`，遇到網路錯誤或 5xx 自動等待 2 秒重試，最多 3 次。這個設定在 Program.cs 的 `AddTransientHttpErrorPolicy` 統一配置，覆蓋所有使用 "MoaApi" 命名 HttpClient 的 Worker，不需要在各個 Worker 內部自己處理重試邏輯。

**遇到的一個 Silent Failure Bug**

MapToEntity 裡的日期解析格式字串寫成 `"yyyy/MM/dd"`，但 PlantEpidemicType API 的實際回傳是 `"2025-12-26"`（dash 連接而非斜線）。TryParseExact 失敗時不拋例外，只是靜靜地回傳 null，導致 incoming 清單是空的，Log 顯示「無新資料需要同步」，看起來像正常運作但一筆都沒有存進資料庫。

這個 bug 再次確認了一個原則：每次對接新的 API，必須親自看一次真實的 JSON 回傳格式，不能依賴記憶或假設「和上一個 API 一樣」。修正是把格式字串改為 `"yyyy-MM-dd"`。

**驗收標準**

Worker 啟動後，Console Log 顯示 `[PestAlertSync] 成功抓取第 1 頁，共 N 筆資料`，接著顯示實際新增筆數（首次同步應有數十筆）。SQL Server 物件總管中，`PestAlerts`、`PestAlertCities`、`PestAlertCrops` 三張表均有資料，FK 關係正確。重複執行 Worker 後 Log 顯示「無新資料」，資料筆數不變。

---

## PR #009 — RainfallStation + RainfallObservation Entity 設計與 Migration

**標題**：`feat(rainfall): 設計雨量站台與觀測資料 Entity，執行 AddRainfallTables Migration`

**背景與動機**

W5-6 上半的第二條資料線：雨量觀測。農業部雨量 API（`AutoRainfallStationType`）每 10 分鐘更新一次，每筆資料包含站台識別、觀測時間、多個時間窗口的累積雨量，以及站台的地理座標。

這個 PR 比 WeatherObservation 複雜的地方在於：雨量系統有兩個邏輯上獨立的「實體」——雨量站台（靜態主檔）和雨量觀測值（動態快照）。這個 PR 的核心任務是把這兩者的邊界劃清楚，並設計對應的 Entity 和索引。

**關鍵設計決策一：正規化，而非反正規化**

`WeatherObservation` 採用反正規化設計——每筆觀測資料直接存站名、縣市等重複性資訊，不另開站台主檔。這個設計在氣象觀測的情境下是刻意的選擇：高頻寫入優先，避免 JOIN 開銷。

`RainfallStation` 採用不同策略：獨立一張站台主檔表，`RainfallObservation` 透過 `StationId` 做邏輯上的 FK 關聯。做出這個不同選擇的原因是：雨量站台是一份被農業部持續維護的清單，站台可以新增、修改、下架，需要 `IsActive`、`CreatedAt`、`UpdatedAt` 這些主檔管理欄位。兩種設計沒有對錯，而是對應不同的資料生命週期：快照型資料反正規化效能好，主檔型資料正規化維護性好。

**關鍵設計決策二：`Station_ID` 用 `int` PK，不用 `string` PK**

農業部的 Station_ID 是字串格式（如 `"C0W110"`、`"467990"`），一開始的直覺是直接把字串當 PK。後來否決了這個方案，原因有兩個：

第一，FK 效能。`RainfallObservation` 有一個對應 `RainfallStations.StationId` 的 FK，規則引擎在查詢「某縣市最近雨量」時會頻繁 JOIN 這兩張表。`int` 是 4 bytes 的固定長度，比對極快；字串 FK 的 B-Tree 索引更大，掃描範圍更廣。

第二，「空間換效能」的條件在這裡不成立。`RainfallObservation` 只需要存 `StationId`（int）這一個欄位，就能 JOIN 拿到所有站台資訊，不需要把縣市、站名複製進觀測表。JOIN 的成本幾乎是零，沒有必要用冗餘來換掉它。

最終設計：`RainfallStation.Id`（int，PK，Identity）+ `RainfallStation.StationId`（string，Unique Index）。

**關鍵設計決策三：ELEV 住在 `RainfallStation`，雖然站台清單 API 沒有提供**

站台清單 API（`TaiwanRainfallStationInformationType`）的 schema 沒有 LAT、LON、ELEV，但觀測資料 API 每筆都有這三個欄位。

選項 A（把 ELEV 存在觀測表）被否決的理由是：海拔是站台的固定地理屬性，不是某個時間點的測量值。把同樣的 `"837"` 公尺值重複存在這個站台的幾萬筆觀測資料裡，是語意上的錯誤，也是不必要的冗餘。

最終選擇選項 C：`RainfallStation` 保留 `Latitude`、`Longitude`、`Elevation` 欄位（初始值為 null），由 `RainfallSyncWorker` 同步觀測資料時順帶 Upsert 進去。第一次 `RainfallSyncWorker` 跑完後，這三個欄位就有值，之後幾乎不再變動。

**驗收標準**

`Update-Database` 執行後，SQL Server 物件總管可以看到 `RainfallStations`（含 `IX_RainfallStations_StationId` Unique Index）和 `RainfallObservations`（含 `IX_RainfallObservations_StationId_ObservedAt` Unique Index）兩張表，欄位型別與 Entity 定義一致。

---

## PR #010 — RainfallStationSyncWorker + RainfallSyncWorker 實作

**標題**：`feat(rainfall): 實作雨量站台與觀測資料 Sync Worker`

**背景與動機**

雨量模組需要兩個協作的 BackgroundService：`RainfallStationSyncWorker` 維護站台主檔（7 天一次 Upsert），`RainfallSyncWorker` 寫入觀測快照（10 分鐘一次，並順帶更新站台座標）。這個 PR 同時修正了 `WeatherSyncWorker` 和 `PestAlertSyncWorker` 裡的 `DateTime.Now` → `DateTime.UtcNow` 問題，以及 `PestAlertSyncWorker` 缺少 `incoming.Count == 0` 防護的 bug。

**關鍵設計決策一：RainfallStationSyncWorker 的 Upsert 策略（三種情況）**

`RainfallStationSyncWorker` 的核心挑戰是 Upsert 邏輯，需要同時處理三種情況：新增新站台、更新已有站台的欄位、軟刪除不再出現在 API 的站台。

原本考慮直接用 `ATTRIBUTE` 欄位的值來判斷軟刪除，但看了幾百筆資料之後發現這個欄位全部都是空字串，農業部根本沒有用它來標記停用狀態。正確的觸發條件是：「這次 API 回傳的清單裡沒有它」。

實作上把 API 回傳的所有 `StationId` 存成 `HashSet`，把 DB 裡的站台用字典存（key = StationId），然後分三步走：`TryGetValue` 找到就更新欄位，找不到就 `db.RainfallStations.Add()`，最後用 `foreach (var existing in existingStations.Values) if (!apiStationIds.Contains(...))` 找出要軟刪除的站台。

選擇 `TryGetValue` 而不是 `ContainsKey + [key]`，是因為 `ContainsKey` 只回答「有沒有」，取值還得再查一次字典，等於兩次查詢。`TryGetValue` 一次完成「確認存在並交出值」，是字典操作的正確慣用寫法。

**關鍵設計決策二：RainfallSyncWorker 的 Polling 啟動模式**

`RainfallSyncWorker` 依賴 `RainfallStations` 表已有資料（需要把觀測資料的 StationId 對應到站台記錄，順帶更新座標）。`AddHostedService` 只保證 `ExecuteAsync` 被呼叫，不保證其他 Worker 的「第一次執行」已完成。

在主同步迴圈開始前加入 Polling：每 30 秒查一次 `RainfallStations.CountAsync()`，大於 0 才繼續。這個 check 只在啟動時執行幾次，一旦站台資料就緒就不再執行，成本極低。這不是臨時 workaround，而是對「我依賴這份前置資料」這個事實的顯式表達。

**關鍵設計決策三：`SaveChangesAsync` 的位置必須涵蓋所有變更，不能和 `newObservations.Count` 綁在一起**

這是這個 PR 裡修正的最隱蔽的 bug。最初的寫法是：`if (newObservations.Count == 0) return;`，讓沒有新觀測資料的情況提前離開，看起來合理——沒有東西可寫，省掉 `SaveChangesAsync` 的開銷。

但這個 return 同時讓站台座標更新（`station.Latitude = ...` 這些對 `RainfallStation` 的修改）永遠不被存回 DB。`RainfallStation` 的 LAT/LON/ELEV 雖然在 Change Tracker 裡被標記為修改，但 `SaveChangesAsync` 沒執行，修改消失了。每次跑完站台座標還是 null。

正確的設計是：`SaveChangesAsync` 永遠執行，放在所有操作的最後，不管有沒有新觀測資料。需要條件判斷的只有 `AddRangeAsync`，不是 `SaveChangesAsync`。

**關鍵設計決策四：決定不抽共用 Helper（Rule of Three 的反向應用）**

完成後把 `RainfallSyncWorker` 和 `WeatherSyncWorker` 並排比較，有 7 成的流程相同（分頁抓取、MapToEntity、去重、寫入）。直覺告訴我應該抽共用。

最後沒有抽，原因是：如果要把這兩個 Worker 共同的部分抽成一個泛型 helper，需要用泛型參數、委派或 callback 來容納所有差異（不同的 DTO 型別、Entity 型別、DB 查詢、MapToEntity 實作，以及 `RainfallSyncWorker` 獨有的站台座標更新邏輯）。想像這個 helper 的方法簽名，它會比現在各自獨立的程式碼更難讀，不是更簡單。Rule of Three 的精神是「重複三次才抽，而且抽出來必須更簡單」。這裡只有兩個 Worker，而且業務細節差異夠大，抽的成本高於重複的成本。

**驗收標準**

`RainfallStationSyncWorker` 啟動後，Log 顯示抓取到站台資料並 Upsert 完成，`RainfallStations` 表有資料。接著 `RainfallSyncWorker` 的 Polling log 出現後消失（代表等到了站台資料），然後顯示觀測資料寫入成功。確認 `RainfallStations.Latitude`、`Longitude`、`Elevation` 不再是 null。重複執行後 Log 顯示「略過 N 筆重複」，不重複寫入。

---
 
## PR #011 — PestDecadeSummary Entity 設計、Migration 與 PestDecadeSyncWorker 實作
 
**標題**：`feat(pest-decade): 新增蔬果病蟲害旬報資料表與同步 Worker`
 
**背景與動機**
 
W5-6 上半的第三條資料線：蔬果病蟲害旬報。農業部 `FruitVegetalePestControlType` API 記錄各縣市鄉鎮每旬（上、中、下旬）的主要害蟲發生情形，是 F04 旬報查詢功能的資料來源。這個 PR 完成 W5-6 上半最後一個遺漏的 Sync Worker，讓資料收集層徹底收尾。
 
**關鍵設計決策一：不拆表，PestName、City、Town 直接存在主表**
 
設計 Entity 時，第一直覺是把 `PestName`（害蟲名稱）、`City`（縣市）、`Town`（鄉鎮）各自拆出獨立的主檔表，理由是「這些值會重複出現，重複就應該正規化」。後來從查詢模式出發重新評估，否決了這個方向。
 
F04 的主要查詢模式是「給我看某縣市這個月所有蟲害的狀況」，對應的 SQL 是：
 
```sql
WHERE City = '宜蘭縣' AND Year = 2025 AND Month = 10
```
 
這個查詢完全不需要 JOIN 任何主檔表，`PestName` 直接從主表讀出來就夠了。正規化的動機是「讓查詢更有效率」或「消除更新異常」，但這裡兩個條件都不成立：查詢不需要 JOIN，旬報資料是歷史快照不會被更新。拆表只是增加 JOIN 的複雜度，不帶來任何實質好處。
 
`PestName` 是 Unique Index 的組成部分，它是主鍵的一部分而非依賴某個非鍵欄位——這不是 3NF 問題。重複出現不等於違反正規化，判斷要看欄位在鍵中的角色。
 
**關鍵設計決策二：Unique Index 的組合——六個欄位共同定義唯一性**
 
看完真實 API 資料後發現一個問題：同樣的欄位值組合（`PestName + City + Town + Year + Month + TenDays`）在同一批回傳資料裡重複出現多次，且每筆的所有欄位值完全相同，`Average` 和 `Proportion_Island` 也都是空字串，沒有任何欄位能區分這些重複筆。
 
這是農業部 API 的資料品質問題，不是程式設計錯誤。處理策略是：把這六個欄位組合定為 Unique Index，API 的重複筆在 `DistinctBy` 階段就被消除，只保留其中一筆寫入 DB。
 
**關鍵設計決策三：`Average` 和 `Proportion_Island` 在 DTO 用 string 接**
 
API 文件標註這兩個欄位型別為 `number`，但實際回傳是空字串 `""`。如果 DTO 用 `decimal?` 接，JSON 反序列化器遇到空字串會拋 `JsonException`，不是靜默回傳 null。
 
正確做法是 DTO 忠實反映 API 的實際格式（`string`），MapToEntity 裡用 `decimal.TryParse()` 嘗試轉換，失敗就給 `null`，Entity 欄位設為 `decimal?`。這個分層處理的模式和 `RainfallSyncWorker` 的 nullable decimal 欄位處理完全一致。
 
**關鍵設計決策四：`HasPrecision(10, 2)` 防止截斷 Warning**
 
第一次跑 `Add-Migration` 時，EF Core 對 `Average` 和 `ProportionIsland` 兩個 `decimal?` 欄位各發出一個 Warning：
 
```
No store type was specified for the decimal property. This will cause values
to be silently truncated if they do not fit in the default precision and scale.
```
 
EF Core 說的「靜默截斷」是指：如果實際值有超過預設精度的小數位，DB 會直接切掉多餘的位數，不拋例外。在 `OnModelCreating` 加入 `HasPrecision(10, 2)` 明確告訴 SQL Server 要用幾位整數、幾位小數，消除不確定性。加完之後 `Remove-Migration` 重來，Warning 消失後才執行 `Update-Database`。
 
**關鍵設計決策五：`incoming` 層面的 `DistinctBy` 比只靠 DB HashSet 更乾淨**
 
去重邏輯的第一版只依賴「從 DB 撈出已存在的 Key，過濾掉 incoming 裡重複的」。後來改成先在 `incoming` 層面用 `DistinctBy` 去掉 API 回傳的重複筆，再去比對 DB：
 
```csharp
var incoming = allDtos
    .Select(MapToEntity)
    .DistinctBy(e => new { e.PestName, e.Year, e.Month, e.TenDays, e.City, e.Town })
    .ToList();
```
 
這樣做的好處是：記憶體裡的資料在進入資料庫比對之前就已經是乾淨的，不把 API 的資料品質問題帶進後面的流程。DB HashSet 比對負責的是「跟歷史資料去重」，`DistinctBy` 負責的是「跟本批次資料去重」，兩層職責清晰。
 
**關鍵設計決策六：API 文件遺漏 `Month` 欄位**
 
農業部的 API 文件和 Try-it-out 範例都沒有列出 `Month` 欄位，但實際呼叫 API 的回傳資料裡有 `"Month": "10"`。這個欄位是 Unique Index 的必要組成——沒有它，無法區分同一年不同月份的旬報。
 
DTO 補上 `Month` 欄位，並加 `[JsonPropertyName("Month")]`。這個發現再次確認：API 文件是參考，真實回傳才是設計依據，每次接新 API 一定要實際打一次看資料。
 
**發現死碼，追查根因，修正上游設計**
 
`incoming` 後面加了 `.Where(e => e != null)` 之後，發現這是死碼——`MapToEntity` 的回傳型別是非 nullable，編譯器保證它永遠不回傳 null，這個 check 永遠不會過濾掉任何東西。
 
一開始的直覺是直接刪掉。但先問了一句：「為什麼 `MapToEntity` 沒有失敗路徑？它應該有嗎？」這一問讓真正的問題浮出來。
 
`MapToEntity` 裡的 `ParseInt` 在解析失敗時回傳 `0`，而不是 null 或例外：
 
```csharp
// 原本的寫法：解析失敗靜默回傳 0
private static int ParseInt(string s)
    => int.TryParse(s, out var v) ? v : 0;
```
 
`Year`、`Month`、`TenDays` 任何一個欄位解析失敗，都會寫進 Year=0、Month=0 這樣的無意義資料，安靜地進資料庫，通過 Unique Index，程式繼續跑，Log 不報警。這是靜默錯誤，比拋例外更難察覺。
 
修正方式是把 `MapToEntity` 改為回傳 `PestDecadeSummary?`，在方法開頭用衛語句擋住解析失敗的情況：
 
```csharp
// 修正後：解析失敗明確回傳 null，跳過這筆資料
private PestDecadeSummary? MapToEntity(PestDecadeSummaryDto dto)
{
    if (!int.TryParse(dto.Year, out var year)) return null;
    if (!int.TryParse(dto.Month, out var month)) return null;
    if (!int.TryParse(dto.Decade, out var tenDays)) return null;
    return new PestDecadeSummary { Year = year, Month = month, TenDays = tenDays, ... };
}
```
 
`MapToEntity` 現在有了真正的失敗路徑，`.Where(e => e != null)` 也從死碼變回了有意義的防禦。整個修正的邏輯是：死碼是症狀 → 追查根因 → 發現是 ParseInt 設計缺陷 → 修正上游 → 防禦鏈閉合。

---


## PR #012 — PestRuleConfig + UserNotifications 資料表設計、規則引擎與 Worker

**標題**：`feat(pest-rule): 實作 PestRuleConfig + UserNotifications 資料表設計、PestRuleEngine 規則引擎與 PestRuleEngineWorker`

**背景與動機**

W5-6 下半的核心目標是讓系統從「被動收集資料」升級成「主動提醒使用者」。上半建好了三條資料收集線（氣象、雨量、旬報），下半要在這些資料上蓋一層規則評估引擎：使用者設定條件，引擎定期跑、對資料做判斷、把符合條件的事件寫入通知記錄，前台讀取通知記錄。這個 PR 完成了這條流程的資料層和邏輯層。

**關鍵設計決策一：跨 DbContext 不建物理 FK，UserId 純字串邏輯 FK**

`PestRuleConfig` 和 `UserNotifications` 都需要 `UserId FK → AspNetUsers`，但 `AspNetUsers` 在 `ApplicationDbContext`（Web 專案）管理，`PestRuleConfig` 在 `WeatherDbContext`（`Modules.Weather` 專案）管理，兩個不同的 DbContext。

EF Core 的 FK 關係需要雙方都在同一個 DbContext 裡，跨 DbContext 建物理 FK 有兩條路：一是手動在資料庫補 FK constraint，但這讓程式碼和 Migration 不再是唯一真相來源，下次跑 Migration 可能衝突；二是強行在 `WeatherDbContext` 加入 `ApplicationUser` 的 `DbSet`，會讓模組依賴關係混亂。

最終選擇：`UserId` 只是一個純字串欄位，`OnModelCreating` 裡不宣告 `HasForeignKey`，資料庫層級沒有 FK constraint，應用程式層負責保證寫入的 `UserId` 是真實存在的使用者。已知的代價是：使用者帳號被刪除後，對應的規則和通知記錄會成為孤兒（orphan records），這個清理邏輯留待 W15-16 實作登入功能時補上。

**關鍵設計決策二：移除導覽屬性，避免 EF Core 跨 DbContext 多建表**

`PestRuleConfig` 和 `UserNotification` 初始設計裡有 `public ApplicationUser User { get; set; }` 這個導覽屬性。第一次 `Add-Migration` 時，Migration 的 `Up()` 裡出現了一個多餘的 `CreateTable("ApplicationUser", ...)`——EF Core 看到導覽屬性就認為自己要管這個 Entity，自動在 `WeatherDbContext` 的管轄範圍裡建了一張全新的 `ApplicationUser` 表，跟 `ApplicationDbContext` 管理的 `AspNetUsers` 完全沒有關聯。

解法是 `Remove-Migration`，從 `PestRuleConfig.cs` 和 `UserNotification.cs` 移除 `ApplicationUser User` 導覽屬性，再重新 `Add-Migration`。沒有導覽屬性，EF Core 就不認為自己要管 `ApplicationUser`，Migration 才是乾淨的。

原則是：導覽屬性是 EF Core 管理關聯的入口，有導覽屬性就等於宣告「我的 DbContext 要管這張表」。跨 DbContext 的關聯只能存在於值層面（字串欄位），不能存在於物件層面（導覽屬性）。

**關鍵設計決策三：PestRuleEngine 抽成普通 Service，由 Worker 定時呼叫**

最初考慮讓 `PestRuleEngine` 直接繼承 `BackgroundService`，把排程和邏輯都放在同一個類別裡，跟其他 SyncWorker 的模式一致。最終選擇了另一種結構：

```
PestRuleEngineWorker（BackgroundService，只管排程）
    └→ 注入 PestRuleEngine（普通 Service，只管邏輯）
           └→ EvaluateAsync() 在這裡
```

選擇這個結構的原因是可呼叫性：如果未來想讓管理員透過 API endpoint 手動觸發一次規則評估，直接注入 `PestRuleEngine` 呼叫 `EvaluateAsync()` 就好；如果 Engine 是 `BackgroundService`，外部根本沒辦法呼叫它的方法，只能等排程時間到。

`PestRuleEngine` 以 `AddSingleton<PestRuleEngine>()` 註冊，這樣它和持有它的 `PestRuleEngineWorker`（Singleton 生命週期）的生命週期一致，不會出現 Scoped 被 Singleton 持有的問題。Engine 內部用 `IServiceScopeFactory` 每次 `EvaluateAsync` 執行時動態建立 Scope 取得 DbContext，用完釋放。

**關鍵設計決策四：通知去重需要 SourceRecordId**

最初 `UserNotifications` 只有 `PestRuleConfigId`，邏輯是「這條規則的通知已存在就跳過」。這個設計有一個根本缺陷：同一條規則在不同時間可能觸發多次——例如 1/1 南投縣榕小蜂警報（`PestAlert Id=42`）寫了一次通知，1/9 又有一筆新的南投縣榕小蜂警報（`PestAlert Id=55`），如果只用 `PestRuleConfigId` 去重，Id=55 的新公告就會被誤判為「已通知過」跳過。

補充 `SourceRecordId int?` 欄位解決這個問題：引擎對每一筆符合條件的來源記錄，去查 `UserNotifications` 有沒有 `PestRuleConfigId == rule.Id AND SourceRecordId == item.Id` 的記錄，有才跳過，沒有才寫新通知。這樣每一筆獨立的來源記錄都能對應到自己的通知，不會混淆。數值型（PestDecade）也用同一套機制，`SourceRecordId` 存 `PestDecadeSummary.Id`。

**關鍵設計決策五：到期通知硬刪除，Event 型通知靠 ExpiryDays 控制生命週期**

`EvaluateAsync` 的第一步是清除 `ExpireAt < DateTime.UtcNow` 的通知，直接 `RemoveRange` 硬刪除。選擇硬刪除而非軟刪除的原因是：過期通知對使用者沒有保留價值，軟刪除只是增加查詢時需要過濾的雜訊，沒有好處。

事件型通知的設計原則是「發一次，讓它在通知列表裡存活 `ExpiryDays` 天，到期消失」。前台呈現用常駐 UI（紅點/鈴鐺）而非重複推播，使用者看完點掉（`IsRead = true`），通知繼續存在直到 `ExpiryDays` 到期自動清除。重複推播同一件事是騷擾，常駐顯示才是正確的持續事件提醒方式。

**EvaluateAsync 完整流程**

```
1. 硬刪除 ExpireAt < now 的通知
2. 撈出所有 IsActive = true 的 PestRuleConfig
3. foreach rule:
   a. switch(RuleType):
      "Numeric" → 衛語句擋 null Threshold
                → WHERE PestDecadeSummaries.Average > Threshold.Value
                → foreach item: AnyAsync(PestRuleConfigId + SourceRecordId) 去重 → 寫通知
      "Event"   → switch(SourceTable):
                  "PlantEpidemic" → 衛語句擋 null FilterJson → Deserialize<PestRuleFilter>
                                  → WHERE Cities.Any(city) && Crops.Any(crop)
                                  → foreach item: AnyAsync 去重 → 寫通知
                  "TreePest"      → LogWarning 尚未實作
   SaveChangesAsync 在每條規則的迴圈外執行
```

**驗收標準**

0 錯誤（21 個 nullable 警告不影響執行）。SQL Server 物件總管中 `PestRuleConfigs` 和 `UserNotifications` 兩張表存在，欄位與 Migration 定義一致，`SourceRecordId` 欄位在 `UserNotifications` 存在。程式啟動後 Log 顯示 `[PestRuleEngineWorker]` 開始執行但因無規則資料直接結束，不拋任何例外。

---

## PR #013 — Market 模組基礎建立：MarketRestDay Entity、DbContext、Migration 與 SyncWorker
 
**標題**：`feat(market): 建立 Market 模組基礎 — MarketRestDay Entity、MarketDbContext、Migration 與 MarketRestDaySyncWorker`
 
**背景與動機**
 
W7-8 正式進入 Market 模組開發。整個模組有四支 API：`AgriProductsTrans`（農產品交易行情）、`PorkTrans`（豬肉交易行情）、`DebrisAlert`（土石流警戒）、`MarketRestDay`（休市日）。開發前先分析四支 API 之間的依賴關係，確認 `MarketRestDay` 必須先做：交易行情 API 在休市日仍會回傳資料，但 `CropName = "休市"`、價格全為 0，若 Worker 直接略過休市判斷，這些 0 值就會混入資料庫，污染後續的均價計算和走勢分析。`MarketRestDay` 是 `AgriProductsTrans` 和 `PorkTrans` 的前置參考資料，決定「哪些天不需要同步」的依據。
 
這個 PR 完成了 Market 模組的基礎建設：新模組的 DbContext 建立、第一張業務資料表的 Entity 設計、Migration 跑通，以及 SyncWorker 完整實作並驗收通過（32,149 筆）。
 
**關鍵設計決策一：AgriProductsTrans 遇到休市筆用 continue 跳過，不存 0 值**
 
農業部的 `AgriProductsTrans` API 在休市日並不回傳空陣列，而是回傳 `CropName = "休市"`、`Upper_Price = 0`、`Middle_Price = 0`、`Lower_Price = 0`、`Avg_Price = 0`、`Trans_Quantity = 0` 的記錄。這帶出一個設計選擇：
 
選項 A 是把這些休市筆原樣存進 `AgriProductsTrans` 表。好處是資料完整，壞處是當系統要做「災害發生後農產品價格波動分析」時，0 值會被計入均價，分析結果失真。
 
選項 B 是 `AgriProductsTrans` 只存真實交易資料（`CropName == "休市"` 時 `continue` 跳過），另外靠 `MarketRestDay` 記錄休市資訊，前台走勢圖查不到資料的那天去 `MarketRestDay` 確認「這天是休市」，在圖上標注「休市」而非顯示斷點。
 
選擇選項 B 的理由是資料純度：`AgriProductsTrans` 存的是農產品交易的業務資料，休市標記是另一個維度的資訊，兩者混在同一張表會讓查詢者永遠需要先過濾 `CropName != "休市"`，這個認知負擔不應該轉嫁給查詢端。
 
**關鍵設計決策二：MarketRestDay 的資料性質判斷——快照型，不是主檔型**
 
`MarketRestDay` 看起來像「固定清單」（台灣農產市場的休市日曆），但它實際的資料性質是快照型：每年的休市日曆不同，舊年份的資料不會變動，新年份的資料會在年初後陸續補入，資料量隨時間持續累積，符合快照型的定義。這個判斷決定了 Entity 設計上不需要 `IsActive` / `UpdatedAt` 欄位（那是主檔型才需要的），也決定了去重策略應該以「這一筆日期記錄已存在嗎」而非「這個市場的資料已存在嗎」為單位。
 
**關鍵設計決策三：五層巢狀 JSON 攤平到關聯式資料庫**
 
農業部的 `MarketRestDay` API 回傳五層巢狀結構：`市場 → 交易類型 → 年 → 月 → 休市日字串`。關聯式資料庫只接受平坦的「一列一筆」結構，需要在 SyncWorker 裡用四層 `foreach` 走訪，在最內層對 `Rest` 字串（格式為 `"05、08、12、19、22、26"`）做 `Split('、')`，每個拆出來的日期組成一筆獨立的 `MarketRestDay` Entity。
 
攤平的原則是：API 的巢狀設計是為了減少傳輸重複欄位（例如 `MarketCode` 不需要重複出現在每一個月份），但資料庫需要的是每一筆記錄都自我完備，能獨立表達完整語義。攤平後一筆記錄長這樣：`MarketCode = "104"、MarketName = "台北二"、MarketType = "F"、Year = 115、Month = 1、RestDay = 5`。
 
**關鍵設計決策四：多 DbContext 專案的 Migration 指令需要明確指定 -Context 和 -Project**
 
這是 Market 模組的第一次 Migration，也是整個專案第一次同時存在兩個 DbContext（`WeatherDbContext` 和 `MarketDbContext`）。此時如果直接下 `Add-Migration`，EF Core 不知道要針對哪個 DbContext 操作，需要明確指定兩個額外參數：
 
```
Add-Migration AddMarketRestDayEntity -Context MarketDbContext -Project TaiwanAgri.Modules.Market
```
 
`-Context` 指定操作對象，`-Project` 指定 Migration 檔案要產生在哪個專案資料夾。同樣地，`Update-Database` 也需要加 `-Context MarketDbContext`，確保 Migration 套用到正確的 DbContext 管轄範圍。
 
**關鍵設計決策五：Modular Monolith 的組裝責任在入口層**
 
`MarketDbContext` 定義在 `TaiwanAgri.Modules.Market`，但連線字串的設定和 Worker 的啟動都在 `TaiwanAgri.Worker` 的 `Program.cs` 裡完成。模組本身只負責「我管哪些表、我的業務邏輯長什麼樣」，不知道也不需要知道「我要連哪個資料庫、什麼時候啟動」。連線字串屬於執行環境的設定，組裝和啟動屬於入口層的責任，這是 Modular Monolith 的核心邊界原則。
 
**SyncWorker 完整流程**
 
```
1. 分頁抓取 MarketRestDay API（Next = false 時停止，加設 20 頁上限保護）
2. 收集所有 MarketRestDayDto 到 allDtos
3. 四層 foreach 走訪 allDtos 的巢狀結構
4. 最內層 Split('、') 拆日期字串，int.TryParse 防禦性解析
5. 組出 MarketRestDay Entity 收集到 entities
6. 從資料庫撈出已存在的自然鍵組合到 HashSet
7. Where !HashSet.Contains 過濾出 toInsert
8. AddRange + SaveChangesAsync 寫入
9. 每 7 天執行一次（休市日曆預告制，一年更新一次，週同步綽綽有餘）
```
 
**驗收標準**
 
編譯 0 錯誤。`MarketRestDays` 表在 SQL Server 物件總管中存在，欄位與 Migration 定義一致。程式啟動後 Log 顯示 `[MarketRestDaySync] 新增 32149 筆休市日資料`，不拋任何例外。第二次執行顯示 `[MarketRestDaySync] 無新資料需要同步`，確認去重邏輯正確運作。
 
---

## 閱讀之後：給你的觀察指南

讀完PR_DESCRIPTION，你會發現每一篇都有固定的段落結構：

**背景與動機**回答「為什麼要做這件事」，而不是「我做了什麼」。一個 PR 如果只說做了什麼，六個月後你自己都不知道當初為什麼這樣決定。

**關鍵設計決策**是最有價值的部分。每一個決策都有「有哪些選項」和「為什麼選這個而不選那個」。這才是工程思維的展示，不是「我新增了一個類別叫 WeatherSyncWorker」。

**驗收標準**讓讀 PR 的人（包括 code reviewer 或面試官）知道怎麼確認這個 PR 是真的可以動的，而不是只是程式碼看起來對。

你可以注意一下，哪些部分是你現在讀了覺得「對，我確實做了這個決定，我知道為什麼」，哪些是「我有做，但當時沒有意識到這是個決策」。後者就是你在未來開發中，紙筆推導最需要捕捉的東西。
