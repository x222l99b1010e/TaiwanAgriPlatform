// src/utils/disasterEvents.ts
// 職責：把天災警戒端點回的「事件＋日期＋類別」列，併成畫面上一張張的「一場警戒」，
// 天災警戒頁與行情查詢頁共用（原本兩頁各寫一份分組，只看事件名稱）。
import type { DisasterResponseDto } from '@/api/market'

export interface DisasterEvent {
  disasterName: string
  alertType: string
  firstDate: string
  lastDate: string
  /** 含頭含尾：同一天發布又解除也算警戒了一天 */
  days: number
  affectedCounties: string[]
}

/**
 * 同名、同類別的兩筆紀錄，日期相隔超過這個天數就當成不同的兩場。
 * 事件名稱是「0728豪雨」這種只有月日的命名，查詢區間跨年時，不同年份的同名事件
 * 只看名稱會被併成一筆，起訖日期也會從前一年拉到後一年
 */
export const SAME_EVENT_MAX_GAP_DAYS = 30

const DAY_MS = 86_400_000

/**
 * 警戒類別的中文。資料只有兩種（2026-11 本機資料：D 19,180 筆、L 1,134 筆）：
 * D＝土石流潛勢溪流的警戒、L＝大規模崩塌潛勢區的警戒。原本非 D 一律寫成「土石流潛勢」，L 寫錯了
 */
export function alertTypeLabel(alertType: string): string {
  if (alertType === 'D') return '土石流'
  if (alertType === 'L') return '大規模崩塌'
  return alertType
}

function daysBetween(from: string, to: string): number {
  return Math.round((Date.parse(to) - Date.parse(from)) / DAY_MS)
}

/**
 * 依「名稱＋類別」分組，組內依日期排序，相鄰兩天相隔超過 SAME_EVENT_MAX_GAP_DAYS 就切成兩場。
 * 同一場豪雨常同時有土石流與大規模崩塌兩種警戒（2026-11 本機資料 38 組「事件＋日期」裡有 23 組），
 * 兩種分開列，各自的受影響縣市才不會混在一起。回傳依開始日期由舊到新排序
 */
export function groupDisasterEvents(rows: readonly DisasterResponseDto[]): DisasterEvent[] {
  const buckets = new Map<string, DisasterResponseDto[]>()
  for (const row of rows) {
    const key = `${row.disasterName}\u0000${row.alertType}`
    const list = buckets.get(key)
    if (list) list.push(row)
    else buckets.set(key, [row])
  }

  const events: DisasterEvent[] = []
  for (const list of buckets.values()) {
    list.sort((a, b) => a.alertDate.localeCompare(b.alertDate))
    let current: { first: DisasterResponseDto; lastDate: string; counties: Set<string> } | null = null
    const flush = () => {
      if (!current) return
      events.push({
        disasterName: current.first.disasterName,
        alertType: current.first.alertType,
        firstDate: current.first.alertDate,
        lastDate: current.lastDate,
        days: daysBetween(current.first.alertDate, current.lastDate) + 1,
        affectedCounties: [...current.counties].sort(),
      })
    }
    for (const row of list) {
      if (current && daysBetween(current.lastDate, row.alertDate) <= SAME_EVENT_MAX_GAP_DAYS) {
        current.lastDate = row.alertDate
        row.affectedCounties.forEach(c => current!.counties.add(c))
      } else {
        flush()
        current = { first: row, lastDate: row.alertDate, counties: new Set(row.affectedCounties) }
      }
    }
    flush()
  }

  return events.sort((a, b) =>
    a.firstDate.localeCompare(b.firstDate)
    || a.disasterName.localeCompare(b.disasterName)
    || a.alertType.localeCompare(b.alertType))
}
