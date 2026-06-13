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
 
## PR #014 — Market 模組 Schema 分離、MarketInfo 重構與 CropMarketSyncWorker
 
**標題**：`feat(market): MarketInfo Entity 重構、全模組 Schema 分離與 CropMarketSyncWorker 實作`
 
**背景與動機**
 
這個 PR 是 Market 模組開發的第二階段，處理 CropMarketSyncWorker 的前置工作——在實作 AgriProductsTrans 的日常行情同步之前，必須先有一份正確的市場清單，AgriProductsTransSyncWorker 才能知道要對哪些市場打 API。
 
這個 PR 完成了四件事：一、將整個專案的資料庫命名空間從扁平的 dbo 重構為以模組為單位的 Schema（market / weather），讓資料庫結構能對應 Modular Monolith 的架構語意；二、重構 MarketInfo Entity，因為真實 API 資料揭示了 MarketCode 不能單獨作為 PK 的事實；三、配合 Entity 重構，調整 AgriProductsTrans 與 MarketInfo 的關聯方式；四、實作 CropMarketSyncWorker，將市場清單同步進 MarketInfos 表，為後續的交易資料同步奠定基礎。
 
**關鍵設計決策一：用 Schema 取代前綴字，讓資料庫結構對應模組邊界**
 
最初考慮在資料表名稱加前綴（MKT_AgriProductsTrans、WEA_WeatherObservations），讓視覺上可以辨識哪個表屬於哪個模組。最終選擇了 SQL Server 的 Schema 機制：
 
```sql
market.AgriProductsTrans
weather.WeatherObservations
```
 
選 Schema 而非前綴的理由：前綴是「用命名解決架構問題」的補丁，Schema 才是資料庫層真正支援的命名空間。在 EF Core 的 `OnModelCreating` 裡只需要一行 `entity.ToTable("TableName", schema: "market")`，Migration 會自動產生 `EnsureSchema` 並把表建在正確的 Schema 下。SQL Server Management Studio 裡也會自動按 Schema 分群顯示，不需要前綴就能一眼看出模組歸屬。
 
這個改動需要重建全部 Migration——兩個模組的 Migrations 資料夾清空，刪除資料庫，重新 `Add-Migration InitialCreate` + `Update-Database`。因為都在開發階段、資料可以重跑 Worker 補回，選擇「重建」而非「補丁 Migration」，確保歷史 Migration 乾淨，不留 `ALTER SCHEMA TRANSFER` 的痕跡。
 
**關鍵設計決策二：MarketInfo 的 PK 必須從 MarketCode 改成 surrogate Id**
 
原始設計是以 MarketCode（string）作為 MarketInfos 的 PK，這在一開始看起來合理——MarketCode 是有業務意義的識別碼，應該當 PK。
 
打開真實 API 資料之後發現了一個問題：MarketCode 514 在不同來源下有兩個名稱——Veg API 叫「溪湖鎮」，Flower API 叫「彰化市場」。這兩個名稱在查詢 AgriProductsTrans API 時各自對應不同的資料集（蔬菜交易 vs 花卉交易），必須分別存為兩筆才能讓後續的 AgriProductsTransSyncWorker 用正確的 MarketName 打 API。
 
這讓「一個 MarketCode 對應一筆主檔」的假設直接失效。解法是：
 
- PK 改成 surrogate Id（int IDENTITY），不與業務代碼綁定
- Unique constraint 改為 `(MarketCode, MarketName)`——同一組 code + name 組合才視為重複，514 溪湖鎮和 514 彰化市場是不同的兩筆，自然並存
- MarketType 欄位（Veg / Fruit / Flower）新增進來，讓 AgriProductsTransSyncWorker 知道每筆市場要用哪種類型的 API 查詢
**關鍵設計決策三：AgriProductsTrans 改用值層面關聯，拿掉 MarketInfo FK**
 
MarketInfo 的 PK 改成 surrogate Id 之後，原本 `AgriProductsTrans.MarketCode → MarketInfos.MarketCode` 的 FK 關係就無法維持——SQL Server 的 FK 只能指向 PK 或有 Unique constraint 的欄位，而 MarketCode 現在只是一個普通欄位。
 
考慮把 FK 改成指向 surrogate Id，但這要求 AgriProductsTransSyncWorker 在寫入每筆交易時先查 MarketInfos 找到對應的 Id——這是額外的查詢代價，而且交易 API 回傳的就是 MarketCode 字串，本來就不需要查 MarketInfos 才能決定要存什麼。
 
最終選擇移除導覽屬性和 `HasForeignKey`，讓 `AgriProductsTrans.MarketCode` 成為純字串欄位。資料完整性由應用程式層保證：AgriProductsTransSyncWorker 的市場清單本來就是從 MarketInfos 表讀出來的，寫進 AgriProductsTrans 的 MarketCode 一定有對應的主檔存在，不需要資料庫的 FK constraint 來重複保護。已知代價：MarketInfos 的記錄被刪除後 AgriProductsTrans 的歷史資料不會連帶刪除——這正是設計意圖，歷史交易快照應該被保留。
 
**關鍵設計決策四：CropMarketSyncWorker 只打 Veg / Fruit / Flower，排除 ComVegFruit / ComFlower**
 
農業部的 `/CropMarketType/` API 提供五種類型：Veg、Fruit、Flower、ComVegFruit、ComFlower。測試後發現：
 
- ComVegFruit 回傳的市場名稱是「台北二市」、「板橋市場」、「三重市場」——這些名稱帶入 AgriProductsTrans API 的 MarketName 查詢參數回傳空陣列，API 做精確比對，不做模糊搜尋
- ComFlower 的清單與 Flower 完全相同，是重複來源
- Veg 和 Fruit 回傳的名稱（「台北二」、「板橋區」、「三重區」）可以正確查到交易資料
所以 Worker 只打三隻 API，ComVegFruit 和 ComFlower 直接排除。
 
**關鍵設計決策五：105 台北市場的硬編碼補丁，以及時機的重要性**
 
Veg 和 Fruit API 都不包含 MarketCode 105，而 Flower API 的 105 名稱是「台北花市」，但 AgriProductsTrans API 的真實 MarketName 欄位回傳的是「台北市場」。兩個名稱不一致，無論打哪隻 API 都無法自然同步進正確的名稱。
 
解法是在 Worker 啟動時硬編碼 upsert 這一筆：
 
```csharp
if (!await db.MarketInfos.AnyAsync(m => m.MarketCode == "105" && m.MarketName == "台北市場"))
{
    db.MarketInfos.Add(new MarketInfo { MarketCode = "105", MarketName = "台北市場", MarketType = "Flower" });
    await db.SaveChangesAsync(stoppingToken);
}
```
 
這筆 upsert 刻意放在三隻 API sync 之前，並且有自己的 `SaveChangesAsync`。原因是：如果放在 API sync 之後，API 打到一半失敗會導致 Worker 中斷，硬編碼那筆沒有寫進去，AgriProductsTransSyncWorker 在這個 Worker 沒有成功完成的情況下跑起來就會找不到 105 台北市場。先存確保「依賴方啟動前一定有這筆資料」。
 
**關鍵設計決策六：HashSet 記憶體鏡像模式——三次 API 只查一次 DB**
 
去重邏輯：從 DB 撈出已存在的 `(MarketCode, MarketName)` 組合建立 `HashSet<(string, string)>`，比對後把新資料 `Add` 進 Change Tracker，同時也 `Add` 進 HashSet——HashSet 扮演「DB + 尚未存入的資料」的聯集，下一次 API 比對時可以直接使用。三次 API 跑完後一次 `SaveChangesAsync`。
 
```csharp
// 比對、Add、HashSet 同步更新，三步合一
var toAdd = incoming
    .Where(m => !existingMarketCodes.Contains((m.MarketCode, m.MarketName)))
    .ToList();
 
await db.MarketInfos.AddRangeAsync(toAdd, stoppingToken);
foreach (var m in toAdd)
{
    existingMarketCodes.Add((m.MarketCode, m.MarketName));  // ← 記憶體鏡像維護
}
```
 
若不維護 HashSet，三次 API 各自查一次 DB，每次查之前還要先 `SaveChangesAsync` 讓上一輪的新資料可見——等同於 3 次查 DB + 3 次 Save，邏輯複雜且效能沒有必要。
 
**驗收標準**
 
編譯 0 錯誤。SQL Server 中 `market` 和 `weather` 兩個 Schema 存在，各自的資料表在對應 Schema 下。`MarketInfos` 表有 `Id`、`MarketCode`、`MarketName`、`MarketType` 欄位，`(MarketCode, MarketName)` 的 Unique Index 存在。Worker 啟動後 Log 顯示三類市場資料同步成功，MarketInfos 表中可查到 105 台北市場（MarketType = Flower）這筆記錄，514 有溪湖鎮（Veg）和彰化市場（Flower）兩筆並存。第二次執行顯示「無新資料需寫入（全部已存在）」，確認去重邏輯正確。

---

## PR #015 — CoreDbContext、SyncState、DateHelper 與 AgriProductsTransSyncWorker 完整實作

**標題**：`feat(market): 建立跨模組 CoreDbContext/SyncState、DateHelper ROC 日期轉換與 AgriProductsTransSyncWorker 增量同步`

**背景與動機**

這個 PR 是 Market 模組後端開發的第三階段，也是迄今複雜度最高的一批。前兩個 PR 完成了休市日（#013）和市場清單（#014），這個 PR 的核心任務是把「農產品交易行情」真正同步進資料庫。

AgriProductsTrans API 有幾個特徵讓它比前兩支 API 都複雜：一、日期格式是民國年字串（`"107.07.10"`），無法直接存進 `DateOnly` 欄位；二、資料量大，從 2018 年累積至今，初次同步需要以「天」為單位逐日推進，中途可以中斷恢復；三、農業部的 API 在休市日不是回傳空陣列，而是回傳帶有特殊標記的記錄，需要識別並跳過；四、同批次的 API 資料本身可能就有重複筆，也需要判斷是否與 DB 已有記錄重疊。

要解決這些問題，這個 PR 除了 Worker 本身之外，還建立了兩個可供跨模組複用的基礎設施：`CoreDbContext` 管理 `SyncStates` 資料表（追蹤增量同步進度），以及 `DateHelper` 提供民國年的雙向轉換工具方法。

**關鍵設計決策一：CoreDbContext 放在 Core 層，而非 Market 模組**

增量同步的進度追蹤（「上次跑到哪一天」）是一個橫切關注點（cross-cutting concern）。`PorkTrans` SyncWorker 以後也需要同樣的機制，`DebrisAlert` SyncWorker 也可能需要。如果 `SyncState` 放在 `TaiwanAgri.Modules.Market`，Market 模組就變成了基礎設施提供者，其他模組要依賴 Market 才能用到這個機制，違反了模組邊界。

最終選擇把 `CoreDbContext` 和 `SyncState` 放在 `TaiwanAgri.Core/Infrastructure/`，schema 設為 `"core"`，在 `TaiwanAgri.Worker` 的 `Program.cs` 裡與 `MarketDbContext`、`WeatherDbContext` 並排以 `AddDbContext` 註冊。遵循的原則是：Core 層存放「任何模組都可能需要的共用工具或基礎設施」，而非把共用機制塞進其中一個業務模組。

**關鍵設計決策二：SyncState 模式取代 MAX(TransDate)——從根本消除休市日卡死**

初版設計是每次 Worker 執行時查 `MAX(TransDate)` 當作上次同步的終點，下次從 `MAX + 1 天` 開始。這個設計看起來直觀，但存在一個無法自癒的缺陷：如果某天全市場都休市，`AgriProductsTrans` 表不會有任何該天的記錄，`MAX(TransDate)` 永遠停在前一天，Worker 會無限重跑同一天——更精確地說，每次都對一個已經有完整記錄的日期重跑，只是全部被去重過濾掉，日期永遠無法前進。

`SyncState` 解決這個問題的方式：`LastSyncedDate` 欄位在每天迴圈結束後更新，不管那天是否有任何交易資料寫入 DB，日期一定往前推進一格。「已完成同步的最後一天」和「有資料寫入的最後一天」是兩個不同的概念，SyncState 追蹤的是前者，MAX(TransDate) 只能得到後者。

`SyncState` 刻意不設 `CreatedAt`——這個 Entity 是被 upsert 維護的持續狀態，不是 append-only 的事件記錄，`CreatedAt` 在這個語意下沒有意義。

**關鍵設計決策三：LastSyncedDate 初始值 = 2018/06/30，而不是 2018/07/01**

農業部農產品交易行情資料從 2018 年 7 月 1 日開始有記錄，因此第一次執行時要從 `2018/07/01` 開始同步。

`SyncState` 的語意是「已完成同步的最後一天」，`startDate` 計算方式是 `LastSyncedDate.AddDays(1)`。如果把初始值設為 `2018/07/01`，`startDate` 就會從 `2018/07/02` 開始，漏掉第一天。因此初始值設為 `2018/06/30`（一個在資料庫裡根本不存在資料的日期），代表「這天之前（含）都已完成，但其實什麼都沒有」，讓 `startDate` 正確地從 `07/01` 開始。

這是一個刻意設計的 off-by-one：欄位的語意決定了初始值的選擇，不是倒推出來的技術修正。在設計任何「上次到哪」的斷點恢復機制時，先想清楚欄位的語意（「已完成的最後一天」vs「下次要從哪天開始」），再推導初始值，避免日後閱讀程式碼時產生語意混淆。

**關鍵設計決策四：三參數同時帶入，抑制 AgriProductsTrans API 的分頁行為**

農業部的 `AgriProductsTrans` API 有一個分頁機制——當查詢結果量大時，會回傳 `Next: true`，要求打下一頁。但實驗後發現，當 URL 同時帶入 `Start_time`、`End_time`、`MarketName` 三個參數時，API 回傳的 `Next` 始終為 `false`，不會觸發分頁。也就是說，三個參數的組合讓 API 回傳的是「這個市場這一天的全部資料」，資料量有限，不需要分頁。

相較之下，只帶 `Start_time` 不帶 `MarketName` 時，API 會回傳全台所有市場的資料，量大到需要分頁，且因為農業部的商業限制（非會員只能取第一頁），後續頁數無法取得。

這個發現讓 Worker 的結構從「外層日期 × 內層分頁」變成「外層日期 × 內層市場（每次打一個市場，不需要分頁）」，簡化了錯誤處理邏輯，也讓每個 API 呼叫的語意更清晰——一次呼叫對應「某個市場某一天的交易資料」。

**關鍵設計決策五：upperBound 用台灣時間計算「昨天」**

同步的上界選「昨天」而不是「今天」，原因是農業部的當天交易資料在台灣時間深夜才會更新完整，Worker 執行時間點不固定，選今天可能取到不完整的資料。選昨天代表「永遠只同步已確定完整的歷史資料」。

計算「台灣時間昨天」需要注意跨平台問題。Windows 系統的時區 ID 是 `"Taipei Standard Time"`，Linux / macOS 是 `"Asia/Taipei"`，兩者不能互換。透過 `OperatingSystem.IsWindows()` 判斷後選擇對應的 ID，確保 Worker 在 Windows 開發環境和未來可能的 Linux 容器部署上都能正確計算時區邊界。

```csharp
var tzId = OperatingSystem.IsWindows() ? "Taipei Standard Time" : "Asia/Taipei";
var tzInfo = TimeZoneInfo.FindSystemTimeZoneById(tzId);
var todayTaipei = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tzInfo).Date;
var upperBound = DateOnly.FromDateTime(todayTaipei.AddDays(-1));
```

**關鍵設計決策六：CropInfo 物理 FK 移除，改用應用程式層保證完整性**

`AgriProductsTrans` Entity 最初設計帶有 `[ForeignKey("CropCode")] public CropInfo CropInfo` 導覽屬性，在資料庫層建立了物理 FK constraint。這在一般情況下是好設計，但這裡有一個特殊的雞生蛋問題：`CropInfos` 表的資料來源正是 `AgriProductsTrans` 同一支 API——Worker 第一次執行時 `CropInfos` 是空的，帶著物理 FK 的 INSERT 必然失敗。

與 PR #014 的 `MarketInfo` 不同，那次是因為 surrogate PK 讓 FK 的指向欄位消失，這次是同模組內也選擇放棄物理 FK。原因是完整性保護的成本與收益不對稱：FK constraint 保護的場景是「有人直接刪除 CropInfos 記錄後，AgriProductsTrans 孤兒記錄還在」，但 CropInfos 本身就不會被外部刪除（快照型填充），收益極低。相反，移除 FK 之後，Worker 自己在寫入 AgriProductsTrans 之前先確保對應的 CropInfo 已存在（有自己的 `SaveChangesAsync`），這個「應用程式層的執行順序保護」完全等效於 FK constraint 的插入保護，且沒有雞生蛋問題。

**關鍵設計決策七：CropInfo 順帶同步的執行順序與獨立 SaveChanges**

CropInfo 的填充邏輯嵌在 AgriProductsTransSyncWorker 的每日迴圈裡：在 MapToEntity 之前，先從這批 API 資料中抽出不重複的 CropCode 集合，與 DB 現有記錄比對，只 Add 尚未存在的 CropInfo 記錄，然後立即 `SaveChangesAsync`，再繼續寫入 AgriProductsTrans。

這個「先存 CropInfo 再存 AgriProductsTrans」的順序模擬了物理 FK 的插入保護語意。如果把 CropInfo 和 AgriProductsTrans 的寫入合在同一個 `SaveChangesAsync`，就等於同一個 transaction 裡既新增了 FK 的被指向記錄又新增了指向它的記錄，在移除物理 FK 後這其實是可以的，但兩個 `SaveChanges` 分開讓程式碼的意圖更清楚：「先確保 CropInfo 存在，再寫 AgriProductsTrans」。

**關鍵設計決策八：TransQuantity 在執行期發現需要改成 decimal**

最初根據 API 文件設計 `TransQuantity` 欄位為 `int`（交易量，直覺上應該是整數）。Worker 實際跑起來之後，遇到 JSON 反序列化失敗，錯誤訊息顯示某些市場的交易量回傳了帶小數的數值（如 `123.5`）。

這個案例再次確認了「API 文件是參考，真實回傳才是設計依據」的原則。修正方式是執行 `FixTransQuantityType` Migration 把欄位改成 `decimal(8,2)`，同時補上 `HasPrecision(8,2)`，避免 EF Core 發出截斷警告。這個修正被獨立成一個 Migration 而不是直接改前一個，確保 Migration 歷史清楚記錄了「發現問題 → 修正」的時間軸，不會在之後 review 時讓人困惑「為什麼欄位一開始就是 decimal」。

**關鍵設計決策九：雙層去重——DistinctBy 處理批次內部，HashSet<ValueTuple> 處理歷史**

去重有兩個維度，職責必須分開：

第一層 `DistinctBy` 處理「這批 API 回傳的資料本身就有重複筆」——農業部的 API 在部分情況下同一批回傳有完全相同的記錄，如果不先去重就直接比對 DB，雖然最終 DB 層的 Unique Index 會攔截重複，但資料庫層的違規錯誤比程式層的靜默過濾付出的代價高得多（前者拋例外、後者只是少寫幾筆）。

第二層 `HashSet<(DateOnly, string, string, string)>` 處理「這批資料與 DB 已有記錄的重複」——從 DB 查出當天已存在的自然鍵組合，建立 HashSet，過濾掉 incoming 裡已在 DB 存在的記錄。使用 `ValueTuple` 而非匿名型別，因為 ValueTuple 的值相等性可以跨方法邊界使用，而匿名型別在 HashSet 的 `Contains` 比對中依賴物件參考相等性，不同 `new { ... }` 即使值相同也判為不同。

兩層去重各自解決一個問題，不要把「批次內去重」和「歷史去重」混在同一個邏輯裡，否則邊界情況會讓程式碼越來越難維護。

**AgriProductsTransSyncWorker 完整流程**

```
第一階段：準備起始日期
1. 讀取 CoreDbContext.SyncStates（SyncKey = "Market_AgriProductsTrans"）
2. 若不存在，建立初始記錄（LastSyncedDate = 2018/06/30），立即 SaveChangesAsync
3. startDate = LastSyncedDate.AddDays(1)

第二階段：準備上界與市場清單
4. 計算 upperBound = 台灣時間昨天（TimeZoneInfo 跨平台）
5. 從 MarketDbContext 一次預載全部 MarketInfos

第三階段：雙層迴圈 × 資料處理 × SyncState 更新
6. for currentDate = startDate to upperBound（每次 AddDays(1)）
   for each market in MarketInfos
     → 打 API：?Start_time={ROC}&End_time={ROC}&MarketName={market.MarketName}
     → CropCode == "-" 過濾（休市筆跳過）
     → DistinctBy(x => (x.TransDate, x.TcType, x.CropCode, x.MarketCode))（批次去重）
   
7. 抽出本日新 CropCode → Add CropInfos → SaveChangesAsync（先存主檔）
8. HashSet<(DateOnly,string,string,string)> 查 DB 建立歷史去重集合
9. 過濾掉 HashSet 已包含的筆 → MapToEntity → AddRange
10. SaveChangesAsync（AgriProductsTrans）
11. lastSyncState.LastSyncedDate = currentDate（EF Core Change Tracker 自動偵測修改）
12. dbCore.SaveChangesAsync()（更新 SyncState，不需要顯式 .Update()）
```

**驗收標準**

編譯 0 錯誤。`core.SyncStates` 資料表存在，SyncKey 欄位有 Unique Index。Worker 首次啟動後 Log 顯示從 `2018/07/01` 開始逐日推進，AgriProductsTrans 表持續寫入資料（歷史資料量大，初次同步需要時間）。中途停止再重啟後，Worker 從 SyncState 記錄的 LastSyncedDate + 1 天繼續，不從頭重跑，確認斷點恢復正確。遇到全市場休市的日期，Log 顯示「跳過休市筆」但 SyncState 仍推進，`MAX(TransDate)` 不卡死。FixTransQuantityType Migration 執行後，`AgriProductsTrans.TransQuantity` 欄位型別為 `decimal(8,2)`，不再有截斷 Warning。

---

## PR #016 — AgriProductsTransSyncWorker 效能優化：併發 API 請求、記憶體快取與批次寫入
 
**標題**：`perf(market): AgriProductsTransSyncWorker 效能優化——Task.WhenAll 併發、HashSet 快取去重與批次 SaveChanges`
 
**背景與動機**
 
PR #015 完成了 `AgriProductsTransSyncWorker` 的完整功能實作，Worker 可以正確地增量同步農產品交易行情資料。但功能正確和效能可用是兩件事。實際跑了一個晚上（約 8 小時）之後，發現只同步了約 1 年 2 個月的資料——這意味著要把 2018 年至今的歷史資料全部補齊，可能需要數十小時甚至更長。
 
效能瓶頸不在資料量，而在**迴圈結構**。原始實作是巢狀迴圈（天 × 市場），在最內層的每一圈裡都進行了一次 HTTP 請求、至少兩次 DB 查詢、一次 SaveChanges。90 天 × 50 個市場 = 4,500 次循環，每次循環都在等待網路 I/O 和資料庫 Round-trip，CPU 大部分時間是閒置的。
 
這個 PR 針對上述瓶頸進行了四個維度的優化，不改變任何業務邏輯，只改變「什麼時候查 DB、什麼時候存、什麼時候等 API」的結構安排。
 
**關鍵設計決策一：Task.WhenAll 併發 API 請求，而非改用執行緒安全集合**
 
同一天內的 50 個市場彼此獨立，不需要等前一個市場完成才能打下一個市場的 API。原始的 `foreach` 是串行的，總等待時間 = 每個市場等待時間的加總。改用 `Task.WhenAll` 之後，50 個 API 同時發出，總等待時間 ≈ 最慢那一個市場的時間。
 
實作上有一個重要的設計選擇：Task 的責任只限於打 API 並回傳原始 json，不在 Task 內部做任何資料處理。
 
```csharp
var rawResults = await Task.WhenAll(marketInfos.Select(async market =>
{
    var url = $"...&MarketName={market.MarketName}";
    try
    {
        var json = await _httpClient.GetStringAsync(url, stoppingToken);
        return (Market: market, Json: json, Success: true);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "市場 {Market} 抓取失敗", market.MarketName);
        return (Market: market, Json: string.Empty, Success: false);
    }
}));
 
// 回到主執行緒，依序處理所有結果
foreach (var (market, json, success) in rawResults) { ... }
```
 
另一個考慮過的方案是把所有共享集合換成執行緒安全版本（`ConcurrentDictionary`、`ConcurrentBag`），讓資料處理也在 Task 內部併發進行。這個方案被否定，原因有兩個：一、`ConcurrentDictionary` 解決了寫入衝突，但無法解決 **TOCTOU（Time of Check to Time of Use）** 問題——Task A 和 Task B 可能同時通過「CropCode 不存在」的檢查，然後各自 Add，導致重複寫入，這需要用 `GetOrAdd` 等原子操作解決，程式碼複雜度大幅提升；二、資料處理本身是 CPU 密集的記憶體操作，速度很快，讓它在主執行緒依序跑完全不影響整體效能——真正的瓶頸是 API 等待，那才是需要並發的部分。
 
同時，這個實作加入了個別市場的錯誤隔離：原本若某個市場 API 失敗，`Task.WhenAll` 會讓整個當天的處理中斷。改成在每個 Task 內部 `try/catch` 後，一個市場失敗不影響其他市場，`Success: false` 的結果在主執行緒裡被跳過。
 
**關鍵設計決策二：CropInfo 全量快取——4,500 次 DB 查詢降為 1 次**
 
原始實作在每圈迴圈（每個市場、每一天）都對 `CropInfos` 表發起一次查詢，確認這些 CropCode 是否已存在。90 天 × 50 個市場 = 4,500 次相同性質的查詢。
 
優化方式是在雙層迴圈開始之前，一次性把所有 CropCode 撈進記憶體的 `HashSet<string>`，之後的比對直接查記憶體（HashSet 的查找是 O(1)）。發現新 CropCode 時，同步更新 HashSet，確保後續的比對能反映最新狀態，不依賴 DB 查詢。
 
```csharp
// 雙層迴圈外，只查一次
var existingCropCodeSet = await dbMarket.CropInfos
    .Select(x => x.CropCode)
    .ToHashSetAsync(stoppingToken);
 
// 迴圈內發現新 CropCode 後，同步更新快取
foreach (var c in newCrops) existingCropCodeSet.Add(c.CropCode);
```
 
這個快取能夠正確運作的前提是：`AgriProductsTrans` 和 `CropInfos` 之間沒有實體外鍵（Physical FK）。如果有 FK，在同一個 Transaction 裡同時 Add CropInfo 和 AgriProductsTrans 會因為 FK 插入順序而失敗。由於 PR #015 已確認移除導覽屬性、不建立物理 FK，這裡可以安全地把 CropInfo 和 AgriProductsTrans 的寫入合併到同一個 `SaveChangesAsync`。
 
**關鍵設計決策三：existingKeys 移至市場迴圈外——消除 SaveChanges 移出後衍生的問題**
 
當 `SaveChangesAsync` 還在市場迴圈內時，原始的 `existingKeys` 查詢（當天已存在的交易記錄）每圈得到的結果都不同，因為前一圈的資料已經存進 DB。把 `SaveChangesAsync` 移出市場迴圈之後，這個查詢的結果在 50 圈內完全相同——今天的資料根本還沒存進 DB，每次都查到 0 筆（或只有歷史重跑的資料）。
 
解法和 CropInfo 快取完全同構：在市場迴圈開始之前，一次撈出當天所有已存在的自然鍵，建立 `HashSet<(DateOnly, string, string, string)>`，迴圈內比對只查記憶體。
 
```csharp
// 市場 foreach 之前，每天只查一次
var existingKeySet = (await dbMarket.AgriProductsTrans
    .AsNoTracking()
    .Where(x => x.TransDate == currentDate)
    .Select(x => new { x.TransDate, x.CropCode, x.MarketCode, x.TcType })
    .ToListAsync(stoppingToken))
    .Select(x => (x.TransDate, x.CropCode, x.MarketCode, x.TcType))
    .ToHashSet();
```
 
這裡需要兩步驟而非一步驟的原因：EF Core 的 `Select` 必須能翻譯成合法 SQL，而 ValueTuple 不是 SQL 認識的型別；先用匿名型別執行查詢（`.ToListAsync()`），資料進入記憶體後，再用純 C# LINQ 轉成 ValueTuple 的 HashSet（`.ToHashSet()`，不需要 `Async`）。
 
**關鍵設計決策四：SaveChanges 批次化與原子性保護**
 
原始實作在每個市場迴圈結束後呼叫一次 `SaveChangesAsync`，90 天 × 50 市場 = 4,500 次。優化後改為每天結束後呼叫一次，降為 90 次。
 
更重要的是**呼叫順序的語意**：
 
```csharp
// 正確順序：先存資料，再更新狀態
await dbMarket.SaveChangesAsync(stoppingToken); // AgriProductsTrans + CropInfos
lastSyncState.LastSyncedDate = currentDate;
lastSyncState.UpdatedAt = DateTime.UtcNow;
await dbCore.SaveChangesAsync(stoppingToken);   // SyncState
```
 
這個順序有原子性保護的語意：若 `dbMarket.SaveChangesAsync` 失敗，`dbCore` 不會執行，SyncState 不更新，下次重啟 Worker 會從同一天重試；若順序反過來，SyncState 已更新但資料沒存，這天資料永遠丟失且 Worker 不會知道需要補跑。
 
**關鍵設計決策五：Log 等級語意化**
 
原始實作在 API 回應異常和 API 回傳無資料兩種情況下使用同一條 `LogInformation`，除錯時無法快速辨別問題性質。優化後拆分：
 
```csharp
if (response?.RS != "OK")
{
    _logger.LogWarning("市場 {Market} API回應異常: {RS}", market.MarketName, response?.RS);
    continue;
}
if (response.Data == null || response.Data.Count == 0)
{
    _logger.LogInformation("市場 {Market} 無資料，跳過", market.MarketName);
    continue;
}
```
 
`RS != "OK"` 是非預期的異常狀態，應該用 `Warning` 讓監控系統可以設定告警；`Data` 為空是正常的商業情況（市場當天沒有交易），應該用 `Information` 安靜記錄。Log 等級應該反映「我需要多快注意這件事」，而不是「這是不是程式的錯」。
 
**優化效果對比**
 
| 指標 | 優化前 | 優化後 |
|------|--------|--------|
| API 請求方式 | 串行，一次一個市場 | 併發，同天 50 個同時打 |
| CropInfo DB 查詢 | 4,500 次（每圈一次） | 1 次（全量快取） |
| existingKeys DB 查詢 | 4,500 次（每圈一次） | 90 次（每天一次） |
| SaveChangesAsync 次數 | 4,500 次（每圈一次） | 90 次（每天一次） |
| 單一市場失敗影響 | 整天中斷 | 只跳過該市場 |
 
**驗收標準**
 
編譯 0 錯誤。Worker 啟動後 Log 可見「--- 開始同步日期: XXXX-XX-XX ---」逐日推進，且每天的處理時間明顯縮短（原本每天約 80 秒，優化後應在數秒以內）。Log 中可見市場併發打 API 的效果（同一天多個市場的結果幾乎同時出現）。若某個市場 API 呼叫失敗，Log 顯示 Warning 但其他市場繼續處理、當天 SyncState 正常推進。重跑已存在資料時，Log 顯示「0 筆新增」而非報錯，確認去重邏輯在 SaveChanges 移出迴圈後依然正確。

---

## PR #017 — AgriProductsTransSyncWorker 跨市場重複寫入 Bug Fix

**標題**：`fix(market): 修正跨市場查詢導致相同自然鍵重複寫入、撞 Unique Index 的問題`

**背景與動機**

PR #016 的效能優化上線後，Worker 實際跑資料時在 `2019-11-04` 這天拋出 `DbUpdateException`：

```
Cannot insert duplicate key row in object 'market.AgriProductsTrans'
with unique index 'IX_AgriProductsTrans_TransDate_TcType_CropCode_MarketCode'.
The duplicate key value is (2019-11-04, N06, FB000, 800).
```

程式碼裡已有兩層去重邏輯（`DistinctBy` + `existingKeySet`），理論上不應該出現重複插入。這個錯誤揭示了一個在設計階段沒有完整追蹤影響範圍的問題：PR #014 已知 `MarketCode 514` 在 `MarketInfos` 有兩筆（溪湖鎮 Veg、彰化市場 Flower），但當時的推論只停在「主檔層面的一對多」，沒有繼續推演到「交易同步時，兩個 MarketName 分別打 API，回傳資料的 MarketCode 欄位可能相同，導致跨市場查詢產生重複的自然鍵」。

**問題根因分析**

PR #016 引入 `Task.WhenAll` 後，同一天的所有市場 API 同時發出，`rawResults` 包含所有市場的原始 json。後續的 `foreach` 依序處理每個市場，每個市場各自做 `DistinctBy` 去重：

```csharp
// 修正前：每個市場各自去重，市場間沒有互相比對
foreach (var (market, json, success) in rawResults)
{
    var incoming = response.Data
        .Where(x => x.CropCode != "-")
        .DistinctBy(x => new { x.TransDate, x.CropCode, x.MarketCode, x.TcType })
        .ToList();
    // 直接 AddRange，沒有跨市場去重
    dbMarket.AgriProductsTrans.AddRange(saveData);
}
```

`existingKeySet` 在 `foreach` 開始前查 DB 建立，記錄的是「今天已存進 DB 的資料」。但由於 `SaveChangesAsync` 移到 `foreach` 外面（PR #016 的優化），Change Tracker 裡尚未存進 DB 的資料對 `existingKeySet` 完全不可見。

所以完整的失敗鏈是：

1. MarketInfos 有兩筆：514 溪湖鎮（Veg）、514 彰化市場（Flower）
2. `Task.WhenAll` 以兩個不同 MarketName 各打一次 API
3. 農業部 API 回傳的資料欄位是 `MarketCode`（514），不是查詢用的 `MarketName`
4. 兩次 API 可能回傳相同的 `(TransDate, TcType, CropCode, MarketCode=514)` 記錄
5. 市場 A 的 `DistinctBy` 通過，AddRange 進 Change Tracker
6. 市場 B 的 `DistinctBy` 也通過（只看自己的 incoming，沒看市場 A 已 Add 的）
7. `existingKeySet` 也攔不到（查的是 DB，不是 Change Tracker）
8. `SaveChangesAsync` 時，兩筆相同自然鍵同時送進 DB，Unique Index 拋例外

**關鍵設計決策：將 DistinctBy 的作用範圍從「單一市場」擴大為「當天所有市場合併後」**

修正方式是在 `foreach` 結束後，對所有市場的資料合併再做統一去重：

```csharp
// 修正後：先收集所有市場的資料
var allIncoming = new List<AgriProductsTransTypeDto>();

foreach (var (market, json, success) in rawResults)
{
    if (!success || string.IsNullOrEmpty(json)) continue;
    var response = JsonSerializer.Deserialize<AgriProductsTransTypeApiResponse>(json);
    if (response?.RS != "OK") { ... continue; }
    if (response.Data == null || response.Data.Count == 0) { ... continue; }

    // 只收集，不去重、不 AddRange
    var incoming = response.Data
        .Where(x => x.CropCode != "-")
        .ToList();
    allIncoming.AddRange(incoming);
}

// foreach 結束後，對合併後的資料統一去重
var targetData = allIncoming
    .DistinctBy(x => new { x.TransDate, x.CropCode, x.MarketCode, x.TcType })
    .ToList();
```

這個修正讓 514 溪湖鎮和 514 彰化市場各自回傳的 `(2019-11-04, N06, FB000, 514)` 在合併後只保留一筆，徹底解決跨市場重複的問題。

值得注意的是：`DistinctBy` 只去掉**完全相同自然鍵**的記錄。如果兩個市場查詢回傳的 MarketCode 相同、但 CropCode 或 TcType 不同，這些是不同的交易紀錄，`DistinctBy` 不會去掉，兩筆都正確保留。

**這次修正暴露的設計推演缺口**

PR #014 做了一個正確的決策（surrogate PK + 允許同一 MarketCode 有多筆 MarketName 並存），但當時的影響追蹤停在主檔層面，沒有繼續推演到交易同步層面：

- 已知：同一 MarketCode 可以有多個 MarketName（主檔一對多）
- 未推演：多個 MarketName 打 API → 回傳資料的 MarketCode 欄位相同 → 需要跨市場去重

