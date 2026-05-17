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
     └─────┬────────────┘   │         RabbitMQ               │
           │                │   Exchange: agri.topic         │
           ▼                │   RoutingKey: agri.price.*     │
┌──────────────────┐        └──────────────┬─────────────────┘
│   SQL Server     │                       │ Subscribe
│   2022           │                       ▼
└──────────────────┘    ┌──────────────────────────────────────┐
                        │          TaiwanAgri.Web              │
                        │   ASP.NET Core Web API               │
                        │   ApplicationDbContext               │
                        │   (繼承 IdentityDbContext)            │
                        │   MarketController (5 支端點)         │
                        └──────────┬───────────────────────────┘
                                   │ Cache-Aside
                                   ▼
                  ┌─────────────────────────────────────┐
                  │ Redis TTL 25hr  |  Vue 3 Frontend   │
                  │ StackExchange   |  Vite + Chart.js  │
                  │                 |  Leaflet + Pinia  │
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
│   │   └── ApplicationUser.cs        # 繼承 IdentityUser，供各模組引用
│   ├── Helpers/
│   │   └── DateHelper.cs             # ParseRocDate / FormatRocDate / ParseRocNumericDate
│   │                                 # ToRocNumericDate / ParseIsoDate
│   └── Data/
│       └── CoreDbContext.cs          # SyncStates 跨模組進度追蹤
│
├── TaiwanAgri.Modules.Weather/       # 模組 2：氣象 + 病蟲害後端
│   └── (WeatherDbContext — 普通 DbContext)
│
├── TaiwanAgri.Modules.Market/        # 模組 4 + 1：行情分析後端
│   ├── Data/
│   │   └── MarketDbContext.cs        # ConfigureConventions decimal(8,2)
│   ├── Dtos/
│   │   ├── WorkerResponses/          # Worker 從 MOA API 反序列化用 DTO
│   │   │   ├── AgriProductsTransTypeDto.cs
│   │   │   ├── AgriProductsTransTypeApiResponse.cs
│   │   │   ├── CropMarketTypeDto.cs
│   │   │   ├── DebrisAlertRecordDto.cs
│   │   │   ├── MarketRestDayDto.cs
│   │   │   ├── PorkTransTypeDto.cs
│   │   │   └── ...
│   │   └── ApiResponses/             # Service 輸出給前端的 DTO（依相依方向放在 Modules.Market）
│   │       ├── CropResponseDto.cs
│   │       ├── MarketResponseDto.cs
│   │       ├── PriceResponseDto.cs
│   │       ├── DisasterResponseDto.cs  # v16.0 重設計：GroupBy 去重 + AffectedCounties
│   │       └── RestDayResponseDto.cs
│   ├── Entities/
│   │   └── (MarketRestDay / MarketInfo / CropInfo / AgriProductsTrans
│   │         / DebrisAlertRecord / PorkTrans)
│   └── Services/
│       ├── IMarketService.cs         # 五支查詢方法的介面定義
│       └── MarketService.cs          # 實作（三表 JOIN、GroupBy 聚合、AsQueryable 動態過濾）
│
├── TaiwanAgri.Modules.FoodSafety/    # 模組 1：食安追溯後端
├── TaiwanAgri.Modules.Pet/           # 模組 3：寵物模組後端
│
├── TaiwanAgri.Worker/                # 入口層：所有排程 Worker + DI 組裝
│
├── TaiwanAgri.Web/                   # 入口層：Web API + Vue 3 Shell
│   ├── Controllers/
│   │   ├── HomeController.cs         # ControllerBase（空殼）
│   │   └── MarketController.cs       # 5 支端點：crops/markets/prices/disasters/restdays
│   └── Program.cs                    # AddControllers / CORS / AddProblemDetails
│                                     # ApplicationDbContext（繼承 IdentityDbContext）
│
├── TaiwanAgri.Frontend/              # Vue 3 + Vite + TypeScript + Pinia + Vue Router
│   ├── src/
│   │   ├── api/market.ts             # API 層：五支端點封裝 + 型別定義
│   │   ├── stores/market.ts          # Pinia Store：全域狀態 + actions
│   │   ├── components/
│   │   │   ├── MarketFilter.vue      # Chip 多選篩選器
│   │   │   ├── DateRangePicker.vue   # 日期區間選擇
│   │   │   └── PriceChart.vue        # Chart.js 折線圖 + 7 日均線 + 天災垂直線
│   │   ├── views/MarketView.vue      # 主視圖（Promise.all 並行 API）
│   │   ├── utils/exportCsv.ts        # CSV 匯出純函式（含 UTF-8 BOM）
│   │   └── router/index.ts
│   └── (模組 4 前台已完成，其他模組前台待開發)
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
| 身分驗證 | ASP.NET Core Identity + JWT | 10.0 | 使用者認證 |
| 前端 | Vue 3 + Vite + TailwindCSS | 最新穩定版 | SPA 前台 |
| 圖表 | Chart.js | 4.x | 折線圖 / 移動平均線 / 天災垂直線 |
| 地圖 | Leaflet.js + OpenStreetMap | 1.9.x | 認領養地圖 |
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

