import { describe, it, expect } from 'vitest'
import { getMonthGrid } from '../calendar'

describe('getMonthGrid', () => {
  it('2026 年 2 月：28 天，1 號是星期日，格線要整除 7', () => {
    const weeks = getMonthGrid(2026, 2)
    const flat = weeks.flat()
    expect(flat.length % 7).toBe(0)

    const firstDay = flat.find(c => c.day === 1)
    expect(firstDay?.date).toBe('2026-02-01')
    // 2026-02-01 是星期日，前面不該有留白格
    expect(weeks[0]![0]!.day).toBe(1)

    const realDays = flat.filter(c => c.day !== null)
    expect(realDays.length).toBe(28)
  })

  it('每一列都剛好 7 格', () => {
    const weeks = getMonthGrid(2026, 9)
    for (const week of weeks) expect(week.length).toBe(7)
  })

  it('月初留白格數＝該月 1 號的星期幾', () => {
    // 2026-09-01 是星期二，前面應有 2 個留白格
    const weeks = getMonthGrid(2026, 9)
    const leadingBlanks = weeks[0]!.filter(c => c.day === null).length
    expect(leadingBlanks).toBe(2)
  })

  it('閏年 2 月有 29 天', () => {
    const weeks = getMonthGrid(2028, 2)
    const realDays = weeks.flat().filter(c => c.day !== null)
    expect(realDays.length).toBe(29)
  })
})