這個缺口在功能正確性測試時不容易被發現（少量測試資料不容易踩到 514 同時有兩個名稱），只有在大規模補跑歷史資料時才會觸發。

**驗收標準**

編譯 0 錯誤。Worker 重啟後繼續從 `2019-11-04` 跑，不再拋 `DbUpdateException`。確認 `2019-11-04` 的資料正確寫入，MarketCode=514 的交易記錄只有一份，不重複。後續的歷史資料補跑也不再出現 Unique Index 違規錯誤。

---

## PR #018 — DebrisAlertRecord Entity、Migration、DTO 與 DebrisAlertRecordSyncWorker 完整實作

**標題**：`feat(market): 實作 DebrisAlertRecordSyncWorker——土石流及大規模崩塌警戒歷史記錄同步`

**背景與動機**

這個 PR 完成 Market 模組後端開發的第四支 SyncWorker：`DebrisAlertRecordSyncWorker`，負責同步農業部「土石流及大規模崩塌警戒發布紀錄」資料。

這支 Worker 的開發過程有一個值得記錄的前置決策過程。原始設計把 DebrisAlert 定位在模組 4「天災與菜價關聯分析」的資料來源，但農業部 `GetCustomerDebrisAlertInfo` 的設計是「只回傳當下正在發布的警戒，警戒解除後記錄消失」，不保留歷史。這對時序分析完全沒有用——分析需要的是「某一天有警戒」這個歷史事實，而不是「現在有沒有警戒」。

轉機出現在另一個端點：農業部開放資料平台的「土石流及大規模崩塌警戒發布紀錄」資料集（`UnitId=kRam3LShuWSv`），它本身就是歷史記錄的彙整——每個災害事件的每一份報別（`ReportID`）都被保留下來，並帶有明確的 `LastUpdateDate`。這個 API 不帶任何查詢參數，一次全量回傳所有歷史記錄（約 3 MB），是設計成「全量資料集」而非「即時查詢」的端點。

這個發現讓整個 Worker 的設計思路從「定時快照」轉為「歷史記錄全量同步 + 定期更新」，大幅提升了對模組 4 分析功能的支撐能力。

**關鍵設計決策一：AlertType D 與 L 合為一張表，以 AlertType 欄位區分**

JSON 資料裡有兩種警戒類型：`AlertType = "D"` 是土石流（`DebrisNo` 有值，`LandslideID` 為 `"-"`），`AlertType = "L"` 是大規模崩塌（`LandslideID` 有值，`DebrisNo` 為 `"-"`）。兩者的欄位結構幾乎完全一樣，差別只有 `DebrisNo` 和 `LandslideID` 互斥出現。

考慮過兩張表（`DebrisAlerts` + `LandslideAlerts`）的方案，但被否定。原因是前台的天災時間軸需要同時顯示 D 和 L 兩種警戒，拆表的代價是查詢時必須 UNION 或在 API 層做兩次查詢再合併，卻沒有帶來任何分析上的收益——`WHERE AlertType = 'D'` 就能在單一表內做到任何需要個別分析的查詢。最終選擇一張表 `DebrisAlertRecords`，設計簡單，查詢直接。

**關鍵設計決策二：自然鍵 `(ReportID, DebrisNo, LandslideID)` 與 `"-"` 轉 `null`**

資料中同一個 `DebrisNo`（例如 `新北DF166`）在不同 `ReportID` 下重複出現，且 `AlertLevel` 可能從 `"y"` 變為 `"r"` 再變回來。這說明每個 `ReportID` 是一次「報別快照」，記錄的是那個時間點的警戒等級。去重的正確單位是「這一份報別，這一個地點」，即 `(ReportID, DebrisNo, LandslideID)` 的組合。

`DebrisNo` 和 `LandslideID` 在原始 JSON 中以 `"-"` 表示「不適用」，而非 JSON `null`。統一在 `MapToEntity` 中把 `"-"` 轉成 C# `null` 存入，讓資料庫的 `NULL` 語意清晰——`NULL` 代表「這個欄位對此筆記錄不適用」，不會和真實的空值混淆。DTO 維持 `string` 接收原始值，轉換邏輯集中在 `MapToEntity`，符合各層職責分離的原則。

**關鍵設計決策三：`HasFilter(null)` 強制覆蓋 EF Core 對 nullable UNIQUE index 的預設行為**

這是這次 Migration 過程中遇到的一個非顯而易見的 EF Core 行為問題。當 UNIQUE index 包含 nullable 欄位時，EF Core 預設會自動加上 `WHERE` filter：

```
filter: "[DebrisNo] IS NOT NULL AND [LandslideID] IS NOT NULL"
```

這個 filter 的語意是「只有當兩個欄位都不是 null 時，UNIQUE index 才生效」。但自然鍵的結構決定了每一筆資料必定有一個欄位是 `null`（D 型態的 `LandslideID` 是 null，L 型態的 `DebrisNo` 是 null），導致這個 filter 條件**永遠不成立**，UNIQUE index 對任何一筆資料都不生效，去重保護形同虛設。

嘗試把 filter 改成 `OR` 語法被 SQL Server 拒絕（SQL Server 的 filtered index 不支援 `OR` 關鍵字）。最終解法是在 `OnModelCreating` 加上 `.HasFilter(null)`，明確告訴 EF Core「不要加任何 filter」，讓 UNIQUE index 對所有資料生效：

```csharp
entity.HasIndex(e => new { e.ReportID, e.DebrisNo, e.LandslideID })
      .HasDatabaseName("IX_DebrisAlertRecords_ReportID_DebrisNo_LandslideID")
      .HasFilter(null)   // 覆蓋 EF Core 預設的 nullable filter，確保 UNIQUE index 對所有資料生效
      .IsUnique();
```

這個設定的正確性建立在一個業務前提上：`DebrisNo` 和 `LandslideID` 不可能同時為 `null`——AlertType D 必有 `DebrisNo`，AlertType L 必有 `LandslideID`，業務上不存在兩者同時為 null 的情況，因此 SQL Server 對 UNIQUE index 中 `null = null` 視為相等的行為不會在此場景觸發。

**關鍵設計決策四：全量拉取 + 只 INSERT 新資料，不用 TRUNCATE**

這支 API 沒有任何日期篩選參數，每次呼叫都回傳完整的歷史記錄。同步策略有兩個選項：TRUNCATE + 全部重寫，或全量拉取 + 只 INSERT 新的。選擇後者，理由有兩個：

第一，效能隨時間退化。TRUNCATE + 重寫的成本與資料量線性相關，資料只會越來越多，未來若警戒記錄累積到數十萬筆，每次 Worker 執行都做一次全量刪除再全量插入，I/O 成本將成為明顯的負擔。

第二，不能依賴上游資料的永久性。農業部有可能在未來清理舊的警戒記錄。一旦做了 TRUNCATE，即使上游刪了資料，本地資料庫也跟著清空，歷史記錄永久遺失且無法復原。選擇只 INSERT 新的，已有資料不動，即使上游刪除，本地記錄依然保存——這個「不依賴上游永久性」的原則在面試中也是一個值得說清楚的設計理由。

**關鍵設計決策五：不需要 SyncState——與 AgriProductsTrans 的本質差異**

`AgriProductsTrans` 需要 `SyncState` 的原因是它的 API 支援日期篩選參數，每次只拉特定日期的資料，必須記錄「上次跑到哪一天」才能接著繼續。

`DebrisAlertRecord` 的 API 無法帶入日期參數，每次都是全量回傳。應用層的去重邏輯（從 DB 載入現有自然鍵 → HashSet 比對 → 只 INSERT 不在 HashSet 裡的）本身就保證了冪等性——不管執行幾次，結果都一樣，重複執行只是多做了一次比對，不會產生重複資料。這種全量冪等的設計不需要進度追蹤，`SyncState` 在此是不必要的複雜度。

**關鍵設計決策六：`LastUpdateDate` 在 DTO 用 `string`，MapToEntity 用 `DateTime.Parse + InvariantCulture`**

原始 JSON 的 `LastUpdateDate` 格式是 `"2026-04-04 15:26"`，沒有秒數部分。`System.Text.Json` 預設的 `DateTime` 反序列化不一定支援這個非標準格式，若在 DTO 就用 `DateTime` 型別接收，反序列化失敗會在最外層直接拋例外，難以定位。

DTO 用 `string` 接收原始字串，把解析的控制權交給 `MapToEntity`，並明確指定 `CultureInfo.InvariantCulture`：

```csharp
LastUpdateDate = DateTime.Parse(dto.LastUpdateDate, System.Globalization.CultureInfo.InvariantCulture)
```

不指定 Culture 的 `DateTime.Parse` 在不同作業系統和地區設定下，對同一個字串的解析行為可能不同。`InvariantCulture` 確保無論 Worker 部署在 Windows 開發機還是 Linux 容器，解析行為一致。這和 PR #015 中跨平台時區 ID 的處理思路相同：主動消除環境依賴，不留下隱性的跨平台風險。

**`DebrisAlertRecordSyncWorker` 完整流程**

```
第一步：建立 Scope，取得 MarketDbContext
第二步：從 DB 查詢所有現有記錄的自然鍵 (ReportID, DebrisNo, LandslideID) → ToHashSet（匿名型別）
第三步：GET 全量 API 資料（一次呼叫，約 3 MB）
第四步：反序列化為 List<DebrisAlertRecordDto>，null check
第五步：Select MapToEntity（含 "-" 轉 null、LastUpdateDate 解析）→ DistinctBy 自然鍵（批次內去重）→ ToList
第六步：Where 不在 existingRecords HashSet 裡的（與 DB 歷史去重）→ ToList
第七步：Count == 0 則 Log 並 return；否則 AddRange + SaveChangesAsync
第八步：Log 新增筆數與略過筆數
第九步：await Task.Delay(TimeSpan.FromHours(6))，每 6 小時執行一次
```

**驗收標準**

編譯 0 錯誤。`market.DebrisAlertRecords` 資料表存在，UNIQUE index `IX_DebrisAlertRecords_ReportID_DebrisNo_LandslideID` 無 filter 條件。Worker 首次啟動後，Log 顯示新增筆數，DB 可查到歷史警戒記錄（涵蓋 2025 年至今的各災害事件）。再次執行時，Log 顯示「無新資料需寫入（全部已存在）」，確認去重邏輯正確。AlertType D 的記錄 `LandslideID` 欄位為 NULL，AlertType L 的記錄 `DebrisNo` 欄位為 NULL，確認 `"-"` 轉 null 邏輯正確。

---

## PR #019 — DateHelper 擴充、PorkTrans Entity/Migration/DTO 與 PorkTransSyncWorker 完整實作

**標題**：`feat(market): 實作 PorkTransSyncWorker——毛豬交易行情歷史資料增量同步`

**背景與動機**

這個 PR 完成 Market 模組後端開發的第五支 SyncWorker：`PorkTransSyncWorker`，負責同步農業部「毛豬交易行情」（`PorkTransType`）資料。資料涵蓋民國 98 年 11 月 27 日至今的每日各市場成交記錄，包含規格豬、各重量區間、冷凍廠、淘汰種豬等完整統計欄位，共 36 個數值欄位，是農業市場模組中欄位最豐富的資料集之一。

這支 Worker 在設計上有幾個值得記錄的決策點，其中最核心的是 API 的日期參數設計決定了整個同步架構：`PorkTransType` 每次只能傳入單一 `TransDate`，每次查詢只回傳當天所有市場的資料，不支援日期區間。這個設計導向單層日期迴圈 + `SyncState` 的增量追蹤模式，和 `AgriProductsTrans` 的雙層迴圈（日期 × 市場）形成了鮮明對比，也是本次開發過程中推導出最清晰的一個架構決策。

**關鍵設計決策一：新增 `ParseRocNumericDate`——以 `.All(char.IsDigit)` 替代 `int.TryParse` 全串**

`PorkTransType` 的 `TransDate` 欄位格式是 `"1040706"`（YYYMMDD，純數字，無分隔符），和 `AgriProductsTrans` 的 `"107.07.10"`（點分隔格式）完全不同，現有的 `ParseRocDate` 無法處理。因此在 `DateHelper` 新增 `ParseRocNumericDate`。

方法的設計過程有一個值得記錄的轉折：最初的直覺是「先對整條字串 `int.TryParse`，再切分三段」，但這行不通——`int.Parse("1040706")` 拿到的是一個整數，整數沒有 `[0..3]` 這種切片操作。正確的順序是先在字串層面切片，再對每一段分別驗證。

最終選擇 `.All(char.IsDigit)` 在入口一次驗完所有字元，後面的 `int.Parse` 就永遠不會拋例外，不需要 `TryParse`。最後一步用 `DateOnly.TryParseExact` 做業務合法性驗證（閏年、月份上限等），用回傳值而非例外做流程控制。整個方法從頭到尾只有一種失敗路徑：`throw ArgumentException`，呼叫端處理起來一致：

```csharp
public static DateOnly ParseRocNumericDate(string inputDate)
{
    if (string.IsNullOrWhiteSpace(inputDate) || inputDate.Length != 7 || !inputDate.All(char.IsDigit))
        throw new ArgumentException($"日期格式錯誤: '{inputDate}'，須為 7 位數字。");

    int adYear = int.Parse(inputDate[0..3]) + 1911;
    int month  = int.Parse(inputDate[3..5]);
    int day    = int.Parse(inputDate[5..7]);

    string isoDate = $"{adYear:D4}-{month:D2}-{day:D2}";
    if (DateOnly.TryParseExact(isoDate, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                               DateTimeStyles.None, out DateOnly result))
        return result;

    throw new ArgumentException($"無效的日期內容: '{inputDate}' (轉換後為 {isoDate})。");
}
```

**關鍵設計決策二：新增 `ToRocNumericDate` Extension Method——`DateOnly` 反向轉民國年 YYYMMDD**

SyncWorker 的迴圈以西元 `DateOnly` 推進，但 API 端點需要的是民國年 `"YYYMMDD"` 字串。這個轉換應該放在 `DateHelper` 而不是 SyncWorker，理由和 `ParseRocNumericDate` 放在 `DateHelper` 是一樣的：可重用性和單一職責——呼叫端不需要知道民國年的計算邏輯，只要呼叫 `currentDate.ToRocNumericDate()` 就能拿到正確格式的字串。

設計成 extension method（`this DateOnly inputDate`）讓呼叫端語法更自然，民國年補零用 `:D3` 確保年份不足三位時正確補零（民國 98 年 → `"098"`）：

```csharp
public static string ToRocNumericDate(this DateOnly inputDate)
{
    int rocYear = inputDate.Year - 1911;
    return $"{rocYear:D3}{inputDate.Month:D2}{inputDate.Day:D2}";
}
```

**關鍵設計決策三：全部欄位存入，包含 `KgPig5`/`KgPig6`，以歷史資料不可補跑為由**

API 回傳 36 個數值欄位，其中 `KgPig5`（135–155 公斤區間）和 `KgPig6`（155 公斤以上區間）在民國 104 年前後的資料全部是 `0`，民國 115 年的資料才有實際數值。這說明農業部在某個時間點新增了這兩個重量區間的統計，而不是「這兩個欄位沒有資料」。

考量到這是歷史交易記錄，不是可以重跑的快照——`SyncState` 是從最後一筆往後走的，一旦選擇性地略過某些欄位，三個月後發現前台需要 `KgPig5_Q`，那些過去的日期資料要重新 sync 的代價很高。最終決策是全部存入，排除 `Page`（分頁控制參數，屬於請求參數而非資料欄位）。欄位名稱從 API 的縮寫（`KgPig5_Q`、`Num_115up`）全部改成語意清楚的 C# 命名（`Count135To155kg`、`Count115To135kg`），並在 DTO 加上 `[JsonPropertyName]` 對應。

**關鍵設計決策四：保留來源負數值，不做清洗**

民國 98 年的早期資料裡，`OtherPigs_AvgWgt` 出現了 `-11`、`-15`、`-49` 等負數值。一頭豬的平均重量為負數在現實中無意義，屬於來源系統的髒資料。

處理選項有三：原樣存入、負數改 `0`、負數改 `null`。選擇原樣存入，理由是 SyncWorker 的職責是忠實同步來源資料，不是資料清洗層。來源怎麼說，資料庫就怎麼存。如果未來需要清洗，顯示層或分析層可以自行決定過濾策略。順帶確認了一個命名原則：如果要清洗，`null` 比 `0` 更誠實——`null` 表達「這個值有問題，語意不明」，`0` 表達「這個欄位的值真的是零」，把 `-11` 改成 `0` 是在捏造資料。

**關鍵設計決策五：SyncState 設計——因為 API 只支援單日查詢且休市日無記錄**

`PorkTransType` 的 API 一次只能傳入單一 `TransDate`，不支援區間。如果用 `MAX(TransDate)` 從資料表推斷上次進度，會被休市日卡死——休市當天沒有任何記錄寫入，`MAX` 永遠回傳前一天，下次 sync 重複從同一天開始，永遠無法推進。

這和 `AgriProductsTrans` 遇到的問題完全一樣，解法也一樣：獨立的 `SyncState` 記錄「我已經處理到哪一天」，而不是看資料表裡有什麼。`SyncKey = "Market_PorkTrans"`，初始 `LastSyncedDate = new DateOnly(2009, 11, 26)`（西元），讓第一次迴圈從 `0981127` 開始跑。

**關鍵設計決策六：`lastSuccessfulDate` 模式——部分失敗時只推進已確認的進度**

SyncWorker 的日期迴圈可能在中途因 API 異常（`RS != "OK"`）或網路錯誤而中斷。如果直接用 `yesterdayDate` 更新 `LastSyncedDate`，中斷後跳過的日期下次就永遠不會補跑。

解法是引入 `lastSuccessfulDate` 變數，初始值等於 `lastSyncState.LastSyncedDate`。每次迭代只有在 API 回傳 `RS == "OK"` 時才推進（包含休市日——休市代表已確認該天沒有資料，不是失敗）；遇到異常就 `break`，迴圈結束後用 `lastSuccessfulDate` 而非 `yesterdayDate` 更新 `SyncState`。這樣確保進度只推進到真正確認過的最後一天，不遺漏任何一天的資料。

**`PorkTransSyncWorker` 完整流程**

```
第一步：建立 Scope，取得 MarketDbContext 和 CoreDbContext
第二步：從 CoreDbContext 查詢 SyncState（SyncKey = "Market_PorkTrans"），不存在則初始化並 SaveChanges
第三步：計算 startDate = LastSyncedDate.AddDays(1)，yesterdayDate 以台灣時區取得
第四步：查詢 PorkTrans 中 TransDate >= startDate 的所有自然鍵 → ToHashSet（匿名型別，值相等）
第五步：日期迴圈 startDate → yesterdayDate，每次呼叫 API 傳入 ToRocNumericDate() 轉換後的日期
        - RS != "OK" → LogError + break
        - 有資料 → AddRange 到 allDtos；無資料（休市）→ 繼續
        - 任何例外 → LogError + break
        - 成功（含休市）→ lastSuccessfulDate = currentDate
第六步：allDtos.Select(MapToEntity).DistinctBy 自然鍵 → 批次內去重
第七步：Where 不在 existingHashSet → 與 DB 歷史去重
第八步：有新資料 → AddRange + SaveChangesAsync（MarketDbContext）
第九步：lastSuccessfulDate > 初始值 → 更新 LastSyncedDate + SaveChangesAsync（CoreDbContext）
第十步：Log 新增筆數與略過筆數；每 12 小時執行一次
```

**驗收標準**

編譯 0 錯誤。`market.PorkTrans` 資料表存在，`IX_PorkTrans_TransDate_MarketName` UNIQUE index 正確建立，所有 `decimal` 欄位精度為 `(8,2)`。Worker 首次啟動後，Log 顯示從民國 98 年 11 月 27 日開始同步，`core.SyncStates` 中 `SyncKey = "Market_PorkTrans"` 的 `LastSyncedDate` 隨每次執行推進至昨天。第二次執行時，Log 顯示「無新資料需寫入」，確認去重邏輯正確。早期資料（民國 98–104 年）的 `KgPig5`/`KgPig6` 欄位值為 `0`，民國 115 年後有實際數值，確認全欄位原樣存入。

---

## PR #020 — MarketController + MarketService 查詢層，TaiwanAgri.Web 改造為純 Web API

**標題**：`feat(web+market): 實作模組 4 查詢層——MarketService / MarketController / DTO 結構重組 / TaiwanAgri.Web Web API 改造`

---

### 背景與動機

W7–8 完成了 Market 模組全部五支 SyncWorker（PR #013–#019），資料已在 SQL Server 中穩定累積。但一個後端服務光有資料同步是不夠的——資料存在 DB 裡，前台取不到，等於把圖書館建好了但沒有任何借書窗口。W9–10 的 Vue 3 前台開發正式啟動前，本 PR 完成三件事：

1. 把 `TaiwanAgri.Web` 從 Visual Studio 預設的 MVC 樣板改造成純 Web API 專案。
2. 建立查詢服務層（`IMarketService` + `MarketService`），實作五支資料查詢方法。
3. 建立 `MarketController`，把五支 API 端點暴露給 Vue 3 呼叫。

同時，隨著 ApiResponse DTO 的加入，`Dtos/` 資料夾的職責問題浮上檯面——原本 Worker 用的反序列化 DTO 和即將新增的 API 輸出 DTO 混在同一個層級，趁此機會一起重組，從根本上分清楚「進來的資料」和「出去的資料」。

---

### 關鍵設計決策一：Dtos 資料夾重組——WorkerResponses / ApiResponses 各司其職

原始的 `Dtos/` 根目錄同時放了 `AgriProductsTransTypeDto`、`DebrisAlertRecordDto` 這類 Worker 用的反序列化 DTO，以及即將新增的 `PriceResponseDto`、`CropResponseDto` 這類 API 輸出 DTO。問題在於這兩種 DTO 服務的是完全相反的資料流方向：

**WorkerResponses**（Worker 用）：從農業部 MOA API 收到 JSON 後反序列化使用，生命週期在 SyncWorker 的 HTTP 呼叫流程裡，欄位設計由 MOA API 的回傳格式決定，維護人員看到它的時候要想到「這是外部資料的形狀」。

**ApiResponses**（API 輸出用）：Service 查詢 DB 後組裝，序列化後回傳給 Vue 3 前端，欄位設計由前台的畫面需求決定，維護人員看到它的時候要想到「這是前端會收到的 JSON 結構」。

兩者放在一起，未來維護時需要逐一查看才能判斷每個 DTO 的用途，認知負擔不必要地提升。重組後拆成兩個子資料夾：`WorkerResponses/` 承接原有的所有 Worker DTO，`ApiResponses/` 放新建的五支輸出 DTO。

命名選擇「角色」而非「來源」（例如 `Moa/`），原因是角色對維護者更直觀——不需要知道 MOA 是什麼組織就能理解資料夾的用途。重組後，五支 SyncWorker 的 using 路徑同步更新為 `WorkerResponses/` 路徑。

---

### 關鍵設計決策二：MarketService 歸屬 Modules.Market，而非 TaiwanAgri.Web

第一直覺可能是「Service 是給 Controller 用的，放在 Web 就好」，但這忽略了相依方向的問題。如果 `MarketService` 放在 `TaiwanAgri.Web`，未來若需要額外的入口——例如後台管理介面（`TaiwanAgri.Admin`）或其他消費端——這些入口也需要呼叫同樣的查詢邏輯，就必須依賴 `TaiwanAgri.Web`，形成下層依賴上層的反向依賴，是錯誤的架構方向。

正確的相依關係是：Web（上層）→ Modules.Market（下層）。`MarketService` 放在 `TaiwanAgri.Modules.Market`，查詢邏輯屬於市場模組本身；`MarketController` 在 `TaiwanAgri.Web` 只做一件事——把 HTTP 請求翻譯成 Service 呼叫，不直接碰 `MarketDbContext`。

對應地，`PriceResponseDto` 等輸出 DTO 也放在 `Modules.Market/Dtos/ApiResponses/`，理由相同：上層（Web）可以依賴下層（Modules.Market）的型別，反過來則不行。

---

### 關鍵設計決策三：IMarketService 介面——依賴反轉與可測試性

`IMarketService + MarketService` 的模式在小型專案裡有時候被省略，認為是過度設計。但本專案的 Solution 結構裡已有 `TaiwanAgri.Tests` 專案，代表測試是明確的規劃目標。`MarketController` 依賴 `IMarketService`（抽象）而非 `MarketService`（具體），讓單元測試可以注入 mock 版本，不需要啟動真實的資料庫連線，這個收益是具體可見的。

`IMarketService` 定義在 `Modules.Market/Services/` 而非 `TaiwanAgri.Core`，考量是它和市場模組高度相關，放到 Core 有過度抽象之嫌；放在 `Modules.Market` 讓 Web 層透過依賴 `Modules.Market` 這個 lower layer 取得介面定義，相依方向依然正確。

---

### 關鍵設計決策四：TaiwanAgri.Web 改造策略——不砍重建，直接改造

`TaiwanAgri.Web` 是 Visual Studio 用「ASP.NET Core Web App with Authentication」樣板建出來的，包含 MVC + Razor Pages + Identity，從未認真改過。現在需要一個純 Web API 專案，有兩個選擇：

**選項 A（砍掉重建 TaiwanAgri.Api）**：代價是 `ApplicationDbContext`（管理 `AspNetUsers` 等 Identity 六張表）的 Migration 歷史要搬移，DB 對齊有踩坑風險，而且要重新配置 Identity 的所有設定。

**選項 B（直接改造 TaiwanAgri.Web）**：步驟只有四個——`AddControllersWithViews()` 改成 `AddControllers()`、移除 `Views/`、`wwwroot/`、`MapRazorPages()`、`MapControllerRoute()`，`HomeController` 繼承從 `Controller` 改成 `ControllerBase`。Migration 完全不需要動，Identity 的 `AspNetUsers` 表繼續在原位。

兩種結果在功能上等效，改造的風險幾乎為零。Portfolio 專案的開發成本是有限的，不應該花在沒有業務價值的重建上。

同時追加了幾項改善：`AddProblemDetails()` 讓正式環境回傳標準化的 Problem Details JSON（而非 MVC 的 HTML 錯誤頁）；CORS 設定允許 Vue 3 dev server（`:5173`）的跨域請求；Middleware 順序調整為 `UseRouting → UseCors → UseAuthentication → UseAuthorization`，確保認證中介軟體在正確的位置。

---

### 關鍵設計決策五：Controller 日期參數 string + ParseIsoDate 取代 [FromQuery] DateOnly

`[FromQuery] DateOnly startDate` 在 ASP.NET Core 的 Model Binding 對 `"yyyy-MM-dd"` 格式的支援在不同版本下行為不確定；更重要的是，即使框架能自動解析，失敗時回傳的是框架預設的不友好錯誤格式，前端無法直接顯示給使用者。

Controller 的職責包含「輸入驗證」，用 `string + DateHelper.ParseIsoDate + null check + BadRequest` 讓整個錯誤路徑完全在應用程式的控制下：

```csharp
var start = DateHelper.ParseIsoDate(startDate);
if (start == null) return BadRequest("startDate 格式錯誤，請使用 yyyy-MM-dd");
```

`ParseIsoDate` 設計為回傳 `DateOnly?`（null 代表格式不合法）而非拋例外，讓 Controller 用 null check 做流程控制，符合「預期的失敗用回傳值處理，不預期的失敗才用例外」的原則。GetPrices 的選填日期則用 `startDate != null && start == null` 的條件，精確區分「使用者沒傳」和「使用者傳了但格式錯」兩種情況。

---

### 關鍵設計決策六：預設日期區間的歸屬——商業邏輯在 Service，輸入驗證在 Controller

`GetPricesAsync` 的日期參數是選填，不傳時應該「預設今天往前 365 天」。判斷這段邏輯應該在哪一層的標準是：這是技術約束還是業務決策？

「格式必須是 yyyy-MM-dd」是技術約束，放 Controller 合理——因為這個規則永遠不會因為 PM 的想法改變。「預設看一年的資料」是產品決策——如果哪天改成「預設看三個月」，你要去改哪一層？Service。那它就屬於 Service 的商業邏輯。

```csharp
// Service 內部，Controller 不需要知道這個規則
DateOnly finalEnd   = endDate   ?? DateOnly.FromDateTime(DateTime.Today);
DateOnly finalStart = startDate ?? finalEnd.AddDays(-365);
```

Controller 的 `DateOnly?` 型別參數（可為 null）傳進 Service 後，由 Service 填補預設值，職責邊界清晰。

---

### 關鍵設計決策七：GetPricesAsync 三表 JOIN + 聚合策略

這是五支 API 中邏輯最複雜的一支，有兩個核心設計問題。

第一，三表 JOIN 的必要性：`AgriProductsTrans` 只存了 `CropCode` 和 `MarketCode`，前端需要的 `CropName` 在 `CropInfos`，`marketType` 過濾條件（Veg/Fruit/Flower）在 `MarketInfos`。這三張表之間沒有 EF Core Navigation Property（同 DbContext 但設計上無物理 FK），只能用 LINQ 的 `join...on...equals` 手動連結。Query Syntax 在三表 JOIN 的可讀性顯著優於連鎖的 Method Syntax `.Join().Join()`（後者需要巢狀匿名型別），選擇 Query Syntax。

第二，全台均價時的聚合策略。不選擇特定市場時，同一天同一作物可能在多個市場各自有交易紀錄，需要 `GroupBy(TransDate, CropCode)` 後聚合：價格欄位（`UpperPrice`、`MiddlePrice`、`LowerPrice`、`AvgPrice`）用 `AVG`——價格是一個比率，跨市場平均反映全台價格水準；`TransQuantity` 用 `SUM`——交易量是絕對數字，「全台今天這個作物一共賣了多少公斤」才是有意義的數字，不是「平均每個市場賣了多少」。這個選擇是由業務語意驅動的，不是隨意決定的。

`AsQueryable()` 讓 `marketCode` 的 optional 過濾條件在一個查詢路徑裡動態組合：基礎查詢建立完成後，透過 `if (!string.IsNullOrEmpty(marketCode)) { baseQuery = baseQuery.Where(...) }` 追加條件，最後才 `ToListAsync()` 送出一條完整 SQL，不需要兩條完全分開的 query 分支。

---

### 驗收標準

編譯 0 錯誤。`dotnet run --project TaiwanAgri.Web` 啟動後：

- `GET /api/market/markets?marketType=Veg` 回傳市場清單 JSON（`[{ marketCode, marketName }]`）
- `GET /api/market/crops?marketType=Veg` 回傳有交易記錄的作物清單（無重複、有 CropName）
- `GET /api/market/prices?marketType=Veg&cropCodes=11&cropCodes=459` 回傳歷史價格資料，格式符合 Contract
- `GET /api/market/disasters?startDate=2024-01-01&endDate=2024-12-31` 回傳天災事件列表
- `GET /api/market/restdays?marketCode=104&startDate=2024-01-01&endDate=2024-12-31` 回傳休市日列表
- 傳入格式錯誤的日期（如 `startDate=abc`）回傳 400 且包含 `"格式錯誤，請使用 yyyy-MM-dd"` 的訊息
- Vue 3 dev server（`:5173`）打 Web API 不出現 CORS 錯誤
- Swagger（如已設定）或 Postman 均可正常測試以上端點

---

# PR #021 — W9-10 前端 Market 模組完整實作

**標題**：`feat(frontend+market): W9-10 前端 Market 模組——Vue 3 折線圖介面、天災事件整合、CSV 匯出`

---

## 背景與動機

PR #020 完成了 Market 模組的後端查詢層（MarketService + MarketController），五支 API 端點已驗證可正常回傳資料。本 PR 是 W9-10 的核心工作：從零建立 `TaiwanAgri.Frontend`（Vue 3）的 Market 模組畫面，讓後端資料真正能呈現在使用者眼前。

這個 PR 同時包含一批後端修正——`DisasterResponseDto` 的重設計。在前端實作天災垂直線標記的過程中，發現原本的 DTO 欄位語意不正確、且資料結構不適合前端使用，趁此機會在同一個 PR 裡一起修正，讓後端輸出格式和前端實際需求對齊。

本次工作量涵蓋：

- 前端三層架構從零建立（api / store / component）
- 五支 API 串接驗證
- Chart.js 折線圖 + 7 日移動均線
- 天災事件垂直線 Plugin（自訂 Chart.js afterDraw）
- Chip 作物多選（取代原生 select multiple）
- CSV 匯出（含 UTF-8 BOM）
- 版面排版調整與視覺質感設計

---

## 後端異動——DisasterResponseDto 重設計

### 問題一：欄位語意錯誤

`DebrisAlertRecord` 實體中有兩個 DateTime 欄位：

| 欄位 | 語意 |
|------|------|
| `LastUpdateDate` | 警報發布時間（對應農業部 API 第 12 欄） |
| `CreatedAt` | 資料同步時間（SyncWorker 寫入時間） |

原本的 DTO 直接把 `LastUpdateDate` 回傳為 `DateOnly`，欄位命名帶有誤導性，且前端拿到的是日期物件而非格式化字串，增加前端解析負擔。

### 問題二：資料結構不適合前端

同一個天災事件（例如「0404豪雨」在 2026-04-04 發布警報）在 DB 裡有幾百筆記錄——每個村落一筆。前端的需求是「這個事件影響了哪些縣市」，不是「這個事件影響了哪些村落」。把幾百筆原始資料回傳給前端，前端反而需要在 JavaScript 做 GroupBy，職責放錯地方。

### 問題三：alertDate 參數設計錯誤

Controller 把 `alertDate` 設計為必填 Query 參數，但前端不可能事先知道這個值，導致 API 根本無法被正常呼叫。

### 修正內容

**DisasterResponseDto.cs**——欄位精簡，語意明確：

```csharp
public class DisasterResponseDto
{
    public string DisasterName { get; set; } = string.Empty;
    public string AlertType    { get; set; } = string.Empty;
    public string AlertDate    { get; set; } = string.Empty;   // yyyy-MM-dd，來自 LastUpdateDate
    public List<string> AffectedCounties { get; set; } = new();
}
```

**MarketService.GetDisastersAsync**——Service 層做 GroupBy，前端收到的是去重後的事件清單：

```csharp
return raw
    .GroupBy(d => new { d.DisasterName, d.AlertDate })
    .Select(g => new DisasterResponseDto
    {
        DisasterName     = g.Key.DisasterName,
        AlertType        = g.First().AlertType,
        AlertDate        = g.Key.AlertDate.ToString("yyyy-MM-dd"),
        AffectedCounties = g.Select(x => x.County).Distinct().OrderBy(c => c).ToList()
    })
    .OrderBy(d => d.AlertDate)
    .ToList();
```

**IMarketService / MarketController**——移除 `alertDate` 參數，讓 API 可被正常呼叫。

---

## 前端架構設計

### 三層架構與後端類比

前端採用和後端對稱的三層結構，讓有後端背景的開發者能快速定位各層職責：

| 後端 | 前端 | 職責 |
|------|------|------|
| Controller | Vue 元件 | 接收使用者操作，觸發動作 |
| Service | Pinia Store | 管理共享狀態，協調 API 呼叫 |
| HttpClient in SyncWorker | `src/api/market.ts` | 封裝 HTTP 呼叫，不管理狀態 |

`api/market.ts` 對應後端的 HttpClient 封裝——只負責「打出去、回傳資料」，不持有任何狀態。Store 呼叫它，就像後端 Service 呼叫 Repository 一樣，職責層次清晰。

### 狀態歸屬判斷原則

判斷一個資料應該放在 Store 還是元件本地 `ref`，標準只有一個：**這個狀態是否需要被多個元件共享**。

- `marketType`（蔬菜/水果/花卉）→ Store：MarketFilter 選的，PriceChart 打 API 時也需要
- `selectedCropCodes` → Store：MarketFilter 選的，MarketView 查詢按鈕和清空按鈕都需要讀寫
- Chip 選單的開關狀態（isOpen）→ 元件本地：只有 MarketFilter 自己關心

---

## 關鍵設計決策一：平鋪 prices → Chart.js 格式的轉換放在 PriceChart.vue 內部 computed()

`GetPricesAsync` 回傳的是平鋪陣列，每筆一列：

```json
[
  { "transDate": "2025-01-01", "cropCode": "A01", "cropName": "高麗菜", "avgPrice": 12.5, ... },
  { "transDate": "2025-01-01", "cropCode": "A02", "cropName": "菠菜",   "avgPrice": 18.0, ... }
]
```

Chart.js 折線圖需要每條線有自己的 data 陣列（每個 cropCode 一條線），格式完全不同。這個「從平鋪陣列 → 按 cropCode 分組」的轉換有三個放置選項：

