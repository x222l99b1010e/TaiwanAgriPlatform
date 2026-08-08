// src/utils/leafletIconFix.ts
// 職責：修正 Leaflet 預設圖示在 Vite 打包環境下讀不到圖片的已知問題
//
// 知識點：Leaflet 原生預期圖示路徑用相對 URL 組出來（例如 "images/marker-icon.png"），
// 但 Vite 打包時會把圖片改成雜湊過的檔名（例如 "marker-icon.a1b2c3.png"）並搬到別的路徑，
// Leaflet 自己組的相對路徑就會 404、地圖上完全看不到預設圖釘。
// 解法：用 import 讓 Vite 把這三張圖當成一般資源處理（拿到打包後的正確網址字串），
// 再呼叫 L.Icon.Default.mergeOptions 把這三個網址「餵」回 Leaflet 的預設圖示設定。
// 這是 Vite + Leaflet 整合的通用作法，不是本專案特有的 workaround。

import L from 'leaflet'
import markerIcon2x from 'leaflet/dist/images/marker-icon-2x.png'
import markerIcon from 'leaflet/dist/images/marker-icon.png'
import markerShadow from 'leaflet/dist/images/marker-shadow.png'

let fixed = false

/** 呼叫多次也安全（第二次呼叫直接 return），main.ts 全域呼叫一次即可 */
export function fixLeafletDefaultIcon() {
  if (fixed) return
  fixed = true

  // Leaflet 內部會先嘗試從自己的 _getIconUrl 組路徑，必須先刪掉這個方法，
  // mergeOptions 給的網址才會生效（否則兩者衝突，圖示依然讀不到）
  delete (L.Icon.Default.prototype as unknown as { _getIconUrl?: unknown })._getIconUrl

  L.Icon.Default.mergeOptions({
    iconRetinaUrl: markerIcon2x,
    iconUrl: markerIcon,
    shadowUrl: markerShadow,
  })
}
