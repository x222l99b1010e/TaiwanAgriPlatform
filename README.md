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

### 🌤️ 模組 2：智慧青農戰情室
面向返鄉青農，整合即時氣象、病蟲害警報與市場行情，透過規則引擎主動推播智慧提示。

- 農場氣象面板（依縣市 + 海拔篩選最近測站）
- 雨量趨勢圖（7 天歷史，Chart.js）
- 病蟲害警報牆（依作物過濾，摘要自動截取）
- **★ 智慧病蟲害提示**：規則引擎偵測「連續 72 小時濕度 > 85%」等條件，主動推送通知
- 農藥查詢（中文俗名 → 學名 → 許可證字號，跨三支 API 橋接）

### 📊 模組 4：大數據探險 — 天災與菜價關聯分析
面向研究者，用歷史資料找出天氣事件與農產品批發價格之間的連動規律。

- 作物歷史價格圖 + 7 日移動平均線（SQL Window Functions）
- 天災事件時間軸疊加（土石流 / 豪雨 / 颱風警戒）
- 事件前後漲跌幅分析（LAG / LEAD 函數）
- 休市日標記（排除統計陷阱）
- 數據 CSV 匯出（串流輸出）

### 🛒 模組 1：台灣生鮮物價與食安透明網
面向一般消費者，今日物價查詢 + 食安追溯核查。

- 今日物價首頁（毫秒級回應，Redis Cache-Aside Pattern）
- 追溯碼查詢（支援 QR Code 掃描）
- 農藥殘留違規警示牆（近 90 天不合格名單）
- 有機認證查詢 / CAS 標章查詢
- RabbitMQ 非同步推播架構（Publisher → Exchange → Queue → Consumer）

### 🐾 模組 3：毛小孩守護地圖
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
│    WeatherSyncWorker | PestAlertSyncWorker                  │
│    RainfallSyncWorker | PestDecadeSyncWorker                │
│    PriceHistoryWorker | DisasterEventWorker                 │
└──────────┬──────────────────────┬───────────────────────────┘
           │ EF Core              │ RabbitMQ
           ▼                      ▼
┌──────────────────┐    ┌────────────────────────────────────┐
│   SQL Server     │    │         RabbitMQ                   │
│   2022           │    │   Exchange: agri.topic             │
│                  │    │   RoutingKey: agri.price.updated   │
└──────────────────┘    └────────────────┬───────────────────┘
                                         │ Subscribe
                                         ▼
┌─────────────────────────────────────────────────────────────┐
│                  TaiwanAgri.Web                             │
│    ASP.NET Core 8 Web API + MVC                             │
│    IHostedService (RabbitMQ Consumer)                       │
│    Controller → Service → Repository                        │
└──────────┬──────────────────────┬───────────────────────────┘
           │ Cache-Aside           │ HTTP JSON
           ▼                      ▼
┌──────────────────┐    ┌─────────────────────────────────────┐
│      Redis       │    │         Vue 3 Frontend              │
│   TTL 25hr       │    │  Vite + TailwindCSS + Chart.js      │
│   (StackExch.)   │    │  Leaflet.js + Pinia                 │
└──────────────────┘    └─────────────────────────────────────┘
```

### Solution 結構

```
TaiwanAgriPlatform/
├── TaiwanAgriPlatform.sln
├── docker-compose.yml
├── .env                              # 敏感設定（不進版控）
│
├── TaiwanAgri.Core/                  # 共用 Interface / DTO / Enum / Constants
│   └── Constants/
│       └── MoaApiEndpoints.cs        # 60 個 API 端點路徑集中定義
│
├── TaiwanAgri.Modules.Weather/       # 模組 2：氣象 + 病蟲害後端
├── TaiwanAgri.Modules.Market/        # 模組 4 + 1：行情分析後端
├── TaiwanAgri.Modules.FoodSafety/    # 模組 1：食安追溯後端
├── TaiwanAgri.Modules.Pet/           # 模組 3：寵物模組後端
│
├── TaiwanAgri.Worker/                # .NET Worker Service（所有排程爬蟲）
├── TaiwanAgri.Web/                   # ASP.NET Core Web API + Vue 3 Shell
└── TaiwanAgri.Tests/                 # xUnit + Moq + TestContainers
```

---

## 🛠️ 技術堆疊

| 層次 | 技術 | 版本 | 用途 |
|------|------|------|------|
| 後端框架 | ASP.NET Core Web API + MVC | 8.0 LTS | 主要後端框架 |
| ORM | Entity Framework Core | 10.0 | Code First + Migration |
| 資料庫 | SQL Server | 2022 | Window Functions、時序查詢 |
| 背景排程 | .NET Worker Service + Hangfire | 最新穩定版 | 資料同步排程 |
| 訊息佇列 | RabbitMQ | 3.13 | 非同步事件推播 |
| 快取 | Redis + IMemoryCache | 7.x | 首頁物價秒讀 |
| 身分驗證 | ASP.NET Core Identity + JWT | 8.0 | 使用者認證 |
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
    "WeatherDb": "Server=你的伺服器;Database=TaiwanAgriPlatform;User Id=你的帳號;Password=你的密碼;TrustServerCertificate=True"
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

等待約 30-60 秒，確認所有服務健康：

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

在 Visual Studio 的套件管理員主控台執行（**注意：使用單行指令，不要換行**）：

```powershell
Update-Database -Context WeatherDbContext -StartupProject TaiwanAgri.Worker
```

Migration 執行完成後，用 SQL Server 物件總管確認 `TaiwanAgriPlatform` 資料庫和各資料表已建立。

### Step 5：啟動應用程式

在 Visual Studio 將啟動專案設定為 `TaiwanAgri.Worker`，按 F5 啟動。

Hangfire Dashboard 可在 `http://localhost:5000/hangfire` 查看排程狀態。

