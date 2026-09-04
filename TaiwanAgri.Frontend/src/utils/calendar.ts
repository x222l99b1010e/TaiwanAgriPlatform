// src/utils/calendar.ts
// 職責：把「年＋月」算成月曆網格的純函式，不含任何畫面邏輯，方便單元測試。
//
// RestDaysView 原本把休市日列成一排排的 chip，使用者要自己在腦中換算「這天是星期幾」
// 才知道連續休市天數有多長。月曆網格把這件事直接攤開來看。

export interface CalendarCell {
  /** "YYYY-MM-DD"，月份前後的留白格是 null */
  date: string | null
  day: number | null
}

/**
 * 把一個月份算成完整週數的網格（每列 7 天，日到六），月初月末補 null 留白，
 * 讓格線對齊星期幾——這是月曆最基本的視覺要求，缺這一步就只是一排數字。
 */
export function getMonthGrid(year: number, month: number): CalendarCell[][] {
  const first = new Date(year, month - 1, 1)
  const daysInMonth = new Date(year, month, 0).getDate()
  const startWeekday = first.getDay() // 0＝日

  const cells: CalendarCell[] = []
  for (let i = 0; i < startWeekday; i++) cells.push({ date: null, day: null })
  for (let d = 1; d <= daysInMonth; d++) {
    const date = `${year}-${String(month).padStart(2, '0')}-${String(d).padStart(2, '0')}`
    cells.push({ date, day: d })
  }
  // 補到 7 的倍數，最後一週不足的格子也留白，網格才會是矩形
  while (cells.length % 7 !== 0) cells.push({ date: null, day: null })

  const weeks: CalendarCell[][] = []
  for (let i = 0; i < cells.length; i += 7) weeks.push(cells.slice(i, i + 7))
  return weeks
}
