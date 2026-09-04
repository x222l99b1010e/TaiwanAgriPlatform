/**
 * src/utils/solarTerms.ts
 * 職責：24 節氣的查表與「今天是哪個節氣、下一個節氣還有幾天」計算。
 *
 * 設計定案：節氣只進內容層，不進 token 層——這裡只回傳文字與季節分組，
 * 不碰任何顏色 token。日期用查表，不打氣象署 API：節氣是固定的天文事件，
 * 為了一行字多一個外部相依不划算。
 *
 * 每個節氣的西曆日期年年只在 ±1 天內漂移（跟置閏有關），這裡取的是最常見的
 * 那個日期，對「首頁一張裝飾用的節氣牌」精度足夠；不是拿來算農民曆或做節氣
 * 相關的商業邏輯判斷。
 */

export type Season = 'spring' | 'summer' | 'autumn' | 'winter'

export interface SolarTerm {
  /** 一年中的順序，1～24，立春為 1 */
  index: number
  zh: string
  en: string
  /** 西曆月，1～12 */
  month: number
  /** 西曆日 */
  day: number
  season: Season
}

// 資料來源：24 節氣定譯；日期是通用的西曆對照表
export const SOLAR_TERMS: SolarTerm[] = [
  { index: 1,  zh: '立春', en: 'Start of Spring',      month: 2,  day: 4,  season: 'spring' },
  { index: 2,  zh: '雨水', en: 'Rain Water',            month: 2,  day: 19, season: 'spring' },
  { index: 3,  zh: '驚蟄', en: 'Awakening of Insects',  month: 3,  day: 6,  season: 'spring' },
  { index: 4,  zh: '春分', en: 'Spring Equinox',        month: 3,  day: 21, season: 'spring' },
  { index: 5,  zh: '清明', en: 'Pure Brightness',       month: 4,  day: 5,  season: 'spring' },
  { index: 6,  zh: '穀雨', en: 'Grain Rain',            month: 4,  day: 20, season: 'spring' },
  { index: 7,  zh: '立夏', en: 'Start of Summer',       month: 5,  day: 6,  season: 'summer' },
  { index: 8,  zh: '小滿', en: 'Grain Buds',            month: 5,  day: 21, season: 'summer' },
  { index: 9,  zh: '芒種', en: 'Grain in Ear',          month: 6,  day: 6,  season: 'summer' },
  { index: 10, zh: '夏至', en: 'Summer Solstice',       month: 6,  day: 21, season: 'summer' },
  { index: 11, zh: '小暑', en: 'Minor Heat',            month: 7,  day: 7,  season: 'summer' },
  { index: 12, zh: '大暑', en: 'Major Heat',            month: 7,  day: 23, season: 'summer' },
  { index: 13, zh: '立秋', en: 'Start of Autumn',       month: 8,  day: 8,  season: 'autumn' },
  { index: 14, zh: '處暑', en: 'End of Heat',           month: 8,  day: 23, season: 'autumn' },
  { index: 15, zh: '白露', en: 'White Dew',             month: 9,  day: 8,  season: 'autumn' },
  { index: 16, zh: '秋分', en: 'Autumn Equinox',        month: 9,  day: 23, season: 'autumn' },
  { index: 17, zh: '寒露', en: 'Cold Dew',              month: 10, day: 8,  season: 'autumn' },
  { index: 18, zh: '霜降', en: "Frost's Descent",       month: 10, day: 24, season: 'autumn' },
  { index: 19, zh: '立冬', en: 'Start of Winter',       month: 11, day: 8,  season: 'winter' },
  { index: 20, zh: '小雪', en: 'Minor Snow',            month: 11, day: 22, season: 'winter' },
  { index: 21, zh: '大雪', en: 'Major Snow',            month: 12, day: 7,  season: 'winter' },
  { index: 22, zh: '冬至', en: 'Winter Solstice',       month: 12, day: 22, season: 'winter' },
  { index: 23, zh: '小寒', en: 'Minor Cold',            month: 1,  day: 6,  season: 'winter' },
  { index: 24, zh: '大寒', en: 'Major Cold',            month: 1,  day: 20, season: 'winter' },
]

function asDate(year: number, term: SolarTerm): Date {
  return new Date(year, term.month - 1, term.day)
}

/** 把日期歸零到當天 00:00，只比較日期不比較時分秒 */
function atMidnight(d: Date): Date {
  return new Date(d.getFullYear(), d.getMonth(), d.getDate())
}

/**
 * 今天生效中的節氣（今天落在這個節氣的起始日之後、下一個節氣之前）
 * ＋下一個節氣與距今天數。
 */
export function getTodaySolarTerm(today: Date = new Date()): {
  current: SolarTerm
  next: SolarTerm
  daysUntilNext: number
} {
  const t = atMidnight(today)
  const year = t.getFullYear()

  // 展開成前一年 12/22（去年冬至前後）到明年年初的連續序列，
  // 這樣「今天在最後一個節氣（大寒）跟明年立春之間」這種跨年邊界不用特殊處理
  const timeline = [
    ...SOLAR_TERMS.map(term => ({ term, date: asDate(year - 1, term) })),
    ...SOLAR_TERMS.map(term => ({ term, date: asDate(year, term) })),
    ...SOLAR_TERMS.map(term => ({ term, date: asDate(year + 1, term) })),
  ].sort((a, b) => a.date.getTime() - b.date.getTime())

  // 今天生效中的節氣＝最後一個「日期 <= 今天」的節氣
  let currentIdx = 0
  for (let i = 0; i < timeline.length; i++) {
    if (timeline[i]!.date.getTime() <= t.getTime()) currentIdx = i
    else break
  }
  const nextEntry = timeline[currentIdx + 1]!
  const msPerDay = 24 * 60 * 60 * 1000

  return {
    current: timeline[currentIdx]!.term,
    next: nextEntry.term,
    daysUntilNext: Math.round((nextEntry.date.getTime() - t.getTime()) / msPerDay),
  }
}
