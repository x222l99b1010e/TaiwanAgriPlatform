import { describe, it, expect } from 'vitest'
import { getTodaySolarTerm } from '../solarTerms'

describe('getTodaySolarTerm', () => {
  it('2026-09-03 生效中的是處暑，下一個是白露', () => {
    const { current, next, daysUntilNext } = getTodaySolarTerm(new Date(2026, 8, 3))
    expect(current.zh).toBe('處暑')
    expect(current.en).toBe('End of Heat')
    expect(next.zh).toBe('白露')
    expect(daysUntilNext).toBe(5)   // 8/23 → 9/8，9/3 距 9/8 還有 5 天
  })

  it('節氣當天本身算生效中，不是前一個', () => {
    const { current } = getTodaySolarTerm(new Date(2026, 7, 23))   // 8/23＝處暑當天
    expect(current.zh).toBe('處暑')
  })

  it('跨年邊界：1 月初落在大寒與立春之間的小寒／大寒區間，不會抓到明年的立春', () => {
    const { current, next } = getTodaySolarTerm(new Date(2026, 0, 10))   // 1/10
    expect(current.zh).toBe('小寒')   // 1/6
    expect(next.zh).toBe('大寒')      // 1/20
  })

  it('跨年邊界：12 月底落在冬至之後、隔年小寒之前', () => {
    const { current, next, daysUntilNext } = getTodaySolarTerm(new Date(2026, 11, 25))   // 12/25
    expect(current.zh).toBe('冬至')     // 12/22
    expect(next.zh).toBe('小寒')        // 隔年 1/6
    expect(daysUntilNext).toBe(12)
  })

  it('24 個節氣的季節分組跟 style tile §6.2 一致：秋＝立秋至霜降', () => {
    const { current } = getTodaySolarTerm(new Date(2026, 8, 3))
    expect(current.season).toBe('autumn')
  })
})