- **Option A：在 PriceChart.vue 內部 computed()**
- Option B：在 MarketView.vue 預處理後傳入
- Option C：在 Pinia Store

選擇 Option A，理由：**顯示格式屬於這個元件自己的職責**。Chart.js 的 dataset 結構是為了「畫折線圖」而存在的，和任何其他元件無關。如果未來加入「表格顯示模式」（同樣的 prices 資料但用表格顯示），Store 裡的 Chart.js 格式資料對表格完全無用，代表這個格式從一開始就不該放在 Store。

類比後端：Service 不負責把資料格式化成特定視圖的格式，那是 Controller（View Model）的職責。PriceChart.vue 就是自己的 Controller。

---

## 關鍵設計決策二：天災垂直線——落在 X 軸沒有的日期時跳過（不找最近交易日）

天災警報日（例如週日颱風登陸）可能剛好是休市日，X 軸 labels 裡不會有這個日期。處理方式有兩種：

- **Option A**：找「最近的交易日」畫線
- **Option B**：那天沒有 label 就直接跳過不畫

選擇 Option B，理由：**Option A 會製造假資訊**。明明颱風是週日發生，垂直線卻畫在週一，使用者會以為颱風影響了週一的市場交易，但這天其實是事後才受到影響的。Option B 讓使用者看到的資訊是真實的——「這段時間有天災，但當天沒有交易記錄」。

```typescript
const idx = labels.indexOf(date)
if (idx === -1) return   // 不在 X 軸，跳過，不製造假資訊
const x = scales['x']!.getPixelForValue(idx)
```

---

## 關鍵設計決策三：Chart.js 自訂 Plugin 的技術選型

天災垂直線不是 Chart.js 內建功能，需要自訂繪圖。選項有：

- **chartjs-plugin-annotation**：第三方套件，需要額外安裝和學習 API
- **Inline Plugin（afterDraw hook）**：直接在 buildChart() 裡定義，使用 Canvas 2D API

選擇 Inline Plugin，理由：需求只有「在特定 X 位置畫垂直線 + 頂部三角 + 旋轉文字」，標準 Canvas API 完全足夠，引入套件只會增加套件相依。`afterDraw` 在 Chart.js 完成所有資料繪製後才執行，確保垂直線覆蓋在圖表上方而非被資料線遮住。

```typescript
const disasterPlugin = {
  id: 'disasterLines',
  afterDraw(chart: Chart) {
    disasterLines.forEach(({ name, date }) => {
      const idx = labels.indexOf(date)
      if (idx === -1) return
      const x = scales['x']!.getPixelForValue(idx)
      // 畫垂直虛線、頂部三角、旋轉文字
    })
  }
}
```

---

## 關鍵設計決策四：Chip 作物多選取代原生 select multiple

原生 `<select multiple>` 需要按住 Ctrl 才能多選，在農業資料查詢的使用情境下體驗極差——使用者不是開發者，不知道要按 Ctrl。

改為 Chip 點擊式選擇，核心邏輯只有一個 `toggleCrop` 函式：

```typescript
function toggleCrop(cropCode: string) {
  const idx = store.selectedCropCodes.indexOf(cropCode)
  if (idx >= 0) {
    store.selectedCropCodes.splice(idx, 1)   // 已選 → 取消
  } else if (store.selectedCropCodes.length < 5) {
    store.selectedCropCodes.push(cropCode)   // 未選且未滿 → 加入
  }
}
```

同時新增「關鍵字搜尋」——`filteredCrops` 是前端 `computed()` 對 `store.crops` 做過濾，不打 API：

```typescript
const filteredCrops = computed(() =>
  cropSearch.value.trim() === ''
    ? store.crops
    : store.crops.filter(c => c.cropName.includes(cropSearch.value.trim()))
)
```

---

## 關鍵設計決策五：CSV 匯出的架構分層

CSV 匯出這個動作由三個部分組成：

1. 讀取 `prices` 資料（來源是元件本地 ref，不在 Store）
2. `prices 陣列 → CSV 字串`（純資料轉換，無副作用）
3. 觸發瀏覽器下載（UI 行為，操作 DOM）

判斷各部分歸屬：

| 部分 | 歸屬 | 理由 |
|------|------|------|
| prices → CSV 字串 | `src/utils/exportCsv.ts` 純函式 | 無狀態、可重用、可單獨測試 |
| 觸發下載 | MarketView.vue method | DOM 操作是 UI 職責，Store 不該碰 DOM |
| 資料來源 | 元件本地 `prices.value` | 查詢結果存在元件，不在 Store |

**後端類比**：把 CSV 轉換邏輯放進 Store（Service）就像把 `Response.WriteAsync(csv)` 放進 Service 層——Service 不應該直接操作 HTTP Response，這是 Controller 的職責。

CSV 加入 UTF-8 BOM（`\uFEFF`）確保 Excel 在 Windows 上開啟中文不亂碼：

```typescript
const blob = new Blob(['\uFEFF' + csvContent], { type: 'text/csv;charset=utf-8;' })
```

---

## 關鍵設計決策六：Promise.all 並行打兩支 API

使用者按下查詢，需要同時取得：
- `GetPrices`（農產品價格資料）
- `GetDisasters`（天災警戒紀錄）

兩支 API 沒有先後依賴關係，用 `Promise.all` 並行呼叫，節省一半等待時間：

```typescript
const [priceResult, disasterResult] = await Promise.all([
  marketApi.getPrices({ ... }),
  marketApi.getDisasters({ ... }),
])
```

任何一支失敗時 `Promise.all` 整體 reject，透過 `try/catch` 統一處理，不需要分開寫兩個 loading 狀態。

---

## 版面問題根本原因——Vite demo 樣式干擾

初始版面只佔左半邊畫面，排查後發現根本原因在 `src/assets/main.css`——這是 Vite 樣板預設生成的 demo 樣式：

```css
/* 元凶一：限制最大寬度並置中 */
#app {
  max-width: 1280px;
  margin: 0 auto;
}

/* 元凶二：強制 flexbox 垂直置中 + 兩欄 grid */
@media (min-width: 1024px) {
  body { display: flex; place-items: center; }
  #app { grid-template-columns: 1fr 1fr; }
}
```

修正方式：將 `main.css` 替換為兩行：

```css
@import './base.css';
#app { width: 100%; min-height: 100vh; }
```

**教訓**：Vite 樣板包含針對 demo 頁面設計的 CSS，在新專案開始時應立即清除，不然隨著元件增加，排版問題越來越難追蹤根本原因。

---

## 最終版面結構

```
.market-view（width:100%, padding:36px 56px, min-width:960px）
├── .filter-section（全寬）
│   ├── MarketFilter（Tab + 市場下拉 + Chip 多選 + 搜尋框）
│   ├── DateRangePicker + 快捷按鈕
│   └── [查詢價格] [匯出 CSV] [清空作物]
└── .bottom-grid（grid-template-columns: 1fr 280px）
    ├── .chart-section（PriceChart — 折線 + 均線 + 天災垂直線）
    └── .disaster-section（天災面板 — 事件清單，合併日期區間 + 受影響縣市）
```

---

## 驗收標準

- `npm run dev` 後，瀏覽器開啟 `http://localhost:5173` 可見完整 Market 介面
- Tab 切換（蔬菜/水果/花卉）自動重打 GetMarkets + GetCrops，下拉選單更新
- Chip 點擊選取作物，最多 5 個；關鍵字搜尋即時過濾（不打 API）
- 查詢後折線圖出現，每個作物一條主線 + 一條 7 日均線虛線
- 天災警戒紀錄面板顯示去重後的事件清單（按 DisasterName 合併，顯示首末日期 + 受影響縣市）
- 圖表上的天災垂直線只出現在 X 軸有的日期，休市日對應的天災日期不強制畫線
- 匯出 CSV 下載後以 Excel 開啟，中文欄位名稱不亂碼
- `GET /api/market/disasters?startDate=...&endDate=...` 回傳去重後的事件清單，每筆包含 `alertDate` 和 `affectedCounties`
- 傳入格式錯誤的日期回傳 400

---

## 檔案異動清單

### 後端修改

| 檔案 | 異動類型 | 說明 |
|------|----------|------|
| `Dtos/ApiResponses/DisasterResponseDto.cs` | M | 重設計：移除 LastUpdateDate / County / Town / AlertLevel，新增 AlertDate（string） + AffectedCounties（List\<string\>） |
| `Services/IMarketService.cs` | M | GetDisastersAsync 移除 alertDate 參數 |
| `Services/MarketService.cs` | M | GetDisastersAsync 重寫：GroupBy 去重 + 聚合縣市 |
| `Controllers/MarketController.cs` | M | GetDisasters 移除 alertDate Query 參數與驗證 |

### 前端新增 / 修改

| 檔案 | 異動類型 | 說明 |
|------|----------|------|
| `src/api/market.ts` | A | axios 封裝 + DTO interface 定義 |
| `src/stores/market.ts` | A | Pinia Store（marketType / markets / crops / selectedCropCodes） |
| `src/components/MarketFilter.vue` | A | Tab + 市場下拉 + Chip 多選 + 關鍵字搜尋 + 捲動框 |
| `src/components/DateRangePicker.vue` | A | 日期範圍選擇器（v-model:startDate / endDate + 快捷按鈕） |
| `src/components/PriceChart.vue` | A | Chart.js 折線圖 + 7 日均線 + 天災垂直線 Plugin |
| `src/views/MarketView.vue` | A | 主頁面（篩選全寬 + 圖表/天災並排） |
| `src/utils/exportCsv.ts` | A | CSV 匯出純函式（UTF-8 BOM） |
| `src/router/index.ts` | M | / → /market 路由設定 |
| `src/assets/main.css` | M | 清除 Vite demo 樣式 |
| `src/assets/base.css` | M | 深色主題色彩變數 |
| `App.vue` | M | 純 RouterView + 全域樣式（農業 × 科技背景漸層） |
| `package.json / package-lock.json` | M | 新增 chart.js、axios 相依 |

### 刪除（Vite 樣板預設檔案）

`HelloWorld.vue` / `TheWelcome.vue` / `WelcomeItem.vue` / `AboutView.vue` / `HomeView.vue`

---

# PR #022 — W11 RBAC 骨架 + Navbar 動態三欄結構

**標題**：`feat(core+web+frontend): W11 RBAC 骨架 + Navbar 動態三欄結構——NavModule Entity、DbInitializer Seed、NavService 三段式查詢、Vue MDI Navbar`

---

## 背景與動機

PR #021 完成了 Market 模組的前端頁面（折線圖 + 天災面板 + CSV 匯出），整個 Market 模組的前後端已成一體。這個 PR 標誌著開發策略的轉折點：從「模組逐一完成」進入「建立全域骨架」階段。

在實際上線的農業資訊系統中，使用者看到什麼功能，由他的身份（訪客 / 管理員 / 付費會員）決定。這個 PR 完成的是讓這件事能夠運作的最小骨架：

**後端 RBAC**：定義「哪個角色可以看哪些模組」的資料結構，並在程式啟動時自動 Seed 初始資料，讓 `GET /api/nav/modules` 能根據使用者身份動態回傳他能看到的模組清單。

**前端 Navbar**：從固定寫死的選單，改為從 API 動態撈取模組清單，根據路由高亮對應頁籤，並在 SideNav 顯示當前模組的子功能，整個 Navbar 不再有任何硬編碼的路由或模組名稱。

---

## 後端實作

### Entity 設計

#### NavModule — 自參照層級結構

```csharp
public class NavModule
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;   // 含前置斜線，如 /market
    public string Icon { get; set; } = string.Empty;    // MDI class name，如 mdi-chart-line
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public int? ParentId { get; set; }

    public NavModule? Parent { get; set; }
    public ICollection<NavModule> Children { get; set; } = new List<NavModule>();
    public ICollection<RoleModulePermission> RoleModulePermissions { get; set; } = new List<RoleModulePermission>();
}
```

頂層模組（`ParentId = null`）和子功能（`ParentId = 父層 Id`）用同一張表管理，靠自參照 FK 區分層級。EF Core 透過命名慣例自動推導 `ParentId ↔ Parent ↔ Children` 三者的關係，`OnDelete(Restrict)` 確保有子功能的模組不能直接刪除。

> **命名決策**：原本命名為 `Module`，與 `System.Reflection.Module` 撞名導致編譯錯誤，改為 `NavModule`（Navigation Module）語意更清楚，也避免了 using 衝突。

#### RoleModulePermission — 跨 DbContext 邏輯 FK

```csharp
public class RoleModulePermission
{
    public string RoleId { get; set; } = string.Empty;  // 邏輯 FK → AspNetRoles.Id，不建物理 FK
    public int ModuleId { get; set; }
    public bool CanView { get; set; }

    public NavModule NavModule { get; set; } = null!;   // 同 DbContext，可建導覽屬性
}
```

`RoleId` 指向 `AspNetRoles`，但屬於 `ApplicationDbContext` 管轄；`RoleModulePermission` 屬於 `CoreDbContext`。跨 DbContext 不能建物理 FK，因此 `RoleId` 純存字串（GUID），查詢時靠應用層保證關聯正確性。這是本專案「跨 DbContext 用邏輯 FK」原則的又一次應用。

複合主鍵在 `OnModelCreating` 設定：

```csharp
entity.HasKey(r => new { r.RoleId, r.ModuleId });
```

### Migration

```
20260515160820_AddNavModuleAndRoleModulePermission（core schema）
```

兩張表均放在 `core` schema，與 `SyncStates` 一致。`NavModules` 建立自參照 FK + 索引，`RoleModulePermissions` 建立複合 PK 及 `ModuleId → NavModules` 的 Cascade 外鍵（模組刪除時權限記錄一併清除）。

### DbInitializer — Seed 策略選擇

種子資料放在 `DbInitializer.cs`（`TaiwanAgri.Core/Infrastructure/`），不使用 `migrationBuilder.InsertData()`，原因是：

Migration InsertData 將「資料版本」與「Schema 版本」綁在一起——未來新增一個模組或改模組名稱，就需要開一個新 Migration，職責混亂且難以 rollback。`DbInitializer` 的運作方式是在 `Program.cs` 啟動時呼叫，先檢查資料是否已存在再決定是否寫入，靠 `if (!context.NavModules.Any()) return;` 確保冪等性，Portfolio 環境重建多次也不會重複插入。

```csharp
// TaiwanAgri.Web/Program.cs
using (var scope = app.Services.CreateScope())
{
    var coreContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    await DbInitializer.SeedAsync(coreContext, roleManager);
}
```

Seed 內容：四個頂層模組（市場行情、青農戰情室、食安透明網、毛小孩地圖）、九個子功能（Market × 4、Weather × 5），以及 Guest 和 Admin 兩個角色各對 13 個模組的 `CanView = true` 權限記錄，合計 26 筆 `RoleModulePermissions`。

> **RoleManager DI 注入**：`DbInitializer` 需要 `RoleManager<IdentityRole>` 來建立角色並查詢 RoleId。這需要在 Identity 註冊時加上 `.AddRoles<IdentityRole>()`，讓 DI 容器知道要提供 `RoleManager`。放在 Web 層而非 Worker 層，也是因為 `RoleManager` 只有 Web 層有完整的 Identity 服務。

### NavService — 三段式查詢

```
第一段：決定 targetRoleId
  → 未登入：RoleManager.FindByNameAsync("Guest") → .Id
  → 已登入：直接使用傳入的 roleId

第二段：三次 DB 查詢
  → permittedModuleIds（先 ToListAsync，避免 IQueryable 被執行兩次）
  → navModules（ParentId == null，OrderBy SortOrder）
  → childNavModules（ParentId IN topLevelIds AND Id IN permittedModuleIds，OrderBy SortOrder）

第三段：記憶體組裝
  → navModules.Select(nm => new NavModuleDto { Children = childNavModules.Where(c => c.ParentId == nm.Id)... })
```

Controller 負責解析 `User.Claims` 取得 `isAuthenticated` 和 `roleId`，Service 只接收這兩個純值，不碰 `ClaimsPrincipal`，職責邊界清晰。

### NavController

```csharp
[Route("api/nav")]
[ApiController]
public class NavController : ControllerBase
{
    [HttpGet("modules")]
    [AllowAnonymous]                   // 未登入也能打，由 Service 決定用 Guest 還是 Member 權限
    public async Task<IActionResult> GetModules()
    {
        var isAuthenticated = User.Identity?.IsAuthenticated ?? false;
        var roleId = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;
        var modules = await _navService.GetNavModulesAsync(isAuthenticated, roleId);
        return Ok(modules);
    }
}
```

---

## 關鍵設計決策

### 決策一：Seed 策略——DbInitializer vs Migration InsertData

選擇 `DbInitializer`，理由：Migration 的職責是管 Schema（表結構），把資料內容混進去會讓歷史紀錄難以閱讀，而且 rollback 一個 Migration 時資料狀態很難預測。`DbInitializer` 的資料邏輯與 Schema 完全分離，未來新增模組只需修改一個檔案，不需要開新 Migration。Portfolio 重建環境時，`dotnet ef database update` 建表，啟動時自動 Seed，兩步即可完全就緒。

### 決策二：NavModule 兩層結構——自參照（方案 A）vs 拆兩張表（方案 B）

選擇自參照（方案 A）：`RoleModulePermission.ModuleId` 永遠指向同一張 `NavModules` 表，不管是頂層模組還是子功能，FK 乾淨、查詢單純。方案 B 需要兩個 nullable FK 或兩張 Permission 表，nullable FK 是 DB 設計的壞味道，業界慣例也傾向自參照處理兩層層級的需求。

### 決策三：角色不自建——沿用 AspNetRoles

自建 Role 表意味著要再建一張 UserRole 中間表，但 `AspNetUserRoles` 已經存在，等於重複造輪子。沿用 Identity 的角色系統，使用者登入後 `ClaimsPrincipal` 已包含 Role 資訊，`NavService` 透過 `RoleManager.FindByNameAsync` 取得 RoleId，不需要任何額外的使用者-角色關聯查詢。

### 決策四：兩個 DTO vs 一個 DTO

```csharp
// 方案 A（一個 DTO）
public class NavModuleDto
{
    public List<NavModuleDto>? Children { get; set; }  // null 代表子層，但「null 代表子層」需要文件才能理解
}

// 方案 B（兩個 DTO）—— 選擇這個
public class NavModuleDto { public List<NavChildDto> Children { get; set; } = new(); }
public class NavChildDto  { /* 沒有 Children，型別本身就說明它不能再展開 */ }
```

選擇方案 B。型別名稱是自解釋的文件：`NavChildDto` 一看就知道是子層，不需要查 API 規格。更重要的是未來擴充彈性：如果子功能需要加 `BadgeCount`（紅點通知數）而頂層不需要，方案 B 只改 `NavChildDto` 即可，方案 A 只能在唯一的 DTO 上加 `int? BadgeCount`，讓型別開始說謊。

### 決策五：permittedModuleIds 的 ToListAsync 時機

```csharp
// ❌ 錯誤：IQueryable 被使用兩次，打兩次 DB
var permittedModuleIds = _context.RoleModulePermissions.Where(...).Select(r => r.ModuleId);
navModules = ....Where(nm => permittedModuleIds.Contains(nm.Id));      // 第一次執行 SQL
childNavModules = ....Where(cnm => permittedModuleIds.Contains(cnm.Id)); // 第二次執行 SQL

// ✅ 正確：先具現化為 List，之後 Contains 是記憶體操作
var permittedModuleIds = await _context.RoleModulePermissions
    .Where(...).Select(r => r.ModuleId).ToListAsync();
```

`IQueryable` 在真正被消費前不執行 SQL，如果同一個 `IQueryable` 被用在兩個 `Contains` 裡，就是兩次 DB 往返。先 `ToListAsync()` 把結果存成 `List<int>`，之後的 `Contains` 在記憶體中完成，只打一次 DB。

### 決策六：前端 API 結構——巢狀 vs 平鋪

```json
// 巢狀（選擇這個）
[{ "name": "市場行情", "route": "/market", "children": [{ "name": "行情查詢" }] }]

// 平鋪
[{ "name": "市場行情", "parentId": null }, { "name": "行情查詢", "parentId": 1 }]
```

選擇巢狀結構：API 回傳的形狀直接對應 UI 的形狀。`TopNav` 用 `modules.map(m => m)` 渲染頂層頁籤，`SideNav` 用 `modules.find(m => path.startsWith(m.route))?.children` 渲染子功能，沒有任何額外的過濾或重組邏輯。

### 決策七：Vite Proxy 設定

前端開發時，`/api/*` 請求由 Vite dev server 攔截轉發至後端，避免瀏覽器 CORS 問題：

```typescript
// vite.config.ts
server: {
  proxy: {
    '/api': {
      target: 'https://localhost:7147',
      changeOrigin: true,
      secure: false,   // 本地開發憑證不驗證
    }
  }
}
```

這個設定需要重啟 `npm run dev` 才能生效（vite.config 變更不支援 hot reload）。

---

## 前端版面結構

```
App.vue（三欄骨架）
├── <TopNav />
│     ├── Logo（mdi-sprout + 台灣農業平台）
│     ├── 頂層模組頁籤（從 API 撈，useRoute().path 比對高亮）
│     └── 登入按鈕（靜態，待後續 Auth 實作）
└── <div class="content-area">
    ├── <SideNav />（依路由顯示對應模組的 children）
    └── <main><RouterView /></main>
```

`SideNav` 透過 `navStore.currentModule(route.path)` 計算當前模組，再渲染 `currentMod.children`，當路由切換至 `/food-safety` 或 `/pet` 時，因子功能尚未定義，`SideNav` 自動隱藏（`v-if="currentMod && currentMod.children.length > 0"`）。

Icon 格式採 MDI CSS class 字串（`"mdi-chart-line"`），`npm install @mdi/font`，前端用 `<span :class="'mdi ' + module.icon" />` 渲染，DB 存的就是這個 class name，格式簡單且語意清楚。

---

## 驗收標準

- Web 啟動時自動執行 `DbInitializer.SeedAsync`，`NavModules` 13 筆、`RoleModulePermissions` 26 筆正確存入。
- `GET /api/nav/modules`（未帶 Token）回傳 Guest 角色的 4 個頂層模組，每個模組含正確的 `children` 陣列。
- 前端啟動後，`TopNav` 顯示四個頂層模組頁籤，圖示正確渲染（MDI）。
- 切換至 `/market` 時，`SideNav` 顯示行情查詢、天災記錄、休市日查詢、畜禽行情四個子功能。
- 切換至 `/weather` 時，`SideNav` 顯示對應的五個子功能。
- 切換至 `/food-safety` 或 `/pet` 時，`SideNav` 不顯示（無子功能）。
- 路由切換時，`TopNav` 頁籤高亮正確跟著當前路由移動。

---

## 檔案異動清單

### 後端新增

| 檔案 | 說明 |
|------|------|
| `TaiwanAgri.Core/Entities/NavModule.cs` | 自參照 Entity，含 Parent + Children 導覽屬性 |
| `TaiwanAgri.Core/Entities/RoleModulePermission.cs` | 複合 PK，RoleId 邏輯 FK |
| `TaiwanAgri.Core/Dtos/NavModuleDto.cs` | 頂層模組 DTO，含 `List<NavChildDto> Children` |
| `TaiwanAgri.Core/Dtos/NavChildDto.cs` | 子功能 DTO，無 Children（型別即語意） |
| `TaiwanAgri.Core/Services/INavService.cs` | 服務介面，`GetNavModulesAsync(bool, string?)` |
| `TaiwanAgri.Core/Services/NavService.cs` | 三段式查詢實作 |
| `TaiwanAgri.Core/Infrastructure/DbInitializer.cs` | Seed 4 頂層 + 9 子功能 + Guest/Admin 26 筆權限 |
| `TaiwanAgri.Core/Infrastructure/Data/Migrations/…AddNavModuleAndRoleModulePermission.cs` | core schema Migration |
| `TaiwanAgri.Web/Controllers/NavController.cs` | `[AllowAnonymous]` GET /api/nav/modules |

### 後端修改

| 檔案 | 異動內容 |
|------|---------|
| `TaiwanAgri.Core/Infrastructure/Data/CoreDbContext.cs` | 補 `DbSet<NavModule>`、`DbSet<RoleModulePermission>`、`OnModelCreating` 設定 |
| `TaiwanAgri.Web/Program.cs` | `async Task Main`、`AddRoles<IdentityRole>`、`AddDbContext<CoreDbContext>`、`DbInitializer.SeedAsync`、`AddScoped<INavService, NavService>` |

### 前端新增

| 檔案 | 說明 |
|------|------|
| `src/api/nav.ts` | axios 封裝，`NavModule` + `NavChild` interface |
| `src/stores/nav.ts` | Pinia store，`modules`、`loadModules`、`currentModule` |
| `src/components/TopNav.vue` | MDI icon + 動態頁籤 + useRoute 高亮 |
| `src/components/SideNav.vue` | 依路由顯示子功能 children |
| `src/views/PlaceholderView.vue` | 施工中佔位頁（Weather / FoodSafety / Pet 子路由用） |

### 前端修改

| 檔案 | 異動內容 |
|------|---------|
| `src/App.vue` | 改為三欄結構（TopNav + SideNav + RouterView），`onMounted` 觸發 `loadModules` |
| `src/router/index.ts` | 新增 `/weather`、`/food-safety`、`/pet`、weather 子路由 |
| `src/main.ts` | 引入 `@mdi/font/css/materialdesignicons.css` |
| `vite.config.ts` | 新增 `server.proxy`（/api → https://localhost:7147） |
| `package.json` | 新增 `@mdi/font` 相依 |

---

## 閱讀之後：給你的觀察指南

這個 PR 的核心是「動態 vs 靜態」的架構選擇。寫死的 Navbar 很簡單，但每次新增模組就要改程式碼；動態的 Navbar 需要 Entity、Migration、Seed、Service、Controller、前端 Store 一整條鏈，但之後新增模組只需要改 `DbInitializer` 一個檔案，其他都自動跟著更新。

注意 `NavService` 三段式查詢的設計：每一段各自完成一個明確的任務，第一段不碰 DB，第二段全部是 DB 查詢（並且刻意讓 `permittedModuleIds` 先具現化），第三段全部是記憶體操作。這樣的切割讓每一段都容易閱讀和測試，也讓 DB 往返次數是可預測的（固定三次，而不是 N+1）。

`NavChildDto` 沒有 `Children` 屬性這件事，不是疏漏，是設計。型別本身在說：「這一層不能再展開」。這是「型別即文件」的具體體現。

---

# PR #023 — P0 Worker 並發限制 + LastSyncedDate 安全推進 + P1 NavService null guard

**標題**：`fix(worker+core): P0 AgriProductsTransSyncWorker 並發超時修正 + LastSyncedDate 安全推進機制 + Serilog 檔案日誌 + P1 NavService roleId null guard 回退 Guest`

---

## 背景與動機

PR #022 完成了 RBAC 骨架與動態 Navbar，系統進入「全域骨架建立完成」的里程碑。但在這個里程碑之下，有兩個每天都在靜默發生的 Bug，以及一個已標記但未修復的 null 參照問題，一起累積成技術債。

這個 PR 的出發點是「出問題的地方不在新功能，而在已有的東西」：

**P0 — Worker 每天燒掉 75% 的市場資料**：`AgriProductsTransSyncWorker` 用 `Task.WhenAll` 同時對農業部 API 送出 20 個 HTTP 請求。農業部 API 承受不住瞬間壓力，回應時間拉長，最終觸發 60 秒 timeout。結果是 20 個市場裡大約 15 個失敗，但 `SyncState.LastSyncedDate` 依然被推進——系統以為同步完成了，其實每天有約 75% 的市場資料是缺失的。

**P1 — NavService 靜默消失**：已登入用戶的 Role Claim 若因任何原因（Identity 設定問題、Token 異常）缺失，`roleId` 傳入 `null`，服務層不做任何防守，結果是 `RoleModulePermissions` 查不到任何資料，使用者看到完全空白的 Navbar，沒有任何錯誤訊息。這個問題已在 commit `c9c4621` 標記，但未修。

兩個問題一起進這個 PR，因為它們都屬於「每天都在發生但沒有人知道」的靜默故障，而且解法都很小（代碼改動各自在 10 行以內），適合一起收尾。

---

## 實作內容

### P0 修正一：SemaphoreSlim 並發限制

問題的根源是 `Task.WhenAll` 讓所有市場的 HTTP 請求同時打出，等於在農業部 API 門口塞進了 20 個人。解法是加一個「閘門」，同時只允許 5 個請求進行，其餘在 C# 層排隊等待——這正是 `SemaphoreSlim` 的用途。

```csharp
// 宣告在 for 迴圈外，確保整個同步過程共用同一個閘門
var semaphore = new SemaphoreSlim(5);

var rawResults = await Task.WhenAll(marketInfos.Select(async market =>
{
    await semaphore.WaitAsync(stoppingToken);   // 拿到入場券才能繼續
    try
    {
        var url = $"...";
        var json = await _httpClient.GetStringAsync(url, stoppingToken);
        return (Market: market, Json: json, Success: true);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "市場 {Market} 抓取失敗", market.MarketName);
        return (Market: market, Json: string.Empty, Success: false);
    }
    finally
    {
        semaphore.Release();   // 離場，讓下一個進來
    }
}));
```

`semaphore` 宣告在 `for` 迴圈外面是刻意的設計：如果每天重建一個 `SemaphoreSlim`，效果相同，但語意是「這個閘門是整個 Worker 共用的限流機制」，而不是「每一天各自的限制」。`catch (Exception ex)` 而非 `catch (TaskCanceledException ex)` 也是刻意的——只攔 timeout 的話，其他如 `HttpRequestException`（網路斷線）、`JsonException`（API 格式異常）都會讓整個 `Task.WhenAll` 爆掉，影響所有市場。

### P0 修正二：LastSyncedDate 條件推進 + 5 天安全閥

修正並發問題後，還需要修正「即使有失敗，LastSyncedDate 也照推」的邏輯。策略是：

全部成功 → 正常推進 `LastSyncedDate`，兩個 DbContext 都 `SaveChanges`。

有任何失敗 → `dbMarket.SaveChangesAsync`（成功的那幾筆資料不浪費），但 `dbCore` 不執行 `SaveChanges`，`LastSyncedDate` 維持不動，下次 Worker 執行時從同一天重試。

```csharp
if (failedMarkets.Any())
{
    _logger.LogWarning("{Date} 有 {Count} 個市場失敗：{Markets}，LastSyncedDate 維持不更新",
        currentDate, failedMarkets.Count, string.Join(", ", failedMarkets));
    await dbMarket.SaveChangesAsync(stoppingToken);

    // 安全閥：若已落後 5 天仍有失敗，強制推進並留下缺口記錄
    var daysBehind = yesterdayDate.DayNumber - currentDate.DayNumber;
    if (daysBehind >= 5)
    {
        _logger.LogWarning("{Date} 已落後 {Days} 天仍有失敗，強制推進 LastSyncedDate，資料存在缺口",
            currentDate, daysBehind);
        lastSyncState.LastSyncedDate = currentDate;
        lastSyncState.UpdatedAt = DateTime.UtcNow;
        await dbCore.SaveChangesAsync(stoppingToken);
    }
}
else
{
    lastSyncState.LastSyncedDate = currentDate;
    lastSyncState.UpdatedAt = DateTime.UtcNow;
    await dbMarket.SaveChangesAsync(stoppingToken);
    await dbCore.SaveChangesAsync(stoppingToken);
    _logger.LogInformation("{Date} 同步完成", currentDate);
}
```

安全閥的存在是防止「農業部 API 整體掛掉持續超過一周」的無限卡死情境。5 天是一個合理的容忍窗口：給 API 恢復的機會，但不讓 Worker 永久停滯在同一天而落後過多。強制推進時會留下 `Warning` 等級的 log，讓日後手動補資料有據可查。

### 可觀測性：Serilog 檔案日誌

「Worker 執行完關掉視窗，出事了不知道」是一個現實的維運問題。加入 Serilog 的 `WriteTo.File` 之後，每天的執行 log 都會存在 `logs/` 資料夾：

```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File(
        path: "logs/worker-.log",
        rollingInterval: RollingInterval.Day,     // 每天一個新檔案
        retainedFileCountLimit: 60,               // 保留最近 60 天
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
    )
    .CreateLogger();
```

`retainedFileCountLimit: 60` 確保磁碟不會無限成長，保留的 60 天窗口足夠覆蓋大多數的事後調查需求。

### P1 修正：NavService roleId null guard

在 `isAuthenticated = true` 但 `roleId` 為空的情境，改為回退 Guest 權限而非靜默消失。同時注入 `ILogger<NavService>` 讓回退行為可被觀察到：

```csharp
private readonly ILogger<NavService> _logger;

public NavService(RoleManager<IdentityRole> roleManager, CoreDbContext coreDbContext, ILogger<NavService> logger)
{
    _roleManager = roleManager;
    _context = coreDbContext;
    _logger = logger;
}

// GetNavModulesAsync 的 else 分支
else
{
    if (string.IsNullOrWhiteSpace(roleId))
    {
        var guestRole = await _roleManager.FindByNameAsync("Guest");
        if (guestRole == null)
            throw new InvalidOperationException("Guest role not found");
        targetRoleId = guestRole.Id;
        _logger.LogWarning("已登入用戶缺少 Role Claim，回退至 Guest 權限顯示");
    }
    else
    {
        targetRoleId = roleId;
    }
}
```

`IsNullOrWhiteSpace` 比 `IsNullOrEmpty` 更嚴謹，連純空白字串（Token 解析出格式異常的 Claim）也能攔截。回退 Guest 而非拋例外的決策是基於使用體驗：使用者看到一個功能受限的 Navbar，遠好過看到一個完全空白的頁面。

---

## 關鍵設計決策

### 決策一：SemaphoreSlim(5) vs Sequential foreach + delay

有兩種方式可以解決 API 打爆的問題。一是回到 sequential `foreach`，每次請求之間加 500ms delay，簡單但慢，20 個市場 × 500ms 加上實際請求時間，每天約需 15 秒以上；二是保留 `Task.WhenAll` 並加 `SemaphoreSlim(5)`，保有 I/O 並發的效能收益，同時限制瞬間壓力。

選擇 `SemaphoreSlim`。Sequential 是退回到「完全放棄並發」，而問題的根源只是「並發數太高」，不是「並發本身有問題」。5 這個數字可以根據農業部 API 的實際承受能力調整，是一個參數，不是一個硬限制。

### 決策二：失敗時 dbMarket 仍要 SaveChanges

失敗時不推進 `LastSyncedDate` 是清楚的，但「成功的那幾筆要不要存」有兩種選項：一是全部 rollback，保持當天資料一致性（要嘛全有、要嘛全無）；二是成功的先存，失敗的下次補。

選擇先存成功的資料。全部 rollback 的問題是：下次重試時，`existingKeys` 的 HashSet 比對會擋掉已存過的資料，不會重複寫入，所以先存不會產生問題；而 rollback 代表成功抓回來的資料也要放棄，多浪費了一次 API 請求。先存、下次補，符合「盡量減少資料缺漏」的原則。

### 決策三：5 天安全閥的閾值選擇

安全閥的目的是避免農業部 API 長期故障時 Worker 永久卡死。閾值選 5 天而非 3 天的理由是：農業部 API 有時候在週末或國定假日維護，3 天可能會誤觸安全閥並強制推進，留下實際可以補回來的缺口；5 天則給了足夠的緩衝，超過 5 天幾乎可以確定是 API 長期故障而非短暫維護。

### 決策四：NavService null guard 回退 Guest 而非 throw

已登入但缺少 Role Claim 是一種「系統內部的異常狀態」，從技術角度可以拋例外。但從使用者角度，拋例外通常意味著頁面崩潰或空白，使用者得不到任何有意義的回饋。回退 Guest 的好處是：使用者至少能看到系統存在，知道自己登入了，而後台的 `LogWarning` 能讓開發者知道有異常發生。這個決策把「系統穩定性優先於嚴格的狀態一致性」的設計哲學落實在具體的程式碼裡。

---

## 驗收標準

執行 Worker 後，`logs/` 資料夾下產生當天的 `.log` 檔案，且 log 內容中所有市場均顯示成功或「無資料跳過」，不出現 `TaskCanceledException`。

`SyncStates.LastSyncedDate` 在全部市場成功後被推進至昨天的日期；若有部分市場失敗，`LastSyncedDate` 維持不動，下次執行時從同一天重試。