本專案採用多 DbContext 架構，**三個 DbContext 各自有獨立的 Migration 目錄，必須分別執行**。若漏掉任何一個，對應的資料表不會建立，相關 Worker 啟動時即報錯。

```powershell
# 1. 氣象 + 病蟲害模組（含 ASP.NET Core Identity 表）
Update-Database -Context WeatherDbContext -StartupProject TaiwanAgri.Worker

# 2. 行情模組（MarketRestDays / MarketInfos / CropInfos / AgriProductsTrans / PorkTrans / DebrisAlertRecords）
Update-Database -Context MarketDbContext -StartupProject TaiwanAgri.Worker

# 3. 跨模組基礎設施（SyncStates — 所有增量 SyncWorker 的進度追蹤表）
#    ⚠️ 若此步驟漏掉，AgriProductsTransSyncWorker 和 PorkTransSyncWorker 啟動即拋出例外
Update-Database -Context CoreDbContext -StartupProject TaiwanAgri.Worker
```

> **注意**：多 DbContext 時，`Add-Migration` 也必須明確指定 `-Context` 和 `-Project` 參數，
> 例如：`Add-Migration InitCore -Context CoreDbContext -Project TaiwanAgri.Core`

Migration 執行完成後，用 SQL Server 物件總管確認 `TaiwanAgriPlatform` 資料庫以及 `weather.*`、`market.*`、`core.*` 三個 Schema 下的資料表均已建立。

### Step 5：啟動應用程式

在 Visual Studio 將啟動專案設定為 `TaiwanAgri.Worker`，按 F5 啟動。

### Step 6：啟動 Web API

在 Visual Studio 將啟動專案設定為 `TaiwanAgri.Web`，按 F5 啟動。預設監聽 `https://localhost:7xxx`。

### Step 7：啟動前台開發伺服器

```bash
cd TaiwanAgri.Frontend
npm install
npm run dev
# 前台伺服器啟動於 http://localhost:5173
```

---

## 🗄️ 資料庫設計概覽

本專案資料表分三類型，由四個 DbContext 分工管理：

**WeatherDbContext**（`TaiwanAgri.Modules.Weather`）管理氣象與病蟲害相關資料表：
`WeatherObservations` | `RainfallStations` | `RainfallObservations` | `PestAlerts` | `PestAlertCities` | `PestAlertCrops` | `PestDecadeSummaries` | `PestRuleConfig` | `UserNotifications`

**MarketDbContext**（`TaiwanAgri.Modules.Market`）管理行情相關資料表：
`MarketRestDays` | `MarketInfos` | `CropInfos` | `AgriProductsTrans` | `PorkTrans` | `DebrisAlertRecords`

> `DebrisAlertRecords`（土石流歷史記錄）是已完成的獨立資料表，提供模組 4 天災時間軸的資料來源。

**CoreDbContext**（`TaiwanAgri.Core`）管理跨模組基礎設施：
`SyncStates`（增量同步進度追蹤，`AgriProductsTransSyncWorker` 和 `PorkTransSyncWorker` 共用）

**ApplicationDbContext**（`TaiwanAgri.Web`）管理身分驗證：
`AspNetUsers`（含擴充欄位）| `AspNetRoles` | 其他 Identity 標準表

完整資料表設計、欄位型別、索引說明請參考 [SA/SD 文件](docs/TaiwanAgriPlatform_SA_SD_v16.1.docx)。

---

## 🌐 API 端點摘要

### 模組 4 — 天災與菜價關聯分析（已完成）

