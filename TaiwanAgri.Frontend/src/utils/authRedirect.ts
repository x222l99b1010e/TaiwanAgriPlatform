// src/utils/authRedirect.ts
// 職責：登入過期（原本有 token 的請求收到 401）時，決定要不要把人帶去登入頁。
// 登入狀態一律先清掉（導覽列變回「登入」）；這裡只回答「要不要換頁」。
import type { LocationQueryValue, RouteLocationNormalizedLoaded } from 'vue-router'

/**
 * 只有「要登入的頁面」才帶去登入頁，公開頁留在原地。
 *
 * - 要登入的頁面（`meta.requiresAuth`）：要。不換頁的話那一頁什麼都載不出來，
 *   使用者看到一片「載入失敗」，也不知道原因只是登入過期。
 * - 公開頁：不要。公開頁的資料不必登入就查得到（後端對匿名端點不會因為帶了過期的 token 就回 401），
 *   會收到 401 的只有兩種：背景的鈴鐺輪詢，以及使用者在這一頁按的、要登入的動作（例如張貼協尋啟事）。
 *   前者把正在看行情的訪客拉去登入頁，像是被趕走（2026-11 總檢查 2-B10）；
 *   後者由那個動作自己的錯誤訊息說「登入已過期」，人留在原頁、剛填的內容也還在。
 *
 * 讀的是合併過的 `route.meta`，跟路由守衛判斷「這頁要不要登入」用的是同一個欄位，兩邊不會各說各話。
 */
export function shouldRedirectToLogin(route: Pick<RouteLocationNormalizedLoaded, 'name' | 'meta'>): boolean {
  if (route.name === 'login') return false
  return route.meta.requiresAuth === true
}

/**
 * 登入成功之後回到哪一頁：網址上 `?redirect=` 的值，沒有就回首頁。
 *
 * 查詢字串帶了兩次 `redirect`（`?redirect=/a&redirect=/b`）時 Vue Router 給的是陣列，
 * 原本寫 `route.query.redirect as string`，轉型把這個可能蓋掉了（2026-11 總檢查 G-C24）；取第一個字串。
 * 導回用的是 `router.push`，只會在站內換頁
 */
export function redirectAfterLogin(value: LocationQueryValue | LocationQueryValue[] | undefined): string {
  const first = Array.isArray(value) ? value[0] : value
  return first || '/'
}
