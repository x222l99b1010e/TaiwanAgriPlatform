# 🌱 台灣農業開放資料整合平台
### Taiwan Agricultural Open Data Integration Platform

> 把農業部 60 支 API 的孤島資料，串成一個對農民、消費者與研究者都友善的整合平台。

[![.NET](https://img.shields.io/badge/.NET-10.0_LTS-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Vue](https://img.shields.io/badge/Vue-3.x-42b883?logo=vue.js)](https://vuejs.org/)
[![SQL Server](https://img.shields.io/badge/SQL_Server-2022-CC2927?logo=microsoftsqlserver)](https://www.microsoft.com/sql-server)
[![Docker](https://img.shields.io/badge/Docker-Compose-2496ED?logo=docker)](https://www.docker.com/)
[![License](https://img.shields.io/badge/License-MIT-green)](LICENSE)

---

## 📖 專案背景

台灣農業部每年維護超過 60 支 REST API，涵蓋氣象、行情、農藥、食品追溯、寵物認養、土石流警戒等面向。但這些資料以「孤島」形式存在——一個返鄉青農想判斷「今天這批高麗菜要不要搶收？」，需要同時查看四個不同政府網頁，四套不同介面邏輯。

**本專案的目標是把這些孤島串成陸地**，讓農民、消費者、研究者在同一個介面找到需要的農業資訊，而且是已經被整理、計算、視覺化的版本，而非原始 API 回傳資料。

> 這是一個 Side Project，同時用來練習從零開始的完整開發流程：SA → SD → DB Design → API Contract → 實作 → 測試。

---

## ✨ 功能模組

### 🔐 RBAC 骨架 + 動態 Navbar（W11 已完成）

首次引入角色型存取控制基礎建設，讓前端導覽列根據登入狀態與角色動態渲染。

- **NavModule** 自參照樹狀 Entity（頂層 + 子功能兩層，DB Seed 13 筆）
- **RoleModulePermission** 複合 PK Entity（RoleId × ModuleId，Seed 26 筆）
- **DbInitializer** 種子資料職責分離（Schema 歸 Migration，Data 歸 Initializer）
- **NavService** 三段式 RBAC 查詢（FindByNameAsync GUID 解析 → permittedModuleIds ToListAsync → 巢狀 DTO 組裝）
- **NavController** `[AllowAnonymous]`：訪客直接取得 Guest 可見模組清單，無需 JWT
- **Vue 3 三欄 Shell**：TopNav（頂層 tabs）+ SideNav（子功能，依路由高亮）+ RouterView
- **Pinia nav store** + **Vite Proxy** `/api → https://localhost:7147`

### 🌤️ 模組 2：智慧青農戰情室（後端已完成）
面向返鄉青農，整合即時氣象、病蟲害警報與市場行情，透過規則引擎主動推播智慧提示。

- 農場氣象面板（依縣市 + 海拔篩選最近測站）
- 雨量趨勢圖（7 天歷史，Chart.js）
- 病蟲害警報牆（依作物過濾，摘要自動截取）
- **★ 智慧病蟲害提示**：規則引擎偵測「連續 72 小時濕度 > 85%」等條件，主動推送通知
- 農藥查詢（中文俗名 → 學名 → 許可證字號，跨三支 API 橋接）

### 📊 模組 4：大數據探險 — 天災與菜價關聯分析（**後端 + 前台均已完成**）
面向研究者，用歷史資料找出天氣事件與農產品批發價格之間的連動規律。

- 作物歷史價格圖 + 7 日移動平均線（Chart.js + computed 轉換層）
- 天災事件垂直線疊加（chartjs-plugin-annotation，土石流 / 豪雨 / 颱風警戒）
- Chip 多選篩選器（市場類型 / 作物 / 日期區間）
- 休市日標記（排除統計陷阱）— **已完成（32,149 筆休市記錄同步完畢）**
- 數據 CSV 匯出（純函式 exportCsv.ts，含 UTF-8 BOM）
- **★ 前台已完成**：Vue 3 + Pinia + api/Store/Component 三層架構，Promise.all 並行兩支 API

### 🛒 模組 1：台灣生鮮物價與食安透明網（待開發）
面向一般消費者，今日物價查詢 + 食安追溯核查。

- 今日物價首頁（毫秒級回應，Redis Cache-Aside Pattern）
- 追溯碼查詢（支援 QR Code 掃描）
- 農藥殘留違規警示牆（近 90 天不合格名單）
- 有機認證查詢 / CAS 標章查詢
- RabbitMQ 非同步推播架構（Publisher → Exchange → Queue → Consumer）

### 🐾 模組 3：毛小孩守護地圖（待開發）
面向寵物飼主，整合認領養地圖、遺失協尋、合法業者查驗。

- 認領養地圖（Leaflet.js + MarkerCluster + 半徑篩選）
- 遺失協尋地圖（地理編碼整合）
- 登記遺失啟事（需登入，照片上傳）
- 合法業者查驗（需 api_key）

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
│    .NET Worker Service + Hangfire                           │
│    WeatherSyncWorker        | PestAlertSyncWorker           │
│    RainfallSyncWorker       | PestDecadeSyncWorker          │
│    PestRuleEngineWorker     | MarketRestDaySyncWorker       │
│    CropMarketSyncWorker     | AgriProductsTransSyncWorker   │
│    DebrisAlertRecordSyncWorker | PorkTransSyncWorker        │
└──────────┬────────────────────────┬─────────────────────────┘
           │ EF Core                │ RabbitMQ
           │ (多 DbContext)         │
     ┌─────┴────────────┐           │
     │ WeatherDbContext │           │
     │ MarketDbContext  │           ▼
     │ CoreDbContext    │   ┌────────────────────────────────┐
     │ (NavModules,     │   │         RabbitMQ               │
     │  RoleModule-     │   │   Exchange: agri.topic         │
     │  Permissions,    │   │   RoutingKey: agri.price.*     │
     │  SyncStates)     │   └──────────────┬─────────────────┘
     └─────┬────────────┘                  │ Subscribe
           │                               ▼
           ▼                ┌──────────────────────────────────────┐
┌──────────────────┐        │          TaiwanAgri.Web              │
│   SQL Server     │        │   ASP.NET Core Web API               │
│   2022           │        │   ApplicationDbContext               │
└──────────────────┘        │   (繼承 IdentityDbContext)            │
                            │   MarketController (5 支端點)         │
                            │   NavController [AllowAnonymous]     │
                            └──────────┬───────────────────────────┘
                                       │ Cache-Aside
                                       ▼
                      ┌─────────────────────────────────────┐
                      │ Redis TTL 25hr  |  Vue 3 Frontend   │
                      │ StackExchange   |  Vite + Chart.js  │
                      │                 |  TopNav + SideNav │
                      │                 |  Pinia nav store  │
                      └─────────────────────────────────────┘
```

### Solution 結構

```
TaiwanAgriPlatform/
├── TaiwanAgriPlatform.sln
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
│   │   └── NavChildDto.cs            # 子功能 DTO（無 Children，型別即契約）
│   ├── Services/
│   │   ├── INavService.cs
│   │   └── NavService.cs             # 三段式 RBAC 查詢（RoleManager GUID 解析）
│   ├── Helpers/
│   │   └── DateHelper.cs             # ParseRocDate / ParseIsoDate 等
│   └── Infrastructure/
│       ├── Data/
│       │   └── CoreDbContext.cs      # SyncStates + NavModules + RoleModulePermissions
│       └── DbInitializer.cs          # Seed 13 NavModules + 26 RoleModulePermissions
│
├── TaiwanAgri.Modules.Weather/       # 模組 2：氣象 + 病蟲害後端
│   └── (WeatherDbContext)
│
├── TaiwanAgri.Modules.Market/        # 模組 4 + 1：行情分析後端
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
│       └── MarketService.cs          # 三表 JOIN、GroupBy 聚合、AsQueryable 動態過濾
│
├── TaiwanAgri.Modules.FoodSafety/    # 模組 1：食安追溯後端
├── TaiwanAgri.Modules.Pet/           # 模組 3：寵物模組後端
│
├── TaiwanAgri.Worker/                # 入口層：所有排程 Worker + DI 組裝
│
├── TaiwanAgri.Web/                   # 入口層：Web API + Vue 3 Shell
│   ├── Controllers/
│   │   ├── MarketController.cs       # 5 支端點：crops/markets/prices/disasters/restdays
│   │   └── NavController.cs          # [AllowAnonymous] GET /api/nav/modules
│   └── Program.cs                    # AddRoles<IdentityRole> / CoreDbContext /
│                                     # DbInitializer.SeedAsync / AddScoped<INavService>
│
├── TaiwanAgri.Frontend/              # Vue 3 + Vite + TypeScript + Pinia + Vue Router
│   ├── src/
│   │   ├── api/
│   │   │   ├── market.ts             # 模組 4 五支端點封裝
│   │   │   └── nav.ts                # GET /api/nav/modules 封裝 + NavModule 型別
│   │   ├── stores/
│   │   │   ├── market.ts             # Pinia：市場行情全域狀態
│   │   │   └── nav.ts                # Pinia：nav store + loadModules + currentModule()
│   │   ├── components/
│   │   │   ├── TopNav.vue            # 頂層模組 tabs + MDI 圖示
│   │   │   ├── SideNav.vue           # 子功能側欄（依 currentRoute 決定顯示）
│   │   │   ├── MarketFilter.vue
│   │   │   ├── DateRangePicker.vue
│   │   │   └── PriceChart.vue        # Chart.js 折線圖 + 7 日均線 + 天災垂直線
│   │   ├── views/
│   │   │   ├── MarketView.vue
│   │   │   └── PlaceholderView.vue   # 🚧 未開發模組佔位頁
│   │   ├── App.vue                   # 三欄 Shell：TopNav + SideNav + RouterView
│   │   ├── router/index.ts           # 4 頂層路由 + weather 子路由
│   │   ├── main.ts                   # import @mdi/font CSS
│   │   └── utils/exportCsv.ts
│   └── vite.config.ts                # server.proxy: /api → https://localhost:7147
│
└── TaiwanAgri.Tests/                 # xUnit + Moq + TestContainers
    └── (目前僅佔位專案，待 W19-20 補完測試案例)
```

---

## 🛠️ 技術堆疊

| 層次 | 技術 | 版本 | 用途 |
|------|------|------|------|
| 後端框架 | ASP.NET Core Web API | **10.0 LTS** | 主要後端框架 |
| ORM | Entity Framework Core | **10.0** | Code First + Migration |
| 資料庫 | SQL Server | 2022 | Window Functions、時序查詢 |
| 背景排程 | .NET Worker Service + Hangfire | 最新穩定版 | 資料同步排程 |
| 訊息佇列 | RabbitMQ | 3.13 | 非同步事件推播 |
| 快取 | Redis + IMemoryCache | 7.x | 首頁物價秒讀 |
| 身分驗證 | ASP.NET Core Identity + JWT | 10.0 | RBAC 骨架 W11 完成，JWT W15 |
| 前端 | Vue 3 + Vite + TailwindCSS | 最新穩定版 | SPA 前台 |
| 圖表 | Chart.js | 4.x | 折線圖 / 移動平均線 / 天災垂直線 |
| 地圖 | Leaflet.js + OpenStreetMap | 1.9.x | 認領養地圖 |
| 圖示 | Material Design Icons（@mdi/font） | 最新版 | Navbar 模組圖示（CSS class 渲染） |
| 容器化 | Docker Compose | 最新版 | 基礎設施服務 |
| 測試 | xUnit + Moq + TestContainers | 最新版 | 單元 + 整合測試 |
| CI/CD | GitHub Actions | — | 自動測試 + 建置 |

---

## 🚀 本機開發環境設定

### 前置需求

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Visual Studio 2022 / 2026](https://visualstudio.microsoft.com/)（含 ASP.NET 工作負載）
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
# SQL Server
SA_PASSWORD=你的密碼

# Redis
REDIS_PASSWORD=

# MOA API Key（申請自 https://data.moa.gov.tw）
MOA_API_KEY=你的api_key
```

同時在 `TaiwanAgri.Worker/appsettings.Development.json` 確認連線字串：

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=你的伺服器;Database=TaiwanAgriPlatform;User Id=sa;Password=你的密碼;TrustServerCertificate=True"
  },
  "MoaApiConfig": {
    "BaseUrl": "https://data.moa.gov.tw/api/v1",
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

本專案採用多 DbContext 架構，**三個 DbContext 各自有獨立的 Migration 目錄，必須分別執行**。

```powershell
# 1. 氣象 + 病蟲害模組（含 ASP.NET Core Identity 表）
Update-Database -Context WeatherDbContext -StartupProject TaiwanAgri.Worker

# 2. 行情模組（MarketRestDays / MarketInfos / CropInfos / AgriProductsTrans / PorkTrans / DebrisAlertRecords）
Update-Database -Context MarketDbContext -StartupProject TaiwanAgri.Worker

# 3. 跨模組基礎設施（SyncStates + NavModules + RoleModulePermissions）
#    ⚠️ W11 新增兩張資料表，Migration 名稱：AddNavModuleAndRoleModulePermission
#    ⚠️ 若此步驟漏掉，NavController 啟動即拋出例外
Update-Database -Context CoreDbContext -StartupProject TaiwanAgri.Worker
```

Migration 執行完成後，確認 `core.NavModules`（13 筆）與 `core.RoleModulePermissions`（26 筆）已由 `DbInitializer.SeedAsync` 自動寫入。

> **注意**：`Add-Migration` 也必須明確指定 `-Context` 和 `-Project` 參數，
> 例如：`Add-Migration AddNavModuleAndRoleModulePermission -Context CoreDbContext -Project TaiwanAgri.Core`

### Step 5：啟動 Worker

在 Visual Studio 將啟動專案設定為 `TaiwanAgri.Worker`，按 F5 啟動。

### Step 6：啟動 Web API

在 Visual Studio 將啟動專案設定為 `TaiwanAgri.Web`，按 F5 啟動。預設監聽 `https://localhost:7xxx`。

`Program.cs` 在 `builder.Build()` 後會自動執行 `DbInitializer.SeedAsync`（AnyAsync 冪等保護，重複執行不重複插入）。

### Step 7：啟動前台開發伺服器

```bash
cd TaiwanAgri.Frontend
npm install
npm run dev
# 前台伺服器啟動於 http://localhost:5173
# /api/* 請求透過 Vite Proxy 自動轉發至 https://localhost:7147
```

---

## 🗄️ 資料庫設計概覽

本專案資料表分三類型，由四個 DbContext 分工管理：

**WeatherDbContext**（`TaiwanAgri.Modules.Weather`）：
`WeatherObservations` | `RainfallStations` | `RainfallObservations` | `PestAlerts` | `PestAlertCities` | `PestAlertCrops` | `PestDecadeSummaries` | `PestRuleConfig` | `UserNotifications`

**MarketDbContext**（`TaiwanAgri.Modules.Market`）：
`MarketRestDays` | `MarketInfos` | `CropInfos` | `AgriProductsTrans` | `PorkTrans` | `DebrisAlertRecords`

**CoreDbContext**（`TaiwanAgri.Core`，schema: core）：
`SyncStates`（增量同步進度追蹤）| `NavModules`（自參照樹狀導覽主檔，Seed 13 筆）| `RoleModulePermissions`（角色模組可見度，複合 PK，Seed 26 筆）

**ApplicationDbContext**（`TaiwanAgri.Web`）：
`AspNetUsers` | `AspNetRoles` | 其他 Identity 標準表

> **跨 DbContext FK 說明**：`RoleModulePermissions.RoleId` 指向 `AspNetRoles.Id`（GUID），以 `nvarchar(450)` 邏輯 FK 處理，無物理 FOREIGN KEY CONSTRAINT。`NavModules` 自參照 FK 使用 `OnDelete Restrict`。

完整資料表設計請參考 [SA/SD 文件](docs/TaiwanAgriPlatform_SA_SD_v17.3.docx)。

---

## 🌐 API 端點摘要

### 導覽 RBAC（W11 完成）

| Method | URL | 說明 | 認證 |
|--------|-----|------|------|
| GET | `/api/nav/modules` | 依登入狀態回傳可見模組清單（巢狀 DTO） | `[AllowAnonymous]` |

未登入時回傳 Guest 角色可見模組；已登入時依 ClaimsPrincipal 中的 RoleId 篩選。回傳 `NavModuleDto`（含 `List<NavChildDto> Children`），前端 TopNav / SideNav 直接消費，無需重組。

### 模組 4 — 天災與菜價關聯分析（已完成）

| Method | URL | 說明 | 認證 |
|--------|-----|------|------|
| GET | `/api/market/crops?marketType=Veg` | 作物清單（三表 JOIN + DISTINCT） | 不需要 |
| GET | `/api/market/markets?marketType=Veg` | 市場清單 | 不需要 |
| GET | `/api/market/prices` | 作物歷史價格走勢（GroupBy 聚合） | 不需要 |
| GET | `/api/market/disasters` | 天災警戒事件清單（GroupBy 去重） | 不需要 |
| GET | `/api/market/restdays` | 市場休市日清單 | 不需要 |

#### GET /api/market/prices 參數

| 參數 | 型別 | 必填 | 說明 |
|------|------|------|------|
| marketType | string | ✅ | Veg / Fruit / Flower |
| cropCodes | string[] | ✅ | 最多 5 個（`&cropCodes=E1&cropCodes=E2`） |
| marketCode | string | — | 不傳則回傳全台均價 |
| startDate | string | — | yyyy-MM-dd，預設今天往前 365 天 |
| endDate | string | — | yyyy-MM-dd，預設今天 |

### 其他模組端點（規劃中）

| Method | URL | 說明 | 認證 |
|--------|-----|------|------|
| GET | `/api/v1/prices/today` | 首頁物價快照（Redis Cache-Aside） | 不需要 |
| GET | `/api/v1/pests/alerts` | 病蟲害警報列表 | 不需要 |
| GET | `/api/v1/weather/stations` | 氣象站即時資料 | 不需要 |
| GET | `/api/v1/traceability/{traceCode}` | 追溯碼查詢 | 不需要 |
| GET | `/api/v1/animals` | 認領養動物列表 + 地理篩選 | 不需要 |
| POST | `/api/v1/animals/lost` | 登記寵物遺失啟事 | **需要 JWT** |
| GET | `/api/v1/notifications/mine` | 使用者個人告警通知 | **需要 JWT** |
| PUT | `/api/v1/users/watchlist` | 更新關注作物清單 | **需要 JWT** |

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
| W5–6 上半 | 模組 2 資料收集（二） | Identity Migration 提前執行；RainfallStation + Rainfall + PestDecade SyncWorker | ✅ 完成 |
| W5–6 下半 | 模組 2 規則引擎 | PestRuleConfig + UserNotifications + PestRuleEngine.EvaluateAsync() 完整實作 | ✅ 完成 |
| W7–8 上半 | 模組 4 後端 — 基礎建設 | MarketDbContext Schema 分離；MarketInfo surrogate PK 重構；MarketRestDaySyncWorker；CropMarketSyncWorker | ✅ 完成 |
| W7–8 中半 | 模組 4 後端 — 核心同步 | CoreDbContext + SyncState；DateHelper ROC 日期雙向轉換；AgriProductsTransSyncWorker | ✅ 完成 |
| W7–8 下半 | 模組 4 後端 — 優化與收尾 | Task.WhenAll 併發；DebrisAlertRecordSyncWorker；PorkTransSyncWorker；ConfigureConventions decimal(8,2) | ✅ 完成 |
| W9–10 前半 | 模組 4 查詢層 | TaiwanAgri.Web 改造（MVC→Web API）；IMarketService + MarketService 五支查詢；MarketController（PR #020） | ✅ 完成 |
| W9–10 後半 | 模組 4 前台 | Vue 3：Chart.js 折線圖 + 7 日均線；天災垂直線；Chip 多選；CSV 匯出；前端三層架構（PR #021） | ✅ 完成 |
| W11 | RBAC 骨架 + 動態 Navbar | NavModule 自參照；RoleModulePermission 複合 PK；DbInitializer；NavService；NavController [AllowAnonymous]；Vue 3 三欄 Shell（PR #022） | ✅ 完成 |
| W12 | 模組 1 後端 | RabbitMQ + Redis + 物價首頁 + 食安功能 | 🔄 進行中 |
| W13–14 | 模組 2 前台 | Vue 3 氣象面板、雨量折線圖、病蟲害警報牆、通知紅點 | ⬜ 待開始 |
| W15–16 | 身分驗證完整實作 | JWT 發行、Login / Register API、Vue 3 登入頁；NavService nullable roleId 修復 | ⬜ 待開始 |
| W17–18 | 模組 3 | Leaflet 認領養地圖、遺失啟事、合法業者查驗 | ⬜ 待開始 |
| W19–20 | 整合優化 | 全域搜尋、xUnit 測試覆蓋 80%+、Docker 打包、GitHub Actions CI | ⬜ 待開始 |

---

## 🧠 關鍵架構決策記錄

這些決策在開發過程中從真實問題推導出來，詳細推論記錄在 [SA/SD 文件](docs/TaiwanAgriPlatform_SA_SD_v17.3.docx)。

**BackgroundService 生命週期管理**
`WeatherSyncWorker` 繼承 `BackgroundService`，被 DI 容器以 Singleton 管理；`DbContext` 是 Scoped。不能直接在建構子注入 DbContext，必須注入 `IServiceScopeFactory`，在每次同步任務執行時建立新 Scope，用完即釋放，避免 Change Tracker 持續累積狀態。

**NavModule 自參照設計（W11）**
選擇單表自參照而非兩張表，`RoleModulePermission` 的 FK 只需指向一張表。命名從 `Module` 改為 `NavModule` 以避免與 `System.Reflection.Module` 的命名衝突（原命名導致 `Add-Migration` Up() 出現非預期行為）。自參照 FK 使用 `OnDelete Restrict`。`Icon` 欄位存 MDI CSS class 字串，更換圖示只改 DB 不需部署前端。

**RoleModulePermission 跨 DbContext 設計（W11）**
`RoleId` 為 `nvarchar(450)` 邏輯 FK，存的是 IdentityRole 的 **GUID**（非 Role Name）。訪客流程透過 `_roleManager.FindByNameAsync("Guest")` 先做名稱→GUID 轉換。複合 PK `(RoleId, ModuleId)` 天然防重複。`ModuleId` 物理 FK 指向 `NavModules`（同 DbContext），Cascade Delete。

> **⚠️ 已知技術債（commit c9c4621，待 W15 修復）**：`NavService.cs` 第 32 行 `targetRoleId = roleId` 未做 null 防護（CS8600）。已登入使用者若 ClaimsPrincipal 未帶 Role Claim，後續查詢靜默回傳空集合，登入後 Navbar 消失且無任何錯誤訊息。W15 修復方向：null guard 回退至 Guest，或 `ArgumentNullException` 提早失敗。

**DbInitializer 與 Migration 職責分離（W11）**
Schema 歸 Migration，Data 歸 DbInitializer。`HasData` 的修改需要新增 Migration，長期維護下遷移歷史可讀性差。DbInitializer 以 `AnyAsync()` 做冪等保護，在 `builder.Build()` 後（DI Container 完整初始化後）透過 `CreateScope()` 呼叫。

**NavController `[AllowAnonymous]` 安全邊界（W11）**
訪客需能取得 Guest 可見的導覽清單，否則前端無法渲染 Navbar。`[Authorize]` 會直接回傳 401。Controller 讀取 `User.Identity.IsAuthenticated` 後傳純值給 Service，Service 不依賴 `HttpContext`，可被任何入口呼叫。W15 JWT 整合時此設計無需異動。

**NavService `RoleManager<IdentityRole>` 注入位置（W11）**
「依名稱解析角色 GUID」是業務邏輯，不是 HTTP 處理層的職責。若將 `FindByNameAsync` 移至 NavController，Controller 開始承擔業務決策，違反薄層原則。`RoleManager<IdentityRole>` 在 ASP.NET Core 生態中接近 Platform-level 工具，此邊界妥協在 Modular Monolith 單一部署情境下可接受。日後如需脫離 Identity 的測試能力，可包裝為 `IRoleIdResolver` 介面。

**防重複寫入策略**
使用 HashSet 存放 DB 中已有的自然鍵組合，而非只比較最新時間欄位。部分資料源（`PestAlerts`）使用 SHA256 SourceHash，因為去重維度是「內容語意相同」而非「相同時間點」。

**多 DbContext 架構（Modular Monolith）**
每個業務模組有獨立的 DbContext，連線字串設定與啟動由入口層統一組裝，模組本身不感知執行環境。`CoreDbContext` 管理跨模組共用的基礎設施（`SyncStates`、`NavModules`、`RoleModulePermissions`）。

**SyncState 模式取代 MAX(TransDate)**
在全市場休市日，`AgriProductsTrans` 表沒有記錄寫入，MAX 值卡死。改用 `SyncStates` 獨立追蹤「已完成同步的最後一天」，不管那天有無資料寫入，日期都往前推進。

**Task.WhenAll 併發 API 請求策略**
`AgriProductsTransSyncWorker` 初版串行 4,500 次 HTTP 請求，8 小時只同步 1 年 2 個月。改用 `Task.WhenAll` 讓同一天的所有市場 API 同時發出。Task 只負責 HTTP，所有有狀態操作集中在主執行緒依序執行，規避執行緒安全問題。

**跨市場合併去重（Change Tracker 可見範圍陷阱）**
不同 MarketName 查詢可能回傳相同自然鍵的交易記錄。各市場獨立 `DistinctBy` 無法攔截跨市場重複。修正為先收集所有市場 incoming 資料進 `allIncoming`，foreach 結束後統一 `DistinctBy`。

**DebrisAlertRecord：HasFilter(null) 解決 nullable UNIQUE Index 失效問題**
`DebrisNo` 和 `LandslideID` 互斥為 null。EF Core 預設對 nullable 欄位的 UNIQUE Index 加 `WHERE ... IS NOT NULL AND ...`，此 AND 條件永遠不成立，UNIQUE Index 形同虛設。`.HasFilter(null)` 覆蓋預設行為。

**ConfigureConventions 取代逐欄位 HasPrecision**
`PorkTrans` Entity 有 36 個 decimal 欄位，在 `ConfigureConventions` 設定全域 `decimal(8,2)` 規則。

**前端三層架構：api / Store / Component 職責分離**
`api/` 負責 HTTP 封裝；`stores/`（Pinia）負責全域狀態；Vue 元件負責 UI 渲染。平鋪 prices → Chart.js datasets 的格式轉換放在 `PriceChart.vue` 的 `computed()`，純顯示格式轉換，不屬於業務邏輯。

**DisasterResponseDto 重設計：GroupBy 去重 + AffectedCounties 彙整**
同一天災在 DB 對應多筆記錄（每縣市一筆）。Service 層以 `(DisasterName, AlertDate)` GroupBy 後，將同群的 County 彙整為 `AffectedCounties`（`Distinct().OrderBy()` 的 `List<string>`）。

---

## 🧪 執行測試

```bash
cd TaiwanAgri.Tests
dotnet test
```

> **注意**：`TaiwanAgri.Tests` 目前為佔位專案，尚無實際測試案例。xUnit + Moq 測試實作規劃於 W19-20 完成。

---

## 📁 相關文件

| 文件 | 說明 |
|------|------|
| `docs/TaiwanAgriPlatform_SA_SD_v17.3.docx` | SA/SD 完整設計文件（W1–W11 全部實戰開發紀錄，含 RBAC 骨架、NavModule 自參照設計、RoleModulePermission 跨 DbContext FK、DbInitializer 職責分離、NavService 三段式查詢、[AllowAnonymous] 安全邊界、前端三欄 Navbar 架構） |

---

## 📝 開發慣例

每完成一個功能，在 GitHub 寫一篇 PR Description，記錄：

1. 這個功能解決了什麼問題（背景與動機）
2. 為什麼這樣設計，而不是另一種方式（關鍵設計決策）
3. 遇到什麼坑，怎麼解決（驗收標準 + 踩坑記錄）

這本身就是最好的 SA/SD 練習，也是面試時最具說服力的履歷素材。

---

## 📄 License

MIT License — 詳見 [LICENSE](LICENSE) 檔案。

---

*最後更新：2026-05 ｜ 對應 SA/SD 文件版本 v17.3 ｜ PR #022 W11 RBAC 骨架 + 動態 Navbar 完成*