NavService 在 `roleId = null` 的情況下不拋例外，前端 Navbar 顯示 Guest 權限的模組，後台 log 出現 `Warning: 已登入用戶缺少 Role Claim，回退至 Guest 權限顯示`。

Serilog 的 outputTemplate 格式正確（`yyyy-MM-dd HH:mm:ss [LEVEL] Message`），`retainedFileCountLimit` 生效（超過 60 個檔案時自動清理最舊的）。

---

## 檔案異動清單

| 檔案 | 異動內容 |
|------|---------|
| `TaiwanAgri.Worker/AgriProductsTransSyncWorker.cs` | 加入 `SemaphoreSlim(5)` 並發限制；`failedMarkets` 統計；`LastSyncedDate` 條件推進；5 天安全閥 |
| `TaiwanAgri.Worker/Program.cs` | 加入 Serilog `WriteTo.File`，每日滾動，保留 60 天 |
| `TaiwanAgri.Core/Services/NavService.cs` | 加入 `ILogger<NavService>` 注入；`IsNullOrWhiteSpace(roleId)` null guard；回退 Guest + LogWarning |
| `TaiwanAgri.Worker/TaiwanAgri.Worker.csproj` | 加入 `Serilog.Sinks.File` 套件參照 |
| `README.md` | 更新進度說明 |

---

## 閱讀之後：給你的觀察指南

這個 PR 沒有新功能，只有修正——但這類 PR 在工程實務中往往比新功能 PR 更有討論價值。

注意 `SemaphoreSlim` 的位置和 `catch (Exception ex)` 的選擇，這兩個細節各自背後都有一個取捨：閘門宣告在迴圈外是為了讓語意清晰（這是全域限流機制，不是每天重建的局部機制）；攔截所有例外是為了讓單一市場的失敗不會影響其他市場的完整性。每一個「看起來很小的細節」背後都有一個「如果不這樣做，會發生什麼」的問題值得問。

`LastSyncedDate` 的條件推進和 `dbMarket` 先存的決策，也是一個「系統的一致性邊界要劃在哪裡」的問題：不是「要嘛全對要嘛全錯」，而是「能保留的先保留，有缺漏的留下記錄」。這個設計哲學在分散式系統或任何涉及外部 API 的系統裡是反覆出現的主題——最終一致性優先於嚴格一致性，但一致性的邊界必須明確且可追蹤。

`NavService` 的 null guard 則展示了「防禦性編程不是到處加 try-catch」，而是「在系統邊界上明確定義異常輸入的處理策略，讓系統的行為在所有情境下都是可預期的」。

---

# PR #024 — W12 技術債補丁：MarketInfos.MarketType 索引 + GetCropsAsync 診斷閉環

**標題**：`perf(market): W12 MarketInfos.MarketType Migration 索引補丁 + EF Core nvarchar(4000) 診斷閉環 + GetCropsAsync 兩段式查詢保留說明`

---

## 背景與動機

這個 PR 的起點不是今天，而是 5/15 的一次生產障礙排查。

### 5/15 的事件：「資料量最小的反而最慢」

5/15 當天沒有任何程式碼異動，但 `GET /api/market/crops?marketType=Fruit` 開始回傳 500 逾時。蔬菜（578 萬筆）和花卉（355 萬筆）完全正常，只有水果（9.5 萬筆）失敗——資料量最小的反而最慢，這個反直覺的現象是整個診斷的起點。

**第一階段：誤判為統計資料失真**。把失敗的 SQL 直接在 SSMS 帶入 `N'Fruit'` 執行，跑了 62 秒，確認問題在資料庫層。執行 `UPDATE STATISTICS ... WITH FULLSCAN` 和 `DBCC FREEPROCCACHE` 後，查詢暫時恢復，但很快三個類型全部變慢。清掉快取後，蔬菜的大資料量查詢先被執行，SQL Server 編譯了一份「針對大資料量最佳化」的計畫並快取，Fruit 沿用這份計畫後效果極差。這指向了 Parameter Sniffing（參數嗅探）問題。

**第二階段：逐一測試查詢 hint，全部失敗**。依序嘗試了 `OPTION (OPTIMIZE FOR UNKNOWN)`（讓優化器用統計平均值估算）和 `OPTION (RECOMPILE)`（每次執行都重新編譯）——兩個都仍然逾時。此時觀察 EF Core 的 SQL log，出現一行關鍵資訊：

```
Parameters=[p0='?' (Size = 4000)]
```

EF Core 對 C# `string` 型別的參數，一律送出 `nvarchar(4000)` 。`MarketType` 欄位的實際型別是較短的 `nvarchar`，兩者長度不符，SQL Server 放棄使用索引，退回全表掃描。這就是為什麼 SSMS 帶字面值 3 秒、App 傳參數逾時：字面值讓優化器直接知道要比對什麼，參數則讓它只知道「有個 nvarchar(4000) 的值」，連 RECOMPILE 也無法解決這個型別層面的根本問題。

**第三階段：找到繞過路徑，兩段式查詢**。解法是把一支三表 JOIN 拆成兩段：第一段從 `MarketInfos` 拿到具體的 MarketCode 清單，第二段的 `IN` 子句傳這些具體值，完全繞開 EF Core 的 nvarchar(4000) 參數路徑：

```csharp
// Step 1：先拿 MarketCodes（小表，幾筆，結果是具體字串值）
var marketCodes = await _context.MarketInfos
    .Where(m => m.MarketType == marketType)
    .Select(m => m.MarketCode)
    .ToListAsync();

// Step 2：IN (marketCodes) 傳的是具體值，SQL Server 能正確判斷型別
var crops = await _context.CropInfos
    .Where(c => c.CropName != "" &&
                _context.AgriProductsTrans
                    .Where(a => marketCodes.Contains(a.MarketCode))
                    .Select(a => a.CropCode)
                    .Contains(c.CropCode))
    .Select(c => new CropResponseDto { CropCode = c.CropCode, CropName = c.CropName })
    .Distinct()
    .ToListAsync();
```

兩段式有效，最慢約 4 秒，對下拉選單初始化可接受。同一次排查中也發現 `MarketInfos` 完全缺少 `MarketType` 的索引，兩段式 Step 1 目前走全表掃描，記錄為待補技術債。

### 這個 PR 的定位

這個 PR 是 5/15 診斷工作的後續閉環：補上當時確認缺少的索引，並驗證索引加上後原始 JOIN 版能不能恢復（結論是不能，nvarchar(4000) 在索引層面無解，兩段式是正確的長期決策）。

---

## 實作內容

### 步驟一：補 MarketDbContext 索引設定

在 `OnModelCreating` 的 `MarketInfo` entity 區塊加入單欄索引：

```csharp
modelBuilder.Entity<MarketInfo>(entity =>
{
    entity.ToTable("MarketInfos", schema: "market");

    entity.HasIndex(e => new { e.MarketCode, e.MarketName })
            .HasDatabaseName("IX_MarketInfos_MarketCode_MarketName")
            .IsUnique();

    // 新增：MarketType 過濾索引，優化兩段式查詢第一段的效能
    entity.HasIndex(e => new { e.MarketType })
            .HasDatabaseName("IX_MarketInfos_MarketType");
});
```

這個索引讓兩段式的 Step 1（`WHERE m.MarketType = @value`）從全表掃描升級為 Index Seek。`MarketInfos` 資料量本身不大，即時效益有限，但索引建立後任何依 `MarketType` 過濾的查詢都能受益。

### 步驟二：跑 EF Core Migration

```
PM> Add-Migration AddMarketInfosMarketTypeIndex
    -Context MarketDbContext
    -Project TaiwanAgri.Modules.Market
    -StartupProject TaiwanAgri.Worker
```

`-Project` 和 `-StartupProject` 分開指定，是多專案架構的必要設定：`MarketDbContext` 定義在模組層，Connection String 和 DI 設定在啟動層，EF Core 需要知道「Migration 存在哪」和「DI 從哪裡取得」這兩個不同的位置。

產生的 Migration 結構符合預期，`Down` 方法確保索引變更可以回滾：

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.CreateIndex(
        name: "IX_MarketInfos_MarketType",
        schema: "market",
        table: "MarketInfos",
        column: "MarketType");
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.DropIndex(
        name: "IX_MarketInfos_MarketType",
        schema: "market",
        table: "MarketInfos");
}
```

### 步驟三：驗證 JOIN 版，確認無法恢復

索引補完後，嘗試把 comment 掉的 JOIN 版恢復，結果仍然 500 逾時，EF Core SQL log 再次出現 `Parameters=[p0='?' (Size = 4000)]`。這個驗證結果確認了 5/15 的判斷：nvarchar(4000) 問題在索引層面無解，兩段式是正確的長期解法。

### 步驟四：補診斷說明到 GetCropsAsync

在 comment 掉的 JOIN 版旁補診斷說明：

```csharp
// JOIN 版因 EF Core 對 string 參數送出 nvarchar(4000)，與 MarketType 欄位型別不符，
// 導致索引失效（Index Seek 退化為 Table Scan）加上 JOIN 計算，查詢逾時。
// 5/15 診斷確認（條目 112、113），兩段式 IN 查詢繞過型別不符問題，效能約 4 秒以內。
// IX_MarketInfos_MarketType 索引已補入，優化第一段查詢的 MarketType 過濾效能。
```

---

## 關鍵設計決策

### 決策一：索引仍然值得補，即使它無法讓 JOIN 版恢復

補這個索引的動機不是「讓 JOIN 版可用」，而是「讓現有的兩段式更快，並為未來的查詢鋪路」。這是 5/15 排查中已識別的缺失，補上它是閉環診斷工作的必要步驟。「現在資料量小所以不需要索引」是一個常見的錯誤判斷，正確的思考是「這個欄位的查詢模式決定索引的必要性，不是當前的資料量」。

### 決策二：nvarchar(4000) 問題為什麼不在 Entity 層直接修

從技術上，在 Entity 設定中明確宣告欄位型別（`.HasColumnType("nvarchar(20)")`）可以讓 EF Core 送出正確長度的參數，這是讓 JOIN 版可用的最乾淨解法。但它需要修改 Entity 設定、跑額外 Migration、驗證現有資料長度不超限，代價不低；而現有的兩段式已被 5/15 的測試確認有效，效能在可接受範圍。技術債的處理原則不是「能修就修」，而是「代價 vs 收益」——這次的評估是保留兩段式、補好文件，把 Entity 型別宣告留作未來有需要時的選項。

### 決策三：「SSMS 快、App 慢」是診斷 nvarchar(4000) 的特徵信號

5/15 的診斷揭露了一個可重複使用的信號：同一支 SQL 在 SSMS 帶字面值快，但透過 EF Core 傳參數慢，根本原因幾乎可以確定是參數型別問題。在 EF Core SQL log 裡的對應特徵是 `Parameters=[p0='?' (Size = 4000)]`，看到這行，就是 nvarchar(4000) 的問題。即使是 `RECOMPILE`、`OPTIMIZE FOR UNKNOWN` 這類針對執行計畫快取的 hint，在型別不符的情況下也無效，因為問題發生在更底層（型別比對），而不是在計畫選擇層。

---

## 驗收標準

`Update-Database` 執行後，SSMS 查詢 `sys.indexes WHERE object_id = OBJECT_ID('market.MarketInfos')` 能看到 `IX_MarketInfos_MarketType` 出現在結果清單中。

`GET /api/market/crops?marketType=Fruit`（以及 Veg、Flower）在 4 秒以內回傳正確資料，不出現 500 逾時。

`GetCropsAsync` 的 JOIN 版保持 comment 狀態，並帶有說明 nvarchar(4000) 根本原因及參考條目的診斷說明。

---

## 檔案異動清單

| 檔案 | 異動內容 |
|------|---------|
| `TaiwanAgri.Modules.Market/Data/MarketDbContext.cs` | `MarketInfo` entity 設定補 `IX_MarketInfos_MarketType` |
| `TaiwanAgri.Modules.Market/Data/Migrations/20260519150011_AddMarketInfosMarketTypeIndex.cs` | 新增 Migration，`Up` 建索引，`Down` 移除 |
| `TaiwanAgri.Modules.Market/Data/Migrations/20260519150011_AddMarketInfosMarketTypeIndex.Designer.cs` | EF Core 自動產生的 Migration 設計器快照 |
| `TaiwanAgri.Modules.Market/Data/Migrations/MarketDbContextModelSnapshot.cs` | EF Core 自動更新的 Model 快照，反映最新索引設定 |
| `TaiwanAgri.Modules.Market/Services/MarketService.cs` | `GetCropsAsync` 補診斷說明，JOIN 版維持 comment |

---

## 閱讀之後：給你的觀察指南

這個 PR 展示的不只是「加一個索引」，而是一次完整診斷週期的閉環：問題出現（5/15 逾時）→ 診斷根本原因（統計資料失真誤判、Parameter Sniffing 確認、nvarchar(4000) 根本原因找到）→ 找到可行解法（兩段式）→ 識別殘留技術債（缺索引）→ 補齊技術債並驗證（這個 PR）→ 確認長期決策（JOIN 版不恢復，兩段式是正確解）。

5/15 的診斷歷程嘗試了統計資料更新、OPTIMIZE FOR UNKNOWN、RECOMPILE 三個方向，全部失效，最後才找到 nvarchar(4000) 的根本原因。這個「逐一排除假設」的過程不是走彎路，而是效能診斷的正確姿勢，每一個失敗的嘗試都縮小了問題的可能範圍。能在面試中完整敘述這個排查過程，遠比只說「我加了個索引然後它就好了」更有說服力，因為它展示的是系統性的問題解決能力。

`MarketDbContextModelSnapshot.cs` 被自動修改的這件事也值得理解：這個檔案是 EF Core 維護的「當前資料庫 Schema 的 C# 表示」，每次跑 Migration 都會自動更新，不需要手動維護，但每次 PR 都應該包含它，因為它是整個 Schema 狀態的單一真實來源。

---

# PR #025 — 前端收尾補完 + DbInitializer Migration 狀態預檢

**標題**：`feat(frontend+core): PriceChart options 深色主題 + TopNav hover dropdown（含間隙修正）+ SideNav 退場 + DbInitializer Fail-Fast 預檢`

---

## 背景與動機

PR #024 完成了後端的索引技術債，這個 PR 收尾前端的三個待修項目，並補上一個後端的開發體驗改善。

**PriceChart.vue 的 options 是空的**。`buildChart` 裡面有一行 `options: { /* 原本的 options 不變 */ }`，這個注釋代表「之後再補」，但它實際上是一個空物件，Chart.js 全部使用預設值：白色 legend 文字、白底 tooltip、灰色格線。在整個系統的深色主題（`#161c18` 背景）下，每次圖表渲染出來都顯得格格不入，是 Portfolio 展示上最明顯的視覺缺陷。

**SideNav 的定位問題**。SideNav 在 PR #022 作為三欄佈局的一部分引入，但實際使用後發現：左側欄位佔用了相當的水平空間，而子路由導覽可以整合進 TopNav 的 hover dropdown，讓內容區域更寬，圖表的呈現更好。

**DbInitializer 的靜默崩潰**。新人 clone 專案後若忘記跑 Migration，`SeedAsync` 直接對尚不存在的表操作，拋出的是底層的 `Invalid object name 'core.NavModules'` SqlException，沒有任何提示說明是 Migration 問題。這是一個典型的「讓開發者花時間在不必要的排查上」的開發體驗問題。

---

## 實作內容

### PriceChart.vue — options 補完

從空物件換成完整的設定，涵蓋響應式、軸線、tooltip 和 legend 四個面向：

**響應式與尺寸控制**

```typescript
responsive: true,
maintainAspectRatio: false,  // 高度由 .canvas-wrap { height: 400px } 決定
```

`maintainAspectRatio: false` 讓高度控制權交還給 CSS。Chart.js 預設會按畫布長寬比自動縮放高度，在窄視窗下圖表會被壓得很矮；設為 `false` 後，視窗縮放時只有寬度改變，高度維持穩定。

**互動模式**

```typescript
interaction: {
  mode: 'index' as const,  // 滑鼠靠近某 X 位置，同時顯示所有作物當天資料
  intersect: false,         // 不需精準點到線上，靠近就觸發
},
```

`mode: 'index'` 對多作物比較場景特別有用——滑鼠在圖表上移動時，所有作物當天的價格一起出現在 tooltip，不需要分別 hover 到每條線。

**深色主題的軸線、格線、tooltip**

軸線文字色 `rgba(170, 185, 205, 0.55)`，格線 `rgba(255, 255, 255, 0.05)`（極淡白，只是輔助線），tooltip 背景 `rgba(22, 30, 24, 0.92)`（接近背景色的深色半透明）。

**Legend — 使用內建點擊切換**

```typescript
legend: {
  position: 'top' as const,
  labels: {
    color: 'rgba(190, 205, 195, 0.75)',
    font: { size: 12 },
    usePointStyle: true,
    pointStyleWidth: 10,
  },
},
```

Chart.js 內建 legend 點擊切換：點擊任一條線的名稱，那條線就會在圖表上隱藏或顯示，包含主線和 7 日均線都可以獨立控制。這個功能是 Chart.js 預設就有的，不需要任何自訂的 `cropVisibility` ref 或 `toggleCrop` 函式。

### TopNav.vue — Hover Dropdown + 滑鼠間隙修正

每個頂層模組的 tab 包在一個 `.tab-wrapper` div 裡，`mouseenter` 和 `mouseleave` 監聽在 wrapper 上，確保滑鼠從 tab 移到 dropdown 時不觸發 `mouseleave`：

```vue
<div
  class="tab-wrapper"
  @mouseenter="hoveredRoute = mod.route"
  @mouseleave="hoveredRoute = null"
>
  <router-link :to="mod.route" class="tab" ...>...</router-link>
  <div class="tab-dropdown" v-if="mod.children?.length > 0 && hoveredRoute === mod.route">
    <router-link v-for="child in mod.children" ...>...</router-link>
  </div>
</div>
```

**滑鼠間隙問題的修正**是這次最有趣的 bug。原始實作用 `top: calc(100% + 4px)` 在 tab 和 dropdown 之間留了 4px 視覺間距，但這 4px 不屬於任何 DOM 元素，滑鼠穿越時觸發 `mouseleave`，dropdown 消失：

```css
/* ❌ gap 在元素外：滑鼠穿越時觸發 mouseleave */
.tab-dropdown { top: calc(100% + 4px); padding: 6px; }

/* ✅ 間距改成 padding：屬元素內部，不觸發 mouseleave */
.tab-dropdown { top: 100%; padding: 4px 6px 6px; }
```

### App.vue — 佈局簡化

移除 `SideNav` import 和 `<SideNav />` 元件，`.content-area` 的 flex 佈局一起移除，`.main-content` 直接作為 TopNav 之後的唯一內容區。

### SideNav.vue — 退場

子路由導覽功能已整合進 TopNav 的 hover dropdown，以 git delete 處理，保留 git 歷史方便日後查閱或還原。

### DbInitializer.cs — Migration 狀態預檢（Fail-Fast）

在 `SeedAsync` 最前面加三行，把錯誤抓在系統啟動時而不是操作資料表時：

```csharp
public static async Task SeedAsync(CoreDbContext coreContext, RoleManager<IdentityRole> roleManager)
{
    // Fail-Fast：有尚未套用的 Migration 就提早拋出，避免後續操作資料表時出現隱晦的 SqlException
    var pendingMigrations = await coreContext.Database.GetPendingMigrationsAsync();
    if (pendingMigrations.Any())
        throw new InvalidOperationException(
            $"CoreDbContext 有 {pendingMigrations.Count()} 筆尚未套用的 Migration，" +
            $"請先執行 Update-Database 再啟動應用程式。\n" +
            $"待套用：{string.Join(", ", pendingMigrations)}");

    await SeedRoleAsync(roleManager);
    await SeedNavModulesAsync(coreContext);
    await SeedRoleModulePermissionsAsync(coreContext, roleManager);
}
```

`GetPendingMigrationsAsync()` 是 EF Core 內建的方法，回傳所有「已在 Migrations 資料夾裡但尚未套用到資料庫」的 Migration 名稱清單。有任何待套用的 Migration，就提早拋出 `InvalidOperationException` 並列出清單，讓開發者立即知道要跑 `Update-Database`。

改動前後的差異：

| 情境 | 改動前 | 改動後 |
|------|--------|--------|
| 忘記跑 Migration | `Invalid object name 'core.NavModules'`（在深層 call stack 爆出） | 啟動時立即：`CoreDbContext 有 N 筆尚未套用的 Migration，請先執行 Update-Database` |
| 正常啟動 | 不受影響 | 不受影響（`pendingMigrations.Any()` 為 false，直接繼續） |

---

## 關鍵設計決策

### 決策一：使用 Chart.js 內建 Legend 而非自訂控制

這個 PR 的前期曾嘗試自訂圖例面板（`cropVisibility` ref、`toggleCrop` 函式、自訂按鈕 HTML）。自訂版本的代碼量遠超圖表核心邏輯本身，而且有一個隱藏的顏色偏移 bug——隱藏某條線時若從 `datasets` 陣列移除，其他作物的顏色索引跳號，整個色彩對應關係亂掉。

Chart.js 內建 legend 點擊切換：三行設定，零自訂邏輯，功能完全對等。選擇內建是「優先使用框架已有的功能」原則的具體應用——不要造已經有人做好的輪子。

### 決策二：Hover 觸發 vs 點擊觸發 dropdown

Hover 觸發只需要 `mouseenter` / `mouseleave`，邏輯單純；點擊觸發需要監聽 `document.click` 做 outside click 偵測，複雜度更高。對「快速切換子路由」的使用場景，hover 更流暢——滑鼠移過去 dropdown 出現，直接點選，移開後自動消失。選擇 hover 觸發，但需要處理 CSS 間隙問題（見上）。

### 決策三：Fail-Fast vs 讓錯誤在底層爆出

`SeedAsync` 在操作資料表之前先檢查 Migration 狀態，有問題就提早拋出，而不是等到 `context.NavModules.Any()` 的那一刻才因為表不存在而爆炸。Fail-Fast 原則的核心是：「越早發現問題，越容易診斷，錯誤訊息越接近根本原因」。把 `Invalid object name` 換成 `請先執行 Update-Database`，不是讓系統更寬容，而是讓系統更誠實——它知道問題在哪，就直接說。

### 決策四：SideNav 退場而非改良

SideNav 本來可以改成可收合的側欄，但需要追加狀態管理和動畫 CSS，而 hover dropdown 已完全覆蓋它的功能。在一個以圖表為主要內容的系統裡，讓圖表佔滿整個視窗寬度比保留側欄導覽更有價值。退場是正確的決策，不是技術債。

---

## 驗收標準

圖表渲染後，legend 顯示在頂部，點擊任一條線名稱後那條線隱藏/顯示。Tooltip 在深色背景上呈現深色半透明樣式，顯示所有作物當天的價格。

TopNav 各模組 tab hover 後出現 dropdown，滑鼠從 tab 移往 dropdown 過程中 dropdown 不消失，點選子路由後路由正確跳轉。

主內容區佔滿 TopNav 以下的全部寬度，沒有左側欄位。

新人 clone 專案後若忘記跑 Migration，啟動時出現「CoreDbContext 有 N 筆尚未套用的 Migration，請先執行 Update-Database」的明確錯誤，不出現底層 SqlException。

---

## 檔案異動清單

| 檔案 | 異動 | 說明 |
|------|------|------|
| `src/components/PriceChart.vue` | M | 補完 Chart.js options（深色主題、legend 頂部可點擊、tooltip 深色、X 軸完整日期） |
| `src/components/TopNav.vue` | M | 新增 hover dropdown、`hoveredRoute` ref、CSS 間隙修正（`top: 100%` + padding） |
| `src/App.vue` | M | 移除 SideNav import 和元件，佈局從三欄簡化為兩層 |
| `src/components/SideNav.vue` | D | 子路由導覽功能移至 TopNav dropdown，元件退場 |
| `TaiwanAgri.Core/Infrastructure/DbInitializer.cs` | M | `SeedAsync` 加入 `GetPendingMigrationsAsync()` 預檢，Fail-Fast 設計 |

---

## 閱讀之後：給你的觀察指南

這個 PR 有兩個值得反覆咀嚼的工程判斷。

第一個是「走了彎路後選擇回頭」。自訂圖例面板的嘗試失敗，失敗的根本原因不是技術能力不夠，而是「Chart.js 已經把這個功能做好了，我們卻在外面重新實作一遍，還踩到顏色索引偏移的坑」。能識別出「這是造輪子」並果斷放棄，選回最簡單有效的解法，是比「把自訂版本做到能用」更難的判斷。

第二個是 Fail-Fast 的哲學。`DbInitializer` 的改動只有三行，但它改變的是「錯誤被發現的時間點和位置」。底層的 SqlException 出現在操作資料表的瞬間，此時 call stack 已經很深，開發者需要從錯誤往回追溯才能找到是 Migration 問題。Fail-Fast 把這個診斷過程壓縮成零：啟動時就說清楚。「讓錯誤盡可能早、盡可能靠近根本原因地出現」是系統設計的通用原則，不只適用於 Migration 檢查。

---

# PR #026 — W13-14 Redis Cache-Aside + RabbitMQ Publisher/Consumer 骨架

**標題**：`feat(infra): W13-14 Redis Cache-Aside for GetPricesAsync + RabbitMQ Publisher/Consumer 骨架`

---

## 背景與動機

這個 PR 是整個平台第一次引入分散式基礎設施：Redis 和 RabbitMQ。在此之前，每一次對 `GET /api/market/prices` 的請求都會直接打 SQL Server，執行三表 JOIN + GroupBy 聚合。這個端點是整個平台查詢量最高的端點，同樣的查詢條件在短時間內可能被重複呼叫數百次，每次都重跑相同的 SQL 是純粹的浪費。

`docker-compose.yml` 從 W1 起就規劃了 Redis 和 RabbitMQ，但應用程式端一直未串接。本 PR 完成這個串接，讓基礎設施的設計意圖真正落地。

本 Sprint 的目標定義清楚：**做骨架，做對，做可以延伸的**。Redis Cache-Aside 要完整實作並驗證，RabbitMQ Publisher/Consumer 則是骨架，Cache invalidation 邏輯留到 W15 JWT 整合後再強化。

---

## 實作內容

### 一、Redis Cache-Aside（`MarketService.GetPricesAsync`）

Cache-Aside Pattern 的核心邏輯是三步：先查 Redis，命中直接回傳；沒命中才查 SQL；查完把結果寫回 Redis。

**Cache Key 設計**是這個 Pattern 最重要的決策。Key 必須包含所有影響查詢結果的參數，任何一個不同就應該對應不同的 cache 條目。`GetPricesAsync` 有五個參數（`marketType`、`cropCodes[]`、`marketCode?`、`startDate?`、`endDate?`），全部進 Key。

其中有兩個設計細節值得說明。第一，`cropCodes` 在進 Key 之前會先排序後再 Join，確保 `["A01","B02"]` 和 `["B02","A01"]` 產生同一個 Key，命中同一個 cache，而不是產生兩份相同內容的 cache 條目。第二，`startDate` 和 `endDate` 如果使用者未傳入，Service 層會先解析成 `finalStart` 和 `finalEnd`（分別為今天往前 365 天和今天），才用 final 值組 Key。如果用原始的 `null` 組 Key，今天呼叫和明天呼叫會命中同一個 cache，但查出來的日期範圍不同——這是一個隱性的正確性 bug。

```csharp
// Cache Key 格式
// market:prices:{marketType}:{sortedCrops}:{marketCode}:{finalStart}:{finalEnd}
// 例：market:prices:Fruit:A01,B02:101:2025-05-23:2026-05-23

var sortedCrops = string.Join(",", cropCodes.OrderBy(c => c));
var cacheKey = $"market:prices:{marketType}:{sortedCrops}:{marketCode ?? ""}:{finalStart}:{finalEnd}";
```

**TTL 設定 25 小時**。農業部的農產品交易資料是歷史資料（同步昨天的交易記錄），一旦寫入就不會再改變。TTL 的意義不是「資料多久會變」，而是「如果 RabbitMQ 主動通知失敗，最多讓舊 cache 撐多久才自動過期」。設 25 小時而不是 24 小時，是為了避免 Worker 每天凌晨同步完成後，cache 剛好在同步前一刻過期，造成短暫空窗。

**驗證方式**：啟動專案後打 Swagger，進入 redis-cli 執行 `KEYS market:prices:*` 確認 Key 已建立，再執行 `TTL {key}` 確認回傳值接近 90000 秒（25 × 3600）。

### 二、RabbitMQ Publisher（`AgriProductsTransSyncWorker`）

`AgriProductsTransSyncWorker` 每天同步完成後，需要通知 Web 端「資料有更新，可以清掉對應的 cache」。兩個獨立的 Process 之間的非同步通訊，就是 RabbitMQ 的設計場景。

Publisher 邏輯放在 `SyncAgriProductsTransAsync` 成功後、`catch` 之前：只有同步成功才發事件，失敗時不發（因為資料沒有真正更新，清 cache 沒有意義）。

Exchange 選擇 **topic**，而不是 fanout 或 direct。未來可能不只 Web 端要監聽 `agri.market.priceUpdated`，Report Worker、Notification Worker 也可能訂閱。topic 讓每個 Consumer 指定自己關心的 routing key pattern，比 fanout 的「全部廣播」更有彈性。

```csharp
// 骨架階段 payload 是空 JSON，W15 之後會帶上更新的作物代碼和日期範圍
// 讓 Consumer 能做精確 invalidation 而不是全部清除
var body = Encoding.UTF8.GetBytes("{}");
await channel.BasicPublishAsync(
    exchange: "agri.events",
    routingKey: "agri.market.priceUpdated",
    body: body);
```

### 三、RabbitMQ Consumer 骨架（`PriceUpdatedConsumer`）

`PriceUpdatedConsumer` 繼承 `BackgroundService`，在 `TaiwanAgri.Web` 啟動時自動連線 RabbitMQ，訂閱 `agri.market.priceUpdated`。

選擇 `IHostedService`（`BackgroundService`）而不是 Controller 或普通 Service，是因為 Consumer 需要「程式啟動就開始監聽，一直保持到程式關閉」的生命週期。Controller 只在有 HTTP request 時才執行，普通 Service 沒有生命週期管理。

Queue 使用**不傳名稱的臨時 Queue**（`amq.gen-xxxx`）。這個選擇對應未來的擴充模式：多個不同的 Consumer（Web 端、Report Worker、Notification Worker）各自有獨立的 Queue，Publisher 發一個訊息，每個 Consumer 都收到一份副本。若使用固定名稱的 Queue，則變成多個相同 Consumer 共用 Queue 做負載平衡，語意完全不同。

`autoAck: false` + 手動 `BasicAckAsync` 確保訊息處理完成後才告知 RabbitMQ 可以刪除。萬一程式在處理過程中崩潰，未 Ack 的訊息會被 RabbitMQ 重新派送，不會遺失。

本 Sprint 的 Cache invalidation 是預留位置（只有 log），W15 會讓 Publisher 帶上具體的更新資訊，Consumer 再根據這些資訊決定清哪些 Key。

### 四、基礎設施調整

`docker-compose.yml` 補上 RabbitMQ 服務，使用 `rabbitmq:3-management` image，同時開放 AMQP port（5672）和 Management UI port（15672）。Management UI 可直接在瀏覽器驗證 Exchange 和 Queue 是否正確建立。

`Program.cs` 補上 `AddStackExchangeRedisCache`（連線字串讀 User Secrets）和 `AddHostedService<PriceUpdatedConsumer>`。

---

## 關鍵設計決策

### 決策一：為什麼用 Cache-Aside 而不是 Read-Through？

Read-Through 是「cache 層自動去 DB 補資料」，應用程式只與 cache 溝通。Cache-Aside 是「應用程式自己負責查 cache、查 DB、寫 cache」，職責更透明。在 ASP.NET Core 的架構下，`IDistributedCache` 不支援 Read-Through，而 Cache-Aside 的三步邏輯清晰易讀、易測試，且能完全控制 cache 的生命週期。對 Portfolio 展示而言，Cache-Aside 比黑盒的 Read-Through 更能在面試中說清楚決策過程。

### 決策二：為什麼 TTL 是 25 小時而不是 24 小時？

剛好 24 小時的問題在於：如果 Worker 在凌晨 2 點跑完，而 cache 是從昨天凌晨 2 點開始計時，剛好 24 小時後過期，就在今天凌晨 2 點——這個時間點 Worker 可能正在跑或剛跑完。多 1 小時的緩衝確保 RabbitMQ 主動 invalidation 優先生效，TTL 只是最後的保底機制。

### 決策三：為什麼現在不做精確 Cache invalidation？

Worker 目前發送的 payload 是空 JSON `{}`，Consumer 收到後無法知道「哪些 cropCode、哪個日期範圍受影響」。精確 invalidation 需要 Publisher 帶上這些資訊，Consumer 根據資訊組出對應的 Key 再刪除。這個邏輯可以做，但它依賴 JWT 整合後確認「哪些使用者的 watchlist 對應哪些 cropCode」，所以合理地推到 W15。骨架階段的 log 佔位，讓整個鏈路可以跑通、可以驗證，是更重要的事。

### 決策四：Connection 為什麼在 StartAsync 建立而不是每次收到訊息時建立？

RabbitMQ 的 Connection 建立是昂貴操作（TCP 握手）。Channel 建立在 Connection 之上，相對輕量。Consumer 的設計是：應用程式啟動時建立一條長連線，之後所有訊息收發都用這條連線，停止時優雅關閉。如果每次收訊息都重新建立 Connection，效能會很差，也不符合 RabbitMQ 的使用慣例。

---

## 驗收標準

Redis 驗收：啟動專案後打 `GET /api/market/prices`，進入 `redis-cli` 執行 `KEYS market:prices:*`，應看到對應的 Key；執行 `TTL {key}` 應看到接近 90000 的數字。

RabbitMQ Publisher 驗收：開啟 RabbitMQ Management UI（`http://localhost:15672`，帳密 guest/guest），在 Exchanges 頁籤確認 `agri.events` 存在，Type 為 `topic`，Feature 有 `D`（durable）。

RabbitMQ Consumer 驗收：啟動 `TaiwanAgri.Web`，啟動 log 出現 `[PriceUpdatedConsumer] 已連線 RabbitMQ，等待事件...`。

---

## 檔案異動清單

| 檔案 | 異動 | 說明 |
|------|------|------|
| `TaiwanAgri.Modules.Market/Services/MarketService.cs` | M | `GetPricesAsync` 加入 Cache-Aside 三步邏輯，注入 `IDistributedCache` |
| `TaiwanAgri.Worker/AgriProductsTransSyncWorker.cs` | M | 同步成功後呼叫 `PublishPriceUpdatedEventAsync`，發布 `agri.market.priceUpdated` |
| `TaiwanAgri.Web/Services/PriceUpdatedConsumer.cs` | A | 新增 `BackgroundService`，訂閱 RabbitMQ，Cache invalidation 預留位置 |
| `TaiwanAgri.Web/Program.cs` | M | 補 `AddStackExchangeRedisCache`、`AddHostedService<PriceUpdatedConsumer>` |
| `docker-compose.yml` | M | 補 RabbitMQ 服務（`rabbitmq:3-management`，port 5672/15672） |
| `TaiwanAgri.Web/TaiwanAgri.Web.csproj` | M | 新增 `Microsoft.Extensions.Caching.StackExchangeRedis`、`RabbitMQ.Client` 套件參考 |
| `TaiwanAgri.Worker/TaiwanAgri.Worker.csproj` | M | 新增 `RabbitMQ.Client` 套件參考 |

---

## 閱讀之後：給你的觀察指南

這個 PR 展示的不只是「把 Redis 和 RabbitMQ 接起來」，而是兩個更深層的工程決策模式。

第一個是**基礎設施的引入時機**。Redis 和 RabbitMQ 從 W1 就在 docker-compose 裡規劃好了，但應用程式端故意推遲到現在才串接。這不是技術債，而是刻意的決策：先把資料同步層和查詢層做正確，確認 SQL 查詢的結構穩定後，再加快取層。如果在 W3 就加 cache，後來 Schema 一改，cache key 設計可能要全部重來。

第二個是**骨架優先的設計哲學**。RabbitMQ Consumer 目前什麼都沒清，只有 log。這個「有跑通、但不完整」的狀態，比「等 W15 功能完整了再一起做」更有價值——因為它讓整個 Worker → RabbitMQ → Web 的鏈路可以在現在就被驗證，而不是累積到 W15 才發現連線設定有問題。分層推進、每層驗收，是這個專案一貫的開發節奏。

