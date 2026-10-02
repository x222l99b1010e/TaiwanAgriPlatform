# TaiwanAgri.Frontend

田野‧農時的前端：Vue 3 + Vite + TypeScript + Pinia + Vue Router。
整個系統的架構、本機啟動順序與雲端部署步驟寫在 repo 根目錄的 [README](../README.md)，這裡只放前端自己的指令與約定。

## 指令

| 指令 | 做什麼 |
|---|---|
| `npm install` | 安裝相依（CI 用 `npm ci`，照 `package-lock.json` 原樣安裝） |
| `npm run dev` | 開發伺服器 `http://localhost:5173`；`/api` 與 `/health` 由 Vite proxy 轉給 `https://localhost:7147`，同源請求、不經過 CORS |
| `npm test` | Vitest，環境是 Node：元件測試以 `vue/server-renderer` 算成 HTML 字串做結構斷言，不需要 jsdom |
| `npm run lint` | oxlint ＋ ESLint ＋ 未定義 CSS 變數檢查，**三項都唯讀**；要自動修正用 `npm run lint:fix`（只在本機用，CI 不用） |
| `npm run type-check` | `vue-tsc` 型別檢查 |
| `npm run build` | 型別檢查＋正式建置，產出 `dist/` |

## 後端網址

正式建置的 API 位址來自 `VITE_API_BASE_URL`，**在建置當下寫死進 JS**，後端網址改了就要重新 build。
雲端部署的寫法（含 Windows PowerShell 5.1 的編碼陷阱，以及建置後到 `dist` 數後端主機名的驗證）
見根目錄 README「雲端部署與重新部署」第 5 步。本機開發時請求走同源的 `/api`，由開發伺服器轉發。
請求等超過 8 秒時，畫面下方會說明卡在伺服器還是資料庫（`src/api/serverWake.ts`，另打 `/health` 與 `/health/ready` 判斷）。

## 約定

- 顏色、字級、間距、圓角、動效一律引用 `src/assets/base.css` 的 token，不在元件裡寫死數值；
  引用了未定義的 CSS 變數時 `npm run lint` 會擋下來。
- 「今天是哪一天」一律用 `src/utils/taiwanDate.ts`（台灣時區）；`toISOString().split('T')[0]` 拿到的是 UTC 日期，ESLint 會擋。
- 網站圖示的原稿是 `public/favicon.svg`，`favicon.ico` 與 `apple-touch-icon.png` 由它點陣化；導覽列、登入卡、頁尾的標誌也直接引用它。
