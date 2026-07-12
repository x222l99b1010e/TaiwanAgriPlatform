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
- 前台已完成：Vue 3 + Pinia + api/Store/Component 三層架構，Promise.all 並行兩支 API

### 🌤️ 模組 2：智慧青農戰情室（後端已完成，前台已完成）

面向返鄉青農，整合即時氣象、病蟲害警報與市場行情，透過規則引擎主動推播智慧提示。

- 農場氣象面板（依縣市篩選測站，卡片式顯示溫濕度、風速、24h 雨量）
- 雨量趨勢圖（Chart.js 折線圖，支援 3h/6h/12h/24h 指標切換）
- 病蟲害警報牆（依縣市過濾，可展開查看內文與防治處方）
- 旬密度查詢（害蟲旬別密度折線圖，支援城市多線切換）
- 智慧病蟲害提示：規則引擎偵測閾值與事件型規則，主動推送通知
- 通知鈴鐺：未讀紅點 + Dropdown 無限捲動 + 一鍵全部已讀

### 🔐 身分驗證 + RBAC + 動態 Navbar（W11~W15 完成）

- ASP.NET Core Identity + JWT（SignInManager + UserManager + JwtSecurityTokenHandler）
- 登入 / 註冊（後端驗證訊息中文翻譯）
- NavModule 自參照樹狀 Entity（頂層 + 子功能兩層，DB Seed 13 筆）
- RoleModulePermission 複合 PK Entity（RoleId × ModuleId，Seed 26 筆）
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

### 🐾 模組 3：毛小孩守護地圖（下一個 Sprint：W22–23）

面向寵物飼主，整合認領養地圖、遺失協尋、合法業者查驗。

