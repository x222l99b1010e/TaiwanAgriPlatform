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

### 📊 模組 4：大數據探險 — 天災與菜價關聯分析（後端進行中）
面向研究者，用歷史資料找出天氣事件與農產品批發價格之間的連動規律。

- 作物歷史價格圖 + 7 日移動平均線（SQL Window Functions）
- 天災事件時間軸疊加（土石流 / 豪雨 / 颱風警戒）
- 事件前後漲跌幅分析（LAG / LEAD 函數）
- 休市日標記（排除統計陷阱）— **已完成（32,149 筆休市記錄同步完畢）**
- 數據 CSV 匯出（串流輸出）

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
│              data.moa.gov.tw  (60 支 REST API)               │
└──────────────────────┬──────────────────────────────────────┘
                       │ HTTP / IHttpClientFactory
                       ▼
┌─────────────────────────────────────────────────────────────┐
│                  TaiwanAgri.Worker                          │
│    .NET Worker Service + Hangfire                           │
│    WeatherSyncWorker   | PestAlertSyncWorker                │
│    RainfallSyncWorker  | PestDecadeSyncWorker               │
│    PestRuleEngineWorker| MarketRestDaySyncWorker            │
│    (AgriProductsWorker | DisasterEventWorker  → 開發中)     │
└──────────┬────────────────────────┬────────────────────────┘
           │ EF Core                │ RabbitMQ
           │ (多 DbContext)         │
     ┌─────┴───────┐               │
     │ WeatherDbCtx│               │
     │ MarketDbCtx │               ▼
     └─────┬───────┘   ┌────────────────────────────────────┐
           │            │         RabbitMQ                   │
           ▼            │   Exchange: agri.topic             │
┌──────────────────┐    │   RoutingKey: agri.price.updated   │
│   SQL Server     │    └────────────────┬───────────────────┘
│   2022           │                     │ Subscribe
└──────────────────┘                     ▼
                        ┌─────────────────────────────────────┐
                        │          TaiwanAgri.Web             │
                        │   ASP.NET Core Web API + MVC        │
                        │   ApplicationDbContext              │
                        │   (繼承 IdentityDbContext)          │
                        └──────────┬──────────────────────────┘
                                   │ Cache-Aside
                                   ▼
                  ┌────────────────────────────────────┐
                  │ Redis TTL 25hr  |  Vue 3 Frontend  │
                  │ StackExchange   |  Vite + Chart.js  │
                  │                 |  Leaflet + Pinia  │
                  └────────────────────────────────────┘
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
│   └── Entities/
│       └── ApplicationUser.cs        # 繼承 IdentityUser，供各模組引用
│
├── TaiwanAgri.Modules.Weather/       # 模組 2：氣象 + 病蟲害後端
│   └── (WeatherDbContext — 普通 DbContext)
├── TaiwanAgri.Modules.Market/        # 模組 4 + 1：行情分析後端
│   └── (MarketDbContext — 普通 DbContext)
├── TaiwanAgri.Modules.FoodSafety/    # 模組 1：食安追溯後端
├── TaiwanAgri.Modules.Pet/           # 模組 3：寵物模組後端
│
├── TaiwanAgri.Worker/                # 入口層：所有排程 Worker + DI 組裝
├── TaiwanAgri.Web/                   # 入口層：Web API + Vue 3 Shell
│   └── (ApplicationDbContext — 繼承 IdentityDbContext)
└── TaiwanAgri.Tests/                 # xUnit + Moq + TestContainers
```

---

## 🛠️ 技術堆疊

| 層次 | 技術 | 版本 | 用途 |
|------|------|------|------|
| 後端框架 | ASP.NET Core Web API + MVC | **10.0 LTS** | 主要後端框架 |
| ORM | Entity Framework Core | **10.0** | Code First + Migration |
| 資料庫 | SQL Server | 2022 | Window Functions、時序查詢 |
| 背景排程 | .NET Worker Service + Hangfire | 最新穩定版 | 資料同步排程 |
| 訊息佇列 | RabbitMQ | 3.13 | 非同步事件推播 |
| 快取 | Redis + IMemoryCache | 7.x | 首頁物價秒讀 |
| 身分驗證 | ASP.NET Core Identity + JWT | 10.0 | 使用者認證 |
| 前端 | Vue 3 + Vite + TailwindCSS | 最新穩定版 | SPA 前台 |
| 圖表 | Chart.js | 4.x | 折線圖 / 移動平均線 |
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
- [Node.js 20+](https://nodejs.org/)（前端建置用）

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
    "WeatherDb": "Server=你的伺服器;Database=TaiwanAgriPlatform;User Id=你的帳號;Password=你的密碼;TrustServerCertificate=True",
    "MarketDb": "Server=你的伺服器;Database=TaiwanAgriPlatform;User Id=你的帳號;Password=你的密碼;TrustServerCertificate=True"
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

專案採用多 DbContext 架構，每個模組有獨立的 Migration 目錄，需分別執行：

```powershell
# 氣象 + 病蟲害模組（含 Identity 表）
Update-Database -Context WeatherDbContext -StartupProject TaiwanAgri.Worker

