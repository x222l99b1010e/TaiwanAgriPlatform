# TaiwanAgriPlatform — PR Description 合集 W1-W6

這份文件收錄 W1 到 W6 已完成工作的 PR Description。
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

## PR #002 — WeatherObservations 資料表設計與第一個 Migration

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

## PR #003 — WeatherSyncWorker：能打 API + 資料寫入資料庫

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

## PR #004 — WeatherSyncWorker：修正防重複邏輯 + 處理分頁商業限制

**標題**：`fix: 重寫防重複邏輯（HashSet 取代 latestTime）+ 處理 MOA API 分頁限制`

**背景與動機**

PR #003 的版本能動，但防重複邏輯有根本缺陷，且發現農業部 API 的分頁功能有商業限制，需要正確處理。這個 PR 是在功能能動之後、做程式碼審查時發現問題並修正的。

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

## PR #005 — Identity Migration 提前執行：讓 AspNetUsers 表存在

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

## PR #006 — PestAlerts 資料表設計與 Migration

**標題**：`feat: 設計 PestAlerts + PestAlertCities + PestAlertCrops 資料表並執行 Migration`

**背景與動機**

模組 2 的病蟲害警報牆需要把農業部 `PlantEpidemicType` API 的公告資料落地資料庫。原因是 API 資料需要「依縣市 + 作物」的複合篩選，直打 API 無法做到這種聚合查詢，必須先落地才能用 SQL 查詢。

**關鍵設計決策：主表 + 兩張關聯表，取代 JSON 欄位**

一筆病蟲害公告可能涉及多個縣市、多種作物（例如「南投縣、嘉義縣的高麗菜、青椒」）。設計上有兩個選項：一是把縣市列表和作物列表存成 JSON 欄位（`string`），二是建立獨立的關聯表 `PestAlertCities` 和 `PestAlertCrops`。

選擇關聯表而非 JSON 欄位，原因是查詢性質：「找出所有涉及南投縣的警報」這個查詢，在 JSON 欄位上需要 LIKE 或 JSON 函數，效能差且難以建立索引；在關聯表上只需要 `JOIN + WHERE CityName = '南投縣'`，可以在 `CityName` 欄位建立索引，效能好得多。

**關鍵設計決策：SourceHash 防重複**

`PlantEpidemicType` API 每日全量回傳所有公告，每次同步都要判斷哪些是新的、哪些是已存在的。用公告 ID 比對不夠穩定（API 回傳的 ID 格式不固定），因此改為對公告的核心欄位（`公告日期 + 縣市 + 作物名稱 + 標題`）組合後計算 SHA256 Hash，存入 `SourceHash` 欄位並設為 UNIQUE 索引。

每次同步時，把 API 回傳的每筆公告計算 Hash，先查 HashSet（從 DB 預載入），Hash 存在的跳過，不存在的才寫入。這個方式不依賴任何 ID 欄位，即使 API 格式改變，只要內容相同 Hash 就相同，防重複邏輯就成立。

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

## 閱讀之後：給你的觀察指南

讀完這六篇，你會發現每一篇都有固定的段落結構：

**背景與動機**回答「為什麼要做這件事」，而不是「我做了什麼」。一個 PR 如果只說做了什麼，六個月後你自己都不知道當初為什麼這樣決定。

**關鍵設計決策**是最有價值的部分。每一個決策都有「有哪些選項」和「為什麼選這個而不選那個」。這才是工程思維的展示，不是「我新增了一個類別叫 WeatherSyncWorker」。

**驗收標準**讓讀 PR 的人（包括 code reviewer 或面試官）知道怎麼確認這個 PR 是真的可以動的，而不是只是程式碼看起來對。

你可以注意一下，哪些部分是你現在讀了覺得「對，我確實做了這個決定，我知道為什麼」，哪些是「我有做，但當時沒有意識到這是個決策」。後者就是你在未來開發中，紙筆推導最需要捕捉的東西。