- 認領養地圖（Leaflet.js + MarkerCluster + 半徑篩選）
- 遺失協尋地圖（地理編碼整合）
- 登記遺失啟事（需登入，照片上傳）

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
│    13 支 SyncWorker 繼承 ScheduledSyncWorkerBase 排程外殼     │
│    （氣象 / 雨量 / 病蟲害×3 / 行情×4 / 天災 / 毛豬            │
│      / 農藥違規 / 有機驗證），落地共用 DbSyncHelper           │
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
     └─────┬────────────┘                  ▼
           │                ┌──────────────────────────────────────┐
     ┌─────┴──────┐         │          TaiwanAgri.Web              │
     │ SQL Server │         │   ASP.NET Core Web API               │
     │   2022     │         │   ApplicationDbContext               │
     └────────────┘         │   (繼承 IdentityDbContext)            │
                            │   GlobalExceptionMiddleware           │
                            │   MarketController  (6 支端點)        │
                            │   FoodSafetyController (4 支端點)     │
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
│   │   └── MoaApiEndpoints.cs        # 60 個 API 端點路徑集中定義
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
│   │   ├── DbSyncHelper.cs           # InsertNewByKeyAsync 共用落地流水線
│   │   └── MoaPagedFetcher.cs        # MOA 分頁抓取共用迴圈
│   ├── Extensions/
│   │   └── MoaApiClientExtensions.cs # AddMoaApiClient() Named Client 共用設定
│   └── Infrastructure/
│       ├── Data/
│       │   └── CoreDbContext.cs      # SyncStates + NavModules + RoleModulePermissions
│       └── DbInitializer.cs          # Seed 13 NavModules + 26 RoleModulePermissions
│
├── TaiwanAgri.Modules.Weather/       # 模組 2：氣象 + 病蟲害
│   └── (WeatherDbContext / Services / Entities / Dtos)
│
├── TaiwanAgri.Modules.Market/        # 模組 4 + 1：行情分析
│   ├── Constants/
│   │   ├── CacheKeys.cs              # Redis Cache Key 前綴常數
│   │   └── MarketTypeMapping.cs      # MarketType ↔ TcType 對應（單一真相來源）
│   ├── Data/
│   │   └── MarketDbContext.cs        # ConfigureConventions decimal(8,2)
│   ├── Dtos/
│   │   ├── WorkerResponses/          # Worker 從 MOA API 反序列化用 DTO
│   │   └── ApiResponses/             # Service 輸出給前端的 DTO
│   ├── Entities/
│   │   └── (MarketRestDay / MarketInfo / CropInfo / AgriProductsTrans
│   │         / DebrisAlertRecord / PorkTrans)
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
├── TaiwanAgri.Modules.Pet/           # 模組 3：寵物模組後端（佔位，W22-23 開發）
│
├── TaiwanAgri.Worker/                # 入口層：13 支排程 Worker + DI 組裝
│   └── ScheduledSyncWorkerBase.cs    # 排程外殼基底（SyncAsync/Interval/LogPrefix + 0–30s 啟動 jitter）
│
├── TaiwanAgri.Web/                   # 入口層：Web API + DI 組裝
│   ├── Controllers/
│   │   ├── AuthController.cs         # POST /api/auth/login、POST /api/auth/register
│   │   ├── FoodSafetyController.cs   # 今日菜價 / 追溯 / 違規牆 / 有機驗證（4 支端點）
│   │   ├── MarketController.cs       # 6 支端點（含 /pork）
│   │   ├── NavController.cs          # [AllowAnonymous] GET /api/nav/modules
│   │   ├── NotificationController.cs # [Authorize] 通知列表 / 未讀數 / 標記已讀
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
│   │   │   ├── authClient.ts         # axios instance（自動注入 Bearer token）
│   │   │   ├── cropApi.ts            # 三市場合併作物清單（profile 用）
│   │   │   ├── foodSafety.ts         # 食安四支端點封裝
│   │   │   ├── market.ts             # 模組 4+畜禽 六支端點封裝
│   │   │   ├── nav.ts                # GET /api/nav/modules
│   │   │   ├── profile.ts            # GET/PUT /api/profile/farm
│   │   │   ├── watchlist.ts          # GET/POST/DELETE /api/watchlist
│   │   │   └── weather.ts            # 氣象 / 雨量 / 病蟲害 / 通知 封裝
│   │   ├── stores/
│   │   │   ├── authStore.ts          # Pinia：JWT + 使用者資訊（localStorage 持久化）
│   │   │   ├── foodSafety.ts         # Pinia：食安狀態（todayVeg TTL / violations / organicCert）
│   │   │   ├── market.ts             # Pinia：市場行情全域狀態
│   │   │   ├── nav.ts                # Pinia：nav store + loadModules
│   │   │   ├── notification.ts       # Pinia：未讀數 + 通知列表 + 無限捲動
│   │   │   ├── profile.ts            # Pinia：農場設定
│   │   │   └── watchlist.ts          # Pinia：監看清單
│   │   ├── composables/
│   │   │   ├── useLatestRequest.ts   # 請求序號防競態（vitest 覆蓋）
│   │   │   └── usePagination.ts      # 分頁邏輯共用
│   │   ├── components/
│   │   │   ├── TopNav.vue            # 頂層模組 tabs + hover dropdown + 通知鈴鐺
│   │   │   ├── NotificationBell.vue  # 鈴鐺 + 未讀紅點 + Dropdown 無限捲動
│   │   │   ├── CitySelector.vue      # 縣市下拉（氣象模組用）
│   │   │   ├── MarketFilter.vue      # 市場類型 Tab + 市場下拉 + 作物 Chip 多選
│   │   │   ├── DateRangePicker.vue   # 日期區間選擇 + 快捷按鈕
│   │   │   └── PriceChart.vue        # Chart.js 折線圖 + 7 日均線 + 天災垂直線
│   │   ├── views/
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
│   │   │   │   ├── RestDaysView.vue  # 休市日查詢（按月分組）
│   │   │   │   └── PorkView.vue      # 毛豬行情（多線折線圖 + 指標切換）
│   │   │   ├── weather/
│   │   │   │   ├── StationView.vue   # 農場氣象（卡片格）
│   │   │   │   ├── RainfallView.vue  # 雨量趨勢（折線圖 + 明細表格）
│   │   │   │   ├── PestAlertsView.vue # 病蟲害警報牆（可展開）
│   │   │   │   └── PestDecadeView.vue # 旬密度趨勢（折線圖 + 全選切換）
│   │   │   ├── FoodSafetyView.vue    # 食安模組容器（RouterView）
│   │   │   ├── MarketView.vue        # 市場模組容器（RouterView）
│   │   │   ├── WeatherView.vue       # 氣象模組容器（RouterView）
│   │   │   ├── ProfileView.vue       # 農場設定（Autocomplete 作物搜尋）
│   │   │   ├── WatchlistView.vue     # 監看清單（MarketType Tab + 均價顯示）
│   │   │   └── PlaceholderView.vue   # 🚧 未開發模組佔位頁
│   │   ├── App.vue                   # 兩層 Shell：TopNav + RouterView
│   │   ├── router/index.ts           # 路由守衛（requiresAuth + redirect-after-login）
│   │   ├── main.ts
│   │   └── utils/exportCsv.ts        # CSV 匯出（UTF-8 BOM）
│   └── vite.config.ts                # server.proxy: /api → https://localhost:7147
│
└── TaiwanAgri.Tests/                 # xUnit + Moq（後端 49 個測試案例）
    ├── Helpers/                       # DateHelper 民國曆邊界值
    ├── Market/                        # Cache Hit / Cache Miss（Mock IDistributedCache）
    ├── User/                          # Watchlist 防重複 / 成功新增（InMemory DB）
    ├── Watchlist/                     # Controller Pattern C 組合（Mock Services）
    ├── FoodSafety/                    # FoodSafetyService 查詢 + 追溯搜尋
    └── Worker/                        # 食安兩支 SyncWorker（MapToEntity 可測化）
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
| 圖示 | Material Design Icons（@mdi/font） | 最新版 | Navbar 模組圖示（CSS class 渲染） |
| 容器化 | Docker Compose | 最新版 | 基礎設施服務（SQL Server / Redis / RabbitMQ） |
| 後端測試 | xUnit + Moq | 最新穩定版 | 單元測試（Service / Controller / Worker 層） |
| 前端測試 | Vitest | 最新穩定版 | composables / utils 單元測試（`npm test`） |
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

