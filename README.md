# 🌱 台灣農業開放資料整合平台
### Taiwan Agricultural Open Data Integration Platform

> 把農業部 60 支 API 的孤島資料，串成一個對農民、消費者與研究者都友善的整合平台。

[![CI](https://github.com/x222l99b1010e/TaiwanAgriPlatform/actions/workflows/ci.yml/badge.svg)](https://github.com/x222l99b1010e/TaiwanAgriPlatform/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-10.0_LTS-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Vue](https://img.shields.io/badge/Vue-3.x-42b883?logo=vue.js)](https://vuejs.org/)
[![SQL Server](https://img.shields.io/badge/SQL_Server-2022-CC2927?logo=microsoftsqlserver)](https://www.microsoft.com/sql-server)
[![Docker](https://img.shields.io/badge/Docker-Compose-2496ED?logo=docker)](https://www.docker.com/)
[![License](https://img.shields.io/badge/License-MIT-green)](LICENSE)

---

## 📖 專案背景

台灣農業部每年維護超過 60 支 REST API，涵蓋氣象、行情、農藥、食品追溯、寵物認養、土石流警戒等面向。但這些資料以「孤島」形式存在——一個返鄉青農想判斷「今天這批高麗菜要不要搶收？」，需要同時查看四個不同政府網頁，四套不同介面邏輯。

**本專案的目標是把這些孤島串成陸地**，讓農民、消費者、研究者在同一個介面找到需要的農業資訊，而且是已經被整理、計算、視覺化的版本，而非原始 API 回傳資料。

---

## ✨ 功能模組

### 📊 模組 4：大數據探險 — 天災與菜價關聯分析（後端 + 前台均已完成）

面向研究者，用歷史資料找出天氣事件與農產品批發價格之間的連動規律。

- 作物歷史價格圖 + 7 日移動平均線（Chart.js + computed 轉換層）
- 天災事件垂直線疊加（inline Chart.js plugin，土石流 / 豪雨 / 颱風警戒）
- Chip 多選篩選器（市場類型 / 作物 / 日期區間）
- 休市日標記（排除統計陷阱）— 已完成（32,149 筆休市記錄同步完畢）
- 數據 CSV 匯出（純函式 exportCsv.ts，含 UTF-8 BOM）
- 毛豬行情查詢（多線折線圖、指標切換、市場下拉 computed 動態萃取）
- 家禽行情查詢（W25）：白肉雞／雞蛋／紅羽土雞／黑羽土雞／肉鵝／番鴨／鴨蛋共 17 個指標分組勾選、多線折線圖、非常態報價明細表、CSV 與圖片匯出
- 前台已完成：Vue 3 + Pinia + api/Store/Component 三層架構，Promise.all 並行兩支 API

### 🌤️ 模組 2：智慧青農戰情室（後端已完成，前台已完成）

面向返鄉青農，整合即時氣象、病蟲害警報與市場行情，透過規則引擎主動推播智慧提示。

- 農場氣象面板（依縣市篩選測站，卡片式顯示溫濕度、風速、24h 雨量）
- 雨量趨勢圖（Chart.js 折線圖，支援 3h/6h/12h/24h 指標切換）
- 病蟲害警報牆（依縣市過濾，可展開查看內文與防治處方）
- 旬密度查詢（害蟲旬別密度折線圖，支援城市多線切換）
- 智慧病蟲害提示：規則引擎偵測閾值與事件型規則，主動推送通知
- 通知鈴鐺：未讀紅點 + Dropdown 無限捲動 + 一鍵全部已讀
- 農藥查詢（W24）：輸入成分俗名／英文名查詢許可證狀態、適用作物與安全採收期，即時打農業部 API 不落地

### 🔐 身分驗證 + RBAC + 動態 Navbar（W11~W15 完成）

- ASP.NET Core Identity + JWT（SignInManager + UserManager + JwtSecurityTokenHandler）
- 登入 / 註冊（後端驗證訊息中文翻譯）
- NavModule 自參照樹狀 Entity（頂層 + 子功能兩層，目前 22 筆：4 個頂層模組 + 18 個子功能）
- RoleModulePermission 複合 PK Entity（RoleId × ModuleId，Guest / Admin 各一列）
- NavController `[AllowAnonymous]`：訪客直接取得 Guest 可見模組清單，無需 JWT
- Vue 3 TopNav：頂層 tabs + hover dropdown 子功能渲染

### 👤 使用者個人化（W16~W18 完成）

- 農場偏好設定（農場縣市、農場類型、主要作物 Autocomplete）
- 監看清單：新增 / 刪除 / 顯示最新均價與交易日期
  - 跨模組資料聚合在 Controller 層（Pattern C），`IUserWatchlistService` + `IMarketService` 同時注入
  - `WatchlistEnrichedItemDto` 含靜態偏好 + 動態均價 + 交易日期（nullable）

### 🛒 模組 1：台灣生鮮物價與食安透明網（W21 完成）

面向一般消費者，今日物價查詢 + 食安追溯核查。

- 今日菜價快覽（重點作物均價卡片，TTL 快取跨日自動更新）+ 全站菜價輪播
- 農產品追溯查詢（溯源碼查詢，串接 MOA 追溯 API）
- 農藥殘留違規警示牆（近 N 天不合格名單，days 上限保護 + 檢驗結果篩選 + 分頁）
- 有機農產品驗證查詢（側邊篩選 + 卡片列表 + 穩定分頁排序）

### 🐾 模組 3：毛小孩守護地圖（W22-23 完成）

面向寵物飼主，整合認領養地圖、遺失協尋、合法業者查驗。

**後端（W22 完成）**

- 三支同步 Worker：`AnimalRecognitionSyncWorker`（收容動物，舊制一次性回填 8187 筆 + 新制逐日增量）、
  `PetLoseListSyncWorker`（官方遺失啟事，2018/01/01 起分批平行回填）、
  `LegalSpecificPetSyncWorker`（合法特定寵物業，舊制回填 + 新制 22 縣市迴圈 upsert）
- 五張資料表 + 33 間收容所座標種子資料（`PetDbInitializer`）
- `PetController` 10 支端點：收容所聚合摘要、單筆動物查詢、收容所詳情分頁查詢、官方遺失啟事、
  合法業者查詢、自建遺失啟事 CRUD（含單筆查詢）
- 自建遺失啟事支援登入後新增／編輯／刪除，越權操作一律回 404

**前端（W23 完成）**

- `ShelterMapView`：認領養地圖（Leaflet.js + MarkerCluster，並依收容所座標分組渲染成一所一標記，
  解決同址上千隻動物疊圖成一團的問題；縣市／動物種類篩選；資料由後端聚合端點直接回傳一所一筆摘要，
  不需要前端自行彙整，也沒有筆數上限問題）
- `LostPetsView`：遺失啟事列表與登記表單（登入後可新增／編輯／刪除自己的貼文並顯示對應按鈕，
  **座標由地圖點選取得，不做地理編碼**；照片渲染縮圖、地圖連結、詐騙警語、描述可展開收合）
- `LegalBusinessView`：合法寵物業查詢表格（縣市／動物類型／評鑑等級／營業狀態／業務項目五條件可疊加篩選、
  三種排序、業務項目中文化、一鍵清除篩選）

**詳情頁與個人管理頁（不掛週次，2026-08-16 完成）**

- 三支詳情頁：`LostPetDetailView`（遺失啟事）／`ShelterDetailView`（收容所，datagrid 分頁動物清單）／
  `AnimalDetailView`（單隻收容動物，唯讀）——共通價值是「可分享的固定網址」，路由參數為資料庫自增 id
- `MyLostPetsView`：個人遺失啟事管理頁（`[Authorize]` 路由，只顯示自己張貼的啟事，可直接編輯／刪除）
- `LostPetPostForm` 抽成共用元件，以單一 `post` prop 判斷新增／編輯模式；註冊表單補確認密碼欄位

---

## 🏗️ 系統架構

```
┌─────────────────────────────────────────────────────────────┐
│                    農業部 Open Data API                      │
│              data.moa.gov.tw  (60 支 REST API)              │
└──────────────────────┬──────────────────────────────────────┘
                       │ HTTP / IHttpClientFactory
                       ▼
┌─────────────────────────────────────────────────────────────┐
│                  TaiwanAgri.Worker                          │
│    .NET Worker Service + Serilog                            │
│    17 支 SyncWorker 繼承 ScheduledSyncWorkerBase 排程外殼     │
│    依模組分資料夾（Weather / Market / FoodSafety / Pet）      │
│    落地共用 DbSyncHelper（InsertNewByKey / UpsertByKey）     │
└──────────┬────────────────────────┬─────────────────────────┘
           │ EF Core                │ RabbitMQ
           │ (多 DbContext)         │
     ┌─────┴────────────┐           │
     │ WeatherDbContext │           │
     │ MarketDbContext  │           ▼
     │ CoreDbContext    │   ┌────────────────────────────────┐
     │ (NavModules,     │   │         RabbitMQ               │
     │  RoleModule-     │   │   Exchange: agri.events        │
     │  Permissions,    │   │   RoutingKey: agri.market.*     │
     │  SyncStates)     │   └──────────────┬─────────────────┘
     │ UserDbContext    │                  │ Subscribe
     │ FoodSafetyDb-    │                  │
     │   Context        │                  │
     │ PetDbContext     │                  │
     └─────┬────────────┘                  ▼
           │                ┌──────────────────────────────────────┐
     ┌─────┴──────┐         │          TaiwanAgri.Web              │
     │ SQL Server │         │   ASP.NET Core Web API               │
     │   2022     │         │   ApplicationDbContext               │
     └────────────┘         │   (繼承 IdentityDbContext)            │
                            │   GlobalExceptionMiddleware           │
                            │   MarketController  (8 支端點)        │
                            │   FoodSafetyController (4 支端點)     │
                            │   PetController     (10 支端點)       │
                            │   NavController [AllowAnonymous]      │
                            │   AuthController   (login/register)   │
                            │   ProfileController [Authorize]       │
                            │   WatchlistController [Authorize]     │
                            └──────────┬───────────────────────────┘
                                       │ Cache-Aside
                                       ▼
                      ┌─────────────────────────────────────┐
                      │ Redis TTL 25hr  |  Vue 3 Frontend   │
                      │ StackExchange   |  Vite + Chart.js  │
                      │                 |  TopNav dropdown  │
                      │                 |  Pinia stores     │
                      └─────────────────────────────────────┘
```

### Solution 結構

```
TaiwanAgriPlatform/
├── TaiwanAgriPlatform.slnx
├── docker-compose.yml
├── .env                              # 敏感設定（不進版控）
│
├── TaiwanAgri.Core/                  # 共用 Interface / DTO / Enum / Entity
│   ├── Constants/
│   │   └── MoaApiEndpoints.cs        # 實際串接的 27 支端點路徑集中定義（含 3 支舊制 TransService 通道）
│   ├── Entities/
│   │   ├── ApplicationUser.cs        # 繼承 IdentityUser，供各模組引用
│   │   ├── NavModule.cs              # 自參照樹狀 Entity（ParentId 自參照 FK）
│   │   └── RoleModulePermission.cs   # 複合 PK (RoleId string, ModuleId int)
│   ├── Dtos/
│   │   ├── NavModuleDto.cs           # 頂層模組 DTO（含 List<NavChildDto> Children）
│   │   └── NavChildDto.cs            # 子功能 DTO
│   ├── Services/
│   │   ├── INavService.cs
│   │   └── NavService.cs             # 三段式 RBAC 查詢（RoleManager GUID 解析）
│   ├── Helpers/
│   │   ├── DateHelper.cs             # ParseRocDate / ParseIsoDate 等
│   │   ├── TaiwanTime.cs             # 台灣時區日界（TimeProvider 注入）
│   │   ├── DbSyncHelper.cs           # InsertNewByKeyAsync / UpsertByKeyAsync 共用落地流水線
│   │   ├── EnumMappingHelper.cs      # enum fallback 轉換的統一警告記錄
│   │   └── MoaPagedFetcher.cs        # MOA 分頁抓取共用迴圈
│   ├── Extensions/
│   │   └── MoaApiClientExtensions.cs # AddMoaApiClient() Named Client 共用設定
│   └── Infrastructure/
│       ├── Data/
│       │   └── CoreDbContext.cs      # SyncStates + NavModules + RoleModulePermissions
│       └── DbInitializer.cs          # Seed NavModules（4 頂層 + 18 子功能）+ RoleModulePermissions
│
├── TaiwanAgri.Modules.Weather/       # 模組 2：氣象 + 病蟲害 + 農藥查詢
│   ├── Constants/
│   │   └── PesticideForms.cs         # 農藥劑型代碼 ↔ 中文名對照（W24，5246 張許可證實測校正）
│   └── (WeatherDbContext / Services / Entities / Dtos；PesticideService 即時查詢不落地)
│
├── TaiwanAgri.Modules.Market/        # 模組 4 + 1：行情分析
│   ├── Constants/
│   │   ├── CacheKeys.cs              # Redis Cache Key 前綴常數
│   │   ├── MarketTypeMapping.cs      # MarketType ↔ TcType 對應（單一真相來源）
│   │   └── PoultryMetrics.cs         # 家禽 MetricCode ↔ 中文名對照（W25，17 個指標的單一真相來源）
│   ├── Data/
│   │   └── MarketDbContext.cs        # ConfigureConventions decimal(8,2)
│   ├── Dtos/
│   │   ├── WorkerResponses/          # Worker 從 MOA API 反序列化用 DTO
│   │   └── ApiResponses/             # Service 輸出給前端的 DTO
│   ├── Entities/
│   │   ├── (MarketRestDay / MarketInfo / CropInfo / AgriProductsTrans
│   │   │     / DebrisAlertRecord / PorkTrans / PoultryTrans)
│   │   └── Enums/PriceStatus.cs      # 家禽價格 7 態（W25，全歷史窮舉後定案）
│   ├── Helpers/
│   │   └── PoultryPriceParser.cs     # 家禽價格字串 → PriceStatus + RawValue（W25）
│   └── Services/
│       ├── IMarketService.cs
│       └── MarketService.cs          # 三表 JOIN、GroupBy 聚合、Cache-Aside
│
├── TaiwanAgri.Modules.User/          # 使用者個人化模組（W16 新增）
│   ├── Data/
│   │   └── UserDbContext.cs          # UserFarmProfile / UserFarmCrop / UserWatchlist
│   ├── Dtos/
│   │   ├── ApiRequests/              # AddWatchlistRequestDto / UpsertFarmProfileRequestDto
│   │   └── ApiResponses/             # WatchlistItemDto / WatchlistEnrichedItemDto
│   ├── Entities/
│   │   ├── UserFarmProfile.cs        # string PK (UserId)
│   │   ├── UserFarmCrop.cs           # 物理 FK → UserFarmProfile，跨 DbContext 快照欄位
│   │   └── UserWatchlist.cs          # CropCode/CropName/MarketCode/MarketName/MarketType
│   └── Services/
│       ├── IUserProfileService.cs
│       ├── UserProfileService.cs     # Upsert（全刪全插）
│       ├── IUserWatchlistService.cs
│       └── UserWatchlistService.cs   # AnyAsync 防重複 + RemoveRange 防越權
│
├── TaiwanAgri.Modules.FoodSafety/    # 模組 1：食安模組（W21 完成）
│   ├── Data/
│   │   └── FoodSafetyDbContext.cs    # schema: foodsafety
│   ├── Entities/
│   │   └── (PesticideViolation / OrganicCertification)
│   ├── Dtos/                         # ViolationQueryDto 等統一查詢簽名
│   └── Services/
│       └── (IFoodSafetyService / FoodSafetyService)
│
├── TaiwanAgri.Modules.Pet/           # 模組 3：寵物模組（W22-23 完成）
│   ├── Constants/
│   │   └── LegalPetCounties.cs       # 縣市代碼 ↔ 名稱對照（22 筆，真實資料反查驗證）
│   ├── Data/
│   │   └── PetDbContext.cs           # schema: pet；6 支 Migration；enum 皆 HasConversion<string>()
│   ├── Entities/
│   │   ├── Shelter.cs                # 收容所主檔（ShelterPkId 為 MOA 真實 ID，ValueGeneratedNever）
│   │   ├── ShelterAnimal.cs          # 收容動物（複合 Unique Index (ShelterPkId, AnimalSubId)）
│   │   ├── OfficialLostPetPost.cs    # 官方遺失啟事（唯讀同步）
│   │   ├── LegalSpecificPet.cs       # 合法特定寵物業（upsert，評鑑/營業狀態會變動）
│   │   ├── LostPetPost.cs            # 自建遺失啟事（使用者 CRUD，UserId 邏輯 FK，W23 補 IsOwner）
│   │   └── Enums/                    # 9 個 enum，皆有 fallback 成員供容錯轉換
│   ├── Infrastructure/
│   │   └── PetDbInitializer.cs       # 33 間收容所座標種子（人工查證，非地理編碼）
│   └── Services/
│       ├── IPetService.cs
│       └── PetService.cs             # 地圖聚合查詢（一所一筆）+ 四支分頁/單筆查詢（W23 補篩選排序）+ CRUD 越權防禦
│
├── TaiwanAgri.Worker/                # 入口層：17 支排程 Worker + DI 組裝（依模組分資料夾）
│   ├── ScheduledSyncWorkerBase.cs    # 排程外殼基底（SyncAsync/Interval/LogPrefix + 0–30s 啟動 jitter）
│   ├── Weather/ Market/ FoodSafety/  # 既有 14 支 Worker（Market 含 W25 的 PoultryTransSyncWorker，
│   │                                 #   單一 Worker 服務四條獨立資料流、四組 SyncState）
│   └── Pet/                          # AnimalRecognition / PetLoseList / LegalSpecificPet 三支
│
├── TaiwanAgri.Web/                   # 入口層：Web API + DI 組裝
│   ├── Controllers/
│   │   ├── AuthController.cs         # POST /api/auth/login、POST /api/auth/register
│   │   ├── FoodSafetyController.cs   # 今日菜價 / 追溯 / 違規牆 / 有機驗證（4 支端點）
│   │   ├── MarketController.cs       # 8 支端點（含 /pork、/poultry、/poultry/metrics）
│   │   ├── NavController.cs          # [AllowAnonymous] GET /api/nav/modules
│   │   ├── NotificationController.cs # [Authorize] 通知列表 / 未讀數 / 標記已讀
│   │   ├── PetController.cs          # 寵物模組 10 支端點（GET 公開，CRUD [Authorize]）
│   │   ├── PestController.cs         # 病蟲害警報 / 旬密度 / 害蟲清單
│   │   ├── ProfileController.cs      # [Authorize] GET + PUT /api/profile/farm
│   │   ├── WatchlistController.cs    # [Authorize] GET / POST / DELETE /api/watchlist
│   │   └── WeatherController.cs      # 氣象站 / 雨量
│   ├── Extensions/                   # Modular Monolith 各模組 Extension Methods
│   │   ├── CoreModuleExtensions.cs
│   │   ├── FoodSafetyModuleExtensions.cs
│   │   ├── IdentityExtensions.cs     # JWT Bearer 設定
│   │   ├── InfrastructureExtensions.cs # Redis / CORS / RabbitMQ Consumer
│   │   ├── MarketModuleExtensions.cs
│   │   ├── PetModuleExtensions.cs
│   │   ├── UserModuleExtensions.cs
│   │   └── WeatherModuleExtensions.cs
│   ├── Middlewares/
│   │   └── GlobalExceptionMiddleware.cs # 全域例外攔截 + 標準化 JSON 錯誤回應
│   ├── Services/
│   │   ├── AuthService.cs            # JWT 發行（HMAC-SHA256）
│   │   └── PriceUpdatedConsumer.cs   # RabbitMQ Consumer 骨架
│   └── Program.cs
│
├── TaiwanAgri.Frontend/              # Vue 3 + Vite + TypeScript + Pinia + Vue Router
│   ├── src/
│   │   ├── api/
│   │   │   ├── auth.ts               # /api/auth/login、/api/auth/register
│   │   │   ├── httpBase.ts           # 兩支 client 的共用底座（timeout、401 統一處理、儲存鍵常數）
│   │   │   ├── authClient.ts         # axios instance（自動注入 Bearer token）
│   │   │   ├── cropApi.ts            # 三市場合併作物清單（profile 用）
│   │   │   ├── foodSafety.ts         # 食安四支端點封裝
│   │   │   ├── market.ts             # 模組 4+畜禽 八支端點封裝
│   │   │   ├── nav.ts                # GET /api/nav/modules
│   │   │   ├── pet.ts                # 寵物模組型別定義 + 10 支端點封裝
│   │   │   ├── profile.ts            # GET/PUT /api/profile/farm
│   │   │   ├── watchlist.ts          # GET/POST/DELETE /api/watchlist
│   │   │   └── weather.ts            # 氣象 / 雨量 / 病蟲害 / 通知 封裝
│   │   ├── stores/
│   │   │   ├── authStore.ts          # Pinia：JWT + 使用者資訊（localStorage 持久化）
│   │   │   ├── foodSafety.ts         # Pinia：食安狀態（todayVeg TTL / violations / organicCert）
│   │   │   ├── market.ts             # Pinia：市場行情全域狀態
│   │   │   ├── nav.ts                # Pinia：nav store + loadModules
│   │   │   ├── notification.ts       # Pinia：未讀數 + 通知列表 + 無限捲動
│   │   │   ├── pet.ts                # Pinia：寵物模組全域狀態
│   │   │   ├── profile.ts            # Pinia：農場設定
│   │   │   └── watchlist.ts          # Pinia：監看清單
│   │   ├── composables/
│   │   │   ├── useLatestRequest.ts   # 請求序號防競態（vitest 覆蓋）
│   │   │   ├── usePagination.ts      # 分頁邏輯共用（分頁視窗固定顯示 6 個頁碼，19 個 vitest 測試覆蓋；paginationWindow 純函式已抽出供巢狀多表重用）
│   │   │   └── useCountUp.ts         # 數字滾動動畫（首頁今日三數字）
│   │   ├── components/
│   │   │   ├── TopNav.vue            # 頂層模組 tabs + hover dropdown + 通知鈴鐺
│   │   │   ├── NotificationBell.vue  # 鈴鐺 + 未讀紅點 + Dropdown 無限捲動
│   │   │   ├── CitySelector.vue      # 縣市下拉（補 includeAll，寵物模組共用）
│   │   │   ├── MarketFilter.vue      # 市場類型 Tab + 市場下拉 + 作物 Chip 多選
│   │   │   ├── DateRangePicker.vue   # 日期區間選擇 + 快捷按鈕
│   │   │   ├── PriceChart.vue        # Chart.js 折線圖 + 7 日均線 + 天災垂直線
│   │   │   ├── PagerBar.vue          # 共用分頁列（寵物模組查詢頁與收容所詳情頁共用）
│   │   │   ├── LeafletCoordinatePicker.vue # 地圖點選座標（遺失啟事表單用）
│   │   │   ├── LostPetPostForm.vue   # 遺失啟事新增/編輯共用表單（以單一 post prop 判斷模式）
│   │   │   ├── LostPetPostPhoto.vue  # 遺失啟事照片渲染（外部圖床連結，載入失敗時降級）
│   │   │   ├── VegPriceTicker.vue    # 全站菜價輪播（今日菜價快覽用）
│   │   │   ├── SiteFooter.vue        # 全站頁尾（P3 抽共用，掛在 App.vue 走 sticky footer）
│   │   │   ├── MonthCalendar.vue     # 休市日月曆（P3，取代原按月分組清單）
│   │   │   ├── SeasonMotif.vue       # 節氣母題（首頁節氣牌，只進內容層不進 token）
│   │   │   ├── ui/                  # 五個共用元件（P0/P1 抽出）＋中英並排
│   │   │   │   ├── PageHeader.vue    # 頁首區塊（標題／英文副標／說明）
│   │   │   │   ├── FilterCard.vue    # 查詢條件卡
│   │   │   │   ├── StateBlock.vue    # 載入／空結果／錯誤三種狀態的統一呈現
│   │   │   │   ├── Btn.vue           # 語意性動作按鈕（查詢／重試／送出／匯出）
│   │   │   │   ├── HintBox.vue       # 提示框（info／success／warning 三階語意色）
│   │   │   │   └── Bilingual.vue     # 中英並排排版（四個模組英文定譯全站唯一）
│   │   │   └── layouts/              # 四個頁面樣板（P2.5 抽出，P3 套到 28 頁）
│   │   │       ├── QueryLayout.vue   # 查詢頁樣板（篩選卡 + 結果區 + 分頁）
│   │   │       ├── DetailLayout.vue  # 詳情頁樣板（可分享固定網址）
│   │   │       ├── MapLayout.vue     # 地圖頁樣板（地圖 + 清單上下排）
│   │   │       └── EntryLayout.vue   # 入口頁樣板（深色頁首帶，首頁 hero 共用同一支）
│   │   ├── views/
│   │   │   ├── HomeView.vue          # 全站首頁（P3 新建，`/` 直接掛此頁不再 redirect）
│   │   │   ├── auth/
│   │   │   │   └── LoginView.vue     # 登入 / 註冊 Tab + 錯誤中文翻譯
│   │   │   ├── food-safety/
│   │   │   │   ├── TodayVegView.vue  # 今日菜價快覽
│   │   │   │   ├── TraceabilityView.vue # 農產品追溯查詢
│   │   │   │   ├── ViolationWallView.vue # 農藥違規警示牆
│   │   │   │   └── OrganicCertView.vue   # 有機農產品驗證查詢
│   │   │   ├── market/
│   │   │   │   ├── PricesView.vue    # 作物行情查詢
│   │   │   │   ├── DisastersView.vue # 天災警戒紀錄
│   │   │   │   ├── RestDaysView.vue  # 休市日查詢（月曆呈現）
│   │   │   │   ├── PorkView.vue      # 毛豬行情（多線折線圖 + 指標切換）
│   │   │   │   └── PoultryView.vue   # 家禽行情（17 指標分組勾選 + 非常態明細表）
│   │   │   ├── weather/
│   │   │   │   ├── StationView.vue   # 農場氣象（卡片格）
│   │   │   │   ├── RainfallView.vue  # 雨量趨勢（折線圖 + 明細表格）
│   │   │   │   ├── PestAlertsView.vue # 病蟲害警報牆（真地圖 + 三級燈號 + 可展開）
│   │   │   │   ├── PestDecadeView.vue # 旬密度趨勢（折線圖 + 全選切換）
│   │   │   │   └── PesticideSearchView.vue # 農藥查詢（三層巢狀：成分 → 劑型 → 用途/許可證）
│   │   │   ├── pet/
│   │   │   │   ├── ShelterMapView.vue    # 收容動物地圖（Leaflet + MarkerCluster + 聚合端點一所一標記）
│   │   │   │   ├── ShelterDetailView.vue # 收容所詳情頁（datagrid + 分頁 + 篩選排序）
│   │   │   │   ├── AnimalDetailView.vue  # 單隻收容動物詳情頁（唯讀）
│   │   │   │   ├── LostPetsView.vue      # 遺失啟事列表 + 登記表單（IsOwner 按鈕、地圖點選座標）
│   │   │   │   ├── LostPetDetailView.vue # 遺失啟事詳情頁（可分享的固定網址）
│   │   │   │   ├── MyLostPetsView.vue    # 我的遺失啟事管理頁（requiresAuth，只顯示本人貼文）
│   │   │   │   └── LegalBusinessView.vue # 合法業者查詢（五條件疊加篩選 + 三種排序）
│   │   │   ├── FoodSafetyView.vue    # 食安模組容器（RouterView）
│   │   │   ├── MarketView.vue        # 市場模組容器（RouterView）
│   │   │   ├── WeatherView.vue       # 氣象模組容器（RouterView）
│   │   │   ├── PetView.vue           # 寵物模組容器（RouterView）
│   │   │   ├── ProfileView.vue       # 農場設定（Autocomplete 作物搜尋）
│   │   │   └── WatchlistView.vue     # 監看清單（MarketType Tab + 均價顯示）
│   │   ├── App.vue                   # 兩層 Shell：TopNav + RouterView
│   │   ├── router/index.ts           # 路由守衛（requiresAuth + redirect-after-login）
│   │   ├── main.ts
│   │   ├── constants/
│   │   │   └── chartTheme.ts         # 全站圖表共用主題（P2 收斂 5 份重複色盤成單一來源）
│   │   └── utils/
│   │       ├── exportCsv.ts          # CSV 匯出（UTF-8 BOM）
│   │       ├── leafletIconFix.ts     # Leaflet 預設圖示在 Vite 打包環境的 404 修正
│   │       ├── calendar.ts           # 休市日月曆計算（vitest 覆蓋）
│   │       └── solarTerms.ts         # 二十四節氣計算（vitest 覆蓋）
│   └── vite.config.ts                # server.proxy: /api → https://localhost:7147
│
└── TaiwanAgri.Tests/                 # xUnit + Moq（後端 222 個測試案例）
    ├── Helpers/                       # DateHelper 民國曆邊界值
    ├── Market/                        # Cache Hit / Cache Miss（Mock IDistributedCache）
    ├── User/                          # Watchlist 防重複 / 成功新增（InMemory DB）
    ├── Watchlist/                     # Controller Pattern C 組合（Mock Services）
    ├── FoodSafety/                    # FoodSafetyService 查詢 + 追溯搜尋
    ├── Weather/                       # PesticideService 成分分組 / 劑型對照 / 已廢止與到期判定（W24）
    ├── Pet/                           # PetService 篩選排序 + IsOwner + 越權防禦 + JSON enum 契約 + TimeProvider 時間戳
    ├── Worker/                        # 食安 / 寵物 SyncWorker（MapToEntity 可測化 + InMemory DB）
    └── Web/                           # Controller 層驗證與分頁契約（PagedQueryDto 界限、PagedResult 計算、
                                       #   PetController 授權判斷、MarketController 白名單與截斷標頭）
```

---

## 🛠️ 技術堆疊

| 層次 | 技術 | 版本 | 用途 |
|------|------|------|------|
| 後端框架 | ASP.NET Core Web API | **10.0 LTS** | 主要後端框架 |
| ORM | Entity Framework Core | **10.0** | Code First + Migration |
| 資料庫 | SQL Server | 2022 | Window Functions、時序查詢 |
| 背景排程 | .NET Worker Service | 10.0 | 資料同步排程 |
| 日誌 | Serilog | 10.x | Console + 滾動式檔案日誌（60 天保留） |
| 訊息佇列 | RabbitMQ | 3.x | 非同步事件推播（Topic Exchange） |
| 快取 | Redis + StackExchange.Redis | 7.x | Cache-Aside Pattern（TTL 25hr） |
| 身分驗證 | ASP.NET Core Identity + JWT | 10.0 | RBAC + JWT Bearer |
| 前端 | Vue 3 + Vite + TypeScript | 最新穩定版 | SPA 前台 |
| 狀態管理 | Pinia | 最新穩定版 | 全域狀態管理 |
| 圖表 | Chart.js | 4.x | 折線圖 / 移動平均線 / 天災垂直線 |
| 地圖 | Leaflet + leaflet.markercluster | 1.9.x | 模組 3 認領養地圖（標記聚合 + 地圖點選取座標） |
| 圖示 | Material Design Icons（@mdi/font） | 最新版 | Navbar 模組圖示（CSS class 渲染） |
| 容器化 | Docker Compose | 最新版 | 基礎設施服務（SQL Server / Redis / RabbitMQ） |
| 後端測試 | xUnit + Moq | 最新穩定版 | 單元測試（Service / Controller / Worker 層，222 個案例） |
| 前端測試 | Vitest | 最新穩定版 | composables / utils / 頁面樣板單元測試（`npm test`，6 檔 50 案例） |
| HTTP 彈性 | Polly | 最新版 | HTTP 錯誤自動重試（3 次，間隔 2s） |

---

## 🚀 本機開發環境設定

### 前置需求

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Visual Studio 2022](https://visualstudio.microsoft.com/)（含 ASP.NET 工作負載）
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- [Node.js 22 LTS](https://nodejs.org/)（前端建置用）

> **架構決策說明**：Docker Compose 只負責啟動基礎設施服務（SQL Server、Redis、RabbitMQ）。
> .NET 應用程式在 Windows 本機直接執行，保留 Visual Studio 完整的 F5 中斷點除錯能力。

---

### Step 1：複製專案

```bash
git clone https://github.com/你的帳號/TaiwanAgriPlatform.git
cd TaiwanAgriPlatform
```

### Step 2：建立環境設定檔

在 Solution 根目錄建立 `.env` 檔案（此檔案已加入 `.gitignore`，不會進版控）：

```env
SA_PASSWORD=你的密碼
REDIS_PASSWORD=
MOA_API_KEY=你的api_key
```

同時在 `TaiwanAgri.Worker/appsettings.Development.json` 與 `TaiwanAgri.Web/appsettings.Development.json` 確認連線字串與 JWT 設定：

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost,1433;Database=TaiwanAgriPlatform;User Id=sa;Password=你的密碼;TrustServerCertificate=True",
    "Redis": "localhost:6379"
  },
  "Jwt": {
    "SecretKey": "至少32字元的隨機字串",
    "Issuer": "TaiwanAgriPlatform",
    "Audience": "TaiwanAgriPlatformUsers",
    "ExpiresInDays": "7"
  },
  "RabbitMQ": {
    "HostName": "localhost"
  },
  "MoaApiConfig": {
    "BaseUrl": "https://data.moa.gov.tw/",
    "ApiKey": ""
  }
}
```

### Step 3：啟動基礎設施服務

```bash
docker-compose up -d
```

等待約 30–60 秒，確認所有服務健康：

```bash
docker-compose ps
```

預期看到：

```
NAME                                STATUS
taiwanagriplatform-sqlserver-1      running (healthy)
taiwanagriplatform-redis-1          running (healthy)
taiwanagriplatform-rabbitmq-1       running (healthy)
```

### Step 4：執行 EF Core Migration

本專案採用多 DbContext 架構，**七個 DbContext 各自有獨立的 Migration 目錄，必須分別執行**。

```powershell
# 1. Identity + ApplicationUser（AspNetUsers 等標準表）
Update-Database -Context ApplicationDbContext -StartupProject TaiwanAgri.Web

# 2. 氣象 + 病蟲害模組
Update-Database -Context WeatherDbContext -StartupProject TaiwanAgri.Worker

# 3. 行情模組（MarketRestDays / MarketInfos / CropInfos / AgriProductsTrans / PorkTrans / PoultryTrans / DebrisAlertRecords）
Update-Database -Context MarketDbContext -StartupProject TaiwanAgri.Worker

# 4. 跨模組基礎設施（SyncStates + NavModules + RoleModulePermissions）
Update-Database -Context CoreDbContext -StartupProject TaiwanAgri.Worker

# 5. 使用者個人化（UserFarmProfiles / UserFarmCrops / UserWatchlists）
Update-Database -Context UserDbContext -StartupProject TaiwanAgri.Web

# 6. 食安模組（PesticideViolations / OrganicCertifications）
Update-Database -Context FoodSafetyDbContext -StartupProject TaiwanAgri.Worker

# 7. 寵物模組（Shelters / ShelterAnimals / OfficialLostPetPosts / LegalSpecificPets / LostPetPosts）
Update-Database -Context PetDbContext -StartupProject TaiwanAgri.Web
```

Migration 執行完成後，`core.NavModules`、`core.RoleModulePermissions` 與 `pet.Shelters`（33 間收容所座標）會由 `DbInitializer.SeedAsync` / `PetDbInitializer.SeedAsync` 在 **`TaiwanAgri.Web` 啟動時**自動寫入。

> **注意**：`Add-Migration` 也必須明確指定 `-Context` 和 `-Project` 參數，
> 例如：`Add-Migration InitialUserSchema -Context UserDbContext -Project TaiwanAgri.Modules.User`
>
> 也可以用 dotnet CLI（在 repo 根目錄執行）：
> `dotnet ef migrations add <Name> --project TaiwanAgri.Modules.Pet --startup-project TaiwanAgri.Web --context PetDbContext`

### Step 5：先啟動 Web API（種子資料必須先寫入）

在 Visual Studio 將啟動專案設定為 `TaiwanAgri.Web`，按 F5 啟動。預設監聽 `https://localhost:7147`。

`Program.cs` 在啟動時會自動執行 `DbInitializer.SeedAsync` 與 `PetDbInitializer.SeedAsync`（皆有 `AnyAsync` 冪等保護，重複執行不重複插入）。

> **⚠ 順序不能顛倒**：`TaiwanAgri.Worker` **不會**呼叫任何 Initializer，種子資料只在 Web 啟動時寫入。全新環境若先跑 Worker，`pet.Shelters` 會是空的，`ShelterAnimal` 的外鍵約束會擋下所有寫入。

### Step 6：啟動 Worker

在 Visual Studio 將啟動專案設定為 `TaiwanAgri.Worker`，按 F5 啟動。Worker 會開始同步農業部 API 資料，初次同步（尤其寵物模組的歷史回填）較耗時，可在 Console 或 `TaiwanAgri.Worker/logs/` 觀察 Serilog 輸出確認進度。

### Step 7：啟動前台開發伺服器

```bash
cd TaiwanAgri.Frontend
npm install
npm run dev
# 前台伺服器啟動於 http://localhost:5173
# /api/* 請求透過 Vite Proxy 自動轉發至 https://localhost:7147
```

### Step 8：執行測試

```bash
# 後端（xUnit + Moq，共 222 個測試案例）
cd TaiwanAgri.Tests
dotnet test

# 前端（Vitest，共 50 個測試案例）
cd TaiwanAgri.Frontend
npm test
```

後端涵蓋 Helpers / Market（含 W25 家禽價格解析 27 個 + 查詢層 7 個）/ User / Watchlist / FoodSafety / Weather / Pet / Worker 八個面向；前端 6 個測試檔共 50 個案例，涵蓋 `useLatestRequest`（請求序號防競態）、`exportCsv`（CSV 匯出純函式）、`usePagination`（分頁視窗計算與跳頁邊界，19 個）、`layouts`（四個頁面樣板契約，14 個）、`calendar`（休市月曆）與 `solarTerms`（二十四節氣）。CI（GitHub Actions）在每次 push / PR 自動執行兩個 job：`build-and-test`（後端 restore → build → test）與 `frontend`（`npm ci` → lint → vitest → build），前後端測試皆在 CI 環境執行。

---

## 🗄️ 資料庫設計概覽

本專案資料表由七個 DbContext 分工管理：

**ApplicationDbContext**（`TaiwanAgri.Web`，schema: dbo）：
`AspNetUsers` | `AspNetRoles` | 其他 Identity 標準表

**WeatherDbContext**（`TaiwanAgri.Modules.Weather`，schema: weather）：
`WeatherObservations` | `RainfallStations` | `RainfallObservations` | `PestAlerts` | `PestAlertCities` | `PestAlertCrops` | `PestDecadeSummaries` | `PestRuleConfigs` | `UserNotifications`

**MarketDbContext**（`TaiwanAgri.Modules.Market`，schema: market）：
`MarketRestDays` | `MarketInfos` | `CropInfos` | `AgriProductsTrans` | `PorkTrans` | `PoultryTrans`（長表設計，見下方說明）| `DebrisAlertRecords`

**CoreDbContext**（`TaiwanAgri.Core`，schema: core）：
`SyncStates`（增量同步進度追蹤）| `NavModules`（自參照樹狀導覽主檔）| `RoleModulePermissions`（角色模組可見度，複合 PK）

**UserDbContext**（`TaiwanAgri.Modules.User`，schema: dbo）：
`UserFarmProfiles` | `UserFarmCrops` | `UserWatchlists`

**FoodSafetyDbContext**（`TaiwanAgri.Modules.FoodSafety`，schema: foodsafety）：
`PesticideViolations` | `OrganicCertifications`

**PetDbContext**（`TaiwanAgri.Modules.Pet`，schema: pet）：
`Shelters`（收容所主檔，PK 為 MOA 真實 ID）| `ShelterAnimals`（收容動物，實體 FK → Shelters，`OnDelete: Restrict`）| `OfficialLostPetPosts` | `LegalSpecificPets` | `LostPetPosts`（使用者自建）

> **跨 DbContext FK 說明**：`RoleModulePermissions.RoleId` 指向 `AspNetRoles.Id`（GUID），以 `nvarchar(450)` 邏輯 FK 處理，無物理 FOREIGN KEY CONSTRAINT。`UserFarmCrop.CropName` 為跨 DbContext 快照欄位，寫入時從 MarketDbContext 複製，不做即時 JOIN。`LostPetPost.UserId` 同樣是跨 DbContext 邏輯 FK（指向 `AspNetUsers`，無導覽屬性）。
>
> **enum 儲存慣例**：`PetDbContext` 的所有 enum 屬性皆設 `HasConversion<string>()`，資料庫存可讀字串而非數字——好處是新增列舉成員不需要 Migration，也讓直接查 DB 時看得懂。
>
> **`PoultryTrans` 長表設計（W25）**：欄位固定為 `Id`（代理鍵 PK）/ `TransDate` / `MetricCode` / `Price`（`decimal?`）/ `PriceStatus` / `RawValue` / `SyncedAt`，`(TransDate, MetricCode)` 為 Unique Index 而非 PK。與 `PorkTrans` 的寬表刻意不同：家禽四支來源 API 的欄位集分別是 5/6/2/4 欄且互不相同，長表讓日後新增第五支來源不必改 Schema。價格欄位在原始 API 是字串且含 8 種非數值型態（休市／未報價／議價／區間報價等，佔全歷史 14.1%），因此拆成 `Price` + 7 態 `PriceStatus` + `RawValue` 原文兜底——`PriceStatus` 為 `Normal` 時 `RawValue` 為 null，反之存原始字串。

完整資料表設計請參考 SA/SD 文件 `TaiwanAgriPlatform_SA_SD_V35_3.docx`（存放於專案文件資料夾，不進版控）。

---

## 🌐 API 端點摘要

### 身分驗證

| Method | URL | 說明 | 認證 |
|--------|-----|------|------|
| POST | `/api/auth/login` | 登入，回傳 JWT token | 不需要 |
| POST | `/api/auth/register` | 註冊並自動指派 Guest 角色 | 不需要 |

### 導覽 RBAC

| Method | URL | 說明 | 認證 |
|--------|-----|------|------|
| GET | `/api/nav/modules` | 依登入狀態回傳可見模組清單（巢狀 DTO） | `[AllowAnonymous]` |

### 模組 4 — 天災與菜價關聯分析

| Method | URL | 說明 | 認證 |
|--------|-----|------|------|
| GET | `/api/market/crops?marketType=Veg` | 作物清單 | 不需要 |
| GET | `/api/market/markets?marketType=Veg` | 市場清單 | 不需要 |
| GET | `/api/market/prices` | 作物歷史價格走勢（GroupBy 聚合 + Cache-Aside） | 不需要 |
| GET | `/api/market/disasters` | 天災警戒事件清單（GroupBy 去重；結果達筆數上限時回應帶 `X-Result-Truncated` 標頭） | 不需要 |
| GET | `/api/market/rest-days` | 市場休市日清單 | 不需要 |
| GET | `/api/market/pork` | 毛豬行情（依日期區間 + 市場篩選） | 不需要 |
| GET | `/api/market/poultry` | 家禽行情（依日期區間 + 指標篩選，長表一列一資料點） | 不需要 |
| GET | `/api/market/poultry/metrics` | 家禽 17 個指標代碼與中文名對照 | 不需要 |

#### GET /api/market/poultry 參數

| 參數 | 型別 | 必填 | 說明 |
|------|------|------|------|
| metricCodes | string[] | — | 不傳則回傳全部 17 個指標（`&metricCodes=Egg_Producer&metricCodes=...`）；代碼須通過 `PoultryMetrics` 白名單，無效代碼回 400 而非安靜回空陣列 |
| startDate | string | — | yyyy-MM-dd，預設今天往前 365 天 |
| endDate | string | — | yyyy-MM-dd，預設今天 |

> 回應**刻意完整回傳非 `Normal` 的資料點**（`Price` 為 null、`PriceStatus` 標明原因、`RawValue` 保留原文），不在查詢層過濾——濾掉的話前端無法分辨「這個指標本來就少報價」（紅羽土雞南區兩條線超過三分之一天數未報價、雞蛋產地價 94% 無數值）與「同步壞掉了」。

#### GET /api/market/prices 參數

| 參數 | 型別 | 必填 | 說明 |
|------|------|------|------|
| marketType | string | ✅ | Veg / Fruit / Flower |
| cropCodes | string[] | ✅ | 最多 5 個（`&cropCodes=E1&cropCodes=E2`） |
| marketCode | string | — | 不傳則回傳全台均價 |
| startDate | string | — | yyyy-MM-dd，預設今天往前 365 天 |
| endDate | string | — | yyyy-MM-dd，預設今天 |

### 模組 1 — 生鮮物價與食安透明網

| Method | URL | 說明 | 認證 |
|--------|-----|------|------|
| GET | `/api/FoodSafety/today-veg-prices` | 今日菜價快覽（重點作物最新均價） | 不需要 |
| GET | `/api/FoodSafety/traceability?traceCode=...` | 農產品追溯查詢 | 不需要 |
| GET | `/api/FoodSafety/violations` | 農藥殘留違規名單（days 上限保護 + 檢驗結果篩選 + 分頁） | 不需要 |
| GET | `/api/FoodSafety/organic-certifications` | 有機農產品驗證查詢（篩選 + 穩定分頁排序） | 不需要 |

### 模組 2 — 智慧青農戰情室

| Method | URL | 說明 | 認證 |
|--------|-----|------|------|
| GET | `/api/Weather/stations?cityName=臺北市` | 氣象站即時資料 | 不需要 |
| GET | `/api/Weather/rainfall?cityName=臺北市` | 雨量趨勢 | 不需要 |
| GET | `/api/Pest/alerts?cityName=臺北市&page=1` | 病蟲害警報（分頁 20 筆） | 不需要 |
| GET | `/api/Pest/pest-names` | 所有害蟲名稱清單 | 不需要 |
| GET | `/api/Pest/decade-density?pestName=東方果實蠅` | 旬密度歷史資料 | 不需要 |
| GET | `/api/Weather/pesticides?keyword=亞滅培` | 農藥查詢（中英文名擇一或併用，含 includeRevoked 參數） | 不需要 |
| GET | `/api/Notification/list?page=1` | 使用者通知列表 | **需要 JWT** |
| GET | `/api/Notification/unread-count` | 未讀通知數 | **需要 JWT** |
| PATCH | `/api/Notification/{id}/read` | 標記單筆已讀 | **需要 JWT** |
| PATCH | `/api/Notification/read-all` | 一次標記全部已讀（取代前端逐筆送 N 個請求） | **需要 JWT** |

### 模組 3 — 毛小孩守護地圖

| Method | URL | 說明 | 認證 |
|--------|-----|------|------|
| GET | `/api/pet/shelters/summary?county=&kind=` | 收容動物地圖聚合查詢（**一間收容所一筆**，含 Dog/Cat/Other 拆分計數） | 不需要 |
| GET | `/api/pet/shelters/{shelterId}/animals?kind=&sex=&sortBy=&sortDescending=&page=&pageSize=` | 收容所詳情頁：單一收容所的分頁動物清單 | 不需要 |
| GET | `/api/pet/shelter-animals/{id}` | 單筆動物詳情 | 不需要 |
| GET | `/api/pet/official-lost-posts?category=&sex=&sortBy=&sortDescending=&page=&pageSize=` | 官方遺失啟事（分頁，無座標） | 不需要 |
| GET | `/api/pet/legal-specific-pets?county=&animalType=&rankGrade=&stateFlag=&businessItem=&sortBy=&sortDescending=&page=&pageSize=` | 合法特定寵物業查詢（分頁，無座標，五條件可疊加） | 不需要 |
| GET | `/api/pet/lost-pet-posts?status=&county=&sortBy=&sortDescending=&page=&pageSize=` | 自建遺失啟事列表（分頁，含座標） | 不需要（登入時額外回傳 `IsOwner`） |
| GET | `/api/pet/lost-pet-posts/{id}` | 單筆自建遺失啟事 | 不需要（登入時額外回傳 `IsOwner`） |
| POST | `/api/pet/lost-pet-posts` | 張貼遺失啟事（Phone/Email 至少填一項） | **需要 JWT** |
| PUT | `/api/pet/lost-pet-posts/{id}` | 修改自己的啟事（非本人回 404） | **需要 JWT** |
| DELETE | `/api/pet/lost-pet-posts/{id}` | 刪除自己的啟事（非本人回 404） | **需要 JWT** |

> **`shelters/summary` 為何一間收容所一筆**：全台上萬筆動物其實只落在約 30 個收容所座標上（同一間
> 收容所的所有動物共用該收容所的經緯度）。舊版直接回傳逐隻動物清單（不分頁＋防禦性 `Take(3000)`
> 上限＋`X-Result-Truncated` 截斷標頭），資料形狀與地圖標記需求不合，撞到上限只是把問題延後發作。
> 改成後端先依 `(ShelterPkId, Kind)` 分組計數、reshape 成一所一筆摘要後，結果集本身只有約 30 筆，
> 不會被截斷，也不需要分頁或上限機制。

### 使用者個人化

| Method | URL | 說明 | 認證 |
|--------|-----|------|------|
| GET | `/api/profile/farm` | 取得農場設定（無設定回傳 200 + null） | **需要 JWT** |
| PUT | `/api/profile/farm` | 儲存農場設定（Upsert，回傳 204） | **需要 JWT** |
| GET | `/api/watchlist` | 監看清單（含最新均價 + 交易日期） | **需要 JWT** |
| POST | `/api/watchlist` | 新增監看項目（重複回傳 409） | **需要 JWT** |
| DELETE | `/api/watchlist?ids=1&ids=2` | 批量刪除監看項目（上限 50 筆） | **需要 JWT** |

---

## 🔌 農業部 API 說明

本專案串接 [農業部開放資料平台](https://data.moa.gov.tw) 共 60 支 API。

- **免費可用（53 支）**：涵蓋所有核心功能，MVP 開發不受限制
- **Swagger 介面標示 api_key 參數（7 支）**：`SheepQuotation`、`WashedEggsTraceabilityType`、`LegalSpecificPet`、`PetFood`、`FeedAndAdditiveInputCertificate`、`FeedManagementInfo`、`MothSpecimenData`。實測這 7 支**同樣可以未登入呼叫**——api_key 的真正作用是分頁權限（見下方限制說明），不是存取權限

> **重要限制**：免費帳號分頁 API 只回傳第一頁資料（每頁最多 1,000 筆）。程式碼中保留分頁迴圈，當 API 回傳 `RS: "ERROR"` 時會優雅地 `break`，不影響正常運作。

本專案**實際串接的 27 支端點**路徑統一定義在：`TaiwanAgri.Core/Constants/MoaApiEndpoints.cs`
（其中 3 支走農業部舊制 `TransService` 通道：土石流警戒、收容動物一次性回填、合法特定寵物業一次性回填）。
探勘後判定不採用的候選端點，在該檔以註解記錄排除理由，避免日後重複探勘。

---

## ⏱️ 開發進度

| Sprint | 階段 | 內容摘要 | 狀態 |
|--------|------|----------|------|
| W1–2 | 基礎建設 | Docker Compose + 8 Project Solution + 第一個 Migration | ✅ 完成 |
| W3–4 | 模組 2 資料收集（一） | WeatherSyncWorker（分頁、HashSet 防重複、30 天自動清除） | ✅ 完成 |
| W5–6 上半 | 模組 2 資料收集（二） | Identity Migration 提前；RainfallStation + Rainfall + PestDecade SyncWorker | ✅ 完成 |
| W5–6 下半 | 模組 2 規則引擎 | PestRuleConfig + UserNotifications + PestRuleEngine.EvaluateAsync() | ✅ 完成 |
| W7–8 上半 | 模組 4 後端（基礎） | MarketDbContext Schema 分離；MarketInfo surrogate PK 重構；MarketRestDaySyncWorker；CropMarketSyncWorker | ✅ 完成 |
| W7–8 中半 | 模組 4 後端（核心同步） | CoreDbContext + SyncState；DateHelper ROC 日期雙向轉換；AgriProductsTransSyncWorker | ✅ 完成 |
| W7–8 下半 | 模組 4 後端（優化） | Task.WhenAll 併發；DebrisAlertRecordSyncWorker；PorkTransSyncWorker；ConfigureConventions decimal(8,2) | ✅ 完成 |
| W9–10 前半 | 模組 4 查詢層 | TaiwanAgri.Web 改造；IMarketService + MarketService 五支查詢；MarketController（PR #020） | ✅ 完成 |
| W9–10 後半 | 模組 4 前台 | Vue 3：Chart.js 折線圖 + 7 日均線；天災垂直線；Chip 多選；CSV 匯出（PR #021） | ✅ 完成 |
| W11 | RBAC 骨架 + 動態 Navbar | NavModule 自參照；RoleModulePermission 複合 PK；DbInitializer；NavService；TopNav hover dropdown（PR #022） | ✅ 完成 |
| W12 | 技術債補丁 | MarketInfos MarketType 索引；AgriProductsTransSyncWorker 並發控制；PriceChart.vue options；Serilog 檔案日誌（PR #023–025） | ✅ 完成 |
| W13–14 | Redis + RabbitMQ + 模組 2 前台 | GetPricesAsync Cache-Aside（TTL 25hr）；RabbitMQ Publisher/Consumer 骨架；WeatherService / PestService 查詢層；Vue 3 天氣面板四頁；前端淺色主題全站重設計；毛豬行情前後端（PR #026–029） | ✅ 完成 |
| W15 | JWT 身分驗證 | ASP.NET Core Identity + JWT；AuthService；LoginView.vue；authClient.ts axios interceptor（PR #033） | ✅ 完成 |
| W16 | 農場偏好設定 | TaiwanAgri.Modules.User；UserFarmProfile / UserFarmCrop；Upsert 策略；ProfileController；ProfileView.vue（PR #034） | ✅ 完成 |
| W17 | 監看清單 | UserWatchlist Entity；IUserWatchlistService（防重複 + 409）；WatchlistController 批量刪除；WatchlistView.vue（PR #035–037） | ✅ 完成 |
| W18 | 監看清單行情整合 | WatchlistEnrichedItemDto 跨模組聚合；Controller Pattern C；UserWatchlist 補 MarketType；均價顯示（PR #038） | ✅ 完成 |
| W19 | 測試 Sprint | xUnit + Moq；MarketServiceCacheTests / UserWatchlistServiceTests / WatchlistControllerTests；共 12 個測試全數綠燈（PR #039） | ✅ 完成 |
| W20 | DevOps | GitHub Actions CI（restore/build/test + badge，W20a）；GlobalExceptionMiddleware 全域例外攔截 + 標準化 JSON 錯誤回應（W20b）。全域搜尋與 Docker 打包延後至功能模組全部完成後統一處理 | ✅ 完成 |
| W21 | 模組 1（食安） | FoodSafetyDbContext + 模組骨架；今日菜價快覽 + 全站菜價輪播（W21a）；農產品追溯查詢（W21b）；農藥違規警示牆 + PesticideViolationSyncWorker（W21c）；有機農產品驗證查詢 + OrganicCertificationSyncWorker（W21d）（GitHub PR #5–#8） | ✅ 完成 |
| —（不掛週次） | Code Review 修正批次 | TimeProvider 時鐘注入 + 台灣時區日界；ScheduledSyncWorkerBase / DbSyncHelper / MoaPagedFetcher 抽共用；Watchlist N+1 批次化；分頁排序穩定性；(CropCode, MarketCode, TransDate DESC) 索引；前端 vitest 導入 + useLatestRequest/usePagination（PR #045–046，GitHub PR #10–#12） | ✅ 完成 |
| W22–23 | 模組 3（寵物地圖） | **後端**：三支同步 Worker（收容動物回填 8187 筆 + 官方遺失啟事 + 合法業者 upsert）、五張資料表、33 間收容所座標種子、PetController 7 支端點、49→65 測試（PR #048）。**前端**：`ShelterMapView`（Leaflet + MarkerCluster，並依收容所座標分組渲染一所一標記）、`LostPetsView`（遺失啟事 CRUD、`IsOwner` 按鈕、地圖點選座標）、`LegalBusinessView`（五條件疊加篩選 + 三種排序 + 業務項目中文化），65→75 後端測試、8→27 前端測試（PR #049）。前端串接期間回頭修正後端四處介面不一致（`IsOwner`／`[FromBody]` enum／篩選排序／標記上限與截斷標頭）（原規劃於 W17-18，因模組 1/2/4 與身分驗證系列功能優先處理而順延） | ✅ 完成 |
| —（不掛週次） | 模組 3 收尾與技術債 | 三支詳情頁 + 我的遺失啟事管理頁 + 註冊確認密碼、`LostPetPostForm` 抽共用元件（PR #050，GitHub PR #21）；前端 lint 16 個錯誤修正 + CI 補 `frontend` job + `MoaApiClient` Timeout 修正（PR #051，GitHub PR #22）；收容動物地圖改用聚合端點 `GET /api/pet/shelters/summary`，移除 3000 筆上限與截斷標頭整套機制（PR #052，GitHub PR #24）。75→84 測試 | ✅ 完成 |
| W24 | 模組 2（農藥查詢） | GET /api/Weather/pesticides：中英文成分名查詢（可併用，英文名字元白名單防護）；即時打農業部 PesticideDataQueryType，不落地；三層回應（成分 → 劑型 → 用途/許可證），使用範圍依 (成分,含量,劑型) 去重後並行抓取；PesticideForms 劑型代碼對照表（5246 張許可證實測校正）；PesticideSearchView.vue 前端畫面；NavModules 補入口。84→151 測試（GitHub PR #26／#27） | ✅ 完成 |
| W25 | 模組 4（家禽行情） | 四支來源 API（白肉雞/雞蛋、紅羽土雞、黑羽土雞、肉鵝/番鴨/鴨蛋）串接完成畜禽面板的家禽半邊。`PoultryTrans` 長表設計取代寬表；價格拆成 7 態 `PriceStatus` + `RawValue`（全歷史窮舉 8 種非數值字串後定案）；四組獨立 `SyncState`（四支歷史起點不一致：2010/10/07 與 2014/04/01）；逐年切塊抓取讓回填與日常增量共用同一段程式碼。Worker 實跑回填 88,236 列，七態分布與探勘預估逐一吻合。`PoultryView.vue` 17 指標分組勾選 + 斷線呈現 + 完整度徽章 + 非常態明細表。151→185 測試 | ✅ 完成 |
| —（不掛週次） | 全專案 Code Review | 四個功能模組首次全部完成後的跨模組一致性盤點（後端 294 個 `.cs` 檔／26,264 行＋前端全案）。核心結論：技術債形態不是「寫錯」，而是「共用抽象建立後沒有回頭替換掉原地舊寫法」，橫跨 Worker 層、查詢層、前端與模組邊界共六例。**批次 B**（內部慣例、行為不變）：`ScheduledSyncWorkerBase` 就緒等待納入例外保護；蔬果/毛豬 Worker 日界改用 `TaiwanTime`；三支 Worker 的 `SyncKey` 抽常數；前端公開端點抽出共用 `apiClient`（GitHub PR #32）。**批次 A**（動契約與 UI）：病蟲害警報改用 `PagedResult` 分頁契約與共用 `PagerBar`；追溯碼查詢自 `FoodSafetyService` 拆出 `TraceabilityService`（GitHub PR #33）。185 測試全過（不新增案例） | ✅ 完成 |
| —（不掛週次） | 前端視覺設計 | 四個功能模組全部完成後的第一次全站視覺統一與設計品質提升，分五階推進。**P0/P1**：建立 design token 系統（`base.css` 從 37 行、14 個色變數擴充為間距／字級／行高／字重／圓角／陰影／容器寬／動效八組尺度＋三組色階＋`prefers-reduced-motion`），容器寬與頁面留白統一，抽出 `PageHeader`／`FilterCard`／`StateBlock`／`Btn`／`HintBox` 五個共用元件，MDI 的 CSS 字符規則改建置期裁切（7447→85 條），CSS bundle 477→126 kB（GitHub PR #35，內部 #057）。**P2**：全站色值與尺度收斂到 token、5 份重複的圖表色盤收成 `chartTheme.ts` 單一來源、拆除相容層舊變數（GitHub PR #36，內部 #058）。**P2.5**：定調「秋田」設計方向（主色秧苗綠、柿橙降第二強調、加入節氣、中英並排），新增 token 第二層 semantic，抽出 `QueryLayout`／`DetailLayout`／`MapLayout`／`EntryLayout` 四個頁面樣板＋`Bilingual`（GitHub PR #38，內部 #059）。**P3**：四模組 28 頁套樣板、**新首頁上線（`/` 不再 redirect）**、病蟲害警報改真 Leaflet 地圖＋三級燈號、休市日改月曆、雨量／旬報／農藥核准用途收進分頁 data grid、語意色改暖色域＋callout 改「色條＋圖示徽章」、抽出全站頁尾 `SiteFooter`、首頁四模組交錯列＋hover 光點動畫、氣象卡片日內溫度量尺、今日菜價 bento，舊色階整組刪除（grep 回傳 0）（GitHub PR #39，內部 #060）。P0–P2 由 release PR #37、P2.5＋P3 由 release PR #40 進 main，整輪已全部同步；前端測試 27→50、CSS gzip 收於 26.28 kB | ✅ 完成 |
| —（不掛週次） | 全專案 Code Review 第二輪 | 前端視覺設計輪與註解衛生批次收工後的第二次跨模組盤點，**首次把「先跑 build 與 lint 記基線」列為第一步**（第一輪的教訓），而這一步抓到兩個純讀檔看不到的問題。核心結論比第一輪更精確：技術債的形態是**慣例按時間順序長出來、新慣例從不回頭套用到舊程式碼**（`CancellationToken` 39 個介面方法只有 1 個有、`AsNoTracking` 全案 1 處、「截斷要給訊號」只存在寵物模組）。**修正**：CancellationToken 補到 42/42、`AsNoTracking` 1→6 處、建置警告 24→0、分頁界限與 `PagedResult` 收斂成共用抽象（原本分別重複 6 處與 7 處）、天災截斷加訊號並在前端顯示、Redis 反序列化失敗改為降級而非癱瘓 25 小時、農藥查詢第二層並行直接設限、前端 HTTP 層補 timeout 與 401 統一處理、通知不再靜默失敗。**修掉一個使用者可見的 bug**：監看清單未指定市場的項目永遠顯示不出價格（SQL 的 IN 不匹配 NULL）。**效能**：路由改動態載入＋字型二進位真正子集化，首屏載入 1254.9→264.3 kB（−79%）。**測試 185→222**，Controller 層覆蓋 1/10→3/10。CI 的 lint 改唯讀（原本 `--fix` 會讓違規被吃掉還顯示綠燈），並新增「未定義 CSS 變數」檢查 | ✅ 完成 |

---

## 🧠 關鍵架構決策記錄

**多 DbContext 架構（Modular Monolith）**
每個業務模組有獨立的 DbContext，連線字串設定與啟動由入口層統一組裝，模組本身不感知執行環境。`CoreDbContext` 管理跨模組共用的基礎設施（`SyncStates`、`NavModules`、`RoleModulePermissions`）。

**跨模組資料組合：Controller 層 Pattern C**
`WatchlistController` 同時注入 `IUserWatchlistService`（User 模組）與 `IMarketService`（Market 模組），在 Controller 層做 foreach 聚合，而非把跨模組邏輯下沉到 Service 層。這讓各模組 Service 保持獨立，可分別測試，組合責任明確落在入口層。

**BackgroundService 生命週期管理**
SyncWorker 繼承 `BackgroundService`，被 DI 容器以 Singleton 管理；`DbContext` 是 Scoped。每次同步任務執行時透過 `IServiceScopeFactory.CreateScope()` 建立新 Scope，用完即釋放，避免 Change Tracker 持續累積狀態。

**NavModule 自參照設計**
選擇單表自參照而非兩張表，自參照 FK 使用 `OnDelete Restrict`。`Icon` 欄位存 MDI CSS class 字串，更換圖示只改 DB 不需部署前端。`RoleModulePermission.RoleId` 存 IdentityRole 的 GUID（非 Role Name），訪客流程透過 `RoleManager.FindByNameAsync("Guest")` 做名稱→GUID 轉換。

**Redis Cache-Aside（GetPricesAsync）**
Cache Key 格式：`market:prices:{marketType}:{sortedCrops}:{marketCode}:{startDate}:{endDate}`。cropCodes 排序後 Join 確保任意排列命中同一 slot。TTL 設定 25 小時（農業部資料每天更新一次，跨天不提早過期）。Cache Key 前綴抽成 `CacheKeys.MarketPricesPrefix` 常數，為後續 RabbitMQ Cache Invalidation 預留介面。

**SyncState 模式取代 MAX(TransDate)**
全市場休市日當天，`AgriProductsTrans` 表沒有記錄寫入，MAX 值卡死。改用 `SyncStates` 獨立追蹤「已完成同步的最後一天」，不管那天有無資料寫入，日期都往前推進。

**Task.WhenAll 併發 API 請求**
`AgriProductsTransSyncWorker` 初版串行 4,500 次 HTTP 請求效能極差。改用 `Task.WhenAll` 讓同一天的所有市場 API 同時發出，`SemaphoreSlim(3)` 控制最大並發數。Task 只負責 HTTP，所有有狀態操作集中在主執行緒依序執行，規避執行緒安全問題。

**UserWatchlist MarketType 快照欄位**
最初 UserWatchlist 未存 MarketType，導致查價時只能猜測作物類別。W18 補入 MarketType 欄位，修正資料模型缺漏。這個設計演變完整記錄在 SA/SD 文件中。

**DbInitializer 與 Migration 職責分離**
Schema 歸 Migration，Data 歸 DbInitializer。`HasData` 的修改需要新增 Migration，長期維護下遷移歷史可讀性差。DbInitializer 以 `AnyAsync()` 做冪等保護，並在啟動時呼叫 `GetPendingMigrationsAsync()` 前置防護。

**DisasterResponseDto：GroupBy 去重 + AffectedCounties 彙整**
同一天災在 DB 對應多筆記錄（每縣市一筆）。Service 層以 `(DisasterName, AlertDate)` GroupBy 後，將同群的 County 彙整為 `AffectedCounties`（`Distinct().OrderBy()` 的 `List<string>`）。

**DebrisAlertRecord：HasFilter(null) 解決 nullable UNIQUE Index 失效**
`DebrisNo` 和 `LandslideID` 互斥為 null。EF Core 預設對 nullable 欄位的 UNIQUE Index 加 `WHERE ... IS NOT NULL`，此條件讓 UNIQUE Index 形同虛設。`.HasFilter(null)` 覆蓋預設行為。

**前端三層架構：api / Store / Component**
`api/` 負責 HTTP 封裝；`stores/`（Pinia）負責全域狀態；Vue 元件負責 UI 渲染。`authClient.ts` 以 axios interceptor 在每次請求自動注入 Bearer token。平鋪 prices → Chart.js datasets 的格式轉換放在 `PriceChart.vue` 的 `computed()`，純顯示格式轉換，不屬於業務邏輯。

**GlobalExceptionMiddleware 標準化錯誤回應**
所有未攔截例外統一在 Middleware 層轉為標準化 JSON 錯誤格式，Controller 不再散落 try-catch。開發環境回傳詳細訊息、正式環境只回傳通用訊息，避免內部細節外洩。

**台灣時區日界 + TimeProvider 時鐘注入**
「今天」的定義統一為台灣時區（`TaiwanTime.Today(TimeProvider)`），查詢服務的時鐘一律走 `TimeProvider` 注入而非直接呼叫 `DateTime.Now`，讓日界邏輯可測試、跨時區部署不出錯。

**Worker 排程外殼與落地流水線抽象**
17 支排程 Worker（16 支資料同步 + `PestRuleEngineWorker` 規則引擎）統一繼承 `ScheduledSyncWorkerBase`（只需實作 SyncAsync / Interval / LogPrefix，基底含 0–30 秒啟動 jitter 錯開啟動風暴）；資料落地共用 `DbSyncHelper.InsertNewByKeyAsync`（以既有鍵視窗掃描防重複）與 `DbSyncHelper.UpsertByKeyAsync`（來源資料會變動時用）；MOA 分頁抓取共用 `MoaPagedFetcher`。Worker 檔案依模組分資料夾（Weather / Market / FoodSafety / Pet）。新 Worker 依此慣例撰寫。

**同一批資料的新舊兩條 API 路徑：按「執行次數」拆分**
農業部部分資料集同時存在官方文件登記的新制端點（有分頁上限）與未登記文件的舊制端點（無限制、一次拿完）。兩者不是二選一，而是按時間維度拆開：**一次性的初始回填走舊制**（效率最高），**長期每日增量走新制**（穩定性有文件保障）。這讓舊制「可能被默默關掉」的風險從長期生產風險降級為一次性風險——回填當下若失敗，重跑即可，不影響任何已上線的排程。

**落地策略由「來源資料會不會變」決定**
`DbSyncHelper` 提供 `InsertNewByKeyAsync`（只新增）與 `UpsertByKeyAsync`（新增或逐欄位更新）兩種語意相反、不可混用的落地方式。事件型資料（收容個案、遺失啟事，登記後不再變動）用前者；主檔型資料（合法業者的評鑑等級、營業狀態會隨時間改變）必須用後者，否則資料會在不知不覺間過期失真，而 log 還顯示「略過 N 筆重複」看起來一切正常。`UpsertByKeyAsync` 採「先查出被追蹤的實體再逐欄位覆寫」，讓 EF Core 得以比對原始快照、**沒變動的欄位完全不產生 UPDATE**。

**寬表 vs 長表：由「來源欄位集穩不穩定」決定，不是照抄同類型的既有表**
`PorkTrans`（毛豬）用 36 欄寬表，`PoultryTrans`（家禽）同屬行情資料卻刻意改用長表（`TransDate` / `MetricCode` / `Price` 三欄恆定），差別在來源形狀：毛豬只有一支 API、欄位集固定；家禽有四支來源 API，欄位數分別是 5/6/2/4 且互不相同，寬表得開 17 個 nullable 欄位、日後接第五支來源還要改 Schema 並跑 Migration。長表把「有哪些指標」從結構問題降級成資料問題。代價是查詢時要自己 GroupBy、單日多指標會是多列而非一列，這在「畫多線折線圖」這個實際用途下反而更好用。`PorkTrans` 沒有跟著改——既有設計在它自己的來源形狀下是對的，一致性不足以構成重寫的理由。

**外部資料的「非數值」要先窮舉再設計，不是遇到一個處理一個**
家禽價格在原始 API 是字串，開工規劃只預期「空字串 → null」。實際窮舉全歷史 20,614 天後發現 8 種非數值型態（休市、未報價、議價、`41-42` 這類區間報價、甚至一筆鍵入錯誤），佔全部儲存格 14.1%，其中紅羽土雞南區兩條線超過三分之一天數、雞蛋產地價 94% 沒有數值。若沿用「TryParse 失敗就 null」，這 14% 全部塌縮成同一種「沒有資料」，前端無從分辨「這個指標本來就少報價」與「同步壞掉了」。最終設計為 `Price` + 7 態 `PriceStatus` + `RawValue` 原文兜底，`Unrecognized` 只承接真正沒見過的型態（全歷史僅 1 筆），維持它作為示警信號而非雜物櫃的定位。**先窮舉再定列舉**是這個決策的可重複部分——列舉成員該有哪幾個，是資料回答的問題，不是設計者憑空決定的。

**一支 Worker 服務多條資料流時：共用機械邏輯，不共用本質知識**
`PoultryTransSyncWorker` 是全案第一支同時服務四條獨立資料流的 Worker。四支來源的歷史起點不同（2010/10/07 與 2014/04/01），共用單一游標必然造成漏抓或空打，因此配置**四組獨立 `SyncState`**。程式碼切法上，「年度切塊 + 游標推進 + 落地」四支等價且容易寫出差一錯誤 → 抽成共用的 `SyncSourceAsync`；「怎麼抓、怎麼攤平成長表」四支本質不同 → 各自獨立方法以委派傳入。這與專案既有共用抽象（`ScheduledSyncWorkerBase` / `DbSyncHelper` / `MoaPagedFetcher`）的判準一致：只抽真的到處一樣、且容易犯錯的部分。另外此 API 支援日期區間參數（不像 `PorkTrans` 只吃單一日期被迫逐日），因此改為逐年切塊，一年 ≤366 天遠低於分頁上限，**回填與日常增量得以共用同一段程式碼**，不需要兩套分支。

**地圖端點的形狀由「用途」決定，不是由「分頁 vs 不分頁」二選一**
地圖端點最初設計成不分頁的完整動物清單：分頁是為「人一頁一頁瀏覽」設計的，不是為「電腦計算完整地理分布」設計的，MarkerCluster 的聚合數字若建立在部分資料上會產生誤導。但「不分頁」帶來的失控風險當時是用防禦性 `Take(3000)` 上限 + `X-Result-Truncated` 截斷標頭壓住，實測全台加總已逼近上限，等於把問題延後發作。真正的解法是換資料形狀：全台上萬筆動物只落在約 30 個收容所座標上，後端先依 `(ShelterPkId, Kind)` 分組計數、reshape 成一所一筆摘要，結果集本身只有約 30 筆——不分頁、不設上限、不需要截斷標頭，三個機制一起消失。**「加上限」是在錯誤的資料形狀上補防禦，「換形狀」才是解決問題。**

**`IsOwner`：回答問題本身，而不是丟出原始資料**
`LostPetPostResponseDto` 不外露 `UserId`（隱私考量），但前端要判斷「這是不是我的貼文」時卻沒有可對照的欄位。解法不是把 `UserId` 補回去讓前端解 JWT 比對，而是後端在查詢當下直接算好布林值 `IsOwner` 回傳——前端完全不需要知道自己的 `userId`，比原設計更安全也更簡單。對應前端慣例：這類「公開可讀、但登入後回應更豐富」的中間態端點一律用會自動附帶 token 的 `authClient`，否則後端永遠收不到身分、`IsOwner` 永遠是 `false`。

**enum 的「一律用字串」慣例只在回應方向成立**
`Response DTO` 由 Service 層逐一 `.ToString()`、`[FromQuery]` 靠 ASP.NET Core Model Binding，兩者都吃字串；但 `[FromBody]` 由 `System.Text.Json` 反序列化，沒有 `[JsonConverter]` 時只吃數字。同一個 enum、換一個傳輸方向就換一套規則，需要在該屬性單獨標註轉換器，不能假設全域一致。

**共用元件不為了「不動舊頁面」而長出兩套行為**
`usePagination` 的分頁視窗改動時，一度考慮加可選參數、預設維持舊行為以避免影響既有食安模組頁面。最終定案是直接改共用預設值——共用元件的價值就是「改一次全部一致」，為了不動舊頁面而讓同一個 composable 分岔，等於用永久的複雜度換一次性的心理安全感。

**座標取得：幾何換算，不是地理編碼**
使用者張貼遺失啟事時，座標由前端 Leaflet 地圖的 `click` 事件直接取得，**不做地址→座標的地理編碼**。可拖曳地圖的圖磚載入時本來就知道每個像素對應的經緯度，換算是函式庫內建公式、不呼叫外部服務，必然成功；地理編碼則是語意比對，成功率取決於資料庫覆蓋率（實測 Nominatim 對台灣門牌地址覆蓋不足，33 筆收容所地址僅成功 1 筆，最終改為人工查證）。

**跨環境種子資料要修兩處**
`DbInitializer` 的冪等 guard（「表裡已有資料就跳過」）代價是它對已上線的環境永遠失效。因此補種子資料一律兩步並行：對現有資料庫直接下 SQL（修正現在）、對 `DbInitializer.cs` 種子清單同步更新（修正未來的新環境），缺一不可。

**前端 useLatestRequest 防競態**
使用者快速切換篩選條件時，晚發先回的舊請求可能覆蓋新結果。以請求序號 composable 統一處理：只採納最後一次發出請求的回應。搭配 `usePagination` 抽離分頁邏輯，均有 vitest 覆蓋。

**xUnit + Moq 三種隔離策略**
Service 層使用真實外部依賴時用 Mock（MarketService → `Mock<IDistributedCache>`）；Service 層只依賴 DB 時用 InMemory（UserWatchlistService → InMemory UserDbContext）；Controller 層測試跨模組組合邏輯時同時 Mock 兩個 Service（WatchlistController）。Extension Method 無法被 Mock 攔截，須 Setup 底層介面方法（GetStringAsync → GetAsync）。

**前端設計系統：token 三層 + 五個頁面樣板（前端視覺設計輪）**
`base.css` 原本 37 行、14 個顏色變數就停住，46 個畫面各自目測調值，累積出 5,051 行 scoped CSS（全域的 120 倍）、102 種顏色、20 種字級。問題不在配色而在「從來沒有設計系統這一層」，所以順序是先立「尺」再逐頁調，不是先去調配色。token 分三層：原始尺度／色階 → semantic 語意層（`--color-action`、`--hint-*` 等）→ 秋田主題色階，畫面一律引用語意層而非寫死色值，改一次全站一致。動效走 `--duration`／`--ease` token，並統一由 `prefers-reduced-motion` 一處歸零。再抽出五個頁面樣板（`QueryLayout` 查詢頁／`DetailLayout` 詳情頁／`MapLayout` 地圖頁／`EntryLayout` 入口頁與首頁 hero 共用），把「28 頁各自排版」收斂成「改 5 個樣板」——這也讓後續逐頁精修的成本從「改 46 個檔」降為「改 5 個檔」。CSS bundle 因此從 477 kB 降到 126 kB（P0–P1 階段）。**第二輪 code review 再把路由改成動態載入之後，首屏實際只需要 46.1 kB CSS（gzip 12.99 kB）**——其餘各頁的樣式跟著各自的 chunk 走，進到那一頁才下載。

**新慣例確立時要回頭套用，否則它只是「那一次的寫法」**
兩輪 code review 的共同頭號結論。第一輪的說法是「共用抽象建立後沒回頭替換舊寫法」，
第二輪量化後形態更精確：慣例按時間順序長出來，最新寫的模組總是比較好，於是這種債
**偽裝成「專案有在進步」**、不會被當成問題。判準是：如果一條慣例只存在於最新寫的程式碼裡，
它就不是慣例。做法是抽共用抽象或立新慣例的當下就 grep 舊寫法有幾處、把數字寫進 PR 描述，
一次套完；套不完就逐處記成待辦，不能寫「其餘同理」。

**自動化檢查不得在通過的同時掩蓋問題**
CI 的 linter 一律唯讀——`--fix` 會在回報前把違規修掉、exit code 0，而修正不會被 commit，
結果是違規留在 repo 而 CI 顯示綠燈。修改模式另開 `lint:fix` 給本機用。
新檢查上線前要先對現況跑一次、逐筆查證命中：本專案曾寫過一份「未定義 CSS 變數」掃描配方，
實跑會噴 9 個假陽性（漏了「元件自己 scoped style 的宣告」與「JS 端 `:style` 綁定注入」
兩種合法定義來源），照原樣掛上 CI 會對乾淨的程式碼亮紅燈——**假紅燈會訓練出「紅了先忽略」
的習慣，等於廢掉這個檢查**。修正成三來源聯集後才掛進 lint 鏈。

**宣稱做了優化，要附最終產物的量測值**
`mdiSubsetPlugin` 原本只裁 CSS 字符規則（7447→85 條）而沒動字型二進位，README 據此寫成
「字型改建置期子集化」——實測 `node_modules` 與 `dist` 的 woff2 都是 403,216 bytes、
位元組完全相同，使用者仍在下載含 7,447 個字符的字型檔。「7447→85」是真的，但那是**中間指標**，
使用者真正承受的成本是字型檔的位元組數。補上二進位重編碼後為 403 kB → 6.3 kB。

---

## 📁 相關文件

| 文件 | 說明 |
|------|------|
| `TaiwanAgriPlatform_SA_SD_V35_3.docx` | SA/SD 完整設計文件（W1–W25 全部實戰開發紀錄 + 全專案 Code Review + 前端視覺設計輪結案記錄，含架構決策日誌 §12 全系列；存放於專案文件資料夾，不進版控） |

---

## 📝 開發慣例

每完成一個功能，在 GitHub 寫一篇 PR Description，記錄：

1. 這個功能解決了什麼問題（背景與動機）
2. 為什麼這樣設計，而不是另一種方式（關鍵設計決策）
3. 遇到什麼坑，怎麼解決（驗收標準 + 踩坑記錄）

---

## 📄 License

MIT License — 詳見 [LICENSE](LICENSE) 檔案。

---

*最後更新：2026-09-04 ｜ 對應 SA/SD 文件版本 V35.3 ｜ 前端視覺設計輪完成（四模組全部完成後的第一次全站視覺統一與設計品質提升：建立 design token 系統與五個頁面樣板、全站色值／尺度收斂、新首頁上線、病蟲害改真地圖、休市日改月曆，GitHub PR #35／#36／#38，前端 50 測試全過）*