import { describe, it, expect } from 'vitest'
import { taiwanDateString, taiwanDateDaysAgo } from '@/utils/taiwanDate'

describe('taiwanDateString', () => {
  it('台灣凌晨（UTC 還是前一天）要算成台灣的日期', () => {
    // 2026-10-01 02:29 台灣時間＝2026-09-30 18:29 UTC；用 toISOString 會拿到 09-30
    expect(taiwanDateString(new Date('2026-09-30T18:29:00Z'))).toBe('2026-10-01')
  })

  it('台灣午夜前一秒仍是當天', () => {
    expect(taiwanDateString(new Date('2026-09-30T15:59:59Z'))).toBe('2026-09-30')
  })

  it('格式是 YYYY-MM-DD，月日補零', () => {
    expect(taiwanDateString(new Date('2026-01-05T04:00:00Z'))).toBe('2026-01-05')
  })
})

describe('taiwanDateDaysAgo', () => {
  it('從台灣凌晨往前推 7 天', () => {
    expect(taiwanDateDaysAgo(7, new Date('2026-09-30T18:29:00Z'))).toBe('2026-09-24')
  })

  it('跨月', () => {
    expect(taiwanDateDaysAgo(3, new Date('2026-10-02T04:00:00Z'))).toBe('2026-09-29')
  })
})
