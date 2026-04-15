# 開發者學習日誌
### TaiwanAgriPlatform — Developer Learning Log v4

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
 
### 條目 036 — .Where(e => e != null) 在不可能為 null 的情況下是死碼
 
**我做了什麼**
模仿 `WeatherSyncWorker` 的寫法，在 `incoming` 後面加了 `.Where(e => e != null)`，覺得這是防禦性寫法，加了比較安全。
 
**我遇到的問題**
`WeatherSyncWorker` 的 `MapToEntity` 回傳 `WeatherObservation?`（nullable），是因為時間格式解析失敗時需要回傳 null 跳過那筆資料。但 `PestDecadeSummary` 的 `MapToEntity` 回傳的是 `PestDecadeSummary`（非 nullable），裡面沒有任何解析失敗就 return null 的路徑，這個方法在任何情況下都會回傳一個有效的 Entity。
 
`.Where(e => e != null)` 永遠不會過濾掉任何東西——這是死碼。
 
**我怎麼想通的**
「防禦性寫法」要有防禦的對象。`WeatherSyncWorker` 的 null check 防的是「時間格式解析失敗」，是真實存在的失敗路徑。`PestDecadeSummary` 的 MapToEntity 沒有失敗路徑，null check 防的是一個不可能發生的情況，留在程式碼裡只會讓讀程式碼的人困惑「這裡為什麼需要過濾 null？是不是有什麼我不知道的失敗情況？」
 
把死碼刪掉，讓型別系統說話：`MapToEntity` 回傳非 nullable，就代表它不會失敗，不需要外層再做 null check。
 
**我學到的原則**
防禦性寫法的前提是「確實存在需要防禦的情況」。複製其他 Worker 的寫法時，先問「這段 code 在那個 Worker 裡存在的原因是什麼」，再決定這個 Worker 是否需要同樣的防禦。照抄而不理解，等於把別人的業務邏輯搬進不適合的地方。
 
**下次遇到類似情況，我會先想到什麼**
看到 null check，先問「這個方法真的可能回傳 null 嗎」。不可能的話，刪掉。讓型別簽名準確反映方法的行為。
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