# 行情模組
Update-Database -Context MarketDbContext -StartupProject TaiwanAgri.Worker
```

> **注意**：多 DbContext 時，`Add-Migration` 也必須明確指定 `-Context` 和 `-Project` 參數，
> 否則 EF Core 無法確定要操作哪個 DbContext。

Migration 執行完成後，用 SQL Server 物件總管確認 `TaiwanAgriPlatform` 資料庫和各資料表已建立。

### Step 5：啟動應用程式

在 Visual Studio 將啟動專案設定為 `TaiwanAgri.Worker`，按 F5 啟動。

Hangfire Dashboard 可在 `http://localhost:5000/hangfire` 查看排程狀態。

---

## 🗄️ 資料庫設計概覽

本專案資料表分三類型，由兩個業務 DbContext 分工管理：

**WeatherDbContext**（`TaiwanAgri.Modules.Weather`）管理氣象與病蟲害相關資料表：
`WeatherObservations` | `RainfallStations` | `RainfallObservations` | `PestAlerts` | `PestAlertCities` | `PestAlertCrops` | `PestDecadeSummaries` | `PestRuleConfig` | `UserNotifications`

**MarketDbContext**（`TaiwanAgri.Modules.Market`）管理行情相關資料表：
`MarketRestDays` | `PriceHistory`（開發中）| `DisasterEvents`（開發中）

**ApplicationDbContext**（`TaiwanAgri.Web`）管理身分驗證：
`AspNetUsers`（含擴充欄位）| `AspNetRoles` | 其他 Identity 標準表

完整 28 張資料表設計、欄位型別、索引說明請參考 [SA/SD 文件第 5.4 節](docs/TaiwanAgriPlatform_SA_SD_v10.docx)。

---

## 🌐 API 端點摘要

| Method | URL | 說明 | 認證 |
|--------|-----|------|------|
| GET | `/api/v1/prices/today` | 首頁物價快照（Redis Cache-Aside） | 不需要 |
| GET | `/api/v1/prices/{cropCode}/history` | 歷史行情 + 移動平均 | 不需要 |
| GET | `/api/v1/prices/disasters/correlations` | 天災前後漲跌分析 | 不需要 |
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

所有 60 個 API 端點路徑統一定義在：`TaiwanAgri.Core/Constants/MoaApiEndpoints.cs`

---

## ⏱️ 開發進度

| Sprint | 階段 | 內容摘要 | 狀態 |
|--------|------|----------|------|
| W1–2 | 基礎建設 | Docker Compose + 8 Project Solution + 第一個 Migration | ✅ 完成 |
| W3–4 | 模組 2 資料收集（一） | WeatherSyncWorker（分頁、HashSet 防重複、30 天自動清除） | ✅ 完成 |
| W5–6 上半 | 模組 2 資料收集（二） | Identity Migration 提前執行；RainfallStation + Rainfall + PestDecade SyncWorker | ✅ 完成 |
| W5–6 下半 | 模組 2 規則引擎 | PestRuleConfig + UserNotifications + PestRuleEngine.EvaluateAsync() 完整實作 | ✅ 完成 |
| W7–8 | 模組 4 後端（Market） | MarketDbContext 建立；MarketRestDaySyncWorker（32,149 筆）；AgriProductsTrans / PorkTrans / DebrisAlert SyncWorker | 🔄 進行中 |
| W9–10 | 模組 4 前台 | 作物歷史價格圖、天災時間軸疊加、漲跌幅分析、CSV 匯出 | ⬜ 待開始 |
| W11–12 | 模組 1 後端 + 前台 | RabbitMQ + Redis + 物價首頁 + 食安功能 | ⬜ 待開始 |
| W13–14 | 模組 2 前台 | Vue 3 氣象面板、雨量折線圖、病蟲害警報牆、通知紅點 | ⬜ 待開始 |
| W15–16 | 身分驗證完整實作 | JWT 發行、Login / Register API、Vue 3 登入頁（AspNetUsers 已在 W5–6 建立） | ⬜ 待開始 |
| W17–18 | 模組 3 | Leaflet 認領養地圖、遺失啟事、合法業者查驗 | ⬜ 待開始 |
| W19–20 | 整合優化 | 全域搜尋、xUnit 測試覆蓋 80%+、Docker 打包、GitHub Actions CI | ⬜ 待開始 |