---

## 🗄️ 資料庫設計摘要

本專案資料表分三類型：

**A 類：API 快取表**（從農業部 API 落地的本地複本）
`WeatherObservations` | `RainfallObservations` | `PestAlerts` | `PriceHistory` | `ShelterAnimals` ...等

**B 類：使用者資料表**（需與 AspNetUsers.Id 建立 FK）
`UserFarmProfiles` | `UserWatchlist` | `UserNotifications` | `LostPetReports`

**C 類：系統設定表**（控制系統行為，只有 Admin 能修改）
`PestRuleConfig` | `SyncScheduleLog`

完整 26 張資料表設計請參考 [SA/SD 文件 第 5.4 節](docs/TaiwanAgriPlatform_SA_SD_v5.docx)。

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

> **重要限制**：免費帳號分頁 API 只回傳第一頁資料（每頁最多 1000 筆）。
> 程式碼中保留分頁迴圈，當 API 回傳 `RS: "ERROR"` 時會優雅地 `break`，不影響正常運作。

申請帳號後在 `.env` 的 `MOA_API_KEY` 欄位填入即可啟用付費功能。

所有 60 個 API 端點路徑定義在：`TaiwanAgri.Core/Constants/MoaApiEndpoints.cs`

---

## ⏱️ 開發進度

| Sprint | 階段 | 狀態 |
|--------|------|------|
| W1-2 | Docker 基礎設施 + Solution 架構 + 第一個 Migration | ✅ 完成 |
| W3-4 | WeatherSyncWorker（含分頁、防重複、30 天清除） | ✅ 完成 |
| W5-6 | Identity Migration + RainfallSyncWorker + PestAlertSyncWorker + 規則引擎 | 🔄 進行中 |
| W7-8 | 模組 2 Vue 3 前台 | ⬜ 待開始 |
| W9-10 | 模組 4 後端（價格歷史 + Window Functions） | ⬜ 待開始 |
| W11-12 | 模組 4 前台（圖表 + 天災標記） | ⬜ 待開始 |
| W13-14 | 模組 1（RabbitMQ + Redis + 食安功能） | ⬜ 待開始 |
| W15-16 | 完整身分驗證（JWT + Login UI） | ⬜ 待開始 |
| W17-18 | 模組 3（Leaflet 地圖 + 寵物功能） | ⬜ 待開始 |
| W19-20 | 整合優化 + 測試 + CI/CD | ⬜ 待開始 |

---

## 🧠 關鍵架構決策記錄

這些決策在開發過程中從真實問題推導出來，詳細推論記錄在 SA/SD 文件第 12 章。

**BackgroundService 生命週期管理**
`WeatherSyncWorker` 繼承 `BackgroundService`，被 DI 容器以 Singleton 管理。`WeatherDbContext` 是 Scoped。不能直接在建構子注入 DbContext，必須注入 `IServiceScopeFactory`，在每次同步任務執行時建立新 Scope，用完即釋放。

**防重複寫入策略**
使用 HashSet 存放 DB 中已有的 `(StationId, ObservedAt)` 組合，而非只比較單一最新時間欄位。後者在多測站同時回傳相同時間時會出現漏判，HashSet 方式在任何情況下都能正確過濾。

**Identity Migration 提前策略**
AspNetUsers 表在 W5-6 就建立（只需三行設定），讓後續所有 B 類資料表的 `UserId FK` 從一開始就是 `NOT NULL`，不欠技術債。Login UI 和 JWT 在 W15-16 才實作，兩件事是獨立的步驟。

**所有 API 端點路徑集中管理**
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
| `docs/TaiwanAgriPlatform_SA_SD_v5.docx` | SA/SD 完整設計文件（含痛點故事、API 清單、DB 設計、Sprint 計畫、實戰開發紀錄） |

---

## 📝 開發慣例

**每完成一個功能，在 GitHub 寫一篇 PR Description，記錄：**
1. 這個功能解決了什麼問題
2. 為什麼這樣設計，而不是另一種方式
3. 遇到什麼坑，怎麼解決

這本身就是最好的 SA/SD 練習，也是面試時最具說服力的履歷素材。

---

## 📄 License

MIT License — 詳見 [LICENSE](LICENSE) 檔案。

---

*最後更新：2026-04 | v5.0 對應 SA/SD 文件版本*
