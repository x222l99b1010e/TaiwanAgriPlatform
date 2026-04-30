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