| Method | URL | 說明 | 認證 |
|--------|-----|------|------|
| GET | `/api/market/crops?marketType=Veg` | 有交易記錄的作物清單（三表 JOIN + DISTINCT） | 不需要 |
| GET | `/api/market/markets?marketType=Veg` | 市場清單（依市場類型篩選） | 不需要 |
| GET | `/api/market/prices` | 作物歷史價格走勢（含全台均價 / 單一市場，GroupBy 聚合） | 不需要 |
| GET | `/api/market/disasters` | 天災警戒事件清單（GroupBy 去重，AffectedCounties 彙整） | 不需要 |
| GET | `/api/market/restdays` | 市場休市日清單（市場代碼 + 日期區間） | 不需要 |

#### GET /api/market/prices 參數說明

| 參數 | 型別 | 必填 | 說明 |
|------|------|------|------|
| marketType | string | ✅ | Veg / Fruit / Flower |
| cropCodes | string[] | ✅ | 可傳多個（`&cropCodes=E1&cropCodes=E2`），最多 5 個 |
| marketCode | string | — | 不傳則回傳全台均價（各價格欄位 AVG + SUM quantity） |
| startDate | string | — | yyyy-MM-dd，不傳預設今天往前 365 天 |
| endDate | string | — | yyyy-MM-dd，不傳預設今天 |

### 其他模組端點（規劃中）

| Method | URL | 說明 | 認證 |
|--------|-----|------|------|
| GET | `/api/v1/prices/today` | 首頁物價快照（Redis Cache-Aside） | 不需要 |
| GET | `/api/v1/pests/alerts` | 病蟲害警報列表 | 不需要 |
| GET | `/api/v1/weather/stations` | 氣象站即時資料 | 不需要 |
| GET | `/api/v1/traceability/{traceCode}` | 追溯碼查詢（直連 API + Redis TTL） | 不需要 |
| GET | `/api/v1/animals` | 認領養動物列表 + 地理篩選 | 不需要 |
| POST | `/api/v1/animals/lost` | 登記寵物遺失啟事 | **需要 JWT** |
| GET | `/api/v1/notifications/mine` | 使用者個人告警通知 | **需要 JWT** |
| PUT | `/api/v1/users/watchlist` | 更新關注作物清單 | **需要 JWT** |
| GET | `/api/v1/prices/export` | 價格 + 天災事件 CSV 匯出 | 不需要 |

---

## 🔌 農業部 API 說明

