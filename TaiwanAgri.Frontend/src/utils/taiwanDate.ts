// src/utils/taiwanDate.ts
// 職責：以台灣時區算出「某個時間點是哪一天」，格式 YYYY-MM-DD（查詢參數用的那種）。
//
// 資料的日界跟後端一致，一律是台灣時區——跟看網頁的人在哪個時區無關，
// 行情、警報都是台灣的日期。
// ⚠ 不要用 toISOString().split('T')[0]：那是 UTC 的日期，台灣時間 00:00–08:00 之間
// 會拿到「昨天」，查詢區間整段往前偏一天，而且白天測試完全看不出來。

// en-CA 的日期格式剛好是 YYYY-MM-DD，不必自己補零拼字串
const formatter = new Intl.DateTimeFormat('en-CA', {
  timeZone: 'Asia/Taipei',
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
})

const DAY_MS = 24 * 60 * 60 * 1000

/** 這個時間點在台灣是哪一天 */
export function taiwanDateString(date: Date = new Date()): string {
  return formatter.format(date)
}

/** 往前推 days 天（以 24 小時為一天）在台灣是哪一天 */
export function taiwanDateDaysAgo(days: number, now: Date = new Date()): string {
  return taiwanDateString(new Date(now.getTime() - days * DAY_MS))
}