`PriceUpdatedConsumer` 裡的 `await Task.Delay(Timeout.Infinite, stoppingToken)` 這行值得特別注意。它讓 `ExecuteAsync` 永遠不結束，直到應用程式關閉。這是 `BackgroundService` 的標準慣用法：事件驅動的 Consumer 不需要輪詢迴圈，只需要一個「保持存活」的機制，讓事件處理器掛在那裡等事件進來。

---

# PR #027 — W13-14 模組2前台：氣象查詢層 + Vue 3 天氣面板 + 通知鈴鐺

**標題**：`feat(weather): W13-14 模組2後端查詢層 + Vue 3 天氣面板完整實作（農場氣象 / 雨量折線圖 / 病蟲害警報牆 / 旬報查詢 / 通知鈴鐺）`

---

## 背景與動機

W3-6 完成了模組2的 Worker 層——WeatherObservations、RainfallObservations、PestAlerts、PestDecadeSummaries、UserNotifications 全部同步到資料庫。但查詢層（Service + Controller）一直缺席，前台更是完全空白（PlaceholderView）。

本 PR 補完整條鏈路：

```
DB（已有資料）→ Service → Controller → Vue 3 前台
```

同時處理一個隱性問題：NavService 從未過濾 `IsActive` 欄位，資料庫停用的選單項目仍會回傳給前端。本 PR 一併修正。

---

## 實作內容

### 一、後端查詢層

#### 1. WeatherService — 兩個查詢方法，各有不同的技術挑戰

**`GetStationsByCityAsync`：每個測站只取最新一筆**

直觀的寫法是 `GroupBy(StationId).Select(g => g.OrderByDescending(ObservedAt).First())`，但 EF Core 無法將這個 LINQ 翻譯成 SQL，執行時拋出 `InvalidOperationException: The LINQ expression could not be translated`。

最終採用兩步式記憶體策略：

```csharp
// 第一步：只撈兩個欄位，資料量小
var raw = await _context.WeatherObservations
    .Where(s => s.CityName == cityName)
    .ToListAsync();

// 第二步：記憶體內 GroupBy，沒有 SQL 翻譯限制
var result = raw
    .GroupBy(s => s.StationId)
    .Select(g => g.OrderByDescending(w => w.ObservedAt).First())
    .Select(s => new WeatherStationResponseDto { ... })
    .ToList();
```

這個設計取捨是刻意的：一個縣市的測站數有限（10-20 個），歷史資料量可控，記憶體代價遠小於引入 `FromSqlRaw` 的維護成本。`ROW_NUMBER()` OVER PARTITION 在 SQL 層是最佳解，但需要放棄整個查詢層的 LINQ 一致性——代價不值得。

**`GetRainfallByCityAsync`：跨表 JOIN + 日期型別轉換**

`RainfallObservation` 只有 `StationId`，城市名稱在 `RainfallStation`。兩張表沒有導覽屬性（因為 Worker 開發時沒有設計雙向導覽），只能用 LINQ `Join`：

```csharp
_context.RainfallObservations
    .Join(_context.RainfallStations,
        obs => obs.StationId,
        sta => sta.StationId,
        (obs, sta) => new { obs, sta })
    .Where(x => x.sta.CityName == cityName
             && DateOnly.FromDateTime(x.obs.ObservedAt) >= finalStart
             && DateOnly.FromDateTime(x.obs.ObservedAt) <= finalEnd)
```

日期參數為 `DateOnly?`，null 時套用預設值（近 14 天）。Controller 層對「有傳但格式錯誤」和「沒傳（null）」做了明確區分：

```csharp
// 有傳但無法解析 → 格式錯誤，回 400
if (startDate != null && start == null)
    return BadRequest("startDate 格式錯誤，請使用 yyyy-MM-dd");

// 沒傳 → 讓 Service 套用預設值，不報錯
```

這個細節避免了「使用者沒填日期也被告知格式錯誤」的 UX 問題。

#### 2. PestService — nullable 城市篩選的 Where 設計

病蟲害警報支援「全台」和「指定縣市」兩種模式，城市參數設計為 `string? cityName = null`：

```csharp
.Where(a => cityName == null || a.Cities.Any(c => c.CityName == cityName))
```

`cityName == null` 時短路求值，條件直接成立，回傳全部資料；有值時才執行 `Cities.Any()`。這個寫法讓同一個方法同時處理兩種情境，不需要 if/else 分支。

`Cities` 和 `Crops` 是導覽屬性（有 `PestAlertCity`、`PestAlertCrop` 兩張關聯表），透過 `Include` 一起載入後直接在 `Select` 裡展平成 `List<string>`：

```csharp
Cities = pa.Cities.Select(c => c.CityName).ToList(),
Crops  = pa.Crops.Select(c => c.CropName).ToList()
```

EF Core 在有 `Select` 時可以省略 `Include`，但加上去更明確；不影響正確性，只是多一個對 Change Tracker 無效的提示。

#### 3. NotificationService — 例外語意 vs. 回傳碼

`MarkAsReadAsync` 有兩種設計選擇：回傳 `Task<int>`（讓呼叫方判斷影響行數）或回傳 `Task` + 例外（Service 自己負責「找不到就報錯」）。

最終選擇例外語意：

```csharp
public async Task MarkAsReadAsync(int notificationId, string userId)
{
    var notification = await _dbContext.UserNotifications
        .FirstOrDefaultAsync(x => x.Id == notificationId && x.UserId == userId);
    if (notification == null)
        throw new KeyNotFoundException($"通知 {notificationId} 不存在或無權限");

    notification.IsRead = true;
    await _dbContext.SaveChangesAsync();
}
```

理由：`SaveChangesAsync()` 的回傳值是「EF 影響幾行」，這是 ORM 的內部細節，不應該洩漏到 Controller 層成為業務判斷依據。Controller 捕捉 `KeyNotFoundException` 回 404，Service 不需要回傳任何值，職責清楚。

#### 4. NotificationController — 暫時的 userId 策略

`[Authorize]` 搭配 `User.FindFirstValue(ClaimTypes.NameIdentifier)` 是生產目標，但 JWT 整合尚未完成。暫時改用 `[FromQuery] string userId`，讓端點在開發期間可測試：

```csharp
// 現在（開發期）
[HttpGet("list")]
public async Task<IActionResult> GetUserNotifications([FromQuery] string userId, ...)

// W15 JWT 整合後改回
[Authorize]
[HttpGet("list")]
public async Task<IActionResult> GetUserNotifications(...)
{
    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (userId == null) return Unauthorized();
    ...
}
```

這個設計讓後端功能可以獨立於前端 auth 完成測試和驗收，不會因為 JWT 未整合而卡住整個開發進度。

#### 5. DTO 分層重組

原本 `Dtos/` 資料夾同時放了給 Worker 用（從農業部 API 反序列化）和給前台用（Service 查詢回傳）的 DTO，混在一起。本 PR 重組為：

```
Dtos/
  ApiResponses/    ← 給前台查詢用（這次新增的 6 個 DTO）
  WorkerResponses/ ← 給 Worker 從農業部 API 反序列化用（原有，搬移）
```

命名以「角色」而非「來源」為依據，讓維護者不需要背景知識就能理解資料夾用途。

#### 6. NavService — 補 IsActive 過濾

原有的查詢從未篩 `IsActive`，導致資料庫停用的選單項目仍然回傳給前端。修正：

```csharp
var navModules = await _context.NavModules
    .Where(nm => nm.ParentId == null
        && nm.IsActive          // ← 補這行
        && permittedModuleIds.Contains(nm.Id))
    .ToListAsync();

var childNavModules = await _context.NavModules
    .Where(cnm => cnm.ParentId != null
        && cnm.IsActive         // ← 補這行
        && topLevelIds.Contains(cnm.ParentId!.Value)
        && permittedModuleIds.Contains(cnm.Id))
    .ToListAsync();
```

停用「智慧提示」（NotificationId=13）後，前端即自動消失，不需要改前端程式碼。RBAC 控制點完全在後端。

---

### 二、前端

#### 1. API 層設計（`weather.ts`）

`notificationApi` 與 `weatherApi` 分開定義，原因是之後加 JWT 時需要在 `notificationApi` 加 Authorization header，分開讓修改範圍最小：

```typescript
export const weatherApi = { ... }    // 公開查詢，不需要 auth
export const notificationApi = { ... } // 個人資料，之後要加 auth header
```

#### 2. StationView — 對應後端的兩步式查詢策略

後端 `GetStationsByCityAsync` 因 EF Core 限制採用「先拉回記憶體再 GroupBy」，這個設計讓前端拿到的永遠是每個測站最新一筆，不需要前端做任何去重。卡片展示 14 個欄位（溫度、日最高/最低、濕度、風速、風向、最大陣風、24h 雨量、日照時數、氣壓），全部標示 nullable——後端欄位是 `decimal?`，前端顯示時用 `?? '—'` 處理。

#### 3. RainfallView — Chart.js 折線圖設計決策

雨量資料的特性：同一縣市多個測站 × 多個時間點，適合「多條折線按測站分線」的顯示方式。

X 軸對齊策略：把所有 `observedAt` 截成 `YYYY-MM-DD HH:mm` 後去重排序，作為共用時間軸。同一測站同一時間點取值，時間點不存在時填 `null`（Chart.js 的 `spanGaps: true` 會自動連線跳過 null）。

**指標切換（3h / 6h / 12h / 24h）**讓使用者判斷降雨型態——短時間強降雨（3h 高、24h 不高）vs. 長時間穩定降雨（各時距都高）。指標切換不需要重新打 API，只需要重繪圖表，資料已全部在記憶體中。

**全不選按鈕**讓使用者從空白狀態開始，逐一點 legend 加回自己關心的測站，解決測站數多時圖表線條雜亂的問題：

```typescript
function toggleAllSeries() {
    if (!chartInstance) return
    const meta = chartInstance.data.datasets.map((_, i) =>
        chartInstance!.getDatasetMeta(i)
    )
    meta.forEach(m => { m.hidden = allVisible.value })
    allVisible.value = !allVisible.value
    chartInstance.update()
}
```

#### 4. PestAlertsView — 警報牆的展開設計

警報的 `Body` 和 `Prescription` 可能很長，不適合直接展開在卡片列表裡。採用「點擊展開」模式，每次只有一筆展開（`expandedId` 是單一值，不是陣列），避免畫面同時展開多筆造成閱讀困難。

縣市和作物標籤用不同顏色區分（藍色 vs 綠色），讓使用者一眼識別影響範圍。

#### 5. NotificationBell — 三個機制的組合

```
① 60 秒輪詢 fetchUnreadCount()     → 紅點即時反映
② dropdown 開啟時 fetchNotifications(reset=true)  → 每次開啟都刷新
③ 捲動到底 fetchNotifications()    → 無限分頁載入
```

點外部關閉用 `document.addEventListener('click', handleOutsideClick)` + `wrapperRef.contains(e.target)` 判斷，在 `onUnmounted` 時移除事件監聽，防止記憶體洩漏。

---

## 關鍵設計決策

### 決策一：EF Core GroupBy + First() 的取捨

「每個測站只取最新一筆」的 SQL 最佳解是 `ROW_NUMBER() OVER (PARTITION BY StationId ORDER BY ObservedAt DESC)`，但 EF Core LINQ 無法直接表達這個語意。

評估了三個選項：

| 方案 | 優點 | 缺點 |
|------|------|------|
| `FromSqlRaw` 原生 SQL | SQL 最佳效能 | 破壞 LINQ 一致性，難以測試 |
| 兩個 Contains HashSet | 保持 LINQ | HashSet 配對有邏輯漏洞（StationId 和 ObservedAt 獨立篩選，可能匹配到不對應的組合） |
| 記憶體內 GroupBy | 正確、簡單 | 拉回更多資料 |

選擇記憶體內 GroupBy。在縣市規模的資料量下（一個縣市幾千筆觀測），記憶體代價可接受，正確性有保證，程式碼最易讀。

### 決策二：通知鈴鐺 dropdown vs. 跳頁

W3-6 設計的路由有 `/weather/notifications`，原本預期是一個完整的通知頁面。本 PR 改為 dropdown，原因：

通知的主要使用場景是「快速確認有沒有新東西」，dropdown 不需要離開目前的工作頁面。跳頁的代價（失去上下文、需要回上一頁）高於收益。參考 GitHub、Slack、Gmail 的設計，通知幾乎都是 dropdown，不是獨立頁面。

`/weather/notifications` 路由保留（資料庫 `IsActive = 0`），未來若有「通知管理」需求再啟用。

### 決策三：userId 的暫時策略

`[Authorize]` 是安全正確的設計，但 JWT 整合在 W15。提前加 `[Authorize]` 的話，整個通知功能在 W15 之前完全無法測試和驗收。

選擇暫時用 `[FromQuery] string userId`，讓後端功能可以獨立驗收，JWT 整合後只需要改 Controller 的取值方式，不需要改 Service 層的任何邏輯（Service 方法簽名不變）。

### 決策四：雨量前台用表格還是純圖表

API 回傳的雨量資料有四個時距（3h/6h/12h/24h），若只做圖表，使用者需要切換才能看到不同時距的絕對值。若只做表格，資料多時趨勢不直觀。

最終兩者都保留：折線圖在上（趨勢、視覺比較），表格在下（精確數值、可複製），各自有不可替代的場景。

---

## 驗收標準

後端：
- `GET /api/Weather/stations?cityName=臺北市` → 回傳臺北市所有測站最新一筆，每個 `stationName` 不重複
- `GET /api/Weather/rainfall?cityName=臺北市&startDate=2026-05-01&endDate=2026-05-25` → 回傳對應區間資料
- `GET /api/Pest/alerts` → 回傳最新 20 筆警報，`cities` 和 `crops` 是展平的字串陣列
- `GET /api/Pest/pest-names` → 回傳不重複的害蟲名稱清單
- `GET /api/Pest/decade-density?pestName=東方果實蠅` → 回傳旬密度資料

前端：
- 點「農場氣象」→ 選縣市 → 查詢 → 出現測站卡片
- 點「雨量趨勢」→ 選縣市和日期 → 查詢 → 折線圖 + 表格出現，指標切換正常
- 點「病蟲害警報」→ 出現警報卡片，點擊展開 Body 和 Prescription
- 點「旬報查詢」→ 選害蟲 → 查詢 → 折線圖 + 明細表格
- 右上角鈴鐺顯示，點擊開啟 dropdown（目前無真實資料，需傳入有效 userId 測試）

---

## 檔案異動清單

| 檔案 | 異動 | 說明 |
|------|------|------|
| `TaiwanAgri.Core/Services/NavService.cs` | M | 補 `IsActive` 過濾條件 |
| `TaiwanAgri.Modules.Weather/Dtos/ApiResponses/WeatherStationResponseDto.cs` | A | 14 個氣象欄位，全 nullable |
| `TaiwanAgri.Modules.Weather/Dtos/ApiResponses/RainfallResponseDto.cs` | A | 四時距雨量 DTO |
| `TaiwanAgri.Modules.Weather/Dtos/ApiResponses/PestAlertResponseDto.cs` | A | 警報 DTO，Cities/Crops 展平為 `List<string>` |
| `TaiwanAgri.Modules.Weather/Dtos/ApiResponses/PestDecadeSummaryResponseDto.cs` | A | 旬密度 DTO |
| `TaiwanAgri.Modules.Weather/Dtos/ApiResponses/UserNotificationResponseDto.cs` | A | 含 RuleName（來自 PestRuleConfig） |
| `TaiwanAgri.Modules.Weather/Dtos/ApiResponses/UnreadCountResponseDto.cs` | A | 單欄 Count |
| `TaiwanAgri.Modules.Weather/Services/IWeatherService.cs` | A | 兩個查詢方法，日期參數 nullable |
| `TaiwanAgri.Modules.Weather/Services/WeatherService.cs` | A | 兩步式記憶體 GroupBy + RainfallStation JOIN |
| `TaiwanAgri.Modules.Weather/Services/IPestService.cs` | A | 三個方法，cityName nullable |
| `TaiwanAgri.Modules.Weather/Services/PestService.cs` | A | Include 導覽屬性、nullable 城市篩選、分頁 |
| `TaiwanAgri.Modules.Weather/Services/INotificationService.cs` | A | MarkAsReadAsync 回傳 Task（例外語意） |
| `TaiwanAgri.Modules.Weather/Services/NotificationService.cs` | A | KeyNotFoundException 例外、RuleName 從導覽屬性取 |
| `TaiwanAgri.Web/Controllers/WeatherController.cs` | A | 日期格式防禦驗證 |
| `TaiwanAgri.Web/Controllers/PestController.cs` | A | 三個端點 |
| `TaiwanAgri.Web/Controllers/NotificationController.cs` | A | 暫時 [FromQuery] userId |
| `TaiwanAgri.Web/Program.cs` | M | 補三個 Service DI 註冊 |
| `TaiwanAgri.Frontend/src/api/weather.ts` | A | weatherApi + notificationApi 分開定義 |
| `TaiwanAgri.Frontend/src/stores/notification.ts` | A | 輪詢、無限捲動、本地 markAsRead |
| `TaiwanAgri.Frontend/src/components/CitySelector.vue` | A | v-model 共用元件 |
| `TaiwanAgri.Frontend/src/components/NotificationBell.vue` | A | 鈴鐺 + dropdown + 三個機制 |
| `TaiwanAgri.Frontend/src/components/TopNav.vue` | M | 插入 NotificationBell |
| `TaiwanAgri.Frontend/src/router/index.ts` | M | weather 子路由補齊，移除 notifications 路由 |
| `TaiwanAgri.Frontend/src/views/WeatherView.vue` | A | 純路由容器 |
| `TaiwanAgri.Frontend/src/views/weather/StationView.vue` | A | 測站卡片牆 |
| `TaiwanAgri.Frontend/src/views/weather/RainfallView.vue` | A | Chart.js 折線圖 + 指標切換 + 全不選 + 表格 |
| `TaiwanAgri.Frontend/src/views/weather/PestAlertsView.vue` | A | 警報牆 + 點擊展開 + 分頁 |
| `TaiwanAgri.Frontend/src/views/weather/PestDecadeView.vue` | A | 旬密度折線圖 + 全不選 + 表格 |

---

## 閱讀之後：給你的觀察指南

這個 PR 有幾個值得注意的設計層次。

**EF Core 的邊界**是整個後端查詢層最核心的學習點。`ToListAsync()` 之前是 SQL 翻譯模式，之後是 C# 執行模式，兩者的能力邊界非常不同。`GetStationsByCityAsync` 的兩步式策略正是接受這個邊界、在邊界之後做記憶體操作，而不是強行讓 EF Core 翻譯它翻譯不了的語意。

**nullable 城市篩選**的 `Where(a => cityName == null || ...)` 是一個常見但容易被過度設計的場景。用兩個方法（有城市/無城市）也能做到，但同一個 `Where` 的短路求值讓呼叫方不需要知道「全台」和「指定縣市」是兩條不同的路徑，介面更乾淨。

**通知鈴鐺的三個機制**（輪詢紅點、開啟刷新、捲動分頁）各自解決不同的使用情境：紅點要即時但不能太頻繁、列表要開啟就最新、歷史通知要按需載入。三個機制分開處理，不互相干擾。

`onUnmounted` 清理事件監聽的慣例在 `NotificationBell.vue` 裡可以看到完整的實作——如果忘記移除，離開頁面後 `handleOutsideClick` 仍然存在於 document，每次點擊都會嘗試存取已卸載的元件，造成記憶體洩漏。

---

# PR #028 — W19 Market 子模組容器化 + 三子頁面落地 + 全站淺色主題收尾

**標題**：`feat(frontend): W19 MarketView 容器化 + PricesView / DisastersView / RestDaysView 落地 + 全站淺色主題統一`

---

## 背景與動機

這個 PR 是 Market 模組前台架構的收尾工作。在 PR #021 裡，Market 模組的前台是一整塊 `MarketView.vue`，把篩選器、圖表、天災面板全部塞在同一個元件裡。隨著天災查詢、休市日查詢被設計成獨立子路由，這個設計就出現了矛盾——`/market/prices`、`/market/disasters`、`/market/rest-days` 三個子路由全部指向 `PlaceholderView`，而真正的行情查詢邏輯卻跑在 `/market` 這個父路由上。

問題的根本是：**MarketView 同時承擔了「路由容器」和「頁面內容」兩個不相容的角色。**

Weather 模組已經示範了正確的做法——`WeatherView.vue` 只有一行 `<RouterView />`，真正的頁面邏輯分散在 `StationView`、`RainfallView` 等子 View 裡。這個 PR 把 Market 模組對齊這個結構，同時補上淺色主題的全站統一。

---

## 實作內容

### 一、MarketView 容器化

`MarketView.vue` 從一個擁有 300+ 行樣式與邏輯的頁面元件，精簡成只有 `<RouterView />` 的純路由容器。原本的所有內容搬進 `src/views/market/PricesView.vue`，**搬移過程中完整保留了「天災對比」的核心功能**——這是一個設計上的明確決策，因為 PricesView 的定位是「天災與菜價關聯分析」，天災面板是這個頁面的核心價值主張，不是附屬功能。

路由也同步更新，`/` 現在直接跳轉到 `/market/prices`，讓使用者一開啟網站就看到有內容的頁面，而不是空白容器。

### 二、DisastersView — 天災警戒獨立頁

`DisastersView.vue` 是天災記錄的獨立查詢頁，它和 PricesView 裡的天災面板有一個本質差異：PricesView 的天災面板是**輔助工具**（幫你解釋為什麼那段時間的菜價異常），而 DisastersView 是**主角**（讓你直接搜尋某縣市在某段時間有哪些天災記錄）。

設計決策上，DisastersView 加了縣市下拉篩選、總筆數統計卡片、土石流與土石流潛勢的分類計數，以及按事件卡片陳列的結果列表。排列順序是降序（最新的事件在前），而 PricesView 裡的天災面板是升序（配合時間軸方向）——這個不一致是刻意的，因為兩個場景的使用者動機不同。

### 三、RestDaysView — 休市日查詢（月份分組）

`RestDaysView.vue` 有一個值得說明的設計細節：市場下拉選單用的是 `marketApi.getMarkets('Veg')` 來初始化，預設只顯示蔬菜市場。這個選擇是一個已知的妥協——理想做法是讓使用者先選擇 Veg/Fruit/Flower，再根據選擇載入對應市場清單，但這樣會讓篩選區變得複雜。在休市日查詢這個場景下，使用者通常只是想確認某個市場什麼時候沒開，選 Veg 的市場作為預設起點是可接受的近似值。

月份分組是後來補上的需求，做法是一個 `groupedByMonth` computed，把 `restDate` 的前 7 碼（`"2026-01"`）作為分組 Key，再格式化成「2026 年 1 月」的中文標籤。這個 computed 完全在記憶體裡操作，不需要重新打 API。

### 四、全站淺色主題收尾

這次 PR 同時完成了全站的淺色主題統一。受影響的元件包括 `MarketFilter.vue`、`DateRangePicker.vue`、`PriceChart.vue`、`TopNav.vue`、`NotificationBell.vue`、`CitySelector.vue`，以及天氣模組的四個 View（`StationView`、`RainfallView`、`PestAlertsView`、`PestDecadeView`）。

`base.css` 和 `main.css` 是主題的根節點，這裡定義的 CSS 變數（`--surface`、`--border`、`--text-primary` 等）被各個元件引用。這個做法讓主題切換理論上只需要改一個地方——雖然這個專案目前不做深色/淺色切換，但結構是對的。

---

## 關鍵設計決策

### 決策一：天災面板留在 PricesView，不拆走

DisastersView 存在之後，有一個很自然的疑問：「PricesView 裡的天災面板是重複的嗎？應該把它移除，讓使用者去 DisastersView 查嗎？」

答案是不應該。PricesView 的天災面板的核心功能是**視覺疊加**——它不只是列出天災事件，而是讓這些事件出現在折線圖上，變成可以對比的垂直線標記。使用者在 PricesView 上的問題是「這個時間點的菜價為什麼異常高？」，天災面板是回答這個問題的工具。DisastersView 的使用者問題是「這段時間某縣市有哪些天災？」，完全是不同的意圖。

### 決策二：pork 維持 PlaceholderView

`market.ts` 裡沒有任何 pork 相關的 API endpoint，後端 `MarketController` 也沒有對應的路由。`PlaceholderView` 是正確的選擇，而不是硬做一個空殼頁面——空殼頁面會給人「功能壞掉了」的感覺，PlaceholderView 裡的「🚧 開發中...」是誠實的狀態聲明。

### 決策三：路由結構對齊 WeatherView

在這個 PR 之前，Market 和 Weather 的路由結構是不對稱的：Weather 是正確的「容器 + 子 View」結構，Market 是「父路由有內容、子路由是空的」的混亂結構。對齊之後，整個應用程式的路由層有了一致的設計語言，任何新加入專案的開發者都能從 WeatherView 的結構類推 MarketView 應該長什麼樣子。

---

## 驗收標準

進入 `/market/prices`，應看到行情查詢篩選器（含作物 Chip 選擇器）、PriceChart 圖表、右側天災面板。

進入 `/market/disasters`，應看到日期範圍選擇器、縣市下拉、查詢後出現統計卡片與事件卡片列表。

進入 `/market/rest-days`，應看到市場下拉（預載蔬菜市場清單）、日期範圍選擇器，查詢後結果以月份分組呈現。

進入 `/market/pork`，顯示「🚧 開發中...」。

TopNav 的 hover dropdown、鈴鐺通知、天氣模組四個子頁面，外觀皆符合淺色主題。

---

## 檔案異動清單

| 檔案 | 異動 | 說明 |
|------|------|------|
| `src/views/MarketView.vue` | M | 改為純路由容器，移除所有頁面邏輯 |
| `src/views/market/PricesView.vue` | A | 原 MarketView 內容搬入，天災對比保留 |
| `src/views/market/DisastersView.vue` | A | 天災查詢獨立頁，含縣市篩選與統計卡片 |
| `src/views/market/RestDaysView.vue` | A | 休市日查詢，月份分組顯示 |
| `src/router/index.ts` | M | 路由更新，/ 跳轉至 /market/prices |
| `src/assets/base.css` | M | 淺色主題 CSS 變數補齊 |
| `src/assets/main.css` | M | 主題根節點調整 |
| `src/components/MarketFilter.vue` | M | 淺色主題樣式統一 |
| `src/components/DateRangePicker.vue` | M | 淺色主題樣式統一 |
| `src/components/PriceChart.vue` | M | 淺色主題樣式統一 |
| `src/components/TopNav.vue` | M | 淺色主題樣式統一 |
| `src/components/NotificationBell.vue` | M | 淺色主題樣式統一 |
| `src/components/CitySelector.vue` | M | 淺色主題樣式統一 |
| `src/views/weather/StationView.vue` | M | 淺色主題樣式統一 |
| `src/views/weather/RainfallView.vue` | M | 淺色主題樣式統一 |
| `src/views/weather/PestAlertsView.vue` | M | 淺色主題樣式統一 |
| `src/views/weather/PestDecadeView.vue` | M | 淺色主題樣式統一 |

---

## 閱讀之後：給你的觀察指南

這個 PR 最值得深思的地方不是「做了什麼」，而是「怎麼決定邊界在哪裡」。

PricesView 和 DisastersView 之間的天災資料是**共享資料來源、不同呈現目的**。PricesView 用天災資料回答「為什麼」，DisastersView 用天災資料回答「有哪些」。同一份資料，在不同使用者意圖下應該有不同的呈現方式——這個判斷在架構層面的體現，就是「不合併」這兩個頁面。

另一個值得注意的是 RestDaysView 的 `groupedByMonth`。它是一個純 computed，輸入是 `restDays.value`（API 資料），輸出是分組後的陣列，沒有任何副作用。這符合「視圖格式轉換放在 computed，不放在 watch 或 action」的原則——computed 的值由輸入決定，自動重算，不需要手動觸發，也不會有「資料更新了但 computed 忘記更新」的 bug。

整個 Market 模組的前端架構，在這個 PR 之後，和 Weather 模組達到了結構對稱。這種對稱性不只是美學問題，它意味著任何新進開發者只需要理解一套模式，就能在兩個模組裡工作。

---

# PR #029 — W20 畜禽行情（Pork）後端 API + Vue 3 前端完整實作

**標題**：`feat(market): W20 PorkResponseDto + GetPorkAsync Service + GET /api/market/pork + PorkView.vue 多線折線圖 + CORS 修正 + Worker Token 修正`

---

## 背景與動機

Market 模組在 PR #028 完成了蔬果行情（PricesView）、天災查詢（DisastersView）、休市日查詢（RestDaysView）的完整落地，但 `/market/pork` 路由一直指向 `PlaceholderView`。

本 PR 補完畜禽行情的完整鏈路：

```
PorkTrans（已有資料）→ PorkResponseDto → GetPorkAsync → GET /api/market/pork → PorkView.vue
```

同時修正兩個在開發過程中發現的隱性問題：

1. **CORS 問題**：`VITE_API_BASE_URL` 設定為絕對路徑繞過 Vite proxy，Vite dev server port 自動從 5173 跳到 5174 後後端 CORS 不允許，導致所有 API 請求失敗。
2. **Worker CancellationToken 問題**：`AgriProductsTransSyncWorker` 把 Worker 生命週期的 `stoppingToken` 同時傳給 `SemaphoreSlim.WaitAsync()` 和 `HttpClient.GetStringAsync()`，導致某個請求 timeout 後整批請求被連帶取消。

---

## 實作內容

### 一、後端

#### 1. PorkResponseDto

```csharp
public class PorkResponseDto
{
    public DateOnly TransDate { get; set; }
    public string MarketName { get; set; } = string.Empty;
    public decimal ExcludeFreezerAvgPrice { get; set; }
    public decimal ExcludeFreezerAvgWeight { get; set; }
    public int ExcludeFreezerCount { get; set; }
}
```

只回傳「不含冷凍廠」系列，原因是業界通常以 `ExcludeFreezer` 系列作為「市場真實行情」代表——冷凍廠豬隻的價格波動大且不代表一般市場行情。

#### 2. IMarketService.GetPorkAsync

```csharp
Task<List<PorkResponseDto>> GetPorkAsync(
    string? marketName,
    DateOnly? startDate,
    DateOnly? endDate);
```

三個參數全選填。`marketName` 為 null 時回傳全部市場，讓同一支 API 同時服務「全台概覽多線圖」和「單一市場查詢」。

#### 3. MarketService.GetPorkAsync

```csharp
.Where(pm => marketName == null || pm.MarketName == marketName)
```

`marketName == null` 時短路求值，`Where` 條件恆為 true，所有市場通過篩選。EF Core 會把這行翻譯成正確的 SQL——有傳值時加 `AND MarketName = 'xxx'`，沒傳時不加條件。

Pork 不需要 JOIN 其他表，因為 `PorkTrans` 本身就直接存著 `MarketName`（沒有對應的 MarketInfos 表），比 AgriProductsTrans 的三表 JOIN 簡單很多。

#### 4. MarketController GET /api/market/pork

三個參數全選填，沿用 `DateHelper.ParseIsoDate` 做格式驗證：

```csharp
if (startDate != null && start == null)
    return BadRequest("開始日期 格式錯誤，請使用 yyyy-MM-dd");
```

驗證邏輯使用「有傳但解析失敗」才回 400，沒傳（null）讓 Service 套預設值，避免「使用者沒填日期也被告知格式錯誤」的 UX 問題。

#### 5. CORS 問題修正（Program.cs）

```csharp
policy.WithOrigins(
    "http://localhost:5173",
    "http://localhost:5174"   // Vite dev server port 自動 +1 時的備援
)
```

Vite dev server 在 5173 被佔用時會自動改用 5174，後端只允許 5173 就會 CORS 失敗。根本解是把 `VITE_API_BASE_URL` 清空，讓所有請求走 Vite proxy——但後端 CORS 同時補上 5174 作為保護層。

---

### 二、前端

#### 1. market.ts — PorkResponseDto interface 與 getPork()

**TypeScript interface 型別對應規則**：

| C# 型別 | TypeScript 型別 |
|---------|----------------|
| `DateOnly` | `string` |
| `decimal` | `number` |
| `int` | `number` |
| `string` | `string` |

```typescript
export interface PorkResponseDto {
  transDate: string
  marketName: string
  excludeFreezerAvgPrice: number
  excludeFreezerAvgWeight: number
  excludeFreezerCount: number
}
```

ASP.NET Core 預設序列化成 camelCase，所以後端的 `TransDate` 到前端是 `transDate`。

**Axios 的 undefined 自動忽略特性**：

```typescript
getPork(params: {
  marketName?: string
  startDate?: string
  endDate?: string
}): Promise<PorkResponseDto[]> {
  return apiClient
    .get<PorkResponseDto[]>('/api/market/pork', { params })
    .then(res => res.data)
}
```

Axios 會自動忽略值為 `undefined` 的 params，不會把它加進 URL。`marketName` 未傳時，後端收到 null，觸發回傳全部市場的邏輯。不需要手動過濾，直接把 params 物件傳進去就好。這和 `getPrices` 用 `URLSearchParams` 的原因不同——`getPrices` 需要 `cropCodes` 陣列重複的 key，Axios params 物件對陣列的格式處理不是 ASP.NET Core 期待的，才需要手動建構。

#### 2. PorkView.vue — 設計決策

**市場下拉為何不需要 store？**

Pork 和蔬果市場在架構上有根本的不同：

```
蔬果：MarketInfos 表（獨立主檔）→ 先撈清單 → 用戶選市場 → 撈交易資料
豬肉：PorkTrans.MarketName（直接存在交易資料裡）→ 撈交易資料 → 從資料動態萃取市場名稱
```

豬肉沒有獨立的「市場清單」表，市場名稱只存在於交易資料本身，所以不能提前撈清單。市場下拉的選項由 `computed` 動態產生：

```typescript
const availableMarkets = computed(() => {
  const names = rawData.value.map(d => d.marketName)
  return [...new Set(names)].sort()
})
```

`rawData` 一更新，`availableMarkets` 自動重算。下拉選單在「查詢前」是空的（只有「全部市場」），「查詢後」才出現各市場選項。

**Chart.js 多線 groupBy 資料整理**：

API 回傳多個市場、多個日期混在一起的陣列。轉換成 Chart.js 需要的格式分三步：

```
步驟 1：收集所有不重複日期 → X 軸 labels（升冪排列）
步驟 2：按 marketName 分組，建立每個市場的 { 日期 → 數值 } map
步驟 3：每個市場對應一個 dataset，data[] 按 labels 日期順序對齊
```

日期不存在時填 `null`（不是 `0`），搭配 `spanGaps: true` 讓圖表視覺連續。用 `0` 填補代表「那個時間點的量測值是零」，在農業資料語境下（有交易量是 0 vs 根本沒開市）會造成誤導。

**Vue 3 computed 在資料轉換的用途**：

```typescript
const filteredData = computed(() => {
  if (!selectedMarket.value) return rawData.value
  return rawData.value.filter(d => d.marketName === selectedMarket.value)
})

const chartData = computed(() => {
  // 從 filteredData 組 Chart.js datasets
})
```

所有衍生資料（市場清單、過濾結果、圖表資料、統計數字）都是 computed，由 `rawData` 單一資料來源推導而來。用戶切換市場選項時，不需要重新打 API，所有 computed 自動更新。

**操作說明設計**：

市場下拉在「查詢前」設為 disabled，並在下方顯示提示框：

```vue
<!-- 查詢前（藍色框）-->
<div class="query-hint" v-if="!hasQueried">
  請先按「查詢行情」載入資料，查詢完成後可從市場下拉選擇單一市場篩選
</div>
<!-- 查詢後（綠色框）-->
<div class="query-hint success" v-else-if="availableMarkets.length > 0">
  已載入 {{ availableMarkets.length }} 個市場的資料，可從上方下拉選擇單一市場篩選
</div>
```

視覺設計：查詢前藍色框 + 資訊 icon，查詢後換成綠色框 + 打勾 icon。用戶看到灰色下拉 + 提示，自然知道要先查詢。

#### 3. PriceChart.vue exportChartImage 白底修正

Canvas `toDataURL()` 匯出時，canvas 背景透明，存成 PNG 在某些環境下顯示為黑底。修正方式：另建一個暫存 canvas，先鋪白底再疊上原始圖表：

