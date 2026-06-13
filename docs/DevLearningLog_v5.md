# 開發者學習日誌
### TaiwanAgriPlatform — Developer Learning Log v5

> 這份文件記錄的不是「我做了什麼」，而是「我是怎麼想通的」。
> 給六個月後的自己看。每完成一個 PR，補一個條目。

---

## 如何使用這份文件

每個條目有五個欄位。「我遇到的問題」和「我怎麼想通的」是最重要的兩欄，花最多時間寫這兩個。「下次遇到類似情況，我會先想到什麼」用一句話寫，逼自己把推導過程壓縮成直覺。

---

## W1-W2

### 條目 001 — Docker 只跑基礎設施，.NET 應用跑在本機

**我做了什麼**
設定 docker-compose.yml，決定哪些東西放進容器、哪些不放。

**我遇到的問題**
一開始沒有想清楚「全部容器化」和「只容器化基礎設施」的差別。如果 .NET Worker 也放進 Docker，F5 除錯就失效了，每次改程式碼都要重新 build image 再等容器重啟。這個代價在當下不明顯，但如果真的這樣做，後面幾個月的開發效率會慢很多。

**我怎麼想通的**
問自己一個問題：「我開發時最常做的動作是什麼？」答案是修改程式碼、F5 跑起來、看 Log。這個流程如果變慢，整個開發體驗就變差。Docker 的價值是讓基礎設施（資料庫、快取、佇列）一行指令啟動，不是讓 .NET 應用也一起進去。兩件事分開想，決策就清楚了。

**我學到的原則**
工具是為了解決問題存在的，不是因為「這個技術很酷」就全部用上。每次引入一個新工具，先問「它解決了什麼問題」，再問「它帶來了什麼代價」。

**下次遇到類似情況，我會先想到什麼**
容器化基礎設施，應用程式本機執行，這是 Side Project 開發階段的預設選擇。

---

### 條目 002 — 8 個 Project 的邊界為什麼這樣劃

**我做了什麼**
建立 Solution，決定要切幾個 Project，每個 Project 負責什麼。

**我遇到的問題**
不知道「幾個 Project 算合理」。太少的話所有東西混在一起，太多的話管理麻煩。而且不知道 `TaiwanAgri.Core` 這個零依賴的共用層為什麼要獨立出來，感覺直接放在 Web 裡就好。

**我怎麼想通的**
如果 Core 放在 Web 裡，Worker 要用 Core 的 Interface 時就必須依賴 Web，但 Web 又依賴 Worker，形成循環依賴，專案無法編譯。Core 獨立出來，所有人都依賴 Core，但 Core 不依賴任何人，循環就不會發生。這不是「設計原則」，是實際的編譯問題逼出來的解法。

**我學到的原則**
架構邊界的劃分，最直接的判斷標準是「依賴方向不能有環」。Core 層存在的原因，是讓所有人都能依賴它，但它自己不依賴任何人。

**下次遇到類似情況，我會先想到什麼**
先畫依賴方向圖，確認沒有環，再開始建 Project。

---

## W3-W4

### 條目 003 — 先打 API 看資料，再設計 Entity

**我做了什麼**
在寫 WeatherObservations Entity 之前，先用 HTTP Client 直接打了一次農業部 API，把 JSON 印出來看。

**我遇到的問題**
我以為 API 文件說什麼，欄位就是什麼。結果發現 `H_FX` 這個欄位，在儀器校驗期間回傳的不是數字，而是「儀器校驗中」這個中文字串。如果 Entity 設計成 `decimal`，`TryParse` 失敗就拋例外，Worker 整個崩潰。

**我怎麼想通的**
農業部是政府機關，API 文件不一定完整描述所有邊界情況。唯一能確認真實行為的方式，就是親自打一次 API，把回傳的 JSON 當作設計的依據，而不是文件。這步驟如果省略，等到 Worker 上線後才發現欄位型別不對，Migration 要改、資料要重跑，代價遠高於現在多花十分鐘。

**我學到的原則**
外部 API 的真實行為，以實際回傳為準，文件只是參考。設計任何從外部 API 落地的 Entity 之前，必須先打一次 API 看真實資料。所有可能出現非數值的欄位，一律用 nullable + TryParse。

**下次遇到類似情況，我會先想到什麼**
先打 API、看 JSON、找異常值，再寫 Entity，順序不能換。

---

### 條目 004 — Singleton 吃掉 Scoped 的問題

**我做了什麼**
把 `WeatherDbContext` 注入進 `WeatherSyncWorker` 的建構子，結果啟動時拋出例外。

**我遇到的問題**
例外訊息是 `Cannot consume scoped service 'WeatherDbContext' from singleton 'WeatherSyncWorker'`。當下完全不知道這是什麼意思，只知道程式跑不起來。

**我怎麼想通的**
後來理解了生命週期的比喻：Singleton 是「這家公司只有一個，從開業到關門都在」，Scoped 是「每個客人進來就開一個服務視窗，客人離開就關掉」。如果讓公司（Singleton）握住一個客服視窗（Scoped），這個視窗就永遠不會關，它服務的「客人」也永遠不會走，這破壞了 Scoped 設計上預期的生命週期。ASP.NET Core 在啟動時就偵測到這個矛盾，直接拋例外，不讓你繼續。解法是注入 `IServiceScopeFactory`，讓 Worker 自己在每次任務執行時「開一個視窗、用完關掉」，而不是永遠握著一個視窗。

**我學到的原則**
BackgroundService 永遠是 Singleton。任何需要 Scoped 服務（DbContext、Repository）的地方，都不能直接注入，必須透過 `IServiceScopeFactory` 在使用時動態建立 Scope，用完釋放。這是這個專案所有 Worker 的固定模式。

**下次遇到類似情況，我會先想到什麼**
Worker 裡只注入三樣東西：Logger、HttpClientFactory、ScopeFactory。DbContext 永遠在方法內部透過 Scope 取得。

---

### 條目 005 — 防重複邏輯的第一版缺陷

**我做了什麼**
第一版的防重複邏輯是從 DB 取出最新一筆的 `ObservedAt` 時間，然後只寫入時間比它新的資料。

**我遇到的問題**
這個邏輯在單一測站的情況下正確，但農業部 API 同一批回傳幾百個測站的資料，而且它們的觀測時間是同一個整點。假設 DB 裡已有 14:00 的南投測站資料，`latestTime` 就是 14:00，然後其他測站的 14:00 資料，因為「不大於 latestTime」，全部被當作重複跳過，但它們根本是不同測站的資料，根本不算重複。

**我怎麼想通的**
問自己「什麼叫做重複」。重複不是「時間一樣」，而是「同一個測站在同一個時間點的觀測值已經存在」。所以判斷的 Key 必須是「測站 ID + 時間」的組合，而不是單獨的時間。用 `HashSet` 存所有已有的組合，每筆新資料先組出同樣的 Key 去查 HashSet，有就跳過，沒有才寫入。

**我學到的原則**
防重複策略的設計，第一步是定義清楚「什麼叫重複」，而不是直接想怎麼寫程式碼。定義清楚了，策略自然出來。如果「重複」的定義涉及多個欄位的組合，HashSet 的 Key 就必須是那幾個欄位的組合。

**下次遇到類似情況，我會先想到什麼**
先問「什麼叫重複」，把答案寫下來，再決定 HashSet Key 要用哪些欄位組合。

---

### 條目 006 — 外部 API 的隱性商業限制

**我做了什麼**
在 Worker 裡實作了分頁迴圈（農業部 API 文件說有 `Page` 參數和 `Next` 欄位），結果第二頁回傳 `RS: "ERROR"`，訊息是「非會員只限回傳第一頁資料」。

**我遇到的問題**
文件說有分頁，但打了才發現是商業功能，免費帳號不能用。而且 Log 上顯示 ERROR，看起來很嚴重，但其實不是程式問題。

**我怎麼想通的**
這是兩個獨立的問題。第一個問題：分頁迴圈要不要移除？不用，因為 `RS != "OK"` 的 `break` 會讓迴圈優雅地停下來，不影響功能。第二個問題：Log 訊息要不要修？要，因為「第一頁就失敗」和「第二頁被商業限制擋住」是完全不同的嚴重程度，前者要 `Warning`，後者只需要 `Information`。Log 的目的是讓人一眼判斷「需不需要去查問題」，如果把正常的商業限制記錄成 WARNING，以後每次看 Log 都會誤以為有問題。

**我學到的原則**
外部 API 的隱性限制是常態，不是例外。程式碼要為「API 行為和文件不符」的情況設計防禦，而不是假設文件說什麼就是什麼。Log 的等級要反映真實嚴重程度，不要讓正常情況製造雜訊。

**下次遇到類似情況，我會先想到什麼**
API 回傳非預期結果時，先區分「程式問題」還是「外部限制」，再決定 Log 等級和處理方式。

---

## W5-W6

### 條目 007 — Identity Migration 為什麼要提前跑

**我做了什麼**
原本計畫 W15-16 才設定 Identity，但在設計 `UserNotifications` 資料表時，發現 `UserId FK` 指向的 `AspNetUsers` 還不存在。

**我遇到的問題**
如果 `AspNetUsers` 在 W15 才建立，那 W5-W14 期間所有需要 `UserId FK` 的資料表（`UserNotifications`、`UserFarmProfiles`、`LostPetReports`）都必須把這個欄位設成 `nullable`。等到 W15 要改成 `NOT NULL`，已有的歷史資料需要補值，Migration 會變複雜，而且這是可以避免的問題。

**我怎麼想通的**
「Identity Migration 存在」和「Login UI 完成」是兩件獨立的事情。讓 `AspNetUsers` 表存在，只需要三行設定（繼承 `IdentityUser`、繼承 `IdentityDbContext`、跑 Migration），完全不需要任何 Controller、JWT、或登入頁。把這三行現在做掉，後面所有資料表的 `UserId` 從第一天起就可以設計成正確的 `NOT NULL + FK`，不欠技術債。

**我學到的原則**
技術債的成本會隨時間累積。能用很低代價現在解決的問題，就現在解決，不要因為「原本計畫是以後再做」而拖著。判斷的標準是：現在做的代價，和以後做的代價，哪個低。

**下次遇到類似情況，我會先想到什麼**
發現資料表之間有 FK 依賴時，先確認被依賴的表是否存在。如果不存在，評估讓它提前存在的代價，通常很低。

---

### 條目 008 — 主表 + 關聯表 vs JSON 欄位的選擇

**我做了什麼**
設計 `PestAlerts` 資料表時，需要儲存「一筆警報可能涉及多個縣市、多種作物」這個一對多關係。

**我遇到的問題**
第一個直覺是把縣市清單存成 JSON 字串（`"南投縣,嘉義縣"`）或 JSON 陣列，簡單快速。但後來想到，如果要查「所有涉及南投縣的警報」，JSON 欄位需要用 `LIKE '%南投縣%'` 或 JSON 函數查詢，效能差，而且很難建立索引。

**我怎麼想通的**
問自己「以後最常執行的查詢是什麼」。答案是「依縣市篩選警報」和「依作物篩選警報」，這兩種查詢都需要精確比對，不是模糊搜尋。精確比對適合用 JOIN，模糊搜尋才用 LIKE。所以應該建立獨立的 `PestAlertCities` 和 `PestAlertCrops` 關聯表，讓每個縣市、每種作物都是獨立一行，在上面建索引，JOIN 查詢就很快。

**我學到的原則**
資料表設計要從「查詢模式」出發，而不是從「儲存方便」出發。在設計欄位之前，先想清楚「這個資料最常被怎麼查」，查詢模式決定表結構。需要精確比對的多值欄位，用關聯表；真正的非結構化資料，才考慮 JSON 欄位。

**下次遇到類似情況，我會先想到什麼**
問「這個欄位以後最常被怎麼查」，再決定用關聯表還是 JSON。

---

### 條目 009 — Transaction 和 SaveChangesAsync 的關係

**我做了什麼**
在設計 `PestAlertSyncWorker` 的寫入邏輯時，需要同時寫入三張有順序依賴的表：`PestAlerts` → `PestAlertCities` → `PestAlertCrops`。

**我遇到的問題**
如果分三次 `SaveChangesAsync()` 寫入，中間任何一次失敗，就會有部分資料寫進去、部分沒有，產生孤立的不完整資料。但我不確定怎麼讓「三張表要嘛全部成功、要嘛全部失敗」。

**我怎麼想通的**
這就是資料庫 Transaction 的核心用途。而 EF Core 的 `SaveChangesAsync()` 本身就是一個隱含的 Transaction——同一次 `SaveChangesAsync()` 裡的所有操作，要嘛全部成功，要嘛全部 Rollback。所以解法不是手動 `BeginTransaction()`，而是把三張表的 `Add` 操作全部放在同一次 `SaveChangesAsync()` 呼叫之前，讓 EF Core 一次送出，一個 Transaction 包住全部。

**我學到的原則**
EF Core 的隱含 Transaction 已經夠用於大多數情況。只有在「跨 DbContext」或「需要手動 Rollback 特定中間狀態」時，才需要手動 `BeginTransaction()`。判斷標準：所有操作能不能放進同一個 DbContext 的同一次 `SaveChangesAsync()`？能的話，就不需要手動管理 Transaction。

**下次遇到類似情況，我會先想到什麼**
多張表的寫入如果有順序依賴，先確認能不能放進同一次 `SaveChangesAsync()`，這是最簡單的 Transaction 策略。

---

### 條目 010 — IHttpClientFactory 解決的不是功能問題，是資源問題

**我做了什麼**
在讀 `WeatherSyncWorker` 建構子時，發現注入的是 `IHttpClientFactory`，但存到欄位的是 `HttpClient`。一開始覺得奇怪，為什麼不直接注入 `HttpClient`？

**我遇到的問題**
我知道「要用 IHttpClientFactory」，但說不出為什麼。這種「知道規則但不知道原因」的狀態，在面試的時候很容易被一句追問就說不下去。

**我怎麼想通的**
想通的關鍵是理解 TCP 連線的生命週期。`new HttpClient()` 每次建立都開一條底層的 socket 連線，物件被 GC 回收後，socket 不會立刻釋放，作業系統會讓它進入 TIME_WAIT 狀態保留幾分鐘。如果程式頻繁建立新的 HttpClient，socket 累積的速度比釋放的快，最終耗盡可用連線，這就是 socket exhaustion。

想像成電話線：每次打電話都拉一條新電話線，打完剪掉，但線頭還插在牆上佔著接口，要等幾分鐘才真正釋放。打 100 次就插了 100 條線頭。`IHttpClientFactory` 的做法是讓電話局管理一批電話線，借用、歸還、重複使用，線頭由電話局統一管理。

另外還發現了一個型別層面的問題：欄位宣告的是 `HttpClient`，建構子注入的是 `IHttpClientFactory`，看起來型別不符。後來確認 `CreateClient()` 的回傳型別就是 `HttpClient`，所以工廠是製造工具的，欄位存的是製造出來的產品，型別完全一致，不衝突。

**我學到的原則**
「用 IHttpClientFactory」不只是最佳實踐，是避免資源洩漏的必要手段。型別不確定的時候，先看方法的回傳型別，IDE hover 就能看到，不需要猜。

**下次遇到類似情況，我會先想到什麼**
任何需要 HttpClient 的地方，一律注入 IHttpClientFactory，用 CreateClient() 取得實例，從不直接 new。

---

### 條目 011 — CancellationToken 不是錯誤處理，是優雅停止

**我做了什麼**
在理解 `WeatherSyncWorker` 的程式碼時，發現幾乎每個 async 方法都把 `stoppingToken` 往下傳，包括 `GetStringAsync`、`SaveChangesAsync`、`Task.Delay`。一開始覺得這樣寫很囉嗦，不傳好像也能跑。

**我遇到的問題**
不理解傳和不傳的實際差異是什麼。如果程式要停止，直接強制終止不就好了？

**我怎麼想通的**
想通的切入點是「外送取消訂單」的概念。外送員在路上，你取消訂單，外送員收到通知就掉頭，不用繼續跑。`CancellationToken` 就是這個取消通知的傳遞機制。

如果不傳 token 給 `Task.Delay(TimeSpan.FromHours(1))`，程式收到停止訊號後，這個等待還是會繼續跑完整整一小時才結束。傳了 token，停止訊號一來，等待立刻中斷，程式可以即時關閉。

這個「即時關閉」的能力就是 Graceful Shutdown 的核心——不是被強制殺死，而是把手邊的事做完（或取消），然後乾淨地離開。

還發現一個架構上的細節：`while (!stoppingToken.IsCancellationRequested)` 這個條件讓整個迴圈的生命週期受 token 控制。`OperationCanceledException` 理論上可以被 catch 到，但因為迴圈條件在 token 取消後就不成立了，所以實務上不需要特別處理這個例外，迴圈自然結束。

**我學到的原則**
async 方法的 CancellationToken 參數要養成習慣傳遞，不只是「規範」，是確保程式能在任何等待點被中斷的機制。一路往下傳，每個等待點都覆蓋到。

**下次遇到類似情況，我會先想到什麼**
每個 async 方法呼叫都確認有沒有 CancellationToken 參數，有就傳進去，不要省略。

---

### 條目 012 — 結構化日誌和字串插值是兩件不同的事

**我做了什麼**
讀 `WeatherSyncWorker` 的 log 呼叫時，發現 `{Count}` 裡面的名字和後面逗號傳入的變數是分開的，看起來像 C# 字串插值，但又沒有 `$` 符號。一開始不確定這兩者的差別。

**我遇到的問題**
以為這只是另一種寫字串的方式，後來發現其實它們底層的運作機制完全不同。

**我怎麼想通的**
C# 字串插值 `$"{Count}"` 是在編譯期就把值嵌入字串，結果就是一個純字串。結構化日誌的 `{Count}` 是佔位符，值在 logger 內部處理時才填入，而且這個名字會被當作獨立的屬性儲存起來。

也就是說，log 工具（例如 Seq、Application Insights）收到的不只是一行文字，而是一個帶有屬性的結構：

```json
{
  "Message": "成功寫入 1000 筆",
  "Properties": { "Count": 1000 }
}
```

有了這個結構，就可以下條件查詢：`Count > 500`、`Count == 0`，把特定情況的 log 篩出來分析。如果用字串插值，log 只是一行文字，裡面的數字無法被程式化地查詢。

`{Count}` 裡面的名字可以改，值是由後面逗號之後的參數按順序決定的，不是名字本身決定的。但名字要取有意義的，因為它會成為查詢時的欄位名稱。

**我學到的原則**
log 語句一律用具名佔位符，不用 `$""` 字串插值。不只是寫法習慣，是讓 log 具備可查詢性的前提。

**下次遇到類似情況，我會先想到什麼**
寫 log 時，把「這個值以後會不會需要拿來過濾或統計」當作判斷是否給它有意義名字的標準。

---

### 條目 013 — MapToEntity 的職責：從外部形狀到內部形狀

**我做了什麼**
在讀 `WeatherSyncWorker` 的 `MapToEntity` 方法時，發現它做的事情就是把 DTO 的每個欄位轉換後填進 Entity。一開始覺得這層轉換有點多餘，直接用 DTO 不行嗎？

**我遇到的問題**
不清楚 DTO 和 Entity 為什麼要分開，以及為什麼需要一個專門的 Mapping 方法。

**我怎麼想通的**
DTO（Data Transfer Object）的形狀由外部 API 決定，欄位名稱、型別都以 API 的格式為準，例如 `H_FX` 是字串，因為 API 可能回傳「儀器校驗中」。Entity 的形狀由資料庫需求決定，例如 `MaxGust` 是 `decimal?`，因為這才是適合儲存和查詢的型別。

這兩個形狀不一樣，所以需要一個轉換層。`MapToEntity` 做的事情就是把「外部格式」轉成「內部格式」，中間處理掉所有骯髒的邊界情況：字串轉數字、空值處理、時間格式解析。

這一層如果省掉，邊界情況的處理就會散落在各個地方，難以維護。集中在 `MapToEntity` 裡，改一個地方就能覆蓋所有資料。

**我學到的原則**
DTO 和 Entity 永遠保持分離，不混用。轉換邏輯集中在一個地方，不要讓邊界情況的處理散落各處。

**下次遇到類似情況，我會先想到什麼**
看到 DTO 欄位和 Entity 欄位型別不一致時，不要想辦法讓它們一樣，而是在 Mapping 層處理轉換。

---

### 條目 014 — TryParse、TryParseExact 和 InvariantCulture 的各自用途

**我做了什麼**
在讀 `MapToEntity` 和 `ParseDecimal` 的實作時，發現同樣是解析字串，時間用 `TryParseExact`，數字用 `TryParse`，而且數字解析還多帶了 `InvariantCulture` 參數。一開始以為這些都是同一件事的不同寫法。

**我遇到的問題**
搞不清楚三者的差異，以及為什麼要特別傳 `InvariantCulture`，感覺有沒有都一樣。

**我怎麼想通的**
**TryParse vs TryParseExact：**
`TryParse` 比較寬鬆，會嘗試猜格式。`TryParseExact` 嚴格，只接受你指定的格式，不符合就失敗。時間解析用 `TryParseExact` 是因為農業部的格式固定是 `"yyyy/MM/dd HH:mm"`，嚴格比對反而更安全，不會因為偶發的奇怪格式被錯誤解析成另一個時間。

**InvariantCulture 的問題：**
不同語系的伺服器對數字格式的理解不同。德文系統把逗號當小數點（`9,3`），英文系統把點當小數點（`9.3`）。如果不指定 `InvariantCulture`，解析行為就取決於伺服器的語系設定。同一行程式碼在英文伺服器上成功，在德文伺服器上失敗。

`InvariantCulture` 的作用是「不管伺服器是什麼語系，永遠用固定標準（點當小數點）解析」。農業部 API 固定回傳英文格式的數字，所以用 `InvariantCulture` 確保在任何部署環境都能正確解析。

這裡的核心區分是：`InvariantCulture` 解決的是「伺服器環境」帶來的差異，不是「資料格式」的差異。資料格式是農業部決定的，不會變；伺服器環境是部署時才知道的，可能變。

**我學到的原則**
數字解析一律帶 `InvariantCulture`，這是防禦性編程的一部分。不是因為現在有問題，是確保未來部署到任何環境都行為一致。

**下次遇到類似情況，我會先想到什麼**
解析數字時先想「這個結果會不會因為伺服器語系不同而不同」，會的話就加 `InvariantCulture`。

---

### 條目 015 — IEnumerable 的延遲執行和 ToList() 的時機

**我做了什麼**
在讀 `SyncWeatherAsync` 的步驟 3 時，發現 `Select().Where().Cast()` 最後都加了 `.ToList()`。一開始覺得這只是把結果轉成 List，沒什麼特別。

**我遇到的問題**
後來發現如果不加 `ToList()`，程式還是能跑。不清楚加和不加的實際差異是什麼。

**我怎麼想通的**
`Select`、`Where`、`Cast` 這些 LINQ 方法回傳的是 `IEnumerable<T>`，它是「延遲執行」的——你只是定義了「要怎麼處理資料」，但還沒有真正執行。每次有人去迭代這個 `IEnumerable`，它才真正跑一次。

問題在於，如果同一個 `IEnumerable` 被用到兩次，它就會執行兩次。`incoming` 在程式裡被用了兩次（判斷 `Count == 0`，以及去重邏輯的比對），如果不加 `ToList()`，`MapToEntity` 就會被呼叫兩次。加了 `ToList()`，立刻執行一次，結果存在記憶體，後續無論用幾次都是讀記憶體，不重複執行。

判斷的標準因此很簡單：**這個結果會被用到幾次？用超過一次就加 `ToList()`。**

**我學到的原則**
IEnumerable 是延遲執行的，不是資料容器。用超過一次就具體化成 List，避免重複計算。

**下次遇到類似情況，我會先想到什麼**
看到 LINQ 鏈式呼叫，先想「這個結果後面會用幾次」，超過一次就加 ToList()。

---

### 條目 016 — Task vs Task\<T\> 和 abstract vs virtual 的對稱性

**我做了什麼**
在理解 `ExecuteAsync` 的簽名時，想搞清楚 `Task` 和 `Task<T>` 的差別，以及 `override` 到底覆寫了什麼。

**我遇到的問題**
知道 `async Task` 和 `async Task<int>` 的用法，但說不清楚背後的對稱關係。也不清楚 `BackgroundService` 是抽象類別，這代表什麼。

**我怎麼想通的**
**Task vs Task\<T\>：**
這個對稱性其實很直觀：

```
同步方法         非同步方法
void          → Task
int           → Task<int>
string        → Task<string>
```

`void` 的非同步版本是 `Task`，不是 `Task<void>`，因為 `Task<void>` 不合法。所以看到 `async Task` 就知道這個方法非同步執行，但不回傳值。看到 `async Task<int>` 就知道最終會回傳一個 int。`return` 在 `Task` 方法裡就是「提早結束，沒有值給你」，在 `Task<int>` 裡如果不帶值，編譯器直接報錯。

**abstract vs virtual：**
抽象類別裡的方法分兩種：`abstract` 是「我只定義簽名，你必須自己實作」，`virtual` 是「我有預設實作，你可以選擇覆寫也可以不管」。`BackgroundService` 的 `ExecuteAsync` 是 `abstract`，所以繼承它的 `WeatherSyncWorker` 必須用 `override` 覆寫，不寫編譯器報錯。其他如 `StopAsync` 是 `virtual`，有預設行為，不覆寫也能跑。

**我學到的原則**
不確定某個方法的行為時，直接在 IDE 裡按 F12 看父類別的定義，是 abstract 還是 virtual 一目了然，比查文件快。

**下次遇到類似情況，我會先想到什麼**
看到 override，先按 F12 找到父類別的對應定義，確認是 abstract（強制）還是 virtual（選擇性）。

---

## 給未來的條目預留位置

### 條目 017 — SourceHash 的設計是業務判斷，不是技術判斷

**我做了什麼**
設計 PestAlertSyncWorker 的去重邏輯時，需要決定「用什麼欄位組合來判斷一筆病蟲害警報是否已存在」。

**我遇到的問題**
農業部的 PlantEpidemicType API 每次全量回傳所有歷史公告，不像氣象資料有明確的「這次更新了什麼」。更複雜的是，同一則事件（例如某縣市的斜紋夜蛾密度上升）可能同時被兩個單位發布：桃園市政府發一篇，農業部防檢署再發一篇，Subject 幾乎相同但措辭略有不同，Issue 欄位不同。

一開始想用 `PubDate + Issue + PlantName` 組合，但農業部實際資料裡出現了這種情況：同一天、同一個改良場，分別對「紅豆」和「大豆,黃豆」發布兩篇警示。這個組合能區分這兩筆，看起來沒問題。但問題在於另一個方向：轉發和原文的 Issue 不同，會被當成兩筆獨立資料存進去，但對農民來說它們是同一則資訊。

**我怎麼想通的**
關鍵是先問「什麼叫重複」，而不是先想怎麼寫程式碼。這個問題的答案取決於系統的目的：這個系統是給農民看警示用的儀表板，不是政府公文存檔系統。

換個角度想：同一天、同一個標題的兩篇公告，對農民的實際意義是一樣的——「有這個警示，要注意」。存兩筆對農民沒有額外價值，只是讓清單變長、規則引擎之後要重複處理。

所以最終選擇 `PubDate + Subject` 的 SHA256 hash 作為 SourceHash，邏輯是「轉發視為重複，先到先得」。這是一個業務判斷，不是技術判斷。技術上要實現哪個方案都不難，難的是想清楚「系統到底應該把什麼當重複」。

**我學到的原則**
去重策略的設計決策來自業務需求，而不是資料結構。同樣是「防重複」，檔案備份系統和農民儀表板的「重複」定義完全不同。先把業務邏輯說清楚，程式碼自然跟著出來。

**下次遇到類似情況，我會先想到什麼**
遇到去重設計，先用白話寫下「什麼叫重複，對誰而言」，再從這個定義推導出 Hash Key 應該包含哪些欄位。

---

### 條目 018 — DTO 不能共用：外部形狀必須獨立

**我做了什麼**
建立 PestAlertSyncWorker 的過程中，需要建立對應 PlantEpidemicType API 的 DTO。一開始想直接沿用 WeatherApiResponse 的外層結構，只換掉裡面的 Data 型別。

**我遇到的問題**
看起來兩個 API 的外層都是 `{ RS, Data, Next }`，直覺覺得應該可以共用。但仔細一看，泛型無法這樣共用：WeatherApiResponse 的 Data 是 `List<WeatherStationDto>`，如果要給 PestAlert 用，就必須把它改成某種泛型，或者 Data 的型別對不上，編譯器報錯。

**我怎麼想通的**
這個問題讓我更清楚地看到 DTO 的職責：它的形狀由「外部 API 說什麼」決定，不是由「程式裡方便共用」決定。外層結構雖然長得一樣，但 Data 的內容完全不同——一個是氣象測站資料，一個是病蟲害公告。強行共用反而讓型別語意模糊。

正確做法是各自建立獨立的 DTO 檔案：PestAlertApiResponse + PestAlertDto。看起來有些重複，但每個 DTO 各自對應一個 API，修改其中一個不影響另一個，職責清晰。

**我學到的原則**
「看起來結構一樣」不代表「應該共用」。共用的代價是耦合：改動 WeatherApiResponse 可能影響 PestAlert 的行為。分開的代價只是多幾行程式碼。在外部資料型別上，獨立通常比共用安全。

**下次遇到類似情況，我會先想到什麼**
每個外部 API 端點對應自己的 DTO，不跨 API 共用。結構相似是巧合，職責相同才值得共用。

---

### 條目 019 — Navigation Property 讓 EF Core 管 FK，不需要手動設定

**我做了什麼**
在 MapToEntity 裡，對一筆 PestAlertDto 同時建立 PestAlert 主表 Entity，以及對應的 PestAlertCities 和 PestAlertCrops 清單，全部放在同一個物件裡透過 navigation property 關聯。

**我遇到的問題**
我以為需要先存 PestAlert 拿到 Id，再手動把 Id 填進每個 PestAlertCity.AlertId 和 PestAlertCrop.AlertId，然後再分別 SaveChanges。這個流程看起來合理，但程式碼會變成：第一次 SaveChanges 拿 Id、填 Id、第二次 SaveChanges。如果中間出錯，PestAlert 存進去了但關聯表沒有，就出現孤立資料。

**我怎麼想通的**
EF Core 的 navigation property 就是為了解決這個問題而存在的。當我在 `new PestAlert { Cities = [...], Crops = [...] }` 這樣建立物件時，EF Core 的 Change Tracker 知道這些 City 和 Crop 是屬於這個 PestAlert 的。呼叫 SaveChangesAsync 時，EF Core 自動安排執行順序：先 INSERT PestAlert 拿到自動產生的 Id，再把這個 Id 填進所有關聯的 PestAlertCity.AlertId 和 PestAlertCrop.AlertId，最後 INSERT 關聯表。全部在一個 Transaction 裡完成。

我不需要手動管 FK 的值，也不需要分兩次 SaveChanges。這正是 ORM 相比直接寫 SQL 的核心優勢之一。

**我學到的原則**
有 navigation property 的地方，讓 EF Core 管 FK，不要手動設定 AlertId。手動管 FK 是 ADO.NET 時代的寫法，在 EF Core 裡是不必要的麻煩，也是 bug 的來源。

**下次遇到類似情況，我會先想到什麼**
多張有 FK 依賴的表需要一起寫入時，先確認有沒有建立 navigation property，有的話就讓 EF Core 自動處理，不需要手動 INSERT 再拿 Id 再填進去。

---

### 條目 020 — SHA256 為什麼要先轉 byte[]，又要轉 hex 字串

**我做了什麼**
實作 ComputeHash 方法，把 PubDate + Subject 的組合字串算出一個固定長度的 hash，存入 SourceHash 欄位。程式碼是三行：字串轉 byte[]，SHA256 計算，byte[] 轉 hex 字串。

**我遇到的問題**
自動補全幫我填完了三行，但我說不清楚為什麼是這三步，為什麼不能直接把字串丟給 SHA256，以及最後轉成 hex 字串的目的是什麼。

**我怎麼想通的**
從底層往上想，就能串起來。

電腦底層處理的是位元組（byte）。SHA256 的輸入規格是 `byte[]`，不接受字串，因為「字串」在記憶體裡的表示方式取決於編碼（UTF-8、UTF-16 等），不同編碼同一個字的 byte 值不同。`Encoding.UTF8.GetBytes()` 的作用就是把「人類看的文字」按照固定編碼規則轉成「機器操作的位元組」，確保同樣的字串在任何環境都產生同樣的 byte 序列。

SHA256 計算完之後，結果是 32 個 byte（256 bits）。這 32 個 byte 沒辦法直接存進 nvarchar 欄位，因為它們包含各種控制字元和不可見字元，在資料庫和字串比對上都會出問題。

`Convert.ToHexString()` 把每個 byte 轉成兩個十六進位字元（因為 1 個 byte = 8 bits，1 個 hex 字元 = 4 bits，所以 1 byte = 2 hex 字元），32 個 byte 就變成 64 個字元的純英數字串。這個字串可以安全地存進 nvarchar(64)，也可以直接用 `=` 比對，不會有編碼問題。

**我學到的原則**
任何處理字串的密碼學函數，都需要先做「字串 → bytes → 計算 → hex 或 Base64」這個流程，編碼格式要固定（UTF-8），最終結果要轉成人類可讀的字串格式才能儲存。

**下次遇到類似情況，我會先想到什麼**
需要 hash 字串的地方，三步固定走：Encoding.UTF8.GetBytes() → SHA256.HashData() → Convert.ToHexString()，順序不能換，編碼要一致。

---

### 條目 021 — 日期格式 bug：文件說格式是什麼不重要，API 回傳什麼才重要

**我做了什麼**
MapToEntity 裡用 DateOnly.TryParseExact 解析 PubDate 欄位，格式字串我寫的是 "yyyy/MM/dd"。Worker 跑起來之後，Log 顯示「無新資料需要同步」，第一直覺以為正常，但仔細看才發現資料根本沒寫進去。

**我遇到的問題**
如果 TryParseExact 格式對不上，它不會拋出例外，只是靜靜地回傳 false，然後 MapToEntity 回傳 null，最後整個 incoming 清單是空的，去重邏輯查不到任何 hash，Log 就顯示「無新資料」——看起來像正常運作，但其實一筆都沒有存進資料庫。

**我怎麼想通的**
回去看 API 實際回傳的 JSON，PubDate 欄位長這樣：`"2025-12-26"`。用 dash 連接，不是斜線。我在設計 Entity 和 DTO 時參考的是氣象 Worker 的日期格式（"yyyy/MM/dd HH:mm"），但兩個 API 根本是不同的服務，日期格式當然可以不一樣。

這個 bug 再次印證了條目 003 的原則：外部 API 的真實行為以實際回傳為準，不是以文件或「另一個 API 怎麼做」為準。問題在於我在設計 PestAlert 的 Mapping 時，沒有再回去核對一次 PestAlert API 的實際 JSON 格式，只是照著記憶填了格式字串。

修正很簡單，把 "yyyy/MM/dd" 改成 "yyyy-MM-dd" 就解決了。但花了一段時間才意識到「無新資料」不是正常結果，而是 silent failure 的信號。

**我學到的原則**
TryParse 系列方法失敗時不拋例外，是「防禦性」的，但也是「沉默」的。當 Log 顯示「沒有資料需要處理」但系統剛上線，要第一個懷疑的不是「真的沒有新資料」，而是「Mapping 有沒有 silent failure」。每次實作新的 Mapping，完成後先確認 Log 裡實際進來的筆數是不是合理的數字。

**下次遇到類似情況，我會先想到什麼**
系統剛跑起來卻顯示零筆，先看 MapToEntity 的 TryParse 格式字串，對照 API 實際回傳的 JSON 欄位，不要依賴記憶。

---

### 條目 022 — 資料來源的性質決定同步策略，不是技術複雜度

**我做了什麼**
實作 PestAlertSyncWorker 的寫入邏輯時，需要決定「Hash 已存在的資料要怎麼處理」。

**我遇到的問題**
最初的設計是 Insert+Update：Hash 存在時，把 API 回來的 Body 和 Prescription 與資料庫現有的比對，如果內容不同就更新。這個設計感覺更「完整」，能跟上公告內容的修改。

**我怎麼想通的**
停下來想了一件事：農業改良場和防檢署的病蟲害公告，到底會不會被修改？這是政府機關發出去的官方公文。政府單位有新資訊的時候，做法是發一篇新公告，不是修改舊公告——這是公文的性質。Insert+Update 的假設前提「來源資料會被修改」，在這個資料來源上根本不成立。

認清這一點之後，Insert+Update 的設計就變成了「為了一個不會發生的情況，增加程式碼複雜度和 DB 查詢次數」。簡化成 Insert-Only：Hash 存在直接跳過，邏輯更清楚，效能也更好。

**我學到的原則**
同步策略的複雜度應該對應資料來源的實際行為，而不是「感覺更完整」。在決定技術方案之前，先問「這個資料來源的內容會不會被修改」——這是業務問題，不是技術問題，答案決定了要不要做 update 邏輯。

**下次遇到類似情況，我會先想到什麼**
設計 sync 策略時，先問「這個資料來源是 append-only 還是 mutable」。公文型、事件型資料通常是 append-only，insert-only 就夠了；價格、庫存、狀態類資料是 mutable，才需要 upsert。

---

### 條目 023 — 資料的生命週期決定表結構，不是「看起來像什麼」

**我做了什麼**
設計 `RainfallStation` 和 `RainfallObservation` 兩張表時，發現 `RainfallStation` 需要 `IsActive`、`CreatedAt`、`UpdatedAt`，但 `RainfallObservation` 不需要這三個欄位。一開始覺得奇怪，兩張表都是雨量相關的，為什麼設計這麼不一樣？

**我遇到的問題**
更困惑的是：`WeatherObservation` 看起來和 `RainfallObservation` 性質相同（都是觀測值），但 WeatherSyncWorker 的討論完全沒有提到 IsActive 或軟刪除。我問「WeatherObservation 不需要這三個欄位嗎」，但說不出理由。

**我怎麼想通的**
轉折點是這句話：「觀測紀錄只增不改，站台主檔會被維護。」我把這句話拆開想了一下：

`WeatherObservation` 和 `RainfallObservation` 都是「某個時間點的測量值」，就像溫度計讀數，讀完就是歷史，不會有人回來說「昨天上午十點的溫度要改一下」。這種資料只會新增，不會修改，不會下架，所以不需要 `IsActive` 或 `UpdatedAt`。

`RainfallStation` 是「測站清單」，農業部可能新增站台、修改站名、或把舊站下線。這是一份持續被維護的清單，站台「下架」這件事確實會發生，需要軟刪除欄位來記錄。

理解了這個差異之後，後來所有 Entity 設計時，我的第一個問題都變成：「這筆資料是一個時間點的快照，還是一個持續被維護的主檔？」

**我學到的原則**
資料有兩種根本性不同的生命週期：快照型（觀測值、事件、公告——只增不改）和主檔型（站台、使用者、商品——可被維護）。快照型不需要 `IsActive`/`UpdatedAt`，主檔型必須有。在設計 Entity 之前，先判斷是哪一種。

**下次遇到類似情況，我會先想到什麼**
問一句：「這筆資料寫進去之後，有沒有人會回來修改或下架它？」有，主檔型；沒有，快照型。設計跟著走。

---

### 條目 024 — int PK 和 string PK 的選擇不是口味問題，是效能問題

**我做了什麼**
設計 `RainfallStation` 時，`Station_ID` 是農業部給的字串（例如 `"C0W110"`、`"467990"`），面臨選擇：要把這個字串直接當 PK，還是另開一個 `int Id` 當 PK？

**我遇到的問題**
我的第一直覺是直接用字串當 PK，理由是「字串 ID 本身就能唯一識別一個站台，多開一個 int Id 感覺多餘」。後來選擇了 int PK，但我說不清楚 FK 效能和 JOIN 頻率這兩個理由，覺得「不太清楚，需要說明」。

**我怎麼想通的**
從最基礎的問題想起：資料庫在做 JOIN 或查 FK 時，比對的速度跟欄位大小直接相關。`int` 是 4 bytes，比對只需要一個整數比較；`"C0W110"` 是 6 個字元，每個字元都要比，長度越長越慢。

更關鍵的是 JOIN 頻率：規則引擎查詢「某縣市最近一小時的累積雨量」，一定會做 `RainfallObservations JOIN RainfallStation`。這個 JOIN 在系統上線後會非常頻繁。如果 FK 是字串，索引也是字串索引，比 int 索引大好幾倍，每次 JOIN 掃描的範圍更大。

而且我一開始想的邏輯「空間換效能」——用冗餘避免 JOIN——在這個情況下條件根本不成立。因為 `RainfallObservation` 只需要存 `StationId`（int），不需要把站名、縣市全部複製進去，JOIN 一個 int FK 的成本幾乎是零。空間換效能的前提是「你要避掉的 JOIN 很貴」，這裡的 JOIN 一點都不貴。

**我學到的原則**
「用 string 當 PK 還是另開 int」不是 personal preference，是效能考量。凡是預期會有頻繁 JOIN 的表，FK 欄位用 int，這是共識。「空間換效能」只在要避掉的操作確實昂貴的情況下才成立——先評估那個操作的代價，再決定要不要用冗餘換掉它。

**下次遇到類似情況，我會先想到什麼**
問「這張表會不會被頻繁 JOIN」，會的話 FK 用 int，不管外部識別碼是什麼格式。

---

### 條目 025 — ELEV 應該住在哪裡：先問「這個屬性屬於誰」

**我做了什麼**
`RainfallObservation` 的觀測 API 每一筆都有 `ELEV`（海拔），但站台清單 API 沒有這個欄位。我要決定 ELEV 存在哪裡。

**我遇到的問題**
直覺選了選項 A：ELEV 從觀測資料拿，直接存在 `RainfallObservation` 裡。理由是「空間換效能」——避免 JOIN 取得海拔。

**我怎麼想通的**
被指出「空間換效能的條件不成立」之後，我重新問了一個更根本的問題：ELEV 到底是誰的屬性？

一個雨量站的海拔，是這個站台本身的固定特徵，不是「某個時間點的觀測值」。基隆站的海拔今天是 26.7 公尺，明天也是 26.7 公尺。如果把這個值重複存在幾萬筆觀測資料裡，每次更新海拔校正值就需要更新幾萬筆，這才是真正的浪費。

ELEV 屬於 `RainfallStation`（站台主檔），只是因為站台清單 API 沒有提供這個值，必須從觀測 API 順帶取得。正確做法是：`RainfallSyncWorker` 同步觀測資料時，順帶把 LAT/LON/ELEV Upsert 回 `RainfallStation`。第一次跑完後，這三個欄位就有值了，之後幾乎不會再變。

**我學到的原則**
一個欄位應該存在哪張表，取決於「這個屬性屬於哪個概念」，而不是「從哪裡拿最方便」。如果一個值是某個實體的固定屬性，就存在那個實體的表，哪怕初始值的來源比較迂迴。

**下次遇到類似情況，我會先想到什麼**
問「這個值描述的是哪個概念的屬性」，按概念歸位，不按資料來源歸位。

---

### 條目 026 — ContainsKey 查兩次，TryGetValue 查一次

**我做了什麼**
在 `RainfallStationSyncWorker` 的 Upsert 迴圈裡，需要判斷一個站台在 DB 裡是否已存在，如果存在就取出來更新。我的第一版用 `ContainsKey` 判斷，再用 `[key]` 取值。

**我遇到的問題**
知道「要用 TryGetValue」，但說不出為什麼。兩種寫法功能一樣，差在哪裡？

**我怎麼想通的**
把兩種寫法並排看：

```csharp
// ContainsKey：查兩次
if (dict.ContainsKey(key))        // 第一次查：有沒有這個 key
{
    var value = dict[key];        // 第二次查：給我這個 key 的值
}

// TryGetValue：查一次
if (dict.TryGetValue(key, out var value))  // 一次完成：有沒有 + 給值
{
    // value 已經在這裡了
}
```

差別很清楚：`ContainsKey` 只回答「有沒有」，不給你值，所以你得再查一次才能拿到值，等於進字典找了兩次。`TryGetValue` 一次做兩件事——確認存在並同時把值交給你，找一次就夠。在幾百個站台的迴圈裡，差距雖然不大，但這是字典查詢在 C# 裡的慣用寫法，讀程式碼的人看到 `TryGetValue` 會立刻理解意圖。

**我學到的原則**
Dictionary 的模式：「查是否存在並同時取值」一律用 `TryGetValue`，`ContainsKey + [key]` 是兩步能合成一步的反模式。語意也更清楚——`TryGetValue` 的名字本身就說了「嘗試取值，成功就給你」。

**下次遇到類似情況，我會先想到什麼**
Dictionary 查找需要值的地方，第一個想到 `TryGetValue`，不是 `ContainsKey`。

---

### 條目 027 — 軟刪除的正確觸發條件：不是「API 說它壞了」，而是「API 忘記提它了」

**我做了什麼**
`RainfallStationSyncWorker` 需要偵測被下架的雨量站並標記為 `IsActive = false`（軟刪除）。我的第一個想法是：看 `ATTRIBUTE` 欄位，如果裡面有「停用」相關的值就軟刪除。

**我遇到的問題**
看了幾百筆 API 回傳資料，`ATTRIBUTE` 全部都是空字串 `""`。農業部文件完全沒說這個欄位有什麼可能值，「停用」的站台在 API 裡長什麼樣子完全不知道。

**我怎麼想通的**
轉折點是換了一個問法：「農業部要讓一個站台停用，他們會怎麼做？」最自然的做法是：在下一次回傳的站台清單裡，直接不包含這個站台。農業部不需要在回傳資料裡「聲明這個站停用了」，停用的站台就是消失在清單裡。

這讓正確的軟刪除邏輯變得很清楚：

```
API 這次回傳的站台 ID → 存成 HashSet
DB 裡 IsActive = true 的站台 → 一一比對
「在 DB 有但在 HashSet 沒有」的站台 → IsActive = false
```

不需要看任何欄位的值，只需要比對「有沒有出現在這次的清單裡」。

**我學到的原則**
外部系統表達「某個項目不再有效」最常見的方式，不是在回傳資料裡加一個「已停用」標記，而是直接在清單裡消失。軟刪除的觸發條件應該是「資料從來源消失」，而不是「來源明確說了它壞了」。

**下次遇到類似情況，我會先想到什麼**
需要偵測下架或停用時，先問「這個來源用『清單』的形式回傳資料嗎」。是的話，「上次有、這次沒有」就是軟刪除的觸發條件。

---

### 條目 028 — Polling 不是 hack，是依賴關係的顯式表達

**我做了什麼**
`RainfallSyncWorker` 依賴 `RainfallStation` 裡有站台資料（因為要做 StationId 對應）。但 `RainfallStationSyncWorker` 是 7 天跑一次的，兩個 Worker 同時啟動時，站台資料不一定已經進 DB。

**我遇到的問題**
一開始覺得這很奇怪。兩個 BackgroundService 都在 `Program.cs` 的 `AddHostedService` 裡，理論上應該都跑起來了，為什麼還要等？

**我怎麼想通的**
「Worker 已啟動」和「Worker 的資料已就緒」是兩件不同的事。`AddHostedService` 只保證 `ExecuteAsync` 被呼叫，但 `RainfallStationSyncWorker` 的第一次 API 呼叫需要幾秒甚至幾十秒。`RainfallSyncWorker` 如果不等，開始同步觀測資料時，`RainfallStations` 是空表，ForEach 更新座標時找不到任何站台，座標永遠不會被填入。

Polling 的寫法是：在主同步迴圈開始前，每 30 秒查一次 `RainfallStations.Count()`，大於 0 才往下走。這個 check 只在啟動時跑幾次，一旦通過就不再執行，成本很低。這不是臨時的 workaround，而是「我需要 X 的前置資料才能開始工作」這個依賴關係的顯式表達。

**我學到的原則**
多個 Worker 之間有資料依賴時，不能假設依賴方「剛好已經跑完了」。Polling 是一種明確的「等待前置條件就緒」機制，對應的依賴越清晰，Polling 的邏輯越容易寫對。

**下次遇到類似情況，我會先想到什麼**
Worker 啟動時問：「我需要的前置資料，在我啟動的同時一定存在嗎？」不確定的話，先 Polling。

---

### 條目 029 — AddRangeAsync 呼叫兩次 + SaveChangesAsync 位置錯誤

**我做了什麼**
`RainfallSyncWorker` 的寫入邏輯在「有新觀測資料」時才呼叫 `AddRangeAsync`，「沒有新資料」時 `return`，讓 `SaveChangesAsync` 不被執行。看起來很合理——沒東西可寫，幹嘛呼叫 SaveChanges？

**我遇到的問題**
這個邏輯裡藏了兩個 bug，而且寫的時候完全沒有察覺。

**我怎麼想通的**
第一個 bug：`AddRangeAsync` 被呼叫了兩次。一次在 `if (count > 0)` 裡，一次在後面。EF Core 的 Change Tracker 會把同一批 Entity 加兩遍，`SaveChangesAsync` 時會試圖 INSERT 兩次，第二次撞到 Unique Index 就拋例外。

第二個 bug 更隱蔽：在「沒有新觀測資料」的情況下 `return`，站台座標更新（`station.Latitude = ...`）雖然在 Change Tracker 裡被記錄了，但 `SaveChangesAsync` 沒執行，修改永遠不會寫回 DB。每次 10 分鐘跑一次，`RainfallStation` 的 LAT/LON/ELEV 永遠停在初始的 `null`。

正確的設計是：不管有沒有新觀測資料，`SaveChangesAsync` 都要執行，因為站台座標的更新是「附帶作業」，不依附在「有新觀測資料」這個條件上。

```csharp
if (newObservations.Count > 0)
    await db.RainfallObservations.AddRangeAsync(newObservations, stoppingToken);
// 不管上面有沒有執行，座標更新都要存
await db.SaveChangesAsync(stoppingToken);
```

**我學到的原則**
`SaveChangesAsync` 的呼叫時機不應該跟某一個特定操作綁在一起——它是「把這個 Scope 裡所有的 Change Tracker 變更一次寫回 DB」，影響的是整個 Scope，不只是某一個 `AddRange`。如果同一個 Scope 裡有多種類型的變更（觀測資料寫入 + 站台座標更新），`SaveChangesAsync` 只呼叫一次，放在所有操作的最後。

**下次遇到類似情況，我會先想到什麼**
寫完 SyncAsync 的最後，問「這個 Scope 裡還有什麼 Change Tracker 操作可能因為 `return` 而被漏掉」，確保 `SaveChangesAsync` 永遠在所有操作之後執行。

---

### 條目 030 — 7 成相似不等於該抽共用，先問「抽出來之後更難讀還是更好讀」

**我做了什麼**
`RainfallSyncWorker` 完成後，把它和 `WeatherSyncWorker` 並排比較，發現有 7 成的結構相同：分頁抓取迴圈、MapToEntity 過濾、targetTimes 去重、AddRangeAsync + SaveChangesAsync 這些步驟幾乎一模一樣。我問：「這樣是否就是要重構，抽服務？」

**我遇到的問題**
「重複就要抽」這個直覺根深蒂固。看到兩個 Worker 這麼像，感覺不抽是在欠技術債。

**我怎麼想通的**
先問了一個反問：「抽出來之後，程式碼會更簡單還是更複雜？」

如果要把分頁邏輯、去重邏輯抽成共用 helper，需要用泛型或委派把不同的地方參數化：不同的 DTO 型別、不同的 Entity 型別、不同的 DB 查詢、不同的 MapToEntity 方法、`RainfallSyncWorker` 還有獨有的站台座標更新……把這些差異全部參數化之後，共用方法的簽名會長得非常複雜。呼叫端反而比現在更難讀。

這違背了 Rule of Three 的精神。Rule of Three 說的是「重複三次才考慮抽」，而且有一個隱含的前提：**抽出來的東西必須比原來更簡單**。這裡兩個 Worker 雖然流程相似，但業務細節（欄位、索引、座標更新、清除舊資料）都不一樣，抽象化的成本高於重複的成本。

**我學到的原則**
重複是需要抽象化的信號，不是命令。判斷要不要抽的標準是：「抽出來之後，程式碼對讀者來說更清楚了嗎？」如果抽象化需要大量泛型、委派、或 callback 來容納不同之處，那個抽象可能讓程式更難讀，不是更好。

**下次遇到類似情況，我會先想到什麼**
重複代碼出現時，先想像抽出來的那個方法的簽名和呼叫方式。如果看起來比現在更複雜，就先不抽。

---

### 條目 031 — 同樣叫「正規化」，動機完全不同：1NF 違反 vs 3NF 違反

**我做了什麼**
設計 `RainfallStation` 和 `RainfallObservation` 兩張表時，發現這個拆表決定和之前 PestAlerts 拆成三張表（`PestAlerts`、`PestAlertCities`、`PestAlertCrops`）表面上很像——都是「把一個 API 的資料拆成多張表存」。但解釋起來感覺哪裡不一樣，說不清楚差在哪裡。

**我遇到的問題**
兩次拆表的理由都叫「正規化」，這讓我一開始以為它們是同一種問題。但 PestAlerts 拆表是因為城市和作物是「多個值」，Rainfall 拆表是因為「重複存了站台資訊」，這兩個感覺不太一樣，卻說不清楚差異在哪裡。

**我怎麼想通的**
把兩個問題分別問清楚之後，差異就出來了。

**PestAlerts 為什麼要拆表？**
農業部 API 回傳的 `City` 欄位長這樣：`"臺北市,新北市,桃園市"`，`PlantName` 欄位：`"水稻,玉米"`。一個欄位裡塞了多個值，用逗號分隔。這違反的是 **1NF**——「每個欄位只能存一個值」。如果直接存字串，以後要查「哪些警報有影響臺北市」，就必須用 `LIKE '%臺北市%'`，這是全表掃描，效能差，而且「臺北市」和「大臺北市」這類近義詞問題根本處理不了。拆表是為了讓每個城市、每個作物都有自己的一列，讓查詢可以用 `WHERE CityName = '臺北市'`。

**Rainfall 為什麼要拆表？**
觀測 API 每一筆資料都長這樣：

```
StationId: "C0W110", StationName: "淡水", City: "新北市", Lon: 121.45, Lat: 25.16, ObservedAt: ..., Rain: 0.5
StationId: "C0W110", StationName: "淡水", City: "新北市", Lon: 121.45, Lat: 25.16, ObservedAt: ..., Rain: 1.2
StationId: "C0W110", StationName: "淡水", City: "新北市", Lon: 121.45, Lat: 25.16, ObservedAt: ..., Rain: 0.0
```

`StationName`、`City`、`Lon`、`Lat` 在每一筆裡都完全一樣。這些欄位的值只取決於 `StationId`，跟 `ObservedAt`（時間）完全沒關係。這違反的是 **3NF**——「非鍵欄位只能依賴主鍵，不能依賴另一個非鍵欄位」。如果全部存在同一張表，一個站台的站名改了，要更新幾萬筆觀測紀錄，而且萬一有幾筆漏更新，同一個 `StationId` 對應到兩個不同的站名，資料就不一致了。拆表是為了讓「站台資訊只存一次」，觀測資料只存 `StationId`，需要站名時再 JOIN。

**核心差異整理：**
- PestAlerts 拆表：欄位裡有多個值（一對多的值塞在一格）→ 1NF 問題
- Rainfall 拆表：多列重複存了同一份資訊（依賴關係搞錯）→ 3NF 問題

**我學到的原則**
「需要拆表」可能有兩種完全不同的根源。看到「應該拆表」的直覺出現時，要先問清楚是哪一種：「這個欄位存了多個值？」（1NF），還是「這個欄位的值只跟某個非主鍵欄位有關，跟主鍵沒有直接關係？」（3NF）。根源不同，拆出來的表結構也不同，搞混了設計方向就會跑偏。

**下次遇到類似情況，我會先想到什麼**
需要拆表時先問兩個問題：（1）「這個欄位裡有多個值嗎？」——有，1NF 問題，每個值拆一列。（2）「這個欄位的值只跟 StationId 有關，而不是跟整個主鍵有關嗎？」——是，3NF 問題，獨立成主檔表。

---

### 條目 032 — API 文件說一套，實際回傳另一套：Month 欄位憑空消失
 
**我做了什麼**
設計 `PestDecadeSummaryDto` 時，對照農業部 API 文件和 Try-it-out 的範例 schema，把欄位一一列出來。文件和範例都沒有 `Month`，我就沒有加。
 
**我遇到的問題**
實際打 API 看真實資料時，每一筆都有 `"Month": "10"`。文件說沒有，資料說有。如果只看文件設計 DTO，`Month` 就會被 JSON 反序列化器默默忽略，進不了 Entity，Unique Index 裡少了 `Month`，無法區分同一年不同月份的旬報，去重邏輯就會整個錯掉。
 
**我怎麼想通的**
這不是我的錯，是農業部文件的疏漏。但它提醒了一件事：API 文件是人寫的，會有錯誤、過時、或遺漏的情況。「文件說沒有」不等於「資料沒有」，唯一可信的來源是真實的 API 回傳。
 
補上 `Month` 欄位並加 `[JsonPropertyName("Month")]` 之後，DTO 才真正完整。
 
**我學到的原則**
API 文件是起點，不是終點。設計 DTO 之前一定要實際打一次 API，把真實的 JSON 回傳貼出來逐欄位確認。文件和實際不一致時，相信實際。
 
**下次遇到類似情況，我會先想到什麼**
DTO 的欄位清單，一定要從實際 API 回傳的 JSON 比對，不是從文件的 schema 抄。
 
---
 
### 條目 033 — DTO 的型別要反映 API 的實際格式，不是你期望的格式
 
**我做了什麼**
設計 `PestDecadeSummaryDto` 時，看到 API 文件寫 `Average: number`、`Proportion_Island: number`，直覺想把 DTO 的這兩個欄位定義成 `decimal?`。
 
**我遇到的問題**
但實際 API 回傳的是空字串 `""`，不是數字也不是 JSON 的 `null`。`decimal?` 可以接受 JSON 的 `null`，但遇到 `""` 時，`System.Text.Json` 的反序列化器不知道怎麼把空字串轉成 decimal，會直接拋 `JsonException`，整批資料同步失敗。
 
**我怎麼想通的**
問題的根源是：DTO 的職責是「如實描述 API 的回傳格式」，不是「描述我希望資料是什麼格式」。API 實際回傳的是字串，DTO 就該用 `string` 接，不管文件說是 `number`。
 
轉型是 MapToEntity 的責任，不是 DTO 的責任。在 MapToEntity 裡用 `decimal.TryParse()` 嘗試轉換，失敗給 `null`，Entity 的欄位是 `decimal?`。這樣 DTO 和 Entity 各司其職，也完全容忍了 API 的資料品質問題。
 
**我學到的原則**
DTO 反映外部格式（API 說什麼就接什麼），Entity 反映內部語意（我要存的是什麼型別）。兩者之間的轉換，是 MapToEntity 的工作。不要讓 DTO 去假設 API 的行為比實際更好。
 
**下次遇到類似情況，我會先想到什麼**
API 欄位型別跟實際回傳不符時，DTO 用 `string` 接，MapToEntity 用 `TryParse` 轉，Entity 用 nullable 型別存。這是應對外部資料品質問題的標準模式。
 
---
 
### 條目 034 — decimal 欄位不指定精度，EF Core 會靜默截斷
 
**我做了什麼**
跑第一次 `Add-Migration` 時，EF Core 對 `Average` 和 `ProportionIsland` 兩個 `decimal?` 欄位各發出 Warning，說如果值超過預設精度會被靜默截斷。我不確定這嚴不嚴重，一開始想直接跑 `Update-Database`。
 
**我遇到的問題**
「靜默截斷」聽起來就不對。截斷是指值被切掉了一部分，而且 Warning 說這個過程不會拋例外——資料悄悄地被改掉了，程式繼續跑，不會有任何錯誤訊息告訴你發生了什麼。
 
**我怎麼想通的**
舉個例子：如果旬報的平均值是 `12.3456`，但 SQL Server 的預設精度只存兩位小數，寫進去就變成 `12.35`，後面的位數消失了。這個問題在現在的資料是空字串、存進去是 `NULL` 的情況下不會觸發，但一旦農業部補上真實數值，就可能發生。
 
解法是在 `OnModelCreating` 裡明確指定精度：
 
```csharp
entity.Property(e => e.Average).HasPrecision(10, 2);
entity.Property(e => e.ProportionIsland).HasPrecision(10, 2);
```
 
`HasPrecision(10, 2)` 的意思是：整數最多 8 位，小數最多 2 位。明確告訴 SQL Server 用什麼規格存，EF Core 就不再猜測，Warning 消失。
 
加完之後要 `Remove-Migration` 重跑，讓 Migration 帶著正確的精度定義，不能用有 Warning 的 Migration 執行。
 
**我學到的原則**
EF Core 的 Warning 不是可以忽略的提示，它是在說「我用了一個你不知道的預設值，可能不是你要的」。`decimal` 欄位永遠要搭配 `HasPrecision`，明確優於隱性。
 
**下次遇到類似情況，我會先想到什麼**
加 `decimal` 欄位時，OnModelCreating 裡同步加 `HasPrecision`，不等到 Migration 的 Warning 出現再補。
 
---
 
### 條目 035 — DistinctBy 在 incoming 層去重，比只靠 DB HashSet 更乾淨
 
**我做了什麼**
`PestDecadeSyncWorker` 的去重邏輯第一版：把 API 回傳的資料全部轉成 Entity 放進 `incoming`，然後去 DB 查已存在的 Key 組成 HashSet，最後過濾 `incoming` 裡不在 HashSet 裡的，寫入。
 
**我遇到的問題**
`FruitVegetalePestControlType` API 的資料有一個品質問題：同樣的 `(PestName, City, Town, Year, Month, TenDays)` 組合，在同一批回傳資料裡重複出現多次，且每筆欄位值完全相同。如果只靠 DB HashSet 比對，第一次同步時 DB 是空的，HashSet 也是空的，過濾不掉任何東西，`incoming` 裡的所有重複筆都會進 `AddRangeAsync`，然後第一筆 INSERT 成功，第二筆撞到 Unique Index 拋例外。
 
**我怎麼想通的**
根本問題是：DB HashSet 只能過濾「跟歷史資料重複」的情況，但沒辦法過濾「本批次自己內部重複」的情況。這是兩個不同的去重需求，需要分別處理。
 
在 MapToEntity 之後、DB 比對之前，加上 `DistinctBy`：
 
```csharp
var incoming = allDtos
    .Select(MapToEntity)
    .DistinctBy(e => new { e.PestName, e.Year, e.Month, e.TenDays, e.City, e.Town })
    .ToList();
```
 
`DistinctBy` 負責「本批次去重」，DB HashSet 負責「跟歷史去重」。兩層各自負責自己的範圍，資料在進入資料庫流程之前就已經是乾淨的。
 
**我學到的原則**
去重有兩個維度：「本批次內部重複」和「跟已存資料重複」。前者用 `DistinctBy` 在 incoming 層解決，後者用 DB 查詢解決。兩者不要混在一起，職責清晰才不會漏掉角落情況。
 
**下次遇到類似情況，我會先想到什麼**
API 資料有重複的可能性時，先在 incoming 層加 `DistinctBy`，再去做 DB 比對。不要假設 API 的資料是乾淨的。
 
---
 
### 條目 036 — 死碼是症狀，根本問題是 ParseInt 靜默回傳 0
 
**我做了什麼**
模仿 `WeatherSyncWorker` 的寫法，在 `incoming` 後面加了 `.Where(e => e != null)`，覺得這是防禦性寫法，加了比較安全。
 
**我遇到的問題**
`WeatherSyncWorker` 的 `MapToEntity` 回傳 `WeatherObservation?`（nullable），是因為時間格式解析失敗時需要回傳 null 跳過那筆資料。但 `PestDecadeSummary` 的 `MapToEntity` 回傳的是 `PestDecadeSummary`（非 nullable），裡面沒有任何解析失敗就 return null 的路徑，這個方法在任何情況下都會回傳一個有效的 Entity。`.Where(e => e != null)` 永遠不會過濾掉任何東西——這是死碼。
 
**我怎麼想通的**
一開始的直覺是「死碼，刪掉就好」。但刪之前多問了一句：「為什麼 `MapToEntity` 沒有失敗路徑？它應該有嗎？」
 
這一問讓問題浮出來了。`MapToEntity` 裡有這樣一行：
 
```csharp
private static int ParseInt(string s)
    => int.TryParse(s, out var v) ? v : 0;
```
 
`Year`、`Month`、`TenDays` 如果解析失敗，回傳的是 `0`，不是 null，不是例外。`0` 看起來像有效資料，一筆 Year=0、Month=0 的記錄會安靜地寫進資料庫，通過 Unique Index，程式繼續跑，Log 不報警。這就是「靜默錯誤」——比拋例外更危險，因為完全不知道資料已經壞了。
 
死碼不是問題本身，死碼是症狀——它在告訴我 `MapToEntity` 根本沒有設計失敗路徑，而它應該要有。
 
修正方式是把 `MapToEntity` 改成回傳 `PestDecadeSummary?`，並用衛語句在方法開頭擋住解析失敗的情況：
 
```csharp
private PestDecadeSummary? MapToEntity(PestDecadeSummaryDto dto)
{
    if (!int.TryParse(dto.Year, out var year)) return null;
    if (!int.TryParse(dto.Month, out var month)) return null;
    if (!int.TryParse(dto.Decade, out var tenDays)) return null;
    // ...
}
```
 
這樣 `MapToEntity` 現在真的有可能回傳 null 了。`.Where(e => e != null)` 從死碼變回了有意義的防禦，過濾的是真實可能發生的 null。
 
**我學到的原則**
發現死碼時，先問「為什麼它是死碼」，不要直接刪。死碼有時候是在提示你上游的設計有問題——不是「這個 check 多餘」，而是「這個 check 想防的失敗路徑根本沒有被設計進去」。找到根本問題，修正上游，死碼就自然恢復意義了。
 
**下次遇到類似情況，我會先想到什麼**
看到死碼，先往上游找：「它想防的那個情況，為什麼現在不存在？是設計刻意排除了，還是根本忘了設計？」刪死碼是最後一步，不是第一步。

---

### 條目 037 — 導覽屬性跨 DbContext 是「我要管這張表」的宣言，不只是方便存取

**我做了什麼**

設計 `PestRuleConfig` 和 `UserNotification` 時，保留了從昨天討論留下來的 `public ApplicationUser User { get; set; }` 導覽屬性。這個屬性的原意是方便從規則直接導航到使用者資訊。`Add-Migration` 之後，檢查產出的 Migration 檔案，發現裡面有一個完全沒預期到的 `CreateTable("ApplicationUser", ...)`。

**我遇到的問題**

Migration 自己多建了一張 `ApplicationUser` 表，跟 `ApplicationDbContext` 管的 `AspNetUsers` 完全沒有關聯，是一張孤立的冗餘表。如果跑 `Update-Database`，資料庫裡會多出一張用不到的表，而且未來可能跟真正的 Identity 表產生混淆。

**我怎麼想通的**

去看 EF Core 的運作方式：`Add-Migration` 執行時，EF Core 掃描所有 DbContext 知道的 Entity，決定要建哪些表。它「知道」一個 Entity 的途徑有兩個——你顯式宣告了 `DbSet<T>`，或者你在某個 Entity 上宣告了導覽屬性指向它。

`WeatherDbContext` 裡面的 `PestRuleConfig` 有 `public ApplicationUser User { get; set; }`，EF Core 看到這個導覽屬性就說「`ApplicationUser` 是我管轄範圍內的 Entity，我要幫它建表」，所以 Migration 裡出現了 `CreateTable("ApplicationUser")`。

這跟「方便存取」的直覺剛好相反——你以為加導覽屬性只是讓程式碼更方便，其實 EF Core 看到的意思是「你要我管這個 Entity」。導覽屬性是 EF Core 管理關聯的入口，不只是語法糖。

解法是移除導覽屬性，只保留 `UserId` 字串欄位。跨 DbContext 的關聯只能存在於值層面（字串），不能存在於物件層面（導覽屬性）——這是跨模組架構必然要遵守的界線。

**我學到的原則**

在一個 Entity 上加導覽屬性，等於向所屬的 DbContext 宣告「我要管被導覽到的那張表」。跨 DbContext 需要關聯的情況，正確的做法是純字串 FK 欄位 + 放棄導覽屬性，讓應用程式層自己保證值的正確性。看到 Migration 裡出現意料之外的 `CreateTable`，第一個要查的就是導覽屬性。

**下次遇到類似情況，我會先想到什麼**

`Add-Migration` 後先檢查 `Up()` 裡有沒有意料之外的 `CreateTable`，有的話往 Entity 的導覽屬性找原因。

---

### 條目 038 — BackgroundService 適合「排程」，普通 Service 適合「邏輯」：從「能不能被外部呼叫」判斷

**我做了什麼**

設計 `PestRuleEngine` 時，面臨選擇：讓它繼承 `BackgroundService`（自己管排程 + 自己管邏輯），還是抽成普通 Service 讓一個 Worker 來持有。

**我遇到的問題**

一開始覺得都是「定期跑的任務」，跟其他 SyncWorker 做一樣的事，就讓它也繼承 `BackgroundService` 好了。但這個直覺有一個問題——說不清楚「為什麼其他的 SyncWorker 繼承 `BackgroundService` 是對的，`PestRuleEngine` 也繼承就是錯的」，感覺只是口味問題。

**我怎麼想通的**

轉折點是一個具體的問題：「未來如果你想讓管理員透過 API endpoint 手動觸發一次規則評估，你能做到嗎？」

如果 `PestRuleEngine` 是 `BackgroundService`，它自己管自己的排程迴圈，外部沒有辦法直接呼叫它的方法，只能等排程時間到了才跑。手動觸發這件事做不到。

如果 `PestRuleEngine` 是普通 Service，注入到任何地方後就能呼叫 `EvaluateAsync()`——無論是 Worker 的定時呼叫，還是 Controller 的手動觸發，都沒有問題。

這讓我看清楚了兩者的本質差異：`BackgroundService` 適合的是「只由時間觸發、不需要被外部呼叫」的任務，也就是純排程；普通 Service 適合的是「需要被呼叫的邏輯」。`WeatherSyncWorker` 打 API 同步資料，永遠只有排程觸發，繼承 `BackgroundService` 完全合適。`PestRuleEngine` 執行規則判斷，有被其他地方呼叫的合理需求，應該抽成普通 Service。

**我學到的原則**

設計一個「定期執行的任務」時，先問「這個邏輯有沒有可能需要從排程以外的地方觸發？」有的話，把邏輯抽成普通 Service，Worker 只負責排程呼叫。`BackgroundService` 承擔「排程」，普通 Service 承擔「邏輯」，職責分離之後兩者都更乾淨。

**下次遇到類似情況，我會先想到什麼**

要寫一個定期跑的任務時，先問「這個邏輯有沒有機會被手動呼叫或被其他地方復用？」有的話就抽 Service，不要直接寫進 `BackgroundService`。

---

### 條目 039 — 通知去重不是過濾重複資料，而是追蹤「哪筆來源已通知過」

**我做了什麼**

設計 `EvaluateAsync()` 的去重邏輯時，`UserNotifications` 只有 `PestRuleConfigId` 這個欄位，初步想法是用這個欄位判斷「這條規則已經通知過了就跳過」。

**我遇到的問題**

這個邏輯在「一條規則一輩子只觸發一次」的假設下是對的，但病蟲害系統顯然不是這樣運作的——同一個縣市可以在一個月內收到多筆不同的榕小蜂警報，每一筆都是獨立的政府公告，應該各自通知。如果只用 `PestRuleConfigId` 去重，第一筆公告通知完之後，第二筆公告永遠被跳過，使用者收不到後來的新警報。

**我怎麼想通的**

問了一個更基本的問題：「引擎要判斷的不是『這條規則通知過了嗎』，而是『這筆具體的來源記錄通知過了嗎』。」這兩個問題是不同的，回答第二個問題才能解決問題。

要回答「這筆來源記錄（`PestAlert Id=42`）有沒有觸發過通知」，`UserNotifications` 必須記錄「是哪一筆來源記錄觸發了這次通知」。補充 `SourceRecordId int?` 欄位，去重查詢變成 `AnyAsync(n => n.PestRuleConfigId == rule.Id && n.SourceRecordId == item.Id)`。兩個維度都對上才算「已通知過」，缺一不可。

nullable 的原因也清楚了：數值型規則（PestDecade）觸發時也用這個欄位存 `PestDecadeSummary.Id`，但如果未來有某種規則的觸發沒有對應的單一來源記錄，這個欄位可以留 `null`，設計比較有彈性。

**我學到的原則**

「去重」的關鍵不是過濾掉重複的資料，而是精確定義「什麼叫做同一件事發生了兩次」。在通知系統裡，「同一件事」的定義是「同一條規則 + 同一筆來源記錄」，而不是「同一條規則」。設計去重機制之前，先把「什麼叫重複」的業務定義寫清楚，欄位設計自然跟著出來。

**下次遇到類似情況，我會先想到什麼**

設計通知或事件去重時，先問「我要追蹤的是哪一個層次的唯一性」——是規則層次、來源記錄層次，還是規則加來源記錄的組合？確認之後再決定需要哪些欄位。

---

### 條目 040 — AnyAsync：查存在性不需要撈資料，只需要問「有沒有」

**我做了什麼**

在 `EvaluateAsync` 的去重邏輯裡，需要查 `UserNotifications` 有沒有符合條件的記錄。第一個直覺是把資料撈出來再判斷，或者用 `.Distinct()`，兩個方向都不對。

**我遇到的問題**

不知道 EF Core 有什麼方法可以「只問有沒有，不撈資料本身」。問題的本質是：我不需要通知記錄的任何欄位值，我只需要知道「這筆通知存不存在」。

**我怎麼想通的**

這個需求和 `HashSet.Contains` 語意完全一樣——查某個東西有沒有，不拿它的值。EF Core 有對應的方法：`.AnyAsync(condition)`，回傳 `bool`，轉譯成 SQL 是 `SELECT CASE WHEN EXISTS (...) THEN 1 ELSE 0`，比先 `FirstOrDefaultAsync` 再判斷 `null` 效能高，也比撈出整個清單再 `.Any()` 省記憶體。

去重查詢因此變成三行：

```csharp
var exists = await db.UserNotifications
    .AnyAsync(n => n.PestRuleConfigId == rule.Id && n.SourceRecordId == item.Id);
if (exists) continue;
```

**我學到的原則**

EF Core 查詢的選擇要對應「我真正需要什麼」：需要資料本身用 `FirstOrDefaultAsync` 或 `ToListAsync`；只需要知道存不存在用 `AnyAsync`；只需要計數用 `CountAsync`。用錯了不是功能問題，是效能問題。

**下次遇到類似情況，我會先想到什麼**

查詢目的是「這個條件的記錄存不存在」時，直接 `AnyAsync`，不先撈資料再判斷 `null`。

---

### 條目 041 — 衛語句加 continue 在 switch/case 裡的跳出模式

**我做了什麼**

在 `foreach + switch` 的結構裡，需要對 `Threshold == null` 的情況跳過當前規則，繼續處理下一條。

**我遇到的問題**

不確定在 `switch` 的 `case` 裡面能不能直接用 `continue`，因為 `continue` 通常對應的是迴圈，`switch` 本身不是迴圈。

**我怎麼想通的**

`continue` 跳過的是最近一層的迴圈，不是 `switch`。這裡的結構是 `foreach` 包著 `switch`，所以 `switch` 裡面的 `continue` 作用是跳過 `foreach` 的當前迭代，效果就是「這條規則不處理，去下一條」。這正好是衛語句的標準用法：先用衛語句擋掉不符合條件的情況，符合條件的才繼續往下跑。

這個模式讓程式碼的主線邏輯很清楚——能跑到主線查詢的，一定已經通過了所有衛語句，所以 `rule.Threshold.Value` 不會丟例外，因為 `null` 已經被上面的衛語句排掉了。

**我學到的原則**

`switch` 裡的 `continue` 作用在外層迴圈，不是 `switch` 本身。衛語句的核心是「讓主線邏輯只處理合法狀態」，所有邊界情況在最前面就排掉，主線程式碼因此可以假設前置條件都已滿足。

**下次遇到類似情況，我會先想到什麼**

`switch case` 裡需要跳過這條迭代時，直接 `continue`，作用到外層的 `foreach`。

---

### 條目 042 — FilterJson 的兩道衛語句：null 字串和反序列化失敗是兩個獨立的問題

**我做了什麼**

`PlantEpidemic` 分支需要把 `rule.FilterJson`（字串）反序列化成 `PestRuleFilter` 物件後才能查詢。一開始想在反序列化完之後，再檢查 `filter.City == null`。

**我遇到的問題**

這個順序是錯的。如果 `rule.FilterJson` 本身就是 `null`，`JsonSerializer.Deserialize(null)` 會拋 `NullReferenceException`，根本跑不到「檢查 `filter.City`」那一行。

**我怎麼想通的**

這是兩個獨立的失敗情況，需要分別處理：

- 第一道衛語句擋 `rule.FilterJson == null`（字串本身為 `null`，無法反序列化）
- 第二道衛語句擋 `filter == null`（JSON 格式錯誤，反序列化失敗，`Deserialize` 回傳 `null`）

兩道擋完，能跑到查詢那行的 `filter` 一定是有效物件，`filter.City` 和 `filter.PlantName` 一定可以使用。這和 `Threshold` 的衛語句邏輯是一樣的原則：先排掉所有不合法狀態，主線只處理合法情況。

**我學到的原則**

涉及反序列化的地方，至少需要兩道衛語句：先擋輸入字串為 `null`，再擋反序列化結果為 `null`。這兩個失敗路徑的原因不同，錯誤訊息也要區分，才能從 Log 快速判斷是「根本沒有填 `FilterJson`」還是「`FilterJson` 格式壞了」。

**下次遇到類似情況，我會先想到什麼**

呼叫 `Deserialize` 之前先查字串是否為 `null`，呼叫之後再查結果是否為 `null`，兩道缺一不可。

---

### 條目 043 — 事件型通知用持續顯示取代重複推播

**我做了什麼**

設計 Event 型規則的通知行為時，討論到「引擎每天跑，同一筆公告符合條件，要不要每天通知使用者一次？」

**我遇到的問題**

事件型公告的性質是「持續存在的威脅」，不像旬報超過閾值那樣是「瞬時狀態」。使用者已經收到第一次通知之後，這段期間這個威脅還在，但重複通知他同樣的事會造成騷擾。

**我怎麼想通的**

問自己「使用者作為農民，真正需要的是什麼？」他需要的是「在有需要的時候能看到警報」，不是「每天被提醒一次同樣的事」。

解法是把通知的呈現方式從「推播」改成「常駐 UI」：通知只寫一次，前台用紅點或鈴鐺圖示持續顯示「有未讀通知」，使用者主動去看。這和 App 的未讀訊息紅點邏輯一樣——有新訊息就顯示，不是每分鐘推一次。

通知的生命週期由 `ExpiryDays` 控制：`ExpireAt = TriggeredAt + ExpiryDays`，到期後 `EvaluateAsync` 下次跑的時候硬刪除。規則本身的 `IsActive` 不受影響，使用者可以隨時重新啟用。

**我學到的原則**

通知設計要先問「使用者真正需要被告知幾次」。持續存在的事件通知一次、讓 UI 持續顯示，比反覆推播同一件事更符合使用者需求，也更符合軟體設計中「低干擾、高資訊密度」的原則。

**下次遇到類似情況，我會先想到什麼**

設計持續性事件的通知時，先區分「這件事需要告知幾次」和「使用者需要在什麼時候能查到這件事」，前者決定推播次數，後者決定 UI 的顯示邏輯。

---

### 條目 044 — IServiceScopeFactory：Singleton Service 取得 Scoped DbContext 的橋樑
 
**我做了什麼**
 
`PestRuleEngine` 以 `AddSingleton` 註冊，設計上需要在 `EvaluateAsync` 裡存取 `WeatherDbContext`。一開始的直覺是直接在建構子注入 `WeatherDbContext`，跟其他類別一樣。
 
**我遇到的問題**
 
`WeatherDbContext` 的生命週期是 Scoped，`PestRuleEngine` 是 Singleton。Singleton 的生命週期比 Scoped 長，如果直接注入，DI 容器在啟動時就會報錯：「Cannot consume scoped service from singleton」。就算繞過這個限制，`DbContext` 也會在整個應用程式生命週期只建立一次，Change Tracker 持續累積所有歷史狀態，最終出現狀態污染或資料重複寫入的問題。
 
**我怎麼想通的**
 
問題的根源是生命週期不匹配。Singleton 不能直接持有 Scoped 服務，但可以持有一個「能夠在需要時建立 Scoped 容器」的工廠——這就是 `IServiceScopeFactory`。
 
`IServiceScopeFactory` 本身是 Singleton 生命週期，可以安全地被 Singleton 持有。每次 `EvaluateAsync` 執行時，呼叫 `_scopeFactory.CreateScope()` 動態建立一個新的 Scope，從這個 Scope 取得一個全新的 `WeatherDbContext`，執行完畢後 `using` 塊結束，Scope 連同 DbContext 一起釋放。每次執行都是乾淨的 DbContext，Change Tracker 沒有殘留狀態。
 
```csharp
public async Task EvaluateAsync(CancellationToken cancellationToken)
{
    using var scope = _scopeFactory.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<WeatherDbContext>();
    // db 在這個 using 塊結束時自動釋放
}
```
 
**我學到的原則**
 
Singleton 需要存取 Scoped 服務時，注入的不是 Scoped 服務本身，而是 `IServiceScopeFactory`，在每次執行時動態建立 Scope、取服務、用完釋放。這讓 Singleton 的長生命週期和 Scoped 服務的短生命週期可以共存，不會互相污染。
 
**下次遇到類似情況，我會先想到什麼**
 
設計一個 Singleton 類別需要 DbContext 時，先確認不能直接注入 DbContext，改為注入 `IServiceScopeFactory`，在方法內部動態建立 Scope。這是 ASP.NET Core 中 Singleton + DbContext 的標準解法。
 
---
 
### 條目 045 — SaveChangesAsync 放在規則迴圈內，讓每條規則的寫入互相獨立
 
**我做了什麼**
 
`EvaluateAsync` 裡有一個 `foreach (var rule in activeRules)` 的迴圈，每條規則可能觸發多筆通知寫入。需要決定 `SaveChangesAsync` 要放在哪裡：迴圈外一次統一存，還是每條規則跑完就存一次。
 
**我遇到的問題**
 
放在迴圈外看起來效能最好——整個批次只有一次 DB round trip，減少 I/O。但這個選擇有一個隱性代價，第一次思考時沒有注意到。
 
**我怎麼想通的**
 
EF Core 的 `SaveChangesAsync` 是把 Change Tracker 裡所有待寫入的變更一次送出，相當於一個 implicit transaction。如果放在迴圈外，第 1、2、3 條規則的所有通知會在同一個 transaction 裡送出。這意味著：只要任何一條規則的寫入失敗，整批都 rollback，第 1 條和第 2 條已經正確的通知也一起消失。
 
把 `SaveChangesAsync` 放在每條規則的通知迴圈結束後，讓每條規則的寫入互相獨立——第 2 條出錯，第 1 條的通知已經成功寫進去了，不受影響。以規則引擎的使用場景而言，規則數量不多，多幾次 round trip 的代價遠小於批次 rollback 的風險。
 
**我學到的原則**
 
`SaveChangesAsync` 的位置是一個隔離邊界的決定，不只是效能決定。放越晚，batch 越大，效能越好，但出錯時的影響範圍也越大。確定每個邏輯單元應該互相獨立時，就在每個單元結束後存一次，不要為了效能把隔離性犧牲掉。
 
**下次遇到類似情況，我會先想到什麼**
 
在迴圈裡決定 `SaveChangesAsync` 位置時，先問「這幾次寫入出錯，我希望哪些已成功的部分保留下來？」答案決定存的粒度，而不是讓效能直覺決定。
 
---
 
### 條目 046 — FilterJson 的 schema 由使用者視角決定，不由害蟲資料結構決定
 
**我做了什麼**
 
設計 `PestRuleFilter` DTO（存在 `FilterJson` 欄位裡）時，面臨一個選擇：要存什麼欄位？第一個直覺是「這是病蟲害規則，應該讓使用者篩選害蟲名稱」，所以考慮存 `PestName`。
 
**我遇到的問題**
 
存 `PestName` 的邏輯放到農民使用者的角度一看，馬上出現問題：農民不會先知道「榕小蜂」這個害蟲名稱，然後設定規則說「我要監控榕小蜂」。他根本不知道自己的香蕉園會遇到什麼害蟲，他只知道「我在屏東縣，我種香蕉」。
 
**我怎麼想通的**
 
農民真正的問題是：「我的縣市、我種的作物，有沒有病蟲害警報？」
 
這兩個維度——「縣市」和「作物」——才是農民設定規則的自然語言。害蟲名稱是系統資料的分類方式，不是農民認識世界的方式。把害蟲資料的結構直接暴露成使用者的設定維度，是一個以資料為中心、忽略使用者視角的設計錯誤。
 
`PestRuleFilter` 的 schema 因此定為：
 
```json
{
  "City": "屏東縣",
  "PlantName": "香蕉"
}
```
 
查詢也因此走 `PestAlerts.Cities`（縣市）和 `PestAlerts.Crops`（作物）兩個導覽屬性，用 `.Any()` 匹配，而不是直接篩 `PestName` 欄位。這個查詢路徑是從使用者的設定維度自然推導出來的，不是從資料表結構推導出來的。
 
**我學到的原則**
 
功能的輸入欄位設計要從使用者回答「我想要什麼」的語言出發，不是從資料庫的欄位結構出發。兩者有時候一致，有時候不一致。不一致的時候，讓使用者語言決定 DTO 的 schema，讓 DTO 的 schema 決定查詢路徑，不要倒過來讓資料庫結構決定使用者能設定什麼。
 
**下次遇到類似情況，我會先想到什麼**
 
設計使用者可以輸入的篩選條件時，先問「使用者用什麼語言描述他想要什麼」，把這個語言直接翻成 DTO 欄位，再從 DTO 欄位決定查詢要走哪些表和欄位。

---
 
### 條目 047 — 依賴關係決定開發順序：`MarketRestDay` 是 `AgriProductsTrans` 的前置參考資料
 
**我做了什麼**
 
進入 Market 模組時，面對四支 API（`AgriProductsTrans`、`PorkTrans`、`DebrisAlert`、`MarketRestDay`），需要決定開發順序。直覺上想從最核心的交易行情開始，但停下來先分析四支 API 之間有沒有依賴關係。
 
**我遇到的問題**
 
一開始以為 `DebrisAlert`（土石流）跟其他三支沒有關係，可以獨立開發。後來意識到土石流和農產品交易行情是「業務層面的關聯」，兩者的資料放在一起才能做「災後價格波動分析」，這是這個模組的核心功能。同時 `MarketRestDay` 對交易行情資料有「資料依賴」，不是業務關聯——這兩種關聯的性質不同，不能混為一談。
 
**我怎麼想通的**
 
農業部的 `AgriProductsTrans` API 在休市日並不回傳空陣列，而是回傳 `CropName = "休市"`、價格全為 0 的記錄。如果先做 `AgriProductsTrans` SyncWorker 而 `MarketRestDay` 還沒到位，Worker 就無法判斷「今天是否休市」，設計上就得先決定怎麼處理這些 0 值。`MarketRestDay` 是做出這個判斷的前置條件，不先建立好，後面的設計決策就懸在空中。
 
**我學到的原則**
 
四支 API 之間存在兩種不同的關係：業務關聯（土石流 + 交易行情 → 一起分析）和資料依賴（交易行情需要先知道今天有沒有休市）。業務關聯不影響開發順序，資料依賴才決定順序。判斷正確的開發順序，要先問「誰需要誰的資料才能正確運作」，而不是「誰比較重要」。
 
**下次遇到類似情況，我會先想到什麼**
 
面對一組相關的 API 或功能，先畫出依賴圖，找出「B 無法正確設計，除非 A 先存在」的箭頭，被箭頭指向的那個先做。業務關聯另外標注，但不影響順序決策。
 
---
 
### 條目 048 — 0 值污染均價分析：休市筆不存資料庫的設計理由
 
**我做了什麼**
 
發現農業部的 `AgriProductsTrans` API 在休市日回傳 `CropName = "休市"`、價格欄位全為 0 的記錄，需要決定這些記錄要不要存進資料庫。
 
**我遇到的問題**
 
第一個反應是「都拿到資料了，直接 `if (CropName == "休市") return` 跳過就好」。但這樣前台畫 30 天走勢圖時，休市的那幾天會出現斷點，使用者無法分辨「那天是休市還是資料遺漏」。另一個想法是把休市筆原樣存進去，這樣至少走勢圖不會有空洞。
 
**我怎麼想通的**
 
把休市筆存進 `AgriProductsTrans` 表的問題在於資料用途：這張表的核心功能是支撐「災後農產品均價分析」。計算均價時，如果 0 值混在裡面，每次查詢都要先過濾 `CropName != "休市"`，這個過濾責任被轉嫁給了所有查詢端，而且萬一哪一個查詢漏掉這個條件，均價就會被拉低，分析結果靜默地失真。
 
正確的設計是「資料的意義要純粹」：`AgriProductsTrans` 只存有意義的交易記錄，休市資訊交給 `MarketRestDay` 負責。前台查不到交易資料的那天，去 `MarketRestDay` 查一下「這天是否休市」，是就在走勢圖上標注「休市」，不是就顯示「資料缺漏」。兩張表各司其職，職責不混淆。
 
**我學到的原則**
 
一張資料表的欄位應該只描述同一種語義的事情。把「休市標記」和「農產品交易價格」混在同一張表裡，是把兩件不同的事情混進了同一個語義空間，查詢端永遠需要知道這個混淆的存在才能寫出正確的查詢。分開存放代表關注點分離，每張表的查詢邏輯才是自我完備的。
 
**下次遇到類似情況，我會先想到什麼**
 
一個欄位出現「無意義值」（例如全 0、`null`、`"N/A"`）時，先問「這個無意義值代表的是另一種狀態，還是這筆資料根本不應該存在這張表裡？」如果是後者，就把它過濾掉，用另一張表或另一個機制記錄那個「另一種狀態」。
 
---
 
### 條目 049 — 巢狀 JSON 攤平：API 格式為傳輸設計，資料庫格式為查詢設計
 
**我做了什麼**
 
農業部的 `MarketRestDay` API 回傳五層巢狀結構：`市場 → 交易類型 → 年 → 月 → 休市日字串`。需要把這個結構存進關聯式資料庫。
 
**我遇到的問題**
 
拿到巢狀 JSON 的第一個反應是「該怎麼設計資料表對應這個結構」，甚至想過是否需要建立對應巢狀層級的多張表，或者把 JSON 直接存成字串。
 
**我怎麼想通的**
 
API 的巢狀結構是為了「傳輸方便」設計的——把同一個市場的所有年月資料包在一起，避免重複傳輸 `MarketCode`、`MarketName` 這些相同的值，減少傳輸體積。但資料庫不關心傳輸效率，它關心的是查詢效率。資料庫的查詢是「給我台北二市場、F 類型、115 年 1 月的所有休市日」，這個查詢需要的是平坦的、每一列都包含完整語義的記錄，而不是巢狀結構。
 
攤平的方法是在 SyncWorker 裡用四層 `foreach` 從外到內走訪，每走到最底層的一個日期，就組出一筆完整的平坦記錄存進去。`"05、08、12"` 這個字串拆成三筆，每筆都完整包含 `MarketCode`、`MarketType`、`Year`、`Month`、`RestDay`，資料庫裡不需要知道這些記錄在 API 傳輸時是被巢狀在同一個父節點下的。
 
**我學到的原則**
 
API 格式和資料庫格式各自服務於不同的需求：API 格式服務於網路傳輸（減少重複欄位），資料庫格式服務於查詢（每列自我完備）。看到巢狀 JSON 時，先問「這個巢狀結構是為了傳輸方便還是真的有業務上的層次語義」，如果是前者，攤平後存入，SyncWorker 承擔這個轉換責任。
 
**下次遇到類似情況，我會先想到什麼**
 
遇到多層巢狀 API 資料時，先定義「攤平後一筆記錄是什麼」，也就是「什麼叫做一個最小的、有獨立意義的資料單元」，再設計 Entity 和攤平邏輯。不要試圖在資料庫裡重現 API 的巢狀結構。
 
---
 
### 條目 050 — 多 DbContext 的 Migration 指令需要明確指定目標
 
**我做了什麼**
 
`Market` 模組是繼 `Weather` 模組之後的第二個模組，整個專案首次同時存在兩個 DbContext（`WeatherDbContext` 和 `MarketDbContext`）。需要在正確的專案下產生 Migration。
 
**我遇到的問題**
 
之前只有一個 DbContext 的時候，直接下 `Add-Migration` 就能運作。這次如果一樣直接下，EF Core 不知道要針對哪個 DbContext 操作，會報錯或操作到錯誤的 DbContext。
 
**我怎麼想通的**
 
需要明確指定兩個額外參數：
 
```
Add-Migration AddMarketRestDayEntity -Context MarketDbContext -Project TaiwanAgri.Modules.Market
```
 
`-Context` 告訴 EF Core「這次操作的對象是哪個 DbContext」，`-Project` 告訴它「Migration 檔案要輸出到哪個專案資料夾」。`Update-Database` 同樣需要加 `-Context MarketDbContext`，否則 EF Core 會不知道要對哪個 DbContext 套用 Migration。
 
**我學到的原則**
 
單一 DbContext 時 EF Core 能自動推斷目標，多個 DbContext 時必須明確指定。這不是例外情況，是多模組架構的正常操作，每次新增模組都需要記得加這兩個參數。
 
**下次遇到類似情況，我會先想到什麼**
 
第二個模組開始跑 Migration 時，直接用完整指令，不嘗試省略 `-Context` 和 `-Project`，也不等到報錯後再補。把完整指令格式記成範本。
 
---
 
### 條目 051 — Modular Monolith 原則：模組定義形狀，入口層決定連線和啟動
 
**我做了什麼**
 
把 `MarketDbContext` 建立在 `TaiwanAgri.Modules.Market` 裡，然後在 `TaiwanAgri.Worker` 的 `Program.cs` 裡用 `AddDbContext<MarketDbContext>` 加上連線字串進行註冊，同樣在這裡用 `AddHostedService<MarketRestDaySyncWorker>()` 啟動 Worker。這引出了一個疑問：為什麼不在模組裡自己做這些設定？
 
**我遇到的問題**
 
直覺上覺得「每個模組自己管自己的 DbContext 設定，不是更封裝嗎？」但仔細想，如果 `TaiwanAgri.Modules.Market` 裡直接寫死連線字串，這個模組就綁定了特定的執行環境，測試環境要用另一個資料庫時，就沒有辦法替換。
 
**我怎麼想通的**
 
`TaiwanAgri.Modules.Market` 負責定義「我的 DbContext 長什麼樣、我管哪些表」，這是業務邏輯，不應該改變。`TaiwanAgri.Worker` 負責「這個模組在這個執行環境裡要連哪個資料庫、什麼時候啟動」，這是執行設定，應該集中管理。兩者的關注點不同，合在一起會讓模組無法在不同環境下重用。
 
這就是 Modular Monolith 的核心邊界：模組是業務單元，入口層是組裝和啟動的場所。
 
**我學到的原則**
 
模組不應該知道自己的執行環境。模組只定義「我需要什麼」（DbContext 的結構），入口層負責「用什麼來滿足這個需求」（連線字串、生命週期設定）。這讓每個模組都可以在不修改自身程式碼的前提下，被組裝進不同的執行環境（Worker、Web、Test）。
 
**下次遇到類似情況，我會先想到什麼**
 
在模組裡看到任何環境相關的設定（連線字串、外部服務 URL、排程週期）時，把它移到入口層。模組只宣告依賴介面，入口層提供實作和設定值。
 
---
 
### 條目 052 — `AddRange` vs `AddRangeAsync`：記憶體操作不需要 async
 
**我做了什麼**
 
寫入資料庫之前呼叫 `AddRangeAsync(toInsert, stoppingToken)`，想說帶上 `CancellationToken` 比較安全，避免程式關閉時中途被打斷。
 
**我遇到的問題**
 
`AddRangeAsync` 確實存在，但思考一下它實際做什麼：把資料加進 EF Core 的 Change Tracker。Change Tracker 是記憶體裡的一個物件，沒有任何 I/O 操作——沒有網路請求、沒有磁碟寫入，純粹是把 `Entity` 物件放進一個 `List`。
 
**我怎麼想通的**
 
`async` / `await` 的意義在於「這個操作需要等待 I/O 完成」，讓執行緒在等待期間可以去做別的事。純粹的記憶體操作根本不需要等待，用 `async` 版本只是多包了一層 `Task` 的包裝，沒有實質意義，反而讓程式碼讀起來像是有 I/O 在發生。
 
真正需要 `await` 的是 `SaveChangesAsync`——那個才是把 Change Tracker 裡的變更實際送到 SQL Server 的 I/O 操作。`CancellationToken` 也應該傳給它。
 
正確寫法：
 
```csharp
db.MarketRestDays.AddRange(toInsert);       // 記憶體操作，同步即可
await db.SaveChangesAsync(stoppingToken);   // I/O，才需要 await 和 CancellationToken
```
 
**我學到的原則**
 
判斷一個操作需不需要 `async`，不是看方法名稱有沒有 `Async` 後綴，而是看這個操作有沒有真正的 I/O 等待。EF Core 提供了很多 `Async` 版本的方法，其中有些（例如 `AddRangeAsync`）的 `Async` 本質上是無意義的，用同步版本更能正確表達「這裡沒有 I/O」的語義。
 
**下次遇到類似情況，我會先想到什麼**
 
看到 `await someMethod()` 時，先確認 `someMethod` 裡有沒有真正的 I/O。如果是純記憶體操作，改用同步版本，讓 `await` 只出現在真正需要的地方，程式碼的 I/O 語義才清晰。
 
---
 
### 條目 053 — 時間複雜度是每層大小相乘，不是層數的次方
 
**我做了什麼**
 
寫完五層巢狀 `foreach` 之後，自問「這是不是 O^5？五層迴圈是不是效能問題？」
 
**我遇到的問題**
 
「O^5」這個表達方式本身是錯的。Big-O 描述的是隨輸入量增長，執行時間如何增長，表達為 `O(f(n))`。「O^5」不是合法的 Big-O 表達式，背後反映的直覺是「五層迴圈 = 指數增長」，這個直覺有誤。
 
**我怎麼想通的**
 
巢狀迴圈的複雜度是每一層的大小相乘，不是層數的指數。這五層各自的大小：市場數（台灣農產市場幾十個）× 交易類型數（2-3 種）× 年份數（API 回傳約 5 年）× 月份數（固定 12）× 每月平均休市天數（約 6-8 天）。全部相乘大約幾千筆，這是一個有上限的常數，不會隨使用者數量或時間無限增長。真正的 Big-O 是 `O(M × T × Y × 12 × D)`，其中所有變數都有自然上限，實際複雜度接近常數。
 
「五層迴圈很慢」的直覺對無限增長的輸入（例如對整個網路爬蟲的結果做五層巢狀處理）是有意義的警戒，但對有自然邊界的資料集（台灣農產市場的數量不會突然變成一百萬）是誤用。
 
**我學到的原則**
 
評估巢狀迴圈的效能時，先問「每一層的大小是什麼，有沒有自然上限？」層數本身不決定效能好壞，每層的增長特性才決定。有自然上限的資料（參考資料、地理資料、固定清單）即使巢狀層數多，實際執行量通常是可接受的常數；無上限增長的資料（使用者行為記錄、交易流水）才需要認真優化。
 
**下次遇到類似情況，我會先想到什麼**
 
看到多層迴圈時，先標出每一層走訪的資料集是什麼，問「這個資料集的大小會無限增長嗎？」如果每層都有自然上限，效能通常不是問題；如果有任何一層是無限增長的，那才是需要優化的地方。

---

### 條目 054 — Schema 是資料庫層的模組邊界，不是前綴字的替代品

**我做了什麼**

發現資料庫裡的 market 和 weather 模組的資料表全部混在 dbo 下，考慮用前綴字（MKT_、WEA_）來做視覺區分，後來改成用 SQL Server 的 Schema 機制：`entity.ToTable("TableName", schema: "market")`。

**我遇到的問題**

不清楚為什麼業界比較推薦 Schema 而不是前綴字——兩個方式視覺上看起來效果差不多，前綴字甚至不用修改 OnModelCreating 的設定方式。

**我怎麼想通的**

前綴字是「用命名解決架構問題」的補丁，Schema 是資料庫本身提供的命名空間機制。具體差異：Schema 讓 SSMS 自動按模組分群顯示，查詢時也可以用 `SELECT * FROM market.AgriProductsTrans` 明確表達意圖。更重要的是，在 Modular Monolith 的語境裡，Schema 讓「模組有自己的資料邊界」這件事變得可驗證——光看資料庫結構就能確認 Market 模組沒有越界存取 Weather 模組的表。

這個改動需要重建整個 Migration（清空 Migrations 資料夾、刪 DB、重新 Add-Migration）。開發階段資料可以重跑 Worker 補回，這個代價是合理的。若在已有生產資料的環境，就需要 `ALTER SCHEMA TRANSFER` 的補丁 Migration，代價更高，所以儘早統一是正確的。

**我學到的原則**

Schema 是 SQL Server 的命名空間機制，不是視覺糖衣。用 Schema 表達模組邊界，讓資料庫結構能對應程式碼的架構層次，這是 Modular Monolith 的資料層設計原則。EF Core 的 `ToTable("name", schema: "module")` 是宣告這個 Entity 歸屬於哪個模組的標準寫法。

**下次遇到類似情況，我會先想到什麼**

新增模組的第一張表時，先確認 Schema 是否已設定。如果沒有，這是最低代價的修正時機。

---

### 條目 055 — 真實 API 資料可以推翻 Entity 設計的假設

**我做了什麼**

設計 MarketInfo Entity 時，把 MarketCode 定為 PK——這是自然的選擇，MarketCode 是有業務意義的識別碼，正規化的書也說業務代碼適合當 PK。Migration 跑完、資料表建好之後，實際打開 API 回傳的 JSON，發現 MarketCode 514 在 Veg API 叫「溪湖鎮」，在 Flower API 叫「彰化市場」。

**我遇到的問題**

這兩個名稱分別可以查到不同的 AgriProductsTrans 交易資料（蔬菜 vs 花卉），兩筆都需要存進 MarketInfos，但 MarketCode = "514" 只能有一筆——PK 衝突。

**我怎麼想通的**

「一個 MarketCode 對應一筆主檔」的假設在這份資料集不成立。農業部的 API 設計讓同一個市場代碼在不同的資料類型 API 裡用不同的名稱——這不是錯誤，是業務現實的反映（溪湖鎮農產批發市場同時辦理蔬菜和花卉交易，名稱依交易類型不同）。Entity 設計必須容納這個現實，而不是試圖把現實強行fit 進一開始的假設。

解法：PK 改成 surrogate Id，讓資料庫 PK 不與業務代碼綁定；Unique constraint 改為 `(MarketCode, MarketName)`，這個組合才是「重複」的真實定義。514 溪湖鎮和 514 彰化市場是兩筆不同的記錄，自然並存。

**我學到的原則**

Entity 設計從真實 API 資料出發，不從直覺或文件出發。業務代碼適合當 PK 是通則，但通則有例外——當同一個代碼在不同 context 下對應多筆記錄時，surrogate PK 才是正確選擇。這個發現的時機越早越好，在 Migration 跑完之前修改代價最低。

**下次遇到類似情況，我會先想到什麼**

設計 Entity 之前，先打一次 API 看真實回傳，特別確認「我打算當 PK 的欄位，在所有資料來源裡是否唯一」。

---

### 條目 056 — 同模組內也可以用值層面關聯：PK 結構改變時的 FK 取捨

**我做了什麼**

MarketInfo 的 PK 從 MarketCode 改成 surrogate Id 之後，`AgriProductsTrans.MarketCode → MarketInfos.MarketCode` 的 FK 關係無法維持——MarketCode 不再是 PK，SQL Server 不允許 FK 指向非 PK 非 Unique 的欄位。

**我遇到的問題**

考慮把 FK 改成指向 surrogate Id（`AgriProductsTrans.MarketInfoId → MarketInfos.Id`），但這要求 Worker 在寫入每筆交易前先查 MarketInfos 找到對應的 Id，多一次查詢且增加邏輯複雜度。

**我怎麼想通的**

先問「FK 在這裡的實際作用是什麼」。FK 的作用有兩個：一是資料完整性（防止寫入不存在的市場代碼），二是表達關聯語意（AgriProductsTrans 知道它有一個 MarketInfo 對應）。

在這個 Worker 的設計裡，AgriProductsTransSyncWorker 的市場清單本來就從 MarketInfos 讀出來，寫進 AgriProductsTrans 的 MarketCode 一定是有效的，FK 的完整性保護是多餘的。而關聯語意可以靠欄位名稱和文件傳達，不一定需要物理 FK。

跨 DbContext 時我們已經學過用值層面關聯的原則——這裡雖然是同一個 DbContext，但 PK 結構改變讓 FK 的建立代價高於它帶來的好處，同樣適用值層面關聯的邏輯。

**我學到的原則**

值層面關聯不只適用於跨 DbContext 的場景。當 FK 的維護成本（額外查詢、邏輯複雜度）高於它帶來的保護價值時，即使在同一個 DbContext 內，值層面關聯也是合理選擇。判斷標準是：應用程式層的邏輯是否已經足夠保證完整性。

**下次遇到類似情況，我會先想到什麼**

FK 是資料庫層的保護機制，但不是唯一的完整性保證手段。當 FK 的建立讓設計變複雜時，先問「應用程式層能不能自己保證這個約束」。

---

### 條目 057 — HashSet 記憶體鏡像：讓多次 API 只查一次 DB

**我做了什麼**

CropMarketSyncWorker 需要對三隻 API（Veg / Fruit / Flower）的回傳資料做去重，去重的 key 是 `(MarketCode, MarketName)`。去重需要知道「DB 裡已有什麼」，有兩個做法：每隻 API 打完後各查一次 DB，或是一開始查一次 DB 建 HashSet 共用。

**我遇到的問題**

如果一開始建一次 HashSet 共用，第二隻 API 比對時 HashSet 裡只有「DB 原有的」資料，不包含第一隻 API 剛 Add 但還沒 SaveChanges 的新資料。如果 Fruit API 恰好有跟 Veg 重複的市場，就會重複 Add，最後 SaveChanges 時撞 Unique constraint。

**我怎麼想通的**

HashSet 不需要只反映 DB 的狀態，可以讓它反映「DB + 尚未存入的資料」的聯集。做法：比對後把新增的 `(MarketCode, MarketName)` 不只 Add 進 Change Tracker，同時也 Add 進 HashSet。這樣 HashSet 就變成一個即時維護的記憶體鏡像，第二、三隻 API 比對時拿到的是最新狀態。

三次 API 一次查 DB + 一次 SaveChanges，比「三次查 DB + 三次 SaveChanges」更清晰，也減少不必要的 I/O。

```csharp
await db.MarketInfos.AddRangeAsync(toAdd, stoppingToken);
foreach (var m in toAdd)
{
    existingMarketCodes.Add((m.MarketCode, m.MarketName));  // 同步維護鏡像
}
```

**我學到的原則**

HashSet 去重模式的完整版是「比對 + Add Change Tracker + Add HashSet」三步。只做前兩步，HashSet 就會和 Change Tracker 的狀態脫節，在多輪比對的情境下去重會失效。

**下次遇到類似情況，我會先想到什麼**

用 HashSet 做去重時，問自己「這個 HashSet 的有效期到哪裡」。如果跨越多次資料新增，就需要在每次新增後同步維護 HashSet，讓它持續反映最新狀態。

---

### 條目 058 — Worker Context 注入欄位：API 不回傳的業務資訊如何寫進 Entity

**我做了什麼**

MarketInfo Entity 設計了 `MarketType` 欄位（Veg / Fruit / Flower），記錄這個市場是哪種類型的農產品市場。但實際打開 `/CropMarketType/` API 的 response，裡面只有 `MarketCode` 和 `MarketName` 兩個欄位，沒有 `MarketType`。

**我遇到的問題**

`MapToEntity(dto)` 方法需要建立 `MarketInfo`，但 `MarketType` 從哪裡來？DTO 沒有這個欄位，如果硬是在 DTO 加一個 `MarketType`，又不符合「DTO 對應 API 真實回傳」的原則。

**我怎麼想通的**

`MarketType` 不是 API 回傳的資料，是 Worker 在呼叫哪個 endpoint 時才知道的 context。這個 context 在迴圈變數 `item`（"Veg" / "Fruit" / "Flower"）裡。

解法：`MapToEntity` 加第二個參數 `string marketType`，呼叫時把迴圈變數傳進去。這樣 DTO 不需要改，API 回傳的資料形狀不被汙染，`MarketType` 的來源明確標示在呼叫端。

```csharp
private MarketInfo MapToEntity(CropMarketTypeDto dto, string marketType)
{
    return new MarketInfo
    {
        MarketCode = dto.MarketCode,
        MarketName = dto.MarketName,
        MarketType = marketType,   // ← 來自 Worker context，不來自 DTO
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}
```

**我學到的原則**

DTO 只對應 API 回傳的資料形狀。需要補充「API 沒有回傳但 Entity 需要的欄位」時，透過方法參數把 Worker context 傳進 mapping 方法，而不是讓 DTO 承擔這個責任。這樣 DTO 的邊界清晰，mapping 方法的輸入來源也一目了然。

**下次遇到類似情況，我會先想到什麼**

看到 mapping 方法需要一個「DTO 裡沒有」的欄位時，先判斷這個值來自哪裡。來自呼叫端的 context → 方法參數。來自其他查詢 → 方法參數或在呼叫前先查好傳進去。不應該讓 DTO 反映非 API 資料。

---

### 條目 059 — 硬編碼的時機原則：讓依賴方的啟動條件永遠成立

**我做了什麼**

MarketInfos 表需要補一筆硬編碼：105 台北市場（任何 API 都沒有這個名稱，但 AgriProductsTrans API 的交易資料裡有）。設計時需要決定這筆硬編碼放在哪個位置——三隻 API sync 之前還是之後。

**我遇到的問題**

放在前面還是後面好像都可以，因為 Unique constraint 是 `(MarketCode, MarketName)`，105 台北市場和 105 台北花市是不同組合，不會衝突。

**我怎麼想通的**

衝突問題確實不存在，但有另一個問題：如果硬編碼放在 API sync 之後，API sync 中途失敗（網路瞬斷、rate limit 被封）→ Worker 拋例外中斷 → 硬編碼那筆沒有寫進去。AgriProductsTransSyncWorker 排程跑起來，讀 MarketInfos 表，找不到 105 台北市場，這個市場的花卉交易資料就永遠無法同步，直到下一次 CropMarketSyncWorker 成功完整跑完為止。

硬編碼放在 API sync 之前，並且有自己的 `SaveChangesAsync`（與 API sync 的最終 Save 分開），就能保證：不管 API sync 成功或失敗，105 台北市場一定已經在 MarketInfos 表裡。

**我學到的原則**

「硬編碼的時機」是一個依賴關係問題，不是衝突問題。判斷標準：「這筆資料是否是後續某個元件的啟動條件？」如果是，就要在那個元件可能被觸發之前確保它存在，並且要有獨立的 Save，不要和可能失敗的操作綁在同一個 transaction 裡。

**下次遇到類似情況，我會先想到什麼**

有硬編碼需求時，先畫出依賴圖：「誰需要這筆資料才能正常運作？」如果有下游依賴，就把硬編碼放在最前面並獨立 Save，確保依賴方的前提條件不受上游失敗影響。

---

### 條目 060 — 匿名型別的值相等性陷阱：HashSet.Contains 在匿名型別上失效

**我做了什麼**

從 DB 建立去重 HashSet 時，第一版寫法是：

```csharp
var existingMarketCodes = await db.MarketInfos
    .Select(m => new { m.MarketCode, m.MarketName })
    .ToHashSetAsync();
```

然後用 `.Contains(...)` 比對 API 回來的 MarketInfo 物件。

**我遇到的問題**

`existingMarketCodes` 是 `HashSet<匿名型別>`，`incoming` 是 `HashSet<MarketInfo>`，兩個型別完全不同，根本無法做比對。

**我怎麼想通的**

C# 的匿名型別（`new { ... }`）沒有名字，只在宣告的那行有效，無法在其他地方以型別名稱引用或作為方法參數型別。雖然 C# 對相同欄位組合的匿名型別有值相等性，但跨方法傳遞時型別已經不被識別。

解法：改用 `ValueTuple`——

```csharp
var existingMarketCodes = await db.MarketInfos
    .Select(m => new ValueTuple<string, string>(m.MarketCode, m.MarketName))
    .ToHashSetAsync();
```

`ValueTuple` 支援值相等性比對，`HashSet<(string, string)>` 可以用 `.Contains(("514", "溪湖鎮"))` 直接比對，而且 `(string, string)` 可以在整個方法裡被正確識別和傳遞。

另一種等效做法是字串拼接（`m.MarketCode + "_" + m.MarketName`），回傳 `HashSet<string>`，加底線分隔符確保不同欄位組合不會碰撞。

**我學到的原則**

匿名型別適合在 LINQ 查詢的同一個 scope 內用，不適合用在需要跨越方法邊界或進行精確型別比對的場景。需要可引用、可比對的複合型別時，`ValueTuple` 或具名 record 是正確選擇。

**下次遇到類似情況，我會先想到什麼**

看到 `new { ... }` 的匿名型別準備放進 HashSet 時，先問「我之後需要用這個 HashSet 做 `.Contains()` 嗎？如果需要，被 Contains 的是什麼型別？」型別不一致就換成 `ValueTuple` 或字串拼接。

---

### 條目 061 — ParseRocDate / FormatRocDate 的命名邏輯與 Core 層放置原則

**我做了什麼**

在 `TaiwanAgri.Core/Helpers/DateHelper.cs` 裡實作了兩個靜態方法，負責農業部 ROC 民國曆日期字串與 `DateOnly` 的雙向轉換。最初我把方法命名為 `GetADYear()`，後來改成 `ParseRocDate()`。

**我遇到的問題**

`GetADYear()` 這個名字暗示回傳一個「年份數字」，但實際回傳的是 `DateOnly`，呼叫端讀到方法名稱時會產生語意誤導。另外，我也思考過要不要把這個方法放在 Market 模組內部。

**我怎麼想通的**

命名要對應回傳值的語意，而不是對應「做了什麼動作」的其中一個步驟。`ParseRocDate()` 清楚說明「輸入是 ROC 格式的日期字串，輸出是一個解析後的日期物件」，而 `GetADYear()` 讓人誤以為只取出了年份數字。改名之後，呼叫端的程式碼讀起來更直白：`var transDate = DateHelper.ParseRocDate(dto.TransDate)` 一眼就懂。

放置位置的選擇也是同樣的邏輯。農業部的很多 API 都用 ROC 民國曆格式（氣象類、行情類都有），如果把 `DateHelper` 放在 `Market` 模組裡，未來其他模組的 SyncWorker 就必須跨模組依賴 Market 才能使用這個工具，違反模組邊界原則。`Core` 層存放的就是「任何模組都可能需要的共用工具」，這個判斷標準讓位置決定變得簡單。

**我學到的原則**

方法命名應該對應方法的整體輸入輸出，而不是對應內部實作的某個中間步驟。看到方法名稱裡有「Get」但實際做的是解析轉換，就應該把名字改成 `Parse`。共用工具的放置位置取決於它的使用範圍，只有一個模組會用的邏輯留在模組裡，任何模組都可能用的提升到 Core 層。

**下次遇到類似情況，我會先想到什麼**

寫完方法之後，讀一遍方法簽章（名稱 + 參數 + 回傳型別），問「如果我第一次看到這個簽章，我能猜到它做什麼嗎？」如果猜不到，就改名。

---

### 條目 062 — SyncState 模式 vs MAX(TransDate)：為什麼看似合理的設計有致命缺陷

**我做了什麼**

設計 `AgriProductsTransSyncWorker` 的增量同步機制時，第一個想法是每次執行前查 `MAX(TransDate)` 當作上次同步的終點，下次從 `MAX + 1 天` 開始。後來改成在 `CoreDbContext` 建立 `SyncStates` 資料表追蹤進度。

**我遇到的問題**

`MAX(TransDate)` 看起來完全合理——既然 DB 裡最新的一筆是某天，那下次就從那天之後繼續，邏輯通順，也不需要額外的資料表。

**我怎麼想通的**

`MAX(TransDate)` 追蹤的是「DB 裡有資料的最後一天」，不是「Worker 最後成功執行到的那天」。這兩件事在正常情況下相同，但在一個特定場景下分叉：全市場休市的日期。

農業部的 API 在休市日回傳的是 `CropCode == "-"` 的特殊記錄，Worker 設計上會過濾掉這些記錄不寫入 DB（因為 0 值會污染均價）。所以休市日這天：API 打了、過濾了、但 DB 沒有寫入任何這天的記錄。`MAX(TransDate)` 永遠停在前一個交易日，下次 Worker 跑起來發現「最新的是 N-2 天，那就從 N-1 天開始同步」，而 N-1 天是休市日，同樣過濾掉，同樣沒有寫入，`MAX` 繼續停著。這是一個永遠無法自癒的卡死。

`SyncState` 的 `LastSyncedDate` 欄位追蹤的是「Worker 最後成功跑完的那天」，不管那天是否有資料寫入 DB，日期都往前推進。休市日也不例外：Worker 跑完這天的迴圈、更新 `LastSyncedDate = 今天`、下次從明天繼續，完全不受休市影響。

**我學到的原則**

「從 DB 的資料推算下次的起始點」這類設計，要先問「有沒有合法情況讓 DB 某天完全沒有資料，但我其實已經處理過那天了？」如果有，就不能用 DB 資料反推進度，必須獨立追蹤。進度追蹤應該記錄「我做了什麼」，而不是「我的結果長什麼樣」。

**下次遇到類似情況，我會先想到什麼**

設計增量同步時，先問「有沒有某天合法地不產生任何資料，但邏輯上應該被算為『已完成』？」如果有，就需要獨立的同步狀態表，不能依賴業務資料表的 MAX 值。

---

### 條目 063 — off-by-one 邊界設計的語意選擇：LastSyncedDate 代表「已完成的最後一天」

**我做了什麼**

`SyncState` 的初始值設計：農業部行情資料從 `2018/07/01` 開始，第一次執行的 Worker 應該從這天開始同步。`SyncState` 初始化時 `LastSyncedDate` 設成 `2018/06/30`。

**我遇到的問題**

初始值為什麼是 `06/30` 而不是 `07/01`？如果「從 07/01 開始」，不是應該把起始點設為 `07/01` 嗎？

**我怎麼想通的**

關鍵在於先確定欄位的語意，再推導初始值，而不是反過來。

`LastSyncedDate` 的語意是「已完成同步的最後一天」。`startDate` 的計算方式是 `LastSyncedDate.AddDays(1)`。所以：

- 如果 `LastSyncedDate = 2018/07/01`，那 `startDate = 2018/07/02`，漏掉第一天。
- 如果 `LastSyncedDate = 2018/06/30`，那 `startDate = 2018/07/01`，正確。

`2018/06/30` 代表「這天之前（含）都已完成，其實什麼都沒有做，但語意上視為已完成」，是一個在農業部資料庫裡完全不存在的日期，純粹用來錨定語意。

這個問題的本質是：當欄位語意是「已完成的最後一個」時，初始值必須是「第一個要做的事情的前一個」，不是「第一個要做的事情本身」。就像一個書籤放在「已讀完的最後一頁」，初始狀態的書籤應該放在第 0 頁（封面之前），而不是第 1 頁，否則第 1 頁就被跳過了。

**我學到的原則**

設計「斷點恢復」機制時，先定義欄位的語意（「已完成的最後一個」vs「下次要從哪個開始」），再從語意推導初始值。不要用直覺猜初始值，猜完再用例子驗算：帶入初始值算出 startDate，確認第一次執行會從正確的位置開始。

**下次遇到類似情況，我會先想到什麼**

設計完斷點恢復的欄位後，做一個心算：「如果初始值是 X，第一次執行會從哪天開始？」把答案說出來，確認和預期一致，才算設計完成。

---

### 條目 064 — API 三參數策略：為什麼同時帶入 Start_time、End_time、MarketName 可以抑制分頁

**我做了什麼**

在實作 `AgriProductsTransSyncWorker` 的 API 呼叫策略時，測試了不同的參數組合，發現帶入三個參數（`Start_time` + `End_time` + `MarketName`）時，回傳的 `Next` 欄位始終為 `false`，不需要分頁迴圈。

**我遇到的問題**

最初設計是外層日期迴圈 × 內層分頁迴圈，參考了 `WeatherSyncWorker` 的架構。但分頁迴圈在這裡反而讓程式碼複雜，還要處理農業部「非會員只限回傳第一頁」的商業限制。

**我怎麼想通的**

農業部的分頁觸發條件是「查詢結果量超過單頁上限」。如果不指定 MarketName，一次 API 呼叫會回傳當天全台所有市場的所有作物交易記錄，這個量很容易超過分頁閾值。但如果同時指定了 MarketName（某個特定市場）加上 Start_time 和 End_time（某一天），查詢範圍被縮減到「某個市場某一天的交易」，這個量就有自然上限，不需要分頁。

這個「三參數策略」帶來了一個副作用：Worker 從「每天一次 API 呼叫（拿全台資料）+ 多頁」變成「每天 × 每個市場各一次 API 呼叫（拿單一市場資料）+ 不需要分頁」。API 呼叫次數增加（等於市場數量），但每次呼叫更小、更可預期、錯誤隔離更好（某個市場 API 失敗不影響其他市場）。

**我學到的原則**

遇到分頁 API 時，先問「有沒有辦法讓一次呼叫的結果量控制在不需要分頁的範圍內？」如果可以透過增加查詢參數的精確度來達到，分頁邏輯就可以省掉。用更多次精確的呼叫取代更少次帶分頁的呼叫，通常讓程式碼更簡單、更容易推理。

**下次遇到類似情況，我會先想到什麼**

在寫分頁迴圈之前，先問「這個分頁是因為查詢範圍太大導致的，還是資料量本身就大？」如果是範圍問題，先試試能否透過增加參數縮小範圍來避免分頁，再決定是否真的需要寫迴圈。

---

### 條目 065 — upperBound 選昨天的理由，以及 TimeZoneInfo 跨平台問題

**我做了什麼**

在計算同步上界（`upperBound`）時，選擇了「台灣時間的昨天」，而不是「今天」或「明天」。實作時用 `TimeZoneInfo.FindSystemTimeZoneById()` 取得台灣時區，發現 Windows 和 Linux 的時區 ID 不同。

**我遇到的問題**

為什麼要選昨天？選今天不是更直觀嗎？另外，部署到 Linux 環境時，`"Taipei Standard Time"` 這個 ID 會拋例外。

**我怎麼想通的**

選昨天的原因是資料完整性。農業部的交易行情資料通常在台灣時間深夜才更新完整，Worker 的執行時間不固定（可能早上也可能下午），選今天代表可能拉到一個尚未更新完整的半成品資料集。昨天的資料已經過了整整一個台灣時間日曆日，更新必然已經完成。「永遠只同步已確定完整的歷史資料」比「嘗試同步今天的資料但可能不完整」要可靠得多。

TimeZoneInfo 的跨平台問題：Windows 使用 Windows 時區 ID（`"Taipei Standard Time"`），Linux / macOS 使用 IANA 時區資料庫 ID（`"Asia/Taipei"`）。這兩套命名系統完全獨立，無法互換。解法是用 `OperatingSystem.IsWindows()` 做運行環境判斷，選擇對應的 ID。這是一個在本機 Windows 開發時很難注意到的問題，因為本機永遠不會踩到 Linux 的路徑，必須在設計時就預想未來部署的可能環境。

**我學到的原則**

時間邊界的選擇不只是「幾點幾分」的問題，還包含「資料在這個時間點是否已經確定完整」。優先選擇資料確定完整的時間點，而非最新的時間點。時區處理要在設計時就考慮跨平台差異，不要等到部署出問題才補。

**下次遇到類似情況，我會先想到什麼**

使用 `TimeZoneInfo` 時，先問「這段程式碼未來可能在 Windows 以外的環境執行嗎？」如果可能，就加上環境判斷邏輯，選擇對應的時區 ID，不要只寫 Windows 版本的 ID。

---

### 條目 066 — surrogate PK 決策：MarketCode 514 問題

**我做了什麼**

`MarketInfos` 的 PK 最初設計為 `MarketCode`（業務代碼字串）。打開 Veg API 和 Flower API 的真實資料後，發現 MarketCode `514` 在兩個來源下有不同的名稱：Veg API 叫「溪湖鎮」，Flower API 叫「彰化市場」。

**我遇到的問題**

如果用 MarketCode 當 PK，同一個代碼只能存一筆，514 就只能選其中一個名稱存進去。但這兩個名稱在後續 AgriProductsTrans API 查詢時各自對應不同的資料集，少了任何一個，對應市場的行情資料就永遠無法同步。

**我怎麼想通的**

PK 的選擇取決於「什麼組合能唯一識別一筆記錄」。如果業務代碼本身不能唯一識別（514 同時存在兩個不同實體），就不能把業務代碼當 PK。

解法是引入 surrogate PK（`int IDENTITY`，系統自動遞增，無業務意義）作為資料庫層的識別鍵，同時把業務層的唯一性約束改成 `(MarketCode, MarketName)` 組合 Unique Index。這樣 514 溪湖鎮和 514 彰化市場是兩筆不同的記錄，各自有不同的 surrogate Id，但對資料庫而言都是合法存在的，不衝突。

這個決策也連帶影響了 `AgriProductsTrans` 和 `MarketInfos` 之間的關聯方式（見條目 067）。

**我學到的原則**

在選擇 PK 之前，先驗證「這個欄位在真實業務資料中真的是唯一的嗎？」光看 API 文件說是識別碼還不夠，要看真實資料。發現同一個業務代碼在不同上下文有不同語意時，放棄以業務代碼作為 PK，改用 surrogate PK + 業務欄位 Unique constraint 的組合。

**下次遇到類似情況，我會先想到什麼**

設計主檔型資料表的 PK 時，先把要選的候選鍵（candidate key）列出來，每個都用一個問題驗證：「有沒有合法的業務情況讓這個值重複出現？」通過驗證才能作為 PK，否則就改用 surrogate Id。

---

### 條目 067 — 值層面關聯：為什麼 surrogate PK 讓原本的 FK 失效，以及如何替代

**我做了什麼**

`MarketInfos` 的 PK 從 `MarketCode` 改成 surrogate `Id` 之後，原本 `AgriProductsTrans.MarketCode → MarketInfos.MarketCode` 的 FK 關係就無法維持，因為 SQL Server 的 FK 只能指向 PK 或有 Unique constraint 的欄位，而 `MarketCode` 現在只是一個普通欄位，沒有 Unique constraint（因為 514 有兩筆）。

**我遇到的問題**

如果把 FK 改成指向 surrogate Id，`AgriProductsTransSyncWorker` 在寫入每筆交易時就必須先查 `MarketInfos` 找到對應的 `Id` 數字，才能填進 `AgriProductsTrans` 的 FK 欄位。但 API 回傳的是 `MarketName` 字串，本來就不帶 surrogate Id。

**我怎麼想通的**

FK 的存在是為了讓資料庫在插入/刪除時自動保護參照完整性。但這個保護是有代價的：必須在寫入前確定被指向的記錄存在（否則 FK 違規），而且被指向記錄的刪除會受到限制（有 FK 指向它時不能隨便刪）。

在這個場景下，FK 帶來的代價（每次寫入前多一次查詢）比收益（資料庫自動保護）更高，因為完整性可以由應用程式層保證：`AgriProductsTransSyncWorker` 的市場清單本來就是從 `MarketInfos` 讀出來的，寫進去的 `MarketCode` 一定存在主檔裡，不需要資料庫再保護一次。

選擇移除導覽屬性和 `HasForeignKey`，讓 `AgriProductsTrans.MarketCode` 成為純字串值欄位，靠應用程式層的邏輯保證正確性。這是「值層面關聯」：兩張表透過值比對關聯（WHERE AgriProductsTrans.MarketCode = MarketInfos.MarketCode），沒有資料庫層的 FK constraint。

**我學到的原則**

FK 帶來的好處是資料庫自動保護參照完整性；代價是寫入時的額外查詢和刪除時的限制。當應用程式層已經能保證完整性（例如寫入資料的來源就是被指向的表），FK 的好處就幾乎消失，只剩代價。此時移除 FK，改用應用程式層的邏輯保證，是合理的取捨。

**下次遇到類似情況，我會先想到什麼**

考慮移除 FK 之前，先問「如果移除 FK，誰負責保證這個欄位的值在被指向的表裡一定存在？」如果有明確的應用程式邏輯擔起這個責任，才能移除 FK；不能移除之後就沒有人管完整性了。

---

### 條目 068 — 物理 FK 移除（同模組內也可選擇值層面關聯）：CropInfo 的雞生蛋問題

**我做了什麼**

`AgriProductsTrans` 和 `CropInfos` 是同一個模組（MarketDbContext）的兩張表，理論上可以建立物理 FK。最初我確實加了 `[ForeignKey("CropCode")] public CropInfo CropInfo` 導覽屬性。但 Worker 第一次執行就失敗了。

**我遇到的問題**

`CropInfos` 表的資料來源是 `AgriProductsTrans` 同一支 API，Worker 第一次執行時 `CropInfos` 是空的。帶著物理 FK 的 INSERT 嘗試把 `AgriProductsTrans` 記錄寫進去，但 `CropCode` 在 `CropInfos` 找不到對應記錄，FK 違規，INSERT 失敗。這是一個典型的雞生蛋問題：兩張表的資料來自同一個來源，誰先存誰都不對。

**我怎麼想通的**

物理 FK 的保護語意是「這張表的這個欄位的值，一定存在於被指向的那張表」。但在這裡，被指向的表（CropInfos）的填充本身就依賴當前這張表的資料來源（同一支 API），導致無法在插入 AgriProductsTrans 之前確保 CropInfos 已有對應記錄。

解法是移除物理 FK（執行 `RemoveCropInfoNavigation` Migration），讓 `CropCode` 成為純字串值欄位。然後在 Worker 裡建立明確的執行順序：先從這批 API 資料中抽出 CropCode，確認 CropInfos 裡還沒有的就先 Add 並 `SaveChangesAsync`，再寫入 AgriProductsTrans。這個「先存 CropInfos 再存 AgriProductsTrans」的手動順序，模擬了物理 FK 的插入保護效果，但不需要 DB 層的 FK constraint 來執行它。

這個決策和條目 067 的 MarketInfo 不同：那是因為 surrogate PK 讓 FK 的指向目標消失；這次是同模組內兩張表，理論上 FK 可行，但語意上行不通（雞生蛋）。結果都選擇了值層面關聯，但原因不同，思考路徑也不同。

**我學到的原則**

物理 FK 並不是在同模組內就一定要用的標準做法，它的適用前提是「被指向的表的資料生命週期獨立於當前表」。如果兩張表的資料來自同一個來源，就存在雞生蛋的可能，此時移除物理 FK 並用應用程式層的執行順序替代，是解決問題而不是逃避問題。

**下次遇到類似情況，我會先想到什麼**

設計 FK 時，問「被指向的表，它的資料是從哪裡來的？如果是從和當前表相同的來源，誰先存誰？」如果這個問題答不清楚，就有雞生蛋的風險，要考慮用應用程式層的執行順序替代 FK constraint。

---

### 條目 069 — 硬編碼的時機（補充視角）：獨立 SaveChanges 讓前置條件不受後續失敗影響

**我做了什麼**

在條目 059 裡記錄了「硬編碼要放在 API sync 之前」的原則。這次在實作 CropMarketSyncWorker 時，105 台北市場的硬編碼不只放在前面，還有自己獨立的 `SaveChangesAsync`，不和三支 API sync 合用同一個 Save。

**我遇到的問題**

硬編碼放在前面已經保證了順序，為什麼還需要獨立的 SaveChanges？三支 API sync 結束後統一 Save 不是更有效率嗎？

**我怎麼想通的**

如果硬編碼和三支 API sync 合用同一個 `SaveChangesAsync`，那它們就在同一個 transaction 裡。若 API sync 中途出現例外，整個 transaction 回滾，硬編碼那筆也跟著消失，就像從沒存過一樣。下次 Worker 跑起來，硬編碼還沒有寫進 DB，`AgriProductsTransSyncWorker` 找不到 105 台北市場，那個市場的行情就永遠缺失。

獨立的 `SaveChangesAsync` 確保「硬編碼這件事是一個已完成的動作，不會因為後續步驟失敗而被撤銷」。這是一個刻意的 transaction 邊界選擇：把「確保前置條件存在」和「執行主要工作」分成兩個獨立的 transaction，讓前置條件的寫入具備永久性，不依賴後續的成功。

**我學到的原則**

「放在前面」解決了順序問題，「獨立 Save」解決了持久性問題。兩個問題是不同的，需要不同的機制。真正想要的結果是「不管後續步驟成功或失敗，這筆前置資料都已經寫入 DB」，達到這個目標需要獨立的 transaction，不只是放在前面。

**下次遇到類似情況，我會先想到什麼**

當我把某個寫入操作放在其他操作之前時，額外問一個問題：「如果後續操作失敗，這個寫入還會存在嗎？」如果答案是不確定，就加上獨立的 `SaveChangesAsync`，讓它的持久性不依賴後續的成功。

---

### 條目 070 — TransQuantity int → decimal：API 文件與實際回傳的落差

**我做了什麼**

根據農業部 API 文件，`Trans_Quantity`（交易量）欄位的型別標記為整數（number without decimal）。我照著文件把 `AgriProductsTrans.TransQuantity` 設計為 `int`。Worker 跑起來之後，遇到 JSON 反序列化失敗，錯誤訊息顯示某些市場的交易量包含小數（例如 `123.5` 公斤）。

**我遇到的問題**

API 文件明確說是整數，為什麼實際回傳了小數？我是不是哪裡用錯了？

**我怎麼想通的**

農業部的 API 文件不一定反映所有市場的實際資料狀況。交易量通常是整數（公斤），但某些特殊市場或特殊作物的計量單位可能是公克或其他，換算後就可能出現小數。API 的實際回傳行為才是真實的規格，文件只是參考。

修正方式：執行 `FixTransQuantityType` Migration 把欄位改成 `decimal(8,2)`，DTO 的對應屬性也改成 `decimal`，並在 `OnModelCreating` 加上 `HasPrecision(8,2)`。把這個修正做成一個獨立的 Migration（而不是退回去改前一個 Migration），是為了讓 Migration 歷史清楚記錄「發現問題 → 修正」的時間軸，有助於日後的 code review 和問題追蹤。

**我學到的原則**

API 文件說的型別是設計起點，不是最終依據。對接任何外部 API 時，第一次實際打一次 API 拿到真實資料，確認欄位的真實型別，再設計 DTO 和 Entity。發現型別和文件不符時，修正 Entity 並建立獨立的 Migration，讓修正過程有記錄。

**下次遇到類似情況，我會先想到什麼**

看到 API 文件說某個欄位是整數或特定型別時，在實際打一次 API 確認之前，選擇更寬容的型別（`decimal` 而非 `int`，`string` 而非 `int`），避免反序列化失敗讓整個 Worker 崩潰。

---

### 條目 071 — EF Core Change Tracker 自動偵測修改：不需要顯式呼叫 .Update()

**我做了什麼**

每天迴圈結束後，更新 `SyncState.LastSyncedDate = currentDate`，然後呼叫 `dbCore.SaveChangesAsync()`。最初我以為還需要 `dbCore.SyncStates.Update(lastSyncState)` 才能讓 EF Core 知道這個 Entity 被修改了。

**我遇到的問題**

`.Update()` 到底做了什麼？如果不呼叫它，EF Core 會知道這個 Entity 被修改了嗎？

**我怎麼想通的**

EF Core 的 Change Tracker 會追蹤所有「被 DbContext 追蹤中的 Entity」的狀態。只要 `lastSyncState` 是透過 EF Core 查詢取得的（`await dbCore.SyncStates.SingleOrDefaultAsync(...)`），它就處於 Change Tracker 的追蹤下，屬於 `Unchanged` 狀態。當我修改了它的任何屬性（`lastSyncState.LastSyncedDate = currentDate`），Change Tracker 會自動偵測到這個變更，把該 Entity 的狀態改為 `Modified`。`SaveChangesAsync` 執行時，EF Core 掃描所有 `Modified` 狀態的 Entity，產生對應的 `UPDATE` SQL 並執行。

`.Update()` 的用途是「明確告訴 EF Core 這個 Entity 被修改了」，通常用在 Disconnected 場景（Entity 不是從當前 DbContext 查詢取得，而是從外部傳入，Change Tracker 不知道它的原始狀態）。在我們的 Scoped DbContext 裡，`lastSyncState` 是當場查出來的，Change Tracker 全程追蹤，顯式 `.Update()` 是多餘的——有也不錯，但沒有也完全可以。

**我學到的原則**

EF Core 的 Change Tracker 讓「查出來 → 修改屬性 → SaveChanges」這個流程可以自動運作，不需要手動通知 EF Core「我改了東西」。`.Update()` 是給 Disconnected 場景用的，在 Scoped DbContext 的正常流程裡幾乎用不到。理解這個機制可以讓程式碼更簡潔，也避免誤用 `.Update()` 在 Attached Entity 上造成意外的行為。

**下次遇到類似情況，我會先想到什麼**

看到程式碼裡有 `.Update()` 時，先問「這個 Entity 是從當前 DbContext 查出來的，還是從外部傳進來的？」如果是前者，`.Update()` 大概是多餘的；如果是後者，`.Update()` 是必要的，它讓 EF Core 知道要處理這個外部 Entity。

---

### 條目 072 — DistinctBy vs HashSet 兩層去重的職責分離

**我做了什麼**

`AgriProductsTransSyncWorker` 的去重分兩層：`DistinctBy` 處理批次內部重複，`HashSet<(DateOnly, string, string, string)>` 處理與 DB 已有記錄的重複。

**我遇到的問題**

為什麼需要兩層？能不能只用一層 HashSet 就解決所有重複？

**我怎麼想通的**

這兩種重複的性質不同，解決它們的工具也不同。

「批次內部重複」是指 API 同一次回傳的資料自身就有重複筆——不同市場、不同時間打的 API，回傳裡可能有相同的（TransDate, TcType, CropCode, MarketCode）組合。`DistinctBy` 在 incoming 資料進入去重流程之前先整理好，確保送進 DB 的每一筆在批次內是唯一的。

「歷史重複」是指 API 回傳的資料與 DB 裡已有的記錄重疊——Worker 可能因為中斷重啟而重跑某天，或者 API 重複回傳了之前已存的資料。HashSet 從 DB 查出當天的自然鍵組合，過濾掉 incoming 裡已在 DB 存在的記錄。

如果只用一層 HashSet 但不做 `DistinctBy`，批次內部的重複筆都會通過 HashSet 篩選（因為 DB 裡都沒有），最終在 INSERT 時因為 Unique Index 違規而拋例外。如果只做 `DistinctBy` 但不查 DB 建 HashSet，Worker 重啟後重跑已存在的資料，同樣在 INSERT 時 Unique Index 違規。

兩層各自有責任，混在一起反而讓職責模糊，邊界情況的 debug 也會更困難。

**我學到的原則**

去重策略要先分析「重複可能從哪裡來」，每個來源對應一種解決機制，不要把不同來源的重複混在同一個邏輯裡處理。批次內部去重用程式邏輯（DistinctBy）；歷史去重用 DB 查詢 + HashSet。兩層的順序也重要：先 DistinctBy 縮小批次，再 HashSet 過濾歷史，減少不必要的 DB 查詢量。

**下次遇到類似情況，我會先想到什麼**

設計去重時，先列出「重複可能的來源」，每個來源對應一個解決機制，按順序排列。不要試圖用一個機制同時解決不同性質的重複，那會讓邊界情況變得難以推理。

---

### 條目 073 — ValueTuple vs 匿名型別的跨方法邊界使用

**我做了什麼**

建立去重 HashSet 時，最初用匿名型別 `new { m.MarketCode, m.MarketName }`，後來改成 `ValueTuple<string, string>（m.MarketCode, m.MarketName)`。

**我遇到的問題**

條目 060 記錄了類似的問題，但這次的場景有一個不同點：匿名型別的 `HashSet` 是在同一個方法裡使用的，理論上 C# 對相同欄位組合的匿名型別有值相等性，不一定需要改成 ValueTuple。那為什麼還是應該用 ValueTuple？

**我怎麼想通的**

C# 匿名型別的值相等性是在同一個編譯單元（同一個方法或 lambda 的 scope）裡有效的——相同欄位、相同型別、相同順序的匿名型別，在同一個 scope 裡確實是同一個型別，`HashSet.Contains()` 可以正常比對。

問題出在「跨方法邊界」時。如果把 `HashSet<匿名型別>` 作為參數傳給另一個方法，目標方法無法宣告這個參數的型別（匿名型別沒有名字），只能宣告成 `object` 或 `dynamic`，失去型別安全。如果用 LINQ 的 `Where` 搭配外部 HashSet，型別推斷在某些情況下也可能失效。

更重要的是可讀性和維護性：`HashSet<(string MarketCode, string MarketName)>` 比 `HashSet<匿名型別>` 更清楚地表達了「我在追蹤什麼」。ValueTuple 支援具名元素（`(string MarketCode, string MarketName)`），讀起來像文件一樣直白，匿名型別沒有辦法在外部被引用和命名。

條目 060 的核心是「匿名型別在 HashSet 裡的值相等性失效」，這個條目補充的是「即使值相等性沒問題，匿名型別也不適合用在需要跨方法邊界傳遞或需要明確型別宣告的場景」。兩個條目合在一起才是完整的判斷框架。

**我學到的原則**

匿名型別適合在 LINQ 查詢的同一個 scope 內使用（臨時的 projection，不需要跨邊界）。需要讓型別可引用、可命名、可跨方法傳遞的場景，選 ValueTuple（輕量、值相等性、可具名）或具名 record（更語意豐富、適合複雜的情況）。

**下次遇到類似情況，我會先想到什麼**

看到匿名型別的 `new { ... }` 時，問兩個問題：「我需要在 HashSet.Contains 裡用它做值比對嗎？」以及「我需要把這個型別傳遞到這個 scope 之外嗎？」任何一個答案是「是」，就改成 ValueTuple 或具名 record。

---

### 條目 074 — Round-trip 的本質：每次 DB 操作都是一趟「去超市」

**我做了什麼**

發現 `AgriProductsTransSyncWorker` 在實際執行 8 小時後只同步了約 1 年 2 個月的資料，開始分析效能瓶頸。

**我遇到的問題**

「Round-trip」這個詞是什麼意思？為什麼說 4,500 次迴圈裡有「數千次 Round-trip」是問題？

**我怎麼想通的**

每一次 `ToListAsync()` 或 `SaveChangesAsync()` 都是一趟完整的網路往返：開啟連線 → 產生 SQL → 透過網路送到 DB Server → DB 執行 → 結果透過網路回來 → 關閉連線。每一趟都有固定的等待成本，跟查多少筆資料無關。

用超市比喻：買 10 樣東西，如果「去超市 → 買一樣 → 回家 → 去超市 → 買一樣 → 回家」重複 10 次，大部分時間都在路上，不在「買東西」本身。DB 的 Round-trip 和這個完全相同——4,500 次迴圈裡，程式大部分時間都在等待網路 I/O，CPU 幾乎沒在做有意義的運算。

原始實作的問題不是邏輯錯誤，而是**把不需要放在迴圈裡的 DB 操作，每圈都重複執行一次**。

**我學到的原則**

評估程式效能時，先數「這段程式碼裡有幾次 DB 操作」，再問「哪些操作的結果在迴圈裡是不變的」。不變的操作移到迴圈外，只執行一次，在迴圈裡查記憶體。

**下次遇到類似情況，我會先想到什麼**

看到巢狀迴圈裡有 `await` 的 DB 操作，先問：「這個查詢的結果，在這一層迴圈裡會改變嗎？」不會改變的就移出去。

---

### 條目 075 — Task.WhenAll：從串行等待到並發等待

**我做了什麼**

把市場迴圈的 API 請求從串行的 `foreach` 改成 `Task.WhenAll`，讓同一天的所有市場 API 同時發出。

**我遇到的問題**

`Task.WhenAll` 是什麼？它跟普通的 `foreach + await` 有什麼不同？它回傳的型別是什麼？

**我怎麼想通的**

串行的 `foreach + await`：打電話給市場 A → 等 A 接聽回答 → 掛電話 → 打給市場 B → 等 B 接聽回答 → 掛電話 → …50 個市場全部加總。大部分時間都在等對方接聽，你自己沒在做任何事。

`Task.WhenAll`：同時把 50 支電話全部撥出去，等所有人都回答完。總等待時間 ≈ 最慢那一個市場的時間，而不是全部加總。

`Task.WhenAll` 接收一組 Task，等全部完成後回傳一個陣列。如果每個 Task 回傳 `T`，`WhenAll` 就給你 `T[]`：

```csharp
// 每個 Task 回傳 (Market, Json, Success) 三元組
var rawResults = await Task.WhenAll(marketInfos.Select(async market =>
{
    var json = await _httpClient.GetStringAsync(url, stoppingToken);
    return (Market: market, Json: json, Success: true);
}));
// rawResults 的型別是 (MarketInfo, string, bool)[]
```

foreach 時，用 Tuple 解構（Tuple Deconstruction）把每個元素拆開成具名變數：

```csharp
foreach (var (market, json, success) in rawResults) { ... }
```

**我學到的原則**

非同步的優勢不在於「不需要等待」，而在於「可以同時等待多件事」。`Task.WhenAll` 是讓多個獨立的等待操作並發進行的最直接工具。適用條件：各個 Task 彼此獨立，不需要等前一個完成才能開始下一個。

**下次遇到類似情況，我會先想到什麼**

看到 `foreach` 裡面有 `await HttpClient` 或其他網路 I/O，先問「這些請求彼此獨立嗎？」獨立的話，就考慮 `Task.WhenAll`。

---

### 條目 076 — Thread Safety 與 TOCTOU：為什麼換成 ConcurrentDictionary 還不夠

**我做了什麼**

在評估 `Task.WhenAll` 的實作方案時，考慮過把 `HashSet<string>` 換成 `ConcurrentDictionary<string, byte>` 讓資料處理也在 Task 內部併發進行，最後否定了這個方案。

**我遇到的問題**

什麼是執行緒安全（Thread Safety）？`ConcurrentDictionary` 怎麼解決寫入衝突？它為什麼還是不夠？

**我怎麼想通的**

`List<T>` 和 `HashSet<T>` 不是執行緒安全的——如果 Task A 和 Task B 同時對同一個 `List` 呼叫 `Add()`，它們可能同時讀取「目前有幾筆」的計數器，各自計算出相同的插入位置，然後互蓋，導致資料損毀或遺失，而不是引發例外。這個問題的根本在於「讀取 + 計算 + 寫入」這三個步驟不是原子的（atomic），可以被其他執行緒插入。

`ConcurrentDictionary` 內部用鎖（Lock）機制讓寫入變成原子操作——當 Task A 在 `TryAdd` 時，Task B 的 `TryAdd` 必須排隊等待，確保一次只有一個 Task 在修改資料結構。語法對比：

```csharp
// 原本的 HashSet<string>（非執行緒安全）
existingCropCodeSet.Add("A001");
existingCropCodeSet.Contains("A001");

// ConcurrentDictionary<string, byte>（執行緒安全，用 Dictionary 模擬 HashSet）
existingCropCodeSet.TryAdd("A001", 0); // byte 值 0 只是佔位，無意義
existingCropCodeSet.ContainsKey("A001");
```

但 `ConcurrentDictionary` 只解決了「Add 本身」的衝突，沒有解決 **TOCTOU（Time of Check to Time of Use）** 問題：

```csharp
if (!existingCropCodeSet.ContainsKey(x.CropCode)) // Task A 檢查「不存在」
{
    // ← Task B 也同時通過了這個檢查，因為 A 還沒 Add
    existingCropCodeSet.TryAdd(x.CropCode, 0); // 兩個都 Add，CropInfo 重複寫入
}
```

「檢查是否存在」和「Add」是兩個分開的操作，即使每個操作本身是原子的，兩個操作合起來仍然不是原子的，中間可以插入其他執行緒。真正的解法是用 `GetOrAdd` 這類原子的「不存在就新增」操作，但這樣程式碼的複雜度會大幅提升，而且還要處理 `List<CropInfo>` → `ConcurrentBag<CropInfo>` 等多個集合的替換。

最終選擇的方案：**Task 只負責打 API，所有有狀態的操作回到主執行緒依序處理**。這個方案的優雅之處在於：真正需要並發的部分（API 等待 I/O）才並發，快速的記憶體操作（資料處理）依序執行，完全規避了執行緒安全的複雜性，也沒有任何效能損失。

**我學到的原則**

TOCTOU 是一個常見的並發錯誤模式：「先檢查（Check），再使用（Use）」之間如果有其他執行緒插入，檢查的結果就失效了。換成執行緒安全的集合型別，只解決了「Use」部分的衝突，沒有解決「Check 到 Use 之間的間隙」。需要把整個「Check + Use」變成原子操作才能真正解決。

**下次遇到類似情況，我會先想到什麼**

看到「先查存不存在，再根據結果決定要不要寫入」的模式，就要想「這段邏輯在多執行緒環境下是 TOCTOU 嗎？」如果是，要麼找原子操作（如 `GetOrAdd`），要麼讓這段邏輯在單一執行緒裡跑。

---

### 條目 077 — EF Core 不能在 Select 裡用 ValueTuple：SQL 翻譯的邊界

**我做了什麼**

在建立 `existingKeySet` 時，嘗試直接在 EF Core 的 `Select` 裡投影成 ValueTuple，發現不可行，改成兩步驟：先用匿名型別查 DB，資料進記憶體後再轉成 ValueTuple。

**我遇到的問題**

為什麼不能直接 `.Select(x => (x.TransDate, x.CropCode, x.MarketCode, x.TcType)).ToHashSetAsync()`？

**我怎麼想通的**

EF Core 的 LINQ 查詢在 `ToListAsync()` / `ToHashSetAsync()` 執行之前，都還沒有真正打到資料庫——EF Core 在這段時間持有一個「查詢表達式」，等到執行時才把它翻譯成 SQL 送給 DB。翻譯的過程中，EF Core 必須認識每一個 C# 表達式並找到對應的 SQL 語法。

`new { ... }` 匿名型別是 EF Core 認識的投影方式，它會翻譯成 `SELECT column1, column2, ...`。但 `(x.A, x.B)` ValueTuple 不是 SQL 認識的概念，EF Core 無法翻譯，在執行期報錯。

解法是利用兩個不同的執行環境：

```csharp
var existingKeySet = (await dbMarket.AgriProductsTrans
    .AsNoTracking()
    .Where(x => x.TransDate == currentDate)
    .Select(x => new { x.TransDate, x.CropCode, x.MarketCode, x.TcType }) // EF Core 翻譯成 SQL
    .ToListAsync(stoppingToken))                // ← 這一行之後，資料進入記憶體，離開 EF Core 的管轄
    .Select(x => (x.TransDate, x.CropCode, x.MarketCode, x.TcType))       // 純 C# LINQ，不需要翻譯成 SQL
    .ToHashSet();                               // 不是 ToHashSetAsync，因為不碰 DB
```

`ToListAsync()` 是關鍵的邊界——它之前是「DB 查詢模式」，EF Core 負責翻譯；它之後是「記憶體操作模式」，任何合法的 C# 語法都可以用。

**我學到的原則**

EF Core LINQ 和普通 LINQ 看起來一樣，但背後的執行環境完全不同。EF Core LINQ 的每個操作都必須能翻譯成 SQL；普通 C# LINQ 在記憶體裡跑，任何 C# 語法都合法。分界點是 `ToListAsync()` / `FirstOrDefaultAsync()` 等「執行 DB 查詢」的方法。

**下次遇到類似情況，我會先想到什麼**

EF Core 查詢報「could not be translated」錯誤時，先看是不是在 `ToListAsync()` 之前用了 DB 不認識的 C# 表達式（ValueTuple、自訂方法、複雜的 C# 運算）。解法是先執行查詢把資料帶進記憶體，再用普通 LINQ 處理。

---

### 條目 078 — 效能優化的診斷方法：先量再改，從最大的浪費開始

**我做了什麼**

對 `AgriProductsTransSyncWorker` 進行系統性效能診斷，找出瓶頸並按照依賴關係排定優化順序（D → B → C → A）。

**我遇到的問題**

發現跑很慢了，但怎麼知道要先改哪個？優化的順序重要嗎？

**我怎麼想通的**

先算出問題的規模：90 天 × 50 個市場 = 4,500 次迴圈，每圈有 1 次 HTTP + 2 次 DB 查詢 + 1 次 SaveChanges。4,500 次 HTTP 是串行等待，這是最大的浪費；4,500 次 DB 查詢裡，大部分都是查同樣的東西（CropInfo 全量、existingKeys 當天同一份資料）。

排定優化順序時，考慮了三個維度：是否獨立（不影響其他邏輯）、是否結構性（影響整體架構）、是否高風險（容易出 Bug）。獨立的優先做（`AsNoTracking`），高風險的最後做（`Task.WhenAll`），中間的按照「後者依賴前者穩定」的順序排列。

這次的順序：D（加一行，獨立）→ B（移動查詢位置，低風險）→ C（改變迴圈結構，中風險）→ A（引入並發，高風險）。C 要先於 A，因為 A 的設計（Task 只打 API）是建立在 C（SaveChanges 移出迴圈）穩定之後才有意義的。

**我學到的原則**

效能優化的正確順序是：量化問題規模 → 找出最大的浪費 → 從獨立的改動開始，逐步推進到結構性的改動。每次改一個維度，確認後再改下一個，這樣出問題時能精確定位是哪個改動引入的。不要一次改太多。

**下次遇到類似情況，我會先想到什麼**

發現效能問題，第一步不是動程式碼，而是先算「這個操作一共執行了幾次？每次的成本是多少？」有了數字才知道優化哪裡的收益最大。

---

### 條目 079 — 設計決策的影響範圍追蹤：從主檔層面的一對多到交易資料層面的重複（補完版）
 
**我做了什麼**
 
`AgriProductsTransSyncWorker` 跑歷史資料時，在 `2019-11-04` 拋出 `DbUpdateException`，錯誤訊息指向 `market.AgriProductsTrans` 的 Unique Index 被重複寫入違反。程式碼裡已有兩層去重邏輯（`DistinctBy` + `existingKeySet`），卻仍然出現重複，診斷後發現是跨市場查詢的資料在合併前沒有去重。
 
**我遇到的問題**
 
PR #014 已經知道 MarketCode 514 在 MarketInfos 有兩筆（溪湖鎮和彰化市場），這個事實早就記錄在設計文件裡了。為什麼到了交易資料同步時才踩到問題？而且程式碼明明有兩層去重，為什麼都攔不住？
 
**我怎麼想通的**
 
**第一步：拉出完整的失敗鏈**
 
PR #014 的思考停在「主檔允許一個 MarketCode 有兩個 MarketName 並存」，沒有繼續往下推演：
 
1. MarketInfos 有兩筆：514 溪湖鎮（Veg）、514 彰化市場（Flower）
2. `Task.WhenAll` 以兩個不同 MarketName 各打一次 API
3. 農業部回傳的交易資料欄位是 `MarketCode`（514），不是查詢時用的 MarketName
4. 兩次 API 可能回傳完全相同的自然鍵組合 `(TransDate, TcType, CropCode, MarketCode=514)`
5. 每個市場的 `DistinctBy` 只看自己的 `incoming`，跨市場的重複沒有被攔截
6. `existingKeySet` 查的是 DB 的狀態，Change Tracker 裡已 Add 但還未 SaveChanges 的資料完全不可見
7. `SaveChangesAsync` 時，兩筆相同自然鍵同時送進 DB，Unique Index 拋例外
 
**第二步：理解為什麼兩層去重都失效**
 
這是這次最關鍵的診斷：
 
```
第一層 DistinctBy：
  市場A的 incoming → DistinctBy（只在自己範圍內去重）→ AddRange
  市場B的 incoming → DistinctBy（只在自己範圍內去重）→ AddRange
  ↑ 兩者之間完全沒有比對過
 
第二層 existingKeySet：
  在 foreach 開始前查 DB → 建立 HashSet（記錄 DB 裡已有的）
  市場A AddRange 進 Change Tracker → DB 還沒變，existingKeySet 不知道
  市場B 比對 existingKeySet → 查到「不存在」（因為A的資料還在 Change Tracker，不在 DB）
  → 兩筆都進了 Change Tracker
  → SaveChanges → Unique Index 違規
```
 
核心問題是：`existingKeySet` 是快照，記錄的是「SaveChanges 之前 DB 的狀態」。PR #016 把 `SaveChangesAsync` 移出市場迴圈之後，Change Tracker 裡累積了整天所有市場的資料，但 `existingKeySet` 對這些「尚未存進 DB 的資料」完全不可見，跨市場的重複就從這個盲區漏過去了。
 
**第三步：修正——讓 DistinctBy 的作用範圍覆蓋所有市場**
 
```csharp
// 修正前：每個市場各自去重，跨市場重複無法被攔截
foreach (var (market, json, success) in rawResults)
{
    var incoming = response.Data
        .Where(x => x.CropCode != "-")
        .DistinctBy(x => new { x.TransDate, x.CropCode, x.MarketCode, x.TcType })
        .ToList();
    dbMarket.AgriProductsTrans.AddRange(saveData); // 跨市場重複直接衝突
}
 
// 修正後：先收集全部，合併後再統一去重
var allIncoming = new List<AgriProductsTransTypeDto>();
foreach (var (market, json, success) in rawResults)
{
    // 只收集，不去重
    var incoming = response.Data
        .Where(x => x.CropCode != "-")
        .ToList();
    allIncoming.AddRange(incoming);
}
 
// foreach 結束後，合併所有市場的資料再統一去重
var targetData = allIncoming
    .DistinctBy(x => new { x.TransDate, x.CropCode, x.MarketCode, x.TcType })
    .ToList();
```
 
`DistinctBy` 只去掉**完全相同自然鍵**的記錄。如果兩個市場查詢回傳的 MarketCode 相同、但 CropCode 或 TcType 不同，這些是不同的交易紀錄，`DistinctBy` 不會去掉，兩筆都正確保留。
 
**第四步：這個問題為什麼只在大規模資料時才出現**
 
MarketCode 514 同時有兩個名稱，需要同時滿足三個條件才能觸發：
- 當天 514 市場有實際交易資料（不是休市日）
- 兩個 MarketName 查詢都回傳了相同的 MarketCode=514 的記錄
- 那筆記錄的完整自然鍵（TransDate, TcType, CropCode, MarketCode）完全相同
 
小規模手動測試很難同時滿足這三個條件，只有在補跑多年歷史資料時才有足夠的樣本量讓這個邊界情況被觸發。
 
**我學到的原則**
 
**原則一：設計決策的影響範圍追蹤需要跨越系統層次。**
「主檔允許一對多」這個決策不只影響主檔表的結構，也影響任何用這份主檔驅動查詢的下游 Worker。做出影響資料來源結構的設計決策時，應該同時問：「所有以這份資料為輸入的 Worker，它們的去重邏輯還成立嗎？」
 
**原則二：Change Tracker 的可見範圍是診斷並發寫入問題的關鍵。**
`existingKeySet` 是 DB 的快照，對 Change Tracker 裡尚未 SaveChanges 的資料不可見。一旦 SaveChanges 被移出迴圈（批次化），Change Tracker 就會累積多個市場的資料，任何依賴「查 DB 做去重」的邏輯都可能產生盲區。解法是讓去重在 Change Tracker 寫入之前完成，而不是依賴 DB 查詢來攔截。
 
**原則三：邊界情況的觸發需要特定的資料組合。**
這類問題無法靠早期的小樣本測試完全防止，但可以在設計決策時就把影響鏈往下拉一層，盡量縮小後來的盲區。
 
**下次遇到類似情況，我會先想到什麼**
 
做出「允許同一業務代碼有多筆記錄」的設計決策之後，立刻問：「哪些下游 Worker 會以這份主檔為清單逐筆打 API？它們回傳的資料欄位是業務代碼還是查詢用的名稱？如果是業務代碼，跨筆查詢的結果需要合併去重嗎？」
 
遇到「兩層去重都失效」的 Unique Index 違規時，先問：「我的去重是在 Change Tracker 寫入之前還是之後做的？有沒有資料已經在 Change Tracker 但還沒進 DB，被我的去重邏輯的盲區漏掉了？」

---

### 條目 080 — EF Core nullable 欄位 UNIQUE index 的隱性 filter 與 `HasFilter(null)`

**我做了什麼**

為 `DebrisAlertRecord` 的自然鍵 `(ReportID, DebrisNo, LandslideID)` 建立 UNIQUE index，其中 `DebrisNo` 和 `LandslideID` 是 nullable 欄位。Migration 產生後發現 EF Core 自動加上了 filter，導致去重完全失效，最終用 `.HasFilter(null)` 解決。

**我遇到的問題**

Migration 產生的 index 長這樣：

```sql
CREATE UNIQUE INDEX [IX_DebrisAlertRecords_ReportID_DebrisNo_LandslideID]
ON [market].[DebrisAlertRecords] ([ReportID], [DebrisNo], [LandslideID])
WHERE [DebrisNo] IS NOT NULL AND [LandslideID] IS NOT NULL;
```

`AND` 的語意是「只有當兩個欄位都不是 null 時，這個 UNIQUE index 才生效」。但資料的結構是 AlertType D 的 `LandslideID` 必為 null，AlertType L 的 `DebrisNo` 必為 null——也就是說，每一筆資料都有一個欄位是 null，filter 的條件**永遠不成立**，UNIQUE index 對任何資料都不生效，去重保護等同沒有。

嘗試改成 `OR`，SQL Server 直接報 `Incorrect syntax near the keyword 'OR'`——SQL Server 的 filtered index 不支援 `OR` 語法。

**我怎麼想通的**

EF Core 這個預設行為的出發點是「保護 null 相等問題」：SQL Server 在 UNIQUE index 中把兩個 `null` 視為相等，如果允許多筆記錄的 nullable 欄位都是 null，理論上可能造成意外的唯一性衝突。EF Core 的解法是加上 filter，只讓有值的記錄參與唯一性判斷。

但這個「保護」在這個場景下反而壞事，因為業務上 `DebrisNo` 和 `LandslideID` 不可能同時為 null（D 型和 L 型必定各有一個有值），根本不會有「兩個 null 衝突」的情況發生。所以正確的做法是明確告訴 EF Core「不要加任何 filter」：

```csharp
entity.HasIndex(e => new { e.ReportID, e.DebrisNo, e.LandslideID })
      .HasDatabaseName("IX_DebrisAlertRecords_ReportID_DebrisNo_LandslideID")
      .HasFilter(null)   // 強制覆蓋 EF Core 預設的 nullable filter
      .IsUnique();
```

`.HasFilter(null)` 代表「不加任何 WHERE 條件」，讓 UNIQUE index 對每一筆資料都生效。這個設定寫在 `OnModelCreating` 裡，之後無論重新跑幾次 Migration，都不需要手動編輯產生的 SQL。

**我學到的原則**

EF Core 對含 nullable 欄位的 UNIQUE index 有一個非顯而易見的預設行為——自動加上 `WHERE field IS NOT NULL` 的 filter。這個行為在「允許多個 null 並存」的場景下有存在意義，但在「null 是互斥佔位符（只有其中一個會是 null）」的場景下會讓 UNIQUE index 完全失效。遇到這個問題，先確認業務邏輯上 null 的語意，再決定要讓 EF Core 加 filter 還是用 `HasFilter(null)` 關掉。

**下次遇到類似情況，我會先想到什麼**

含 nullable 欄位的 UNIQUE index Migration 跑完後，立刻打開產生的檔案確認有沒有 `filter` 參數。如果有，想一下「這個 filter 的條件，對每一筆正常資料，會成立嗎？」如果不成立，就用 `HasFilter(null)` 關掉。

---

### 條目 081 — 全量 API 的同步策略：什麼情況不需要 SyncState

**我做了什麼**

設計 `DebrisAlertRecordSyncWorker` 的同步策略，確認這支 Worker 不需要 `SyncState`，和 `AgriProductsTransSyncWorker` 的設計做了明確的比較。

**我遇到的問題**

`AgriProductsTrans` 需要 `SyncState`，`DebrisAlertRecord` 不需要，但兩者都是「同步外部 API 資料」的 Worker，差別到底在哪裡？

**我怎麼想通的**

關鍵差異在於**API 的查詢能力**和**同步邏輯的冪等性**：

`AgriProductsTrans` 的 API 支援 `Start_time` 和 `End_time` 參數，可以只拉「特定日期」的資料。正因為如此，每次只拉一小段，就必須記錄「上次拉到哪裡」，否則不知道下次從哪裡繼續。這是 SyncState 存在的根本原因：**有能力增量，才需要追蹤進度**。

`DebrisAlertRecord` 的 API 沒有任何日期篩選參數，每次都回傳全部歷史記錄。因此，同步邏輯設計成：全量拉取 → 與 DB 現有資料比對（HashSet）→ 只 INSERT 新的。這個設計本身就是冪等的：不管執行幾次，結果都一樣，重複執行只是多做了一次比對，不會產生重複或遺漏。冪等性保證了不需要「記住上次的狀態」——**因為每次都是從全量出發，從不假設上次停在哪裡**。

一句話總結：SyncState 解決的是「我只知道一部分資料，需要追蹤從哪裡繼續」的問題。如果每次都能拿到全量資料，就不存在這個問題，SyncState 是不必要的複雜度。

**我學到的原則**

設計 SyncWorker 之前，先問一個問題：「API 的查詢能力，決定了我能不能做增量同步？」能增量就設計增量 + SyncState；只能全量就設計全量 + 冪等比對，不需要 SyncState。把 SyncState 當成必要配件是錯誤的前提——它只在有增量查詢能力時才有意義。

**下次遇到類似情況，我會先想到什麼**

遇到新的 API，先確認：「這支 API 有沒有日期或游標參數讓我做增量查詢？」有的話，設計增量 + SyncState；沒有的話，設計全量 + HashSet 比對，不引入 SyncState。

---

### 條目 082 — 不依賴上游資料永久性：INSERT-only 優於 TRUNCATE 的深層理由

**我做了什麼**

選擇「全量拉取 + 只 INSERT 新資料」而非「TRUNCATE + 全部重寫」作為 `DebrisAlertRecordSyncWorker` 的同步策略，分析了兩個獨立的理由。

**我遇到的問題**

TRUNCATE + 全部重寫看起來更簡單——不需要比對，直接清空再寫，程式碼更少。為什麼不用？

**我怎麼想通的**

TRUNCATE + 重寫有兩個問題，而且是性質完全不同的兩個問題：

**第一個問題是效能**。TRUNCATE + 重寫的成本和資料量線性相關。現在這支 API 回傳約 3 MB，可能對應幾千筆記錄。三年後，記錄可能是幾萬筆。TRUNCATE + 重寫每次都要刪掉全部再全部插入，I/O 成本隨時間增長。INSERT-only 每次只做一次全量拉取和一次 HashSet 比對，真正寫入的只有新記錄（增量），不受歷史資料量影響。

**第二個問題是資料可靠性**，而且這個更根本。農業部有可能在未來清理舊的警戒記錄——無論是技術原因還是政策原因，都不在我們的控制範圍內。一旦 TRUNCATE 之後，上游的舊資料消失了，本地資料庫也跟著清空，那份歷史記錄永久遺失，而且沒有任何補救方式。INSERT-only 讓本地資料庫成為上游資料的**持久化備份**：即使上游刪了，本地記錄依然在，因為我們從不主動刪除已有的資料。

這個原則換一種說法：**只有在可以完全信任上游資料源的永久性時，TRUNCATE + 重寫才是安全的**。現實世界的外部 API 幾乎都不能被完全信任，所以 INSERT-only 應該是預設選擇，TRUNCATE 是例外。

**我學到的原則**

對外部資料源的同步，預設用 INSERT-only（只增不刪），除非有非常明確的理由需要清除本地資料。理由是：本地資料庫不只是快取，它是上游資料的持久化記錄；一旦 TRUNCATE，歷史就永遠消失了。TRUNCATE + 重寫適合的場景是「資料本身就沒有歷史意義，每次都是全新的狀態」（例如即時氣象快照），不適合有歷史價值的記錄型資料。

**下次遇到類似情況，我會先想到什麼**

設計同步策略時問：「這份資料有歷史價值嗎？上游有沒有可能在未來刪除舊資料？」兩個問題只要有一個答案是「是」，就選 INSERT-only。

---

### 條目 083 — C# 匿名型別的值相等性：HashSet.Contains 的比對機制

**我做了什麼**

在 `DebrisAlertRecordSyncWorker` 的去重邏輯裡，用匿名型別的 HashSet 做歷史比對，確認 `existingRecords.Contains(new { e.ReportID, e.DebrisNo, e.LandslideID })` 的語意是值比較，而不是參考比較。

**我遇到的問題**

`HashSet` 預設用參考相等判斷重複——兩個物件是不是同一個記憶體位置。但去重邏輯需要的是「只要欄位值相同，就視為相同」的值相等。用 `DebrisAlertRecord` Entity 做 HashSet 會有問題，因為兩個不同的 `new DebrisAlertRecord { ReportID = "115A-3-0" }` 內容一樣，但參考不同，HashSet 會認為是兩筆不同的記錄。為什麼換成匿名型別就沒有這個問題？

**我怎麼想通的**

C# 的匿名型別（`new { A = x, B = y }`）有一個特殊設計：編譯器會自動為它生成 `Equals` 和 `GetHashCode` 的實作，比的是**所有屬性的值**，而不是記憶體位置。這是「值相等（value equality）」，和普通 class 的預設「參考相等（reference equality）」完全不同。

所以：

```csharp
// 兩個不同的 new DebrisAlertRecord，即使內容相同，HashSet 認為不同（參考相等）
var set = new HashSet<DebrisAlertRecord>();
set.Add(new DebrisAlertRecord { ReportID = "A" }); 
set.Contains(new DebrisAlertRecord { ReportID = "A" }); // false

// 兩個不同的匿名型別 new { }，只要屬性值相同，HashSet 認為相同（值相等）
var set2 = records.Select(r => new { r.ReportID, r.DebrisNo, r.LandslideID }).ToHashSet();
set2.Contains(new { ReportID = "A", DebrisNo = "B", LandslideID = (string?)null }); // true（只要值相同）
```

這也是為什麼 `existingRecords` 要用 `.Select(r => new { r.ReportID, r.DebrisNo, r.LandslideID }).ToHashSet()` 而不是 `.ToHashSet()`——後者是 `HashSet<DebrisAlertRecord>`，參考相等，`Contains` 永遠是 false（因為查詢出來的物件和你 `new` 出來的是不同記憶體位置）；前者是 `HashSet<匿名型別>`，值相等，`Contains` 比的是欄位值。

**我學到的原則**

在 EF Core 查詢的 `Select` 後面用匿名型別建立 HashSet，是一個利用「匿名型別值相等性」做應用層去重的標準模式。需要「多個欄位組合的唯一性比對」時，匿名型別比自己實作 `IEqualityComparer` 更簡潔，也比 ValueTuple 更適合在 EF Core 的 LINQ 查詢內使用（ValueTuple 無法被 EF Core 翻譯成 SQL，必須在 `ToListAsync()` 之後才能用）。

**下次遇到類似情況，我會先想到什麼**

需要「用多個欄位組合做去重比對」時，先選匿名型別的 HashSet，不用自己實作 `IEqualityComparer`。記得：匿名型別在 `ToListAsync()` 之前（EF Core LINQ 範圍內）可以用於 `Select` 投影；`ToListAsync()` 之後（記憶體範圍）才能轉成 ValueTuple。

---

### 條目 084 — `DateTime.Parse` 的 `InvariantCulture`：主動消除跨平台解析風險

**我做了什麼**

在 `MapToEntity` 中解析 `LastUpdateDate`（格式為 `"2026-04-04 15:26"`），從最初的 `DateTime.Parse(dto.LastUpdateDate)` 改成明確指定 `CultureInfo.InvariantCulture`。

**我遇到的問題**

`DateTime.Parse` 不指定 Culture 的時候，為什麼有風險？`"2026-04-04 15:26"` 這個格式看起來很標準，難道不同環境會解析出不同結果？

**我怎麼想通的**

`DateTime.Parse` 在不指定 Culture 時，使用的是系統目前的 `CurrentCulture`。不同地區設定的系統，對「日期分隔符」、「年月日順序」、「時間格式」的預設解讀可能不同。

以 `"2026-04-04 15:26"` 為例，在大部分環境下這個格式確實能正確解析（年-月-日是 ISO 8601 標準，廣泛支援）。但這是「運氣好」而不是「設計保證」。一旦 Worker 部署到地區設定不同的 Linux 容器，或者未來遇到格式稍微不同的 API 回傳（例如 `"04/04/2026 15:26"`），沒有指定 Culture 的 `DateTime.Parse` 就可能悄悄解析成錯誤的日期，而且不拋例外，是一種 silent failure。

`CultureInfo.InvariantCulture` 是「與地區無關的固定格式」，解析行為不受作業系統設定影響。這和 PR #015 中用 `OperatingSystem.IsWindows()` 判斷時區 ID 的出發點一樣——**主動消除隱性的環境依賴，不讓程式的正確性依賴部署環境的特定設定**。

```csharp
// 不穩定：依賴系統 CurrentCulture，跨環境行為不一致
LastUpdateDate = DateTime.Parse(dto.LastUpdateDate)

// 穩定：InvariantCulture 確保任何環境都用同一套解析規則
LastUpdateDate = DateTime.Parse(dto.LastUpdateDate, System.Globalization.CultureInfo.InvariantCulture)
```

**我學到的原則**

解析外部資料的日期字串，永遠明確指定 `CultureInfo.InvariantCulture`（或用 `DateTime.ParseExact` 指定格式字串），不依賴系統 `CurrentCulture`。這行程式碼的差異很小，但它讓程式的行為從「在我的機器上測試是對的」升級到「在任何環境部署都保證正確」。這是一個一次養成、終身受益的習慣。

**下次遇到類似情況，我會先想到什麼**

看到 `DateTime.Parse(someString)` 沒有第二個參數，就問：「這個解析行為是否依賴 CurrentCulture？」如果字串來自外部（API、檔案、資料庫字串欄位），一律加上 `CultureInfo.InvariantCulture` 或用 `DateTime.ParseExact` 指定格式。

---

### 條目 085 — `.All(char.IsDigit)` 先驗後 Parse：消除 TryParse 的必要性

**我做了什麼**

實作 `ParseRocNumericDate`，處理 `PorkTransType` API 的 `"1040706"` 格式（YYYMMDD 純數字，無分隔符），和現有 `ParseRocDate` 的點分隔格式完全不同，需要新的解析方法。

**我遇到的問題**

最初的設計思路是「先對整條字串 `int.TryParse`，得到數字後再切分三段」。這個思路為什麼行不通？

`int.TryParse("1040706", out int result)` 成功執行後，`result` 是整數 `1040706`。整數沒有 `[0..3]` 這種範圍切片操作——切片是字串的語法，不是整數的語法。想切分年月日，必須在字串層面操作，解析成整數只能在切片之後做。

**我怎麼想通的**

正確的順序是：先在字串上切片，再對每一段分別做數字驗證。驗證的工具有兩種選擇：`int.TryParse`（切分後對每段分別驗）或 `.All(char.IsDigit)`（在切分前對整條字串一次驗完）。

選擇 `.All(char.IsDigit)` 的原因是它把驗證提升到「入口守衛」的位置：只要整條字串通過這個檢查，後面的每一段 `int.Parse` 就絕對不會拋例外，不需要 `TryParse`。這讓方法的結構更扁平——驗證和解析是兩個清晰分開的步驟，沒有混在一起的 `out` 參數和分支判斷。

最後一步用 `DateOnly.TryParseExact` 做業務合法性驗證（月份 1-12、日期是否存在、含閏年），用回傳值而非例外控制流程。整個方法只有一種失敗出口：`throw ArgumentException`，呼叫端不需要處理不同型別的例外。

**我學到的原則**

解析外部字串時，先問「我能不能在入口一次排除所有非法輸入」。`.All(char.IsDigit)` 是一個成本極低但防禦效果很強的守衛：通過之後，所有後續的 `int.Parse` 都有絕對保障。把驗證集中在入口，比在每一個解析步驟裡分別防禦更清晰，也更容易測試。

**下次遇到類似情況，我會先想到什麼**

看到「解析固定格式的純數字字串」需求，先考慮 `.All(char.IsDigit)` + `int.Parse`，而不是 `int.TryParse` 分支。TryParse 適合「不知道輸入是不是數字」的情況，但如果入口守衛已經保證了輸入全是數字，TryParse 的 `bool` 回傳就是多餘的複雜度。

---

### 條目 086 — `catch` 型別精確性：裸 `catch` vs `catch(ArgumentOutOfRangeException)`

**我做了什麼**

在 `ParseRocNumericDate` 的第一個草稿版本裡，用裸 `catch` 攔截 `new DateOnly(year, month, day)` 可能拋出的例外，後來改成 `catch (ArgumentOutOfRangeException)` 再改成完全不用 `try/catch`，改用 `DateOnly.TryParseExact`。

**我遇到的問題**

裸 `catch`（不指定例外型別）的問題是什麼？

`new DateOnly(year, month, day)` 在月份或日期非法時只會拋 `ArgumentOutOfRangeException`。但裸 `catch` 會攔截所有型別的例外，包含 `NullReferenceException`、`OutOfMemoryException`，甚至是你自己的程式碼 bug 觸發的任何例外。這樣所有的失敗都被轉換成同一條錯誤訊息「無效的日期內容」，你無法分辨「是日期不合法」還是「程式碼本身出了問題」。偵錯時所有錯誤都長一樣，是非常痛苦的處境。

**我怎麼想通的**

`catch` 應該只攔截你真正知道如何處理的例外型別。如果你想攔截「DateOnly 建構子因為日期非法而失敗」，就寫 `catch (ArgumentOutOfRangeException)`，讓其他意料外的例外繼續往上拋，讓呼叫端看到真實的錯誤型別。

但更進一步的反思是：用 `try/catch` 做預期內的輸入驗證，本身就是設計的壞味道。「月份可能是 13」不是一個意外，是一個預期的輸入情況，應該用回傳值（`TryParseExact` 的 `bool`）來處理，而不是讓程式拋例外再攔截。例外應該保留給真正意外的情況。

**我學到的原則**

`catch` 永遠指定型別，不寫裸 `catch`。對於可預期的輸入驗證失敗，優先使用 `TryXxx` 系列方法（`TryParse`、`TryParseExact`）的回傳值，而不是 `Parse` + `try/catch`。兩者的差別不只是風格，而是語意：例外代表「意料之外的情況」，回傳值代表「預期的結果之一」。

**下次遇到類似情況，我會先想到什麼**

看到 `catch` 沒有型別，立刻問：「這裡我真正想攔截的是什麼？」如果答案是一個具體的例外型別，就加上去。如果發現自己在用 `try/catch` 做輸入驗證，改用 `TryXxx` 系列。

---

### 條目 087 — Extension Method 設計：`ToRocNumericDate` 讓轉換邏輯歸位

**我做了什麼**

在 `DateHelper` 新增 `ToRocNumericDate(this DateOnly inputDate)`，讓 SyncWorker 可以直接用 `currentDate.ToRocNumericDate()` 組出 API 需要的民國年 `"YYYMMDD"` 字串，不需要在 SyncWorker 裡寫任何日期轉換邏輯。

**我遇到的問題**

這個轉換應該放在哪裡？最初的疑問是：直接在 SyncWorker 裡寫 `$"{rocYear:D3}{month:D2}{day:D2}"` 也只有一行，有必要抽到 `DateHelper` 嗎？

**我怎麼想通的**

判斷的關鍵不是「這行程式碼有多長」，而是「這個知識屬於誰」。民國年和西元年的換算規則（年份減 1911、格式補零）是日期工具的知識，不是毛豬同步 Worker 的知識。如果未來有其他 SyncWorker 也需要這個格式，它應該直接重用 `DateHelper.ToRocNumericDate`，而不是各自重寫一次換算邏輯。

設計成 extension method 讓呼叫端的語法更自然——`currentDate.ToRocNumericDate()` 讀起來像「這個日期，轉成民國年格式」，比靜態方法 `DateHelper.ToRocNumericDate(currentDate)` 更接近自然語言。`:D3` 確保民國 98 年補零成 `"098"`，不會漏掉前導零。

**我學到的原則**

「這個知識屬於誰」是判斷抽出 Helper 方法的標準，而不是「這段程式碼有多長」。只要邏輯屬於某個概念（日期、字串處理、業務規則），就把它放在對應的 Helper 裡。Extension method 是一個讓工具方法更貼近呼叫語境的好選擇，特別適合對既有型別（`DateOnly`、`string`）做領域相關的轉換。

**下次遇到類似情況，我會先想到什麼**

看到 SyncWorker 裡有日期運算或格式轉換，先問「這個邏輯屬於 Worker 還是屬於 DateHelper？」如果換一個 Worker 也會需要同樣的邏輯，它就屬於 Helper。

---

### 條目 088 — 歷史資料的欄位策略：全部存入 vs 篩選存入

**我做了什麼**

決定 `PorkTrans` Entity 的欄位策略，面對 API 回傳的 36 個數值欄位（含 `KgPig5`/`KgPig6` 兩個重量區間），以及歷史資料中這六個欄位全部為 `0` 的情況，確認是否要存入。

**我遇到的問題**

`KgPig5`/`KgPig6` 在民國 104 年前後的資料全是 `0`，民國 115 年才有實際數值。這看起來像「沒有資料，不需要存」。

**我怎麼想通的**

`0` 有兩種截然不同的語意：「這個交易區間當天真的沒有成交」和「農業部當時還沒有統計這個區間」。後者才是正確解讀——民國 115 年有實際數值，說明農業部在某個時間點新增了這兩個欄位的統計，舊資料的 `0` 是「尚未統計」的預設值，不是「無交易」。

更根本的判斷基礎是：這份資料是歷史交易記錄，不是每天都可以重新拉取的快照。`SyncState` 設計成從最後一筆往後走，一旦放棄某個欄位，回頭補跑那些過去的日期成本極高，而且可能再也補不到（API 有沒有保留那麼早的資料是另一個問題）。全部存入的代價是 Migration 多幾十個欄位，幾乎為零；選擇性存入的代價是未來任何需要那些欄位的功能都要回頭修 schema 並補跑歷史資料。

**我學到的原則**

對歷史記錄型資料，預設全部存入。判斷是否可以略過某個欄位的關鍵問題是：「如果三個月後發現需要這個欄位，我能以多少成本補回來？」對不可重跑的歷史資料，答案通常是「很高或不可能」，這讓「全部存入」幾乎永遠是正確選擇。`Page` 等請求控制參數（不是資料本身）才是唯一應該排除的例外。

**下次遇到類似情況，我會先想到什麼**

遇到「這個欄位現在全部是 `0`，需要存嗎」的問題，先確認：這是「現在沒有資料」還是「API 尚未支援這個統計」？前者可以不存，後者一定要存。歷史資料的補跑成本是決策的核心變數。

---

### 條目 089 — `lastSuccessfulDate` 模式：部分失敗時精確推進同步進度

**我做了什麼**

設計 `PorkTransSyncWorker` 的日期迴圈中斷後的進度記錄機制，從最初的「迴圈結束後直接用 `currentDate` 更新 `LastSyncedDate`」演進到 `lastSuccessfulDate` 變數模式。

**我遇到的問題**

`for` 迴圈終止條件是 `currentDate <= yesterdayDate`，迴圈跳出時 `currentDate` 等於 `yesterdayDate.AddDays(1)`——也就是明天的日期。如果直接用這個值更新 `LastSyncedDate`，代表「我今天已經 sync 完了」，但實際上迴圈可能在中途因 API 異常而 `break`，後面幾天的資料根本沒有拉到。

**我怎麼想通的**

引入 `lastSuccessfulDate`，初始值等於 `lastSyncState.LastSyncedDate`（上次的終點），在迴圈的每次迭代裡，只有當 API 回傳 `RS == "OK"` 時才推進：

```csharp
lastSuccessfulDate = currentDate;
```

休市日（API 回傳 OK 但 `Data` 是空的）也算成功——因為已經確認了那天沒有資料，下次不需要重跑。遇到 `RS != "OK"` 或網路例外就 `break`，迴圈結束後用 `lastSuccessfulDate` 更新 `SyncState`，而不是用 `currentDate` 或 `yesterdayDate`。

這樣不管迴圈是正常結束還是中途中斷，`SyncState` 都只推進到「真正確認過的最後一天」，下次執行自動從那天的隔天繼續，不會跳過任何一天的資料。

**我學到的原則**

進度追蹤的語意應該是「我已經確認完成的最後一步」，不是「迴圈變數目前在哪裡」。迴圈變數在中斷時的值是「下次應該嘗試的起點」或「已超出邊界的值」，不代表「已完成」。需要一個獨立的變數明確記錄「已確認成功」的進度，而且每次推進這個變數的時機要有明確的業務語意（成功 → 推進，失敗 → 不推進）。

**下次遇到類似情況，我會先想到什麼**

看到「迴圈結束後更新進度」的設計，先問：「這個迴圈有沒有可能在中途 `break`？`break` 時的迴圈變數值代表什麼語意？」如果迴圈可以中途中斷，就需要獨立的進度變數，不能依賴迴圈變數。

---

### 條目 090 — 原始髒資料的保留決策：SyncWorker 的職責邊界

**我做了什麼**

在 `PorkTrans` Entity 和 `MapToEntity` 的設計上，決定對 `OtherPigs_AvgWgt = -11` 這類負數值的處理策略：原樣存入、改成 `0`，還是改成 `null`。

**我遇到的問題**

一頭豬的平均重量為負數，在現實世界沒有意義，直覺上這是髒資料應該清洗。那麼清洗應該在哪裡做，改成什麼值？

**我怎麼想通的**

先確認 `MapToEntity` 的職責：它負責「把 DTO 轉成 Entity」，不負責「清洗來源資料」。如果在 `MapToEntity` 裡悄悄把 `-11` 改成 `0`，日後維護的人看到資料庫的值和原始 API 回傳值不一致，會很困惑——這個差異是 bug 還是設計？

其次確認如果真的要清洗，`null` 比 `0` 更誠實。`0` 的語意是「這個欄位的值真的是零」，把 `-11` 改成 `0` 是在捏造資料。`null` 的語意是「這個值有問題，我不確定正確答案」，至少保留了「原始資料有異常」的資訊。

最終決定原樣存入。SyncWorker 的職責是忠實同步來源資料，清洗邏輯應該在顯示層或分析層由功能需求驅動，不應該在 sync 層靜默地改掉資料。

**我學到的原則**

SyncWorker 是資料的搬運工，不是資料的裁判。來源髒資料原樣存入，讓下游（顯示層、分析層）自己決定如何處理。把清洗邏輯混入 `MapToEntity`，會讓資料庫裡的資料和原始來源出現無法追蹤的差異，這比「存了一個負數」更危險。如果清洗是必要的，要在明確的資料清洗層做，並記錄清洗規則，不要悄悄改掉數值。

**下次遇到類似情況，我會先想到什麼**

遇到來源資料有疑似髒資料時，先問：「清洗這個值，責任歸哪一層？」SyncWorker 負責同步，不負責清洗。如果真的要清洗，確認清洗後的值的語意：`null` 表達「不知道」，`0` 表達「真的是零」，兩者不能混用。

---

### 條目 091 — DTO 職責分層：WorkerResponses 與 ApiResponses 解決「這個 DTO 是給誰用的」

**我做了什麼**

將 `TaiwanAgri.Modules.Market/Dtos/` 資料夾從一個扁平目錄重組為兩個子資料夾：`WorkerResponses/`（既有的 Worker 反序列化 DTO）和 `ApiResponses/`（新建的 Service 輸出 DTO），並同步更新五支 SyncWorker 的 using 路徑。

**我遇到的問題**

開始建立 `PriceResponseDto`、`CropResponseDto` 時，直覺是放到 `Dtos/` 資料夾根目錄，和 `AgriProductsTransTypeDto`、`DebrisAlertRecordDto` 並列。這樣做有什麼問題？

問題在於打開 `Dtos/` 資料夾後，你看到的是十幾個 DTO 檔案，但你無法一眼判斷每一個是「SyncWorker 用來解析農業部 API 回傳 JSON 的」還是「Service 用來組裝給前端看的」。兩者的維護邏輯截然不同——前者的欄位名稱由農業部 API 的 JSON 格式決定，後者的欄位名稱由前台畫面的需求決定。

**我怎麼想通的**

判斷「這個 DTO 屬於哪個資料夾」的標準不是它有多長或多複雜，而是它服務的資料流方向：資料是從外部進來還是從系統出去？DTO 作為資料的形狀描述，它的用途決定了它的歸屬。

命名子資料夾時，`WorkerResponses/` 和 `ApiResponses/` 比 `Moa/`（來源命名）更好，因為後者需要讀者事先知道「MOA 是什麼」才能理解；前者用角色命名，維護者不需要任何背景知識就能讀懂意圖。這個原則可以推廣到任何命名決策：角色命名比來源命名更有自解釋性。

**我學到的原則**

資料夾結構是程式碼的第一層說明文件。當一個資料夾裡的檔案服務兩種截然不同的用途時，應該拆開，而不是讓讀者自己去辨別。重組的代價很低（只是移動檔案和更新 using），但帶來的可讀性提升是永久的。

**下次遇到類似情況，我會先想到什麼**

看到一個資料夾裡的檔案需要仔細辨別才能分類時，先問「這些檔案服務幾種不同的用途？」。如果答案超過一種，分資料夾的成本幾乎為零，但帶來的清晰度提升是持久的。

---

### 條目 092 — 相依方向決定 Service 的位置：上層依賴下層，而非反過來

**我做了什麼**

決定 `MarketService` 應該放在 `TaiwanAgri.Modules.Market/Services/` 而不是 `TaiwanAgri.Web/Services/`，並確認 `PriceResponseDto` 等輸出 DTO 也應該跟著放在 `Modules.Market`，不是 Web 層。

**我遇到的問題**

「Service 是 Controller 用的，放在 Web 不是更直覺嗎？」這個直覺從何而來，又為什麼是錯的？

**我怎麼想通的**

問題的關鍵不在 Service 是給誰「用」，而在「誰依賴誰」的方向。如果 `MarketService` 放在 `TaiwanAgri.Web`，那麼 `TaiwanAgri.Web` 就同時是「所有 HTTP 入口的組裝點」和「查詢邏輯的持有者」，兩個職責混在一個專案裡。

更嚴重的問題是未來的擴展性：如果哪天需要一個 `TaiwanAgri.Admin` 後台，它也需要查詢市場資料，就必須參考 `TaiwanAgri.Web` 才能拿到 `MarketService`——這是一個下層依賴上層的反向依賴。正確的架構方向是：`TaiwanAgri.Web`（上層，負責 HTTP）→ 依賴 → `TaiwanAgri.Modules.Market`（下層，負責業務邏輯）。

這個判斷讓所有後續的位置決策都變得清晰：凡是「查詢資料、組裝資料」的邏輯，都是 `Modules.Market` 的職責；凡是「收 HTTP 請求、回 HTTP 回應」的邏輯，才是 `TaiwanAgri.Web` 的職責。

**我學到的原則**

決定一段程式碼屬於哪個層的問題不是「誰用它」，而是「它的知識屬於哪個概念域」。查詢市場價格資料是市場模組的知識；處理 HTTP Request/Response 是 Web 入口層的知識。知識歸位，相依方向自然正確。

**下次遇到類似情況，我會先想到什麼**

遇到「這個 Class 要放哪」的問題，先問：「它的核心知識（它本來就該知道的事）是什麼？哪個專案最直接擁有這份知識？」而不是「誰會呼叫它」。

---

### 條目 093 — IMarketService 介面的具體價值：不是為了優雅，是為了可測試

**我做了什麼**

在 `Modules.Market/Services/` 建立 `IMarketService` 介面和 `MarketService` 實作，讓 `MarketController` 透過建構子注入 `IMarketService` 而非直接使用 `MarketService`。

**我遇到的問題**

Portfolio 專案加 `IMarketService` 是不是過度設計？介面增加了兩個檔案和一定的複雜度，但在這個規模下，它帶來的價值在哪裡？

**我怎麼想通的**

先確認這個專案有沒有讓介面有意義的前提：Solution 裡有 `TaiwanAgri.Tests` 專案。Controller 的單元測試需要注入一個「不會真的去打資料庫」的 `IMarketService`——透過 Moq 之類的工具建立 mock 版本，讓測試可以獨立執行，不依賴 DB 的狀態。這個需求是具體的，不是假設的。

如果沒有測試計畫，介面的確可以省略；一旦有測試，介面就不是「優雅的錦上添花」，而是「讓測試成為可能的基礎設施」。決定加不加介面，應該從測試需求出發，而不是從架構上的美感出發。

**我學到的原則**

設計決策要有具體的理由，不是「這樣比較『正確』」。`IMarketService` 的存在理由是：Controller 的單元測試需要 mock，mock 需要介面，所以介面有必要。這個推導鏈清楚，任何人看到這個介面都能理解它為什麼存在。

**下次遇到類似情況，我會先想到什麼**

問「要不要加介面」之前，先問「這個 Class 的呼叫端有沒有需要在測試時被替換掉的需求？」。有測試需求 → 加介面。沒有 → 可以省略，之後有需要再加也不遲。

---

### 條目 094 — TaiwanAgri.Web 改造策略：不砍重建的工程判斷

**我做了什麼**

將 Visual Studio 用「ASP.NET Core Web App with Authentication」樣板建出來的 `TaiwanAgri.Web` 直接改造成純 Web API 專案，而不是砍掉重建一個 `TaiwanAgri.Api`。

**我遇到的問題**

樣板專案帶了很多「不需要的包袱」：MVC、Razor Pages、Views 資料夾、wwwroot、Identity UI 等。直覺上「砍掉重建一個乾淨的 Api 專案」似乎更正確，為什麼最後選擇直接改造？

**我怎麼想通的**

砍重建的代價不只是「新建一個專案」，最大的成本在 `ApplicationDbContext`。它管理 Identity 的六張表（`AspNetUsers`、`AspNetRoles` 等），並且已有 Migration 歷史。搬移 `ApplicationDbContext` 到新專案意味著要決定：重新 Migration 還是保留舊 Migration？重新 Migration 要處理 DB 現有資料的對齊；保留舊的要設定 Migration Assembly 路徑。

直接改造的步驟只有四個：`AddControllersWithViews()` 改 `AddControllers()`、移除 `Views/wwwroot/Razor` 的殘骸、改 `HomeController` 繼承、調整 Middleware pipeline。Migration 完全不動，Identity 表繼續在原位。兩者的結果完全等效，改造的風險趨近於零。

工程判斷不只是「哪種架構更美」，也要考量「這個決策的代價是否與它帶來的好處相稱」。在這個場景下，代價不相稱，所以選改造。

**我學到的原則**

評估「重建 vs 改造」的標準是：改造後的結果和重建後的結果，在功能上是否等效？如果等效，而且改造的代價遠低於重建，就不應該重建。「更乾淨」不是充分的理由，除非它帶來的可維護性提升能具體說清楚在哪裡。

**下次遇到類似情況，我會先想到什麼**

想到「砍掉重建」的衝動出現時，先列出：重建的實際步驟和代價是什麼？直接改造需要哪些步驟？兩者的結果有什麼實質差異？如果差異只是「感覺更乾淨」，通常選改造。

---

### 條目 095 — CORS 與 Middleware 順序：前後端分離架構的必要配置

**我做了什麼**

在 `TaiwanAgri.Web` 的 `Program.cs` 設定 CORS Policy，允許 Vue 3 dev server（`http://localhost:5173`）的跨域請求，並確認 `UseCors()` 在 Middleware pipeline 中放在 `UseAuthentication()` 之前的正確位置。

**我遇到的問題**

CORS 錯誤是前後端分離架構最常見的第一個障礙。為什麼瀏覽器會拒絕這個請求？`UseCors()` 放錯位置會發生什麼事？

**我怎麼想通的**

CORS 是瀏覽器的安全機制，不是伺服器的安全機制。當 Vue 3（`localhost:5173`）發送一個請求到 Web API（`localhost:7000`，不同 port = 不同 Origin），瀏覽器在真正發出請求之前，會先發一個 OPTIONS 預檢請求問伺服器「你允許我這個 Origin 嗎？」。如果伺服器沒有回應正確的 CORS 標頭，瀏覽器就阻止請求，開發者工具才看到那個經典的紅色 CORS 錯誤。

Middleware 的執行順序在 ASP.NET Core 裡是固定的，每個請求依序通過每個 Middleware。`UseCors()` 必須放在 `UseRouting()` 之後（路由已解析）、`UseAuthentication()` 之前（CORS 預檢請求不帶認證 token，如果認證 Middleware 先跑，預檢請求就會被擋掉，導致所有 API 對未認證用戶都無法預檢）。

**我學到的原則**

Middleware 順序錯誤的 bug 很難追蹤，因為問題不在個別 Middleware 的邏輯，而在它們的交互作用。理解每個 Middleware 在做什麼、它需要什麼前提，是排正確順序的依據，不是死記硬背。

**下次遇到類似情況，我會先想到什麼**

設定 CORS 後，先確認：`UseCors()` 在 `UseRouting()` 後面了嗎？在 `UseAuthentication()` 前面了嗎？這兩個位置要求是 CORS 能正常工作的必要條件。

---

### 條目 096 — Controller 日期驗證策略：string + ParseIsoDate 取代 [FromQuery] DateOnly

**我做了什麼**

設計 `MarketController` 的日期參數時，選擇用 `[FromQuery] string? startDate` 加上手動呼叫 `DateHelper.ParseIsoDate` 做驗證，而不是直接用 `[FromQuery] DateOnly? startDate` 依賴框架的 Model Binding。

**我遇到的問題**

為什麼不直接讓框架幫我解析日期？`[FromQuery] DateOnly startDate` 看起來更簡潔，也更「標準」。

**我怎麼想通的**

問題有兩個層面。第一，技術可靠性：`DateOnly` 是 .NET 6 加入的型別，ASP.NET Core 的 Model Binding 對它的 Query String 解析支援在不同版本間有行為差異，不是永遠可靠的。

第二，也是更重要的：Controller 的職責包含「輸入驗證」，這個職責應該被明確表達，而不是隱藏在框架的自動行為裡。當 `startDate = "abc"` 傳進來，我希望發生的是：Controller 回傳一個友好的 400 並告訴前端「請使用 yyyy-MM-dd 格式」。框架自動解析失敗的話，它回傳的錯誤訊息格式不在我的控制下。

這個選擇讓 Controller 對「壞輸入」的行為是明確的、可測試的：

```csharp
var start = DateHelper.ParseIsoDate(startDate);
if (start == null) return BadRequest("startDate 格式錯誤，請使用 yyyy-MM-dd");
```

**我學到的原則**

越靠近使用者的層（Controller）越應該主動表達驗證邏輯，而不是依賴框架的隱式行為。隱式行為在 happy path 下很方便，但在錯誤路徑下很難控制——而錯誤路徑的體驗往往比 happy path 更重要（因為使用者在出錯時最需要清楚的引導）。

**下次遇到類似情況，我會先想到什麼**

遇到「要不要用框架的自動型別轉換」，先問：「如果輸入不合法，我希望使用者看到什麼錯誤訊息？這個訊息是框架自動產生的，還是我自己控制的？」如果需要自訂錯誤訊息，就選手動驗證。

---

### 條目 097 — 商業邏輯與輸入驗證的邊界：預設日期區間的正確歸屬

**我做了什麼**

決定 `GetPricesAsync` 的「選填日期不傳時預設今天往前 365 天」這段邏輯應該放在 `MarketService` 裡，而不是在 `MarketController` 中補預設值再傳進 Service。

**我遇到的問題**

同樣是「處理日期」，為什麼格式驗證放 Controller，預設值邏輯卻放 Service？這條線怎麼劃的？

**我怎麼想通的**

區分的標準是：這段邏輯是「技術約束」還是「業務決策」？

格式驗證（`"abc"` 不是合法日期）是技術約束，它的正確性是由資料型別的定義決定的，跟任何業務需求無關，也永遠不會因為 PM 的想法改變。放 Controller 合理。

預設日期區間（「不傳日期就看最近一年」）是業務決策，它的正確性由產品設計決定——如果 PM 說「使用者研究發現三個月比一年更有用，改成九十天」，這個邏輯就要改。它應該放在能反映業務決策的層（Service），讓 Controller 完全不知道「預設值是多少」這件事。

這個分層讓 Controller 的測試變得簡單：Controller 只需要測「傳了格式錯的日期會回傳 400」；Service 的測試只需要測「不傳日期時，查詢的起始日期是否正確套用了 -365 天的規則」。兩個測試各自獨立，互不干擾。

**我學到的原則**

「這段邏輯如果需要改，我會去哪一層改？」是判斷它歸屬哪一層的最好問題。會因為業務需求改變的邏輯放 Service；只由技術規格決定的邏輯放 Controller（輸入驗證）。

**下次遇到類似情況，我會先想到什麼**

看到某段邏輯不確定歸屬時，問：「如果需求改了，這段邏輯要跟著改，會是因為 PM/使用者的想法改了，還是因為技術規格改了？」前者 → Service；後者 → Controller。

---

### 條目 098 — AsQueryable() 延遲執行：讓 LINQ 查詢條件動態組合成為可能

**我做了什麼**

在 `GetDisastersAsync` 和 `GetPricesAsync` 裡用 `AsQueryable()` + 條件式 `if` 追加 `.Where()` 的模式，讓動態過濾條件在不重複基礎查詢邏輯的情況下優雅地組合。

**我遇到的問題**

`counties` 是選填陣列，不傳時要回傳全台資料，傳了才過濾特定縣市。如果不用 `AsQueryable()`，這個條件怎麼表達？

**我怎麼想通的**

不用 `AsQueryable()` 的話，唯一的選項是兩條分開的查詢路徑：

```csharp
if (counties.Any())
    // 查詢帶 counties 過濾的版本（含日期範圍）
else
    // 查詢不帶 counties 過濾的版本（含日期範圍）
```

日期範圍這個共同條件要在兩個分支裡各寫一次，重複且難維護。

`AsQueryable()` 的核心概念是：它回傳的是一個「查詢計畫」（`IQueryable<T>`），不是資料。每次追加 `.Where()` 只是在這個計畫上加一個條件，SQL 語句只在 `ToListAsync()` 那一刻才真正被組裝出來並送往資料庫。因此可以這樣寫：

```csharp
var query = _context.DebrisAlertRecords.AsQueryable();
query = query.Where(d => d.LastUpdateDate >= start && d.LastUpdateDate <= end); // 必填條件
if (counties != null && counties.Any())
    query = query.Where(d => counties.Contains(d.County)); // 選填條件
return await query.Select(...).ToListAsync();
```

共同條件只寫一次，動態條件以 `if` 組合，最後只送出一條完整 SQL。

**我學到的原則**

EF Core 的 `IQueryable<T>` 是一個延遲執行的查詢建構器，不是一個資料集合。在 `ToListAsync()` 之前的所有操作都是在「描述查詢」，不是在「執行查詢」。理解這一點，動態條件組合就是自然而然的結果，不需要任何 SQL 字串拼接或多條分開的查詢。

**下次遇到類似情況，我會先想到什麼**

看到「這個條件是不是要加，要看使用者傳了什麼參數」的情境，立刻想到 `AsQueryable()` + 條件式 `.Where()` 的模式，而不是寫多條分開的 LINQ 查詢。

---

### 條目 099 — EF Core 查詢的兩個世界：ToListAsync 前後的 SQL vs C# 邊界

**我做了什麼**

在實作 `GetRestDaysAsync` 時，發現 `new DateOnly(r.Year, r.Month, r.RestDay)` 無法放在 `ToListAsync()` 之前的 EF Core LINQ 裡，必須拆成兩段：先 `ToListAsync()` 把資料載入記憶體，再用純 C# LINQ 做 `DateOnly` 的建構和日期範圍過濾。

**我遇到的問題**

為什麼 `GetMarketsAsync` 的 `Select` 可以在 `ToListAsync()` 之前，但 `GetRestDaysAsync` 裡的 `Select` 就不行？兩個都是 `Select`，差在哪裡？

**我怎麼想通的**

關鍵在於 EF Core 需要把 LINQ 翻譯成 SQL。SQL 理解「取這個資料表的某個欄位的值」，但不理解「用三個值呼叫 C# 的建構子」。

`GetMarketsAsync` 的 `Select(m => new MarketResponseDto { MarketCode = m.MarketCode, ... })` 本質上是在說「從這個資料列取這幾個欄位的值，放到新物件裡」——SQL 可以翻譯成 `SELECT MarketCode, MarketName FROM ...`，完全合法。

`GetRestDaysAsync` 的 `Select(r => new DateOnly(r.Year, r.Month, r.RestDay))` 是在說「呼叫一個 C# 的建構子，傳入三個值」——SQL 裡沒有「呼叫 C# 建構子」這個概念，EF Core 不知道怎麼翻譯，在執行時拋例外。

解法是讓資料先越過 `ToListAsync()` 這條邊界進入 C# 的記憶體空間，再用普通的 C# 操作——包括呼叫建構子、使用任何 .NET 方法——處理資料。兩步驟的拆分不是因為「我想分兩步」，而是因為「SQL 翻譯邊界」要求必須這樣做。

**我學到的原則**

EF Core LINQ 和普通 LINQ 看起來一模一樣，但執行環境完全不同：`ToListAsync()` 之前是 SQL 翻譯模式（每個操作必須能對應到合法 SQL），之後是 C# 執行模式（任何 C# 語法都可以用）。這條邊界是很多 EF Core 新手踩坑的根本原因，理解它之後，「這個操作能不能放在 `ToListAsync()` 之前」就有了清楚的判斷標準。

**下次遇到類似情況，我會先想到什麼**

在 EF Core LINQ 裡看到自訂建構子、C# 專屬方法（`string.IsNullOrEmpty()`、`new DateOnly(...)`）或 ValueTuple 投影，立刻想到「這必須放在 `ToListAsync()` 之後」，先載入記憶體再處理，不要嘗試讓 EF Core 翻譯 C# 語法。

---

### 條目 100 — 聚合語意的選擇：AVG(價格) + SUM(數量) 不是隨意決定的

**我做了什麼**

設計 `GetPricesAsync` 的「全台均價」模式（使用者不選特定市場）時，決定對 `UpperPrice`、`MiddlePrice`、`LowerPrice`、`AvgPrice` 用 `AVG()`，對 `TransQuantity` 用 `SUM()`，而不是全部用 `AVG()` 或全部用 `SUM()`。

**我遇到的問題**

為什麼五個欄位不是統一用同一種聚合函數？既然是「均價」，全部用 AVG 不是更一致？

**我怎麼想通的**

這個問題需要從業務語意出發，而不是從數學操作出發。

**價格欄位的語意**：`AvgPrice = 26.80 元/公斤` 代表的是「這個市場這一天的批發均價」，是一個比率，代表的是「這個市場對這個作物的定價水準」。當你想知道「全台高麗菜今天的市場均價是多少」，把各個市場的均價再平均，得到的是各市場定價水準的平均值，這是有業務意義的。

**數量欄位的語意**：`TransQuantity = 1410 公斤` 代表的是「這個市場這一天賣出了多少公斤」，是一個絕對數量，不是比率。當你想知道「全台高麗菜今天一共賣了多少公斤」，正確的答案是把所有市場的數量加起來（SUM），而不是算「平均每個市場賣了多少」（AVG）。使用者關心的是「全台今天的總成交量」，不是「平均每個市場的成交量」。

這個區分——比率用 AVG、絕對量用 SUM——是一個通用的統計設計原則，在任何需要跨維度聚合的場景都適用。

**我學到的原則**

設計聚合策略時，先問「這個欄位的語意是什麼，它是比率還是絕對量？跨維度聚合後，使用者期望看到的是平均值還是加總？」。技術上 AVG 和 SUM 都可以用，但只有語意正確的那個才能給前端提供有意義的資料。

**下次遇到類似情況，我會先想到什麼**

看到需要 `GroupBy` 後聚合的設計，對每個欄位逐一問：「這個值是比率還是絕對量？使用者對 GroupBy 後的這個欄位，最想看到的是什麼？」從業務語意決定聚合函數，而不是憑直覺選一個「看起來合理」的。

---

### 條目 101 — 前端三層架構：api / Store / Component 和後端 HttpClient / Service / Controller 的對應

**我做了什麼**

在 TaiwanAgri.Frontend 從零建立 `src/api/market.ts`（axios 封裝）、`src/stores/market.ts`（Pinia Store）、元件層（MarketFilter / DateRangePicker / PriceChart / MarketView），並在開發過程中持續用後端架構類比來判斷每段邏輯放在哪一層。

**我遇到的問題**

前端的「Service 層」在哪裡？API 呼叫函式是直接寫在 Store 裡，還是另外拆出 `src/api/` 層？

**我怎麼想通的**

從後端出發：你的 SyncWorker 用 HttpClient 打農業部 API，這個 HttpClient 呼叫不會直接寫在 Service 裡，而是封裝在獨立的 HTTP 呼叫層。前端的 `src/api/market.ts` 就是這個 HttpClient 封裝——只負責「打出去、回傳資料」，不持有任何狀態。Store 呼叫它，就像 Service 呼叫 Repository 一樣。

```
MarketView.vue（元件，接收使用者操作）
    ↓ 呼叫 store.action()
Pinia Store（管理共享狀態）
    ↓ 呼叫 marketApi.getXxx()
src/api/market.ts（axios，打 HTTP）
    ↓ GET /api/market/...
ASP.NET Core（localhost:5258）
```

**我學到的原則**

前後端架構有鏡像關係。判斷前端某段邏輯應該放哪一層，把它類比到後端對應的概念就有答案——「DOM 操作」是 Controller 的事（元件），「HTTP 呼叫」是基礎設施的事（api 層），「業務邏輯與狀態」是 Service 的事（Store）。

**下次遇到類似情況，我會先想到什麼**

看到「這個函式是打 API 還是管狀態還是觸發 UI」，三個問題各對應一層，不用猜，直接分類。

---

### 條目 102 — computed() 的職責邊界：顯示格式轉換屬於元件，不屬於 Store

**我做了什麼**

在 `PriceChart.vue` 裡用 `computed()` 把 `props.prices`（平鋪陣列）轉換成 Chart.js 需要的 `datasets` 格式（按 cropCode 分組的折線資料），而非把這個轉換放在 Pinia Store 或 MarketView.vue。

**我遇到的問題**

同一份 `prices` 資料，Chart.js 需要的格式和原始格式完全不同。這個「格式轉換」的邏輯應該放在哪一層——Store（因為它要存資料）、MarketView（因為它傳資料給圖表），還是 PriceChart 內部？

**我怎麼想通的**

關鍵問題是：「如果未來加入表格顯示模式（同樣的 prices 資料，但用表格呈現），Store 裡的 Chart.js 格式資料還有用嗎？」

答案是沒有。Chart.js 的 `datasets` 結構把高中低價全部丟掉了，只留下均價，這個格式只服務折線圖這一個顯示元件。把它放進 Store 意味著 Store 在管一個「只有圖表才懂的顯示格式」，Store 開始知道太多它不需要知道的事。

後端類比：Service 不負責把資料格式化成 CSV 或 JSON，那是 Controller 的事。`PriceChart.vue` 就是它自己的 Controller，決定怎麼把資料呈現給 Chart.js。

**我學到的原則**

`computed()` 的正確使用場景是「從已知狀態衍生出這個元件需要的顯示格式」。如果某個 computed 的結果只有一個元件會用到，它就屬於那個元件，不屬於 Store。Store 管原始狀態，元件管顯示格式。

**下次遇到類似情況，我會先想到什麼**

看到「需要把 A 格式轉成 B 格式來顯示」，先問「這個 B 格式有其他元件也需要嗎」。沒有 → 放元件的 computed，有 → 才考慮放 Store。

---

### 條目 103 — TypeScript 嚴格模式與陣列索引的 undefined 問題

**我做了什麼**

在 `PriceChart.vue` 使用 TypeScript 嚴格模式開發，遇到四個紅線錯誤，全部都是「陣列索引存取可能回傳 undefined」的問題，逐一用非 null 斷言（`!`）或 helper function 解決。

**我遇到的問題**

```typescript
// ❌ 錯誤：TS 認為 groups[p.cropCode] 可能是 undefined
groups[p.cropCode].priceMap[p.transDate] = p.avgPrice

// ❌ 錯誤：maRaw[i] 是 number | undefined
actualPairs.forEach((p, i) => { maMap[p.d] = maRaw[i] })

// ❌ 錯誤：PALETTE[i % PALETTE.length] 可能是 undefined
const color = PALETTE[ci++ % PALETTE.length]
```

**我怎麼想通的**

TypeScript 在嚴格模式下，任何陣列索引存取的回傳型別都是 `T | undefined`，即使你用 `%` 確保不會越界，TypeScript 靜態分析不知道你的迴圈邏輯能保證這點，它只看型別定義。

修法有幾種：

```typescript
// 方法一：非 null 斷言，告訴 TS「我確定這裡有值」
const entry = groups[p.cropCode]!
entry.priceMap[p.transDate] = p.avgPrice

// 方法二：同上，對陣列元素
actualPairs.forEach((p, i) => { maMap[p.d] = maRaw[i]! })

// 方法三：封裝成 helper，讓 ! 只出現一次
const getColor = (i: number) => PALETTE[i % PALETTE.length]!
```

**我學到的原則**

TypeScript 嚴格模式下的陣列索引型別是 `T | undefined`，這是設計上刻意的——陣列索引越界在 JavaScript 是常見的 bug 來源，TS 透過型別系統強迫開發者明確表態「我確定這裡有值」或「我需要處理 undefined 的情況」。`!` 是告訴 TS「我比你更了解這裡的執行期行為」，合理但要謹慎——只在你能靠邏輯保證不越界時使用。

**下次遇到類似情況，我會先想到什麼**

看到 `Property 'x' does not exist on type 'T | undefined'`，優先找「為什麼 TS 認為這裡可能是 undefined」，再決定是加 `!` 斷言、用 `?.` 可選鏈、還是先做 null check。

---

### 條目 104 — Chart.js 按需註冊設計：不用的功能不打包

**我做了什麼**

在 `PriceChart.vue` 引入 Chart.js 時，沒有用 `import Chart from 'chart.js/auto'`（全量引入），而是只 import 和 register 實際用到的元件（LineElement、PointElement、CategoryScale 等）。

**我遇到的問題**

為什麼要分開 import 再 register？直接 `import 'chart.js/auto'` 不是更方便嗎？

**我怎麼想通的**

`chart.js/auto` 會把所有圖表類型（Bar、Pie、Radar、Polar...）和所有 Plugin 全部打包進 bundle，但我們只用折線圖。按需引入讓 bundler（Vite）的 tree-shaking 能移除沒有用到的部分，最終打包體積顯著更小。

```typescript
// 只引入折線圖需要的六個元件
Chart.register(LineElement, PointElement, LineController, CategoryScale, LinearScale, Tooltip, Legend, Filler)
```

**我學到的原則**

前端套件的「全量引入」方便開發但懲罰效能。Chart.js 採用按需註冊的設計，是刻意讓開發者能精確控制打包體積。對一個展示型的 Side Project，載入速度是面試官會注意的細節。

**下次遇到類似情況，我會先想到什麼**

引入任何前端套件前，先確認有沒有按需引入的路徑。能精確 import 的，不用 `/auto` 或全量版本。

---

### 條目 105 — Chart.js 自訂 Plugin 與 afterDraw hook

**我做了什麼**

在 `PriceChart.vue` 的 `buildChart()` 裡定義了一個 inline plugin `disasterPlugin`，使用 `afterDraw` hook 在圖表所有資料線繪製完成後，用 Canvas 2D API 畫天災垂直虛線、頂部三角標記和旋轉文字。

**我遇到的問題**

Chart.js 沒有內建「在特定 X 位置畫垂直線並標注文字」的功能。Plugin 系統是什麼？`afterDraw` 是什麼時機觸發的？`scales['x']!.getPixelForValue(idx)` 的 `idx` 為什麼要傳索引而不是 label 字串？

**我怎麼想通的**

Chart.js 的 Plugin 是一個物件，定義了在圖表生命週期各個時間點的 callback。`afterDraw` 在 Chart.js 完成主體繪製後觸發，此時 canvas context 已經有資料線，我們在它上面再疊加繪製，就不會被資料線覆蓋。

`getPixelForValue(idx)` 的參數是 label 的索引（數字），不是 label 的值（字串），因為 X 軸是 `CategoryScale`，它用整數索引定位每個類別的像素位置。傳字串會找不到對應位置。

```typescript
const idx = labels.indexOf(date)     // 先把字串 date 轉成索引
if (idx === -1) return               // 不在 X 軸，跳過
const x = scales['x']!.getPixelForValue(idx)  // 索引 → 像素 X 座標
```

**我學到的原則**

Canvas 2D API 的繪圖是「所有操作都在同一張畫布上堆疊」。後畫的覆蓋先畫的，所以在 `afterDraw` 時機畫的東西永遠在資料線上方。`save()` + `restore()` 確保每次繪圖操作結束後還原 context 狀態，不影響後續的繪圖。

**下次遇到類似情況，我會先想到什麼**

需要在 Chart.js 圖表上疊加非標準圖形時，先查 Plugin 的 hook 時機（`beforeDraw` / `afterDraw` / `afterDatasetsDraw`），而不是去找第三方套件。`afterDraw` 是最常用的，`afterDatasetsDraw` 用在需要圖形出現在資料線之間的情況。

---

### 條目 106 — 天災垂直線的設計決策：日期不在 X 軸時跳過，不找最近交易日

**我做了什麼**

在設計天災垂直線的邏輯時，面對「天災日期是休市日，X 軸沒有這個日期」的情況，選擇直接跳過（`if (idx === -1) return`），不改為在最近的交易日畫線。

**我遇到的問題**

如果颱風在週日登陸，但 X 軸只有交易日（週一到週五），垂直線就沒辦法畫在正確的位置。為什麼不找「最近的交易日」來畫？

**我怎麼想通的**

「找最近的交易日畫線」等於把颱風週日發生這件事，在圖表上呈現成「發生在週一」。使用者看到週一有一條紅線，自然會以為颱風的影響是從週一開始，但實際上週一的價格已經是天災之後的交易，不是天災當天。這個位移製造了因果關係的誤導。

跳過不畫雖然讓圖表看起來「少一條線」，但它呈現的是真實情況——「這一天有天災，但沒有交易記錄」。右側的天災面板清單仍然顯示完整的事件資訊（日期 + 受影響縣市），使用者可以對照查看。

**我學到的原則**

資料視覺化的首要原則是「忠實呈現資料，不製造假資訊」。圖表的工作是把資料變得易於理解，不是讓圖表「看起來更完整」。當「讓圖表更完整」和「呈現真實」衝突時，選後者。

**下次遇到類似情況，我會先想到什麼**

任何「為了視覺效果而移動資料點位置」的設計，先問「這樣做是否改變了使用者對資料的理解方式，以及改變的方向是否符合真實情況」。

---

### 條目 107 — DisasterResponseDto 重設計：GroupBy 去重 + AffectedCounties 聚合

**我做了什麼**

重新設計 `DisasterResponseDto` 和 `GetDisastersAsync` 的資料處理邏輯，把「同一個天災事件在每個村落各一筆」的資料庫記錄，彙整成「一個事件 + 受影響縣市清單」的輸出格式，並移除前端無法使用的 `alertDate` 必填參數。

**我遇到的問題**

原本 API 回傳幾百筆資料（每個受警戒的村落一筆），前端拿到這份資料後要自己 GroupBy，才能在天災面板上顯示「0404豪雨：苗栗縣、台中市」這樣的格式。問題出在哪裡？

**我怎麼想通的**

前端做 GroupBy 不是不可以，但這個彙整操作涉及業務判斷（「什麼叫做同一個天災事件」），屬於業務邏輯，應該在 Service 層做，而不是散落在 Vue 的 computed 裡。

後端在 Service 層做 GroupBy 還有另一個好處：`AffectedCounties` 的去重（`Distinct()`）和排序（`OrderBy`）在 SQL 層完成，前端拿到的是乾淨、可直接渲染的資料：

```csharp
.GroupBy(d => new { d.DisasterName, d.AlertDate })
.Select(g => new DisasterResponseDto
{
    DisasterName     = g.Key.DisasterName,
    AlertDate        = g.Key.AlertDate.ToString("yyyy-MM-dd"),
    AffectedCounties = g.Select(x => x.County).Distinct().OrderBy(c => c).ToList()
})
```

`alertDate` 參數的問題更基本：前端不可能事先知道天災的發布日期，這個必填參數讓 API 根本無法被正常呼叫，是設計上的根本錯誤，必須移除。

**我學到的原則**

API 的輸出格式應該以「前端可以直接使用」為設計目標。如果前端收到資料後還需要大量的 GroupBy / 去重 / 排序，說明 Service 層沒有做好它的工作。Service 的職責是把業務需求翻譯成正確的資料，不是把原始資料直接丟給前端自己想辦法。

**下次遇到類似情況，我會先想到什麼**

設計 API 輸出 DTO 時，從「前端需要渲染什麼畫面」出發，倒推 DTO 應該長什麼樣子，再決定 Service 要做哪些彙整。不要從「DB 裡存了什麼」出發設計 DTO。

---

### 條目 108 — Promise.all 並行 API 呼叫：兩支無依賴關係的 API 不需要等第一支完成才打第二支

**我做了什麼**

在 `MarketView.vue` 的 `handleQuery()` 裡，用 `Promise.all` 同時呼叫 `GetPrices` 和 `GetDisasters`，而非依序呼叫。

**我遇到的問題**

直覺是先拿到 prices 再拿 disasters，但這樣「先後」的感覺對嗎？

**我怎麼想通的**

先問「GetDisasters 需要 GetPrices 的結果嗎？」——不需要。它們的輸入只有 `startDate` 和 `endDate`，彼此沒有依賴關係。兩支 API 並行打，等待時間從「A + B 毫秒」變成「max(A, B) 毫秒」，對使用者來說查詢速度快一倍。

```typescript
// ❌ 依序：等 A 完成才打 B，浪費了等待時間
const priceResult   = await marketApi.getPrices({ ... })
const disasterResult = await marketApi.getDisasters({ ... })

// ✅ 並行：A 和 B 同時打，等最慢的那個
const [priceResult, disasterResult] = await Promise.all([
  marketApi.getPrices({ ... }),
  marketApi.getDisasters({ ... }),
])
```

**我學到的原則**

`await` 的依序語義在「B 需要 A 的結果」時才有意義。兩個沒有因果關係的非同步操作，依序呼叫是在浪費使用者的時間。`Promise.all` 是前端並行化的標準工具，任何一支 reject 時整體 reject，不需要分別處理兩個 loading 狀態。

**後端類比**：`Task.WhenAll(taskA, taskB)` 就是 C# 的 `Promise.all`。同樣的原則，在後端已經用過了。

**下次遇到類似情況，我會先想到什麼**

看到兩個以上的 `await` 並排，先問「後面的需要前面的結果嗎」。不需要 → 改成 `Promise.all`。需要 → 依序 await 是正確的。

---

### 條目 109 — CSV 匯出的架構分層：純函式、DOM 操作、資料來源各屬不同層

**我做了什麼**

把 CSV 匯出拆成三個部分並放在三個不同的位置：`src/utils/exportCsv.ts`（轉換純函式）、`MarketView.vue` method（觸發下載）、元件本地 `prices.value`（資料來源），而非全部塞進一個函式或放進 Store。

**我遇到的問題**

CSV 匯出這個功能看起來不大，但要判斷「匯出邏輯放哪裡」。是放 Store、放 utils、還是直接在元件裡寫？

**我怎麼想通的**

把這個動作分解成三個部分：

1. **讀取資料**：`prices.value`，是元件本地的查詢結果，不在 Store
2. **資料轉換**（prices → CSV 字串）：純函式，輸入確定輸出確定，沒有副作用
3. **觸發瀏覽器下載**：`document.createElement('a')`，這是 DOM 操作，是 UI 行為

後端類比：把「生成 CSV 字串」的邏輯放進 Store（Service），就像把 `Response.WriteAsync(csv)` 放進 Service 層。Service 不應該直接操作 HTTP Response，這是 Controller 的事。

純函式放 `src/utils/`，讓它可以被其他元件重用、可以單獨測試。DOM 操作放在元件，因為只有元件有資格操作瀏覽器的視窗。Store 不需要知道「CSV 是什麼格式的」這件事。

UTF-8 BOM（`'\uFEFF'`）是一個必要的細節——Excel 在 Windows 上開啟沒有 BOM 的 UTF-8 CSV 會把中文當成亂碼，加 BOM 後 Excel 才知道這是 UTF-8 編碼。

**我學到的原則**

「這個函式可以不依賴任何外部狀態獨立執行嗎？」如果可以，它就是純函式，放 `utils/`。「這個操作需要改變 UI 或操作 DOM 嗎？」如果是，它屬於元件。Store 管的是「應用程式狀態」，不是「UI 行為」或「工具函式」。

**下次遇到類似情況，我會先想到什麼**

看到「產生檔案並下載」這類操作，拆成「生成內容（純函式）」和「觸發下載（UI 行為）」兩步，分開放，不要揉在一起。

---

### 條目 110 — Vite 樣板的 demo 樣式是版面問題的根本原因

**我做了什麼**

排查「MarketView 版面為何只佔畫面左半邊、圖表無法撐滿寬度」的問題，最終發現根本原因在 `src/assets/main.css`——這是 Vite 在建立新專案時自動生成的 demo 頁面樣式，包含對儀表板應用完全有害的 CSS。

**我遇到的問題**

MarketView.vue 和各子元件的 `width: 100%` 設定都正確，但版面死活就是縮在左半邊，各種追加 CSS 都無效。

**我怎麼想通的**

`width: 100%` 的意思是「100% 的父層寬度」，如果父層（`#app`）被限制了，子元素再怎麼設都沒用。追蹤到 `src/assets/main.css`：

```css
/* Vite demo 樣式——對儀表板應用有害的三行 */
#app {
  max-width: 1280px;   /* 限制寬度 */
  margin: 0 auto;      /* 置中，兩側留白 */
}

@media (min-width: 1024px) {
  body {
    display: flex;
    place-items: center;  /* 強制垂直置中，整頁縮在中間 */
  }
  #app {
    grid-template-columns: 1fr 1fr;  /* 強制兩欄 Grid */
  }
}
```

這三段 CSS 是為 Vite 的 HelloWorld demo 頁面設計的，對儀表板應用完全有害。修正方式是把整個 `main.css` 替換為：

```css
@import './base.css';
#app { width: 100%; min-height: 100vh; }
```

**我學到的原則**

新專案開始時，Vite / Vue CLI / Create React App 等腳手架工具會生成一批 demo 用的樣式和元件，這些東西服務的是「讓你看到一個可以運行的初始畫面」，而不是服務你的應用。開始真正開發前，應該先清理這些文件，不然後面的排版問題會越來越難追蹤根本原因。

**下次遇到類似情況，我會先想到什麼**

版面問題無法靠修改元件 CSS 解決時，沿著 DOM 樹往上找父層，從 `#app` → `body` → `html` 逐層確認有沒有意外的寬度限制或 flex/grid 設定干擾。

---

### 條目 111 — spanGaps 與農業資料的斷點：真實資料的空白應該被如實呈現

**我做了什麼**

在 Chart.js 的折線圖設定中，對每條 dataset 設定 `spanGaps: true`，讓圖表在資料缺失的日期（休市、產季外）跨越空白繼續畫線，而不是中斷折線。同時了解「中間有明顯斷口」是正常且被允許的行為。

**我遇到的問題**

查詢高麗菜和火鶴花一年的資料後，圖表中間出現了幾段斷口——某幾個日期區間完全沒有折線。這是 bug 嗎？

**我怎麼想通的**

農業交易資料不是每天都有，斷口有兩種情況：

1. **有 label 但值是 null**：`spanGaps: true` 讓 Chart.js 跨越這個點繼續連線，視覺上看起來是連續的（但那個日期沒有資料點）
2. **連續多天的完全空白**：某個作物整段時間都沒有交易記錄（產季結束、特定市場不交易這個品項），折線在這個區間根本沒有資料，出現明顯斷口是正確的

斷口不是 bug，是「這個作物在這段時間沒有交易」的視覺呈現，反映真實資料情況。如果強制消除斷口（例如用前後的值做線性插值），反而是在捏造不存在的交易資料。

**我學到的原則**

圖表的工作是「讓資料說話」，不是「讓圖表看起來漂亮」。資料有空白，圖表就應該有斷口。`spanGaps: true` 解決的是「偶發性的單天缺失」，對「連續多天的完全缺失」，斷口是正確且誠實的呈現方式。

**下次遇到類似情況，我會先想到什麼**

看到時序圖表有斷口時，先確認是「資料本身就沒有這段時間的記錄」還是「資料存在但沒有被正確載入」。前者是正常的，後者才需要排查。

---

### 條目 112 — SQL Server 統計資料失真：9 萬筆的查詢比 578 萬筆更慢的根本原因

**我做了什麼**

排查 `GetCropsAsync` 在「水果」市場類型下發生 CommandTimeout（30 秒）的生產問題。當天沒有任何程式碼異動，蔬菜（578 萬筆）和花卉（355 萬筆）查詢完全正常，只有水果（9.5 萬筆）失敗。

**我遇到的問題**

資料量最小的市場類型反而最慢，而且同一支查詢、同樣的索引，三個 `@marketType` 值卻產生截然不同的效能結果。直覺上「資料越少應該越快」，但實際上完全相反。

為什麼索引都有了，資料量也最少，還是會逾時？

**我怎麼想通的**

排查分三個階段展開：

第一階段：確認問題在 DB 層。把逾時的 SQL 直接在 SSMS 執行，帶入 `N'Fruit'`——跑了整整 62 秒。確認問題不在 ASP.NET Core 或 EF Core，而在資料庫的執行計畫。

第二階段：排除明顯假設。查三個 MarketType 的資料筆數——水果只有 9.5 萬，蔬菜 578 萬，花卉 355 萬。「水果資料量太大」這個假設直接被推翻，問題方向轉移到「為什麼同一份索引對不同參數值產生不同效能」。

第三階段：確認根本原因。SQL Server 在第一次執行帶參數的查詢時，會根據當時的統計資料（Statistics）編譯一份執行計畫並快取。統計資料記錄的是各欄位值的分佈情況——資料量多少、哪些值出現頻率高——優化器根據這份資訊選擇 Join 策略（Nested Loop vs Hash Join vs Merge Join）。當統計資料與實際資料分佈脫節，優化器會對某些特定的參數值選出極差的執行計畫，其他參數值剛好沒踩到這個壞計畫，就形成了「同一支 SQL，A 快 B 慢」的現象。

修正方式：

```sql
-- 強制重新掃描全表建立正確的統計資料
UPDATE STATISTICS market.AgriProductsTrans WITH FULLSCAN;

-- 清除快取的執行計畫，強迫下次執行時用新統計資料重新編譯
DBCC FREEPROCCACHE;
```

執行後，`N'Fruit'` 的查詢瞬間完成。

**為什麼「自動更新統計資料」開著還是出問題**

確認資料庫的 `is_auto_update_stats_on = 1`，但自動更新有觸發門檻——大型資料表需要累積約 `√(1000 × 總列數)` 筆變更才會觸發。`AgriProductsTrans` 將近 900 萬筆，門檻非常高。SyncWorker 每天寫入的量在門檻以下，但統計資料已經累積到足夠失真、讓優化器做出壞決策的程度。「昨天還好」是因為昨天剛好還在臨界點以內，今天寫入量讓某個分佈統計值跨過了另一個臨界點。

**我學到的原則**

執行計畫的品質依賴統計資料的準確性。統計資料的自動更新是有延遲的，不是即時的。對於每天持續寫入的資料表，「自動更新開著」不代表「執行計畫永遠最佳」——在兩次自動更新之間，統計資料會逐漸失真，直到某次查詢踩到壞計畫才暴露問題。

這類問題的特徵是：沒有程式碼異動、只有特定參數值失敗、資料量和直覺預期不符——三個現象同時出現，根本原因幾乎確定是統計資料或執行計畫快取。

**下次遇到類似情況，我會先想到什麼**

看到「同一支查詢、相同索引，A 參數正常但 B 參數逾時」，第一反應是統計資料失真或執行計畫被快取的壞版本。確認方式：直接在 SSMS 帶入那個失敗的參數值執行，確認是 DB 層的問題。修正方式：`UPDATE STATISTICS ... WITH FULLSCAN` + `DBCC FREEPROCCACHE`。長期維護：對持續寫入的核心資料表，排一個週期性的 SQL Server Agent Job 強制更新統計資料，不依賴自動更新的門檻機制。

---

### 條目 113 — EF Core 參數型別預設值：nvarchar(4000) 讓索引失效的根本原因

**我做了什麼**

在排查 `GetCropsAsync` 水果逾時問題的過程中，發現 `OPTION (RECOMPILE)` 無法解決問題，最終確認根本原因是 EF Core 對 `string` 型別參數的預設行為：不管欄位實際定義是 `nvarchar(20)`，EF Core 統一送出 `nvarchar(4000)`。改用兩段式查詢（先取 `MarketCode` 清單，再用 `IN` 查 `AgriProductsTrans`）繞開型別不符問題，查詢恢復正常。

**我遇到的問題**

同一支 SQL：

- SSMS 帶入字面值 `N'Fruit'` → 約 3 秒，正常
- App 帶入參數 `@p0` → 30 秒逾時

兩者的 SQL 文字完全相同，差異只有「字面值」vs「參數」。`OPTIMIZE FOR UNKNOWN` 和 `OPTION (RECOMPILE)` 都沒有解決問題。為什麼改成參數就掛掉？

**我怎麼想通的**

從 log 看到關鍵差異：

```
-- SSMS 字面值
WHERE m.MarketType = N'Fruit'

-- App 參數
Parameters=[p0='?' (Size = 4000)]
WHERE m.MarketType = @p0
```

`MarketType` 欄位是 `nvarchar(20)`，但 EF Core 對 `string` 型別的參數，預設一律送 `nvarchar(4000)`。型別不符有兩個後果：

第一，SQL Server 必須對每一列做隱式型別轉換（把欄位值從 `nvarchar(20)` 轉成 `nvarchar(4000)` 再比對），這讓索引無法有效使用——索引是對 `nvarchar(20)` 值建立的，參數型別不同，優化器選擇 scan 而不是 seek。

第二，`RECOMPILE` 重新編譯也無濟於事，因為問題不在計畫快取，而在每次執行時的型別轉換本身。SSMS 傳字面值，SQL Server 直接用值做比對，不需要型別轉換，所以快。

**解法：兩段式查詢**

把 `MarketType → MarketCode` 的翻譯拆成獨立的 Step 1：

```csharp
// Step 1: MarketInfos 是小表，幾筆，nvarchar(4000) 的成本可以忽略
var marketCodes = await _context.MarketInfos
    .Where(m => m.MarketType == marketType)
    .Select(m => m.MarketCode)
    .ToListAsync();

// Step 2: 用具體的 MarketCode 值查 AgriProductsTrans
// EF Core 產生 IN (@marketCodes1, @marketCodes2, ...) 且型別為 nvarchar(20)
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

Step 1 對小表做型別轉換，成本可以忽略。Step 2 的 `IN` 清單是從資料庫取回的真實 `MarketCode` 值，EF Core 會正確推斷型別為 `nvarchar(20)`，不再有型別不符的問題。

**這個解法的已知取捨**

兩段式代表兩次 SQL 往返，且 `IN` 清單的大小等於該 MarketType 下的市場數量（蔬菜 20 個、水果 5 個、花卉 20 個）。原始單一 JOIN 查詢如果型別正確，理論上效能更好（一次往返、資料庫端做完所有事）。長期來看，補 `MarketInfos.MarketType` 的索引才是根本解，加完索引後原始 LINQ JOIN 版恢復正常的可能性很高。

**我學到的原則**

EF Core 對 `string` 型別參數一律送 `nvarchar(4000)`，不管對應欄位的實際定義是多少。在欄位本身有索引、且欄位長度遠小於 4000 的情況下，這個預設行為會讓索引失效，造成隱式 scan。

這個問題的診斷特徵是：「SSMS 字面值快，App 參數慢」——兩者 SQL 文字相同但效能截然不同，排查方向應該先看 log 裡的參數型別（`Size`），確認是否和欄位定義不符。

**下次遇到類似情況，我會先想到什麼**

看到「SSMS 快、App 慢」且排除了計畫快取的影響，立刻查 log 裡的 `Parameters=[... (Size = ???)]`，對比欄位實際的 `nvarchar` 長度。如果 `Size` 是 4000 但欄位是短字串，型別不符就是根本原因。短期解法是兩段式查詢；長期解法是補齊相關欄位的索引，讓優化器有足夠資訊選出正確計畫。

---

### 條目 114 — DbInitializer vs Migration InsertData：種子資料應該放在哪裡

**我做了什麼**

在建立 RBAC 骨架後，需要寫入初始資料（NavModules 模組清單 + Guest/Admin 角色 + RoleModulePermissions 權限記錄）。面臨兩個選擇：把資料寫進 Migration 的 `migrationBuilder.InsertData()`，或另外建立獨立的 `DbInitializer` 類別在程式啟動時執行。選擇了後者，並在 `Web/Program.cs` 的 `builder.Build()` 之後、middleware 設定之前呼叫。

**我遇到的問題**

起初不確定為什麼已經有 Migration 機制，還需要另外建一個 Seed 腳本——感覺像是重複的工作。

**我怎麼想通的**

關鍵在於理解 Migration 的職責邊界。Migration 管的是**資料庫結構（Schema）的版本**，每一個 Migration 檔案對應一個歷史時間點的結構變化。如果把資料塞進去：

```sql
-- 三個月後的 Migration 歷史長這樣
0001_CreateMarketTables
0002_CreateWeatherTables
0003_CreateRBACTables
0004_SeedInitialModules       ← 塞資料
0005_AddFoodSafetyTable
0006_UpdateModuleNameWeather  ← 只是改一個模組名稱，卻要開 Migration？
0007_AddPetModule             ← 新增一筆資料，又開一個？
```

Migration 的歷史紀錄就開始說兩種故事（結構變化和資料變化），rollback 一個 Migration 時資料狀態也很難預測。`DbInitializer` 的優點是：

```csharp
// 每次啟動時執行，邏輯自己控制
if (!context.NavModules.Any()) return;   // 冪等：有就跳過，沒有才寫入

context.NavModules.AddRange(/* 初始模組 */);
context.SaveChanges();
```

未來新增模組？直接改這個檔案，不碰 Migration。Portfolio 環境重建？`dotnet ef database update` 建表，啟動時自動 Seed，不需要任何額外步驟。

**我學到的原則**

Migration 管 Schema，`DbInitializer` 管初始資料，兩者職責分離。可能會改動的初始資料（模組清單、角色設定）屬於業務邏輯，不屬於資料庫結構，應放在可以靈活修改的 Seed 腳本裡，而不是版本化的 Migration 歷史中。

**下次遇到類似情況，我會先想到什麼**

問自己：「這份資料在系統正式上線後會不會被業務需求改動？」如果答案是「可能」，那就放 `DbInitializer`，不要放 Migration。Migration 只處理「資料庫結構的一次性變更」。

---

### 條目 115 — System.Reflection.Module 命名衝突：.NET 內建型別的隱性陷阱

**我做了什麼**

將 RBAC 的導覽模組 Entity 命名為 `Module`，新增到 `TaiwanAgri.Core.Entities` 命名空間後，`CoreDbContext.cs` 出現編譯錯誤：

```
CS0104: 'Module' 是 'TaiwanAgri.Core.Entities.Module' 與 'System.Reflection.Module' 之間模稜兩可的參考
```

**我遇到的問題**

命名空間已經很明確地寫了 `TaiwanAgri.Core.Entities`，為什麼編譯器還是找不到正確的型別？

**我怎麼想通的**

`System.Reflection.Module` 是 .NET 基礎類別庫的內建型別，在啟用 `<ImplicitUsings>` 的現代 .NET 專案中，`System.Reflection` 會被隱性引入，所以即使你沒有明確寫 `using System.Reflection`，`Module` 這個名稱還是存在於解析範圍內。當自訂類別和內建型別同名，編譯器無法決定用哪個，就會報 CS0104。

最乾淨的解法是改名，而不是到處加完整命名空間前綴。`NavModule`（Navigation Module）語意更清楚——一看就知道這是「導覽用的模組」，而不是軟體架構中的「模組」這個抽象概念。

**我學到的原則**

自訂類別命名時要注意 .NET BCL（基礎類別庫）的常見型別名稱。`Module`、`Task`、`Action`、`Type`、`Path`、`Console`、`Stream` 這類通用詞彙都有風險，稍微加個前綴（`Nav`、`App`、`Market`）讓語意更具體，同時也能避免命名衝突。

**下次遇到類似情況，我會先想到什麼**

看到 CS0104 報「是 X 與 Y 之間模稜兩可的參考」，先確認 Y 是不是 .NET 內建型別——如果是，直接改自訂類別的名稱，不要試圖用 `using alias` 或完整命名空間來繞開，那只是掩蓋問題而不是解決問題。

---

### 條目 116 — EF Core 自參照設計：一張表表達父子層級的關係

**我做了什麼**

`NavModule` 同時存放頂層模組（TopNav 頁籤）和子功能（SideNav 清單），用 `ParentId` 自參照來區分層級。在 Entity 裡宣告兩個導覽屬性，在 `OnModelCreating` 裡設定關聯，EF Core 自動推導出父層和子層的對應關係。

**我遇到的問題**

一開始不確定 EF Core 怎麼「知道」`Parent` 和 `Children` 分別指向哪個方向——同一個型別怎麼能同時是父層和子層？

**我怎麼想通的**

EF Core 用命名慣例和型別推導：

```csharp
public int? ParentId { get; set; }              // FK，nullable = 可選（頂層無父層）
public NavModule? Parent { get; set; }           // 型別是 NavModule → 多對一，指向父層
public ICollection<NavModule> Children { get; set; } = new List<NavModule>(); // 型別是 ICollection<NavModule> → 一對多，指向子層
```

`ParentId` 是 FK，`Parent` 的型別和自身相同，EF Core 推導出「這個實體的 ParentId 欄位指向同型別的另一筆記錄」，`Children` 則是另一方向的導覽——「有哪些同型別的實體以我為父層」。

`OnModelCreating` 明確聲明：

```csharp
entity.HasOne(n => n.Parent)
      .WithMany(p => p.Children)
      .HasForeignKey(n => n.ParentId)
      .OnDelete(DeleteBehavior.Restrict);  // 有子功能的模組不能直接刪除
```

`Restrict` 而不是 `Cascade`，是因為頂層模組被刪除時，應該先確認子功能都已處理，不應靜默地把子功能也一起刪掉。

**我學到的原則**

兩層固定深度的層級關係（頂層 → 子層），自參照是最乾淨的選擇。一張表，Permission 表的 FK 也指向同一張，查詢邏輯統一。超過兩層、或深度不固定時，才需要考慮 Closure Table 或 Path Enumeration 等更複雜的設計。

**下次遇到類似情況，我會先想到什麼**

看到「需要表達父子層級」的需求時，先問深度：只有兩層就用自參照，三層以上才評估 Closure Table。自參照的 `Children` 記得初始化 `= new List<T>()`，否則未載入時是 null，`.Count` 會拋 NullReferenceException。

---

### 條目 117 — AddRoles<IdentityRole>()：讓 RoleManager 進入 DI 容器

**我做了什麼**

`DbInitializer.SeedAsync` 需要 `RoleManager<IdentityRole>` 來建立 Guest / Admin 角色並取得對應的 RoleId。在 DI 容器解析時拋出「找不到 `RoleManager<IdentityRole>` 的服務」的錯誤。

**我遇到的問題**

已經呼叫了 `AddDefaultIdentity<ApplicationUser>()`，為什麼 `RoleManager` 不可用？

**我怎麼想通的**

`AddDefaultIdentity` 是 Identity 的精簡版本，它預設**不**包含角色管理功能（`RoleManager`、`RoleStore`、`IdentityRole`）——這個設計是刻意的，因為很多應用不需要角色系統，加上去只會增加複雜度。

要啟用角色功能，需要在 Identity builder 鏈上加一個擴充方法：

```csharp
builder.Services.AddDefaultIdentity<ApplicationUser>(options => ...)
    .AddRoles<IdentityRole>()          // ← 這行把 RoleManager 和所有角色相關服務加進 DI
    .AddEntityFrameworkStores<ApplicationDbContext>();
```

`AddRoles<IdentityRole>()` 做的事包括：把 `RoleManager<IdentityRole>`、`IRoleStore<IdentityRole>`、`RoleValidator<IdentityRole>` 都加進 DI 容器，讓後面注入 `RoleManager<IdentityRole>` 時能正確解析。

**我學到的原則**

ASP.NET Core Identity 採用「按需啟用」的設計，基礎功能（使用者認證）和進階功能（角色管理）是分開的。需要用 `RoleManager` 的地方（DbInitializer、NavService），都需要確認 Identity 有加上 `.AddRoles<IdentityRole>()`，否則 DI 解析會在 runtime 而不是 compile time 爆掉，不容易及早發現。

**下次遇到類似情況，我會先想到什麼**

看到 DI 找不到 `RoleManager<IdentityRole>` 的服務，立刻去 `Program.cs` 的 Identity 註冊部分確認有沒有 `.AddRoles<IdentityRole>()`。這比找其他原因快得多。

---

### 條目 118 — NavService 三段式查詢：permittedModuleIds 的具現化時機

**我做了什麼**

`NavService.GetNavModulesAsync` 需要三次 DB 查詢：（1）撈出有權限的 ModuleId 清單，（2）用這個清單撈頂層模組，（3）再用這個清單撈子功能。第一段查詢如果不先具現化，就會在後續兩次查詢時各自觸發一次 SQL，等於同一份資料打了三次 DB。

**我遇到的問題**

起初寫成：

```csharp
var permittedModuleIds = _context.RoleModulePermissions
    .Where(rmp => rmp.RoleId == targetRoleId && rmp.CanView)
    .Select(rmp => rmp.ModuleId);   // ← IQueryable，還沒執行

// 查頂層
var navModules = await _context.NavModules
    .Where(nm => permittedModuleIds.Contains(nm.Id))   // ← 第一次執行 SQL
    .ToListAsync();

// 查子層
var childNavModules = await _context.NavModules
    .Where(cnm => permittedModuleIds.Contains(cnm.Id)) // ← 第二次執行 SQL！
    .ToListAsync();
```

**我怎麼想通的**

EF Core 的 `IQueryable` 是「延遲執行」的——它只是一個查詢描述，不是資料。每次把 `IQueryable` 當作 `Contains` 的參數傳入另一個查詢，EF Core 就會把它翻譯成子查詢（subquery），在那個時刻打一次 DB。

解法是在第一段查詢後就 `ToListAsync()`，把結果具現化為 `List<int>`：

```csharp
var permittedModuleIds = await _context.RoleModulePermissions
    .Where(rmp => rmp.RoleId == targetRoleId && rmp.CanView)
    .Select(rmp => rmp.ModuleId)
    .ToListAsync();   // ← 立刻執行，結果存在記憶體

// 後續兩次 Contains 都是記憶體操作（IN 清單），只各打一次 DB
```

`ToListAsync()` 是 EF Core 查詢邊界的明確標誌：之前是 SQL 世界，之後是 C# 記憶體世界。

**我學到的原則**

`IQueryable` 被使用多次時，一定要問「這個查詢會被執行幾次？」。如果同一個 `IQueryable` 出現在兩個不同查詢的 `Contains` 或 `Any` 裡，就是兩次 DB 往返。解法是提前 `ToListAsync()` 具現化，之後的操作都在記憶體中完成。

**下次遇到類似情況，我會先想到什麼**

看到一個 `IQueryable` 變數被傳入多個 `.Where(x => someQuery.Contains(x.Id))` 時，立刻加 `ToListAsync()` 把它具現化。這個細節在 N+1 問題的討論中很少被提到，但卻是同樣性質的反模式——同一份資料因為「查詢描述被重複使用」而打了多次 DB。

---

### 條目 119 — 兩個 DTO 而不是一個：型別即文件的具體實踐

**我做了什麼**

回傳導覽模組的 API 需要兩層結構：頂層模組有 `Children` 陣列，子功能沒有。面臨選擇：一個可以 nullable 遞迴的 `NavModuleDto`，還是兩個分開的 `NavModuleDto` + `NavChildDto`。

**我遇到的問題**

一個 DTO 的方案看起來程式碼更少，直覺上「更簡單」，為什麼要多寫一個 `NavChildDto`？

**我怎麼想通的**

問題的關鍵不是程式碼行數，而是型別在說什麼：

```csharp
// 一個 DTO（Children 是 nullable）
public class NavModuleDto
{
    public List<NavModuleDto>? Children { get; set; }   // null 代表子層？還是「還沒載入」？型別不說明
}

// 兩個 DTO
public class NavModuleDto  { public List<NavChildDto> Children { get; set; } = new(); }
public class NavChildDto   { /* 沒有 Children —— 型別本身說「這層不能再展開」 */ }
```

`NavChildDto` 沒有 `Children` 屬性，不是疏漏，是設計。任何讀到這個型別的人都能立刻知道：子功能不會有子子功能。這種「型別即文件」的設計在後端的 `IEnumerable<NavChildDto>` 出現時，就已經在說明系統的結構，不需要額外的注釋。

更重要的是未來擴充彈性：如果子功能需要加 `BadgeCount`（紅點通知數）而頂層不需要，兩個 DTO 方案只改 `NavChildDto`，一個 DTO 方案只能在唯一的型別上加 `int? BadgeCount`，讓型別對頂層模組也撒謊（「我有 BadgeCount 欄位，但永遠是 null」）。

**我學到的原則**

「兩個地方看起來相似，是否要合併成一個型別」這個問題，答案取決於：「這兩個概念的**演化方向**是否相同？」如果頂層模組和子功能未來可能有不同的屬性需求，它們就不是同一個概念，不應強行合併。類似的決策也出現在後端 DTO 命名（`WorkerResponses` vs `ApiResponses`）：分開命名是因為服務對象不同，演化方向不同。

**下次遇到類似情況，我會先想到什麼**

看到「這兩個型別結構現在一樣，要不要合併」時，先想「三個月後它們還會一樣嗎？」如果不確定，保持分開。合併兩個型別容易，但把一個已被廣泛使用的型別拆成兩個要付出更大的代價。

---

### 條目 120 — Vite Proxy：前端開發時的 API 轉發機制

**我做了什麼**

前端 `axios` 呼叫 `/api/nav/modules`，在 `npm run dev` 環境下被 Vite dev server 攔截，回傳的是 `index.html`（前端的 HTML entry point）而不是後端 API 的 JSON 回應。設定 `vite.config.ts` 的 `server.proxy` 後問題解決。

**我遇到的問題**

瀏覽器 Network 顯示 `/api/nav/modules` 的狀態碼是 200，但 Response 內容是 HTML——API 呼叫「成功」了，但拿到的是錯誤的東西。

**我怎麼想通的**

Vite dev server 在開發時作為一個 HTTP 伺服器，它攔截所有對 `localhost:5173`（前端 port）的請求。對它不認識的路徑（如 `/api/*`），它沒有轉發的指示，就直接回傳前端的 SPA entry point（`index.html`）——這是 SPA 的標準行為，用來支援 client-side routing。問題是前端的 `axios` 以為拿到的是 JSON，卻拿到一個 HTML 字串，解析失敗。

解法是告訴 Vite：凡是符合 `/api` 前綴的請求，轉發給後端：

```typescript
// vite.config.ts
server: {
  proxy: {
    '/api': {
      target: 'https://localhost:7147',  // .NET 後端的 port
      changeOrigin: true,                // 修改 Host header
      secure: false,                     // 不驗證本地開發憑證
    }
  }
}
```

> **重要**：`vite.config.ts` 的變更不支援 hot reload，一定要重啟 `npm run dev` 才能生效。

**我學到的原則**

前後端分離開發時，前端的請求路徑和後端路由之間存在一個「轉發層」。在正式部署時這個轉發通常由 nginx 或 API Gateway 處理；在本地開發時，前端的 bundler（Vite、webpack、vite）自帶的 proxy 設定承擔這個角色。沒有設定 proxy，前端打 `/api/*` 就是打自己的 dev server，不是後端。

**下次遇到類似情況，我會先想到什麼**

前端 API 呼叫狀態碼是 200 但拿到 HTML——立刻想到：「這個請求有沒有被轉發到後端？」先看 `vite.config.ts` 有沒有 `server.proxy` 設定，再確認 proxy target 的 port 和後端的 launchSettings 是否吻合。

---

### 條目 121 — RBAC 模組可見度繼承：父層 AND 子層的查詢設計

**我做了什麼**

在 RBAC 的設計討論中，確認了「父層關閉 → 子功能全部不可見」的繼承語意，並在 `NavService` 的查詢邏輯中同時過濾頂層模組和子功能，確保兩者都必須通過 `permittedModuleIds` 的驗證。

**我遇到的問題**

討論過三個選項：（1）DB 存的時候就算好繼承關係、（2）API 層做 AND 計算、（3）前端做過濾。起初不清楚為什麼選項 3（前端過濾）是不可接受的。

**我怎麼想通的**

選項 3 的問題是安全性，不是效能：如果前端過濾，後端 API 還是把「父層 false、子層 true」的資料都回傳給前端，只是靠 JavaScript 把它藏起來。使用者打開瀏覽器的 DevTools → Network，就能看到所有模組的完整清單，包括應該被隱藏的付費功能或管理功能。

選項 2（API 層）才是正確的做法：沒有權限的資料在後端就被過濾掉，根本不出現在 HTTP response 裡。`NavService` 的查詢同時驗證頂層和子功能是否在 `permittedModuleIds` 清單內：

```csharp
// 子功能查詢：ParentId 存在（是子層）AND 父層有權限（topLevelIds）AND 自身也有權限（permittedModuleIds）
var childNavModules = await _context.NavModules
    .Where(cnm => cnm.ParentId != null 
               && topLevelIds.Contains(cnm.ParentId!.Value)
               && permittedModuleIds.Contains(cnm.Id))
    .ToListAsync();
```

這樣確保了：頂層關閉 → 子功能不出現在 `topLevelIds` → 子功能被過濾掉，語意等同於「父層 AND 子層都必須有 `CanView = true`」。

**我學到的原則**

權限判斷永遠在後端做，前端只負責渲染。這個原則適用於所有需要存取控制的場景：不論是模組可見度、按鈕狀態、還是資料列的顯示，後端負責「決定你能看什麼」，前端負責「把能看到的東西呈現出來」。前端的隱藏只是 UI 行為，不是安全邊界。

**下次遇到類似情況，我會先想到什麼**

看到「這個過濾邏輯要放在前端還是後端」的問題，如果過濾的是「哪些資料使用者有權存取」，答案永遠是後端。如果過濾的是「哪些資料要顯示在這個視圖（但資料本身是使用者有權存取的）」，才是前端的責任。

---

### 條目 122 — SemaphoreSlim：用閘門控制並發，而不是放棄並發

**我做了什麼**

`AgriProductsTransSyncWorker` 用 `Task.WhenAll` 對農業部 API 同時送出 20 個 HTTP 請求，導致 API 承受不住，大多數請求 timeout 失敗。修正方案是引入 `SemaphoreSlim(5)`，把同時進行中的請求數限制在 5 個以內。

**我遇到的問題**

面臨兩個選項：（1）保留 `Task.WhenAll` 並加 `SemaphoreSlim`；（2）改回 `foreach` 加 `Task.Delay(500)`。第二個選項看起來更簡單，直覺上「一個一個打就不會爆掉」。但直接捨棄並發等於用最保守的方式解決問題，而問題的根源只是「並發數太高」，不是「並發本身有問題」。

**我怎麼想通的**

`SemaphoreSlim` 是一個計數式閘門：初始化時指定「最多幾個人同時進門」，每個進去的人拿走一個名額（`WaitAsync`），出來時歸還（`Release`）。它解決的是「控制瞬間並發數」，而不是「消滅並發」。

```csharp
// semaphore 宣告在 for 迴圈外，是語意上的刻意選擇
// 代表這個閘門是整個 Worker 的全域限流機制，不是每天重建的局部機制
var semaphore = new SemaphoreSlim(5);

var rawResults = await Task.WhenAll(marketInfos.Select(async market =>
{
    await semaphore.WaitAsync(stoppingToken);   // 沒有名額就在這裡等
    try
    {
        // ... HTTP 請求 ...
    }
    finally
    {
        semaphore.Release();   // 不管成功還是失敗，一定要還名額
    }
}));
```

`Release()` 放在 `finally` 是關鍵：如果放在 `try` 裡，一旦請求拋例外，`Release()` 就不會執行，名額永遠不回來，最終所有執行緒都卡在 `WaitAsync` 等一個永遠不會出現的名額——系統死鎖。`finally` 保證「不管發生什麼，名額一定歸還」。

另一個細節是 `catch (Exception ex)` 而非 `catch (TaskCanceledException ex)`。只攔 timeout 的話，`HttpRequestException`（網路斷線）或 `JsonException`（API 回傳格式異常）都會讓 `Task.WhenAll` 把整個任務炸掉，不只是記錄那個市場失敗，而是當天所有市場的處理全部中斷。攔所有例外讓每個市場的失敗都被隔離在自己的 Task 裡。

**我學到的原則**

「並發造成問題」不等於「並發本身是問題」。先診斷問題的根源（是並發數過高、還是並發本身不安全），再選擇對應的解法。`SemaphoreSlim` 是「並發數過高」的解法；改回 sequential 是「並發本身不安全」的解法。把兩個不同問題的解法搞混，會讓代碼付出不必要的效能代價。

**下次遇到類似情況，我會先想到什麼**

看到 `Task.WhenAll` + 外部 API 的組合，先問「這個 API 能承受同時幾個請求？」如果答案不是「不限制」，就加 `SemaphoreSlim`。`Release()` 永遠放 `finally`，這是 `SemaphoreSlim` 的鐵律。

---

### 條目 123 — 同步狀態推進的邊界：什麼叫「這天完成了」

**我做了什麼**

修正 `AgriProductsTransSyncWorker` 的 `LastSyncedDate` 推進邏輯：原本是不管有沒有市場失敗都推進，改為只有全部市場成功才推進，並加入「成功的資料先存，失敗的下次補」的策略，以及「落後 5 天自動強制推進」的安全閥。

**我遇到的問題**

修完之後面臨一個新問題：如果農業部 API 整體故障，Worker 每天重試都失敗，`LastSyncedDate` 永遠不推進，系統卡死在同一天。這是「修了一個 Bug，引入了另一個風險」的典型情境。

需要在「有失敗就不推進（資料正確性）」和「不能永久卡死（系統可用性）」之間找到一個平衡點。

**我怎麼想通的**

這個問題本質上是「一致性 vs 可用性」的取捨，在任何需要推進進度的增量同步系統裡都會出現。解法是分兩層處理：

第一層（正常情況）：有失敗 → 不推進 `LastSyncedDate`，下次重試。這是「資料正確性優先」的設計，給 API 恢復的機會。

第二層（長期故障）：落後超過 N 天 → 強制推進並留下缺口記錄。這是「系統可用性保底」的設計，防止無限卡死。

```csharp
var daysBehind = yesterdayDate.DayNumber - currentDate.DayNumber;
if (daysBehind >= 5)
{
    // Warning 等級記錄：這是一個需要人工關注的異常狀態
    _logger.LogWarning("{Date} 已落後 {Days} 天仍有失敗，強制推進 LastSyncedDate，資料存在缺口",
        currentDate, daysBehind);
    lastSyncState.LastSyncedDate = currentDate;
    lastSyncState.UpdatedAt = DateTime.UtcNow;
    await dbCore.SaveChangesAsync(stoppingToken);
}
```

N 選 5 是一個考量了農業部 API 維護週期的判斷：週末或連假可能 2-3 天無回應，3 天的閾值可能誤觸；5 天幾乎可以確定是長期故障而非短暫維護。

「成功的資料先存」的決策也是類似的思路：下次重試時，`existingKeys` HashSet 會擋掉已存過的資料，不會重複寫入，所以先存不會造成資料問題；而如果全部 rollback，已成功抓回的資料就浪費了一次 API 請求。能保留的先保留，有缺漏的留下記錄，符合「最終一致性優先於嚴格一致性」的增量同步設計原則。

**我學到的原則**

任何「有進度追蹤」的增量同步系統，都需要明確定義三件事：（1）什麼條件才算「這天完成」，（2）失敗時進度要不要退回，（3）長期故障時如何避免卡死。這三個問題如果在設計時沒有答案，出問題的時候會很難追查。安全閥（強制推進機制）的閾值設定不是精確科學，是對外部系統故障模式的主觀判斷，值得在代碼注釋中說明選擇這個數字的理由。

**下次遇到類似情況，我會先想到什麼**

看到增量同步的進度推進邏輯，馬上問：「如果外部系統長期故障，這段代碼的行為是什麼？」如果答案是「卡死」，就需要一個安全閥。安全閥觸發時一定要用 `Warning` 或更高等級記錄，讓運維人員知道有資料缺口需要關注。

---

### 條目 124 — ILogger 注入服務層：可觀測性是服務的一部分

**我做了什麼**

在 `NavService` 加入 `ILogger<NavService>` 依賴注入，用於記錄「已登入用戶缺少 Role Claim，回退至 Guest 權限」的警告。這是 P1 null guard 修正的配套工作。

**我遇到的問題**

原本 `NavService` 沒有 logger，只有 `RoleManager` 和 `CoreDbContext` 兩個依賴。加 null guard 時，只是靜默回退 Guest 也能達到「不崩潰」的效果——為什麼一定要加 logger？

**我怎麼想通的**

靜默回退的問題在於：當這個異常狀態真的發生時（使用者的 Role Claim 缺失），系統表面上正常運作（Navbar 顯示），但沒有任何地方記錄了「這件事發生過」。調查問題時，你只知道某個使用者反映 Navbar 權限好像不對，但完全沒有線索。

`ILogger` 注入服務層是 ASP.NET Core 的標準模式，成本極低：

```csharp
private readonly ILogger<NavService> _logger;

public NavService(
    RoleManager<IdentityRole> roleManager,
    CoreDbContext coreDbContext,
    ILogger<NavService> logger)   // DI 容器自動提供
{
    _roleManager = roleManager;
    _context = coreDbContext;
    _logger = logger;
}
```

`ILogger<T>` 的泛型參數 `T` 是 category name，也就是這個 logger 的身份標記。在結構化日誌系統（如 Serilog、Application Insights）裡，category name 是過濾和搜尋 log 的關鍵維度——搜尋 `NavService` 就能找到所有來自這個服務的 log，不需要靠關鍵字比對。

`IsNullOrWhiteSpace` 的選擇比 `IsNullOrEmpty` 更嚴謹，是因為 Token 解析異常時可能產生純空白字串的 Claim，而不是真正的 `null`。`IsNullOrWhiteSpace` 一次攔截兩種情況。

**我學到的原則**

服務層的可觀測性（logging）和業務邏輯是同等重要的設計考量，不是「有空再加」的附加工作。異常路徑（fallback、error handling）尤其需要 log，因為這些路徑發生時往往不會有任何外部可見的異常，只有 log 才能告訴你「這件事發生過，而且是什麼時候」。`Warning` 等級是「系統還在運作，但有值得注意的異常狀態」——這個場景正好符合：Navbar 仍然顯示，但以非預期的方式（Guest 權限而非使用者自己的權限）顯示。

**下次遇到類似情況，我會先想到什麼**

設計 fallback 邏輯時，先問：「如果這個 fallback 真的被觸發，我怎麼知道？」如果答案是「不知道」，就加一行 `LogWarning`。Fallback 是正確的設計，但靜默的 fallback 是隱患。

---

### 條目 125 — Serilog 檔案日誌：從「跑完就消失」到「可追查的歷史」

**我做了什麼**

在 `Worker/Program.cs` 加入 Serilog 的 `WriteTo.File` sink，讓每天的執行 log 以滾動方式存在 `logs/` 資料夾，保留最近 60 天。

**我遇到的問題**

Worker 的問題是：它跑完就關掉終端機視窗，控制台 log 消失了。如果某天農業部 API 出問題導致資料缺漏，事後調查時完全沒有記錄可以查。這是「只有 Console logger」的根本限制——log 的生命週期和進程一樣短。

**我怎麼想通的**

Serilog 的設計哲學是「sink 化」：日誌寫到哪裡，是一個配置問題，而不是代碼問題。同一份 log，可以同時寫到 Console（開發時即時查看）和 File（生產時持久保存），兩個 sink 並列不互斥。

```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()              // 開發時即時查看
    .WriteTo.File(
        path: "logs/worker-.log",   // 路徑中的 "-" 會被 Serilog 替換為日期
        rollingInterval: RollingInterval.Day,      // 每天一個新檔案：worker-20260519.log
        retainedFileCountLimit: 60,               // 超過 60 個檔案時自動刪最舊的
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
    )
    .CreateLogger();
```

`path: "logs/worker-.log"` 裡的 `-` 不是筆誤，是 Serilog 的命名慣例——滾動檔案的日期部分會被插在這個位置，產生 `worker-20260519.log` 這樣的檔名。

`retainedFileCountLimit: 60` 是磁碟管理的平衡點：60 天的 log 窗口足夠覆蓋大多數的事後調查，而且每個 `.log` 檔案大小通常只有幾 KB 到幾百 KB，60 個檔案的總磁碟佔用很低。不設限制（`null`）在生產環境是危險的——log 會無限累積直到磁碟滿。

`builder.Logging.ClearProviders()` 後 `builder.Logging.AddSerilog()` 是把 ASP.NET Core 的預設 logging infrastructure 接管給 Serilog，讓所有透過 `ILogger<T>` 寫出的 log 都走 Serilog 的 sink，包括 EF Core 的 SQL log 和框架自身的 log。

**我學到的原則**

Production-grade 的系統需要持久化的 log。Console logger 是開發工具，不是運維工具。決定「要不要加檔案 log」的標準問題是：「如果這個服務出問題，而我當時不在電腦前，我有辦法事後重建當時發生了什麼嗎？」如果答案是「沒有辦法」，就需要持久化 log。`retainedFileCountLimit` 這類「上限」設定在系統設計中是一個通用原則：任何可以無限成長的資源，都應該設定明確的上限和清理策略，否則遲早耗盡。

**下次遇到類似情況，我會先想到什麼**

新加一個背景服務或定時任務時，第一件事確認：「這個服務的 log 會存在哪裡，我事後可以看嗎？」如果只有 Console，評估是否需要加 Serilog File sink。`retainedFileCountLimit` 一定要設定，不要讓 log 無限累積。

---

### 條目 126 — 診斷閉環：索引補完後的驗證，以及「確認不改」的工程意義

**我做了什麼**

這個條目是條目 112（統計資料失真）和條目 113（nvarchar(4000) 參數型別不符）的後續驗證。5/15 的診斷結束時，我找到了根本原因（nvarchar(4000)）、找到了繞過解法（兩段式），但也識別出一個殘留的技術債：`MarketInfos.MarketType` 欄位缺少索引，兩段式的第一段走的是全表掃描。PR #024 補上這個索引後，依計畫驗證原始 JOIN 版能否恢復，結論是不能。

**我遇到的問題**

索引補完後，原本以為「型別不符讓索引失效」這個問題應該可以被索引本身「繞過」——有了索引，SQL Server 應該更傾向使用它，即使參數型別不完全符合。但驗證結果是仍然逾時，EF Core SQL log 又出現了 `Parameters=[p0='?' (Size = 4000)]`。

**我怎麼想通的**

索引的作用是「告訴 SQL Server 這個欄位可以用更快的方式查找」，但使用索引的前提是「查詢條件和索引欄位的型別必須相容」。`nvarchar(4000)` 的參數要比對一個 `nvarchar(20)` 的欄位，SQL Server 的型別相容規則不允許直接使用索引——它必須做隱式型別轉換，而轉換本身就讓索引失效。索引的存在與否不改變這個型別層面的根本限制。

這個驗證雖然沒有帶來「修好了」的結果，但它帶來了「確認判斷是正確的」這個價值。5/15 的診斷是在時間壓力下推斷的，PR #024 的驗證在沒有壓力的情況下把這個推斷變成了有實驗依據的結論。

另一個值得記錄的是「確認不改」本身是一個工程動作。技術上可以在 Entity 設定中加上 `.HasColumnType("nvarchar(20)")` 來修正 EF Core 的參數型別送出行為，這樣 JOIN 版就能恢復。但這需要修改 Entity、跑額外 Migration、驗證資料長度，而現有的兩段式已被測試有效、效能可接受。在技術債代價 vs 收益的評估下，「確認不改」是比「因為能改就改」更成熟的判斷——差別在於一個是有意識的決策，一個是習慣性的行動。

**我學到的原則**

診斷工作的閉環不只是「找到原因、修好問題」，也包括「在非緊急情境下驗證緊急情境下的推斷是否正確」。5/15 的診斷是在故障排查的壓力下進行的，當時的結論是推斷，不是實驗結果。PR #024 的驗證把它升格為確認。這個習慣讓「技術債的記錄」從「我猜這是原因所以這樣做」變成「我驗證過這是原因，這是我做的決策和依據」，後者在面試或文件回顧時說服力更強。

**下次遇到類似情況，我會先想到什麼**

緊急修復後，如果當時有識別出「這是暫時解法、根本原因還需要補完」，排出時間做驗收閉環。即使驗收結論是「暫時解法就是長期解法」，記錄驗證過程也比讓它停留在猜測狀態更好。

---

### 條目 127 — EF Core Migration 的多專案架構：-Project 和 -StartupProject 的分工邏輯

**我做了什麼**

在多專案的解決方案裡執行 EF Core Migration，踩到「Your target project doesn't match your migrations assembly」的錯誤，透過加入 `-Project` 和 `-StartupProject` 參數解決。

**我遇到的問題**

直接執行 `Add-Migration ... -Context MarketDbContext` 報錯，EF Core CLI 抱怨 target project 和 migrations assembly 不吻合。

**我怎麼想通的**

EF Core 的 Migration 命令需要兩個完全不同性質的定位點，理解它們的職責分工就能理解為什麼需要各自指定。

「把生成的 Migration 檔案存在哪裡？」這是 `-Project` 的責任。它指向包含 `DbContext` 的專案，也就是 `TaiwanAgri.Modules.Market`，Migration 檔案會產生在這個專案的 `Data/Migrations/` 資料夾下。

「執行時從哪裡取得 DI 設定和 Connection String？」這是 `-StartupProject` 的責任。它指向可執行的進入點，也就是 `TaiwanAgri.Worker`，EF Core 需要從這裡找到 `AddDbContext<MarketDbContext>` 的設定和對應的 Connection String。

```
Add-Migration AddMarketInfosMarketTypeIndex
  -Context MarketDbContext
  -Project TaiwanAgri.Modules.Market     ← Migration 存放位置（DbContext 所在）
  -StartupProject TaiwanAgri.Worker      ← DI + Connection String 來源（啟動專案）
```

這兩個定位點分開是架構的必然結果，而不是 EF Core 的設計缺陷。乾淨的模組化架構讓 `DbContext` 定義在業務模組層，啟動和設定屬於應用層，兩個職責分別在不同的 csproj，所以需要分別告訴 EF Core。Package Manager Console 的 Default Project 下拉選單只控制 `-Project`，不控制 `-StartupProject`，這是容易造成誤解的地方。

`Designer.cs` 和更新後的 `MarketDbContextModelSnapshot.cs` 是 EF Core 自動產生的，不需要手動維護，但每次 PR 都應該包含這兩個檔案。`Designer.cs` 記錄這次 Migration 時的 Model 狀態，`ModelSnapshot.cs` 是最新的完整 Schema 快照——它是整個資料庫 Schema 的「代碼版」，是 EF Core 判斷「下一個 Migration 需要產生什麼 SQL」的依據。

**我學到的原則**

多專案架構下，EF Core Migration 的完整命令格式幾乎每次都需要 `-Project` 和 `-StartupProject` 兩個參數。把這個命令格式記錄在專案的 README 或開發文件裡，可以節省每次開新 Migration 時重新查文件的時間。`ModelSnapshot.cs` 每次 PR 都要包含，漏掉它會讓下一個 Migration 產生的 SQL 基準狀態不正確。

**下次遇到類似情況，我會先想到什麼**

多個 csproj 的解決方案裡跑 Migration，直接用完整格式：`Add-Migration [Name] -Context [ContextName] -Project [DbContext所在] -StartupProject [啟動專案]`。遇到「migrations assembly mismatch」報錯，第一反應就是補這兩個參數。

---

### 條目 128 — 「決定不改」也是工程決策：技術債的代價 vs 收益框架

**我做了什麼**

在 PR #024 的決策過程中，面對三個可以讓 JOIN 版走索引的技術解法，最終選擇了「不改代碼，保留兩段式現狀，補充診斷說明」。這個「決定不改」的過程值得記錄，因為它不是因為懶，而是一個有意識的評估結果。

**我遇到的問題**

「明明找到了問題的根本原因（nvarchar(4000) 型別不符），為什麼不直接修掉？」這個問題合理，但它隱含了一個假設：「找到根本原因就應該修根本原因」。這個假設忽略了「修」這件事本身也有代價。

**我怎麼想通的**

把三個選項的代價和收益並列評估，答案就清楚了。

第一個選項是在 Entity 設定中明確宣告欄位型別（`.HasColumnType("nvarchar(20)")`），讓 EF Core 送出正確長度的參數。這讓 JOIN 版可以恢復、代碼最乾淨。但它需要修改 Entity 設定、跑額外 Migration、驗證現有資料裡 `MarketType` 的實際長度沒有超過宣告值——如果有任何資料超長，Migration 會失敗。這是一個代價不低的 schema 變更。

第二個選項是改用 `Database.SqlQueryRaw` 直接寫 SQL 並帶入 query hint。但 5/15 的測試已確認，即使 `RECOMPILE` 也無法解決型別不符問題，而且直接寫 Raw SQL 會失去 EF Core LINQ 的型別安全和可讀性，是一種設計退步。

第三個選項是保持現有的兩段式，它已被測試有效，效能（約 4 秒）在可接受範圍，不需要任何代碼修改，只需要補充清楚的診斷說明。

選擇第三個選項。「技術債的代價 vs 收益」的正確評估框架是三個問題：現在改的代價是什麼（時間、風險、測試工作），改完的收益是什麼（效能提升多少、代碼多乾淨），現在不改而以後改的代價是什麼（維護者困惑、債務累積）。在這個場景裡，第一個問題的答案是「不低」，第二個問題的答案是「邊際效益小（效能已可接受）」，第三個問題的答案是「靠診斷說明可以大幅降低」。三個答案合起來支持保留現狀。

補充診斷說明是這個決策裡代價最低但價值最高的動作：寫四行注釋，讓下一個人（包括幾個月後的自己）不需要重新排查，不需要重新踩一遍 OPTIMIZE FOR UNKNOWN 失效和 RECOMPILE 失效的路徑，直接從結論開始思考「如果以後要改，往哪個方向」。沒有說明的 comment 掉的代碼是技術債，有清楚說明的 comment 掉的代碼是已知的設計限制——兩者之間的差距只是幾行文字。

**我學到的原則**

每次遇到「技術上可以改，但要不要改」的問題，先用三個問題的框架評估，而不是靠直覺或習慣。「找到根本原因就應該修根本原因」只有在「修的代價遠小於不修的代價」的時候才成立，而這個條件需要評估，不能假設。「記錄診斷結論」是低代價高價值的投資，讓任何關於「要不要改」的未來決策都有完整的背景可以參考。

**下次遇到類似情況，我會先想到什麼**

找到問題根本原因之後，先問「修這個的代價是什麼」，再問「不修的代價是什麼」。如果決定不修，一定留下「為什麼不修，以及如果以後要修，方向在哪裡」的說明。這個說明的受眾不是現在的自己，而是三個月後什麼都不記得的自己。

---

### 條目 129 — Chart.js options 不是可選的：空物件等於全部用預設值

**我做了什麼**

把 `PriceChart.vue` 裡 `buildChart` 的 `options: { /* 原本的 options 不變 */ }` 換成完整的設定，包含響應式、軸線樣式、tooltip 深色主題和 legend 位置。這個注釋代表的是「待補」，但實際上是空物件——Chart.js 全部使用預設值。

**我遇到的問題**

圖表渲染出來的樣式和整個系統的深色主題完全不一致：白色 legend 文字、白底 tooltip、淺灰格線。這些都是 Chart.js 預設值，在白色背景的系統上合理，在 `#161c18` 深色背景上完全不搭。問題不在程式碼有 bug，而在「沒有設定」本身就是一種選擇，選擇了預設值。

**我怎麼想通的**

Chart.js 的 `options` 物件裡的每一個屬性都有預設值，開發者不設定就等於接受預設值。`options: {}` 和 `options: { responsive: true, maintainAspectRatio: true, ... }` 在執行效果上是一樣的——差別只在於後者是顯式地選擇了某個值，而前者是隱式地接受了同樣的值。隱式接受的代價是「不知道自己接受了什麼」，一旦需要覆蓋某個預設值，就要先去查文件才知道要覆蓋哪個鍵。

補完 options 的過程也讓幾個設計細節變得清晰。`maintainAspectRatio: false` 是讓 Chart.js 把高度控制權交還給 CSS，一旦設定了 `.canvas-wrap { height: 400px }`，就需要這個設定讓圖表真的用那個高度，否則 Chart.js 的比例計算會覆蓋 CSS 設定的高度。`interaction.mode: 'index'` 讓 hover 同時顯示所有資料集在那個 X 位置的值，對多條線比較的場景特別有用；預設的 `'nearest'` 只顯示最近的那條線，使用者需要精準 hover 到每條線才能看到各別的資料。Tooltip 的 `backgroundColor` 設為接近背景色的深色（`rgba(22, 30, 24, 0.92)`），而不是純黑，因為純黑在深色主題上太突兀，半透明的深色有「浮在畫面上」而不是「蓋在畫面上」的感覺。

**我學到的原則**

任何需要「設定一次、永遠生效」的視覺元素，都應該在初始化時明確設定所有需要的屬性，不依賴框架的預設值。依賴預設值是一種隱性的依賴關係，框架版本升級時預設值可能改變，你的視覺效果就會在完全沒有改動程式碼的情況下改變。明確設定代表「我知道我選擇了什麼」，不明確設定代表「我接受了一個隨時可能改變的值」。

**下次遇到類似情況，我會先想到什麼**

引入新的視覺元件（Chart.js、Leaflet、Quill）時，第一件事是找到該框架的「主題設定」入口，把所有視覺相關的屬性顯式設定一遍，確認和系統的設計語言一致。不要讓「留著之後補」的空物件留在程式碼裡。

---

### 條目 130 — Hover Dropdown 的 DOM 邊界：padding 是你的，gap 不是

**我做了什麼**

實作 `TopNav.vue` 的 hover dropdown 子選單。第一版用 `top: calc(100% + 4px)` 在 tab 和 dropdown 之間留了 4px 的視覺間距，結果滑鼠從 tab 移往 dropdown 時 dropdown 會消失，無法點選。修正方式是把間距從「外部 gap」改成「元素內部 padding」，dropdown 行為恢復正常。

**我遇到的問題**

第一版的實作思路很直觀：tab 和 dropdown 之間有 4px 的視覺留白，就在 CSS 上設 4px 的 gap。結果是 dropdown 在滑鼠移過那 4px 的空白時就消失。

**我怎麼想通的**

`mouseenter` 和 `mouseleave` 是基於 DOM 元素的邊界觸發的。`.tab-wrapper` 是事件的監聽者，滑鼠在 wrapper 範圍內，`hoveredRoute` 保持有值；滑鼠離開 wrapper，`mouseleave` 觸發，`hoveredRoute` 變成 `null`，dropdown 消失。

`top: calc(100% + 4px)` 讓 dropdown 在 `.tab-wrapper` 底部邊界的外面 4px 之後才開始。滑鼠從 tab 移往 dropdown 時，必須穿越這 4px 的空隙，這段路程不屬於任何 DOM 元素，`mouseleave` 在此觸發。

解法是把間距從外部移到內部：

```css
/* ❌ 間距在元素外：滑鼠穿越時觸發 mouseleave */
.tab-dropdown {
  top: calc(100% + 4px);
  padding: 6px;
}

/* ✅ 間距在元素內：滑鼠停在 padding 區域，不觸發 mouseleave */
.tab-dropdown {
  top: 100%;
  padding: 4px 6px 6px;   /* 頂部 4px padding 取代原本的外部 gap */
}
```

`padding` 是元素盒模型的一部分，滑鼠在 `padding` 區域時仍然在元素內部，不觸發 `mouseleave`。視覺上看起來有 4px 間距（padding 透明，背景不顯示），但 DOM 事件的邊界是連貫的。

**我學到的原則**

DOM 事件的邊界由元素的盒模型決定，不是由「視覺上看起來連在一起」決定。`padding`、`border` 都屬於元素，`margin` 和元素外的空隙不屬於。任何「hover 觸發、hover 維持」的互動，觸發元素和響應元素之間不能有 DOM 上的空隙；如果需要視覺間距，就用 `padding` 或 `pseudo-element` 填補，確保 hover 範圍連貫。

**下次遇到類似情況，我會先想到什麼**

實作任何 hover dropdown 時，CSS 預設用 `top: 100%`（無縫連接），視覺留白靠 `padding-top` 產生，絕對不用 `top: calc(100% + Npx)` 在外面留空隙。遇到 dropdown 在滑鼠移動時消失的 bug，第一個問題是：「觸發元素和響應元素之間有沒有 DOM 空隙？」

---

### 條目 131 — 用框架已有的功能：Chart.js 內建 Legend 的點擊切換

**我做了什麼**

在嘗試了自訂圖例面板（`cropVisibility` ref、`toggleCrop` 函式、自訂按鈕）之後，回到了 Chart.js 內建 legend 的點擊切換功能。最終的設定只有 `position: 'top'` 加上標籤樣式，三行。

**我遇到的問題**

自訂圖例面板走進了複雜性陷阱：`cropVisibility` ref、`maVisibility` ref、兩個 toggle 函式、圖例面板的 HTML 和 CSS。更嚴重的是發現了一個隱藏 bug——隱藏某條作物線時若從 `datasets` 陣列直接移除，其他作物的顏色索引跳號（橘色變成綠色、藍色變成橘色），整個色彩對應關係亂掉。

**我怎麼想通的**

Chart.js 的 legend 本來就有點擊切換功能，是框架的預設行為：點擊 legend 裡的任一條線名稱，Chart.js 自動切換那條線的顯示狀態，內部用 `hidden` 屬性處理（不從陣列移除，所以顏色索引固定）。不需要任何額外的程式碼，只需要設定 legend 的位置和樣式。

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

自訂圖例的所有複雜性歸零，連顏色索引偏移的 bug 也自動消失，因為 Chart.js 內部就是用 `hidden` 屬性處理的，從來不從陣列移除。

這個經歷讓「造輪子」有了更具體的感受：造輪子最常見的形式不是「重新發明完全相同的東西」，而是「在框架內自己實作框架已提供的功能，但因為不熟悉框架，所以不知道它已經有了」。

**我學到的原則**

引入新功能之前，先查框架文件：「框架的這個部分有沒有內建的互動能力？」判斷標準不是「我能不能自己實作」，而是「框架的現有功能是否已夠用，且維護成本更低」。先用框架現有的，只有在真的不夠用時再自訂。自訂的代碼量永遠比框架提供的多，bug 也更多。

**下次遇到類似情況，我會先想到什麼**

在開始為某個 UI 互動寫自訂邏輯之前，先花五分鐘查框架文件：「這個元件有沒有內建的點擊/hover/切換行為？」有的話優先使用。只有在內建行為真的無法滿足需求時（例如需要額外顯示圖標、需要和外部狀態同步），才考慮自訂。

---

### 條目 132 — Fail-Fast 設計：讓錯誤盡早、盡可能靠近根本原因地出現

**我做了什麼**

在 `DbInitializer.SeedAsync` 的最前面加入 `GetPendingMigrationsAsync()` 的預檢，把「新人 clone 專案後忘記跑 Migration」這個情境從底層的 SqlException 換成啟動時的明確提示。

**我遇到的問題**

原本的行為：新人 clone 專案，啟動應用程式，在某個深層的資料庫操作時拋出 `Invalid object name 'core.NavModules'`。這個錯誤告訴你「這張表不存在」，但不告訴你「為什麼不存在，你應該怎麼辦」。開發者需要往上追 call stack，推斷是 Migration 問題，再想到要跑 `Update-Database`——這整個推斷過程是額外的認知負擔。

**我怎麼想通的**

問題的根本不在錯誤本身，而在「錯誤發生的時間點和位置」。`Invalid object name` 出現在 `context.NavModules.Any()` 被執行的瞬間，此時已經進入業務邏輯的深處。但 Migration 未套用這件事，在 `SeedAsync` 被呼叫之前就已經是事實——只是沒有人去檢查。

Fail-Fast 的原則是：「知道前提條件沒有滿足，就立刻拋出錯誤，不要讓程式繼續走到它依賴這個前提條件的地方才爆炸。」EF Core 提供了 `GetPendingMigrationsAsync()` 讓我們可以在任何時間點查詢「有沒有待套用的 Migration」：

```csharp
var pendingMigrations = await coreContext.Database.GetPendingMigrationsAsync();
if (pendingMigrations.Any())
    throw new InvalidOperationException(
        $"CoreDbContext 有 {pendingMigrations.Count()} 筆尚未套用的 Migration，" +
        $"請先執行 Update-Database 再啟動應用程式。\n" +
        $"待套用：{string.Join(", ", pendingMigrations)}");
```

這三行放在 `SeedAsync` 最前面，確保一進入方法就先確認前提條件成立。有問題就立刻拋出一個描述性的、有行動指引的錯誤訊息，讓開發者不需要任何診斷就知道要做什麼。

「讓錯誤盡早出現」和「讓錯誤靠近根本原因出現」是兩個互相強化的目標。在 call stack 的深處爆出的錯誤，通常是一個症狀（表不存在），而不是根本原因（Migration 未套用）。越早拋出，越容易保持錯誤訊息的語意貼近根本原因。

**我學到的原則**

任何方法或流程有隱含的前提條件（資料庫 Schema 存在、外部服務可連線、設定值格式正確），就應該在入口處顯式地驗證這些前提條件，有問題就 Fail-Fast。「先跑完看看會不會壞」讓錯誤的出現位置偏離根本原因，增加診斷成本。驗證前提條件的代碼不是多餘的防禦，是讓系統「誠實」的必要步驟。

**下次遇到類似情況，我會先想到什麼**

寫一個有隱含前提條件的方法時，在方法最前面問：「這個方法依賴什麼才能正常執行？如果那個前提不成立，現在能知道嗎，還是要到很深的地方才會爆？」能提早知道，就提早驗證，提早拋出有行動指引的錯誤。`GetPendingMigrationsAsync()` 是 EF Core 提供的標準工具，任何在應用程式啟動時涉及資料庫操作的初始化方法都可以在入口加這個預檢。

---
 
### 條目 133 — Cache-Aside Pattern：Redis 是保底，RabbitMQ 才是正常路徑
 
**我做了什麼**
 
在 `MarketService.GetPricesAsync` 加入 Redis Cache-Aside Pattern。注入 `IDistributedCache`，在方法最前面先查 Redis，命中則直接回傳反序列化後的結果；沒命中才執行三表 JOIN + GroupBy 的 SQL 查詢，查完把結果序列化成 JSON 寫入 Redis，TTL 設定 25 小時。
 
**我遇到的問題**
 
第一個困惑是 TTL 應該設多長。直覺上覺得「農業部資料每天更新一次，所以 TTL 設 24 小時」，但這個邏輯有一個隱性的問題：TTL 從「第一次有人查詢」開始計時，不是從「Worker 同步完成」開始計時。如果 Worker 凌晨 2 點同步完，下午 2 點第一個人查詢，TTL 12 小時後會在凌晨 2 點過期——剛好 Worker 又在跑了。這時候 cache 的過期時間和資料的更新時間是兩條完全無關的時間線，不應該混為一談。
 
第二個困惑是：TTL 設 25 小時，如果 Worker 今天凌晨 2 點同步完新資料，但 cache 還有 3 小時才過期，這 3 小時查詢到的不是最新資料，這樣不是有問題嗎？
 
**我怎麼想通的**
 
第一個問題的解答是：TTL 的職責不是「讓 cache 在資料更新時失效」，它做不到這件事，因為它從來不知道資料有沒有更新。TTL 的正確定位是「保底機制」——萬一 RabbitMQ 的主動通知沒有送達（Worker 崩潰、網路斷線、Consumer 沒啟動），cache 最多撐多久再自動清掉。所以 TTL 的長短設計考量不是資料更新頻率，而是「我能接受的最壞情況延遲是多少小時」。
 
第二個問題的解答讓我理解了這個系統的核心設計：農業部的農產品交易資料是**歷史快照**，不是即時資料。Worker 今天同步的是「昨天的交易記錄」。如果使用者查的是 2024 年的資料，那 cache 永遠有效，因為 2024 年的資料不會改變。如果使用者查的範圍包含到昨天，那 Worker 跑完後應該透過 **RabbitMQ 主動通知 Consumer 去清掉對應的 cache**，而不是靠 TTL 等它自然過期。TTL 是保底，RabbitMQ 才是正常路徑。這兩個機制各有職責，不是替代關係。
 
**Cache Key 設計的細節**
 
```csharp
// 日期必須用 finalStart/finalEnd（解析後的值），不能用原始的 startDate/endDate
// 原因：兩個使用者都沒傳 startDate，startDate 都是 null
// 但今天呼叫的 finalStart 是 2025-05-23，明天呼叫的是 2025-05-24
// 如果 Key 用 null，兩次查詢命中同一個 cache，但結果不同——這是 bug
DateOnly finalEnd = endDate ?? DateOnly.FromDateTime(DateTime.Today);
DateOnly finalStart = startDate ?? finalEnd.AddDays(-365);
 
// cropCodes 先排序再 Join，確保 ["B02","A01"] 和 ["A01","B02"] 命中同一個 cache
var sortedCrops = string.Join(",", cropCodes.OrderBy(c => c));
var cacheKey = $"market:prices:{marketType}:{sortedCrops}:{marketCode ?? ""}:{finalStart}:{finalEnd}";
```
 
**我學到的原則**
 
Cache 的 TTL 和資料的更新頻率是兩個獨立的概念，不應該強行耦合。正確的思考框架是：「如果主動失效機制（RabbitMQ）失敗了，我願意讓 cache 最多撐多久？」這個問題的答案才是 TTL 設定的依據。另外，Cache Key 的設計必須窮舉所有影響查詢結果的參數，任何一個遺漏都可能造成「不同查詢命中同一個 cache」的隱性 bug，這種 bug 難以復現，難以診斷。
 
**下次遇到類似情況，我會先想到什麼**
 
設計 Cache Key 之前，先把這個方法的所有輸入參數列出來，問：「這個參數如果不同，查詢結果會不同嗎？」如果答案是會，就必須進 Key。設計 TTL 之前，問：「主動失效機制是什麼？TTL 是保底還是主要失效手段？」
 
---
 
### 條目 134 — RabbitMQ 的三個 Exchange Type：不是選一個最好的，而是選符合語意的
 
**我做了什麼**
 
在 `AgriProductsTransSyncWorker` 同步成功後加入 `PublishPriceUpdatedEventAsync`，使用 topic exchange `agri.events`，routing key `agri.market.priceUpdated`，發布一個空 JSON payload 的事件。
 
**我遇到的問題**
 
選 Exchange Type 時面臨三個選項：direct（一對一精確配對）、fanout（廣播，所有訂閱者都收到）、topic（萬用字元配對，一對多但可篩選）。直覺上覺得「fanout 最簡單，反正我就是要廣播給所有人」，但這個直覺忽略了一個問題：「所有人」的定義會隨時間擴充。
 
**我怎麼想通的**
 
把未來的可能擴充列出來之後，答案就清楚了。目前只有 Web 端要監聽 `agri.market.priceUpdated`，但未來可能有 Report Worker（重跑報表）、Notification Worker（推使用者通知）也要監聽這個事件，甚至可能有新的 Worker 只想監聽 `agri.weather.updated` 而不想收到 `agri.market.priceUpdated`。
 
fanout 會把訊息送給所有綁定到這個 Exchange 的 Queue，沒有篩選能力。這意味著如果 Notification Worker 只關心天氣事件，它的 Queue 還是會收到農產品價格的事件，然後把它丟棄——這是噪音，不是設計。
 
topic 讓每個 Consumer 的 Queue 在 bind 時宣告自己的 routing key pattern，只收自己關心的訊息。`agri.market.*` 就收所有農業市場事件，`agri.weather.*` 就只收天氣事件，靈活而且語意清楚。
 
```csharp
// topic exchange 的命名慣例：用點號分隔，代表事件的階層結構
// agri        → 系統識別碼
// market      → 模組識別碼
// priceUpdated → 事件類型
await channel.ExchangeDeclareAsync(
    exchange: "agri.events",
    type: ExchangeType.Topic,
    durable: true);   // durable: RabbitMQ 重啟後 Exchange 仍然存在
```
 
**我學到的原則**
 
選 Exchange Type 不是選「最強」或「最簡單」的，而是選「語意最貼近你的通訊模式」的。fanout 的語意是「廣播，我不在乎你是誰，全部送」；topic 的語意是「訂閱，我只送給關心這個主題的人」。現在的場景是訂閱，所以用 topic。另外，`durable: true` 在大多數生產場景都應該開，確保 RabbitMQ 重啟後 Exchange 和 Queue 的設定不會消失。
 
**下次遇到類似情況，我會先想到什麼**
 
決定 Exchange Type 之前，先問：「這個事件的訂閱者是固定的還是可能擴充的？不同訂閱者需要篩選嗎？」如果需要篩選，就用 topic；如果真的所有人都需要所有訊息，才考慮 fanout。
 
---
 
### 條目 135 — IHostedService 的生命週期：跟應用程式一起活，跟應用程式一起死
 
**我做了什麼**
 
在 `TaiwanAgri.Web` 新增 `PriceUpdatedConsumer` 繼承 `BackgroundService`，在 `StartAsync` 建立 RabbitMQ 長連線、宣告 Exchange 和臨時 Queue；在 `ExecuteAsync` 設定事件處理器並用 `Task.Delay(Timeout.Infinite, stoppingToken)` 保持存活；在 `StopAsync` 優雅關閉連線。
 
**我遇到的問題**
 
第一個困惑是為什麼不把 Consumer 邏輯放在 Controller 裡，用一個 HTTP 端點來觸發。第二個困惑是 `Task.Delay(Timeout.Infinite, stoppingToken)` 這行看起來很奇怪，為什麼要讓方法「永遠等下去」？
 
**我怎麼想通的**
 
第一個問題的答案來自於理解 Controller 的本質：Controller 是「有 HTTP request 進來才執行」的，它的生命週期是一個 request-response 週期。但 RabbitMQ Consumer 需要「應用程式啟動就開始監聽，不管有沒有 HTTP request」。這兩種需求根本不搭配，Controller 不是正確的載體。
 
`IHostedService` 的設計語意剛好相反：它跟著應用程式生命週期走，`StartAsync` 在應用程式啟動時呼叫，`StopAsync` 在應用程式停止時呼叫。這正是 Consumer 需要的生命週期。
 
第二個問題的答案是 `ExecuteAsync` 的角色定位。RabbitMQ Consumer 是事件驅動的：有訊息進來，`ReceivedAsync` 事件處理器就被呼叫；沒有訊息，就靜靜等待。`ExecuteAsync` 不需要做任何事，只需要「不結束」——因為如果 `ExecuteAsync` 返回了，`BackgroundService` 就認為這個 hosted service 已經完成工作，不會再監聽了。`Task.Delay(Timeout.Infinite, stoppingToken)` 是一個慣用法：等一個永遠不會到的 timeout，但如果 `stoppingToken` 被取消（應用程式停止），這個 await 就會拋出 `OperationCanceledException`，`ExecuteAsync` 優雅地結束。
 
```csharp
// StartAsync：建立連線（昂貴，做一次）
// ExecuteAsync：設定事件處理器，然後「等著」
// StopAsync：優雅關閉（清理資源）
 
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    var consumer = new AsyncEventingBasicConsumer(_channel!);
    consumer.ReceivedAsync += async (_, ea) =>
    {
        // 有訊息進來時，這裡才會被呼叫
        await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false);
    };
    
    await _channel!.BasicConsumeAsync(queue: queueName, autoAck: false, consumer: consumer);
    
    // 這行讓 ExecuteAsync 永不返回，直到應用程式停止
    await Task.Delay(Timeout.Infinite, stoppingToken);
}
```
 
**Queue 名稱設計的決策**
 
不傳名稱給 `QueueDeclareAsync` 讓 RabbitMQ 自動產生臨時 Queue（`amq.gen-xxxx`）。這個選擇決定了 Consumer 的行為模式：每個獨立的 Consumer 服務都有自己的 Queue，Publisher 發一個訊息，每個 Consumer 都收到一份副本（廣播語意）。如果傳固定名稱，多個相同的 Consumer 實例會共用同一個 Queue，訊息被輪流分配給它們（負載平衡語意）。本專案的場景是廣播，所以用臨時 Queue。
 
**我學到的原則**
 
`IHostedService` 是 ASP.NET Core 中「需要跟應用程式生命週期綁定的背景邏輯」的標準容器。Controller 處理 HTTP，`IHostedService` 處理一切不靠 HTTP 觸發的事情——定時排程、訊息消費、長連線維護。這兩個各司其職，不互相替代。
 
**下次遇到類似情況，我會先想到什麼**
 
看到「需要在應用程式啟動時自動開始的背景邏輯」，就想到 `IHostedService`。看到「需要持續監聽某個事件源（RabbitMQ、WebSocket、gRPC streaming）的 Consumer」，就想到 `BackgroundService` + `Task.Delay(Timeout.Infinite, stoppingToken)` 的慣用法。
 
---
 
### 條目 136 — RabbitMQ 的 Ack 機制：外送員類比，為什麼手動確認比自動確認更安全
 
**我做了什麼**
 
`PriceUpdatedConsumer` 的 `BasicConsumeAsync` 設定 `autoAck: false`，在 `ReceivedAsync` 事件處理器裡處理完訊息後，手動呼叫 `BasicAckAsync`。
 
**我遇到的問題**
 
`autoAck: true` 看起來更簡單，為什麼要用 `autoAck: false` 加上手動 Ack？這樣不是更麻煩嗎？
 
**我怎麼想通的**
 
用外送員的類比來想。`autoAck: true` 相當於：外送員剛按你家門鈴，你還沒打開門，他就在 app 上標記「已送達」，然後走了。如果這時候你不在家，餐點就消失了，沒有任何補救機制。`autoAck: false` 相當於：外送員把餐點交到你手上，你確認收到，他才標記「已送達」。如果你不在家，他會重新跑一次。
 
對應到 RabbitMQ：`autoAck: true` 時，訊息一從 Queue 取出就被標記為已消費，無論後續處理是否成功。如果 Consumer 在處理到一半時崩潰，訊息已經被刪掉，這份訊息就永遠消失了。`autoAck: false` 時，訊息取出後保持 Unacked 狀態，只有在 Consumer 明確呼叫 `BasicAckAsync` 後才會從 Queue 刪除。如果 Consumer 崩潰，RabbitMQ 偵測到連線斷開，會把 Unacked 的訊息重新放回 Queue，等下一個 Consumer 來處理。
 
```csharp
consumer.ReceivedAsync += async (_, ea) =>
{
    try
    {
        // 處理訊息（未來是 Cache invalidation）
        // ...
 
        // 處理成功才 Ack，告訴 RabbitMQ 可以刪掉這筆訊息
        await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false);
    }
    catch (Exception ex)
    {
        // 處理失敗可以 Nack，讓 RabbitMQ 重新派送
        // await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
    }
};
```
 
`multiple: false` 的意思是「只 Ack 這一筆訊息」，不批次確認。`multiple: true` 會確認所有 DeliveryTag 小於等於當前值的訊息，在批次處理場景下可以提升效能，但這個 Consumer 目前是逐筆處理，用 `false` 更精確。
 
**我學到的原則**
 
在任何需要可靠訊息傳遞的場景，`autoAck: false` + 手動 Ack 是預設選擇，不是可選項。`autoAck: true` 只在「訊息遺失完全可以接受」的場景才適合，例如發送遙測資料、統計計數器這類即使漏一筆也沒關係的事件。Cache invalidation 屬於「最好不要遺失」的場景，用手動 Ack。
 
**下次遇到類似情況，我會先想到什麼**
 
設計任何 RabbitMQ Consumer，預設就用 `autoAck: false`。想清楚「如果這個訊息在處理到一半時消失了，後果是什麼」——如果後果不可接受，就手動 Ack；如果後果可以接受，才考慮 `autoAck: true`。
 
---
 
### 條目 137 — 骨架優先：讓整個鏈路跑通比讓功能完整更重要
 
**我做了什麼**
 
W13-14 完成了 Redis Cache-Aside、RabbitMQ Publisher、RabbitMQ Consumer 骨架。Consumer 的 Cache invalidation 是預留位置，實際上什麼都沒清，只有 log。有人問：「這樣不就是半成品嗎？」
 
**我怎麼想通的**
 
「骨架」和「半成品」的差異在於：骨架是刻意的、有計劃的不完整，每一層都做到足以驗證的程度；半成品是不小心的不完整，不知道缺了什麼、缺了多少。
 
W13-14 的骨架驗收標準很明確：Worker 能發訊息（RabbitMQ Management UI 驗證）、Web 端能收訊息（啟動 log 驗證）、SQL 查詢有被 cache（redis-cli 驗證）。這三件事都做到了。整個 Worker → RabbitMQ → Web → Redis 的鏈路是通的，可以在現在就發現「連線設定錯誤」、「套件版本不兼容」這類基礎問題。
 
如果等到 W15 JWT 整合完成後才一起做 Cache invalidation，到時候可能發現 RabbitMQ 的 Exchange 宣告方式跟套件版本不合、或者 `IDistributedCache.GetStringAsync` 的 Key 格式跟 Consumer 預期的不一樣——這些問題在現在做骨架的時候就能發現，比在 W15 資訊量更大時發現要容易處理得多。
 
這個開發節奏在整個專案裡是一致的：想清楚 → 建骨架 → 驗收 → 再往下一層走。
 
**我學到的原則**
 
分散式系統的整合最容易在「所有元件都做完才接在一起」的時候爆炸。骨架優先的價值是把整合風險前移：在功能最少、複雜度最低的時候就讓鏈路通，之後每次新增功能都在一個已驗證的基礎上做，而不是在「這個連線本身對不對都不知道」的情況下繼續往上疊。
 
**下次遇到類似情況，我會先想到什麼**
 
引入新的基礎設施元件（Redis、RabbitMQ、外部 API）時，第一步是讓連線通、讓最簡單的一個操作成功、用眼睛驗收。功能的完整性是第二步。「連得上」和「用得好」是兩個可以分開的問題，不要混在一起解決。

---

### 條目 138 — EF Core 的查詢邊界：GroupBy + First() 為什麼在 SQL 層跑不動

**我做了什麼**

`GetStationsByCityAsync` 需要「每個測站只取最新一筆觀測」。直覺的 LINQ 寫法是：

```csharp
_context.WeatherObservations
    .Where(s => s.CityName == cityName)
    .GroupBy(s => s.StationId)
    .Select(g => g.OrderByDescending(w => w.ObservedAt).First())
    .ToListAsync()
```

這段程式碼編譯成功，但執行時拋出例外：`The LINQ expression could not be translated. Either rewrite the query in a form that can be translated...`

**我遇到的問題**

編譯器看不到問題，但 EF Core 的 SQL 翻譯器看到了。我原本以為「能編譯就能跑」，這次才清楚地碰到 EF Core 的翻譯限制。

**我怎麼想通的**

`ToListAsync()` 是一條邊界線。邊界之前，EF Core 嘗試把整個 LINQ 表達式翻譯成一條 SQL 送到資料庫執行；邊界之後，回到普通的 C# 記憶體操作，任何 .NET 語法都可以用。

`GroupBy(s => s.StationId).Select(g => g.OrderByDescending(w => w.ObservedAt).First())` 在 SQL 裡沒有一個直接對應的語法——SQL 的 GROUP BY 後面接的是聚合函數（MAX、AVG、COUNT），而不是「取整列」。要在 SQL 層做這件事，需要用 `ROW_NUMBER() OVER (PARTITION BY StationId ORDER BY ObservedAt DESC)`，這是視窗函數語法，EF Core 沒有辦法從 LINQ 自動翻譯出來。

所以問題的本質是：我在 EF Core 的 SQL 翻譯模式裡，寫了一段 SQL 翻譯器表達不了的語意。

**我嘗試過的中間方案（以及為什麼放棄）**

方案一：兩個 HashSet 配對篩選

```csharp
var stationIds = latestTimes.Select(l => l.StationId).ToHashSet();
var latestDates = latestTimes.Select(l => l.LastObservedAt).ToHashSet();

.Where(s => stationIds.Contains(s.StationId) && latestDates.Contains(s.ObservedAt))
```

EF Core 可以把 `Contains` 翻譯成 SQL 的 `IN`，這段編譯和執行都沒問題。但它有一個邏輯漏洞：兩個 `Contains` 是獨立的，「StationId 在集合裡」和「ObservedAt 在集合裡」是分別成立的條件，不是「這個 StationId 配對這個 ObservedAt 才成立」。如果測站 A 最新時間是 5/22，測站 B 最新時間是 5/20，而測站 A 剛好也有一筆 5/20 的資料，這筆資料也會被撈出來——這是一個「看起來對、資料量小時不容易發現」的 bug。

方案二：字串組合 Key

```csharp
var latestKeys = ... .Select(l => $"{l.StationId}_{l.LastObservedAt}").ToHashSet();
.Where(s => latestKeys.Contains($"{s.StationId}_{s.ObservedAt}"))
```

這個配對邏輯是正確的，但 EF Core 無法把 `$"{s.StationId}_{s.ObservedAt}"` 翻譯成 SQL——字串插值包含多個欄位的組合，超出 SQL 翻譯器的能力範圍。

**最終解法：接受邊界，在邊界後操作**

```csharp
var raw = await _context.WeatherObservations
    .Where(s => s.CityName == cityName)
    .ToListAsync();   // ← 在這裡穿越邊界，進入記憶體模式

var result = raw
    .GroupBy(s => s.StationId)
    .Select(g => g.OrderByDescending(w => w.ObservedAt).First())
    .Select(s => new WeatherStationResponseDto { ... })
    .ToList();        // ← 記憶體操作，沒有翻譯限制
```

代價是把整個縣市的觀測資料拉回記憶體。這個取捨是刻意接受的——一個縣市最多幾十個測站、歷史資料幾千筆，記憶體代價遠小於引入 `FromSqlRaw` 的複雜性和維護成本。

**我學到的原則**

EF Core LINQ 和普通 LINQ 看起來語法相同，但執行環境完全不同。`ToListAsync()` 之前是「SQL 翻譯模式」，這裡的每個操作都必須能對應到合法的 SQL 片段；之後是「C# 執行模式」，沒有任何限制。遇到「編譯過但執行炸」的 EF Core 問題，第一個問題是：「這個操作發生在 `ToListAsync()` 之前還是之後？」

**下次遇到類似情況，我會先想到什麼**

看到 `GroupBy + 取整列`、`複雜的字串操作`、`多欄位組合邏輯` 出現在 EF Core LINQ 裡，先問：「這段邏輯有辦法翻譯成一條合法 SQL 嗎？」有辦法就繼續，沒辦法就把它移到 `ToListAsync()` 之後的記憶體操作裡，同時評估這樣做的資料量代價是否可接受。

---

### 條目 139 — 跨表 JOIN 的兩種路徑：導覽屬性 vs. LINQ Join

**我做了什麼**

`GetRainfallByCityAsync` 需要依縣市篩選雨量資料，但 `RainfallObservation` 本身沒有 `CityName` 欄位，城市資訊在 `RainfallStation` 裡。兩張表透過 `StationId` 關聯。

**我遇到的問題**

WeatherObservation 有 CityName 直接存在同一張表；RainfallObservation 沒有。查詢時需要 JOIN 兩張表，但不確定應該用 `Include` 還是 LINQ `Join`。

**我怎麼想通的**

`Include` 是走導覽屬性的方式。EF Core 透過 Entity 之間定義好的導覽屬性知道「這張表和那張表有關聯」，`Include` 告訴它「一起載入」。但前提是：Entity 上必須有導覽屬性。

```csharp
// 如果有導覽屬性，可以用 Include：
public class RainfallObservation {
    public RainfallStation Station { get; set; }  // ← 這個存在才能 Include
}

// 如果沒有，只能用 Join：
_context.RainfallObservations
    .Join(_context.RainfallStations,
        obs => obs.StationId,
        sta => sta.StationId,
        (obs, sta) => new { obs, sta })
```

`RainfallObservation` 在 Worker 開發時沒有設計雙向導覽屬性（因為那時候的職責只是「寫入」，不是「查詢」）。所以這裡只能用 LINQ `Join`。

`Join` 的四個參數分別是：外部來源（`RainfallObservations`）、內部來源（`RainfallStations`）、外部 Key 選擇器（`obs.StationId`）、內部 Key 選擇器（`sta.StationId`）、結果選擇器（如何組合兩個物件）。

**我學到的原則**

「要 JOIN 兩張表」有兩條路徑，選哪條取決於 Entity 設計：有導覽屬性就用 `Include`（語意更清楚、EF Core 自動決定 JOIN 類型）；沒有就用 LINQ `Join`（手動指定 Key，但效果相同）。設計 Entity 時如果能預見查詢需求，提前加導覽屬性可以讓查詢層更簡潔。

**下次遇到類似情況，我會先想到什麼**

看到跨表查詢，先確認 Entity 有沒有導覽屬性。有 → 用 Include；沒有 → 用 LINQ Join，格式是 `.Join(內部來源, 外部Key, 內部Key, 結果選擇器)`。

---

### 條目 140 — nullable 參數的兩種邏輯錯誤：格式錯誤 vs. 沒有傳值

**我做了什麼**

`WeatherController` 的 `GetRainfallByCity` 接受可選的日期參數：

```csharp
[HttpGet("rainfall")]
public async Task<IActionResult> GetRainfallByCity(
    [FromQuery] string cityName,
    [FromQuery] string? startDate,
    [FromQuery] string? endDate)
```

需要驗證日期格式，但不能把「沒有傳日期」誤判為「格式錯誤」。

**我遇到的問題**

最初的版本：

```csharp
var start = DateHelper.ParseIsoDate(startDate);
if (start == null) return BadRequest("startDate 格式錯誤");
```

問題是：`startDate` 本來就是 nullable，使用者沒傳時 `startDate` 是 `null`，`ParseIsoDate(null)` 也回傳 `null`，這樣使用者沒傳日期也會收到 400 格式錯誤——但使用者根本沒有傳任何格式錯誤的東西。

**我怎麼想通的**

「格式錯誤」的定義是：**有傳值，但值是無法解析的字串**。「沒傳值」不是錯誤，是合法的輸入（讓 Service 套用預設值）。

這兩種情況需要用兩個條件才能分開：

```csharp
var start = DateHelper.ParseIsoDate(startDate);

// 「有傳」但「解析失敗」= 格式錯誤
if (startDate != null && start == null)
    return BadRequest("startDate 格式錯誤，請使用 yyyy-MM-dd");

// 「沒傳」（startDate == null）→ start 就是 null，讓 Service 套預設值，不報錯
```

這個 `null` 的語意需要人為區分：

| `startDate` | `start` | 判斷 |
|-------------|---------|------|
| `null`（未傳） | `null` | 正常，讓 Service 套預設值 |
| `"2026-05-01"`（合法格式） | `DateOnly(...)` | 正常，傳給 Service |
| `"abc"`（非法格式） | `null` | 格式錯誤，回 400 |

第一列和第三列的 `start` 都是 `null`，但語意完全不同，必須靠 `startDate` 本身是不是 `null` 來區分。

**我學到的原則**

處理 nullable 輸入參數時，先列出所有合法和非法的組合（參數有值/沒有值 × 解析成功/失敗），再設計對應的 if 條件。「解析結果為 null」不等於「錯誤」，因為輸入本身可能就是合法的 null。兩個 null 出於不同原因，需要不同的處理策略。

**下次遇到類似情況，我會先想到什麼**

看到「可選參數 + 格式驗證」的組合，先畫出那張表（輸入值 × 解析結果 × 期望回應），確認每一格都有對應的處理，再寫程式碼。

---

### 條目 141 — Service 的例外語意 vs. 回傳碼：MarkAsReadAsync 的設計選擇

**我做了什麼**

`MarkAsReadAsync` 需要找到指定的通知並標記已讀。找不到時有兩種設計：

- 回傳 `Task<int>`（0 代表沒找到），讓 Controller 根據回傳值判斷
- 回傳 `Task`，找不到時拋出 `KeyNotFoundException`，Controller 捕捉後回 404

**我遇到的問題**

`Task<int>` 看起來讓呼叫方有更多資訊，為什麼選例外語意？

**我怎麼想通的**

`SaveChangesAsync()` 的回傳值是「EF Core 影響了幾行資料」，這是 ORM 底層的實作細節。如果讓這個數字穿透 Service 層到達 Controller 層，Controller 就需要知道「0 行代表沒找到」這個假設——但這個假設只在目前的實作下成立。如果未來改用其他 ORM 或直接 SQL，回傳值的語意可能不同，Controller 的判斷邏輯就會壞掉。

例外語意的設計讓職責更清楚：

```csharp
// Service 負責「找不到就報錯」這個業務邏輯
public async Task MarkAsReadAsync(int notificationId, string userId)
{
    var notification = await _dbContext.UserNotifications
        .FirstOrDefaultAsync(x => x.Id == notificationId && x.UserId == userId);
    if (notification == null)
        throw new KeyNotFoundException($"通知 {notificationId} 不存在或無權限");

    notification.IsRead = true;
    await _dbContext.SaveChangesAsync();
}

// Controller 負責「把例外翻譯成 HTTP 狀態碼」
try
{
    await _notificationService.MarkAsReadAsync(id, userId);
    return NoContent();
}
catch (KeyNotFoundException)
{
    return NotFound();
}
```

EF Core 的 Change Tracker 追蹤機制在這裡也值得注意：透過 EF Core 查詢取得的 Entity 會被 Change Tracker 自動追蹤，修改屬性後 `SaveChangesAsync()` 會自動產生 `UPDATE` SQL，不需要呼叫 `.Update(entity)`。`.Update()` 是給「Disconnected 場景」（Entity 從外部傳入，不在 Change Tracker 追蹤中）使用的，在 Scoped DbContext 的正常查詢流程裡幾乎不需要。

**我學到的原則**

Service 層應該用業務語意表達結果（找到/找不到/無權限），不應該洩漏底層實作的細節（影響幾行）。業務邏輯用例外表達「不應該發生的情況」，讓呼叫方只需要處理成功路徑，失敗路徑集中在 catch。

**下次遇到類似情況，我會先想到什麼**

設計「寫入操作」的 Service 方法時，先問：「呼叫方需要知道什麼？」只需要知道成功/失敗 → `Task` + 例外；需要知道影響了多少筆 → 考慮 `Task<int>`，但要確認這個數字有業務意義，不只是 ORM 的技術回傳值。

---

### 條目 142 — 前端 API 層的分層邊界：誰需要 Auth Header，就誰獨立

**我做了什麼**

`src/api/weather.ts` 同時定義了 `weatherApi` 和 `notificationApi`，但刻意把兩者分開：

```typescript
export const weatherApi = {
    getStations(cityName: string): Promise<...> { ... },
    getRainfall(...): Promise<...> { ... },
    // 其他公開查詢...
}

export const notificationApi = {
    getList(userId: string, page = 1): Promise<...> { ... },
    getUnreadCount(userId: string): Promise<...> { ... },
    markAsRead(id: number, userId: string): Promise<void> { ... },
}
```

**我遇到的問題**

為什麼不把通知 API 放進 `weatherApi` 物件裡，或獨立成 `notification.ts`？

**我怎麼想通的**

這個分層的依據是「未來的修改會同時影響哪些東西」。

`weatherApi` 裡的方法都是公開查詢，不需要登入。未來即使有修改（加新端點、改回傳格式），也不需要動到認證邏輯。

`notificationApi` 裡的方法都需要使用者身份。W15 JWT 整合後，這些方法需要在請求 header 加上 `Authorization: Bearer {token}`。如果把通知 API 混在 `weatherApi` 裡，加 Auth header 時要小心不要影響不需要 auth 的公開查詢——這種「要改一部分、要留一部分」的修改最容易出錯。

分開定義後，W15 的修改範圍就很清楚：只動 `notificationApi`，`weatherApi` 完全不用碰。

**我學到的原則**

API 函式的分組依據不是「功能屬於同一個模組」，而是「這批函式會不會因為同一個原因被修改」。Auth 需求的有無是一個非常強的分組信號——需要 auth 的 API 和不需要 auth 的 API，幾乎一定會在不同的時間點、因為不同的原因被修改，應該分開管理。

**下次遇到類似情況，我會先想到什麼**

定義 API 函式時，先問：「這個函式需要 Authorization header 嗎？」需要的放一組，不需要的放另一組，不因為業務上都是同一個模組就混在一起。

---

### 條目 143 — Vue 元件的記憶體管理：onUnmounted 的清理職責

**我做了什麼**

`NotificationBell.vue` 有兩個需要清理的資源：

```typescript
onMounted(() => {
    store.fetchUnreadCount()
    const timer = setInterval(() => store.fetchUnreadCount(), 60000)
    document.addEventListener('click', handleOutsideClick)
    
    onUnmounted(() => {
        clearInterval(timer)
        document.removeEventListener('click', handleOutsideClick)
    })
})
```

**我遇到的問題**

`onUnmounted` 嵌在 `onMounted` 裡面是正確的嗎？為什麼不把它放在外層？

**我遇到的問題的解答**

`onUnmounted` 在 `onMounted` 裡面是 Vue 3 Composition API 的合法用法，效果和放在外層相同（都會在元件卸載時執行）。嵌在裡面的好處是 timer 變數的作用域剛好也在 `onMounted` 裡，清理邏輯緊靠著建立邏輯，更容易一眼確認「有建立就有清理」。

**為什麼這兩個資源必須清理**

`setInterval` 的 callback 每 60 秒執行一次，不管元件在不在。如果使用者離開了有 `NotificationBell` 的頁面，Vue 卸載了這個元件，但 `setInterval` 還在執行，它的 callback 嘗試存取已經卸載的元件的 store，可能造成記憶體洩漏或意外的 API 呼叫。

`document.addEventListener` 是全域的，元件卸載後 `handleOutsideClick` 函式仍然在 `document` 上監聽，每一次點擊都會執行這個函式，即使 `NotificationBell` 已經不在畫面上了。移除事件監聽確保這個副作用跟著元件消失。

**我學到的原則**

「有建立就有清理」是副作用管理的基本原則。全域副作用（`setInterval`、`document.addEventListener`、WebSocket 連線、`ResizeObserver`）不會跟著 Vue 元件的生命週期自動消失，必須在 `onUnmounted` 裡手動清理。每次在 `onMounted` 裡建立全域副作用，就要同時問：「這個副作用怎麼清？」

**下次遇到類似情況，我會先想到什麼**

在 `onMounted` 裡建立任何東西之前，先問：「這個東西會不會在元件卸載後繼續存在？」如果會，`onUnmounted` 就是必須的，不是可選的。

---

### 條目 144 — Chart.js 的 spanGaps 與 null 資料點

**我做了什麼**

雨量折線圖和旬密度折線圖都有一個共同問題：不同測站（或城市）的觀測時間點不完全對齊，X 軸是所有時間點的聯集，某個測站在某個時間點沒有資料時，那個位置的值是 `null`。

如果不特別處理，`null` 資料點會讓折線在那個位置斷掉，圖表變成很多段不連續的線段，視覺上很難閱讀。

**我使用的解法**

```typescript
datasets: [{
    data: labels.map(t => timeMap[t] ?? null),  // 沒有對應資料就填 null
    spanGaps: true,     // Chart.js 會自動跳過 null，用直線連接前後的有值點
    // ...
}]
```

`spanGaps: true` 讓 Chart.js 在遇到 `null` 資料點時，直接畫一條線從上一個有值的點連到下一個有值的點，而不是斷開。這在稀疏資料的折線圖中是標準做法。

**我學到的原則**

時間序列資料幾乎都有「某些時間點沒有資料」的情況，這不是錯誤，而是資料的正常狀態。在組裝 Chart.js datasets 時，`null` 是「這個時間點沒有值」的正確表達方式，搭配 `spanGaps: true` 讓圖表仍然保持連續、易讀。不要用 `0` 填補缺失的資料點——`0` 會被解讀為「這個時間點雨量是 0mm」，和「沒有觀測到」的意義完全不同。

**下次遇到類似情況，我會先想到什麼**

時間序列 + 多條線的圖表，先問：「每個資料源的時間點對齊了嗎？」沒對齊就需要建立共用時間軸（所有時間點的聯集），再讓每個資料源在沒有對應值的時間點填 `null`，搭配 `spanGaps: true` 處理視覺上的連續性。

---

### 條目 145 — Vue 路由的兩種角色：容器 vs 頁面，不能同時

**我做了什麼**

把 `MarketView.vue` 從一個同時擔任「路由容器」和「頁面內容」的混合元件，拆成純容器 + 三個獨立子 View（`PricesView`、`DisastersView`、`RestDaysView`）。

**我遇到的問題**

原本的設計是：`/market` 路由對應 `MarketView.vue`（裡面有完整的篩選器、圖表、天災面板），但 `/market/prices`、`/market/disasters`、`/market/rest-days` 子路由全部指向 `PlaceholderView`。這在功能上「能跑」，但有一個根本矛盾：使用者點「行情查詢」子功能，路由跳去 `/market/prices`，顯示的卻是空殼。實際有內容的頁面在 `/market`，但 `/market` 按照設計是一個不應該有頁面內容的父路由。

**我怎麼想通的**

Vue Router 的父子路由結構有一個隱性的設計語意：**父路由元件的職責是決定「子路由要渲染在哪裡」，而不是「自己要顯示什麼內容」**。`WeatherView.vue` 示範了正確的做法：它只有一行 `<RouterView />`，所有頁面邏輯分散在子 View 裡。

一個元件同時做兩件事（提供 RouterView 插槽 + 顯示頁面內容），就是「單一職責原則被打破」的具體症狀。症狀表現是：路由跳轉行為讓人困惑（點子功能但頁面不更換），以及新增子頁面時不知道放在哪裡才對。

修正方式直白：MarketView 改成空容器，內容全部下沉到子 View，路由同步調整。對齊 WeatherView 的結構之後，任何人看到 MarketView 就知道它是容器——因為在這個專案裡，所有模組的父 View 都遵循同一個模式。

**我學到的原則**

Vue Router 的父子結構裡，「有 `<RouterView />` 的元件」和「有頁面內容的元件」通常應該是不同的東西。如果一個元件同時有 `<RouterView />` 和大量業務 UI，就是一個值得警惕的信號：它在做兩件事。正確的拆分方式是讓父元件只負責 `<RouterView />`，讓內容完全下沉到子 View，這樣子路由的增減完全不影響父元件。

**下次遇到類似情況，我會先想到什麼**

看到父路由元件裡有業務 UI，就問：「如果這個路由下新增一個子路由，使用者跳過去之後，這些業務 UI 應該消失嗎？」如果答案是「應該消失（被子路由內容取代）」，那這些業務 UI 就應該下沉到子 View 裡，而不是放在父元件。

---

### 條目 146 — 同一份資料，不同使用意圖，應該有不同的頁面

**我做了什麼**

Market 模組下同時有 `PricesView`（行情查詢 + 天災對比）和 `DisastersView`（天災查詢獨立頁），兩者都在消費天災資料，但沒有合併成一個頁面。

**我遇到的問題**

DisastersView 建好之後，出現了一個看起來合理的問題：「PricesView 裡已經有天災面板了，DisastersView 是重複的嗎？使用者要查天災，去 PricesView 不就好了？」

**我怎麼想通的**

區分的關鍵是**使用者的意圖（Intent）**，不是資料的來源。

PricesView 裡，天災面板是一個**解釋工具**。使用者來這裡是想看菜價走勢，天災資料是用來解答「為什麼這段時間的菜價異常？」——它服務的是圖表解讀，而不是天災查詢本身。介面設計也反映了這一點：天災面板在 PricesView 裡是側欄，篇幅有限，排列順序是升序（配合時間軸從左讀到右），沒有縣市篩選。

DisastersView 裡，天災資料是**主角**。使用者來這裡是要查「某段時間某縣市有哪些土石流警戒」，不關心菜價。所以介面有縣市下拉篩選、有統計卡片（幾筆土石流、幾筆土石流潛勢）、卡片排列是降序（最新的在前，符合「查記錄」的閱讀習慣）。

同一份資料，兩種意圖，兩種呈現方式，兩個頁面。合併成一個頁面的代價是：每增加一個功能，頁面的意圖就變得更模糊，使用者不知道這個頁面「到底是給誰用的、用來做什麼的」。

**我學到的原則**

「資料來源相同」不是頁面合併的充分理由。合併的判斷標準應該是「使用者進入這個頁面的問題是否相同」。同一個問題 → 合併。不同的問題 → 分開，哪怕資料重疊。頁面分拆的好處是每個頁面的意圖純粹，介面可以針對那個意圖最佳化，而不是在一個頁面裡試圖同時服務兩種不同的使用者。

**下次遇到類似情況，我會先想到什麼**

「這兩個功能要合併成一頁還是分成兩頁？」的判斷：先寫出使用者進入每個頁面的問題（用一句話描述）。如果兩個問題是不同的，就分開。如果是同一個問題，才考慮合併。

---

### 條目 147 — computed 的正確定位：視圖格式轉換放在 computed，不放在 watch

**我做了什麼**

`RestDaysView.vue` 裡，API 回傳的休市日列表（`restDays`）需要按月份分組顯示，做法是宣告一個 `groupedByMonth` computed：

```typescript
const groupedByMonth = computed(() => {
  const map = new Map<string, RestDayResponseDto[]>()
  for (const d of restDays.value) {
    const key = d.restDate.substring(0, 7) // "2026-01"
    if (!map.has(key)) map.set(key, [])
    map.get(key)!.push(d)
  }
  return Array.from(map.entries())
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([key, days]) => ({
      label: `${key.substring(0, 4)} 年 ${parseInt(key.substring(5, 7))} 月`,
      days,
    }))
})
```

**我遇到的問題**

有個選擇要做：同樣的月份分組邏輯，可以放在 `computed`，也可以放在 `watch`（監聽 `restDays` 變化後，把結果寫進一個 `ref`），或者直接放在 `handleQuery` 函式裡（查詢完成後手動整理）。哪種最正確？

**我怎麼想通的**

把這三種選擇的本質分開：

`watch` 版本：`restDays` 改變 → 觸發 watch → 手動寫入 `groupedByMonth` ref。這是命令式的（「你告訴我要做什麼」），需要多一個 ref 來儲存結果，也需要確保 watch 在正確的時機被觸發。

`handleQuery` 版本：查詢後手動整理。這把「資料轉換」的邏輯耦合進「資料取得」的邏輯，職責不分離——如果未來有其他地方也會改動 `restDays`，這份轉換就會遺漏。

`computed` 版本：宣告「`groupedByMonth` 的值是由 `restDays` 推導而來的」，Vue 自動在 `restDays` 變化時重算。這是聲明式的（「你描述資料之間的關係」），沒有副作用，不需要額外的 ref，不需要記得在對的時機觸發。

這個選擇的原則是：如果一個值的計算規則是「完全由其他響應式資料決定，沒有自己的狀態」，就用 `computed`。月份分組完全符合這個條件——給定相同的 `restDays`，永遠得到相同的 `groupedByMonth`，是純函式。

**我學到的原則**

`computed` 的語意是「這個值是從其他資料推導出來的」，不是「這個值需要在某個時機被計算」。任何「把 A 格式化成 B 以顯示在畫面上」的轉換，都是 `computed` 的適用場景——因為它是響應式的推導，不是有副作用的操作。判斷標準：這個計算有沒有副作用？輸入相同結果是否永遠相同？如果兩個答案都是「是」，就用 `computed`。

**下次遇到類似情況，我會先想到什麼**

看到「watch 某個資料，然後把轉換結果寫進另一個 ref」這個模式，先問：「這份轉換有副作用嗎？有自己的狀態嗎？」如果沒有，就換成 `computed`，程式碼會更簡潔，也不會有「watch 沒觸發導致資料不同步」的 bug。

---

### 條目 148 — 一致的設計模式是最好的文件

**我做了什麼**

Market 模組的路由結構對齊 Weather 模組（都是「父路由只有 `<RouterView />`，內容在子 View」的結構），讓整個專案的路由層有了一致的設計語言。

**我遇到的問題**

這個改動的理由，在功能層面並不是「之前壞掉了，現在修好了」——原本的架構雖然不整齊，但是可以運作的。那為什麼要花時間對齊？

**我怎麼想通的**

一致性的價值不在當下，而在未來。

當整個專案的路由結構有一致的模式（「父 View = 容器，子 View = 內容」），任何人看到 MarketView 只有 `<RouterView />` 的時候，不需要讀文件就知道「這是容器，頁面在子 View 裡找」。這個知識是從 WeatherView 遷移過來的，不需要重新學習。

反過來說，如果 Weather 是容器 + 子 View，但 Market 是混合的（父路由有內容），那每次有人要加一個 Market 的子頁面，就需要先搞清楚「這個模組的規則和 Weather 不一樣」，然後判斷要不要先重構，還是繼續沿用不一致的結構。這個認知負擔會隨著模組數量增加而乘數放大。

在 Side Project 的脈絡下，這個決策尤其值得記錄，因為它展示了一種工程素養：**不只把功能做出來，還把它做得讓未來的自己（或別人）容易理解**。面試官看程式碼的時候，一致性和模式的清晰度往往比「用了多少高階技術」更能說明一個工程師的成熟度。

**我學到的原則**

一致的設計模式是一種隱性的文件：它讓讀程式碼的人能夠把在 A 模組學到的規則直接套用到 B 模組，不需要每個模組都讀一遍才知道它是怎麼組織的。建立一致性的代價是一次性的重構成本，但換來的是長期的可預測性。每次設計一個新模組，優先問「我在這個專案裡已經做過類似的事情嗎？那個模式是正確的嗎？如果是，就沿用」。

---

### 條目 149 — 資料架構的差異決定前端的設計模式

**我做了什麼**

實作畜禽行情（Pork）前端時，發現市場下拉選單的實作方式和蔬果行情完全不同，原因是後端資料的組織方式根本就不一樣。

**我遇到的問題**

蔬果行情有一個 `MarketInfos` 獨立主檔表，可以在頁面載入時就打 `GET /api/market/markets` 拿到市場清單，存進 Pinia store，讓 `MarketFilter.vue` 的下拉選單在查詢前就有選項。

豬肉行情沒有對應的主檔表。`PorkTrans` 的 `MarketName` 直接存在交易資料裡，沒有獨立的「豬肉市場清單」API。如果要照著蔬果的做法先撈清單，根本沒有可以打的 endpoint。

**我怎麼想通的**

豬肉市場的正確流程是倒過來的：

```
蔬果：先撈市場清單 → 用戶選市場 → 再撈交易資料
豬肉：先撈交易資料 → 從資料裡 distinct 出市場清單 → 市場下拉才有選項
```

這不是暫時的設計妥協，而是反映後端資料架構差異的必然結果。不同日期範圍的查詢，可能出現的市場組合也不同（某些市場只在特定時期有資料），所以豬肉的市場清單本來就沒辦法提前決定。

**我學到的原則**

前端元件的設計方式（要不要 store、幾時載入、怎麼觸發）由後端資料的架構決定，不是由 UI 的形狀決定。看起來一樣的「市場下拉」UI，背後的資料流可能完全不同：一個從 store 讀靜態清單，一個從查詢結果動態萃取。設計前端之前，先搞清楚這份資料在後端是「主檔型（提前可知）」還是「從屬型（查詢後才知）」。

**下次遇到類似情況，我會先想到什麼**

看到新的「下拉選單」需求，先問：「這份選項清單是靜態的（有獨立的主檔 API）還是動態的（從查詢結果萃取）？」靜態 → store + 頁面載入時初始化。動態 → computed + 等查詢完成後才顯示。

---

### 條目 150 — Vue 3 computed：聲明「資料之間的關係」，而不是「什麼時候計算」

**我做了什麼**

`PorkView.vue` 裡所有的衍生資料（市場清單、過濾結果、圖表資料、統計數字）都用 `computed` 實作，整個元件只有 `rawData` 這一個資料來源。

```typescript
const rawData = ref<PorkResponseDto[]>([])

const availableMarkets = computed(() =>
  [...new Set(rawData.value.map(d => d.marketName))].sort()
)

const filteredData = computed(() =>
  selectedMarket.value
    ? rawData.value.filter(d => d.marketName === selectedMarket.value)
    : rawData.value
)

const chartData = computed(() => {
  // 從 filteredData 組 Chart.js datasets
})

const maxPrice = computed(() =>
  filteredData.value.length
    ? Math.max(...filteredData.value.map(d => d.excludeFreezerAvgPrice))
    : 0
)
```

**我遇到的問題**

實作前有個疑問：「同樣的邏輯可以放在 `watch`（監聽 rawData 變化後，手動更新另一個 ref），也可以放在 `handleQuery` 裡（查詢完手動整理）。為什麼一定要 `computed`？」

**我怎麼想通的**

三種選擇的本質不同：

- `watch` 版本：命令式。「你告訴 Vue：當某個資料變化時，執行這段邏輯」。需要多一個 ref 存結果，需要確保 watch 在正確時機觸發。
- `handleQuery` 版本：把資料轉換耦合進資料取得，職責不分離。如果未來有其他地方修改 `rawData`，轉換邏輯就會遺漏。
- `computed` 版本：聲明式。「你告訴 Vue：這個值的定義是由某些資料推導而來的」。Vue 自動追蹤依賴，原始資料改變時自動重算，沒有副作用。

判斷標準很清楚：如果一個值的計算規則是「完全由其他響應式資料決定，輸入相同永遠得到相同輸出（純函式）」，就用 `computed`。

**我學到的原則**

`computed` 的語意是「這個值是由其他資料推導出來的」。任何「把 A 格式化成 B 以顯示在畫面上」的轉換都符合這個定義：它是響應式的推導，不是有副作用的操作。看到「watch 某個資料 → 手動寫入另一個 ref」這個模式，先問「這份轉換有副作用嗎？」如果沒有，換成 `computed`，程式碼更簡潔，也不會有「watch 沒觸發導致資料不同步」的 bug。

---

### 條目 151 — CancellationToken 的生命週期不能共用

**我做了什麼**

`AgriProductsTransSyncWorker` 把 Worker 生命週期的 `stoppingToken` 同時傳給了 `SemaphoreSlim.WaitAsync()` 和 `HttpClient.GetStringAsync()`，導致某個請求 timeout 或失敗後，其他還在等待的請求被連帶取消，出現「17 秒就 timeout（明顯小於 HttpClient 設定的 60 秒）」的奇怪行為。

修正方式是把三件事的取消邏輯完全分開：

```csharp
// Semaphore 等待：不受任何外部 token 影響，就是單純等位子
await semaphore.WaitAsync(CancellationToken.None);

// HTTP 請求：用獨立的計時器，和 stoppingToken 解耦
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
var json = await _httpClient.GetStringAsync(url, cts.Token);

// Worker 停止邏輯：只在 while loop 的條件判斷用 stoppingToken
while (!stoppingToken.IsCancellationRequested)
```

**我怎麼想通的**

每個 token 有它自己的語意，不能混用：

- `stoppingToken`：「整個應用程式要關閉了」。Worker 停止是全局事件，只應該影響 while loop 是否繼續，不應該影響正在進行的單一請求。
- `CancellationToken.None`：「這個操作不需要被取消」。等 Semaphore 就是純粹的等，不需要外部干預。
- `cts.Token`：「這個請求超時就取消」。HTTP 請求有自己的超時邏輯，和 Worker 生命週期完全無關。

把 `stoppingToken` 傳給 HTTP 請求，語意是「只有應用程式關閉才取消這個請求」，而不是「請求超時才取消」。當某個請求真的超時，應該只取消那一個請求，而不是影響其他正在 Semaphore 等待的請求。

**我學到的原則**

`CancellationToken` 的設計原則：每個取消 token 對應一個特定的取消理由，不要共用。`stoppingToken` 是 Worker 的，`cts.Token` 是單一請求的，`CancellationToken.None` 是「不需要取消」的。混用會導致取消訊號錯誤傳播，出現難以診斷的「為什麼這個請求這麼快就被取消了」問題。

**下次遇到類似情況，我會先想到什麼**

看到 `stoppingToken` 被傳入 HTTP 請求，先問：「我希望『應用程式關閉』和『這個請求超時』是同一件事嗎？」幾乎所有情況下答案是否定的——這兩件事應該用不同的 token 控制。

---

### 條目 152 — Vite Proxy 的正確用途：讓前端不需要知道後端的 port

**我做了什麼**

排查了一個隱性問題：`VITE_API_BASE_URL` 設定為 `http://localhost:5258`（後端 http 絕對路徑），讓 Axios 的所有請求直接打後端，完全繞過 Vite dev server 的 proxy。後端 port 換掉或前端 port 跳到 5174 時，CORS 立刻報錯。

修正方式：

```
VITE_API_BASE_URL=
```

清空之後，Axios 的 `baseURL` 是空字串，請求走相對路徑 `/api/...`，Vite proxy 接管，轉發到 `vite.config.ts` 設定的 `https://localhost:7147`。後端的 port 變化對前端完全透明。

**我怎麼想通的**

之前「沒有 CORS 問題」只是因為後端剛好跑在 5258（http profile）、前端在 5173，三個條件同時成立所以沒報錯：
1. `VITE_API_BASE_URL` 指向的 port 有後端在監聽
2. 後端 CORS 允許 5173
3. 前端 port 恰好是 5173

任何一個條件失效（後端換用 https profile 改跑 7147、前端 port 跳到 5174），CORS 立刻失效。這是「靠巧合運作的代碼」，不是正確設計。

Vite proxy 存在的意義就是讓前端開發時不需要知道後端的確切 URL。前端只知道「API 在 `/api/...`」，具體轉發到哪裡由 `vite.config.ts` 決定，和環境變數無關。

**我學到的原則**

前端開發環境的 API 請求應該走 Vite proxy，不應該直打後端。`baseURL` 在開發時應該是空字串（或省略），讓請求保持相對路徑，由 proxy 負責轉發。「直打後端」雖然在某些條件下能運作，但這意味著前端依賴後端的確切 URL，是隱性的脆弱耦合。

---

### 條目 153 — Code Review 報告是假設，資料才是證據

**我做了什麼**

Code Review 報告標記 B-1：「MarketCode 514 對應多筆 MarketInfo，下拉選單會出現重複項目，應加 DistinctBy」。我沒有直接照改，而是先去查資料庫確認：

```
514  溪湖鎮    Veg
514  彰化市場  Flower
```

514 確實對應兩筆，但它們是兩個不同的市場，不是重複資料。`GetMarketsAsync` 已有 `.Where(m => m.MarketType == marketType)` 篩選，查蔬菜時拿到溪湖鎮，查花卉時拿到彰化市場，不會混在一起。

**我遇到的問題**

如果照報告直接加 `DistinctBy(m => m.MarketCode)`，反而會把其中一個市場從選單裡吃掉，製造新的 Bug。

**我怎麼想通的**

Code Review 報告的推論有一個隱含前提：「相同 MarketCode 代表重複資料」。但實際上相同 MarketCode 跨不同 MarketType 是政府 API 的資料設計，不是錯誤。查看原始資料之後，前提不成立，結論（需要去重）也就不成立。

**我學到的原則**

Code Review 報告描述的是「看到了什麼現象」，不一定描述「現象的真正原因是什麼」。每一個 Bug 報告都要先問：「假設這是真的，背後的機制是什麼？」把資料攤出來驗證假設，再決定要不要改、改什麼。直接照改有時候比不改更危險。

---

### 條目 154 — 從 MarketCode 到 TcType：找到真正能區分類別的欄位

**我做了什麼**

`GetCropsAsync` 原本用兩段式查詢：Step 1 取特定 `MarketType` 的 `MarketCode` 清單，Step 2 用 `IN` 查 `AgriProductsTrans`。查蔬菜 tab 卻出現花卉作物，水果查詢 30 秒 Timeout。

我去看 `AgriProductsTrans` 的實際欄位，發現有 `TcType`：

```sql
SELECT DISTINCT TcType FROM market.AgriProductsTrans
-- 結果：N04、N05、N06、''
```

再對照業務語意：N04 = 蔬菜、N05 = 水果、N06 = 花卉。

**我遇到的問題**

「污染」和「Timeout」看起來是兩個獨立的問題，但追根究柢是同一個根因：查詢用了錯誤的篩選欄位。`MarketCode` 跨 `MarketType` 共用（MarketCode 400 同時出現在蔬菜和花卉的 MarketInfos），所以用 `MarketCode` 做類別篩選會污染。`TcType` 沒有索引，加上查詢邏輯結構複雜，導致全表掃描 Timeout。

**我怎麼想通的**

`AgriProductsTrans` 只有 `MarketCode`，沒有 `MarketType`。兩段式查詢的設計前提是「同一個 MarketCode 只屬於一個 MarketType」，但這個前提不成立。`TcType` 才是 `AgriProductsTrans` 裡真正代表「這筆交易屬於哪個農產品類別」的欄位，它跟 `MarketType` 的對應關係是 N04↔Veg、N05↔Fruit、N06↔Flower。把篩選改成 `WHERE TcType = 'N04'` 才是語意正確的查詢。

然後發現 `TcType` 沒有索引，`WHERE TcType = 'N05'` 對幾百萬筆全表掃描，這才是 Timeout 的真正原因。兩個問題同一個根，一起修。

**我學到的原則**

「找到能精確代表業務語意的欄位」比「用現有欄位繞路」更重要。如果一個查詢需要兩個表才能確定某個值（先查 MarketInfos 取 MarketType，再用 MarketCode 間接定位 AgriProductsTrans 的類別），就要問：AgriProductsTrans 裡有沒有直接代表這個語意的欄位？有的話直接用，省掉繞路的設計複雜度。

**下次遇到類似情況，我會先想到什麼**

查詢結果「污染」的第一個問題：「我用來篩選的欄位，在目標表裡真的是唯一識別這個維度的欄位嗎？還是它可以映射到多個不同的值？」如果是後者，就要找目標表裡真正代表那個維度的欄位。

---

### 條目 155 — 常數類別的設計動機：業務知識只定義一次

**我做了什麼**

決定把 `MarketType → TcType` 的對應關係放在獨立的 `MarketTypeMapping` 靜態類別，而不是在 `GetCropsAsync` 方法內部用 `switch` 或局部 `Dictionary`。

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

**我遇到的問題**

實作前的疑問：「這個對應關係只有 `GetCropsAsync` 用，放在方法內部不是更簡潔嗎？」

**我怎麼想通的**

業務知識（N04 = 蔬菜）和方法的私有邏輯（這個方法怎麼查資料庫）是不同層次的東西。前者屬於整個 Market 模組，後者屬於某個方法。如果 `GetPricesAsync` 之後也需要根據 `MarketType` 做對應（例如：只顯示符合 TcType 的統計），就需要再寫一次，而且可能寫出不一致的結果。常數類別讓這份知識只定義一次，修改時也只需要改一個地方。

**我學到的原則**

判斷一個值應該放在哪個作用域：如果這個值的語意屬於某個模組（對整個 Market 模組都有意義），就不應該侷限在某個方法裡。「目前只有一個地方用到」不是把它縮到方法內部的理由，「未來可能有其他地方用到」才是正確的判斷依據。

---

### 條目 156 — EF Core 查詢邊界：ToListAsync 之前的 GroupBy 和之後的 GroupBy

**我做了什麼**

修正 `WeatherService.GetStationsByCityAsync`，把「全城市觀測記錄載入記憶體後在 C# 做 GroupBy」改為「在 DB 端先取每站最新時間戳，再取完整資料」。

```csharp
// 修正前：全表載入再記憶體 GroupBy
var raw = await _context.WeatherObservations
    .Where(s => s.CityName == cityName)
    .ToListAsync();  // ← 幾千筆全進記憶體

var result = raw
    .GroupBy(s => s.StationId)  // ← 這裡已經是 C# 執行，不是 SQL
    .Select(g => g.OrderByDescending(w => w.ObservedAt).First())
    ...
```

**我遇到的問題**

理解了問題後，第一反應是「EF Core 應該能把 GroupBy + First() 翻譯成 SQL」。但實際上 EF Core 對這個 pattern 的 SQL 翻譯穩定性有歷史問題（`GroupBy + 取整列` 的 subquery 翻譯不一定能走索引），而 `ToListAsync()` 之後的 GroupBy 根本就已經是 C# 了。

**我怎麼想通的**

`ToListAsync()` 是 EF Core 的「執行邊界」。這個邊界之前，LINQ 操作都會被 EF Core 嘗試翻譯成 SQL；之後，資料已經在記憶體裡，是普通的 C# LINQ，跟資料庫完全無關。

```
.Where(...)              ← SQL WHERE
.GroupBy(...)            ← 如果在 ToListAsync 之前，嘗試翻譯為 SQL GROUP BY
.ToListAsync()           ← ← ← 執行邊界，資料進記憶體
.GroupBy(...)            ← 這行已經是 C# LINQ，不是 SQL
```

原本的寫法把 `ToListAsync()` 放在第一行 `.Where()` 之後，後面的 `GroupBy` 和 `Select` 都在 C# 裡執行，但執行的是幾千筆資料。

修正後的兩段式把「找每站最新時間戳」這個聚合操作（`GroupBy + Max`）放在 `ToListAsync()` 之前，讓 SQL Server 用它最擅長的方式處理（GROUP BY + MAX，有索引時極快），只把幾十筆時間戳拉回記憶體，第二段查詢再拿這幾十筆去取完整資料。

**我學到的原則**

遇到「記憶體 GroupBy 取最大/最新值」這個 pattern，先問：「這個 GroupBy 可以在 ToListAsync 之前做嗎？」如果可以，SQL Server 處理 `GROUP BY + MAX()` 遠比 C# 處理幾千筆 GroupBy 有效率。`ToListAsync()` 之後的操作要有意識地問自己：「這裡的資料量是幾筆？是幾十筆還是幾千筆？」

---

### 條目 157 — ClaimTypes.Role 存的是名稱，不是 GUID

**我做了什麼**

修正 `NavController` → `NavService` 的 role 傳遞邏輯。原本 Controller 從 JWT Claim 取到角色名稱（`"Admin"`），命名為 `roleId`，NavService 把它當 GUID 去查 `RoleModulePermissions.RoleId`。結果查不到任何記錄，靜默 fallback 到 Guest，已登入用戶的導覽列永遠和未登入相同。

修正後：

```csharp
// Controller：變數改名，語意準確
var roleName = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;

// Service：傳入 roleName，用 RoleManager 解析成真正的 GUID
var role = await _roleManager.FindByNameAsync(roleName);
targetRoleId = role?.Id ?? guestRole.Id;
```

**我遇到的問題**

為什麼這個 Bug 不容易被發現？因為它是「靜默失敗」——不拋例外，不回 500，功能「看起來運作」，只是顯示的是 Guest 的導覽，而不是 Admin 的導覽。如果 Guest 和 Admin 的導覽差異不明顯，開發過程中就很容易漏掉。

**我怎麼想通的**

`ClaimTypes.Role` Claim 的值由程式碼在登入時決定。查 `UserManager.AddToRoleAsync` 或 JWT 產生邏輯，就能確認存進 Claim 的是什麼值。Identity 框架的慣例是把角色「名稱」存進 Claim，不是 GUID，因為名稱是人可讀的，GUID 不是。但資料庫裡 `RoleModulePermissions` 用的是 `RoleId`（GUID），這個轉換需要透過 `RoleManager.FindByNameAsync`。

**我學到的原則**

「靜默失敗」是最難抓的 Bug 類型。它不產生錯誤訊號，只是悄悄地走了錯誤的 fallback 路徑。防範方式：在 fallback 路徑加 `LogWarning`，讓「不正常的 fallback」變成可見的信號。這個 PR 的修正裡已經加了：`_logger.LogWarning("Role '{RoleName}' 不存在，回退至 Guest 權限顯示", roleName)`。

---

### 條目 158 — RabbitMQ Consumer 的 Queue：宣告、Binding、Consume 必須是同一個

**我做了什麼**

修正 `PriceUpdatedConsumer` 的 Queue 管理錯誤。原本 `StartAsync` 宣告並 Binding 了一個臨時 Queue，`ExecuteAsync` 裡又呼叫一次 `QueueDeclareAsync()`，產生另一個全新的 Queue，對這個新 Queue（沒有任何 Binding）呼叫 `BasicConsumeAsync`。

```csharp
// 修正前：ExecuteAsync 裡重新宣告（錯誤）
queue: (await _channel.QueueDeclareAsync(cancellationToken: stoppingToken)).QueueName,

// 修正後：重用 StartAsync 宣告並 Binding 好的 Queue name
private string _queueName = string.Empty;
// StartAsync 裡：_queueName = queueResult.QueueName;
// ExecuteAsync 裡：
queue: _queueName,
```

**我遇到的問題**

這個 Bug 的特徵是：程式能跑、Consumer 也能啟動、不報錯，但就是永遠收不到訊息。因為 `BasicConsumeAsync` 訂閱的 Queue 沒有 Binding，永遠不會有訊息送進來。

**我怎麼想通的**

RabbitMQ 的訊息流向：`Producer → Exchange → (依 routing key) → Queue → Consumer`。Exchange 的 Binding 決定「哪些訊息會進這個 Queue」。`QueueDeclareAsync()` 每次都會產生一個**新的**臨時 Queue，即使名稱格式相同（`amq.gen-xxx`），每次呼叫得到的是不同的 Queue，Binding 不會自動跟過來。

所以 `StartAsync` 做好 Binding 之後，`ExecuteAsync` 必須用**同一個** Queue name 去 `BasicConsumeAsync`，不能重新宣告。把 Queue name 存在類別欄位 `_queueName` 是最直接的解法。

**我學到的原則**

RabbitMQ 的三步驟「宣告 Queue → Binding Exchange → Consume」必須針對**同一個** Queue。臨時 Queue 的名稱由 broker 分配，每次 `QueueDeclareAsync()` 呼叫都可能產生不同的名稱。任何需要在方法之間共享 Queue name 的場景，都應該把 Queue name 存在類別欄位裡，不要重複呼叫宣告。

---

### 條目 159 — 變數命名的語意邊界：query 前綴為什麼會誤導人

**我做了什麼**

Code Review 指出 `GetPorkAsync` 裡有一個叫 `queryPork` 的變數，實際上存的是已從資料庫取回的結果集（`List<PorkTransResponseDto>`）。我把它改名為 `porkList`。

**我遇到的問題**

改之前我沒有意識到這是個命名問題，因為程式可以跑、結果正確，感覺不需要動。

**我怎麼想通的**

在 C# / EF Core 的語境裡，`query` 這個字有特定的隱含意義：它通常指 `IQueryable<T>`，也就是「尚未執行、還沒有打到資料庫的查詢物件」。一個已經 `ToListAsync()` 之後的變數叫 `queryXxx`，會讓閱讀者停下來想「等一下，這還是 IQueryable 嗎？還是已經執行了？」這個停頓就是命名造成的認知成本。

`porkList` 直接說出「這是一份豬肉資料的 List」，沒有任何歧義。

**我學到的原則**

變數命名應該反映它「是什麼」，而不是「它從哪個操作來的」。`query` 作為前綴是描述操作過程，不是描述結果。結果已經確定（List），就用能直接說明結果形狀的名稱。

---

### 條目 160 — 變數名稱應該反映資料的形狀，不只是資料的來源

**我做了什麼**

`GetDisastersAsync` 裡有個變數叫 `raw`，存的是 `AgriProductsTrans` 資料已經過 `GroupBy` 聚合的結果。我把它改名為 `groupedRaw`。

**我遇到的問題**

`raw` 讓我以為這是「未加工的原始資料」，但實際上它已經是聚合過的中間結果。兩個字的差距，造成閱讀時對資料形狀的錯誤預設。

**我怎麼想通的**

「資料的形狀」包含兩個維度：它是什麼型別（List、Dictionary、IGrouping...）以及它經過了什麼處理（raw、grouped、filtered、sorted）。`raw` 只說了「未加工」，但沒說「已聚合」。`groupedRaw` 同時說出了兩件事：它包含原始欄位（raw），但已經過 GroupBy 的組織（grouped）。

**我學到的原則**

中間變數的命名要能讓人在不追蹤查詢的情況下，猜到它的資料形狀。如果一個變數存的是「已 GroupBy 但還沒投影成 DTO 的中間結果」，它的名稱就應該說出這個中間狀態。

---

### 條目 161 — 防禦性設計的判斷依據：代價 vs. 不可預測性

**我做了什麼**

在 `GetDisastersAsync` 的查詢鏈加入 `.Take(5000)` 上限。

**我遇到的問題**

一開始覺得「現在資料量還好，加不加沒差」。但這個想法忽略了一件事：`DebrisAlertRecords` 是歷史型資料集，每次有土石流或大規模崩塌警戒就會新增一筆，沒有設計上的上限，會持續累積。

**我怎麼想通的**

防禦性設計的判斷不是「現在需不需要」，而是「不加的代價是什麼」。不加 `Take()` 的代價是：資料量累積到某個臨界點之後，查詢會突然變慢，而且這個臨界點是不可預測的。加 `Take(5000)` 的代價是：5000 筆之後的資料在這支 API 看不到（但前端只渲染圖表，不需要全量）。代價明確且可接受；不加的代價不明確且不可預測。

**我學到的原則**

防禦性設計不是「預防不可能發生的事」，而是「讓潛在的風險變成可見的邊界」。`Take(5000)` 讓「這支 API 最多處理多少資料」這件事從隱性變成顯性。面試時說「我知道這個上限，也知道超過時前端需要改成分頁」，比「我沒有加上限因為現在資料量還好」更展現設計思維。

---

### 條目 162 — 什麼時候應該把 lambda 抽成具名方法

**我做了什麼**

`GetRestDaysAsync` 裡的民國年轉換邏輯原本是一個匿名 lambda：

```csharp
.Select(r => {
    try { return (DateOnly?)new DateOnly(r.Year + 1911, r.Month, r.RestDay); }
    catch { return null; }
})
```

我把它抽出來，放進 `DateHelper`：

```csharp
public static DateOnly? ConvertRocRestDay(int rocYear, int month, int day)
```

**我遇到的問題**

抽出去之前，我覺得這段邏輯「放在這裡也沒問題，反正就一行」。但問題不在長度，而在語意的可見性。

**我怎麼想通的**

Lambda 的問題是它沒有名字。閱讀者遇到匿名 lambda 時，必須先讀完整段程式碼才能理解「這在做什麼」。而 `.Select(r => DateHelper.ConvertRocRestDay(r.Year, r.Month, r.RestDay))` 則是直接在 LINQ 鏈裡說出「這一步在做民國年轉換」，不需要讀實作就能理解意圖。

**選擇 DateHelper 而非 private static 的判斷**

民國年轉換是「台灣農業資料日期格式轉換」這個領域的通用知識，不是 MarketService 的私有邏輯。如果把它放成 `private static`，代表「這個知識只屬於 MarketService」。事實上 DateHelper 本來就是收集這類知識的地方，新的方法應該和其他民國日期方法放在一起。

**我學到的原則**

抽出具名方法的兩個判斷依據：（1）這段邏輯有沒有一個清楚的名字可以說明它在做什麼？（2）這個邏輯是方法的私有細節，還是可以被其他地方共用的領域知識？兩個問題的答案是「有名字 + 屬於領域知識」時，抽出去。

---

### 條目 163 — Doc Comment 的真正用途：讓設計決策留在程式碼裡

**我做了什麼**

為 `WeatherService.GetStationsByCityAsync` 加入完整的 `/// <summary>`，說明兩段式查詢策略、每個步驟的設計動機，以及末段記憶體 GroupBy 的防護理由。同時也補齊了 `DateHelper` 所有方法的 doc comment，每個方法都有具體的輸入輸出範例。

**我遇到的問題**

寫 doc comment 的直覺是「說明這個方法接受什麼、回傳什麼」，但這樣寫出來的 summary 往往和方法簽名完全重複，沒有增加任何資訊。

**我怎麼想通的**

方法簽名已經說了「接受什麼和回傳什麼」。Doc comment 應該說「簽名沒辦法說的事情」：

- 為什麼用兩段式而不是一段？（EF Core GroupBy + First() 翻譯穩定性問題）
- 末段 GroupBy 在記憶體裡執行，開銷為什麼可以接受？（資料量只有站台數，幾十筆）
- 什麼情況下會回傳 null？（DateHelper 方法：月份超出範圍、日期組合不合法）

**DateHelper doc comment 的範例設計**

```
/// 輸入：(107, 7, 15)　→　輸出：DateOnly(2018, 7, 15)
/// 輸入：(107, 2, 30)　→　輸出：null（2 月沒有 30 日）
/// 輸入：(107, 13, 1)　→　輸出：null（月份超出範圍）
```

範例的價值在於它是可執行的規格。閱讀者不需要在腦中計算「民國 107 年 7 月 15 日是西元幾年」，直接看答案就知道這個方法做什麼。

**我學到的原則**

好的 doc comment 說的是「如果你要使用這個方法，你需要知道什麼」，而不是「這個方法的程式碼在做什麼」（後者讀程式碼本身就能知道）。設計決策、邊界條件、為什麼不是用另一種更直覺的寫法——這些才是 doc comment 應該說的。

---

### 條目 164 — Program.cs 模組化：讓物理結構對應架構文件

**我做了什麼**

把 `Program.cs` 裡超過 60 行的平鋪 DI 註冊，按模組職責拆分成五個 Extension Method 檔案，放在 `TaiwanAgri.Web/Extensions/` 資料夾下。最終 `Program.cs` 的 builder 區段變成五行：

```csharp
builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddMarketModule(builder.Configuration);
builder.Services.AddWeatherModule(builder.Configuration);
builder.Services.AddCoreModule(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
```

**我遇到的問題**

原本的平鋪寫法「可以運作」，但每次要找某個 Service 的 DI 註冊在哪裡，必須在 60 行裡用 Ctrl+F 搜尋。更根本的問題是：程式碼的物理結構沒有反映架構設計的模組邊界。

**我怎麼想通的**

SA/SD 文件第 3.3 節明確定義了五個 DbContext，對應五個模組邊界。Program.cs 的平鋪寫法讓這五個邊界消失了——讀者必須先讀過 SA/SD 文件，才能理解為什麼某些 Service 被放在一起。

Extension Method 讓邊界變成顯性的：`AddMarketModule()` 就是「Market 模組的所有依賴」，不多也不少。名稱本身就是文件。

**面試時能說的話**

「你看 Program.cs，這五行對應到 SA/SD 文件第 3.3 節定義的五個模組。每個 Extension Method 的名稱直接反映模組邊界，不需要再讀文件才能理解 DI 的組織邏輯。」

**我學到的原則**

「讓程式碼的物理結構對應架構設計的邏輯邊界」不是美化，而是降低未來維護時的認知負擔。當 Program.cs 的五行直接對應架構圖的五個方塊，閱讀者不需要在程式碼和文件之間來回切換才能理解整體設計。

---

### 條目 165 — 設定與程式碼分離：什麼算是環境事實，什麼算是程式邏輯

**我做了什麼**

把 `InfrastructureExtensions.cs` 裡硬編碼的 `localhost:5173` / `localhost:5174` 移入 `appsettings.Development.json` 的 `Cors.AllowedOrigins` 設定區塊，程式碼改為從 `configuration.GetSection()` 讀取。

**我遇到的問題**

一開始覺得這個改動很瑣碎，因為 CORS 的 localhost port 幾乎不會變，硬編碼「也沒什麼問題」。

**我怎麼想通的**

分辨的問題是：「這個值如果需要修改，應該讓誰來修改，以什麼方式修改？」

硬編碼在 C# 裡：需要修改程式碼 → 重新編譯 → 部署。修改者必須知道這個值藏在 `InfrastructureExtensions.cs` 的哪一行。

放在 `appsettings.Development.json`：修改設定檔即可，不需要重新編譯。所有環境設定集中在同一個地方，不散落在程式碼裡。

`localhost` 的 port 是「開發環境的事實」，不是「CORS 策略的邏輯」。環境事實屬於設定，程式邏輯屬於程式碼。這兩件事混在一起，是讓程式碼在不同環境（dev / staging / prod）部署時容易出問題的根源。

**我學到的原則**

判斷一個值該放設定還是程式碼：如果這個值在不同環境（本機開發 / 測試機 / 正式機）可能不同，它就是環境事實，屬於設定檔。如果它是邏輯規則（例如「CORS 只允許特定 method 和 header」），它屬於程式碼。

---

### 條目 166 — 術語統一：為什麼 CSS class 名稱也算是「介面契約」

**我做了什麼**

把 `MarketFilter.vue` 裡的 `chip` / `chip-container` / `chip-list` 改名為 `crop-btn` / `crop-container` / `crop-list`，template 和 `<style scoped>` 同步更新。

**我遇到的問題**

原本的 `chip` 是 UI Component Library（Material Design / Vuetify）的通用術語，但本專案沒有使用這些框架，`chip` 只是我在設計時套用了外部術語，沒有考慮它對本專案的語意是否合適。

**我怎麼想通的**

「作物多選按鈕」是特定業務元件，不是通用 UI 元件。用 `crop-btn` 命名，任何人看到這個 class 就知道「這是作物選擇按鈕的樣式」。用 `chip` 命名，需要先理解「哦，作者把作物選擇按鈕設計成 Chip 的形式」才能建立連結。

CSS class 名稱本身是一種介面文件。當 class 名稱說的是業務語意而非 UI Pattern 術語，程式碼的自解釋性更高。

**另一個收穫：template 和 style 必須同步**

這次修改一個很容易漏掉的陷阱是：template 裡的 class 名稱改了，但 `<style scoped>` 裡的 selector 如果沒有同步更新，樣式會靜默失效——不報錯、不崩潰，只是按鈕的 hover / selected / disabled 效果全部消失。這是「靜默失敗」的一個典型案例。

**我學到的原則**

CSS class 名稱應該反映「這個元素在業務上是什麼」，而不是「它長什麼樣子」或「它像哪個通用 UI 元件」。修改 class 名稱時，template 和 style 是一組，不能只改一個。

---

### 條目 167 — 「可改可不改」的決策框架：能說清楚為什麼才算決策

**我做了什麼**

這輪 Code Review 有幾個「可改可不改」的項目，我最終選擇不改，並且記錄了不改的理由。

**F-2：7 日均線不另抽 computed**

`calcMA` 的計算已包裹在 `chartData computed` 內。`prices` 不變時，`chartData` 不重算，`calcMA` 自然也不重算——快取邊界已經在 `chartData` 這層建立了。如果要進一步優化，可以把 `calcMA` 的結果單獨抽成 `movingAverageMap computed`，讓其他 computed 可以共用這個計算結果。但目前的使用場景只有 `chartData` 一個地方用到均線，沒有共用需求，抽出去只增加間接層，不增加清晰度。

**M-4：try-catch 不改 DateOnly.TryCreate**

`DateOnly.TryCreate(year, month, day, out DateOnly result)` 的用途是驗證年月日組合是否構成合法日期（例如 2 月 30 日非法）。現有的 `try-catch` 包的是格式轉換失敗，語意上是「嘗試建立 DateOnly，如果失敗（日期無效）就回傳 null」。把 try-catch 改成 TryCreate 不會改變行為，但會讓閱讀者覺得「作者是否理解兩者的差異」變成一個問題。為改而改不是進步。

**我學到的原則**

「可改可不改」不是隨機選擇，而是需要一個決策框架：（1）改了有什麼收益？（2）不改有什麼風險？（3）兩者的代價各是什麼？三個問題都能回答，才算是技術決策。面試時說「我知道可以改成 TryCreate，但語意上 try-catch 在這裡更準確地表達了意圖」，遠比默默照改更有說服力。

---

### 條目 168 — pure function 的歸屬判斷：目前的使用者數量才是決定因素

**我做了什麼**

把 `GetPricesAsync` 裡的 Cache Key 組裝邏輯抽出為 `BuildPricesCacheKey()` private static 方法，放在 `MarketService` 類別內部。

**我遇到的問題**

一開始我問了自己：這個方法應該放在 `MarketService` 內部（private static），還是放到 Core 層的工具類別（例如 CacheKeyHelper）？理由是「未來的 `PriceUpdatedConsumer` 做 Cache Invalidation 時，可能也需要組出同樣格式的 Key」。

**我怎麼想通的**

追蹤這個「未來需求」的實際設計：W15 的 `PriceUpdatedConsumer` Cache Invalidation 的設計是清除所有 `market:prices:*` 開頭的 Key，不需要組出精確的完整 Cache Key。也就是說，「第二個使用者」根本不存在，只是想像中的未來需求。

把「沒有第二個使用者」的知識放到共用層，是過度設計的典型形式——用架構複雜度換取一個實際上不會發生的靈活性。

對照 PR #031 的 `ConvertRocRestDay`：那個方法放入 DateHelper，是因為民國年轉換是跨模組可能共用的領域知識（有潛在的第二個、第三個使用者）。`BuildPricesCacheKey` 只服務 `MarketService` 一個地方，放在 `private static` 是正確的邊界。

**我學到的原則**

判斷一個純函式放在哪裡，問一個問題：「現在有幾個使用者？」一個 → `private static`，保持在使用它的類別裡。多個（或有合理的跨模組共用理由）→ 搬到工具類別。「未來可能需要」不是搬移的理由，「現在確實需要」才是。

---

### 條目 169 — Doc Comment 說明隱性設計決策，而非重述方法簽名

**我做了什麼**

在 `BuildPricesCacheKey()` 加入 `/// <summary>`，特別說明了兩件在方法簽名裡看不到的事：（1）cropCodes 需要排序的原因；（2）為什麼用 finalStart / finalEnd 而非原始參數。

```csharp
/// <summary>
/// 組裝 GetPricesAsync 的 Redis Cache Key。
/// cropCodes 排序後 Join，確保 ["A01","B02"] 和 ["B02","A01"] 命中同一個 cache。
/// 使用 finalStart / finalEnd（已解析的實際日期），防止 null 預設值碰撞到同一個 Key。
/// 格式：market:prices:{marketType}:{sortedCrops}:{marketCode}:{startDate}:{endDate}
/// </summary>
```

**我遇到的問題**

一開始寫 doc comment 的直覺是描述「這個方法做了什麼」——組裝 Cache Key。但這件事看方法名稱就知道了，不需要 doc comment 重複。

**我怎麼想通的**

讀者真正需要知道的是「為什麼這樣做」：為什麼要排序 cropCodes？如果不排序會怎樣？為什麼用 finalStart 而不用原始的 null？如果用 null 會有什麼 bug？

這兩個「為什麼」如果沒有寫進 doc comment，六個月後連自己都可能不記得，更遑論其他讀者。

**另一個收穫：排序的 Cache Key 是反直覺的設計**

`["A01","B02"]` 和 `["B02","A01"]` 在業務語意上是同一個查詢（選了 A01 和 B02 這兩種作物），但如果直接 Join 不排序，會產生兩個不同的 Cache Key，命中率下降一半。排序這個動作本身很簡單，但「需要排序的理由」才是需要被記錄的設計知識。

**我學到的原則**

好的 doc comment 回答「為什麼」，不重複「是什麼」。方法名稱說「做什麼」，方法簽名說「輸入輸出」，doc comment 說「為什麼這樣設計、邊界條件是什麼、和其他選擇相比有什麼取捨」。

---

### 條目 170 — 輸入驗證的防禦邊界在哪裡：白名單 vs enum vs 靜默空清單

**我做了什麼**

在 `MarketController` 新增 `IsValidMarketType()` private static 方法，在 GetMarkets / GetCrops / GetPrices 三個 Action 加入白名單驗證，傳入非法 `marketType` 時回傳 400 BadRequest。

**我遇到的問題**

這個問題讓我思考了三種解法：

第一種：什麼都不做，讓 Service 靜默回傳空清單。問題是面試現場打非法值進去，拿到空陣列，說不清楚這是「設計」還是「沒處理到」。

第二種：enum 重構，讓編譯器在型別層面阻擋非法值。問題是 enum 在 EF Core 查詢中需要手動 `.ToString()` 轉換，容易漏，而且改動範圍橫跨 Controller / Service / Mapping，風險不小。

第三種：Controller 白名單，pattern matching 三個合法字串。改動只在 Controller，不影響 Service，也不影響 EF Core 查詢。

**我怎麼想通的**

enum 的型別安全收益在「有 switch/case 分支行為」的情境下最大——不同的 enum 值走不同的程式碼路徑，編譯器確保所有 case 都被處理了。但 `marketType` 在這個專案裡只是一個 SQL `WHERE` 過濾條件，從頭到尾沒有任何分支行為，enum 的核心收益在這裡根本沒有觸發點。

Controller 層本來就是輸入驗證的第一道防線。把白名單放在這裡是最自然的位置，讓錯誤路徑（400 BadRequest + 清楚的訊息）和正常路徑（200 OK）一樣清晰可見。

**我學到的原則**

enum 解決的是「有分支行為的閉合集合」問題，不是「要防止非法字串輸入」的問題。防止非法輸入是 Controller 的職責，用白名單 + BadRequest 是最直接、最低代價的解法。當一個工具（enum）的代價（EF Core 轉換、改動範圍）高於它的收益（型別安全），就不是正確的工具。

---

### 條目 171 — 設定外化的判斷：哪些值屬於設定，哪些屬於程式邏輯

**我做了什麼**

把 `GetDisastersAsync` 裡的 `.Take(5000)` 硬編碼，改為從 `appsettings.json` 的 `MarketQueryLimits:DisasterRecordLimit` 讀取，並保留 `GetValue<int>` 的 fallback 預設值 5000。

**我遇到的問題**

一開始覺得這個改動「有點多餘」，因為 5000 這個數字短期內不太可能需要修改。而且 fallback 預設值還是 5000，改完之後程式行為完全一樣。

**我怎麼想通的**

分辨的問題不是「這個值需不需要常常修改」，而是「如果需要修改，應該以什麼代價修改」。

硬編碼在 C# 裡：找到那一行 → 修改 → 重新編譯 → 重新部署。修改者必須知道這個值藏在哪個 Service 的哪個方法裡。

放在 `appsettings.json`：找到設定檔 → 改數字 → 重啟。所有「可能需要調整的業務限制」集中在設定檔，不散落在程式碼的各個角落。

`GetValue<int>` 的 fallback 設計則是另一個習慣：設定檔是優化，程式應該在設定缺失時有合理的預設行為，而不是崩潰。

**為什麼 5000 這個數字說得出口**

`DebrisAlertRecords` 是歷史型資料，線性累積。估算：30 個災害事件 × 每個事件最多 150 個受影響村落 = 4,500 筆。5,000 是有餘量的上限，不是隨便填的。面試時「能說出估算依據」和「說不出來但就是寫了 5000」是完全不同的等級。

**我學到的原則**

「可能需要調整的業務限制」屬於設定，「程式邏輯的規則」屬於程式碼。判斷依據是修改的原因和修改者的身份——純業務需求的數字（查詢上限、TTL、retry 次數）應該在設定檔，不需要懂程式的人也能改。

---

### 條目 172 — 邊界值測試：選什麼案例，比怎麼寫更重要

**我做了什麼**

為 `DateHelper.ConvertRocRestDay` 新增 6 個 xUnit 測試，覆蓋 Happy Path（正常日期、閏年2/29）和 Null Path（2/30、非閏年2/29、月份13、月份0）。Test Explorer 顯示 6/6 全綠。

**我遇到的問題**

一開始想「隨便寫幾個測試讓它過」，比如 `(100, 1, 1)` → `DateOnly(2011, 1, 1)`。這當然能過，但沒有意義——這個案例只驗證了加法（`rocYear + 1911`），沒有觸及任何邊界。

**我怎麼想通的**

`ConvertRocRestDay` 設計的核心承諾是：**日期無效時回傳 null，不拋例外**。要驗證這個承諾，需要找到「什麼情況下 `new DateOnly()` 會拋例外」——就是 `ConvertRocRestDay` 要捕捉的邊界。

自然推導出的案例：
- `2/30`：任何年份的2月都沒有30日 → 最明確的「必回 null」
- 非閏年的 `2/29`：依年份而異 → 展示「設計者知道閏年的語意」
- 月份 13 / 月份 0：月份範圍邊界 → 完整覆蓋月份驗證

**閏年對組的設計意圖**

`(94, 2, 29)` 回 null 和 `(109, 2, 29)` 回 DateOnly 這兩個案例必須一起寫才完整。只寫其中一個，讀者看不出「作者理解閏年語意」——只寫 null 案例，可能只是「2/29 就回 null」；只寫成功案例，可能只是「2/29 可以輸入」。兩個合在一起，才能清楚傳達：「同樣的結構，閏年合法，非閏年不合法，程式碼正確區分了兩者。」

**從「0 測試」升級的意義**

這個專案在這個 PR 之前沒有任何測試。測試的價值不只是「確保程式碼正確」，更是「讓讀者（包括面試官）看到你對邊界條件的思考方式」。選 `ConvertRocRestDay` 作為第一個測試標的，是因為它是純函式（零 mock 負擔）、邊界清楚（民國年 + 日期合法性）、且展示了真實的設計知識（閏年判斷）。

**我學到的原則**

寫測試案例之前，先問：「這個方法的核心承諾是什麼？什麼輸入最能驗證這個承諾？」從承諾出發選案例，而不是從「哪個輸入最容易構造」出發。邊界值的價值在於它們剛好在合法與非法的分界線上，最能展示設計者對語意的理解。

---

### 條目 173 — JWT 是格式，OAuth 是協議：把概念放對抽屜才不會混用

**我做了什麼**

在實作 W15 JWT 登入時，第一次碰到「JWT」和「OAuth」這兩個詞，一開始以為它們是同一件事或上下位關係。

**我遇到的問題**

文件和技術文章常常把 JWT 和 OAuth 混著講（「用 OAuth 登入」「發行 JWT」），沒有脈絡的情況下很容易以為它們是互斥的選擇，或者 JWT 是 OAuth 的一部分。

**我怎麼想通的**

用類比拆開來看：

| 概念 | 比喻 |
|------|------|
| JWT | 手環的材質（防偽、可印資訊、有效期限） |
| OAuth | 讓第三方（Google、Facebook）幫你發手環的協議 |

這個專案用 JWT 自己發手環（`JwtSecurityTokenHandler`），不依賴 Google 登入。OAuth 是「讓別人幫你做這件事」的協議，和 JWT 本身不是同一個層次的概念。換句話說，OAuth 登入的 token 可以是 JWT 格式，也可以不是；JWT 的使用也不需要 OAuth 流程。

**我學到的原則**

遇到兩個常被放在一起的技術詞彙，先問「它們的層次一樣嗎？」JWT 是 token 格式（規格書），OAuth 是授權協議（流程），不同層次的東西不能拿來二選一比較。

---

### 條目 174 — 密碼 hash 和 JWT 簽名是兩件完全不同的事

**我做了什麼**

實作 `AuthService.LoginAsync`，理解了 `SignInManager.CheckPasswordSignInAsync` 和 `JwtSecurityTokenHandler.WriteToken` 各自在做什麼。

**我遇到的問題**

一開始把「密碼 hash」和「JWT 密鑰簽名」混在一起，以為 token 就是由密碼 hash 產生的，或者密鑰就是密碼經過某種處理。

**我怎麼想通的**

登入流程實際上是兩條完全獨立的管線：

**管線一：密碼驗證**
```
使用者輸入明文密碼
→ Identity 把輸入的密碼重新 hash 一次
→ 比對 DB 裡存的 hash 值
→ 相符 → 驗證通過（密碼明文從未離開這個步驟）
```

**管線二：JWT 產生**
```
Claims（userId, email, role）打包
→ 用 SecretKey 做 HMAC-SHA256 簽名
→ 輸出 Base64URL 編碼的 token 字串
```

兩條管線唯一的交點是「管線一成功之後，管線二才執行」。密碼 hash 是存在 DB 的驗證憑證，JWT 密鑰是伺服器自己知道的簽名印章，兩者沒有任何直接關係。

**我學到的原則**

程式碼的執行順序和邏輯層次是不同的維度。`B → D → A → C`（查帳號 → 驗密碼 → 產生 token → 回傳）描述的是執行順序，但每個步驟背後的機制完全獨立。理解某個系統之前，先分清楚「這是順序問題」還是「這是機制問題」。

---

### 條目 175 — 無狀態 token：為什麼 JWT 不需要存 DB

**我做了什麼**

理解了 JWT 的「無狀態」設計，以及這和固定 token 存 DB 的根本差異。

**我遇到的問題**

最初的直覺是「token 應該存在 DB 才能驗證它是否有效」，因為傳統的 Session 機制就是這樣運作的（Server 端存 Session ID，收到請求時查表確認）。

**我怎麼想通的**

JWT 的驗證不依賴「查名單」，而是依賴「驗印章」：

**Session 模式（有狀態）**
```
收到 token → 去 DB 查這個 token 有沒有登記過 → 有 → 放行
問題：每次請求都查 DB；水平擴展需要共享 Session 存儲
```

**JWT 模式（無狀態）**
```
收到 token → 用密鑰驗簽章數學正確性 → 正確 → 放行
優點：純運算，不查 DB；任何持有密鑰的伺服器都能驗證
```

JWT 的安全性不依賴「這個 token 有沒有被登記」，而是依賴「這個簽章在數學上是否能用我的密鑰還原」。因為沒有密鑰就無法偽造合法簽章，所以不需要中央查表。

代價是：JWT 一旦發行就無法立即廢止（只能等 `exp` 到期）。固定 token 存 DB 可以即時撤銷（刪掉那筆記錄），JWT 不行。這是無狀態設計的已知取捨，本專案設定 7 天過期，在 Side Project 規模下可接受。

**我學到的原則**

每一個設計決策都有取捨，「無法立即廢止」不是 JWT 的 bug，而是無狀態設計的代價。面試時能說出「知道這個限制，在什麼情況下可接受，在什麼情況下需要搭配黑名單機制（Redis 存廢止的 token ID）」，才是真正理解了這個設計。

---

### 條目 176 — Claims 是手環上的資料：三個就夠了

**我做了什麼**

設計 JWT 的 Claims，選擇放什麼資料進去。

**我遇到的問題**

一開始想把很多資訊都放進 Claims（顯示名稱、UserType、PreferredCity...），因為覺得「放越多，前端要查的資料越少」。

**我怎麼想通的**

Claims 的正確用途是「授權決策所需的最小資訊」，不是「前端所有可能會用到的資訊」。

判斷依據：**這個 Claim 會影響「後端決定放不放行」嗎？**

| Claim | 用途 | 放？ |
|-------|------|------|
| `NameIdentifier`（userId） | 後端查通知、偏好設定需要知道是誰 | ✅ |
| `Email` | 顯示用，後端有時需要識別 | ✅ |
| `Role` | 後端做 `[Authorize(Roles = "Admin")]` 需要 | ✅ |
| `DisplayName` | 只是 UI 顯示，不影響授權決策 | ❌ 前端從 `authStore.user` 取 |
| `UserType` | 目前沒有基於 UserType 的授權邏輯 | ❌ 過早放入 |

token 每次請求都要傳輸，Claims 越多 token 越大，每次請求的 payload 越重。最小必要集合是正確的設計方向。

**我學到的原則**

設計時問「必要性」而非「可用性」。Claims 裡放的是「授權需要的事實」，不是「前端可能有用的資料」。兩者的差別決定了系統的精簡程度。

---

### 條目 177 — Pinia 的使用邊界：為什麼 api 層不能 import store

**我做了什麼**

設計 `authClient.ts` 時，理解了為什麼不能在 api 層直接 `import { useAuthStore }`，改用 `localStorage.getItem('token')`。

**我遇到的問題**

一開始覺得 `localStorage` 是個「繞路」的做法，真正想要的是 `authStore.token`，為什麼不直接拿？

**我怎麼想通的**

這是兩個層次的問題：

**技術層面（直接原因）**：`useAuthStore()` 需要在 Vue 的 `setup()` 或 Composition API 環境下呼叫，因為 Pinia 依賴 Vue 的響應式系統（`getCurrentInstance()`）。在純 TypeScript 模組（api 層）裡呼叫，會得到：
```
Error: getActivePinia() was called with no active Pinia
```

**架構層面（根本原因）**：即使技術上可以繞過這個限制，在 api 層 import store 也是錯的，因為它違反了單向依賴：

```
Vue 元件（Component）
      ↓ 依賴
Pinia Store
      ↓ 依賴
API 層（axios）
```

箭頭只能往下。api 層向上依賴 store，等於讓底層知道上層的狀態，這讓 api 層無法在沒有 Vue 環境的地方使用（比如 Node.js 測試環境、Server-Side Rendering）。

`localStorage` 是瀏覽器原生的全域存儲，不屬於任何一個應用層，任何地方都能讀，符合「api 層從環境取資料，而非從應用狀態取資料」的原則。

**我學到的原則**

架構邊界的存在不只是為了代碼整潔，更是為了讓每一層都能獨立測試和替換。「技術上繞得過去」不等於「架構上是對的」。當需要繞路才能實作某件事時，先問繞路的方向對不對，再問繞路的方式對不對。

---

### 條目 178 — [FromBody] vs [FromQuery]：敏感資料絕對不能放 URL

**我做了什麼**

設計 `AuthController` 時，使用 `[FromBody]` 接收 `LoginRequestDto`，理解了和 `[FromQuery]` 的本質差異。

**我遇到的問題**

之前所有的 Controller 都是 `[FromQuery]`（日期、作物代碼、市場類型），這是第一次用到 `[FromBody]`，一開始不確定什麼時候應該用哪個。

**我怎麼想通的**

| | `[FromQuery]` | `[FromBody]` |
|---|---|---|
| 資料位置 | URL 後面 `?email=xxx` | Request 的 HTTP Body（JSON） |
| 可見性 | URL 會被瀏覽器歷史、Server log、Proxy log 記錄 | Body 不出現在 URL |
| 適合場景 | 查詢、篩選（公開的搜尋條件） | 新增、登入（有敏感資料） |

密碼如果放 URL，就等於「明文密碼印在每一份 log 檔案上」。這是不可接受的安全性問題，和加密傳輸（HTTPS）無關——HTTPS 保護的是傳輸中的 Body，但 URL 的 query string 在 Server 端是可見的。

判斷原則：**這個資料如果出現在 log 裡，會造成安全問題嗎？** 會 → `[FromBody]`；不會 → 可以 `[FromQuery]`。

**我學到的原則**

HTTP 的設計本意是：URL 是公開可快取、可收藏的資源識別符；Body 是不應被快取的操作內容。遵循這個設計語意，敏感資料（密碼、個資）放 Body，查詢條件（篩選、分頁）放 Query。

---

### 條目 179 — Identity 錯誤訊息的英文不是 bug，是邊界設計的機會

**我做了什麼**

實作 `translateIdentityError()` 函式，把 ASP.NET Core Identity 的英文錯誤訊息翻譯成繁體中文，並放在 `LoginView.vue` 元件內部（而非抽到共用層）。

**我遇到的問題**

一開始想把翻譯函式放到 `src/utils/` 資料夾，讓它「更通用」。但想了一下，整個前端除了 `LoginView.vue` 之外，沒有其他地方需要解析 Identity 錯誤訊息。

**我怎麼想通的**

判斷抽出到共用層的標準是：**「現在有幾個使用者？」**

- 一個使用者 → 放在使用它的地方（`LoginView.vue` 的 `<script setup>` 裡）
- 多個使用者 → 才值得抽到 `src/utils/`

把只有一個使用者的邏輯放到 `src/utils/`，看起來很整齊，但實際上是在為「也許未來會有第二個使用者」做過早的抽象。YAGNI（You Aren't Gonna Need It）原則：「未來可能需要」不是搬移的理由，「現在確實有第二個使用者」才是。

這個判斷和後端 `BuildPricesCacheKey` 放在 `private static` 而非搬到 Core 層是同一個思路，只是一個在後端 C#，一個在前端 TypeScript。

**我學到的原則**

抽象化是工具，不是目的。每一次說「我要把這個抽出來讓它更通用」之前，問：「現在有具體的第二個使用者嗎？」有 → 抽；沒有 → 等到出現了再抽，代價不高，收益卻是真實的。

---

### 條目 180 — DbContext 歸屬的判斷依據：問「消費者是誰」

**我做了什麼**

在設計 UserFarmProfiles 的資料庫歸屬時，需要選擇放進哪個 DbContext：ApplicationDbContext（Identity）、CoreDbContext（跨模組基礎設施）、或新建 UserDbContext。

**我遇到的問題**

直覺上覺得 UserFarmProfiles 和 Identity 有關（都跟使用者有關），所以差點放進 ApplicationDbContext。另一個選項 CoreDbContext 也感覺合理，因為「Core 是共用的」。

**我怎麼想通的**

問了一個問題：「這份資料的消費者是誰？」

| 資料 | 消費者 | 歸屬 |
|------|--------|------|
| SyncStates | 所有 SyncWorker 都需要 | CoreDbContext |
| NavModules | 整個導覽系統 | CoreDbContext |
| RoleModulePermissions | 所有模組的存取控制 | CoreDbContext |
| UserFarmProfiles | 只有使用者農場設定功能 | UserDbContext |

ApplicationDbContext 的職責是管 Identity 六張表，不是業務資料的容器。CoreDbContext 是「消費者是所有模組」的東西的家，不是「只有特定業務用到」的東西的家。UserFarmProfiles 的消費者只有一個業務領域，所以應該新建 UserDbContext。

**我學到的原則**

判斷資料應該放在哪個 DbContext，先問「消費者是誰」。消費者是所有模組 → Core；消費者是特定業務 → 那個業務的模組；消費者是 Identity 本身 → ApplicationDbContext。不是看「感覺上屬於誰」，是看「誰真的需要這份資料」。

---

### 條目 181 — UserId 當 PK 與 int Id 當 PK 的本質差異

**我做了什麼**

設計 UserFarmProfile 的主鍵時，需要在「UserId（string）當 PK」和「int Id 當 PK + UserId 加 Unique Index」之間做選擇。

**我遇到的問題**

一開始覺得 int 自增 PK 是「標準做法」，不確定為什麼這裡要用 string PK。

**我怎麼想通的**

關鍵不是技術問題，是業務問題：「一個使用者應該有幾份農場偏好設定？」

| PK 設計 | 業務語意 | API 設計影響 |
|---------|---------|------------|
| int Id | 一個使用者可以有多份設定 | `GET /api/profile/farms/{id}` 需要帶 id |
| UserId | 一個使用者只能有一份設定 | `GET /api/profile/farm` 不需要任何參數 |

農場偏好設定是「個人化的閱讀偏好」，語意上一個人只有一份。選 UserId 當 PK，資料庫層面就強制了這個業務規則，不需要另加 Unique Index，也讓 API 設計更乾淨。

**我學到的原則**

PK 的選擇是業務決策，不是技術決策。選 UserId 當 PK 是在說「這是一對一的關係，資料庫保證它」；選 int Id 是在說「可以有多份」。這個選擇決定了後續 API 的形狀、前端的邏輯、以及整個功能的使用者心智模型。

---

### 條目 182 — HasPrincipalKey：EF Core 的關聯推導邏輯與 shadow property

**我做了什麼**

Migration 跑完後，第一次呼叫 `GET /api/profile/farm` 回傳 500，錯誤訊息是：

```
Invalid column name 'UserFarmProfileUserId'
```

資料庫裡根本沒有這個欄位，但 EF Core 產生的 SQL 在找它。

**我遇到的問題**

UserFarmCrop 有 `UserId` 欄位當 FK，也有 `UserFarmProfile` 導覽屬性。UserFarmProfile 有 `Crops` 集合。DbContext 已經設定了 `HasOne/WithMany/HasForeignKey`，但還是出錯。

**我怎麼想通的**

EF Core 命名 shadow property 的規則是：「導覽屬性名稱 + 主表 PK 屬性名稱」。

`UserFarmCrop.UserFarmProfile`（導覽屬性名稱）+ `UserFarmProfile.UserId`（主表 PK 名稱）
→ `UserFarmProfileUserId`（EF Core 自己推導出的欄位名）

問題在於：`WithMany()` 沒有指定集合（應該是 `WithMany(p => p.Crops)`），EF Core 不知道該用哪個集合對應，所以建立了 shadow property。加上 `HasPrincipalKey(p => p.UserId)` 明確告知主表端的 Key 是 `UserId`，而不是 EF Core 假設的 `Id`。

```csharp
entity.HasOne(c => c.UserFarmProfile)
      .WithMany(p => p.Crops)          // 明確指定集合
      .HasForeignKey(c => c.UserId)    // FK 欄位
      .HasPrincipalKey(p => p.UserId)  // 主表 Key（必要！因為不是 int Id）
      .OnDelete(DeleteBehavior.Cascade);
```

**我學到的原則**

EF Core 的關聯設定有三個要素：FK 是哪個欄位（HasForeignKey）、主表 Key 是哪個欄位（HasPrincipalKey）、兩端的導覽屬性是什麼（HasOne/WithMany 的 lambda）。當任何一個要素沒有明確指定，EF Core 就會用命名慣例去猜，猜錯了就產生 shadow property。主表 PK 是非常規型別（string）時，HasPrincipalKey 是必要的。

---

### 條目 183 — Upsert 模式：為什麼一個使用者的設定只需要一支 PUT

**我做了什麼**

設計 ProfileController 時，需要決定要不要分開實作 `POST /api/profile/farm`（新增）和 `PUT /api/profile/farm`（更新）。

**我遇到的問題**

REST 語意上，POST 是新增、PUT 是更新，但這個場景下分開實作感覺有點奇怪——前端怎麼知道使用者「是第一次設定」還是「要更新已有的設定」？

**我怎麼想通的**

因為 UserId 是 PK，同一個 UserId 永遠只會有一筆資料。「新增」和「更新」對前端來說都是同一個動作：「我要把這份設定存起來」。

前端不應該需要知道「資料庫裡現在有沒有這筆資料」，那是後端的細節。後端用 Upsert 語意（先查、再決定 INSERT 還是 UPDATE）統一處理。

HTTP 語意上：PUT 的定義是「把指定資源覆蓋成這個狀態」，資源的識別是 UserId（從 JWT 取，不是 URL 參數），這完全符合 PUT 的語意。POST 的定義是「建立新資源，伺服器決定 ID」，但這裡 ID 是已知的（UserId），不適合 PUT。

**我學到的原則**

Upsert 適合的場景：資源的識別已知（不需要伺服器分配 ID），且業務規則是「一個識別只有一筆資料」。這種場景下，分開 POST + PUT 是讓前端承擔了不該承擔的狀態判斷職責。

---

### 條目 184 — GET 回傳 200+null vs 404：HTTP 狀態碼的語意精確性

**我做了什麼**

設計 `GET /api/profile/farm` 的回傳，當使用者還沒有設定過農場資料時，需要決定回傳 200+null 還是 404。

**我遇到的問題**

一開始直覺是「找不到資料就回 404」，這是常見的 REST API 做法。

**我怎麼想通的**

404 的語意是「你要找的資源不存在，這是錯誤狀態」。但「使用者還沒有設定農場偏好」不是錯誤，是正常的初始狀態。

| 狀態碼 | 語意 | 前端反應 |
|--------|------|---------|
| 404 | 「應該存在但找不到」→ 錯誤 | 顯示錯誤訊息 |
| 200 + null | 「查詢了，結果是空的」→ 正常 | 顯示空白表單讓使用者填寫 |

設計 API 時，要區分「業務上的空值」和「技術上的錯誤」。前者用 200+null，後者才用 4xx 或 5xx。

**我學到的原則**

HTTP 狀態碼傳遞的是「這個請求在業務語意上成功了嗎」，不只是「有沒有找到資料」。「沒有設定過」是業務上的合法狀態，不是技術錯誤，應該回 200。有了這個原則，任何「空」都需要先判斷：是「不應該空，空了代表出錯」還是「可以空，空是一個合法值」。

---

### 條目 185 — 前端 store 邊界：為什麼作物清單不共用 marketStore

**我做了什麼**

ProfileView 需要顯示作物 Autocomplete 下拉，作物資料來自 `marketApi.getCrops()`，需要決定是透過 `marketStore` 取還是直接呼叫 API。

**我遇到的問題**

`marketStore` 已經有 `fetchCrops()` 方法，感覺可以直接重用。但 `marketStore.crops` 只有當前 `marketType` 的作物（Veg 或 Fruit 或 Flower，三選一），ProfileView 需要全部三種合併的結果。

**我怎麼想通的**

`marketStore.crops` 的語意是「使用者在行情頁目前選擇的類型對應的作物清單」，它是有狀態的（隨使用者在行情頁切換類別而改變）。ProfileView 需要的是「一個靜態的、完整的作物搜尋池」，兩個需求的語意完全不同。

如果透過 marketStore 取，需要把 marketStore 改成存三份清單（vegCrops、fruitCrops、flowerCrops），這會讓 marketStore 同時服務兩種語意不同的需求，職責變模糊。

正確做法是 ProfileView 自己在 onMounted 打三次 API，在本地狀態合併，不影響 marketStore 的設計。

```typescript
const [veg, fruit, flower] = await Promise.all([
  marketApi.getCrops('Veg'),
  marketApi.getCrops('Fruit'),
  marketApi.getCrops('Flower'),
])
allCrops.value = [...veg, ...fruit, ...flower]
```

**我學到的原則**

重用 store 的判斷標準是：「這兩個地方的需求語意相同嗎？」語意相同才重用，語意不同就各自管自己的資料。看起來是「同一份資料」，但如果用途不同（一個是有狀態的篩選器、一個是靜態搜尋池），就不應該強行共用。共用帶來的是隱性耦合，讓兩個不相關的功能互相影響。

---

### 條目 186 — onBlur 延遲關閉下拉：事件順序與 UI 競態問題

**我做了什麼**

實作 Autocomplete 下拉時，點選下拉選項後發現作物沒有被加入，debug 後發現 blur 事件比 click 先觸發，導致下拉在 click 執行前就消失了。

**我遇到的問題**

`@blur` 關閉下拉 → `@click` 選取作物，但實際執行順序是：blur 發生 → 下拉消失 → click 的目標（下拉選項的 DOM）不見了 → click 無法觸發。

**我怎麼想通的**

瀏覽器的事件觸發順序：`mousedown` → `blur` → `mouseup` → `click`。

解法是把 `@click` 換成 `@mousedown`（在 blur 之前觸發），或在 `@blur` 加延遲讓 click 先跑完：

```typescript
function onBlur() {
  setTimeout(() => {
    showDropdown.value = false
  }, 150) // 延遲 150ms，讓 mousedown/click 先觸發
}
```

搭配下拉選項用 `@mousedown` 而非 `@click`，確保在 blur 觸發之前就選中了目標。

**我學到的原則**

任何「點選某個東西同時會觸發 blur」的 UI 模式都有這個競態問題。瀏覽器事件順序：mousedown → blur → mouseup → click。解法是改用 mousedown（比 blur 早），或 onBlur 加延遲。這是 Dropdown/Select/Combobox 元件的常見實作細節。

---

### 條目 187 — .gitignore glob pattern 的意外陷阱

**我做了什麼**

建立 TaiwanAgri.Modules.User 後執行 `git add TaiwanAgri.Modules.User/`，Git 說這個路徑被 .gitignore 忽略。

**我遇到的問題**

.gitignore 裡有 `*.user`，原意是忽略 Visual Studio 的 `*.csproj.user` 使用者設定檔，但 `*.user` 這個 glob pattern 也會匹配「名稱以 `.User` 結尾的資料夾」，導致整個 `TaiwanAgri.Modules.User/` 被忽略。

**我怎麼想通的**

`*` 在 glob 裡可以匹配任何字元，包括大小寫。`*.user` 匹配「任何以 `.user` 或 `.User` 結尾的名稱」，資料夾名稱也在匹配範圍內。

正確做法是改為 `*.csproj.user`，精確描述要忽略的副檔名：

```gitignore
# 改前（會誤匹配資料夾名稱）
*.user

# 改後（精確描述 VS 使用者設定檔）
*.csproj.user
```

**我學到的原則**

.gitignore 的 glob pattern 同時匹配檔案和資料夾名稱。當 pattern 過於寬泛（如 `*.user`），可能意外忽略與 pattern 名稱相符的資料夾。規則：pattern 應該精確描述「你真正想忽略的東西」，不要用過於寬泛的模式，避免意外副作用。

---

### 條目 188 — EF Core HasOne<T>() 泛型寫法：無導覽屬性的關聯設定

**我做了什麼**

UserWatchlist Entity 沒有加導覽屬性（沒有 `public UserFarmProfile UserFarmProfile { get; set; }`），但在 UserDbContext 的 Fluent API 裡仍然需要設定它和 UserFarmProfile 的外鍵關聯。

**我遇到的問題**

用 `entity.HasOne(c => c.UserId)` 寫法報錯——`HasOne` 的 lambda 裡應該放**導覽屬性**，不是欄位值。但 UserWatchlist 根本沒有導覽屬性，lambda 裡放不了任何屬性。

**我怎麼想通的**

EF Core 提供了兩種 HasOne 寫法，對應兩種場景：

```csharp
// 有導覽屬性時：lambda 裡放屬性名稱
entity.HasOne(c => c.UserFarmProfile)
      .WithMany(p => p.Crops)
      ...

// 沒有導覽屬性時：用泛型指定關聯的 Entity 型別
entity.HasOne<UserFarmProfile>()
      .WithMany()               // 主表也沒有對應集合屬性，留空
      .HasForeignKey(c => c.UserId)
      .HasPrincipalKey(p => p.UserId)
      .OnDelete(DeleteBehavior.Cascade);
```

`HasOne<UserFarmProfile>()` 的意思是「這個 Entity 和 UserFarmProfile 有 Has-One 關係，但我不需要在程式碼裡透過導覽屬性存取它」。

**我學到的原則**

導覽屬性是 EF Core 在物件層面表達關聯的方式，但它不是必須的。導覽屬性是否加入，應該由「查詢時需不需要從這個 Entity 導航到另一個 Entity」來決定，不是因為「有 FK 就一定要加」。

沒有導覽屬性讓 Entity 更輕，不用維護雙向關聯的一致性。這是有意識的設計決策，不是遺漏。

---

### 條目 189 — Service 層 userId 參數的雙重理由：架構 + 安全

**我做了什麼**

設計 IUserWatchlistService 時，把 userId 加進新增和刪除方法的參數清單裡：

```csharp
Task<bool> AddWatchlistItemAsync(string userId, AddWatchlistRequestDto request);
Task RemoveWatchlistItemsAsync(string userId, IEnumerable<int> ids);
```

**我一開始的疑問**

「JWT token 裡不是已經有 userId 了嗎？為什麼還要傳進來？」

**我怎麼想通的**

這個問題實際上有兩個獨立的答案：

**架構面**：JWT token 在 HTTP Request Header 裡，只有 Controller 層能讀取（透過 `User.FindFirstValue(ClaimTypes.NameIdentifier)`）。Service 層的職責是業務邏輯，它不知道也不應該知道 HTTP Context 的存在。所以 userId 必須從 Controller 傳進來。

**安全面**：刪除時用 `WHERE UserId == userId AND Id IN (ids)` 兩個條件同時過濾，確保使用者只能刪自己的資料。如果只有 `Id IN (ids)`，惡意使用者猜到別人的 Watchlist Id 後就能直接刪除他人資料。

兩個理由互相獨立，各自成立。好的架構邊界設計往往也順帶帶來安全性。

**我學到的原則**

「這個參數到底有沒有必要」這個問題，要從「誰有責任提供這份資訊」和「這份資訊在哪一層才能被可信地取得」兩個角度回答，不是單純看「資料在哪裡已經有了」。

---

### 條目 190 — AnyAsync vs Distinct：去重的時機和語意完全不同

**我做了什麼**

新增 Watchlist 項目前，需要防止使用者重複監看同一個作物+市場組合。

**我的第一個想法**

「去重用 Distinct？」

**我怎麼想通的**

`Distinct` 和 `AnyAsync` 解決的是完全不同的問題：

- **Distinct**：「我已經查出了一堆資料，把重複的過濾掉再回傳」——重複的資料已經在 DB 裡了
- **AnyAsync**：「我要寫入之前，先確認這筆資料是否已存在」——攔截在 SaveChanges 之前

去重的正確時機是「存入前先確認」，而不是「存入後查詢時過濾」：

```csharp
var exists = await context.UserWatchlists
    .AnyAsync(w => w.UserId == userId
                && w.CropCode == request.CropCode
                && w.MarketCode == request.MarketCode);

if (exists) return false;
// 確認不重複後才 Add + SaveChanges
```

三個條件都要對上才算重複：同一個使用者、同一個作物代碼、同一個市場代碼（包括 null = 全台）。

**我學到的原則**

遇到「去重」這個詞，先問「我要防止的是什麼」：防止髒資料進 DB（寫入前），還是顯示時過濾（查詢後）。兩種情境對應完全不同的技術手段。

---

### 條目 191 — Service 回傳 bool 讓 Controller 決定 HTTP 狀態碼：職責分離

**我做了什麼**

AddWatchlistItemAsync 在資料已存在時，設計成回傳 `false` 而不是拋例外。Controller 收到 `false` 後回傳 409 Conflict。

**為什麼不直接在 Service 層拋例外**

Service 層的職責是業務邏輯——「這筆資料是否已存在」是業務判斷，結果是「是/否」。把它翻譯成 HTTP 狀態碼（409、200、201）是 Controller 的職責。

如果 Service 直接拋 `ConflictException`，就等於 Service 層開始依賴 HTTP 語意，邊界開始模糊。

```csharp
// Service：回傳業務結果
public async Task<bool> AddWatchlistItemAsync(...) {
    if (exists) return false;  // 業務判斷：已存在
    // ...
    return true;               // 業務判斷：新增成功
}

// Controller：翻譯成 HTTP 語意
var success = await userWatchlistService.AddWatchlistItemAsync(userId, request);
if (!success) return Conflict("此作物與市場組合已在監看清單中");
return NoContent();
```

**我學到的原則**

Service 說「發生了什麼」（業務語意），Controller 說「對 HTTP 客戶端這意味著什麼」（HTTP 語意）。兩者語意不同，不應該混在一起。回傳 bool 是最輕量的業務結果表達方式，不帶任何 HTTP 假設。

---

### 條目 192 — DELETE 的 [FromQuery] 與 axios URLSearchParams：陣列參數的跨層傳遞

**我做了什麼**

刪除多筆 Watchlist 項目，後端用 `[FromQuery] IEnumerable<int> ids`，前端用 axios 的 `delete` 方法傳陣列。

**我遇到的問題**

刪除功能一直無法正常運作。後來發現 axios 預設把陣列 `ids=[1,2,3]` 展開成 `ids[0]=1&ids[1]=2&ids[2]=3`，但 ASP.NET Core 的 `[FromQuery]` 期望的格式是 `ids=1&ids=2&ids=3`（重複的同名參數）。

**解法**

用 `URLSearchParams` 手動控制展開格式：

```typescript
removeItems(ids: number[]): Promise<void> {
  const params = new URLSearchParams()
  ids.forEach(id => params.append('ids', String(id)))
  return authClient.delete('/api/watchlist', { params }).then(() => undefined)
}
```

`params.append('ids', '1')` 反覆呼叫同一個 key，就會產生 `ids=1&ids=2&ids=3` 的格式，對應後端的 `IEnumerable<int>`。

**我學到的原則**

前後端傳遞陣列參數時，Query String 的「同名多值」格式因框架而異，不能假設兩端預設行為一致。遇到陣列參數傳遞問題，先確認前端送出的實際 Query String 格式，和後端期望的格式是否吻合。

同理，之前在市場頁面的 cropCodes 陣列也有相同問題，解法也是 URLSearchParams。

---

### 條目 193 — Vue Router v4 beforeEach：return 取代 next()

**我做了什麼**

實作路由守衛 `beforeEach` 時，先用了 `next()` callback 寫法，執行後 console 出現：

```
[Vue Router warn]: The next() callback in navigation guards is deprecated. Return the value instead of calling next(value).
```

**我怎麼修正的**

Vue Router v4 新的寫法是直接 return，不再需要 `next` 參數：

```typescript
// ❌ 舊寫法（v3 相容，v4 deprecated）
router.beforeEach((to, _from, next) => {
  if (to.meta.requiresAuth && !isAuthenticated) {
    next({ name: 'login', query: { redirect: to.fullPath } })
  } else if (to.name === 'login' && isAuthenticated) {
    next({ name: 'home' })
  } else {
    next()
  }
})

// ✅ 新寫法（v4 原生）
router.beforeEach((to, _from) => {
  if (to.meta.requiresAuth && !isAuthenticated) {
    return { name: 'login', query: { redirect: to.fullPath } }
  }
  if (to.name === 'login' && isAuthenticated) {
    return { name: 'home' }
  }
  return true
})
```

return 值的語意：
- return 路由物件 → 導向該路由
- return true → 放行
- return false → 取消導航

**我學到的原則**

return 寫法去掉了 `next` 這個 callback 參數後，邏輯更直觀：每個 `if` 分支直接說「我的結果是什麼」，不需要在函式末尾記得呼叫 `next()`。早期的 `next()` 寫法容易因為忘記呼叫或重複呼叫而出現 bug，return 寫法完全規避了這個問題。

---

### 條目 194 — 路由守衛 redirect query：讓「被踢到登入頁」的使用者登入後回到原目標

**我做了什麼**

路由守衛在使用者未登入時把目標路徑存進 query，讓登入頁能在登入成功後跳回原目標：

```typescript
// router/index.ts：守衛把目標路徑存進 query
return { name: 'login', query: { redirect: to.fullPath } }
// URL 變成：/login?redirect=%2Fwatchlist

// LoginView.vue：登入成功後讀取並跳轉
const route = useRoute()
const redirect = (route.query.redirect as string) || '/'
router.push(redirect)
```

**為什麼用 to.fullPath 而非 to.path**

`to.path` 只有路徑（`/watchlist`），`to.fullPath` 包含完整路徑加 query string（`/watchlist?filter=xxx`）。保留完整的 fullPath 讓使用者跳轉後不會遺失原本帶的查詢條件。

**為什麼 useRoute 要在元件內呼叫**

`useRoute()` 是 Vue Composition API，必須在 `<script setup>` 或 `setup()` 函式內呼叫，不能在模組頂層。這和 `useAuthStore()` 必須在函式內呼叫的理由相同——它們依賴 Vue 的響應式系統，需要有活躍的 Vue instance。

**我學到的原則**

「Return URL」（登入後跳回原目標）是標準的 UX 模式，實作方式是把目標路徑存在 URL query string 裡，讓資訊跟著導航一起傳遞，不需要額外的狀態管理。

---

### 條目 195 — 前端錯誤狀態的清除時機：重新選擇 vs 重新送出

**我做了什麼**

在 WatchlistView 裡，使用者選了作物 A → 新增 → 409（已存在）→ 再選一次作物 A（或換選作物 B）→ 下方還殘留著上次的「已存在」錯誤訊息。

**我的判斷**

清除 errorMessage 的正確時機是「使用者重新做選擇」，不是「使用者按送出」：

```typescript
// 重新選作物時清除
function selectCrop(crop: CropResponseDto) {
  selectedCrop.value = crop
  cropSearchText.value = ''
  showCropDropdown.value = false
  store.errorMessage = null  // 重新選擇 = 舊錯誤不再適用
}

// 清掉作物時也清除
function clearCrop() {
  selectedCrop.value = null
  cropSearchText.value = ''
  store.errorMessage = null
}
```

如果在 `handleAdd`（送出時）清除，會有一個問題：送出 → 清除錯誤 → API 回傳 409 → Store 重新設定錯誤。這樣沒問題，但如果使用者不送出，只是重新選了一個作物，舊的錯誤訊息會繼續殘留，顯示出一條「對當前選擇不適用」的訊息，造成混淆。

**我學到的原則**

錯誤訊息的語意是「你上一個操作的結果」。當使用者換了「操作的對象」（選了不同的作物），上一次的錯誤就不再描述當前狀態，應該清除。「使用者改變了選擇」是「狀態已過時」的信號。

---

### 條目 196 — 成功才重置表單：保留失敗時的使用者輸入

**我做了什麼**

新增 Watchlist 項目後，表單重置的邏輯：

```typescript
await store.addItem({ ... })
// 用 errorMessage 判斷是否成功
if (!store.errorMessage) {
  selectedCrop.value = null
  selectedMarketCode.value = null
}
```

**為什麼不直接在 addItem 後面無條件重置**

如果新增失敗（409 重複），使用者會看到：
1. 表單清空了
2. 同時下方出現「此作物已存在」的錯誤訊息

這兩件事同時發生會讓使用者困惑——表單清空了，但我看到了一個錯誤，我是應該重新填一遍還是怎樣？

保留表單讓使用者能清楚看到「剛才選的是什麼」加上「為什麼不能新增」，下一步要換選不同作物或確認自己已經有在監看了，判斷成本低很多。

**我學到的原則**

表單重置不是「操作完成的清理動作」，而是「成功完成後的 UX 反饋」。失敗不是完成，所以不應該觸發重置。判斷依據：「如果重置了，使用者的下一步是什麼？重新填回同樣的資料？這是在幫他還是在添麻煩？」

---

### 條目 197 — 勾選狀態不進 Store：UI 狀態 vs 應用狀態的分界

**我做了什麼**

WatchlistView 裡的 checkbox 勾選狀態用 View 層的 `ref<number[]>` 管理，而非放進 Pinia Store：

```typescript
// ← 直接在 View 內宣告
const selectedIds = ref<number[]>([])

function toggleSelect(id: number) {
  const idx = selectedIds.value.indexOf(id)
  if (idx >= 0) selectedIds.value.splice(idx, 1)
  else selectedIds.value.push(id)
}
```

**判斷依據**

問兩個問題：
1. 這個狀態需要被其他元件讀取嗎？→ 不，勾選狀態只在這個 View 裡有意義
2. 這個狀態需要跨頁面保留嗎？→ 不，離開監看清單頁後勾選就沒意義了

兩個問題都是「否」，就應該留在 View 層，不進 Store。

Store 管理是有成本的：需要定義 state、action，所有使用它的地方都耦合到這個 Store。如果一個狀態不需要跨層共享，這個成本完全是浪費。

**我學到的原則**

判斷狀態應不應該進 Store，不是看「這個狀態重不重要」，而是看「這個狀態有沒有跨元件/跨頁面的生命週期需求」。有 → Store；沒有 → View 層 ref。Pinia Store 是通訊工具，不是「把所有狀態都集中管理」的筒倉。

---
 
### 條目 198 — Docker 網路的 localhost 陷阱：為什麼 hostname hardcode 是部署炸彈
 
**我做了什麼**
 
`PriceUpdatedConsumer.cs` 和 `AgriProductsTransSyncWorker.cs` 裡的 `ConnectionFactory` 原本寫死 `HostName = "localhost"`。我把它改成從 `IConfiguration` 讀取，並在 `appsettings.json` 加入 `RabbitMQ:HostName` 區段，在 `docker-compose.yml` 的 web / worker 服務用環境變數覆蓋成 `rabbitmq`。
 
**為什麼 localhost 在 Docker 裡是錯的**
 
本機開發時，所有服務（SQL Server、Redis、RabbitMQ）都跑在同一台電腦上，`localhost` 是「這台電腦」，所以能連到任何東西。
 
Docker Compose 啟動後，每個服務跑在獨立容器裡。容器有自己的網路空間，`localhost` 在容器內指的是「這個容器自己」，不是宿主機，也不是其他容器。RabbitMQ 跑在另一個叫 `rabbitmq` 的容器，Docker Compose 會自動為 service name 建立 DNS，所以 web 容器要連 RabbitMQ，正確的 hostname 是 `rabbitmq`。
 
這種問題的特徵是：本機測試永遠是好的，部署才炸，而且炸的訊息是「RabbitMQ 連線失敗」，看不出是 hostname 寫錯了。
 
**正確的解法：appsettings + 環境變數覆蓋**
 
```csharp
var factory = new ConnectionFactory
{
    HostName = _configuration["RabbitMQ:HostName"] ?? "localhost"
};
```
 
```json
// appsettings.json（本機開發預設值）
"RabbitMQ": { "HostName": "localhost" }
```
 
```yaml
# docker-compose.yml（Docker 環境覆蓋）
environment:
  - RabbitMQ__HostName=rabbitmq
```
 
.NET 設定系統的優先順序：環境變數 > appsettings.json。`__` 雙底線對應設定 key 裡的 `:` 分隔符，所以 `RabbitMQ__HostName` 覆蓋的是 `RabbitMQ:HostName`。兩個環境各自讀到正確的值，不需要維護兩份 appsettings。
 
**我學到的原則**
 
「本機正常、部署才炸」是一類特別難追的 bug，因為開發期間完全看不到症狀。所有和環境相關的值（hostname、port、連線字串、feature flag）都應該外化到設定檔，程式碼裡只保留讀取邏輯。
 
---
 
### 條目 199 — ?? null 合併運算子：「先抓設定，讀不到才用預設值」
 
**我做了什麼**
 
把 `_configuration["RabbitMQ:HostName"]` 的讀取改成：
 
```csharp
HostName = _configuration["RabbitMQ:HostName"] ?? "localhost"
```
 
**?? 是什麼**
 
`??` 叫做 null 合併運算子（null-coalescing operator）。語意是：左邊如果是 null，就用右邊的值。
 
```csharp
var result = 可能是null的值 ?? 預設值;
```
 
這裡的流程是：
1. 去 appsettings.json（或環境變數）找 `RabbitMQ:HostName`
2. 找到了 → 用那個值
3. 找不到（key 不存在或值是 null）→ 用 `"localhost"`
本機開發時 appsettings.json 有 `"HostName": "localhost"` → 讀到 `"localhost"`。Docker 環境有環境變數 `RabbitMQ__HostName=rabbitmq` → 讀到 `"rabbitmq"`。
 
`"localhost"` 的 `??` 後面是最後的安全網，在兩個環境都有設定的前提下實際上不會走到它。
 
**和 ! 的差別**
 
`!` 是告訴編譯器「我保證這不是 null，不要警告我」。如果真的是 null，執行期才炸。`??` 是「如果是 null，就做這個」，是真正的防禦，不是保證。
 
---
 
### 條目 200 — Fail-Fast：讓問題在最早的時間點暴露
 
**我做了什麼**
 
`AuthService.GenerateJwtToken` 原本：
 
```csharp
var secretKey = _configuration["Jwt:SecretKey"]!;
var expiresInDays = int.Parse(_configuration["Jwt:ExpiresInDays"]!);
```
 
改為在建構子加 Fail-Fast 驗證：
 
```csharp
public AuthService(...)
{
    _ = configuration["Jwt:SecretKey"]
        ?? throw new InvalidOperationException("Jwt:SecretKey 未設定");
    _ = configuration["Jwt:ExpiresInDays"]
        ?? throw new InvalidOperationException("Jwt:ExpiresInDays 未設定");
}
```
 
並在 `GenerateJwtToken` 裡改用 `int.TryParse`：
 
```csharp
var expiresInDaysStr = _configuration["Jwt:ExpiresInDays"]
    ?? throw new InvalidOperationException("Jwt:ExpiresInDays 未設定");
 
if (!int.TryParse(expiresInDaysStr, out var expiresInDays))
    throw new InvalidOperationException("Jwt:ExpiresInDays 必須是整數");
```
 
**! 的問題**
 
`!` 只是跟編譯器的承諾，執行期沒有保護。`_configuration["Jwt:SecretKey"]` 在 key 不存在時回傳 null，加了 `!` 之後 null 繼續往下傳，在 `Encoding.UTF8.GetBytes(null)` 那行才炸，錯誤訊息是一堆 stack trace，完全看不出是 appsettings 少了一個 key。
 
**Fail-Fast 的核心價值**
 
不只是「錯誤訊息更清楚」。更重要的是**時機**。
 
- 原本：有人登入的瞬間才炸。服務可能已經跑了幾個小時，甚至被當成健康的服務對外服務。
- Fail-Fast：應用程式啟動時 DI 容器建立 AuthService 就炸。立刻知道設定有問題，不會讓壞掉的服務默默跑著。
一個啟動就炸的服務比一個平時正常偶爾炸的服務更容易診斷。
 
**`int.Parse` vs `int.TryParse`**
 
`int.Parse(null)` → `ArgumentNullException`，訊息看不出是哪個設定的問題。
`int.Parse("abc")` → `FormatException`，同樣看不出根本原因。
 
`int.TryParse(str, out var n)` 回傳 `bool`，失敗時不拋例外，`out` 參數是 0。自己決定失敗時做什麼：
 
```csharp
if (!int.TryParse(expiresInDaysStr, out var expiresInDays))
    throw new InvalidOperationException("Jwt:ExpiresInDays 必須是整數");
```
 
這樣的例外訊息精確指出是哪個設定、錯在哪裡。
 
---
 
### 條目 201 — JWT 的 Issuer vs Audience：演唱會票上印的兩件事
 
**我做了什麼**
 
`AuthService` 裡 `audience` 原本錯用 `Jwt:Issuer` 的值：
 
```csharp
audience: _configuration["Jwt:Issuer"],   // 兩個都一樣，語意錯誤
```
 
改為獨立讀取 `Jwt:Audience`，並在 appsettings.json 加入：
 
```json
"Audience": "TaiwanAgriPlatform-Frontend"
```
 
**Issuer 和 Audience 是什麼**
 
JWT token 就像演唱會票。票上印了兩個資訊：
 
- **Issuer（發行者）**：這張票是誰印的？→ 票務公司（KKTIX）→ 你的後端（`TaiwanAgriPlatform`）
- **Audience（受眾）**：這張票能進哪個場地？→ 台北小巨蛋 → 前端（`TaiwanAgriPlatform-Frontend`）
**驗證的流程**
 
```csharp
// IdentityExtensions.cs（TokenValidationParameters）
ValidIssuer = configuration["Jwt:Issuer"],
ValidAudience = configuration["Jwt:Audience"],
```
 
前端每次請求帶著 JWT，.NET middleware 解開 token，讀出裡面的 `iss` 和 `aud` 欄位，跟 `ValidIssuer` / `ValidAudience` 比對。不符合 → 401 Unauthorized。
 
**值本身填什麼不重要，重要的是兩邊一致**
 
發 token 時（AuthService）寫 `issuer = "TaiwanAgriPlatform"`，驗證時（IdentityExtensions）設 `ValidIssuer = "TaiwanAgriPlatform"`，兩邊一致就過。填 123 兩邊都填 123 也能跑，但語意毫無意義。
 
**為什麼要分開**
 
現在的系統只有一個前端一個後端，`Issuer == Audience` 技術上完全可行。但拆開的語意價值在未來：若加入管理後台（`audience = "TaiwanAgriPlatform-Admin"`），用戶的 token `aud` 是 Frontend，拿去打管理後台，`audience` 不符合，直接 401 擋掉。不用改任何程式碼，只是設定的差異就能在兩個 audience 之間做出隔離。
 
---
 
### 條目 202 — const string 的正確位置與抽取判斷標準
 
**我做了什麼**
 
`MarketController` 裡 `"marketType 必須為 Veg、Fruit 或 Flower"` 在 `GetMarkets` 和 `GetCrops` 各出現一次，抽成：
 
```csharp
public class MarketController : ControllerBase
{
    private const string InvalidMarketTypeMessage = "marketType 必須為 Veg、Fruit 或 Flower";
 
    private readonly IMarketService _marketService;
    // ...
}
```
 
**為什麼放在 class 頂部**
 
C# 慣例是把常數宣告放在 class 內最上面的欄位宣告區，在建構子之前。這樣打開檔案很快就能看到這個 class 定義了哪些常數，不需要捲到處找。
 
**什麼情況下值得抽成 const**
 
判斷標準：同一條業務規則在同一個 class 裡重複出現。
 
`"marketType 必須為 Veg、Fruit 或 Flower"` 值得抽，因為它是同一條業務規則被寫了兩遍——改訊息要改兩個地方，很容易漏掉一個。
 
日期格式錯誤字串（`"開始日期 格式錯誤，請使用 yyyy-MM-dd"`、`"結束日期 格式錯誤..."`）不抽，因為：
1. 語意獨立：「開始日期」和「結束日期」是兩個不同的錯誤情境，抽成同一個 const 反而讓人以為是同一件事
2. 改動機率極低：`yyyy-MM-dd` 是 ISO 標準，幾乎不會變
3. 兩個 const 換掉幾行字串，閱讀者還要往上跳看定義，閱讀成本高於維護收益
**核心原則**：抽 const 不是「字串看起來很像」就抽，是「改了一個地方，另一個地方理所當然也要改」才抽。
 
---
 
### 條目 203 — 跨模組耦合：不是現在會炸，是未來改東西不知道影響範圍
 
**我做了什麼**
 
`ProfileView.vue` 原本直接 import Market 模組的 `marketApi`：
 
```typescript
import { marketApi } from '../api/market'
// onMounted 裡
const [veg, fruit, flower] = await Promise.all([
  marketApi.getCrops('Veg'),
  marketApi.getCrops('Fruit'),
  marketApi.getCrops('Flower'),
])
```
 
新增 `cropApi.ts` 封裝這三次呼叫，`ProfileView` 改 import `cropApi`：
 
```typescript
// cropApi.ts
import { marketApi } from './market'
 
export async function getAllCrops(): Promise<CropItem[]> {
  const [veg, fruit, flower] = await Promise.all([
    marketApi.getCrops('Veg'),
    marketApi.getCrops('Fruit'),
    marketApi.getCrops('Flower'),
  ])
  return [...veg, ...fruit, ...flower]
}
 
// ProfileView.vue
import { getAllCrops } from '../api/cropApi'
cropSearchPool.value = await getAllCrops()
```
 
**耦合的問題不是現在**
 
功能上，改之前和改之後完全一樣，使用者感受不到任何差異。
 
問題在維護期。假設某天 `market.ts` 的 `getCrops` 改了介面（改名、改參數格式）：
 
- 改之前：要去改 `market.ts` 本身 + `ProfileView.vue`。但改 Market 模組時，完全不會想到要去找 Profile 的 View 檔。漏改了就是執行期錯誤。
- 改之後：只需要改 `market.ts` + `cropApi.ts`。`ProfileView` 完全不知道背後打的是哪個 API，`market.ts` 的介面怎麼變都和它無關。
**為什麼加一個中間層能解決這個問題**
 
`cropApi.ts` 讓 Profile 模組和 Market 模組之間有一個明確的邊界。`ProfileView` 只依賴 `cropApi`，`cropApi` 依賴 `marketApi`，依賴方向清楚，沒有跨模組的直接連線。
 
這不是過度設計——封裝三次 API 呼叫成一個函式本身就有意義，名字 `getAllCrops` 說清楚了這個函式在做什麼，不需要讀 `ProfileView` 裡那段 `Promise.all` 才能理解。
 
---
 
### 條目 204 — Log 等級的語意：Information 和 Warning 不是裝飾，是信號
 
**我做了什麼**
 
`PriceUpdatedConsumer.cs` 的骨架行為原本記 `LogInformation`：
 
```csharp
_logger.LogInformation("[PriceUpdatedConsumer] Cache invalidation 預留位置（W15 實作）");
```
 
改成：
 
```csharp
// TODO(W15): implement cache invalidation
_logger.LogWarning("[PriceUpdatedConsumer] Cache invalidation 尚未實作，跳過");
```
 
**Log 等級的判斷依據**
 
| 等級 | 語意 | 使用時機 |
|------|------|---------|
| `LogInformation` | 系統正常運作中，這是預期發生的事情 | 請求進來、資料寫入成功、連線建立 |
| `LogWarning` | 系統還能繼續跑，但有一件事不完整或不理想 | 骨架未實作、降級處理、重試成功 |
| `LogError` | 發生了預期外的錯誤，需要關注 | 例外、連線失敗、資料格式異常 |
 
Cache invalidation 尚未實作是「不完整的行為」，不是正常的商業流程。記 `Information` 等於說「這完全正常」，會讓未來看 log 的人誤以為 cache 有被正確清除。記 `Warning` 說「這個地方跳過了，需要注意」，語意正確。
 
**`// TODO(W15)` tag 的價值**
 
`TODO` 是開發工具和 IDE 能 grep 的標記。未來要查「還有哪些地方沒做完」，搜尋 `TODO` 就能找到所有待辦位置，不需要靠記憶。加上期號（W15）讓你知道這個 TODO 預計在哪個 sprint 解決。
 
---

### 條目 205 — XML doc comment：介面才是合約的正確位置
 
**我做了什麼**
 
在 `IUserProfileService` 的 `UpsertUserFarmProfileAsync` 上加了 XML `<summary>`：
 
```csharp
/// <summary>
/// 以 Upsert 語意更新農場設定檔：
/// 若該 userId 已有設定檔則更新欄位；若無則新增一筆。
/// <para>
/// ⚠️ 注意：crops 欄位採全量取代（先刪後寫），
/// 呼叫端必須每次傳入完整的作物清單，不可只傳差異。
/// </para>
/// </summary>
```
 
**為什麼加在介面，不加在實作**
 
`UserProfileService.cs`（實作）和 `IUserProfileService.cs`（介面）技術上都能加 doc comment。但合約說明應該在介面——呼叫端只看介面，如果合約說明只存在於實作，呼叫端永遠看不到。未來如果有第二個實作（測試用的 fake、Mock 等），介面上的 doc comment 仍然適用，實作上的則會漂移。
 
**為什麼 crops 全量取代特別需要標注**
 
Upsert 語意本身很直觀（有則改、無則建），但 crops 全量取代（先刪後寫，非 merge）不是從方法簽名能推導出來的行為。一個拿到這個介面的人，最自然的假設是「我只要傳我想新增的作物就好」，結果傳進去把現有作物都刪了。⚠️ 警告不是裝飾，是告訴維護者「這裡有一個反直覺的行為，你需要特別注意」。
 
**doc comment 的 `<para>` 標籤是什麼**
 
`<para>` 在 XML doc comment 裡是段落（paragraph）。加了 `<para>` 的內容在 IDE hover 時會換行顯示，讓警告視覺上更突出，不會和主說明擠在同一段。
 
---
 
### 條目 206 — 單一真相來源：驗證邏輯應從定義派生，不應各自維護
 
**我做了什麼**
 
把 `MarketController` 裡的 `ValidMarketTypes HashSet` 移除，改在 `MarketTypeMapping.cs` 加入：
 
```csharp
public static bool IsValidMarketType(string? marketType)
    => marketType is not null && _map.ContainsKey(marketType);
```
 
`MarketController` 改呼叫：
 
```csharp
if (!MarketTypeMapping.IsValidMarketType(marketType))
    return BadRequest(InvalidMarketTypeMessage);
```
 
**問題的本質**
 
`_map` 的 Key 集合（`"Veg"`, `"Fruit"`, `"Flower"`）本身就已經定義了「什麼是合法的 marketType」。Controller 另外維護一份 HashSet，等於把同一份知識寫了兩遍。改動的時候必須兩處同步，漏掉一處就出現「API 實際支援但 Controller 擋掉」的 bug。
 
**這樣改還是有兩件事要改**
 
新增類型還是要改 `_map` 和 `InvalidMarketTypeMessage`（錯誤提示文字）。但改之前是三處（`_map`、Controller HashSet、Controller pattern matching），改之後是兩處，而且這兩處的距離更近——`_map` 和 `IsValidMarketType()` 在同一個檔案裡，改完 `_map` 立刻能看到驗證方法，不需要跨檔案跳轉。
 
**什麼是同一模組內部耦合（正常）vs 跨模組耦合（問題）**
 
`MarketController` 知道 `MarketTypeMapping`，兩者都屬於 Market 模組。這是正常的模組內部依賴，Market 模組自己的 Controller 用 Market 模組自己的常數，職責清楚。
 
如果是 `WeatherController` 知道 `MarketTypeMapping`，那才是問題——Weather 模組對 Market 模組的內部實作產生了隱性依賴，Market 的改動可能意外破壞 Weather 的邏輯。
 
**判斷一個常數/定義應該放在哪裡**
 
問：「這個知識屬於哪個概念？誰是它的自然擁有者？」
 
合法的 marketType 清單，是 Market 類型映射關係的一部分，自然擁有者是 `MarketTypeMapping`，放這裡最合理。這個問題的答案通常比較直觀，不需要複雜的分析。
 
---
 
### 條目 207 — 後端防禦上限的判斷依據：何時 hardcode、何時設定化
 
**我做了什麼**
 
在 `RemoveWatchlistItemsAsync` 加了 `.Take(50)`：
 
```csharp
var targetWatchListItems = context.UserWatchlists
    .Where(w => w.UserId == userId && ids.Contains(w.Id))
    .Take(50);
```
 
**為什麼需要後端防禦**
 
前端 UI 是使用者手動勾選，正常使用下不會送出幾千個 id。但這是後端 API，任何持有有效 JWT 的人可以直接打 API 傳任意數量的 id 進來。後端不做限制的話，`WHERE Id IN (id1, id2, ... id10000)` 的 SQL 和一次刪除一萬筆的 transaction 都會正常執行，輕則資料庫壓力大，重則形成 DoS 攻擊面。
 
**50 要不要設定化（不需要）**
 
判斷一個數字是否需要設定化的三個問題：
 
1. 這個值在不同環境（dev / staging / prod）需要不同值嗎？→ 不，防禦上限跟環境無關
2. 這個值可能隨業務需求調整嗎？→ 不，這是技術上限，不是業務規則
3. 這個值有多個地方需要保持一致嗎？→ 不，只在這一個地方使用
三個都是否，維持 hardcode 比設定化更合適。設定化帶來的代價是「啟動時多讀一個設定 key」，但收益幾乎是零，只增加複雜度。
 
對比 `CropCodesMaxCount`：業務規則（查詢上限可能隨 API 性能調整）、多處涉及（驗證邏輯 + 錯誤訊息）→ 設定化有意義。
 
**前後端上限必須一致的理由**
 
前端 50 後端 50，是因為：
- 前端限制是「對後端真實限制的準確反映」
- 前端限制 < 後端：UI 比後端更保守，使用者少刪了但後端其實可以更多，沒有安全意義
- 前端限制 > 後端：UI 允許但後端靜默截斷，使用者以為刪了 60 個，實際只刪了 50 個
靜默截斷是最壞的情況——使用者的意圖和系統的行為不一致，且沒有任何提示。
 
---
 
### 條目 208 — Cache Key 管理：常數化不是現在，是為未來的 Invalidation
 
**我做了什麼**
 
新建 `CacheKeys.cs`：
 
```csharp
public static class CacheKeys
{
    /// <summary>
    /// 農產品交易價格查詢結果。
    /// 完整格式：market:prices:{marketType}:{sortedCrops}:{marketCode}:{startDate}:{endDate}
    /// </summary>
    public const string MarketPricesPrefix = "market:prices:";
}
```
 
`MarketService.BuildPricesCacheKey` 改用：
 
```csharp
return $"{CacheKeys.MarketPricesPrefix}{marketType}:{sortedCrops}:{marketCode ?? ""}:{finalStart}:{finalEnd}";
```
 
**現在只有一個用途，為什麼還要抽**
 
現在只有 `BuildPricesCacheKey` 用到 `"market:prices:"`，抽成常數的直接收益確實很小。但 Cache Invalidation（PriceUpdatedConsumer 的 TODO W15）必然需要這個前綴做 Redis pattern scan：
 
```csharp
// 未來 Cache Invalidation 的用法（W15 實作時）
await _cache.KeyDeleteByPatternAsync($"{CacheKeys.MarketPricesPrefix}*");
```
 
如果現在不抽，W15 的工程師（或未來的自己）會在 Consumer 裡手寫 `"market:prices:"`。兩個字串完全獨立，一旦有人改了 `BuildPricesCacheKey` 裡的 prefix，Invalidation 的 pattern 不會跟著改，快取永遠不清，使用者看到舊資料。這個 bug 沒有任何編譯錯誤，只能靠 Redis 手動查才能發現。
 
**這是 Cache 設計的通用原則**
 
Cache Set 和 Cache Invalidation 必須使用相同的 Key 結構，否則 Invalidation 無效。把 prefix 抽成常數是最低成本的方式，確保兩個操作始終指向同一個 Key 空間。
 
**doc comment 裡記錄完整 Key 格式**
 
```csharp
/// 完整格式：market:prices:{marketType}:{sortedCrops}:{marketCode}:{startDate}:{endDate}
```
 
這一行的價值：W15 工程師不需要去讀 `BuildPricesCacheKey` 才能知道 Key 的格式是什麼，文件就說清楚了。
 
---
 
### 條目 209 — GetValue fallback：設定外化不應改變行為
 
**我做了什麼**
 
`AgriProductsTransSyncWorker.cs` 把 hardcode 的 90 秒改成讀設定：
 
```csharp
var httpTimeoutSeconds = _configuration.GetValue<int>(
    "AgriProductsSyncWorker:HttpTimeoutSeconds", 90);
using var httpTimeoutCts = new CancellationTokenSource(
    TimeSpan.FromSeconds(httpTimeoutSeconds));
```
 
**GetValue<T>(key, defaultValue) 的語意**
 
```csharp
_configuration.GetValue<int>("AgriProductsSyncWorker:HttpTimeoutSeconds", 90)
```
 
行為：
1. 去設定系統（appsettings.json + 環境變數等）找 `AgriProductsSyncWorker:HttpTimeoutSeconds`
2. 找到了 → 用那個值
3. 找不到（key 不存在）→ 用 `90` 當預設值，不拋例外
第二個參數 `90` 不是隨便選的，必須等於原本 hardcode 的值。理由：設定外化這個動作本身不應該改變系統行為。如果 fallback 是 `0`，所有沒有加設定的環境 timeout 會變成 0 秒（立刻超時），造成部署後才發現的 bug。
 
**和 Fail-Fast（條目 200）的對比**
 
兩種讀取設定的模式語意不同：
 
| 模式 | 使用時機 | 設定缺失時的行為 |
|------|----------|-----------------|
| `GetValue(key, defaultValue)` | 有合理預設值的技術參數 | 用預設值繼續跑 |
| `?? throw new InvalidOperationException` | 必要的安全相關設定 | 啟動時報錯 |
 
`HttpTimeoutSeconds` 有合理預設值（90 秒），缺失時用預設值繼續跑是合理的。`Jwt:SecretKey` 沒有合理預設值，缺失代表設定錯誤，應該 Fail-Fast。
 
**設定外化的真正收益是什麼**
 
不是「改起來更方便」（每次改都要重新部署），而是：
- 不同環境可以有不同值（測試環境可能要更短的 timeout 讓測試跑快一點）
- 修改不需要改程式碼 → 不需要重新編譯 → 不需要重新測試整個 build
- 設定統一在設定檔管理，不散落在程式碼各處

---

## 跨條目的通用原則整理

這個區塊隨著條目增加而更新。每次發現某個原則在不只一個條目裡出現，就把它移到這裡，代表它已經從「這次的經驗」升級成「我的習慣」。

**關於外部 API**
對接任何外部 API 之前，先打一次、看真實資料，再設計 Entity。文件是參考，真實回傳才是設計依據。API 的隱性限制是常態，設計要能容忍它。

**關於防重複**
先定義「什麼叫重複」，再決定 HashSet Key 的組合。Key 必須精確對應「重複」的定義，不是隨便選一個欄位。

**關於 Log**
Log 的等級要反映真實嚴重程度。正常的商業限制不應該出現在 Warning，程式錯誤不應該只記 Information。一律用結構化日誌的具名佔位符，不用字串插值，讓 log 具備可查詢性。

**關於資料表設計**
從查詢模式出發設計表結構。先問「這個資料最常被怎麼查」，再決定欄位和索引。需要精確比對的多值欄位用關聯表，非結構化的才用 JSON 欄位。

**關於技術債**
能用低代價現在解決的問題，就現在解決。評估標準是：現在做的代價 vs 以後做的代價。

**關於 Worker 的開發順序**
想清楚 → 建骨架 → 看資料 → 寫邏輯 → 眼睛驗收。每步做完才進下一步。

**關於讀懂不熟悉的程式碼**
遇到不認識的型別或方法：（1）滑鼠 hover 看 IDE 說明，（2）按 F12 看定義和回傳型別，（3）還不懂再查文件或問人。大部分疑問在第一步就能解決。

**關於防禦性編程**
解析外部資料一律用 TryParse 而不是 Parse，避免例外讓程式崩潰。數字解析帶 InvariantCulture，確保跨環境行為一致。nullable 欄位在 Mapping 層統一處理，不散落在各處。

**關於去重策略**
先問「什麼叫重複，對誰而言」，把業務答案寫下來，再從答案推導 Hash Key 的組合。去重是業務判斷，不是技術問題。

**關於 DTO 的邊界**
每個外部 API 端點對應自己的 DTO，不因結構相似就共用。外部形狀由 API 決定，不由程式內部的方便性決定。共用代表耦合，獨立代表清晰。

**關於 Silent Failure**
TryParse 系列失敗不拋例外，是沉默的。新 Worker 第一次跑完，要主動確認 Log 裡的筆數是否合理——「無新資料」在系統剛上線時是需要懷疑的信號，不是值得放心的結果。

**關於資料生命週期**
資料有兩種根本不同的生命週期：快照型（觀測值、事件——只增不改）和主檔型（站台、使用者——可被維護）。快照型不需要 IsActive/UpdatedAt，主檔型必須有。設計 Entity 前先判斷是哪一種。

**關於欄位歸位**
一個值應該存在哪張表，取決於「這個屬性屬於哪個概念」，不是「從哪裡拿最方便」。固定屬性存在對應實體的主檔表，哪怕初始值的來源比較迂迴。

**關於軟刪除觸發條件**
外部清單型資料的軟刪除觸發點是「上次有、這次沒有」，不是「API 在某個欄位說它停用了」。來源不包含它，比來源聲明它停用，更可靠。

**關於抽象化**
重複是需要考慮抽象化的信號，不是命令。判斷標準是「抽出來之後更清楚了嗎」。若抽象需要大量泛型或 callback 才能容納差異，抽象的成本可能高於重複的成本，先不抽。

**關於 SaveChangesAsync 的位置**
SaveChangesAsync 影響整個 Scope 裡所有的 Change Tracker 變更，不只是最近一次的 AddRange。多種操作共存時（例如寫入觀測資料 + 更新站台座標），SaveChangesAsync 放在所有操作的最後，確保沒有任何一種變更因為提前 return 而被遺漏。

**關於正規化動機**
「需要拆表」有兩種不同根源，不能混用。1NF 問題：一個欄位存了多個值（用逗號等分隔符塞入），解法是把每個值拆成獨立的一列。3NF 問題：某些欄位只跟某個非主鍵欄位有關（與主鍵無直接依賴），解法是把這些欄位抽成獨立的主檔表。看到「應該拆表」的直覺出現時，先判斷是哪一種，設計方向才不會跑偏。

**關於死碼**
發現死碼時，先問「為什麼它是死碼」，不要直接刪。死碼有時候是症狀，指向上游設計有缺失的失敗路徑——找到根本問題，修正上游，死碼就自然恢復意義了。

**關於跨模組關聯**
跨 DbContext 的關聯只能存在於值層面（字串欄位），不能存在於物件層面（導覽屬性）。導覽屬性是 EF Core 向所屬 DbContext 宣告「我要管這張表」的入口，加了就會在 Migration 裡多建表。跨模組的 FK 靠應用程式層保證正確性，放棄 FK constraint 和導覽屬性帶來的自動保護。已知的代價是使用者刪除後可能產生孤兒記錄，這個清理責任由應用程式層在對應的功能實作時補上。

---

## 跨條目的通用原則整理（v11.0 更新）

以下為 v11.0 新增或強化的原則，和既有原則並列管理：

**關於增量同步設計**
進度追蹤應該記錄「我執行了什麼」，而不是「我的執行結果長什麼樣」。用 DB 的 `MAX(某欄位)` 反推進度，在「有執行但沒有產生資料」的合法情況下會卡死。獨立的同步狀態表（SyncState 模式）是更可靠的替代方案。

**關於 off-by-one 的設計語意**
設計斷點恢復機制時，先定義欄位語意（「已完成的最後一個」vs「下次從哪個開始」），再從語意推導初始值。做完後用一個具體例子驗算：帶入初始值算出第一次執行的起點，確認和預期一致。

**關於 API 參數精確度與分頁**
遇到分頁 API 時，先評估能否透過增加查詢參數精確度（縮小每次查詢的範圍）來讓每次回傳結果自然不需要分頁。精確參數 + 多次呼叫，通常比寬泛參數 + 分頁迴圈更易維護、錯誤隔離更好。

**關於 EF Core Change Tracker 的自動追蹤**
透過 EF Core 查詢取得的 Entity 處於 Change Tracker 追蹤下，修改屬性後 `SaveChangesAsync` 會自動產生 UPDATE SQL，不需要顯式呼叫 `.Update()`。`.Update()` 是給 Disconnected 場景（Entity 從外部傳入，不在追蹤中）使用的，在 Scoped DbContext 的正常查詢流程裡幾乎不需要。

**關於獨立 SaveChanges 的語意**
把某個寫入操作放在其他操作之前（順序）是不夠的，還需要確保它有獨立的 SaveChanges（持久性）。只有具備獨立 SaveChanges 的前置寫入，才能保證「不管後續操作成功或失敗，這筆資料都已永久存在」。

---

## 跨條目的通用原則整理（v12.0 更新）

以下為 v12.0 新增或強化的原則，和既有原則並列管理：

**關於並發與執行緒安全**
需要並發的部分和需要保持狀態一致性的部分應該分開。讓 Task 只負責 I/O 等待（打 API、讀檔案），把所有有狀態的操作（比對、更新快取、寫入集合）集中在主執行緒依序執行。這樣能獲得 I/O 並發的效能收益，同時完全規避執行緒安全的複雜性。

**關於 TOCTOU**
「先檢查存不存在，再根據結果寫入」這個模式在多執行緒環境下是危險的。換成執行緒安全的集合型別只解決了寫入操作本身的衝突，沒有解決「Check 到 Use 之間可能被插入」的問題。需要把整個「Check + Use」變成原子操作，或讓這段邏輯在單一執行緒執行。

**關於 EF Core 的查詢邊界**
EF Core LINQ 和普通 LINQ 看起來相同，但執行環境不同。`ToListAsync()` 之前是 SQL 翻譯模式，每個操作都必須能對應到合法 SQL；之後是記憶體模式，任何 C# 語法都可以用。ValueTuple 投影、自訂方法、複雜運算都必須放在 `ToListAsync()` 之後。

**關於效能診斷**
先量化問題規模（操作執行了幾次、每次成本多少），再找最大浪費，最後按照「獨立 → 結構性 → 高風險」的順序逐步優化。每次只改一個維度，確認後再進行下一個。

---
 
## 跨條目的通用原則整理（v12.1 更新）
 
以下為 v12.1 補充的原則：
 
**關於 Change Tracker 可見範圍與去重邏輯的時序**
把 SaveChangesAsync 移出迴圈（批次化）之後，Change Tracker 會在迴圈執行期間累積多個來源的資料。任何依賴「查 DB 做去重」的 existingKeySet 對這些尚未存入 DB 的資料完全不可見。批次化之後的去重邏輯必須在 AddRange 之前完成，作用範圍必須覆蓋本次批次的所有來源，不能依賴 DB 查詢的快照來攔截批次內的重複。

---

## 跨條目的通用原則整理（v15.0 更新）

以下為 v15.0 新增或強化的原則，和既有原則並列管理：

**關於 API 查詢層的設計原則**
Service 層（MarketService）的職責是「查什麼資料、怎麼計算」，Controller 層的職責是「收什麼輸入、驗什麼格式、回什麼 HTTP 狀態碼」。任何根據業務需求可能改變的邏輯（預設日期區間、聚合策略）屬於 Service；任何永遠由技術規格決定的邏輯（輸入格式驗證）屬於 Controller。

**關於輸入驗證的顯式表達**
Controller 越靠近使用者，越應該對「壞輸入」做出明確的、友好的回應，而不是依賴框架的隱式行為。用 `TryXxx` 或 `nullable` 回傳值做驗證，配合 `BadRequest("清楚的訊息")` 回應，讓錯誤路徑和 happy path 一樣清晰可見。

**關於 DTO 的分層命名**
DTO 資料夾的命名應該反映「這個 DTO 服務的角色」，而不是「這個 DTO 的來源」。角色命名（`WorkerResponses`、`ApiResponses`）比來源命名（`Moa`）更有自解釋性，讓維護者不需要任何背景知識就能理解資料夾的用途。

**關於 EF Core 的查詢邊界**
`ToListAsync()` 是 EF Core 查詢的 SQL/C# 邊界。邊界之前是 SQL 翻譯模式，每個操作必須能對應到合法 SQL；邊界之後是 C# 執行模式，可以使用任何 .NET 語法。C# 專屬操作（建構子呼叫、自訂方法、ValueTuple 投影）必須放在邊界之後。

**關於聚合語意**
聚合函數的選擇由欄位的業務語意決定：比率型欄位（價格、濃度）跨維度聚合用 AVG；絕對量型欄位（數量、金額）跨維度聚合用 SUM。技術上兩者都可行，但只有語意正確的那個能給前端提供有意義的資料。

---
 
## 跨條目的通用原則整理（v18.0 更新）
 
以下為 v18.0 新增或強化的原則，和既有原則並列管理：
 
**關於 Cache 設計**
Cache Key 必須包含所有影響查詢結果的參數，包括那些有預設值的可選參數（用解析後的 final 值，不用原始的 null）。TTL 的設計依據不是資料更新頻率，而是「主動失效機制失敗時能接受的最大延遲」。Cache-Aside 的三步邏輯（查 cache → 查 DB → 寫 cache）是最常見的模式，職責透明，易於控制。
 
**關於 RabbitMQ 的設計選擇**
Exchange Type 的選擇依據是通訊的語意，不是複雜度的高低。fanout 是廣播（所有人都收），topic 是訂閱（只有關心這個主題的人才收）。routing key 用點號分層命名（`系統.模組.事件`）讓未來的 pattern matching 更有彈性。`durable: true` 在絕大多數生產場景都應該開。
 
**關於訊息可靠性**
`autoAck: false` + 手動 `BasicAckAsync` 是 RabbitMQ Consumer 的預設選擇，確保訊息在成功處理後才從 Queue 刪除。訊息遺失的代價決定了 Ack 策略：代價可接受才考慮 `autoAck: true`。
 
**關於 IHostedService**
需要跟應用程式生命週期綁定的背景邏輯（訊息消費、排程、長連線維護），就用 `IHostedService`。事件驅動的 Consumer 用 `Task.Delay(Timeout.Infinite, stoppingToken)` 保持 `ExecuteAsync` 不返回，直到應用程式停止。
 
**關於分散式系統整合**
骨架優先：先讓鏈路通（連線建立、基本操作驗收），再實作完整功能。整合風險前移比在功能完整後才整合更容易控制和診斷。

---

## 跨條目的通用原則整理（v19.0 更新）

以下為 v19.0 新增或強化的原則，和既有原則並列管理：

**關於 EF Core 的查詢邊界（強化）**
`ToListAsync()` 是 SQL 翻譯模式和 C# 執行模式的分界點，之前的操作必須能對應合法 SQL，之後可以使用任何 .NET 語法。`GroupBy + 取整列`、多欄位字串組合、自訂方法呼叫都屬於 C# 模式，必須放在 `ToListAsync()` 之後。遇到「編譯過但執行拋 InvalidOperationException: could not be translated」，就是越界了。

**關於跨表關聯的兩條路徑**
有導覽屬性 → `Include`（語意清楚，EF Core 自動決定 JOIN 類型）；沒有導覽屬性 → LINQ `Join`（手動指定 Key）。Entity 是否有導覽屬性，取決於當初設計時是否預見了查詢需求。後補導覽屬性需要修改 Entity 和可能需要 Migration，不是隨時都能輕易加上去的。

**關於 nullable 參數的驗證邊界**
可選參數 + 格式驗證的組合，需要明確區分「有傳但格式錯誤」和「沒有傳（合法 null）」。前者回 400，後者套預設值正常處理。判斷條件是 `參數本身 != null && 解析結果 == null`，而不是只看解析結果。

**關於 API 層的分組依據**
API 函式的分組依據是「修改原因是否相同」，而不是「業務模組是否相同」。需要 auth header 的 API 和不需要的 API 一定要分開，因為它們會在不同時間點因為不同原因被修改（Auth 整合、安全性調整不應該影響公開查詢的邏輯）。

**關於全域副作用的清理**
`setInterval`、`document.addEventListener`、WebSocket、`ResizeObserver` 等全域副作用的生命週期不跟著 Vue 元件自動結束，必須在 `onUnmounted` 手動清理。「有建立就有清理」是鐵則。每次在 `onMounted` 裡建立全域副作用，立刻問：「清理邏輯在哪裡？」

**關於時間序列圖表的缺失資料**
多個資料源的時間點不完全對齊是常態，不是錯誤。正確表達方式是 `null`（不是 `0`），搭配 `spanGaps: true` 讓圖表視覺連續。用 `0` 填補缺失值等同於「聲稱那個時間點的量測值是零」，在農業資料的語境下（雨量 0mm vs. 沒有觀測）會造成誤導。

---

## 跨條目的通用原則整理（v21.0 更新）

以下為 v21.0 新增或強化的原則，和既有原則並列管理：

**關於資料架構與前端設計模式的對應**
前端元件的設計方式（要不要 store、幾時初始化、如何觸發）由後端資料的組織結構決定。看起來相同的 UI 元件（如市場下拉），背後的資料流可能完全不同：有獨立主檔 API 的用 store 提前載入；市場名稱只存在交易資料裡的，用 computed 從查詢結果動態萃取。設計前先問「這份資料在後端是主檔型還是從屬型」。

**關於 computed 的適用判斷**
判斷標準是兩個問題：這個計算有副作用嗎？輸入相同，結果是否永遠相同？兩個都是「是」，就用 `computed`。「watch 某個資料 → 手動寫入另一個 ref」這個模式，幾乎所有情況下都可以改成 `computed`，且更安全——不會有「watch 沒觸發導致資料過時」的問題。

**關於 CancellationToken 的生命週期**
每個 CancellationToken 對應一個特定的取消理由，不要跨越語意邊界共用。Worker 生命週期 token（`stoppingToken`）只控制「是否繼續下一輪」，不應傳入單一 HTTP 請求。HTTP 請求用獨立的 `CancellationTokenSource` 計時，Semaphore 等待用 `CancellationToken.None`。混用會導致取消訊號意外傳播。

**關於 Vite Proxy 的正確用途**
前端開發環境的 API 請求應走 Vite proxy，`baseURL` 應為空字串，不應直打後端絕對 URL。直打後端讓前端依賴後端的確切 port 和 CORS 設定，是隱性的脆弱耦合，「巧合能跑」不等於「正確設計」。Vite proxy 的存在意義是讓前端對後端的實際部署細節保持透明。

---

## 跨條目的通用原則整理（v21.2 更新）

以下為 v21.2 新增或強化的原則，和既有原則並列管理：

**關於命名的語意邊界**
變數名稱應該反映資料當下的形狀和狀態，而非它的來源或操作過程。`query` 前綴在 C# / EF Core 語境下暗示未執行的 `IQueryable`，用在已 `ToListAsync()` 的結果上會造成閱讀誤解。`grouped` / `filtered` / `sorted` 這類形容詞前綴能幫助讀者在不追蹤查詢的情況下猜到變數的資料形狀。

**關於防禦性設計的判斷**
防禦性設計的判斷依據不是「現在有沒有問題」，而是「不加的代價是明確的還是不可預測的」。對歷史型、線性累積的資料集加查詢上限（Take / 日期範圍），代價明確（超過上限的資料需要分頁），不加的代價不可預測（某個時間點之後查詢突然變慢）。

**關於抽出具名方法的判斷**
Lambda 的代價是它沒有名字，閱讀者必須讀完整個 lambda 才能理解意圖。抽出具名方法的兩個判斷依據：（1）有沒有一個清楚的名字能說明這段邏輯在做什麼；（2）這是方法的私有實作細節，還是可能被其他地方共用的領域知識。後者應優先考慮放入對應的工具類別（如 DateHelper）而非保留為 private static。

**關於 Doc Comment 的定位**
好的 doc comment 說的是「方法簽名沒辦法說的事情」：設計決策的動機、邊界條件的行為、為什麼不用另一種更直覺的寫法。輸入輸出的具體範例是最有效的說明形式，讓閱讀者不需要在腦中計算就能驗證理解。

**關於架構邊界的物理化**
Extension Method 等重構手法的價值不只是「讓程式碼更短」，而是讓架構設計的模組邊界從「只存在於文件」變成「在程式碼的物理結構中可見」。當 Program.cs 的五行直接對應架構圖的五個方塊，維護者不需要在程式碼和文件之間來回切換。

**關於設定與程式碼分離**
判斷一個值屬於設定還是程式碼：如果這個值在不同環境（dev / staging / prod）可能不同，它是環境事實，屬於設定檔。如果它是邏輯規則，屬於程式碼。環境事實混在程式碼裡，是多環境部署時容易出問題的根源，也讓修改設定的代價不必要地升高（需要重新編譯）。

**關於「可改可不改」的決策框架**
遇到「可改可不改」的項目，需要回答三個問題：改了有什麼收益？不改有什麼風險？兩者的代價各是什麼？三個問題都能回答，才是技術決策，而不是隨機選擇或盲目照單全收。能說清楚為什麼不改，和能說清楚為什麼改，同樣是工程師素養的展現。

---

## 跨條目的通用原則整理（v21.3 更新）

以下為 v21.3 新增或強化的原則，和既有原則並列管理：

**關於 pure function 的歸屬位置**
判斷一個 pure function 放在哪裡，問「現在有幾個使用者」。一個使用者 → `private static`，保持在使用它的類別裡，符合 YAGNI 原則。多個使用者（或有合理的跨模組共用理由）→ 搬到工具類別。「未來可能需要」不是搬移的理由，「現在確實有第二個使用者」才是。相同的邏輯可能歸屬不同位置——`ConvertRocRestDay` 放 DateHelper（跨模組領域知識），`BuildPricesCacheKey` 放 `private static`（目前唯一呼叫點）——兩者都正確，因為判斷依據不同。

**關於 Doc Comment 的焦點**
Doc comment 說「為什麼」，方法簽名說「是什麼」。方法名稱已經告訴讀者「做什麼」，doc comment 最有價值的部分是：設計決策的動機（為什麼需要排序）、邊界條件的行為（為什麼用 finalStart 而非 null）、和其他選擇相比的取捨。好的 doc comment 讓讀者在不追蹤實作的情況下能驗證自己的理解。

**關於 enum 的適用情境**
enum 解決「有分支行為的閉合集合」問題，不是「防止非法字串輸入」的問題。只有在不同 enum 值對應不同程式碼路徑（switch/case）時，enum 的型別安全才真正發揮價值。若值從頭到尾只是一個 SQL `WHERE` 過濾條件，沒有任何分支行為，enum 的核心收益不會觸發，但轉換成本（EF Core `.ToString()`）和改動範圍（全面修改）依然存在。

**關於 GetValue fallback 的語意**
`IConfiguration.GetValue<T>(key, defaultValue)` 的第二個參數是「設定缺失時的行為」。設定是優化，程式應該在設定缺失時有合理的預設行為，而不是崩潰或回傳型別預設值（如 `int` 的 0）。fallback 值應該和原本的硬編碼值相同，確保設定外化這個動作本身不改變行為。

**關於測試案例選擇的出發點**
寫測試案例之前，先識別「這個方法的核心承諾」，再從承諾出發選案例。邊界值的價值在於它們剛好在合法與非法的分界線上，最能驗證設計者對語意的理解。「同一結構，不同年份，結果截然相反」（閏年 vs 非閏年的 2/29）是展示語意理解最有力的測試對。

---

## 跨條目的通用原則整理（v22.0 更新）

以下為 v22.0 新增或強化的原則，和既有原則並列管理：

**關於身分驗證的層次劃分**
JWT 是 token 格式（規格），OAuth 是授權協議（流程），兩者不在同一個抽象層次，不能拿來二選一比較。使用 JWT 不代表使用 OAuth；使用 OAuth 的 token 可以是也可以不是 JWT 格式。碰到新的技術詞彙時，先問「它在哪個層次解決哪個問題」，再和已知的詞彙比較。

**關於 Claims 的最小必要原則**
Claims 的設計依據是「授權決策所需的最小資訊」，不是「前端所有可能有用的資訊」。判斷標準：這個 Claim 會影響「後端決定放不放行」嗎？不影響的資料由 API response 或 Store 管理，不應打包進 token 增加每次請求的 payload 大小。

**關於無狀態設計的已知取捨**
JWT 的無狀態性讓後端無需查 DB 驗證 token，代價是無法即時廢止已發行的 token。「無法立即廢止」不是 bug，是設計選擇的代價。需要即時廢止能力時，搭配 Redis 黑名單（儲存廢止的 token jti）是標準補救方案。在 Portfolio 規模的短過期時間（7 天）下，這個代價通常可接受。

**關於 Pinia Store 的使用邊界**
`useAuthStore()` 只能在 Vue 的 Composition API 環境（`setup()`、`<script setup>`）下呼叫。純 TypeScript 模組（api 層、工具函式）需要跨越這個邊界時，應從環境取資料（`localStorage`、`sessionStorage`、`window` 等瀏覽器原生 API），而非嘗試 import store。這不只是技術限制，更是架構邊界的正確體現：api 層依賴環境，不依賴應用狀態。

**關於 HTTP 語意與敏感資料**
URL（Query String）是公開、可記錄、可快取的資源識別符；Request Body 是不應被快取的操作內容。敏感資料（密碼、個資、token）放 Body（`[FromBody]`），查詢條件（篩選參數、分頁、日期範圍）放 Query（`[FromQuery]`）。這不只是 HTTP 規範的要求，也是 Server log 安全性的基本防線。

---

## 跨條目的通用原則整理（v23.0 更新）

以下為 v23.0 新增或強化的原則，和既有原則並列管理：

**關於身分驗證的層次劃分**
JWT 是 token 格式（規格），OAuth 是授權協議（流程），兩者不在同一個抽象層次，不能拿來二選一比較。使用 JWT 不代表使用 OAuth；使用 OAuth 的 token 可以是也可以不是 JWT 格式。碰到新的技術詞彙時，先問「它在哪個層次解決哪個問題」，再和已知的詞彙比較。

**關於 Claims 的最小必要原則**
Claims 的設計依據是「授權決策所需的最小資訊」，不是「前端所有可能有用的資訊」。判斷標準：這個 Claim 會影響「後端決定放不放行」嗎？不影響的資料由 API response 或 Store 管理，不應打包進 token 增加每次請求的 payload 大小。

**關於無狀態設計的已知取捨**
JWT 的無狀態性讓後端無需查 DB 驗證 token，代價是無法即時廢止已發行的 token。「無法立即廢止」不是 bug，是設計選擇的代價。需要即時廢止能力時，搭配 Redis 黑名單（儲存廢止的 token jti）是標準補救方案。在 Portfolio 規模的短過期時間（7 天）下，這個代價通常可接受。

**關於 Pinia Store 的使用邊界**
`useAuthStore()` 只能在 Vue 的 Composition API 環境（`setup()`、`<script setup>`）下呼叫。純 TypeScript 模組（api 層、工具函式）需要跨越這個邊界時，應從環境取資料（`localStorage`、`sessionStorage`、`window` 等瀏覽器原生 API），而非嘗試 import store。這不只是技術限制，更是架構邊界的正確體現：api 層依賴環境，不依賴應用狀態。

**關於 HTTP 語意與敏感資料**
URL（Query String）是公開、可記錄、可快取的資源識別符；Request Body 是不應被快取的操作內容。敏感資料（密碼、個資、token）放 Body（`[FromBody]`），查詢條件（篩選參數、分頁、日期範圍）放 Query（`[FromQuery]`）。這不只是 HTTP 規範的要求，也是 Server log 安全性的基本防線。

**關於 DbContext 歸屬的判斷依據**
問「這份資料的消費者是誰」。消費者是所有模組 → CoreDbContext；消費者是 Identity 本身 → ApplicationDbContext；消費者是特定業務領域 → 那個業務的獨立 DbContext。不是看資料「感覺上屬於誰」，是看「誰真的需要這份資料」。

**關於 EF Core 關聯設定的完整性**
EF Core 的關聯設定需要三個要素都明確：HasForeignKey（FK 是哪個欄位）、HasPrincipalKey（主表 Key 是哪個欄位）、HasOne/WithMany 的 lambda（導覽屬性是什麼）。任何一個缺失，EF Core 用命名慣例推導，推導錯了就產生 shadow property，症狀是 SQL 找不到自動推導出的欄位名稱（格式：導覽屬性名 + 主表 PK 名）。主表 PK 是 string 等非 int 型別時，HasPrincipalKey 是必要的。

**關於 HTTP 狀態碼的語意精確性**
區分「業務上的空值」和「技術上的錯誤」。空值是業務合法狀態時用 200+null；找不到資源是技術錯誤時用 404。「使用者還沒有設定過」是初始狀態，不是錯誤，應回 200。設計 API 時，先問「這個空是應該存在但找不到，還是合法的初始狀態」。

**關於 Upsert 的適用條件**
Upsert（合併新增和更新為一個操作）適合的條件：資源的識別已知（不需要伺服器分配 ID），且業務規則是「一個識別只有一筆資料」。這種場景下，分開 POST + PUT 是讓呼叫方承擔了不該承擔的狀態判斷職責（「現在有沒有這筆資料？」屬於資料層的事，不屬於呼叫方的事）。

**關於前端 Store 的共用邊界**
重用 store 的判斷標準是「需求語意相同嗎」，而不是「資料來源相同嗎」。來自同一個 API、但在不同頁面的用途和語意不同時，各自管自己的資料，不強行共用。共用帶來隱性耦合：一個地方的行為改變可能意外影響另一個地方。

**關於瀏覽器事件順序的競態問題**
`mousedown → blur → mouseup → click` 是瀏覽器的標準事件順序。任何「點選某個元素同時會觸發目前聚焦元素的 blur」的 UI 模式都會遇到這個問題（典型場景：Dropdown 的選項點選）。解法：改用 mousedown（比 blur 早），或在 onBlur 加 setTimeout 延遲讓 click 先完成。

---

## 跨條目的通用原則整理（v24.0 更新）

以下為 v24.0 新增或強化的原則，和既有原則並列管理：

**關於 EF Core 導覽屬性的選擇性**
導覽屬性是否加入 Entity，應由「查詢時是否需要從這個方向導航」決定，不是「有 FK 就一定要加」。不加導覽屬性讓 Entity 更輕，查詢不需要 Include，也不用維護雙向一致性。EF Core 的 `HasOne<T>()` 泛型寫法支援「知道關聯但不需要導覽屬性」的場景，是有意識的設計選項，不是退而求其次。

**關於 Service 層與 Controller 層的職責邊界**
Service 說「業務上發生了什麼」（存在/不存在、成功/失敗），Controller 說「對 HTTP 客戶端這意味著什麼」（200/409/204）。Service 回傳 bool 或業務結果物件，不回傳 HTTP 物件，是職責邊界正確落點的體現。讓 Service 拋帶 HTTP 語意的 Exception 是邊界模糊的症狀。

**關於 userId 在 Service 方法簽名的必要性**
Service 層必須透過參數接收 userId，有架構和安全兩個獨立理由：架構面，Service 層沒有 HTTP Context，JWT Claims 只能在 Controller 層讀取；安全面，刪除等異動操作應同時比對資源歸屬（userId）和資源識別（id），防止越權操作。兩個理由互相獨立，各自成立。好的架構邊界設計往往順帶提升安全性。

**關於前端陣列參數的跨框架格式差異**
axios 預設把陣列展開成 `key[0]=v1&key[1]=v2` 格式，但 ASP.NET Core 的 `[FromQuery] IEnumerable<T>` 期望 `key=v1&key=v2`（重複同名參數）。需要用 `URLSearchParams.append()` 手動控制格式。遇到陣列參數傳遞問題，先確認實際送出的 Query String 格式，再比對後端期望格式。

**關於 Vue Router v4 beforeEach 語法**
Vue Router v4 建議用 return 值取代 next() callback。return 路由物件 = 導向，return true = 放行，return false = 取消。return 寫法消除了「忘記呼叫 next()」和「重複呼叫 next()」兩類 bug，也讓每個分支的意圖更直觀可見。

**關於 UI 狀態 vs 應用狀態的分界**
判斷狀態是否進 Store：「有跨元件/跨頁面的生命週期需求嗎？」有 → Store；沒有 → View 層 ref。Store 管理是成本，不是免費工具。瞬間性的操作狀態（當前勾選項、下拉展開狀態）屬於 UI 狀態，不應該推進 Store。過度使用 Store 會讓狀態管理複雜度無謂上升。

**關於錯誤訊息的清除時機**
錯誤訊息描述的是「上一個操作的結果」。當使用者改變了操作對象（重新選擇），舊的錯誤描述不再對應當前狀態，應清除。清除時機是「使用者重新做選擇」，不是「使用者按送出」。後者會導致「送出→清除錯誤→API回傳新錯誤」的閃爍感，且如果使用者不送出而只是換選，舊錯誤會持續殘留造成混淆。

**關於表單重置的語意**
表單重置是「成功完成後的 UX 清理」，不是「操作完成的自動行為」。失敗不是完成，所以失敗時保留表單內容，讓使用者能看清楚「剛才的選擇是什麼」加上「為什麼失敗」，降低下一步的判斷成本。

---
 
## 跨條目的通用原則整理（v24.1 更新）
 
以下為 v24.1 新增或強化的原則，和既有原則並列管理：
 
**關於環境相關設定的外化**
所有和執行環境相關的值（hostname、port、連線字串、feature flag、業務規則上限）都應該外化到設定檔，程式碼只保留讀取邏輯。「本機正常、部署才炸」是 hardcode 環境值的典型症狀。.NET 設定系統的優先順序（環境變數 > appsettings.json）讓同一份 .cs 在不同環境讀到不同的值，是標準的多環境設定模式。
 
**關於 Fail-Fast 的定位**
Fail-Fast 的核心價值不只是錯誤訊息更清楚，而是讓問題在最早的時間點暴露。設定驗證放在建構子（DI 容器建立時），讓設定錯誤在應用程式啟動時就報錯，而不是在第一個請求進來才炸。一個啟動就炸的服務比一個平時正常偶爾炸的服務更容易診斷和修復。
 
**關於 JWT issuer / audience 的語意分離**
`Issuer` 描述「token 的發行者」，`Audience` 描述「token 的預期使用者」。兩者相同在單一服務架構下技術可行，但語意上是不同的概念，應各自獨立設定。Audience 的設計價值在未來有多個 audience（多個前端、管理後台）時才充分發揮：不同 audience 的 token 天然無法跨越邊界使用，不需要額外的授權邏輯。
 
**關於 Log 等級的語意精確性（強化）**
Log 等級是給運維人員看的信號，不是給程式碼看的裝飾。骨架行為（功能預留但未實作）屬於 Warning，因為它是不完整的行為；記 Information 等於宣稱「一切正常」，會誤導未來看 log 的人。`// TODO(week)` tag 讓 grep 找到所有待辦位置，不靠記憶。
 
**關於 const 抽取的判斷依據（強化）**
「字串看起來很像」不是抽 const 的理由。正確判斷標準是「同一條業務規則在同一個 class 裡出現多次，改一個理所當然要改另一個」。語意獨立的字串即使結構相似也不應強行合併，否則閱讀者會誤以為它們是同一件事。const 的位置：class 頂部欄位宣告區，在建構子之前。
 
**關於前端 API 層的模組邊界**
前端的 api/ 層要和後端的模組邊界對應：Profile 頁面的資料需求應透過 profileApi 或獨立的 cropApi 滿足，不直接呼叫 marketApi。這不是過度設計，而是「改 Market 模組的時候不需要去找 Profile 的 View 檔」這個維護需求的最小成本實現。中間層（cropApi.ts）讓依賴方向清楚，也讓函式有一個說清楚自己在做什麼的名字。

---
 
## 跨條目的通用原則整理（v24.2 更新）
 
以下為 v24.2 新增或強化的原則，和既有原則並列管理：
 
**關於介面 doc comment 的歸屬**
合約說明應在介面，不在實作。呼叫端只看介面，合約說明寫在實作等於讓呼叫端永遠看不到。介面可能有多個實作（正式、測試 fake、Mock），doc comment 寫在介面對所有實作都適用，寫在實作只對那一個實作適用。
 
**關於驗證邏輯的單一真相來源**
「什麼是合法輸入」的定義應從資料定義派生，不應在驗證邏輯裡另外維護一份。已有 Dictionary、HashSet、Enum 定義了一組合法值時，驗證方法應查它，不應手動列舉相同的值集合。兩份清單沒有連動，新增成員時容易漏改一處。
 
**關於防禦上限的設定化判斷**
純技術防禦上限（後端 Take(N)、request body size limit）通常不需要設定化。判斷標準：不同環境需要不同值嗎？可能隨業務需求調整嗎？有多個地方需要保持一致嗎？三個問題都是否，維持 hardcode 比設定化更合適，後者只增加複雜度而沒有對應收益。業務規則上限（查詢最多幾個作物、單次批次最多幾筆）才需要設定化。
 
**關於 Cache Key 常數的架構意義**
Cache Set 和 Cache Invalidation 必須使用相同的 Key 結構。把 prefix 抽成常數讓兩個操作的依賴關係在程式碼層面可見，而不只存在於工程師記憶裡。改了 prefix 的常數，所有用到它的地方（Set、Invalidate、scan）都能被 IDE 的「找所有參考」功能找到，不會有遺漏。
 
**關於 GetValue fallback 的選擇原則**
設定外化不應改變行為，fallback 值必須等於原本的 hardcode 值。選 `0` 或型別預設值作為 fallback 是危險的——設定缺失時行為改變，且通常沒有任何錯誤訊息，只能從結果倒推原因。Fail-Fast（沒有 fallback，缺失即報錯）適合沒有合理預設值的必要設定；帶 fallback 的 GetValue 適合有合理預設值的技術參數。
 
**關於前後端限制的對稱性**
前端的輸入限制應反映後端的真實限制，不應比後端更嚴格（無安全收益，只限制使用者）或更寬鬆（靜默截斷，使用者意圖和實際行為不一致）。靜默截斷是最壞的 UX 情況：使用者以為操作成功，系統實際只做了一部分。
 
 