本專案採用多 DbContext 架構，**六個 DbContext 各自有獨立的 Migration 目錄，必須分別執行**。

```powershell
# 1. Identity + ApplicationUser（AspNetUsers 等標準表）
Update-Database -Context ApplicationDbContext -StartupProject TaiwanAgri.Web

# 2. 氣象 + 病蟲害模組
Update-Database -Context WeatherDbContext -StartupProject TaiwanAgri.Worker

# 3. 行情模組（MarketRestDays / MarketInfos / CropInfos / AgriProductsTrans / PorkTrans / DebrisAlertRecords）
Update-Database -Context MarketDbContext -StartupProject TaiwanAgri.Worker

# 4. 跨模組基礎設施（SyncStates + NavModules + RoleModulePermissions）
Update-Database -Context CoreDbContext -StartupProject TaiwanAgri.Worker

# 5. 使用者個人化（UserFarmProfiles / UserFarmCrops / UserWatchlists）
Update-Database -Context UserDbContext -StartupProject TaiwanAgri.Web

# 6. 食安模組（PesticideViolations / OrganicCertifications）
Update-Database -Context FoodSafetyDbContext -StartupProject TaiwanAgri.Worker
```

Migration 執行完成後，`core.NavModules`（13 筆）與 `core.RoleModulePermissions`（26 筆）會由 `DbInitializer.SeedAsync` 在應用程式啟動時自動寫入。

> **注意**：`Add-Migration` 也必須明確指定 `-Context` 和 `-Project` 參數，
> 例如：`Add-Migration InitialUserSchema -Context UserDbContext -Project TaiwanAgri.Modules.User`

### Step 5：啟動 Worker

在 Visual Studio 將啟動專案設定為 `TaiwanAgri.Worker`，按 F5 啟動。Worker 會開始同步農業部 API 資料，初次同步較耗時，可在 Console 觀察 Serilog 輸出確認進度。

### Step 6：啟動 Web API

在 Visual Studio 將啟動專案設定為 `TaiwanAgri.Web`，按 F5 啟動。預設監聽 `https://localhost:7147`。

