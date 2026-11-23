// src/utils/url.ts
// 職責：判斷外部資料的欄位是不是 http(s) 網址（純函式）

/**
 * 字串是不是 http／https 開頭的網址。外部資料的欄位有時是網址、有時是一般文字
 * （例如違規牆的抽樣地點被誤填成銷售頁連結、相簿欄位是空字串），畫成連結之前先判斷；
 * 只放行 http／https，`javascript:` 之類的不會變成可以點的連結。
 * 原本三處各寫一次同一個正則（2026-11 總檢查 1-C1）
 */
export function isHttpUrl(value: string | null | undefined): value is string {
  return !!value && /^https?:\/\//i.test(value)
}