本專案串接 [農業部開放資料平台](https://data.moa.gov.tw) 共 60 支 API。

- **免費可用（53 支）**：涵蓋所有核心功能，MVP 開發不受限制
- **需要 api_key（7 支）**：`SheepQuotation`、`WashedEggsTraceabilityType`、`LegalSpecificPet`、`PetFood`、`FeedAndAdditiveInputCertificate`、`FeedManagementInfo`、`MothSpecimenData`

> **重要限制**：免費帳號分頁 API 只回傳第一頁資料（每頁最多 1,000 筆）。
> 程式碼中保留分頁迴圈，當 API 回傳 `RS: "ERROR"` 時會優雅地 `break`，不影響正常運作。

> **AgriProductsTransSyncWorker 的分頁抑制策略**：同時帶入 `Start_time + End_time + MarketName`
> 三個參數，讓 API 回傳特定市場特定天的資料，結果量有自然上限，`Next` 始終為 `false`，無需分頁迴圈。

所有 60 個 API 端點路徑統一定義在：`TaiwanAgri.Core/Constants/MoaApiEndpoints.cs`

---

## ⏱️ 開發進度

| Sprint | 階段 | 內容摘要 | 狀態 |
|--------|------|----------|------|
| W1–2 | 基礎建設 | Docker Compose + 8 Project Solution + 第一個 Migration | ✅ 完成 |
| W3–4 | 模組 2 資料收集（一） | WeatherSyncWorker（分頁、HashSet 防重複、30 天自動清除） | ✅ 完成 |
| W5–6 上半 | 模組 2 資料收集（二） | Identity Migration 提前執行；RainfallStation + Rainfall + PestDecade SyncWorker | ✅ 完成 |
| W5–6 下半 | 模組 2 規則引擎 | PestRuleConfig + UserNotifications + PestRuleEngine.EvaluateAsync() 完整實作 | ✅ 完成 |
| W7–8 上半 | 模組 4 後端 — 基礎建設 | MarketDbContext Schema 分離；MarketInfo surrogate PK 重構；MarketRestDaySyncWorker（32,149 筆）；CropMarketSyncWorker | ✅ 完成 |
| W7–8 中半 | 模組 4 後端 — 核心同步 | CoreDbContext + SyncState；DateHelper ROC 日期雙向轉換；AgriProductsTransSyncWorker 完整實作 | ✅ 完成 |
| W7–8 下半 | 模組 4 後端 — 優化與收尾 | Task.WhenAll 併發優化；SaveChanges 批次化；跨市場重複寫入 Bug Fix；DebrisAlertRecordSyncWorker；PorkTransSyncWorker；ConfigureConventions 全域 decimal(8,2) | ✅ 完成 |
| W9–10 前半 | 模組 4 查詢層 | TaiwanAgri.Web 改造（MVC→Web API）；IMarketService + MarketService 五支查詢方法；MarketController 五支端點；DTO 結構重組（WorkerResponses / ApiResponses）（PR #020） | ✅ 完成 |
| W9–10 後半 | 模組 4 前台 | Vue 3 完整前台：Chart.js 折線圖 + 7 日均線；天災垂直線 Plugin；Chip 多選篩選器；CSV 匯出；DisasterResponseDto 重設計；前端三層架構（api/Store/Component）（PR #021） | ✅ 完成 |
| W11–12 | 模組 1 後端 + 前台 | RabbitMQ + Redis + 物價首頁 + 食安功能 | ⬜ 待開始 |
| W13–14 | 模組 2 前台 | Vue 3 氣象面板、雨量折線圖、病蟲害警報牆、通知紅點 | ⬜ 待開始 |
| W15–16 | 身分驗證完整實作 | JWT 發行、Login / Register API、Vue 3 登入頁 | ⬜ 待開始 |
| W17–18 | 模組 3 | Leaflet 認領養地圖、遺失啟事、合法業者查驗 | ⬜ 待開始 |
| W19–20 | 整合優化 | 全域搜尋、xUnit 測試覆蓋 80%+、Docker 打包、GitHub Actions CI | ⬜ 待開始 |

---

## 🧠 關鍵架構決策記錄

這些決策在開發過程中從真實問題推導出來，詳細推論記錄在 [SA/SD 文件](docs/TaiwanAgriPlatform_SA_SD_v16.1.docx)。

**BackgroundService 生命週期管理**
`WeatherSyncWorker` 繼承 `BackgroundService`，被 DI 容器以 Singleton 管理；`DbContext` 是 Scoped。不能直接在建構子注入 DbContext，必須注入 `IServiceScopeFactory`，在每次同步任務執行時建立新 Scope，用完即釋放，避免 Change Tracker 持續累積狀態。

**防重複寫入策略**
使用 HashSet 存放 DB 中已有的 `(StationId, ObservedAt)` 組合，而非只比較單一最新時間欄位。後者在多測站同時回傳相同時間時會出現漏判；HashSet 方式在任何情況下都能正確過濾。部分資料源（`PestAlerts`）使用 SHA256 SourceHash 取代 HashSet，因為去重維度是「內容語意相同」而非「相同時間點」——業務定義不同，策略也不同。

**多 DbContext 架構**
採用 Modular Monolith 設計。每個業務模組有獨立的 DbContext（`WeatherDbContext`、`MarketDbContext`），部署在各自的 Module Project 中，只宣告 Entity 結構，不碰連線字串。連線字串設定與 Worker 啟動統一由入口層（`TaiwanAgri.Worker`、`TaiwanAgri.Web`）的 `Program.cs` 負責組裝，模組本身不感知執行環境。`ApplicationDbContext`（繼承 `IdentityDbContext`）僅在 `TaiwanAgri.Web` 管理 Identity 六張表，與業務 DbContext 完全分離。`CoreDbContext` 放在 `TaiwanAgri.Core`，專門管理跨模組共用的基礎設施實體（目前只有 `SyncStates`），讓任何模組的 Worker 都能引用，不需要跨模組依賴。

**跨 DbContext FK 策略**
`UserNotifications.UserId` 指向 `AspNetUsers.Id`，但兩者分屬不同 DbContext，EF Core 無法建立物理 FOREIGN KEY CONSTRAINT 跨 DbContext 邊界。解法是以 `nvarchar(450)` 純字串欄位作為邏輯 FK，由應用程式層保證值的正確性。同時不加 Navigation Property——一旦加了，EF Core `Add-Migration` 會把被導航到的 Entity 誤判為自己要管的表，在 Migration 中多建一張孤立的冗餘表。

**Identity Migration 提前策略**
`AspNetUsers` 表在 W5–6 就建立（只需三行設定），讓後續所有 B 類資料表的 `UserId FK` 從一開始就是 `NOT NULL`，不欠技術債。Login UI 和 JWT 在 W15–16 才實作，兩件事獨立進行互不干擾。

**API 端點路徑集中管理**
60 個 MOA API 端點全部定義在 `MoaApiEndpoints.cs` 為 `const string`。散落各處等同於 60 個潛在的手動維護點；集中定義後，IDE 的「尋找所有參考」可以立即找到每個端點的所有使用位置。

**SyncState 模式取代 MAX(TransDate)**
增量同步的進度不能從 DB 業務資料推算（`MAX(TransDate)`）——在全市場休市日，`AgriProductsTrans` 表沒有任何記錄寫入，MAX 值永遠停在前一天，Worker 無限重跑同一天無法自癒。改用 `CoreDbContext` 的 `SyncStates` 資料表獨立追蹤「已完成同步的最後一天」，不管那天有無資料寫入，日期都往前推進。`SyncState` 放在 `TaiwanAgri.Core` 而非 Market 模組，確保 `PorkTransSyncWorker` 等後續 Worker 都能共用同一套機制。

**MarketInfo surrogate PK 設計**
MarketCode 514 在 Veg API 叫「溪湖鎮」、在 Flower API 叫「彰化市場」，兩個名稱各自查詢到不同的 AgriProductsTrans 資料集，必須分別存為兩筆。「一個 MarketCode 對應一筆主檔」的假設失效，PK 改成 surrogate Id，Unique constraint 改為 `(MarketCode, MarketName)`。這個決策同時移除了 `AgriProductsTrans` 對 `MarketInfos` 的物理 FK，改用應用程式層保證：Worker 的市場清單本來就從 `MarketInfos` 讀出，寫入的 `MarketCode` 一定有效，不需要 DB 層重複保護。

**Task.WhenAll 併發 API 請求策略**
`AgriProductsTransSyncWorker` 初版串行跑 90 天 × 50 市場 = 4,500 次 HTTP 請求，實際執行 8 小時只同步 1 年 2 個月。改用 `Task.WhenAll` 讓同一天的所有市場 API 同時發出，等待時間從串行加總降為最慢單一市場。Task 只負責打 API 回傳原始 json，所有有狀態的操作（去重、快取更新、AddRange）集中在主執行緒依序執行，完全規避執行緒安全與 TOCTOU 問題，不需要 `ConcurrentDictionary` 或 `ConcurrentBag`。

**跨市場合併去重（Change Tracker 可見範圍陷阱）**
MarketInfos 允許同一 MarketCode 有多筆 MarketName，`Task.WhenAll` 會以多個 MarketName 各打一次 API。農業部回傳的交易資料欄位是 `MarketCode`，不是查詢用的 MarketName，導致不同 MarketName 查詢可能回傳相同自然鍵的交易記錄。原本各市場獨立 `DistinctBy` 的設計無法攔截跨市場重複——SaveChanges 批次化之後，Change Tracker 累積的新增資料對 `existingKeySet`（查 DB 快照建立）完全不可見，批次內的跨來源重複只有在 `AddRange` 前合併去重才能正確攔截。修正為先把所有市場 incoming 資料收集進 `allIncoming`，foreach 結束後統一執行 `DistinctBy`。

**DebrisAlertRecord：HasFilter(null) 解決 nullable UNIQUE Index 失效問題**
`DebrisAlertRecord` 的去重自然鍵由 `(ReportID, DebrisNo, LandslideID)` 組成，其中 `DebrisNo` 和 `LandslideID` 互斥為 null（D 型土石流 `DebrisNo` 有值、L 型大規模崩塌 `LandslideID` 有值）。EF Core 對含 nullable 欄位的 UNIQUE Index 預設自動加上 `WHERE [DebrisNo] IS NOT NULL AND [LandslideID] IS NOT NULL`，這個 AND 條件對所有記錄永遠不成立，UNIQUE Index 形同虛設。解法是在 `OnModelCreating` 加 `.HasFilter(null)`，覆蓋 EF Core 的預設行為，讓 SQL Server 建立不帶任何 filter 的完整 UNIQUE Index。

**PorkTransSyncWorker：lastSuccessfulDate 精確斷點模式**
PorkTrans API 一次只接受單一 `TransDate`，休市日無資料寫入，和 `AgriProductsTrans` 一樣面對 `MAX(TransDate)` 卡死問題，因此也需要 `SyncState`。進度推進策略使用 `lastSuccessfulDate`：只有 API 回傳 `RS==OK`（含休市日空回傳）才推進 `lastSuccessfulDate`；`RS != OK` 或例外則 `break`；迴圈結束後以 `lastSuccessfulDate` 更新 `SyncState`，而非迴圈計數器。這確保「已確認完成的最後一天」精確記錄，中途任何一天失敗都不會讓進度跳過那天。

**ConfigureConventions 取代逐欄位 HasPrecision**
`PorkTrans` Entity 有 36 個 decimal 欄位，若逐一設定 `HasPrecision(8,2)` 過於繁瑣且易漏。在 `MarketDbContext.ConfigureConventions` 設定全域規則，讓所有 decimal 欄位自動套用 `decimal(8,2)`，`OnModelCreating` 只在需要例外精度時才個別覆蓋。這是從「局部設定」演進為「全域標準」的系統性改善。

**查詢層的相依方向：Service 歸屬 Modules.Market**
`MarketService` 放在 `TaiwanAgri.Modules.Market/Services/` 而非 `TaiwanAgri.Web`，確保相依方向正確：Web（上層）→ Modules.Market（下層）。任何需要市場查詢邏輯的入口（Web API、後台管理、測試）都可以直接依賴 Modules.Market，不需要跨層依賴。`IMarketService` 定義在同一模組，讓 Controller 依賴抽象而非具體實作，支援後續的單元測試 mock。

**DTO 結構分層：WorkerResponses vs ApiResponses**
`Dtos/` 資料夾依資料流方向分兩個子目錄，而非混放：`WorkerResponses/` 存放 MOA API 回傳資料的反序列化 DTO（欄位形狀由外部 API 決定），`ApiResponses/` 存放 Service 輸出給前端的 DTO（欄位形狀由前台畫面需求決定）。角色命名比來源命名更有自解釋性，讓維護者不需要任何背景知識就能判斷檔案的用途。

**Controller 輸入驗證策略：string + ParseIsoDate 取代 [FromQuery] DateOnly**
日期參數用 `string` 接收，手動呼叫 `DateHelper.ParseIsoDate` 解析，格式不合法時回傳明確的 `BadRequest("...請使用 yyyy-MM-dd")`。這讓錯誤訊息對前端友好，驗證邏輯明確可見，且不依賴 ASP.NET Core Model Binding 對 `DateOnly` 的版本特定行為。選填日期的預設值邏輯（今天往前 365 天）放在 Service 而非 Controller，因為它是業務決策而非技術約束。

**前端三層架構：api / Store / Component 職責分離**
模組 4 前台採用嚴格的三層架構：`api/market.ts` 負責 HTTP 封裝與型別定義（對應後端 HttpClient 層）；`stores/market.ts`（Pinia）負責全域狀態與 actions（對應後端 Service 層）；Vue 元件負責 UI 渲染與使用者互動（對應後端 Controller 層）。平鋪 prices → Chart.js datasets 的格式轉換放在 `PriceChart.vue` 的 `computed()`，而非 Store 或 api 層——這是純顯示格式轉換，不需要跨元件共享，也不屬於業務邏輯。

**DisasterResponseDto 重設計：GroupBy 去重 + AffectedCounties 彙整**
同一天災事件在 DB 中對應多筆記錄（每縣市一筆）。前端的天災卡片以「事件」為單位呈現，不是以「縣市紀錄」為單位。Service 層以 `(DisasterName, AlertDate)` GroupBy 後，將同群的 County 彙整為 `AffectedCounties`（`Distinct().OrderBy()` 的 `List<string>`）。DTO 輸出的 `AlertDate` 是 Entity 欄位 `LastUpdateDate` 的改名 + 格式轉換（yyyy-MM-dd），Entity 本身未異動。

---

## 🧪 執行測試

```bash
cd TaiwanAgri.Tests
dotnet test
```

> **注意**：`TaiwanAgri.Tests` 目前為佔位專案，尚無實際測試案例。xUnit + Moq 測試實作規劃於 W19-20 完成。

或在 Visual Studio 使用測試總管（Test Explorer）執行。

---

## 📁 相關文件

| 文件 | 說明 |
|------|------|
| `docs/TaiwanAgriPlatform_SA_SD_v16.1.docx` | SA/SD 完整設計文件（痛點故事、API 清單、DB 設計、Sprint 計畫、W1–W10 全部實戰開發紀錄，含前端三層架構、DisasterResponseDto 重設計、Chart.js 整合決策） |

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

*最後更新：2026-05 ｜ 對應 SA/SD 文件版本 v16.1 ｜ PR #021 模組 4 前台完整實作完成*