```typescript
const exportCanvas = document.createElement('canvas')
exportCanvas.width = canvas.width
exportCanvas.height = canvas.height
const ctx = exportCanvas.getContext('2d')!
ctx.fillStyle = '#ffffff'
ctx.fillRect(0, 0, exportCanvas.width, exportCanvas.height)
ctx.drawImage(canvas, 0, 0)
```

同樣的問題在 `PorkView.vue` 的 `exportChartImage` 也一並修正。

---

### 三、Worker 修正

#### AgriProductsTransSyncWorker — CancellationToken 問題

**問題根本原因**：

`stoppingToken` 是整個 Worker 的生命週期 token，被同時傳給三件事：

```csharp
// 問題一：Semaphore 等待
await semaphore.WaitAsync(stoppingToken);

// 問題二：HTTP 請求
var json = await _httpClient.GetStringAsync(url, stoppingToken);
```

當任何一個請求因為網路問題產生例外時，`stoppingToken` 的取消狀態可能傳播，導致其他還在 Semaphore 等待的 lambda 被提早取消，表現為「17 秒就 timeout（遠小於 HttpClient 設定的 60 秒）」。

**修正方式**：

```csharp
// Semaphore 等待改用 CancellationToken.None，不受任何外部 token 影響
await semaphore.WaitAsync(CancellationToken.None);

// HTTP 請求改用獨立計時器，和 stoppingToken 解耦
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
var json = await _httpClient.GetStringAsync(url, cts.Token);
```

**Worker/Program.cs timeout 同步調整**：

HttpClient 的 `Timeout` 設定和 CancellationTokenSource 取「最短的那個」生效，因此 `HttpClient.Timeout` 需要大於 `cts` 的 90 秒，設為 120 秒：

```csharp
client.Timeout = TimeSpan.FromSeconds(120);
```

---

## 關鍵設計決策

### 決策一：marketName 選填，一支 API 服務兩種需求

不傳 `marketName` 回傳全部市場，傳入特定市場名稱只回傳該市場。這樣一支 API 同時滿足：
- 前端初次查詢：不帶市場，取得全部市場資料，讓前端動態產生市場下拉
- 用戶篩選後：帶入市場名稱，只取該市場資料（或前端直接 filter 記憶體資料也可）

### 決策二：回傳 ExcludeFreezer 系列，不混用 Total 系列

`PorkTrans` 有兩套數字：
- `TotalTrans` 系列：全部成交，含冷凍廠
- `ExcludeFreezer` 系列：扣掉冷凍廠的成交

業界使用 `ExcludeFreezer` 作為「市場行情」代表，因為冷凍廠豬隻的定價邏輯與一般批發市場不同。Dto 只回傳 `ExcludeFreezer` 系列，避免混用兩套口徑。

### 決策三：市場下拉「查後才顯示」而非「頁面載入就顯示」

蔬果市場可以提前撈清單（MarketInfos 是獨立表，資料與日期無關）。豬肉市場沒有獨立清單，市場名稱來自交易資料，不同日期範圍可能有不同的市場組合（某些市場只在特定時期有資料），所以必須等查詢結果出來後才能產生選項。「查後才顯示」是正確的設計，不是暫時的妥協。

### 決策四：VITE_API_BASE_URL 清空，全走 Vite proxy

原本直打後端（`http://localhost:5258`）是「昨天剛好 port 對上」的僥倖，不是正確設計。清空後，所有 `/api/...` 請求都走 Vite proxy 轉發到 `https://localhost:7147`，完全不受後端 port 變化影響。

---

## 驗收標準

後端：
- `GET /api/market/pork` 不帶參數 → 回傳多個不同 `marketName` 的資料
- `GET /api/market/pork?marketName=南投縣` → 只回傳南投縣的資料
- `GET /api/market/pork?startDate=2026-05-01&endDate=2026-05-30` → 回傳該區間所有市場

前端：
- 進入 `/market/pork`，看到篩選區（DateRangePicker + 灰色市場下拉 + 提示框）
- 按「查詢行情」後，下拉出現各市場選項，多線折線圖顯示
- 切換指標（均價 / 平均體重 / 成交頭數），圖表自動更新
- 選擇單一市場，圖表只顯示該市場一條線
- CSV 匯出正常，圖片匯出為白底 PNG

---

## 檔案異動清單

| 檔案 | 異動 | 說明 |
|------|------|------|
| `TaiwanAgri.Modules.Market/Dtos/ApiResponses/PorkResponseDto.cs` | A | 5 個欄位，ExcludeFreezer 系列 |
| `TaiwanAgri.Modules.Market/Services/IMarketService.cs` | M | 新增 GetPorkAsync 方法簽名 |
| `TaiwanAgri.Modules.Market/Services/MarketService.cs` | M | 實作 GetPorkAsync |
| `TaiwanAgri.Web/Controllers/MarketController.cs` | M | 新增 GET /api/market/pork endpoint |
| `TaiwanAgri.Web/Program.cs` | M | CORS 補上 localhost:5174 |
| `TaiwanAgri.Worker/AgriProductsTransSyncWorker.cs` | M | CancellationToken 解耦修正 |
| `TaiwanAgri.Worker/Program.cs` | M | HttpClient Timeout 改為 120 秒 |
| `TaiwanAgri.Frontend/src/api/market.ts` | M | 新增 PorkResponseDto interface 及 getPork() |
| `TaiwanAgri.Frontend/src/views/market/PorkView.vue` | A | 完整畜禽行情頁面 |
| `TaiwanAgri.Frontend/src/router/index.ts` | M | /market/pork 換成 PorkView |
| `TaiwanAgri.Frontend/src/components/PriceChart.vue` | M | exportChartImage 補白底 |

---

## 閱讀之後：給你的觀察指南

這個 PR 有幾個值得深思的技術點。

**Pork 的架構和蔬果的根本差異**，是整個 PR 最值得記錄的設計層次。蔬果有 `MarketInfos` 主檔表，豬肉市場名稱只存在交易資料裡——這個差異不只影響後端的查詢設計，也直接決定了前端「市場下拉」的實作方式：蔬果用 store（提前載入），豬肉用 computed（查詢後動態產生）。同一個「市場下拉」的 UI 元件，背後的資料流向完全不同。

**Vue 3 computed 的聲明式推導**在 PorkView 裡展示了最乾淨的形態：`rawData` 是唯一資料源，`availableMarkets`、`filteredData`、`chartData`、`maxPrice`、`minPrice` 全部是 computed，不需要 watch，不需要在 `handleQuery` 之後手動更新這些值。Vue 自動追蹤依賴，`rawData` 改變時所有 computed 同步更新。

**CancellationToken 的生命週期設計**是 Worker 修正裡最需要理解的細節。`stoppingToken` 的語意是「整個應用程式要關閉了」，不是「這個請求超時了」。把 `stoppingToken` 傳給 HTTP 請求，意味著「應用程式關閉才取消這個請求」，而不是「請求超時就取消」。正確的做法是給每個 HTTP 請求一個獨立的計時器，兩者不互相干擾。

---

# PR #030 — W21 Code Review 修正：作物選單污染根因修正 + 效能優化 + Bug 修正 + 清理

**標題**：`fix(market/weather/nav/consumer): GetCropsAsync TcType 篩選 + WeatherService 查詢優化 + NavService roleId 錯位修正 + PriceUpdatedConsumer Queue binding 修正 + 清理`

---

## 背景與動機

PR #029（W20 畜禽行情）完成後，進行了一輪全專案 Code Review。這個 PR 針對 Code Review 報告中「需要修改程式碼」的項目逐一修正，同時清除 scaffold 遺留的死碼。

本輪修正橫跨四個子系統，涵蓋資料污染 Bug、效能瓶頸、執行期 Bug、架構問題四種類型。

---

## 實作內容

### 一、GetCropsAsync 作物選單污染根因修正（最重要）

**問題根因**

Code Review 原本推測是 `MarketCode 514` 對應多筆 `MarketInfo` 造成去重問題，但實際查資料庫後發現：

```
514  溪湖鎮  Veg     ← 蔬菜市場
514  彰化市場 Flower  ← 花卉市場

400  台中市   Veg     ← 蔬菜市場
400  台中市場 Flower  ← 花卉市場
```

`MarketCode` 跨 `MarketType` 共用，是政府 API 的來源資料設計，不是 Bug。真正的問題在這裡：

`GetCropsAsync` 原本的兩段式查詢邏輯：

```
Step 1: 查 MarketInfos WHERE MarketType = 'Veg' → 取得 marketCodes 清單（含 400）
Step 2: 查 AgriProductsTrans WHERE MarketCode IN (..., '400', ...)
```

Step 2 的 `IN` 條件用的是 `MarketCode`，而 `AgriProductsTrans` 只有 `MarketCode` 欄位、沒有 `MarketType`。`MarketCode 400` 在蔬菜和花卉各有交易記錄，查蔬菜的 `MarketCode` 清單同時把花卉的作物也撈進來了。

**關鍵發現：TcType 才是真正的類別欄位**

查 `AgriProductsTrans` 的實際資料：

```sql
SELECT DISTINCT TcType FROM market.AgriProductsTrans
```

結果：`N04`（蔬菜）、`N05`（水果）、`N06`（花卉）、`''`（少數特殊資料，CropName 為空，已由 WHERE CropName != '' 過濾）

`TcType` 才是真正對應交易類別的欄位，`MarketCode` 不能作為類別篩選依據。

**修正方式**

新增 `MarketTypeMapping` 常數類別（`TaiwanAgri.Modules.Market/Constants/MarketTypeMapping.cs`）：

```csharp
public static class MarketTypeMapping
{
    private static readonly Dictionary<string, string> _map = new()
    {
        { "Veg",    "N04" },
        { "Fruit",  "N05" },
        { "Flower", "N06" },
    };

    public static string? ToTcType(string marketType)
        => _map.TryGetValue(marketType, out var tcType) ? tcType : null;
}
```

`GetCropsAsync` 改為直接用 `TcType` 過濾：

```csharp
var tcType = MarketTypeMapping.ToTcType(marketType);
if (tcType == null) return new List<CropResponseDto>();

var crops = await _context.CropInfos
    .Where(c => c.CropName != "" &&
                _context.AgriProductsTrans
                    .Where(a => a.TcType == tcType)
                    .Select(a => a.CropCode)
                    .Contains(c.CropCode))
    .Select(c => new CropResponseDto { CropCode = c.CropCode, CropName = c.CropName })
    .Distinct()
    .ToListAsync();
```

**為什麼常數類別放在 Constants 資料夾，而不是在 GetCropsAsync 內部**

`MarketType → TcType` 的對應關係是業務知識，不是某個方法的私有邏輯。將來 `GetPricesAsync` 也需要根據 `MarketType` 做 `TcType` 層面的思考，統一放在常數類別確保定義只有一處。

**同步新增 TcType 索引**

原本查詢效能問題的真正根源是 `AgriProductsTrans` 沒有 `TcType` 單欄索引。既有的複合唯一索引 `(TransDate, TcType, CropCode, MarketCode)` 最左前綴是 `TransDate`，`WHERE TcType = 'N05'` 無法走這個索引，導致幾百萬筆全表掃描，水果的查詢因此 30 秒 Timeout。

```csharp
entity.HasIndex(e => e.TcType)
      .HasDatabaseName("IX_AgriProductsTrans_TcType");
```

Migration：`20260602161355_AddAgriProductsTransTcTypeIndex`，`CREATE INDEX` 執行耗時 14 秒，驗證資料量確實不小，也驗證了之前 Timeout 的成因。

---

### 二、WeatherService.GetStationsByCityAsync 全表載入修正（P1 效能）

**問題**

原本寫法把整個城市所有觀測記錄 `ToListAsync()` 全部載入記憶體後，再在 C# 裡 `GroupBy` 取每個站的最新一筆：

```csharp
var raw = await _context.WeatherObservations
    .Where(s => s.CityName == cityName)
    .ToListAsync();  // ← 全城市 30 天 × 幾十個站 × 小時 = 可能幾千筆全進記憶體

var result = raw
    .GroupBy(s => s.StationId)
    .Select(g => g.OrderByDescending(w => w.ObservedAt).First())
    ...
```

**為什麼 ToListAsync 之後的 GroupBy 無法在 DB 端執行**

`ToListAsync()` 是 EF Core 查詢的 SQL/C# 邊界，之後的操作已脫離 SQL 翻譯模式，在 C# 記憶體中執行。即使 LINQ 語法相同，`ToListAsync()` 之前是 SQL，之後是 C#。

**修正方式：兩段式，但邊界在正確的地方**

```csharp
// Step 1：只在 DB 端計算每個站的最新時間戳，回傳幾十筆
var latestTimes = await _context.WeatherObservations
    .Where(s => s.CityName == cityName)
    .GroupBy(s => s.StationId)
    .Select(g => new { StationId = g.Key, LatestAt = g.Max(w => w.ObservedAt) })
    .ToListAsync();

// Step 2：用 (StationId, ObservedAt) 組合撈完整資料，只撈每站最新一筆
var stationIds = latestTimes.Select(x => x.StationId).ToList();
var latestObservedAts = latestTimes.Select(x => x.LatestAt).ToList();

var result = await _context.WeatherObservations
    .Where(s => stationIds.Contains(s.StationId)
             && latestObservedAts.Contains(s.ObservedAt)
             && s.CityName == cityName)
    .Select(s => new WeatherStationResponseDto { ... })
    .ToListAsync();

// 記憶體端防護：確保每站只保留一筆（資料量為站台數，幾十筆，開銷可忽略）
return result
    .GroupBy(r => r.StationName)
    .Select(g => g.OrderByDescending(r => r.ObservedAt).First())
    .ToList();
```

原本幾千筆 → 現在 Step 1 幾十筆（時間戳）+ Step 2 幾十筆（完整資料）。`GroupBy + Max()` 是 SQL Server 最擅長的操作，有索引時極快。

---

### 三、NavService roleId/roleName 型別錯位修正（B-2 執行期 Bug）

**問題**

`NavController` 從 JWT Claims 取出角色資訊：

```csharp
var roleId = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;
```

但 `ClaimTypes.Role` Claim 儲存的是角色**名稱**（`"Admin"`、`"Guest"`），不是 GUID。`NavService.GetNavModulesAsync` 把這個值當 GUID 去查 `RoleModulePermissions.RoleId`，查不到任何結果，靜默 fallback 到 Guest 權限——已登入用戶的導覽列永遠和訪客相同。

**修正方式**

Controller 層變數改名為 `roleName`，語意準確：

```csharp
var roleName = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;
var modules = await _navService.GetNavModulesAsync(isAuthenticated, roleName);
```

`NavService` 的 else 分支改用 `RoleManager` 解析成真正的 GUID：

```csharp
else
{
    // 傳入的是 role name（"Admin"），需透過 RoleManager 解析成真正的 GUID
    var role = await _roleManager.FindByNameAsync(roleName);
    if (role == null)
    {
        var guestRole = await _roleManager.FindByNameAsync("Guest");
        targetRoleId = guestRole?.Id
            ?? throw new InvalidOperationException("Guest role not found");
        _logger.LogWarning("Role '{RoleName}' 不存在，回退至 Guest 權限顯示", roleName);
    }
    else
    {
        targetRoleId = role.Id;
    }
}
```

`INavService` 方法簽名同步更名，讓 interface 語意清楚：

```csharp
Task<List<NavModuleDto>> GetNavModulesAsync(bool isAuthenticated, string? roleName);
```

---

### 四、PriceUpdatedConsumer Queue Binding Bug 修正（B-3 執行期 Bug）

**問題**

`StartAsync` 宣告了一個臨時 Queue 並 binding 到 exchange，但 `ExecuteAsync` 裡又呼叫了一次 `QueueDeclareAsync()`，產生**另一個不同的** Queue（`amq.gen-xxx2`），對這個沒有被 binding 的 Queue 呼叫 `BasicConsumeAsync`。Worker publish 的 `agri.market.priceUpdated` 事件從來不會被這個 Consumer 收到，Cache invalidation 機制從一開始就是壞的。

**修正方式**

在類別欄位區加入 `_queueName`，`StartAsync` 存下 Queue name，`ExecuteAsync` 重用它：

```csharp
// 欄位
private string _queueName = string.Empty;

// StartAsync：存下宣告好並 binding 過的 Queue name
var queueResult = await _channel.QueueDeclareAsync(cancellationToken: cancellationToken);
_queueName = queueResult.QueueName;  // ← 新增這一行

// ExecuteAsync：不再重新宣告，直接重用
queue: _queueName,  // ← 原本是 (await _channel.QueueDeclareAsync(...)).QueueName
```

一個 Queue、一次 Binding、一次 Consume，鏈路才真正通。

---

### 五、api/weather.ts 重複 RainfallResponseDto interface 修正（C-5）

`RainfallResponseDto` 在 `weather.ts` 第 28 行和第 35 行各定義一次，內容完全相同。TypeScript 不報錯但是維護陷阱——修改一個時容易忘記另一個。移除第二個定義。

---

### 六、清理（C-1 / C-2 / C-3）

| 項目 | 說明 |
|------|------|
| `HomeController.cs` | MVC scaffold 遺留，繼承 ControllerBase 但無任何 Action，刪除 |
| `stores/counter.ts` | Vue scaffold 預設 store，專案中零 import，刪除 |
| `src/components/icons/*.vue`（5 個）| Vue scaffold 遺留，全部未使用，刪除整個資料夾 |

---

## 關鍵設計決策

### 決策一：GetCropsAsync 改用 TcType 而非繼續改良 MarketCode 兩段式

兩段式查詢的設計前提是「同一個 MarketCode 只屬於一個 MarketType」，但資料庫驗證這個前提不成立（MarketCode 400 同時屬於 Veg 和 Flower）。修補兩段式查詢只會治標，根本上應該改用真正能區分類別的欄位：`TcType`。

### 決策二：MarketTypeMapping 作為獨立常數類別

`MarketType → TcType` 的對應關係是業務知識，放在 `GetCropsAsync` 內部會讓這份知識在未來 `GetPricesAsync` 也需要時造成重複。常數類別讓定義只有一處，修改時不會遺漏。

### 決策三：WeatherService 改為兩段式但 GroupBy 留在 DB 端

不選擇「EF Core GroupBy + First() 一次翻譯」的寫法，原因是這個 LINQ pattern 在 EF Core 的 SQL 翻譯穩定性有歷史問題，而兩段式（Step 1 取時間戳、Step 2 取完整資料）的每一段都是 SQL Server 擅長的簡單查詢，更可預測。

### 決策四：NavService 在 Service 層做 RoleManager 解析，不在 Controller 層

Controller 不應該知道「roleName 要轉成 GUID 才能查資料庫」這個內部細節，這是 Service 的職責。Controller 只傳它從 Claim 拿到的值（roleName），Service 負責轉換。

---

## 驗收標準

- `GET /api/market/crops?marketType=Veg` → 只回傳有 N04 交易記錄的作物，不含水果、花卉
- `GET /api/market/crops?marketType=Fruit` → 回傳有 N05 交易記錄的作物，不再 Timeout
- `GET /api/market/crops?marketType=Flower` → 只回傳有 N06 交易記錄的作物
- `GET /api/weather/stations?cityName=臺北市` → 回應時間正常，不再全表載入
- 已登入用戶的導覽列正確顯示 Admin 可見的模組，不再 fallback 到 Guest

---

## 檔案異動清單

| 檔案 | 異動 | 說明 |
|------|------|------|
| `TaiwanAgri.Core/Services/INavService.cs` | M | 方法參數 `roleId` 更名為 `roleName` |
| `TaiwanAgri.Core/Services/NavService.cs` | M | 新增 RoleManager 解析，roleName → GUID |
| `TaiwanAgri.Frontend/src/api/weather.ts` | M | 移除重複的 RainfallResponseDto 定義 |
| `TaiwanAgri.Frontend/src/components/icons/*.vue`（5 個）| D | 刪除未使用 scaffold 元件 |
| `TaiwanAgri.Frontend/src/stores/counter.ts` | D | 刪除未使用 scaffold store |
| `TaiwanAgri.Modules.Market/Constants/MarketTypeMapping.cs` | A | 新增 MarketType → TcType 對應常數類別 |
| `TaiwanAgri.Modules.Market/Data/MarketDbContext.cs` | M | 新增 IX_AgriProductsTrans_TcType 索引定義 |
| `TaiwanAgri.Modules.Market/Data/Migrations/20260602161355_*` | A | AddAgriProductsTransTcTypeIndex Migration |
| `TaiwanAgri.Modules.Market/Data/Migrations/MarketDbContextModelSnapshot.cs` | M | Snapshot 同步更新 |
| `TaiwanAgri.Modules.Market/Services/MarketService.cs` | M | GetCropsAsync 改用 TcType 篩選；GetStationsByCityAsync 改為兩段式 |
| `TaiwanAgri.Modules.Weather/Services/IPestService.cs` | M | 同步清理（簽名調整） |
| `TaiwanAgri.Modules.Weather/Services/PestRuleEngine.cs` | M | 補 P2 N+1 TODO 標記 |
| `TaiwanAgri.Modules.Weather/Services/PestService.cs` | M | 同步清理 |
| `TaiwanAgri.Modules.Weather/Services/WeatherService.cs` | M | GetStationsByCityAsync 兩段式查詢修正 |
| `TaiwanAgri.Web/Controllers/HomeController.cs` | D | 刪除 MVC scaffold 空殼 |
| `TaiwanAgri.Web/Controllers/MarketController.cs` | M | 同步 GetCropsAsync 調整 |
| `TaiwanAgri.Web/Controllers/NavController.cs` | M | roleId 改名 roleName |
| `TaiwanAgri.Web/Controllers/PestController.cs` | M | 同步清理 |
| `TaiwanAgri.Web/Services/PriceUpdatedConsumer.cs` | M | Queue binding bug 修正，ExecuteAsync 重用 _queueName |
| `TaiwanAgri.Worker/WeatherSyncWorker.cs` | M | 移除大段 debug comment（C-4） |

---

## 閱讀之後：給你的觀察指南

這個 PR 最值得思考的是**「Code Review 報告需要被驗證，不是直接照做」**這件事。

原本 Code Review 報告把 B-1（MarketCode 514 重複）標為 Bug，建議加 `DistinctBy`。但實際查資料庫後發現：514 對應溪湖鎮（Veg）和彰化市場（Flower），是兩個真實存在的不同市場，不是重複資料。如果照報告直接加 `DistinctBy`，反而會把其中一個市場從選單裡吃掉，製造新的 Bug。

同理，「水果作物查詢 Timeout」原本被推測為 `IN` 查詢效率問題，但真正根因是 `TcType` 缺少索引，加上查詢本身用了錯誤的篩選欄位（`MarketCode` 而非 `TcType`）。兩個問題同時存在，單獨修任何一個都只治標。

**工程師的診斷習慣**：每一個「看起來是 Bug」的報告，都要先問「假設這是真的，背後的機制是什麼？」把資料攤出來驗證，再決定要不要改，改什麼。

---

# PR #031 — W21 Code Review 修正（第二輪）：命名清理 + 防禦性設計 + 模組化重構 + 前端術語統一

**標題**：`fix(market/weather/web): 命名語意修正 + GetDisastersAsync 防護 + ConvertRocRestDay 職責單一 + Program.cs Modular Extension + CORS 設定外化 + MarketFilter 術語統一`

---

## 背景與動機

PR #030（W21 Code Review 第一輪）完成了作物選單污染根因修正、WeatherService 查詢優化、NavService roleId 錯位修正、RabbitMQ Queue Binding Bug 等執行期問題的修復。本 PR 為第二輪，針對同一份 Code Review 報告中「命名品質」、「防禦性設計」、「架構模組化」、「前端術語一致性」四個維度，逐一完成剩餘修正項目。

本輪修正不涉及任何功能邏輯異動，全部屬於「讓程式碼更容易被讀懂、更容易被維護、更能展示工程師思維」的品質提升。

---

## 實作內容

### 一、MarketService.cs 命名與防禦性設計（M-1 / M-2 / M-3 / M-6）

**M-1：queryPork → porkList**

`GetPorkAsync` 最終結果的變數名稱從 `queryPork` 改為 `porkList`。`query` 前綴在 C# 語境下通常暗示「尚未執行的查詢（IQueryable）」，用在已執行並取回的結果集上會造成閱讀誤解。

**M-2：raw → groupedRaw**

`GetDisastersAsync` 中的 `raw` 變數改名為 `groupedRaw`。此變數的值是已經過 `GroupBy` 聚合的結果，原名稱遺漏了這個關鍵的資料形狀資訊，閱讀者需要追蹤查詢才能理解它不是「原始資料」。

**M-6：GetDisastersAsync 加 .Take(5000) 防護**

`DebrisAlertRecords` 是歷史型資料集，隨時間線性累積，沒有設計上的上限。在 `groupedRaw` 的查詢鏈加入 `.Take(5000)` 防護，確保即使資料持續累積，單次查詢也不會無限制地拉取記憶體。

面試說法：「這是防禦性設計（Defensive Programming）。目前資料量可能還在可接受範圍，但設計一個沒有上限的歷史資料查詢，是把效能風險留給未來的自己。加 `Take()` 的代價是零，不加的代價是不可預測的。」

**M-3：民國年轉換 lambda 抽出為 DateHelper.ConvertRocRestDay()**

`GetRestDaysAsync` 原本的民國年轉換邏輯是內嵌於 LINQ 鏈的匿名 lambda：

```csharp
.Select(r => {
    try { return (DateOnly?)new DateOnly(r.Year + 1911, r.Month, r.RestDay); }
    catch { return null; }
})
```

重構後抽出為 `DateHelper` 的靜態方法：

```csharp
public static DateOnly? ConvertRocRestDay(int rocYear, int month, int day)
```

`GetRestDaysAsync` 改為：

```csharp
return records
    .Select(r => DateHelper.ConvertRocRestDay(r.Year, r.Month, r.RestDay))
    .Where(d => d.HasValue)
    .Select(d => d!.Value)
    .Where(d => d >= startDate && d <= endDate)
    .Select(d => new RestDayResponseDto { RestDate = d })
    .ToList();
```

**設計決策：為何放入 DateHelper 而非 private static**

`ConvertRocRestDay` 是純函式（輸入三整數，輸出可能為 null 的 DateOnly，無副作用）。DateHelper 本來就是民國日期轉換工具類別，將此方法收進去保持了類別的高內聚性，也讓未來其他地方若有相同需求能找到正確的位置，而不是在各 Service 各自散落。

---

### 二、DateHelper.cs 完整 XML Doc Comment（W-1 附帶）

所有現有方法補齊 `/// <summary>`，每個方法的 Summary 包含輸入輸出的具體範例，讓閱讀者不需要追蹤實作即可理解行為：

```csharp
/// <summary>
/// 將民國年、月、日三個整數轉換為西元 DateOnly；日期無效時回傳 null，不拋例外。
/// 輸入：(107, 7, 15)　→　輸出：DateOnly(2018, 7, 15)
/// 輸入：(107, 2, 30)　→　輸出：null（2 月沒有 30 日）
/// 輸入：(107, 13, 1)　→　輸出：null（月份超出範圍）
/// </summary>
public static DateOnly? ConvertRocRestDay(int rocYear, int month, int day)
```

---

### 三、WeatherService.cs — GetStationsByCityAsync 加 Doc Comment（W-1）

`GetStationsByCityAsync` 採用兩段式查詢策略，但方法名稱本身無法反映這個設計選擇。加入 `/// <summary>` 說明：

```csharp
/// <summary>
/// 查詢指定縣市下所有氣象站的最新觀測資料。
/// 採兩段式查詢策略：
/// Step 1：SQL 端 GroupBy 取各站最新 ObservedAt（回傳筆數 = 站台數，通常幾十筆）
/// Step 2：用 (StationId, ObservedAt) 撈完整欄位資料
/// Step 3：記憶體端 GroupBy 做最後防護，排除極端情況下同一站有多筆相同時間的重複資料
/// 末段記憶體 GroupBy 開銷可忽略，因為資料量僅為站台數。
/// </summary>
```

面試說法：「方法名稱只能說明『做什麼』，但有時候更重要的是說明『為什麼這樣做』。兩段式查詢的決策動機（EF Core GroupBy + First() 翻譯穩定性問題、SQL Server 擅長 GROUP BY + MAX）屬於設計知識，不應該只存在於 PR 描述或口頭傳遞，應該留在程式碼裡。」

---

### 四、Program.cs 模組化重構（P-1）— 最具架構展示價值

**問題**

原本的 `Program.cs` 將所有 DI 註冊平鋪在同一個方法裡，Identity、Market、Weather、Core、Redis、CORS、Swagger 混在一起，整個 builder 區段超過 60 行，閱讀需要逐行追蹤才能理解哪些服務屬於哪個模組。

**修正方式**

新建 `TaiwanAgri.Web/Extensions/` 資料夾，將 DI 註冊按模組職責拆分為五個 Extension Method 檔案：

| 檔案 | 方法 | 負責內容 |
|------|------|----------|
| `IdentityExtensions.cs` | `AddIdentityModule()` | ApplicationDbContext + ASP.NET Core Identity |
| `MarketModuleExtensions.cs` | `AddMarketModule()` | MarketDbContext + IMarketService |
| `WeatherModuleExtensions.cs` | `AddWeatherModule()` | WeatherDbContext + IWeatherService + IPestService |
| `CoreModuleExtensions.cs` | `AddCoreModule()` | CoreDbContext + INavService + INotificationService |
| `InfrastructureExtensions.cs` | `AddInfrastructure()` | Redis + CORS + PriceUpdatedConsumer + Swagger |

最終 `Program.cs` 的 builder 區段精簡為：

```csharp
builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddMarketModule(builder.Configuration);
builder.Services.AddWeatherModule(builder.Configuration);
builder.Services.AddCoreModule(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
```

**為什麼這個改動有面試價值**

這不只是「讓程式碼更短」。Extension Method 的邊界直接對應 SA/SD 文件裡定義的模組邊界（Market Module / Weather Module / Core Module），讓程式碼的物理結構和架構文件的邏輯結構對齊。面試時可以翻開 Program.cs 說：「你看這五行，對應到文件第 3.3 節描述的五個 DbContext，每一行都是一個模組的入口。」

---

### 五、CORS 設定外化（D-2）

`localhost:5173` / `localhost:5174` 原本硬編碼在 `InfrastructureExtensions.cs` 裡。

**修正方式**

`appsettings.Development.json` 新增：

```json
"Cors": {
  "AllowedOrigins": [
    "http://localhost:5173",
    "http://localhost:5174"
  ]
}
```

`InfrastructureExtensions.cs` 改為：

```csharp
var origins = configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

policy.WithOrigins(origins)
      .AllowAnyMethod()
      .AllowAnyHeader()
      .AllowCredentials();
```

**面試說法**：「開發環境的 port 是環境事實，不應該是程式邏輯的一部分。放在 appsettings 讓設定和程式碼各司其職，也讓未來換 port 不需要改程式碼再重新編譯。」

---

### 六、MarketFilter.vue 術語統一（F-1）

`chip` 是 UI Component Library 的通用術語（Material Design、Vuetify 等框架的 Chip 元件），但本專案的作物多選按鈕並非通用 Chip，而是特定業務元件。統一改為 `crop-btn` / `crop-container` / `crop-list`，讓 CSS class 名稱直接說明業務用途。

template 和 `<style scoped>` 同步更新：

```css
/* 改前 */
.chip { ... }
.chip.selected { ... }
.chip.disabled { ... }

/* 改後 */
.crop-btn { ... }
.crop-btn.selected { ... }
.crop-btn.disabled { ... }
```

---

## 關鍵設計決策

### 決策一：ConvertRocRestDay 放 DateHelper 而非 MarketService private static

`private static` 版本在技術上沒有問題，但代表「這個知識只屬於 MarketService」。民國年轉換是跨模組可能共用的領域知識，不是某個 Service 的內部細節，放入 DateHelper 讓這份知識有明確的歸屬位置。

### 決策二：GetCropsAsync 兩段式保留，不改 JOIN

本輪評估三種寫法的取捨：

| 寫法 | 弱點 |
|------|------|
| Correlated Subquery（原版） | 外層資料量大時子查詢重複執行 |
| 兩段式 IN（現行） | Step 1 結果集大時 IN 參數數量爆炸 |
| JOIN | 效能最穩定，但 EF Core 寫法可讀性最差 |

在台灣農產品作物種類的資料規模下（頂多幾百種），IN 爆炸不會發生，兩段式的可讀性優勢才是真正的決定因素。說得出這個分析過程，比選哪一種寫法更重要。

### 決策三：F-2（7 日均線 computed）不修

`calcMA` 的計算已包裹在 `chartData computed` 內，`prices` 不變時 `chartData` 不重算，`calcMA` 自然也不重算。真正的快取邊界在 `chartData` 這層，已達到 computed 的效果。若要進一步優化，可將 `calcMA` 的結果單獨抽成 `movingAverageMap computed` 讓其他 computed 共用，但此改動的實際效益很低，且 `chartData computed` 的包裹本身已足夠。

---

## 不修項目說明

| 項目 | 不修理由 |
|------|----------|
| M-4 try-catch 不改 DateOnly.TryCreate | `TryCreate` 驗證年月日合法性，`try-catch` 處理格式轉換失敗，語意不同，為改而改 |
| M-7 不加 Repository 介面 | EF Core DbContext + IQueryable 本身已是 Repository 抽象，Side Project 不需額外包裝 |
| D-1 NotificationController [FromQuery] userId 保留 | JWT 整合是整個認證架構的事，commit 已標 TODO，刻意保留的技術債 |
| W-2 PestService 分頁參數不封裝 DTO | 只有一個方法有分頁需求，過早封裝只增加間接層 |
| F-2 7 日均線不另抽 computed | calcMA 已在 chartData computed 內，快取效果已達到 |
| F-3 exportCsv.ts 單元測試暫緩 | 時間優先序，切入點已知：從純函式開始補最易驗證 |

---

## 驗收標準

- `GET /api/market/disasters` 回應正常，查詢不會無限載入歷史資料（Take(5000) 防護）
- `GET /api/market/rest-days` 回應正常，ConvertRocRestDay 重構後行為不變
- Program.cs 可正常啟動，五個 Extension Method 均能正確注入對應服務
- 前端 MarketFilter 作物按鈕 hover/selected/disabled 樣式正常（CSS class 重命名後無斷裂）

---

## 檔案異動清單

| 檔案 | 異動 | 說明 |
|------|------|------|
| `TaiwanAgri.Core/Helpers/DateHelper.cs` | M | 新增 ConvertRocRestDay 方法；補齊所有方法 XML doc comment |
| `TaiwanAgri.Frontend/src/components/MarketFilter.vue` | M | chip → crop-btn / crop-container / crop-list 術語統一 |
| `TaiwanAgri.Modules.Market/Services/MarketService.cs` | M | M-1 queryPork→porkList；M-2 raw→groupedRaw；M-6 加 Take(5000)；GetCropsAsync 改兩段式 |
| `TaiwanAgri.Modules.Weather/Services/WeatherService.cs` | M | GetStationsByCityAsync 加 /// <summary> |
| `TaiwanAgri.Web/Extensions/CoreModuleExtensions.cs` | A | AddCoreModule() Extension Method |
| `TaiwanAgri.Web/Extensions/IdentityExtensions.cs` | A | AddIdentityModule() Extension Method |
| `TaiwanAgri.Web/Extensions/InfrastructureExtensions.cs` | A | AddInfrastructure() Extension Method（含 CORS 讀設定）|
| `TaiwanAgri.Web/Extensions/MarketModuleExtensions.cs` | A | AddMarketModule() Extension Method |
| `TaiwanAgri.Web/Extensions/WeatherModuleExtensions.cs` | A | AddWeatherModule() Extension Method |
| `TaiwanAgri.Web/Program.cs` | M | 精簡為五行 AddXxxModule() + Seed + HTTP Pipeline |
| `TaiwanAgri.Web/appsettings.Development.json` | M | 新增 Cors.AllowedOrigins 設定區塊 |

---

## 閱讀之後：給你的觀察指南

這個 PR 最值得思考的是**「Code Review 修正的兩種性質」**。

第一種是「有錯要修」——M-1、M-2 這類命名問題，如果不修，六個月後連自己都不確定 `queryPork` 是尚未執行的查詢還是已取回的結果。這類問題修了就消失，沒有討論空間。

第二種是「有選擇才值得記錄」——M-6 的 `Take(5000)` 不是「一定要加」，而是「加了能展示防禦性設計的思維」。GetCropsAsync 選兩段式而非 JOIN，不是因為兩段式一定更快（在這個資料規模下三種寫法效能相同），而是因為可讀性和可預測性在 Side Project 等級的資料規模下是更重要的決策依據。