`Program.cs` 在啟動時會自動執行 `DbInitializer.SeedAsync`（AnyAsync 冪等保護，重複執行不重複插入）。

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
# 後端（xUnit + Moq，共 49 個測試案例）
cd TaiwanAgri.Tests
dotnet test

# 前端（Vitest，共 8 個測試案例）
cd TaiwanAgri.Frontend
npm test
```

後端涵蓋 Helpers / Market / User / Watchlist / FoodSafety / Worker 六個面向；前端涵蓋 `useLatestRequest`（請求序號防競態）與 `exportCsv`（CSV 匯出純函式）。CI（GitHub Actions）在每次 push / PR 自動執行 restore → build → test。

---

## 🗄️ 資料庫設計概覽

本專案資料表由六個 DbContext 分工管理：

**ApplicationDbContext**（`TaiwanAgri.Web`，schema: dbo）：
`AspNetUsers` | `AspNetRoles` | 其他 Identity 標準表

**WeatherDbContext**（`TaiwanAgri.Modules.Weather`，schema: weather）：
`WeatherObservations` | `RainfallStations` | `RainfallObservations` | `PestAlerts` | `PestAlertCities` | `PestAlertCrops` | `PestDecadeSummaries` | `PestRuleConfigs` | `UserNotifications`

**MarketDbContext**（`TaiwanAgri.Modules.Market`，schema: market）：
`MarketRestDays` | `MarketInfos` | `CropInfos` | `AgriProductsTrans` | `PorkTrans` | `DebrisAlertRecords`

**CoreDbContext**（`TaiwanAgri.Core`，schema: core）：
`SyncStates`（增量同步進度追蹤）| `NavModules`（自參照樹狀導覽主檔）| `RoleModulePermissions`（角色模組可見度，複合 PK）

**UserDbContext**（`TaiwanAgri.Modules.User`，schema: dbo）：
`UserFarmProfiles` | `UserFarmCrops` | `UserWatchlists`

**FoodSafetyDbContext**（`TaiwanAgri.Modules.FoodSafety`，schema: foodsafety）：
`PesticideViolations` | `OrganicCertifications`

> **跨 DbContext FK 說明**：`RoleModulePermissions.RoleId` 指向 `AspNetRoles.Id`（GUID），以 `nvarchar(450)` 邏輯 FK 處理，無物理 FOREIGN KEY CONSTRAINT。`UserFarmCrop.CropName` 為跨 DbContext 快照欄位，寫入時從 MarketDbContext 複製，不做即時 JOIN。

完整資料表設計請參考 SA/SD 文件 `TaiwanAgriPlatform_SA_SD_V30.4.docx`（存放於專案文件資料夾，不進版控）。

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
| GET | `/api/market/disasters` | 天災警戒事件清單（GroupBy 去重） | 不需要 |
| GET | `/api/market/rest-days` | 市場休市日清單 | 不需要 |
| GET | `/api/market/pork` | 毛豬行情（依日期區間 + 市場篩選） | 不需要 |

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
| GET | `/api/Notification/list?page=1` | 使用者通知列表 | **需要 JWT** |
| GET | `/api/Notification/unread-count` | 未讀通知數 | **需要 JWT** |
| PATCH | `/api/Notification/{id}/read` | 標記單筆已讀 | **需要 JWT** |

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
- **需要 api_key（7 支）**：`SheepQuotation`、`WashedEggsTraceabilityType`、`LegalSpecificPet`、`PetFood`、`FeedAndAdditiveInputCertificate`、`FeedManagementInfo`、`MothSpecimenData`

> **重要限制**：免費帳號分頁 API 只回傳第一頁資料（每頁最多 1,000 筆）。程式碼中保留分頁迴圈，當 API 回傳 `RS: "ERROR"` 時會優雅地 `break`，不影響正常運作。

所有 60 個 API 端點路徑統一定義在：`TaiwanAgri.Core/Constants/MoaApiEndpoints.cs`

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
| W15 | JWT 身分驗證 | ASP.NET Core Identity + JWT；AuthService；LoginView.vue；authClient.ts axios interceptor（PR #032） | ✅ 完成 |
| W16 | 農場偏好設定 | TaiwanAgri.Modules.User；UserFarmProfile / UserFarmCrop；Upsert 策略；ProfileController；ProfileView.vue（PR #033） | ✅ 完成 |
| W17 | 監看清單 | UserWatchlist Entity；IUserWatchlistService（防重複 + 409）；WatchlistController 批量刪除；WatchlistView.vue（PR #034–037） | ✅ 完成 |
| W18 | 監看清單行情整合 | WatchlistEnrichedItemDto 跨模組聚合；Controller Pattern C；UserWatchlist 補 MarketType；均價顯示（PR #038） | ✅ 完成 |
| W19 | 測試 Sprint | xUnit + Moq；MarketServiceCacheTests / UserWatchlistServiceTests / WatchlistControllerTests；共 12 個測試全數綠燈（PR #039） | ✅ 完成 |
| W20 | DevOps | GitHub Actions CI（restore/build/test + badge，W20a）；GlobalExceptionMiddleware 全域例外攔截 + 標準化 JSON 錯誤回應（W20b）。全域搜尋與 Docker 打包延後至功能模組全部完成後統一處理 | ✅ 完成 |
| W21 | 模組 1（食安） | FoodSafetyDbContext + 模組骨架；今日菜價快覽 + 全站菜價輪播（W21a）；農產品追溯查詢（W21b）；農藥違規警示牆 + PesticideViolationSyncWorker（W21c）；有機農產品驗證查詢 + OrganicCertificationSyncWorker（W21d）（GitHub PR #5–#8） | ✅ 完成 |
| —（不掛週次） | Code Review 修正批次 | TimeProvider 時鐘注入 + 台灣時區日界；ScheduledSyncWorkerBase / DbSyncHelper / MoaPagedFetcher 抽共用；Watchlist N+1 批次化；分頁排序穩定性；(CropCode, MarketCode, TransDate DESC) 索引；前端 vitest 導入 + useLatestRequest/usePagination（PR #046，GitHub PR #10–#12） | ✅ 完成 |
| W22–23 | 模組 3（寵物地圖） | Leaflet 認領養地圖 + MarkerCluster；遺失啟事 CRUD；合法業者查詢；地理編碼整合（原規劃於 W17-18，因模組 1/2/4 與身分驗證系列功能優先處理而順延） | ⬜ 待開始（下一個 Sprint） |

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
13 支 SyncWorker 統一繼承 `ScheduledSyncWorkerBase`（只需實作 SyncAsync / Interval / LogPrefix，基底含 0–30 秒啟動 jitter 錯開啟動風暴）；資料落地共用 `DbSyncHelper.InsertNewByKeyAsync`（以既有鍵視窗掃描防重複）；MOA 分頁抓取共用 `MoaPagedFetcher`。新 Worker 依此慣例撰寫。

**前端 useLatestRequest 防競態**
使用者快速切換篩選條件時，晚發先回的舊請求可能覆蓋新結果。以請求序號 composable 統一處理：只採納最後一次發出請求的回應。搭配 `usePagination` 抽離分頁邏輯，均有 vitest 覆蓋。

**xUnit + Moq 三種隔離策略**
Service 層使用真實外部依賴時用 Mock（MarketService → `Mock<IDistributedCache>`）；Service 層只依賴 DB 時用 InMemory（UserWatchlistService → InMemory UserDbContext）；Controller 層測試跨模組組合邏輯時同時 Mock 兩個 Service（WatchlistController）。Extension Method 無法被 Mock 攔截，須 Setup 底層介面方法（GetStringAsync → GetAsync）。

---

## 📁 相關文件

| 文件 | 說明 |
|------|------|
| `TaiwanAgriPlatform_SA_SD_V30.4.docx` | SA/SD 完整設計文件（W1–W21 全部實戰開發紀錄 + Code Review 修正批次結案記錄，含架構決策日誌 §12 全系列；存放於專案文件資料夾，不進版控） |

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

*最後更新：2026-07-12 ｜ 對應 SA/SD 文件版本 v30.4 ｜ W21 食安模組完成 + Code Review 修正批次收尾，W22-23 寵物模組開工前基準點*