---

## 🧠 關鍵架構決策記錄

這些決策在開發過程中從真實問題推導出來，詳細推論記錄在 [SA/SD 文件第 12 章](docs/TaiwanAgriPlatform_SA_SD_v10.docx)。

**BackgroundService 生命週期管理**
`WeatherSyncWorker` 繼承 `BackgroundService`，被 DI 容器以 Singleton 管理；`DbContext` 是 Scoped。
不能直接在建構子注入 DbContext，必須注入 `IServiceScopeFactory`，在每次同步任務執行時建立新 Scope，用完即釋放，避免 Change Tracker 持續累積狀態。

**防重複寫入策略**
使用 HashSet 存放 DB 中已有的 `(StationId, ObservedAt)` 組合，而非只比較單一最新時間欄位。後者在多測站同時回傳相同時間時會出現漏判；HashSet 方式在任何情況下都能正確過濾。部分資料源（`PestAlerts`）使用 SHA256 SourceHash 取代 HashSet，因為去重維度是「內容語意相同」而非「相同時間點」——業務定義不同，策略也不同。

**多 DbContext 架構**
採用 Modular Monolith 設計。每個業務模組有獨立的 DbContext（`WeatherDbContext`、`MarketDbContext`），部署在各自的 Module Project 中，只宣告 Entity 結構，不碰連線字串。連線字串設定與 Worker 啟動統一由入口層（`TaiwanAgri.Worker`、`TaiwanAgri.Web`）的 `Program.cs` 負責組裝，模組本身不感知執行環境。`ApplicationDbContext`（繼承 `IdentityDbContext`）僅在 `TaiwanAgri.Web` 管理 Identity 六張表，與業務 DbContext 完全分離。

**跨 DbContext FK 策略**
`UserNotifications.UserId` 指向 `AspNetUsers.Id`，但兩者分屬不同 DbContext，EF Core 無法建立物理 FOREIGN KEY CONSTRAINT 跨 DbContext 邊界。解法是以 `nvarchar(450)` 純字串欄位作為邏輯 FK，由應用程式層保證值的正確性。同時不加 Navigation Property——一旦加了，EF Core Add-Migration 會把被導航到的 Entity 誤判為自己要管的表，在 Migration 中多建一張孤立的冗餘表。

**Identity Migration 提前策略**
`AspNetUsers` 表在 W5–6 就建立（只需三行設定），讓後續所有 B 類資料表的 `UserId FK` 從一開始就是 `NOT NULL`，不欠技術債。Login UI 和 JWT 在 W15–16 才實作，兩件事獨立進行互不干擾。

**API 端點路徑集中管理**
60 個 MOA API 端點全部定義在 `MoaApiEndpoints.cs` 為 `const string`。散落各處等同於 60 個潛在的手動維護點；集中定義後，IDE 的「尋找所有參考」可以立即找到每個端點的所有使用位置。

---

## 🧪 執行測試

```bash
cd TaiwanAgri.Tests
dotnet test
```

或在 Visual Studio 使用測試總管（Test Explorer）執行。

---

## 📁 相關文件

| 文件 | 說明 |
|------|------|
| `docs/TaiwanAgriPlatform_SA_SD_v10.docx` | SA/SD 完整設計文件（痛點故事、API 清單、DB 設計、Sprint 計畫、W1–W7 實戰開發紀錄） |

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

*最後更新：2026-04 ｜ 對應 SA/SD 文件版本 v10.0*