**工程師的判斷習慣**：遇到「可改可不改」的項目，先問「改了能說清楚為什麼嗎？不改也能說清楚為什麼嗎？」兩個問題都能說清楚，才是真正的技術決策，而不只是照單全收或全盤拒絕。

---

# PR #032 — W21 Code Review 修正（第三輪）：Cache Key 重構 + 輸入驗證補強 + 設定外化 + 單元測試建立

**標題**：`fix(market/web/tests): BuildPricesCacheKey pure function 抽取 + MarketController 白名單驗證 + DisasterRecordLimit 設定化 + DateHelper xUnit 測試覆蓋`

---

## 背景與動機

PR #031（W21 Code Review 第二輪）完成了命名語意修正、防禦性設計、Program.cs 模組化重構、CORS 設定外化、前端術語統一。本 PR 為第三輪，針對以下四個維度繼續改善：

1. **Cache Key 組裝邏輯的可維護性**：抽取為具名 pure function，讓意圖在閱讀時可見
2. **輸入驗證的防禦邊界**：補 Controller 層白名單，不依賴 Service 層靜默回傳空清單
3. **設定與程式碼分離**：將查詢上限從硬編碼移至 appsettings.json
4. **測試覆蓋**：從「0 測試」升級至「有 xUnit 覆蓋」，補齊 DateHelper 邊界值測試

本輪改動涵蓋 MarketService.cs、MarketController.cs、appsettings.json 與全新的 DateHelperTests.cs，全部屬於品質提升，不涉及任何功能邏輯異動。

---

## 實作內容

### 一、BuildPricesCacheKey() 抽取為 private static（MarketService.cs）

**問題**

原本的 Cache Key 組裝邏輯直接內嵌在 `GetPricesAsync` 方法體內：

```csharp
var sortedCrops = string.Join(",", cropCodes.OrderBy(c => c));
var cacheKey = $"market:prices:{marketType}:{sortedCrops}:{marketCode ?? ""}:{finalStart}:{finalEnd}";
```

這段邏輯有兩個隱性設計——cropCodes 需要排序確保任意順序命中同一個 slot、使用 finalStart/finalEnd（已解析的實際日期）而非原始 null——但讀者必須閱讀整段程式碼才能理解。

**修正方式**

```csharp
/// <summary>
/// 組裝 GetPricesAsync 的 Redis Cache Key。
/// cropCodes 排序後 Join，確保 ["A01","B02"] 和 ["B02","A01"] 命中同一個 cache。
/// 使用 finalStart / finalEnd（已解析的實際日期），防止 null 預設值碰撞到同一個 Key。
/// 格式：market:prices:{marketType}:{sortedCrops}:{marketCode}:{startDate}:{endDate}
/// </summary>
private static string BuildPricesCacheKey(
    string marketType,
    string[] cropCodes,
    string? marketCode,
    DateOnly finalStart,
    DateOnly finalEnd)
{
    var sortedCrops = string.Join(",", cropCodes.OrderBy(c => c));
    return $"market:prices:{marketType}:{sortedCrops}:{marketCode ?? ""}:{finalStart}:{finalEnd}";
}
```

`GetPricesAsync` 呼叫點精簡為一行：

```csharp
// 2. 組裝 Cache Key（cropCodes 排序確保任意排列命中同一 slot）
var cacheKey = BuildPricesCacheKey(marketType, cropCodes, marketCode, finalStart, finalEnd);
```

**關鍵設計決策：為何放在 MarketService 內部，而非抽至 Core 層**

目前只有一個呼叫點。W15 的 PriceUpdatedConsumer Cache Invalidation 設計是清除所有 `market:prices:*` 開頭的 Key，不需要組出精確的 Cache Key，因此不存在第二個使用者。在沒有明確跨模組需求的前提下，放在 `private static` 符合 YAGNI 原則，未來真的有第二個呼叫點時搬移才有具體理由。

這個判斷和 PR #031 的 `ConvertRocRestDay` 決策對稱：那個方法因為有潛在的跨模組共用價值而放入 DateHelper；這個方法因為目前唯一服務一個地方而保留在 Service 內部。

---

### 二、MarketController 補 IsValidMarketType 白名單驗證

**問題**

原本的 MarketController 沒有對 marketType 做白名單驗證。繞過前端直接打 API 傳入非法值（例如 `"veg"`、`"蔬菜"`）時，Service 層會靜默回傳空清單——不崩潰、不報錯，但面試現場難以解釋「為什麼回傳空陣列」。

**修正方式**

```csharp
private static bool IsValidMarketType(string? marketType) =>
    marketType is "Veg" or "Fruit" or "Flower";
```

GetMarkets / GetCrops / GetPrices 三個 Action 各自在方法開頭加入：

```csharp
if (!IsValidMarketType(marketType))
    return BadRequest("marketType 必須為 Veg、Fruit 或 Flower");
```

**關鍵設計決策：白名單而非 enum 重構**

評估 enum 重構的取捨如下：

| 面向 | enum 重構 | Controller 白名單 |
|------|-----------|-------------------|
| 型別安全 | 編譯期保證 | 執行期驗證 |
| EF Core 查詢 | 需手動 `.ToString()` 轉換，易漏 | 無影響 |
| 改動範圍 | Controller + Service + Mapping 全面修改 | 只動 Controller |
| 面試解釋成本 | 需額外說明 EF Core 轉換問題 | 直接說明設計邊界 |

在這個專案裡，`marketType` 從頭到尾只是一個 SQL `WHERE` 過濾條件，沒有任何 switch/case 分支行為，enum 的型別安全收益不值得承擔 EF Core 轉換成本與改動範圍的風險。

**面試說法**：「Controller 層是輸入驗證的第一道防線。`IsValidMarketType` 明確列出合法值，讓錯誤路徑和 200 路徑一樣清晰可見——閱讀者看到 BadRequest 就知道這是預期的邊界條件，不需要追蹤進 Service 層才能理解。」

---

### 三、DisasterRecordLimit 設定外化（MarketService.cs + appsettings.json）

**問題**

`GetDisastersAsync` 的 `.Take(5000)` 是硬編碼的魔術數字。設定值藏在 C# 檔案裡，調整時需要修改程式碼並重新編譯。

**修正方式**

`appsettings.json` 新增：

```json
"MarketQueryLimits": {
  "DisasterRecordLimit": 5000
}
```

`MarketService` 建構子注入 `IConfiguration`，`GetDisastersAsync` 改為：

```csharp
// 為了避免一次撈出超過 10 萬筆資料導致 OutOfMemory，先設定一個合理的上限
var limit = _configuration.GetValue<int>("MarketQueryLimits:DisasterRecordLimit", 5000);
var groupedRaw = await query
    .Take(limit)
    .ToListAsync();
```

`GetValue<int>` 的第二個參數是 fallback 預設值——設定檔讀不到時的保底，確保行為不因設定缺失而中斷。這是讀設定值的好習慣：設定是優化，不是依賴。

**為什麼是 5000**

`DebrisAlertRecords` 是歷史型、線性累積的資料集。估算依據：一次查詢範圍內最多 30 個災害事件 × 每個事件最多 150 個受影響村落 = 4,500 筆，5,000 有合理餘量。這個數字設計的意義是「面試時說得出估算依據」，而非隨意填入。

---

### 四、DateHelper 單元測試建立（TaiwanAgri.Tests）

**這個 PR 面試 CP 值最高的部分**

專案從「0 個測試」升級至「有 xUnit 測試覆蓋」。測試標的選 `DateHelper.ConvertRocRestDay` 的原因：這是純函式（輸入三整數，輸出 `DateOnly?`，無副作用），測試撰寫成本最低，但覆蓋的邊界條件展示了「對民國年日期轉換的設計意圖理解」。

```csharp
public static DateOnly? ConvertRocRestDay(int rocYear, int month, int day)
{
    try { return new DateOnly(rocYear + 1911, month, day); }
    catch { return null; }
}
```

**6 個測試案例**

| 測試名稱 | 輸入 | 預期 | 設計說明 |
|----------|------|------|----------|
| NormalDate | (107, 7, 15) | DateOnly(2018, 7, 15) | Happy Path，民國107 = 西元2018 |
| LeapYearFeb29 | (109, 2, 29) | DateOnly(2020, 2, 29) | 民國109 = 西元2020（閏年），2/29 合法 |
| Feb30 | (107, 2, 30) | null | 2月沒有30日，任何年份都不合法 |
| NonLeapYearFeb29 | (94, 2, 29) | null | 民國94 = 西元2005（非閏年），2/29 不存在 |
| InvalidMonth13 | (107, 13, 1) | null | 月份超出範圍 |
| InvalidMonth0 | (107, 0, 1) | null | 月份為0，超出範圍 |

**閏年測試的設計說明**

`(94, 2, 29)` 和 `(109, 2, 29)` 這兩個案例一起展示了「同樣的輸入結構（2月29日），但因年份不同結果截然相反」。這正是邊界值測試最有說服力的形式：它告訴讀者「設計者知道閏年的語意，不是碰巧讓它過了」。

**驗收結果**：Test Explorer 顯示 6/6 全綠通過。

---

## 不修項目說明

| 項目 | 決策 | 理由 |
|------|------|------|
| MarketTypeMapping → enum | 不改，Controller 補白名單 | enum 在 EF Core 查詢需手動 `.ToString()`，改動範圍大，面試解釋成本高 |
| porkList → porkRecords | 跳過 | 命名反映當前語意，改名反而製造疑惑 |
| _queueName 補 readonly | 跳過 | StartAsync 動態寫入，技術上不能是 readonly |
| rocYear 語意前綴 | 不改 | `rocYear` 比 `year` 更能區分民國年/西元年，反而更清楚 |
| AddInfrastructure() 拆分 | 跳過 | Infrastructure 是合理聚合，拆了只增加行數無架構收益 |
| IN 清單過長風險 | 跳過 | 台灣測站約 700 站，IN 清單不會超過 1000，此風險不成立 |
| Take(5000) 設定化 | **已完成**（本 PR） | 練習設定外化，GetValue fallback 保留防護底線 |

---

## 驗收標準

- `GET /api/market/prices?marketType=xxx`（非法值）回傳 400 BadRequest，訊息含合法選項
- `GET /api/market/markets?marketType=Veg` 正常回傳（白名單通過）
- `Test Explorer` 顯示 `DateHelperTests` 6/6 全綠
- `appsettings.json` 含 `MarketQueryLimits:DisasterRecordLimit`，`GetDisastersAsync` 讀取設定值

---

## 檔案異動清單

| 檔案 | 異動 | 說明 |
|------|------|------|
| `TaiwanAgri.Modules.Market/Services/MarketService.cs` | M | 抽取 `BuildPricesCacheKey()` private static；注入 `IConfiguration`；`Take(limit)` 取代硬編碼 5000 |
| `TaiwanAgri.Web/Controllers/MarketController.cs` | M | 新增 `IsValidMarketType()` private static；三個 Action 各自加入白名單驗證 |
| `TaiwanAgri.Web/appsettings.json` | M | 新增 `MarketQueryLimits:DisasterRecordLimit = 5000` |
| `TaiwanAgri.Tests/Helpers/DateHelperTests.cs` | A | 新增 6 個 xUnit 測試（2 Happy Path + 4 Null Path） |
| `TaiwanAgri.Tests/UnitTest1.cs` | D | 刪除空殼樣板 |

---

## 閱讀之後：給你的觀察指南

這個 PR 最值得思考的是**「同一個問題，有多種解法，哪一種的整體代價最小」**。

`marketType` 的型別安全問題，有兩條路：enum 重構（設計層面解決）和 Controller 白名單（防禦層面解決）。這個 PR 選了後者，不是因為 enum 不好，而是因為 enum 在這個具體情境下有額外的轉換成本——而且那個成本在面試現場是額外的解釋負擔，不是工程收益。

`BuildPricesCacheKey` 的歸屬決策則是另一個維度的判斷：「這個知識現在有幾個使用者？」一個使用者 → `private static`；多個使用者 → 搬入共用層。這個判斷和 PR #031 的 `ConvertRocRestDay` 形成對照——兩個都是純函式，但歸屬位置不同，因為它們的共用潛力不同。

這些判斷不是隨機的，也不是「感覺哪個比較好」，而是從具體的設計問題出發，逐步推導出改動範圍最小、面試說明最清楚的那個選項。

---

# PR #033 — W15 JWT 身分驗證完整實作

**標題**：`feat(auth): JWT 發行基礎設施 + Login/Register API + Vue 3 登入頁 + NotificationController 還原 [Authorize]`

---

## 背景與動機

W15 目標是實作完整的 JWT 身分驗證流程。在此之前，NotificationController 以 `[FromQuery] string userId` 作為暫時替代方案（技術債，已記錄於 PR #027），整個系統沒有任何真實的身分驗證機制，前端的「登入按鈕」只是未接通的 UI 佔位。

本 PR 將三件事一次完成：

1. **後端 JWT 發行基礎設施**：讓後端能夠產生、驗證 JWT token
2. **後端 Login / Register API**：讓使用者能夠建立帳號並取得 token
3. **前端登入頁與狀態管理**：讓 token 能夠被儲存、使用、登出清除，並讓 NotificationController 正式還原為 JWT 驗證

---

## 實作內容

### 一、後端 JWT 發行基礎設施

**`IdentityExtensions.cs` — JWT Middleware 設定**

在 `AddIdentityModule()` 內新增：

```csharp
services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = configuration["Jwt:Issuer"],
        ValidAudience = configuration["Jwt:Issuer"],
        IssuerSigningKey = key
    };
});
```

四個 `Validate*` 全部 `true`：最嚴格的驗證，缺一不可。

| 設定 | 驗證內容 |
|------|----------|
| `ValidateIssuer` | token 是否由本伺服器發行 |
| `ValidateAudience` | token 是否給本服務用 |
| `ValidateLifetime` | token 是否已過期 |
| `ValidateIssuerSigningKey` | 簽章印章是否正確 |

**`appsettings.json` — JWT 設定區塊**

```json
"Jwt": {
  "SecretKey": "x7Kp2mQr9vLnT4wY8jZcA3bFhD6sEuN0iWoG1yRqP5tXmJkV2",
  "Issuer": "TaiwanAgriPlatform",
  "ExpiresInDays": 7
}
```

密鑰要求：最少 32 字元（HMAC-SHA256），實際生產環境應替換為環境變數注入。

---

### 二、後端 Auth 功能模組

**DTO 設計**

| DTO | 方向 | 欄位 |
|-----|------|------|
| `LoginRequestDto` | 前端 → 後端 | `Email`、`Password` |
| `RegisterRequestDto` | 前端 → 後端 | `Email`、`Password`、`DisplayName?`、`UserType?` |
| `AuthResponseDto` | 後端 → 前端 | `Token`、`Email`、`DisplayName?`、`Role` |

**`AuthService.cs` — 核心邏輯**

登入流程（`LoginAsync`）：

```
UserManager.FindByEmailAsync        → 確認帳號存在
SignInManager.CheckPasswordSignInAsync → 驗密碼（lockoutOnFailure: true）
UserManager.GetRolesAsync           → 取得角色
GenerateJwtToken()                  → 用密鑰產生 JWT
```

`lockoutOnFailure: true`：密碼連續輸錯後自動鎖定帳號，一個參數啟用 Identity 內建防暴力破解機制，不需要自行實作計數邏輯。

`GenerateJwtToken()` — JWT 組裝：

```csharp
var claims = new[]
{
    new Claim(ClaimTypes.NameIdentifier, user.Id),
    new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
    new Claim(ClaimTypes.Role, role)
};
```

Claims 是「手環上印的資料」，後端之後驗 token 時直接從 Claims 讀取 `userId` 和 `Role`，完全不需要再查 DB。

**`RequireConfirmedAccount = false`**

專案目前無 Email 驗證基礎設施，若設為 `true` 所有新帳號都無法登入（等待 Email 確認），開發期間設為 `false`。

**`AuthController.cs` — HTTP 層**

| 端點 | 例外 → HTTP 狀態 |
|------|-----------------|
| `POST /api/auth/login` | `UnauthorizedAccessException` → 401 |
| `POST /api/auth/register` | `InvalidOperationException` → 400（密碼規則不符、Email 重複） |

---

### 三、NotificationController 還原 [Authorize]

W15 的核心技術債結清。移除所有 `[FromQuery] string userId` 暫時方案，改為：

```csharp
[Authorize]
// ...
var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
if (userId == null) return Unauthorized();
```

Service 層簽名完全不需要改，因為設計之初就預留了此路徑（PR #027 設計決策）。

---

### 四、前端三層架構

前端同樣遵循「api 層 → Pinia Store → Vue 元件」的三層架構。

**第一層：`src/api/auth.ts`**

封裝 `POST /api/auth/login` 和 `POST /api/auth/register`，定義對應的 TypeScript 介面（`LoginRequestDto`、`RegisterRequestDto`、`AuthResponseDto`）。

**第一層（並列）：`src/api/authClient.ts`**

帶 JWT 的獨立 axios instance，interceptor 自動從 `localStorage` 取 token：

```typescript
authClient.interceptors.request.use((config) => {
  const token = localStorage.getItem('token')
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})
```

**為何從 `localStorage` 取而非 import `authStore`**

`useAuthStore()` 必須在 Vue 元件的 `setup()` 內呼叫（Pinia 需要 Vue 響應式環境）。API 層是普通 TypeScript 模組，直接呼叫 `useAuthStore()` 會觸發 `getActivePinia() was called with no active Pinia` 錯誤。`localStorage` 是瀏覽器原生全域存儲，無此限制，且與 `authStore` 存的是同一份資料，兩者完全一致。

這也是一個架構邊界原則的體現：**api 層不得向上依賴 store 層**（違反三層架構的單向依賴方向）。

**第二層：`src/stores/authStore.ts`**

```typescript
const token = ref<string | null>(localStorage.getItem('token'))
const user = ref<...>(JSON.parse(localStorage.getItem('user') ?? 'null'))

const isLoggedIn = computed(() => !!token.value)
const displayName = computed(() => user.value?.displayName ?? user.value?.email ?? '')
const role = computed(() => user.value?.role ?? 'Guest')
```

`token` 和 `user` 同時存 `localStorage`，確保頁面重新整理後不會登出。

**第三層：`src/views/auth/LoginView.vue`**

- 登入 / 註冊 Tab 切換（單一頁面處理兩種模式）
- `translateIdentityError()`：9 條規則將 ASP.NET Core Identity 英文錯誤翻譯為中文

| Identity 英文 | 翻譯 |
|--------------|------|
| `already taken` | 此 Email 已被註冊，請直接登入或使用其他信箱 |
| `at least one non alphanumeric` | 密碼需包含至少一個特殊符號（如 !@#$） |
| `Invalid login attempt` | 帳號或密碼錯誤，請重新確認 |
| `locked out` | 帳號已被鎖定，請稍後再試 |

- 成功後 `router.push('/')` 導回首頁

**`TopNav.vue` 登入狀態切換**

```vue
<template v-if="authStore.isLoggedIn">
  <span class="user-name">{{ authStore.displayName }}</span>
  <button class="login-btn" @click="handleLogout">登出</button>
</template>
<button v-else class="login-btn" @click="router.push('/login')">登入</button>
```

**通知系統對齊 JWT**

`notificationApi` 改用 `authClient`（自動帶 token），移除所有 `userId` 參數。`notification.ts` store 移除 `TEMP_USER_ID`，呼叫點全面清乾淨。

---

## 關鍵設計決策

| 決策 | 內容 |
|------|------|
| `authClient` 從 `localStorage` 取 token | API 層不得 import Pinia Store（違反三層架構單向依賴），`localStorage` 是瀏覽器原生全域存儲，無依賴問題，且與 `authStore` 存的是同一份資料 |
| `lockoutOnFailure: true` | 使用 Identity 內建帳號鎖定機制防暴力破解，一個參數啟用，不需自行實作計數邏輯 |
| 註冊成功直接發行 token | 使用者體驗：註冊完直接進入首頁，不需要再手動登入一次，與現代網站（GitHub、Notion 等）慣例一致 |
| `RequireConfirmedAccount = false` | 專案目前無 Email 驗證基礎設施，若設為 true 會導致所有新帳號無法登入 |
| `translateIdentityError` 放在元件內 | 翻譯邏輯與登入 UI 強耦合（只有這個表單需要），不值得抽到共用層 |
| JWT 不存 DB | 無狀態設計（Stateless），靠 `exp` claim 管理過期，後端驗印章純運算，不需要查 DB |
| Claims 三個即可 | `NameIdentifier`（userId）、`Email`、`Role` 三個 Claim 足以支撐現有所有授權邏輯，不過度打包 token |

---

## 不修項目說明

| 項目 | 不修理由 |
|------|----------|
| NavController `[AllowAnonymous]` 保留不變 | 設計本身正確——訪客（未登入）仍需取得 Guest 可見的導覽清單，否則前端 Navbar 在未登入時完全失效。JWT middleware 到位後，已登入使用者的 `ClaimsPrincipal` 自動注入，`[AllowAnonymous]` 不需異動 |
| UserFarmProfiles CRUD | W15 原始範圍包含此功能，但優先完成核心 JWT 流程，CRUD 排入後續 Sprint |
| UserWatchlist 管理 | 同上 |
| NuGet `Microsoft.AspNetCore.Authentication.JwtBearer` | 需手動安裝（`dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer`），提供 `JwtSecurityToken`、`SymmetricSecurityKey` 等類別 |

---

## 驗收標準

- [x] `POST /api/auth/register` 回傳 JWT token + 使用者基本資訊
- [x] `POST /api/auth/login` 驗證帳密後回傳 JWT token
- [x] 密碼規則不符時回傳 400 + 中文錯誤訊息
- [x] Email 重複註冊時回傳 400 + 中文錯誤訊息
- [x] 帳密錯誤時回傳 401
- [x] JWT token 可在 jwt.io 解碼，Payload 含 `NameIdentifier`、`Email`、`Role`、`exp`、`iss`
- [x] 登入後 TopNav 顯示 `displayName` + 登出按鈕
- [x] 登出後清除 `localStorage`，TopNav 還原登入按鈕
- [x] `GET /api/Notification/unread-count` 未帶 token 時回傳 401
- [x] 帶有效 token 時通知 API 正常運作

---

## 檔案異動總表

| 檔案 | 異動類型 | 說明 |
|------|----------|------|
| `TaiwanAgri.Web/Extensions/IdentityExtensions.cs` | 修改 | 加入 JWT Middleware（`AddAuthentication` + `AddJwtBearer`）；`RequireConfirmedAccount = false`；`IAuthService` 注入 |
| `TaiwanAgri.Web/appsettings.json` | 修改 | 新增 `Jwt` 設定區塊 |
| `TaiwanAgri.Web/Dtos/LoginRequestDto.cs` | 新增 | |
| `TaiwanAgri.Web/Dtos/RegisterRequestDto.cs` | 新增 | |
| `TaiwanAgri.Web/Dtos/AuthResponseDto.cs` | 新增 | |
| `TaiwanAgri.Web/Services/IAuthService.cs` | 新增 | |
| `TaiwanAgri.Web/Services/AuthService.cs` | 新增 | `SignInManager` + `UserManager` + `JwtSecurityTokenHandler` |
| `TaiwanAgri.Web/Controllers/AuthController.cs` | 新增 | `[FromBody]` 接收 DTO；例外對應 HTTP 狀態碼 |
| `TaiwanAgri.Web/Controllers/NotificationController.cs` | 修改 | 還原 `[Authorize]`；移除 `[FromQuery] string userId` |
| `TaiwanAgri.Frontend/src/api/auth.ts` | 新增 | |
| `TaiwanAgri.Frontend/src/api/authClient.ts` | 新增 | axios interceptor + `localStorage` token |
| `TaiwanAgri.Frontend/src/stores/authStore.ts` | 新增 | `login / register / logout` + `localStorage` 持久化 |
| `TaiwanAgri.Frontend/src/views/auth/LoginView.vue` | 新增 | Tab 切換 + `translateIdentityError` |
| `TaiwanAgri.Frontend/src/router/index.ts` | 修改 | 加入 `/login` 路由 |
| `TaiwanAgri.Frontend/src/components/TopNav.vue` | 修改 | 登入狀態切換 + 登出 |
| `TaiwanAgri.Frontend/src/api/weather.ts` | 修改 | `notificationApi` 改用 `authClient`，移除 `userId` 參數 |
| `TaiwanAgri.Frontend/src/stores/notification.ts` | 修改 | 移除 `TEMP_USER_ID` |

---

## 閱讀之後：給你的觀察指南

這個 PR 最值得思考的是 **「架構邊界的語意」**。

`authClient` 為什麼不能直接 `import { useAuthStore }`？表面上是「技術限制」（Pinia 需要 Vue 上下文），但背後是一個更根本的設計原則：api 層的職責是「發 HTTP 請求、接回應」，它不應該知道應用程式的狀態長什麼樣子。如果讓 api 層知道 store 的存在，就等於讓「食材供應商」打電話問「廚房今天要做幾道菜」——職責邊界壞掉了。用 `localStorage` 繞過這個問題，不只是技術上的 workaround，而是恰好符合架構語意：api 層從「系統環境」（瀏覽器儲存）取 token，而非依賴「應用程式狀態」（Pinia store）。

另一個值得注意的是 **JWT 的無狀態性**。和固定 token（存 DB）的設計相比，JWT 最重要的收益不是效能（不查 DB），而是**部署彈性**：任何持有密鑰的伺服器實例都能獨立驗證 token，水平擴展時不需要共享 Session 狀態。這個設計選擇在 Side Project 規模感受不明顯，但在面試現場說得出這個理由，才是真正掌握了 JWT 存在的意義。

---

# PR #034 — W16 UserFarmProfiles CRUD：新模組建立 + 農場偏好設定完整實作

**標題**：`feat(user): TaiwanAgri.Modules.User 新 Project + UserDbContext + ProfileController + Vue 3 農場設定頁（Autocomplete 作物搜尋）`

---

## 背景與動機

W16 目標是實作使用者農場偏好設定（UserFarmProfiles CRUD），讓登入後的農民能夠記錄自己的農場基本資訊（縣市、類型）與主要作物清單，作為未來個人化功能（通知推送、行情過濾）的資料基礎。

本 PR 同時完成三件事：

1. **新業務模組建立**：TaiwanAgri.Modules.User 作為獨立 Class Library Project，建立清晰的使用者業務邊界
2. **後端 CRUD API**：Entity 設計 + DbContext + Migration + Service + Controller 完整鏈路
3. **前端農場設定頁**：包含作物 Autocomplete 搜尋的完整 CRUD 頁面

---

## 架構決策：為什麼是 TaiwanAgri.Modules.User，而不是放進既有 DbContext？

本次實作的核心設計問題是：UserFarmProfiles 屬於哪個 DbContext？

### 選項分析

| 選項 | 說明 | 問題 |
|------|------|------|
| ApplicationDbContext | 放進 Identity 所在的入口層 | Web 入口層不應承載業務資料，架構邊界崩潰 |
| CoreDbContext | 放進跨模組基礎設施層 | Core 是工具箱（SyncStates、NavModules），不是業務層 |
| **新建 UserDbContext（本 PR 選擇）** | 獨立的使用者業務模組 | 無；邊界最清晰，擴充性最好 |

**判斷依據**：UserFarmProfiles 的消費者是「使用者農場設定功能」，不是「所有模組都要依賴的基礎設施」。CoreDbContext 的定義是「消費者是所有模組的共用機制」，放 UserFarmProfiles 進去會讓 Core 的職責模糊。

**面試說法**：「我問了一個問題：『這份資料的消費者是誰？』如果答案是某個特定業務領域，它就屬於那個業務的模組。如果答案是所有模組，它才屬於 Core。UserFarmProfiles 的消費者只有使用者個人化功能，所以單獨開一個 User 模組是正確的邊界設計。」

---

## 實作內容

### 一、UserFarmProfile / UserFarmCrop Entity 設計

**關鍵決策一：UserId 當 PK（不是 int Id）**

```csharp
[Key]
[MaxLength(450)]
public string UserId { get; set; } = string.Empty;
// PK = 邏輯 FK → AspNetUsers.Id
// 一個 UserId 只能有一筆農場設定，PK 本身保證唯一性
```

選擇 UserId 當 PK 的理由：
- 業務語意清晰：「一個使用者只有一份農場偏好設定」，PK 直接強制此約束
- API 設計最簡單：`GET /api/profile/farm` 不需要帶任何 id 參數，後端從 JWT Claims 取 UserId 即可
- 如果未來需要「多農場管理」，那是另一個功能（UserFarms 子表），不是修改這張表

**關鍵決策二：CropName 儲存快照**

UserFarmCrop.CropName 存的是快照值，不是 JOIN 到 CropInfos 取的即時值。原因是 CropInfos 在 MarketDbContext，跨 DbContext 無法做 EF Core JOIN，快照讓查詢不需要跨界。

**關鍵決策三：UserId 是邏輯 FK（無物理 FK constraint）**

UserId 對應 AspNetUsers.Id，但 ApplicationDbContext 和 UserDbContext 是兩個獨立的 DbContext，EF Core 無法建立跨 DbContext 的物理 FK。這和 PestRuleConfig、UserNotification 的設計模式完全一致：跨 DbContext 邊界只能是邏輯關聯，完整性由應用程式層負責。

---

### 二、UserDbContext 關聯設定的陷阱與解法

Migration 跑完後第一次測試 GET /api/profile/farm 回傳 500，錯誤訊息是：

```
Invalid column name 'UserFarmProfileUserId'
```

**根本原因**：UserFarmCrop 有導覽屬性 `UserFarmProfile`，UserFarmProfile 也有集合 `Crops`，EF Core 看到兩端導覽屬性，但 `WithMany()` 沒有明確指定對應的集合，EF Core 自己建立了一個 shadow property `UserFarmProfileUserId`。

**解法**：明確指定雙向導覽屬性關聯，同時告知 EF Core 主表端的 Key：

```csharp
entity.HasOne(c => c.UserFarmProfile)
      .WithMany(p => p.Crops)          // 明確指定集合導覽屬性
      .HasForeignKey(c => c.UserId)    // FK 欄位是 UserFarmCrop.UserId
      .HasPrincipalKey(p => p.UserId)  // 主表 Key 是 string UserId，不是 int
      .OnDelete(DeleteBehavior.Cascade);
```

`HasPrincipalKey` 在 PK 是非常規型別（string）的場景下是必要的，缺少它 EF Core 會試圖自己推導關聯，產生錯誤的 shadow property。

**面試說法**：「這個 bug 讓我搞清楚了 EF Core 建立關聯時的推導邏輯：EF Core 看到兩端導覽屬性，如果你不明確告訴它用哪個欄位關聯、主表的 Key 是什麼，它就自己發明一個欄位名。`HasPrincipalKey` 是在說：主表這端，請用 UserId 而不是你猜測的 Id。」

---

### 三、Upsert 設計：為什麼用一支 PUT 而不是 POST + PUT

```csharp
[HttpPut("farm")]
public async Task<IActionResult> UpsertFarmProfile([FromBody] UpsertFarmProfileRequestDto request)
```

**原因**：UserId 是 PK，同一個 UserId 永遠只會有一筆資料。前端儲存時不需要知道「這是第一次存還是更新」，後端統一處理：

```csharp
var existing = await context.UserFarmProfiles
    .Include(p => p.Crops)
    .FirstOrDefaultAsync(p => p.UserId == userId);

if (existing is null)
{
    // 新增：設定 CreatedAt 和 UpdatedAt
}
else
{
    // 更新：只改欄位，CreatedAt 不動
    // 作物清單：全刪全插
    context.UserFarmCrops.RemoveRange(existing.Crops);
    // foreach 新增
}

await context.SaveChangesAsync();
```

**作物清單為何全刪全插（不做 diff）**：農民通常種 3-10 種作物，數量少，全刪全插比「比對新舊清單找出新增/刪除」的邏輯更簡單可靠。diff 邏輯適合「清單有幾千筆、每次只改幾筆」的情境，這裡不符合。

---

### 四、GET 回傳 200 + null 而非 404

```csharp
if (profile is null)
{
    return Ok(null); // 不回 404
}
```

**語意區別**：
- `404 Not Found`：「你要找的資源不存在，這是錯誤」
- `200 + null`：「你查詢了，結果是你還沒有設定過，這是正常狀態」

第一次進個人設定頁，使用者還沒填過資料，前端應該顯示空白表單讓使用者填寫，而不是看到錯誤。回 404 會讓前端誤判「API 出錯了」，設計語意不正確。

---

### 五、Extension Method 對齊模組化模式

```csharp
// TaiwanAgri.Web/Extensions/UserModuleExtensions.cs
public static IServiceCollection AddUserModule(
    this IServiceCollection services,
    IConfiguration configuration)
{
    services.AddDbContext<UserDbContext>(...);
    services.AddScoped<IUserProfileService, UserProfileService>();
    return services;
}
```

Program.cs 維持五行格式，對齊現有 Market / Weather / Core / Identity 的模組化模式：

```csharp
builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddMarketModule(builder.Configuration);
builder.Services.AddWeatherModule(builder.Configuration);
builder.Services.AddCoreModule(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddUserModule(builder.Configuration);  // ← 新增
```

---

### 六、前端三層架構

前端同樣遵循「api 層 → Pinia Store → Vue 元件」三層架構。

**第一層：src/api/profile.ts**

使用 `authClient`（帶 JWT 的 axios instance），與 notificationApi 一致。GET 後端回 200+null 時，`res.data` 即為 null，前端用 null 判斷「顯示空白表單」。

**第二層：src/stores/profile.ts**

`saveFarmProfile()` 成功後主動重新呼叫 `fetchFarmProfile()`，確保畫面顯示的是資料庫裡實際存的資料，不只是「前端剛送出去的資料」。

**第三層：src/views/ProfileView.vue — 作物 Autocomplete**

作物下拉採 Autocomplete（而非靜態清單），因為台灣農產品作物種類多，使用者用關鍵字搜尋比翻選單更快。

```typescript
// 直接呼叫 API，不依賴 marketStore
const [veg, fruit, flower] = await Promise.all([
  marketApi.getCrops('Veg'),
  marketApi.getCrops('Fruit'),
  marketApi.getCrops('Flower'),
])
allCrops.value = [...veg, ...fruit, ...flower]
```

**為什麼不透過 marketStore 取作物清單？**

`marketStore.crops` 的語意是「目前使用者在行情頁選擇的類型所對應的作物清單」，它是有狀態的（隨使用者切換 Veg/Fruit/Flower 而變化）。ProfileView 需要的是「三種類型全部合併的搜尋池」，這是不同的需求，不應該共用同一份狀態，以免 Profile 頁的操作影響行情頁的篩選器狀態。

`onBlur` 延遲 150ms 關閉下拉，讓 `mousedown` 先觸發（否則點選下拉選項時，blur 先發生，下拉消失，click 就抓不到選中的項目）。

---

## 關鍵設計決策彙整

| 決策 | 選擇 | 理由 |
|------|------|------|
| DbContext 歸屬 | 新建 UserDbContext | 消費者只有使用者業務，不屬於 Core 或 ApplicationDbContext |
| 主鍵設計 | UserId（string）當 PK | 一人一份偏好設定，PK 保證唯一性，API 不需要帶 id 參數 |
| 作物更新策略 | 全刪全插 | 作物數量少（3-10 種），全刪全插比 diff 更簡單可靠 |
| HTTP 方法 | PUT（Upsert） | 資源識別（UserId）已知，前端無需區分新增或更新 |
| 空資料回傳 | 200 + null | 「沒有設定過」是正常狀態，不是錯誤；前端顯示空白表單 |
| 前端作物資料 | 直接打 API，不共用 marketStore | marketStore.crops 是有狀態的篩選器，語意不同 |
| EF Core 關聯設定 | HasPrincipalKey(p => p.UserId) | 主表 PK 是 string，EF Core 需要明確告知才不會推導錯誤 |

---

## .gitignore 修正

原本 `*.user` pattern 誤匹配到 `TaiwanAgri.Modules.User/` 資料夾（因資料夾名稱以 `.User` 結尾），導致新 Project 的所有檔案被 Git 忽略。

修正方式：將 `*.user` 改為 `*.csproj.user`，精確描述要忽略的 Visual Studio 使用者設定檔，不再誤傷資料夾名稱。

---

## 不修項目說明

| 項目 | 說明 |
|------|------|
| UserWatchlist | 下一個 PR 的功能，目前 UserFarmProfiles 先做偏好設定 |
| 作物清單下拉來源 API | 目前直接呼叫 marketApi.getCrops()，未來可以考慮增加 GET /api/profile/crops 端點讓前端統一走 profile API，但目前直打沒有問題 |
| YAGNI | UserFarmProfiles 設計為「偏好設定」而非「多農場管理」；若未來需要多農場功能，另開 UserFarms 表，不修改現有設計 |

---

## 驗收標準

- [x] `GET /api/profile/farm`（未登入）回傳 401
- [x] `GET /api/profile/farm`（登入、第一次）回傳 200 + `null`
- [x] `PUT /api/profile/farm` 儲存後，重新整理頁面資料保留
- [x] Autocomplete 輸入關鍵字後出現下拉選單，點選作物後加入清單
- [x] 移除作物後再儲存，重新整理確認作物清單已更新
- [x] Migration InitialUserSchema 建立 UserFarmProfiles + UserFarmCrops 兩張表
- [x] TaiwanAgri.Modules.User 正確加入 Git 追蹤（.gitignore 修正）

---

## 檔案異動清單

| 檔案 | 異動 | 說明 |
|------|------|------|
| `.gitignore` | M | `*.user` → `*.csproj.user`，修正誤匹配資料夾名稱 |
| `TaiwanAgri.Modules.User/TaiwanAgri.Modules.User.csproj` | A | 新 Class Library Project，相依 TaiwanAgri.Core + EF Core SqlServer |
| `TaiwanAgri.Modules.User/Entities/UserFarmProfile.cs` | A | UserId PK + FarmCity + FarmType + CreatedAt/UpdatedAt + Crops 集合 |
| `TaiwanAgri.Modules.User/Entities/UserFarmCrop.cs` | A | int Id PK + UserId FK + CropCode + CropName 快照 + 導覽屬性 |
| `TaiwanAgri.Modules.User/Data/UserDbContext.cs` | A | HasOne/WithMany/HasForeignKey/HasPrincipalKey 完整關聯設定 |
| `TaiwanAgri.Modules.User/Data/Migrations/20260607154349_InitialUserSchema.cs` | A | 建立兩張表 + FK + Index |
| `TaiwanAgri.Modules.User/Data/Migrations/UserDbContextModelSnapshot.cs` | A | EF Core 模型快照 |
| `TaiwanAgri.Modules.User/Dtos/ApiRequests/UpsertFarmProfileRequestDto.cs` | A | PUT 請求 DTO |
| `TaiwanAgri.Modules.User/Dtos/ApiRequests/CropItemDto.cs` | A | 作物項目 DTO |
| `TaiwanAgri.Modules.User/Services/IUserProfileService.cs` | A | 介面定義 |
| `TaiwanAgri.Modules.User/Services/UserProfileService.cs` | A | Upsert 邏輯；作物全刪全插；一次 SaveChangesAsync |
| `TaiwanAgri.Web/Extensions/UserModuleExtensions.cs` | A | AddUserModule() Extension Method |
| `TaiwanAgri.Web/Controllers/ProfileController.cs` | A | [Authorize]；從 JWT Claims 取 userId；GET/PUT 各一個 Action |
| `TaiwanAgri.Web/Program.cs` | M | 加入 AddUserModule() |
| `TaiwanAgri.Web.csproj` | M | 加入 TaiwanAgri.Modules.User Project Reference |
| `TaiwanAgri.Worker/Program.cs` | M | 確認 Worker 不需要 UserDbContext（已移除，僅後端 Web 需要） |
| `TaiwanAgriPlatform.sln` | M | 加入 TaiwanAgri.Modules.User Project |
| `TaiwanAgri.Frontend/src/api/profile.ts` | A | profileApi + TypeScript 介面 |
| `TaiwanAgri.Frontend/src/stores/profile.ts` | A | Pinia profile store |
| `TaiwanAgri.Frontend/src/views/ProfileView.vue` | A | 農場設定表單 + Autocomplete 作物搜尋 |
| `TaiwanAgri.Frontend/src/router/index.ts` | M | 加入 /profile 路由 |
| `TaiwanAgri.Frontend/src/components/TopNav.vue` | M | 已登入狀態加入「農場設定」連結 |

---

## 閱讀之後：給你的觀察指南

這個 PR 最值得思考的是**「主鍵設計如何影響 API 設計」**。

選擇 UserId 當 PK，帶來的不只是「資料庫保證唯一性」這一個好處，它讓整個 API 變得更簡單：沒有 `POST /api/profile/farm`（因為沒有「建立」這個動作的概念）；PUT 的語意從「更新一個你已知 id 的資源」變成「把這份設定存起來」；GET 不需要任何參數，後端自己知道要查誰的資料。

PK 的選擇是業務模型的決策，不是資料庫技術的決策。選了 `int Id` 就是說「一個使用者可以有多份設定」；選了 `UserId` 就是說「這是一對一的關係」。這個選擇決定了後續所有 API、前端、邏輯的形狀。

另一個值得注意的是 **EF Core 的 HasPrincipalKey**。大多數時候，EF Core 的約定設定可以自動推導出正確的關聯，但當 PK 是非常規型別（string）且兩端都有導覽屬性時，自動推導會產生錯誤的 shadow property。這個 bug 的症狀（「Invalid column name 'UserFarmProfileUserId'」）讓我理解了 EF Core 的命名慣例：它用「導覽屬性名稱 + 主表 PK 名稱」組合出外鍵欄位名。明確指定 `HasPrincipalKey` 是告訴 EF Core「不要猜了，主表的 Key 就是 UserId」。

---

# PR #035 — W17 UserWatchlist 完整實作：Entity + Service + Controller + 前端三層 + 路由守衛強化

**標題**：`feat(user): UserWatchlist Entity + Migration + Service（去重/防越權）+ WatchlistController（Pattern C）+ Vue 3 監看清單頁 + beforeEach return 語法升級`

---

## 背景與動機

W17 目標是實作使用者監看清單（UserWatchlist）功能，讓農民能夠保存「我想持續追蹤哪些作物在哪個市場的行情」這份偏好設定，作為個人化儀表板的資料基礎。

本 PR 同時完成四件事：

1. **後端資料層**：UserWatchlist Entity + UserDbContext Fluent API（無導覽屬性關聯寫法）+ Migration
2. **後端服務層**：IUserWatchlistService + UserWatchlistService，包含去重防護與越權刪除防護
3. **後端控制層**：WatchlistController，採用 Controller 層組合架構（Pattern C）
4. **前端三層**：api / Pinia Store / WatchlistView.vue，完整 CRUD 體驗，含 409 衝突處理與 redirect 登入流程

---

## 架構決策：UserWatchlist 的跨模組資料問題

### 問題背景

監看清單的靜態偏好（UserId、CropCode、MarketCode）存在 UserDbContext，但使用者最終想看的是「最新價格」——這份資料在 MarketDbContext。如何組合這兩份資料？

### 三個方案的取捨

| 方案 | 說明 | 問題 |
|------|------|------|
| 方案 A：直接注入跨模組 DbContext | UserWatchlistService 同時注入 UserDbContext + MarketDbContext | 模組邊界崩潰，User 模組直接依賴 Market 模組 |
| 方案 B：HTTP 呼叫 | UserWatchlistService 呼叫 /api/market/prices 取得價格 | 同一個 process 內繞一圈 HTTP，無謂開銷 |
| **方案 C：Controller 層組合（本 PR 選擇）** | WatchlistController 分別注入 IUserWatchlistService + IMarketService，各自取資料後在 Controller 組合 | 無；邊界清楚，IMarketService 已實作可直接重用 |

**面試說法**：「我的判斷依據是『誰的職責是什麼』。Service 層的職責是業務邏輯，不是跨模組協調。Controller 層本來就是組合資料、回應請求的位置，讓 Controller 分別拿兩個 Service 的資料再組合，比讓 Service 知道另一個模組更符合職責分離。」

---

## 實作內容

### 一、UserWatchlist Entity 設計

**欄位設計決策：快照而非 FK**

```csharp
public class UserWatchlist
{
    [Key]
    public int Id { get; set; }

    [Required, StringLength(450)]
    public string UserId { get; set; } = string.Empty;

    [Required, StringLength(10)]
    public string CropCode { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string CropName { get; set; } = string.Empty;   // 快照

    [StringLength(10)]
    public string? MarketCode { get; set; }

    [StringLength(100)]
    public string? MarketName { get; set; }               // 快照，nullable
}
```

CropName 和 MarketName 存快照的原因：CropInfos 在 MarketDbContext，MarketInfos 同樣在 MarketDbContext，跨 DbContext 無法 EF Core JOIN。快照讓查詢不需要跨界，顯示名稱不需要額外 API 呼叫。

MarketCode/MarketName nullable 的語意：null 代表「全台均價」，是合法的業務狀態，不是資料缺失。

---

### 二、UserDbContext Fluent API：無導覽屬性的 HasOne<T>() 寫法

UserWatchlist 故意不加導覽屬性，原因是使用場景只需要「給我某個 UserId 的所有 Watchlist」，永遠不需要從一筆 Watchlist 反查 UserFarmProfile。

沒有導覽屬性時，EF Core 的關聯設定寫法：

```csharp
// PR #034 UserFarmCrop：有導覽屬性
entity.HasOne(c => c.UserFarmProfile)
      .WithMany(p => p.Crops)
      ...

// PR #035 UserWatchlist：無導覽屬性，改用泛型
entity.HasOne<UserFarmProfile>()   // ← 泛型，不是 lambda
      .WithMany()                   // ← 空括號，主表無對應集合屬性
      .HasForeignKey(c => c.UserId)
      .HasPrincipalKey(p => p.UserId)
      .OnDelete(DeleteBehavior.Cascade);

entity.HasIndex(c => c.UserId);
```

`HasOne<T>()` 泛型寫法是「我知道這個關聯指向哪個 Entity，但我不需要導覽屬性」的標準 EF Core 表達方式。

---

### 三、Service 層安全設計：userId 參數的兩個理由

```csharp
public interface IUserWatchlistService
{
    Task<IEnumerable<WatchlistItemDto>> GetUserWatchlistItemsAsync(string userId);
    Task<bool> AddWatchlistItemAsync(string userId, AddWatchlistRequestDto request);
    Task RemoveWatchlistItemsAsync(string userId, IEnumerable<int> ids);
}
```

異動方法都帶 `userId` 參數，有兩個獨立的理由：

**理由一（架構）**：Service 層沒有 HTTP Context，無法存取 JWT Claims。`User.FindFirstValue(ClaimTypes.NameIdentifier)` 只能在 Controller 層呼叫，Controller 取出後向下傳遞給 Service。

**理由二（安全）**：刪除時同時比對 id 和 userId，確保使用者只能刪自己的資料：

```csharp
var targetItems = context.UserWatchlists
    .Where(w => w.UserId == userId && ids.Contains(w.Id));
context.UserWatchlists.RemoveRange(targetItems);
```

如果只傳 ids，惡意使用者猜到別人的 Watchlist Id 後可以直接刪除他人資料。雙重條件讓越權刪除在 Service 層被攔截。

---

### 四、AddWatchlistItemAsync 的去重邏輯與 HTTP 語意

**去重防護：AnyAsync 而非 Distinct**

兩者語意完全不同：
- `Distinct`：「回傳時過濾重複」，重複資料已存入 DB
- `AnyAsync`：「存入前先確認是否已存在」，攔截在 SaveChanges 之前

```csharp
var exists = await context.UserWatchlists
    .AnyAsync(w => w.UserId == userId
                && w.CropCode == request.CropCode
                && w.MarketCode == request.MarketCode);

if (exists) return false;
// 繼續新增 ...
return true;
```

**回傳 bool 而非拋例外**

Service 回傳 bool，Controller 決定 HTTP 狀態碼，職責分離的正確體現：

```csharp
var success = await userWatchlistService.AddWatchlistItemAsync(userId, request);
if (!success) return Conflict("此作物與市場組合已在監看清單中");
return NoContent();
```

409 Conflict 是語意正確的狀態碼：「請求本身合法，但因資源狀態衝突無法完成」。

---

### 五、DELETE 的 [FromQuery] 設計

```csharp
[HttpDelete]
public async Task<IActionResult> RemoveWatchlistItems([FromQuery] IEnumerable<int> ids)
```

DELETE 請求帶 Request Body 在部分 Proxy 和早期 HTTP Client 實作上有相容性問題。使用 Query String 是更安全的選擇。

前端對應：axios 預設陣列展開格式（`ids[0]=1`）與 ASP.NET Core 的 `[FromQuery]` 不相容，需要用 `URLSearchParams` 手動控制：

```typescript
removeItems(ids: number[]): Promise<void> {
  const params = new URLSearchParams()
  ids.forEach(id => params.append('ids', String(id)))
  return authClient.delete('/api/watchlist', { params }).then(() => undefined)
}
```

---

### 六、Vue Router beforeEach：return 取代 next()

Vue Router v4 將 `next()` callback 標記為 deprecated，改用 return 值：

```typescript
// ❌ 舊寫法（deprecated warning）
router.beforeEach((to, _from, next) => {
  if (condition) next({ name: 'login', query: { redirect: to.fullPath } })
  else next()
})

// ✅ 新寫法（v4 原生）
router.beforeEach((to, _from) => {
  if (condition) return { name: 'login', query: { redirect: to.fullPath } }
  return true
})
```

語意完全一致：return 物件 = 導向，return true = 放行，return false = 取消導航。

---

### 七、登入後 redirect 跳轉

路由守衛把原始目標路徑存進 query string：

```typescript
return { name: 'login', query: { redirect: to.fullPath } }
```

LoginView 登入成功後讀取並跳轉：

```typescript
const route = useRoute()
const redirect = (route.query.redirect as string) || '/'
router.push(redirect)
```

使用者原本要去 `/watchlist`，被踢到 `/login`，登入成功後自動跳回 `/watchlist`。

---

### 八、前端 Store 錯誤狀態管理與表單重置邏輯

```typescript
// Store：操作開始前清除舊訊息，捕捉 409 vs 其他錯誤
async function addItem(request: AddWatchlistRequest) {
  errorMessage.value = null
  try {
    await watchlistApi.addItem(request)
    await fetchItems()
  } catch (err: any) {
    if (err?.response?.status === 409) {
      errorMessage.value = '此作物與市場組合已在監看清單中'
    } else {
      errorMessage.value = '新增失敗，請稍後再試'
    }
  }
}

// View：只有成功（沒有 errorMessage）才重置表單
await store.addItem({ ... })
if (!store.errorMessage) {
  selectedCrop.value = null
  selectedMarketCode.value = null
}
```

重新選作物時清除錯誤訊息（`selectCrop` / `clearCrop` 各加一行 `store.errorMessage = null`），確保舊錯誤不會殘留到下次送出。

---

## 關鍵設計決策彙整

| 決策 | 選擇 | 理由 |
|------|------|------|
| 跨模組資料組合 | Controller 層組合（Pattern C） | Service 層不應跨模組依賴；Controller 本就是組合層 |
| 無導覽屬性關聯 | HasOne<UserFarmProfile>() 泛型寫法 | 查詢場景不需要反向導航，不加導覽屬性是有意為之 |
| userId 在 Service 方法簽名 | 必須傳入 | 架構（Service 無 HTTP Context）+ 安全（防越權刪除）兩個獨立理由 |
| 去重防護 | AnyAsync + 回傳 bool | 攔截在寫入前；bool 讓 Controller 決定 HTTP 語意 |
| 重複衝突 HTTP 狀態碼 | 409 Conflict | 語意精確：資源狀態衝突，非格式錯誤 |
| DELETE 參數傳遞 | [FromQuery] + URLSearchParams | Body 在 DELETE 的跨實作相容性問題 |
| beforeEach 語法 | return 取代 next() | Vue Router v4 官方推薦，消除 deprecated warning |
| 勾選狀態 | View 層 ref，不進 Store | 瞬間 UI 狀態，無跨元件/跨頁面需求 |
| 新增/刪除後重新 fetch | fetchItems() | 資料集小，保證 UI 與 DB 完全一致 |

---

## 不修項目說明

| 項目 | 說明 |
|------|------|
| WatchlistController 的 IMarketService 注入 | 已注入但本 PR 未使用，為後續「顯示即時價格」功能預留入口 |
| 市場下拉只載入蔬菜市場 | onMounted 只呼叫 getMarkets('Veg')，後續 PR 可擴充 |
| Watchlist 即時價格組合 | Pattern C 架構已就位，Controller 層補呼叫 IMarketService 即可 |

---

## 驗收標準

- [x] 未登入直接訪問 `/watchlist` → 被導向 `/login?redirect=/watchlist`
- [x] 登入成功後 → 自動跳回 `/watchlist`
- [x] `GET /api/watchlist`（未帶 token）→ 401
- [x] 新增監看項目 → 清單出現新增資料
- [x] 重複新增同一作物+市場組合 → 顯示「已存在」提示，表單保留
- [x] 勾選多筆後刪除 → 清單移除對應項目
- [x] Console 無 `[Vue Router warn]: The next() callback is deprecated` 警告

---

## 檔案異動清單

| 檔案 | 異動 | 說明 |
|------|------|------|
| `TaiwanAgri.Modules.User/Entities/UserWatchlist.cs` | A | Entity 定義 |
| `TaiwanAgri.Modules.User/Data/UserDbContext.cs` | M | DbSet + Fluent API |
| `TaiwanAgri.Modules.User/Migrations/20260609164901_AddNewTableUserWatchList.cs` | A | Migration |
| `TaiwanAgri.Modules.User/Migrations/20260609164901_AddNewTableUserWatchList.Designer.cs` | A | 設計器快照 |
| `TaiwanAgri.Modules.User/Migrations/UserDbContextModelSnapshot.cs` | M | 模型快照更新 |
| `TaiwanAgri.Modules.User/Dtos/ApiRequests/AddWatchlistRequestDto.cs` | A | 新增請求 DTO |
| `TaiwanAgri.Modules.User/Dtos/ApiResponses/WatchlistItemDto.cs` | A | 回應 DTO |
| `TaiwanAgri.Modules.User/Services/IUserWatchlistService.cs` | A | 介面定義 |
| `TaiwanAgri.Modules.User/Services/UserWatchlistService.cs` | A | 實作：去重 + 防越權 |
| `TaiwanAgri.Web/Controllers/WatchlistController.cs` | A | [Authorize] + Pattern C + [FromQuery] DELETE |
| `TaiwanAgri.Web/Controllers/ProfileController.cs` | M | 無邏輯異動 |
| `TaiwanAgri.Web/Extensions/UserModuleExtensions.cs` | M | 新增 Scoped 註冊 |
| `TaiwanAgri.Frontend/src/api/watchlist.ts` | A | watchlistApi + URLSearchParams |
| `TaiwanAgri.Frontend/src/stores/watchlist.ts` | A | Pinia store + 409 分流 |
| `TaiwanAgri.Frontend/src/views/WatchlistView.vue` | A | Autocomplete + 勾選多刪 |
| `TaiwanAgri.Frontend/src/router/index.ts` | M | /watchlist 路由 + return 語法 |
| `TaiwanAgri.Frontend/src/views/auth/LoginView.vue` | M | redirect query 跳轉 |
| `TaiwanAgri.Frontend/src/components/TopNav.vue` | M | 監看清單連結 |

---

## 閱讀之後：給你的觀察指南

這個 PR 最值得思考的是**「同一個 userId 參數，有兩個完全獨立的存在理由」**。

架構理由和安全理由剛好都指向同一個設計，這不是巧合，而是「好的架構邊界往往也帶來安全性」的體現。Service 層不知道 HTTP Context（架構邊界），所以只能接受呼叫方傳入的 userId；傳入的 userId 用於 WHERE 條件（安全邊界），越權操作在資料層就被攔截。兩個理由互相補強。

另一個值得注意的是 **HasOne<T>() vs HasOne(lambda) 的選擇依據**。選哪種取決於查詢需求，不是哪種寫起來更簡單。沒有導覽屬性是一個有意識的設計決策：「我不需要從 Watchlist 反查 Profile，所以不加這個屬性，讓 Entity 更輕。」

最後，**勾選狀態不進 Store** 展示了一個重要習慣：Store 是成本，不是免費的工具。每次問「這個狀態需要進 Store 嗎」，等同於在問「有跨元件/跨頁面的需求嗎」。沒有的話，留在 View 層更輕量。

---

# PR #036 — W17 Code Review 修正：必修項目 + 命名/文件建議改善 + 跨模組耦合消除

**標題**：`fix(code-review): RabbitMQ hostname 設定外化 + AuthService Fail-Fast + 命名一致性 + 跨模組耦合消除`

---

## 背景與動機

本 PR 是針對 Code Review 回饋的集中修正，不包含新功能。所有修正項目分為三個層次：

1. **必修**：部署正確性問題，在 Docker 環境下會直接導致服務啟動失敗或設定讀取錯誤
2. **建議修正**：命名一致性、magic number 抽取、文件補強，提升可維護性與面試可讀性
3. **額外處理**：Code Review 過程中發現但原始清單未列的問題

---

## 一、RabbitMQ hostname 設定外化

### 問題

`PriceUpdatedConsumer.cs` 和 `AgriProductsTransSyncWorker.cs` 裡的 `ConnectionFactory` 均 hardcode `HostName = "localhost"`。

在本機開發時沒有問題，因為所有服務跑在同一台機器上，`localhost` 連得到。但在 Docker Compose 環境下，每個服務跑在獨立容器，容器內的 `localhost` 指的是「自己這個容器」，不是 RabbitMQ 容器。Docker Compose 會自動為同一個 compose 裡的服務建立 DNS，service name 就是 hostname，連 RabbitMQ 應該用 `rabbitmq`。

### 修正方式

**兩個 .cs 檔案**：建構子注入 `IConfiguration`，改用：

```csharp
var factory = new ConnectionFactory
{
    HostName = _configuration["RabbitMQ:HostName"] ?? "localhost"
};
```

`?? "localhost"` 是最後的安全網，確保本機開發在 appsettings 沒有此 key 時也能跑。

**appsettings.json（Web + Worker 各一份）**：

```json
"RabbitMQ": {
  "HostName": "localhost"
}
```

本機開發讀到 `localhost`，Docker 環境由環境變數覆蓋。

**docker-compose.yml**：web 和 worker 服務各自加入：

```yaml
environment:
  - RabbitMQ__HostName=rabbitmq
```

.NET 的設定系統會自動用環境變數的值覆蓋 appsettings.json 裡的對應 key（`__` 對應 `:` 分隔符）。這樣兩個環境各自讀到正確的 hostname，不需要維護兩份設定檔。

---

## 二、AuthService + IdentityExtensions Fail-Fast

### 問題

`AuthService.GenerateJwtToken` 裡兩處使用 `!` 強制解 null：

```csharp
var secretKey = _configuration["Jwt:SecretKey"]!;
var expiresInDays = int.Parse(_configuration["Jwt:ExpiresInDays"]!);
```

`!` 的語意是「我向編譯器保證這不是 null」，但這個保證沒有執行期的保障。如果 appsettings 缺少對應 key，會在有人登入的瞬間炸掉，而不是在應用程式啟動時就報錯，增加了排查難度。

同樣的問題出現在 `IdentityExtensions.cs` 的 JWT middleware 設定：

```csharp
var secretKey = configuration["Jwt:SecretKey"]!;   // 同樣的 !
ValidAudience = configuration["Jwt:Issuer"],        // 錯用 Issuer 當 Audience
```

### 修正方式

**建構子加 Fail-Fast**（應用程式啟動時 DI 容器建立 `AuthService` 就檢查）：

```csharp
public AuthService(...)
{
    // ...
    _ = configuration["Jwt:SecretKey"]
        ?? throw new InvalidOperationException("Jwt:SecretKey 未設定");
    _ = configuration["Jwt:ExpiresInDays"]
        ?? throw new InvalidOperationException("Jwt:ExpiresInDays 未設定");
    _ = configuration["Jwt:Audience"]
        ?? throw new InvalidOperationException("Jwt:Audience 未設定");
}
```

**`GenerateJwtToken` 裡改用 `int.TryParse`**：

```csharp
var expiresInDaysStr = _configuration["Jwt:ExpiresInDays"]
    ?? throw new InvalidOperationException("Jwt:ExpiresInDays 未設定");

if (!int.TryParse(expiresInDaysStr, out var expiresInDays))
    throw new InvalidOperationException("Jwt:ExpiresInDays 必須是整數");
```

**appsettings.json 新增 `Jwt:Audience`**，issuer 與 audience 語意正確分開：

```json
"Jwt": {
  "Issuer": "TaiwanAgriPlatform",
  "Audience": "TaiwanAgriPlatform-Frontend",
  "ExpiresInDays": 7
}
```

### Issuer vs Audience 的語意

| 欄位 | 語意 | 比喻 |
|------|------|------|
| `Issuer` | 這個 token 是誰發的（後端） | 票務公司（KKTIX） |
| `Audience` | 這個 token 是發給誰用的（前端） | 場地（台北小巨蛋） |

現在的系統是單體架構，`Issuer == Audience` 技術上完全可行。拆開的價值在未來：若加入管理後台（audience = `TaiwanAgriPlatform-Admin`），用戶 token 拿去打管理後台，audience 不符合，直接 401 擋掉。語意上的正確性是後續擴充的基礎。

---

## 三、方法命名複數錯誤修正

### 問題

`IUserProfileService` 和 `UserProfileService` 的方法名稱：

```csharp
// 修正前（錯誤）
Task<UserFarmProfile?> GetUsersFarmProfileAsync(string userId);
Task UpsertUsersFarmProfileAsync(string userId, ...);

// 修正後（正確）
Task<UserFarmProfile?> GetUserFarmProfileAsync(string userId);
Task UpsertUserFarmProfileAsync(string userId, ...);
```

`Users` 複數在這裡語意是錯的。這兩個方法的語意是「取得/更新某個指定 userId 的農場資料」，是單用戶操作，不是批次操作。複數形式會讓閱讀者誤以為這是回傳多個用戶資料的方法，語意誤導。

---

## 四、建議修正清單

### rest-days endpoint 命名

```csharp
// 修正前
[HttpGet("restDays")]

// 修正後
[HttpGet("rest-days")]
```

統一與其他 endpoint 的 kebab-case 風格（`markets`、`crops`、`disasters`、`prices`）。

### cropCodes 上限抽成設定值

```csharp
// 修正前
if (cropCodes.Length > 5) return BadRequest("cropCodes 最多只能傳入 5 個");

// 修正後
if (cropCodes.Length > _cropCodesMaxCount)
    return BadRequest($"cropCodes 最多只能傳入 {_cropCodesMaxCount} 個");
```

`5` 是業務規則，不是程式邏輯。抽到 appsettings 的 `MarketQueryLimits:CropCodesMaxCount` 後，調整上限不需要改程式碼。錯誤訊息也動態帶入，保持一致。

### MarketController 重複驗證字串抽成 const

```csharp
private const string InvalidMarketTypeMessage = "marketType 必須為 Veg、Fruit 或 Flower";
```

同一條業務規則在 `GetMarkets` 和 `GetCrops` 各出現一次。抽成 const 之後，改訊息只需改一處。

### allCrops → cropSearchPool

`ProfileView.vue` 中 `allCrops` 更名為 `cropSearchPool`，語意更精確——這個 ref 存放的是作物搜尋候選池，不是「所有作物的完整清單」。

### UserFarmCrop.CropName 快照設計說明

```csharp
// Snapshot: intentionally denormalized, not a FK join
// 快照欄位：CropName 來自 MarketDbContext 的 CropInfos
// 跨 DbContext 無法 JOIN，故在寫入時複製一份到 UserDbContext
// 代價是資料可能與來源略有落差，但農產品名稱極少變動，可接受
[MaxLength(50)]
public string? CropName { get; set; }
```

說明了「為什麼」，而不只是「怎麼做」。

### UserProfileService 全刪全插上限假設說明

```csharp
// 作物清單：全刪全插（選 A）
// 理由：農民種 3-10 種作物，數量少，全刪全插比 diff 比對簡單可靠
// 前提假設：單一用戶作物數量有上限（API 層限制最多 5 個 cropCode）
// 若未來開放大量作物，應改為 diff 比對策略
```

補充了前提假設和未來改策略的觸發條件，讓維護者知道這個設計的邊界在哪。

### cts → httpTimeoutCts

```csharp
// 修正前
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));

// 修正後
using var httpTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
```

名稱說明這個 token 的用途是 HTTP 請求超時控制。

---

## 五、額外處理

### PriceUpdatedConsumer TODO tag + LogWarning

```csharp
// TODO(W15): implement cache invalidation
// 骨架階段：清除所有 market:prices 開頭的 key，目前尚未實作
_logger.LogWarning("[PriceUpdatedConsumer] Cache invalidation 尚未實作，跳過");
```

骨架行為的 log 等級從 `Information` 改為 `Warning`，清楚標示這是一個不完整的行為。加上 `// TODO(W15):` tag 方便日後 grep 找到所有待辦項目。

### ProfileView 跨模組耦合消除

**修正前**：`ProfileView.vue` 直接 import `marketApi`（Market 模組的 API 層），Profile 模組對 Market 模組產生直接依賴。

```typescript
// 問題
import { marketApi } from '../api/market'
```

如果 `market.ts` 的 `getCrops` 介面改動（改名、改參數），`ProfileView.vue` 就要跟著改，但改 Market 模組時通常不會想到要去找 Profile 的 View 檔。

**修正後**：新增 `cropApi.ts` 作為封裝層。

```typescript
// cropApi.ts（新增）
export async function getAllCrops(): Promise<CropItem[]> {
  const [veg, fruit, flower] = await Promise.all([
    marketApi.getCrops('Veg'),
    marketApi.getCrops('Fruit'),
    marketApi.getCrops('Flower'),
  ])
  return [...veg, ...fruit, ...flower]
}

// ProfileView.vue（修正後）
import { getAllCrops } from '../api/cropApi'
cropSearchPool.value = await getAllCrops()
```

`ProfileView` 不再知道背後打的是哪個 API，Market 模組的介面變動只需要修改 `cropApi.ts` 一個地方。

---

## 關鍵設計決策彙整

| 決策 | 選擇 | 理由 |
|------|------|------|
| RabbitMQ hostname 讀取 | IConfiguration + 環境變數覆蓋 | 本機 / Docker 兩個環境共用同一份 .cs，行為由設定決定 |
| Fail-Fast 驗證位置 | 建構子（DI 建立時） | 啟動即報錯，不等到第一個請求進來才炸 |
| `int.TryParse` 取代 `int.Parse(!)` | TryParse + 明確例外 | 格式錯誤有清楚的錯誤訊息，不是 FormatException |
| Issuer / Audience 分開 | 各自獨立 key | 語意正確，為未來多 audience 場景奠定基礎 |
| cropCodes 上限 | appsettings 設定值 | 業務規則不應 hardcode 在程式碼，調整不需重新編譯 |
| 重複字串抽 const | 同一方法簽名 class 頂部 | 業務規則只寫一次，修改成本最低 |
| 跨模組 API 呼叫 | 封裝到 cropApi.ts | Profile 模組不直接依賴 Market 模組實作 |

---

## 不修項目說明

| 項目 | 理由 |
|------|------|
| IJwtTokenGenerator / ICurrentUserProvider 等介面抽象 | 測試基礎設施，推遲到 W19-20 測試 sprint |
| GlobalExceptionMiddleware | 同上，和測試覆蓋一起規劃 |
| 日期格式錯誤字串（4 處） | 兩個 const，各自語意獨立，改動機率極低，抽取收益低於閱讀成本 |

---

## 驗收標準

- [x] 本機 `dotnet run` — RabbitMQ 連線正常（appsettings 讀到 localhost）
- [x] `appsettings.json` 缺少 `Jwt:SecretKey` 時，應用程式啟動立刻報錯，而非等到登入才炸
- [x] `GET /api/market/rest-days` 回應正常（endpoint 路徑改動後仍可 reach）
- [x] `GET /api/market/prices?cropCodes=A&cropCodes=B&cropCodes=C&cropCodes=D&cropCodes=E&cropCodes=F` → 400 BadRequest（6 個超過上限 5 個）
- [x] ProfileView 農場設定頁作物搜尋正常運作（cropApi.ts 封裝後功能不變）
- [x] Console 無新的 warning 或 error

---

## 檔案異動清單

| 檔案 | 異動 | 說明 |
|------|------|------|
| `docker-compose.yml` | M | 移除 sqlserver 誤植行；web / worker 加入 RabbitMQ__HostName |
| `TaiwanAgri.Frontend/src/api/cropApi.ts` | A | 封裝三市場作物查詢，消除 ProfileView 跨模組依賴 |
| `TaiwanAgri.Frontend/src/views/ProfileView.vue` | M | 改用 cropApi.getAllCrops()；allCrops → cropSearchPool |
| `TaiwanAgri.Modules.User/Entities/UserFarmCrop.cs` | M | CropName 加快照設計說明 comment |
| `TaiwanAgri.Modules.User/Services/IUserProfileService.cs` | M | 方法命名複數 → 單數 |
| `TaiwanAgri.Modules.User/Services/UserProfileService.cs` | M | 方法命名同步修正；全刪全插加上限假設 comment |
| `TaiwanAgri.Web/Controllers/MarketController.cs` | M | rest-days kebab-case；InvalidMarketTypeMessage const；cropCodesMaxCount 設定化 |
| `TaiwanAgri.Web/Controllers/ProfileController.cs` | M | 方法呼叫端更新為單數命名 |
| `TaiwanAgri.Web/Extensions/IdentityExtensions.cs` | M | Jwt:SecretKey ! 改 Fail-Fast；ValidAudience 改讀 Jwt:Audience |
| `TaiwanAgri.Web/Services/AuthService.cs` | M | 建構子 Fail-Fast；int.TryParse；audience 改讀 Jwt:Audience |
| `TaiwanAgri.Web/Services/PriceUpdatedConsumer.cs` | M | LogWarning；TODO(W15) tag |
| `TaiwanAgri.Worker/Services/AgriProductsTransSyncWorker.cs` | M | RabbitMQ hostname 設定化；cts → httpTimeoutCts |
| `TaiwanAgri.Web/appsettings.json` | M | 新增 RabbitMQ 區段；Jwt:Audience |
| `TaiwanAgri.Worker/appsettings.json` | M | 新增 RabbitMQ 區段 |

---

## 閱讀之後：給你的觀察指南

這個 PR 最值得思考的是**「同一個問題，本機測試永遠是好的，部署才炸」**的類型。

RabbitMQ hostname hardcode 就是這種問題的典型。本機跑完全正常，開發期間不會感覺到任何問題，一進 Docker 就連線失敗，而且錯誤訊息是 RabbitMQ 連線失敗，不是「hostname 寫錯了」。把設定值外化到 `appsettings.json` + 環境變數覆蓋，是讓「本機正常 / 部署正常」這兩件事能同時成立的標準做法。

**Fail-Fast 的價值不只是錯誤訊息更清楚。** 更根本的價值是讓問題在最早的時間點暴露——應用程式啟動時，而不是「剛好有人登入的那一刻」。一個啟動就炸的服務，比一個平時正常、偶爾炸的服務更容易診斷。

**跨模組耦合的問題不是現在會炸，而是未來你不知道改了什麼東西。** `ProfileView` 直接 import `marketApi`，在功能上完全沒問題。問題在於：改 `market.ts` 的時候，沒有任何工具或規範會提示你「ProfileView 也用了這個」。`cropApi.ts` 的存在讓模組邊界在程式碼結構上可見，不只存在於文件描述裡。

---

## 閱讀之後：給你的觀察指南

讀完PR_DESCRIPTION，你會發現每一篇都有固定的段落結構：

**背景與動機**回答「為什麼要做這件事」，而不是「我做了什麼」。一個 PR 如果只說做了什麼，六個月後你自己都不知道當初為什麼這樣決定。

**關鍵設計決策**是最有價值的部分。每一個決策都有「有哪些選項」和「為什麼選這個而不選那個」。這才是工程思維的展示，不是「我新增了一個類別叫 WeatherSyncWorker」。

**驗收標準**讓讀 PR 的人（包括 code reviewer 或面試官）知道怎麼確認這個 PR 是真的可以動的，而不是只是程式碼看起來對。

你可以注意一下，哪些部分是你現在讀了覺得「對，我確實做了這個決定，我知道為什麼」，哪些是「我有做，但當時沒有意識到這是個決策」。後者就是你在未來開發中，紙筆推導最需要捕捉的東西。
