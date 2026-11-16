import { describe, it, expect } from 'vitest'
import { alertTypeLabel, groupDisasterEvents } from '@/utils/disasterEvents'
import type { DisasterResponseDto } from '@/api/market'

const row = (disasterName: string, alertType: string, alertDate: string, affectedCounties: string[]): DisasterResponseDto =>
  ({ disasterName, alertType, alertDate, affectedCounties })

describe('groupDisasterEvents', () => {
  it('同一場警戒的連續幾天併成一筆，縣市取聯集、天數含頭含尾', () => {
    const events = groupDisasterEvents([
      row('0822豪雨', 'D', '2026-08-23', ['高雄市']),
      row('0822豪雨', 'D', '2026-08-24', ['屏東縣', '高雄市']),
    ])
    expect(events).toEqual([{
      disasterName: '0822豪雨', alertType: 'D', firstDate: '2026-08-23', lastDate: '2026-08-24',
      days: 2, affectedCounties: ['屏東縣', '高雄市'],
    }])
  })

  it('同一場豪雨的土石流與大規模崩塌分開列（原本只取第一筆的類別）', () => {
    const events = groupDisasterEvents([
      row('0822豪雨', 'D', '2026-08-24', ['高雄市']),
      row('0822豪雨', 'L', '2026-08-24', ['臺南市']),
    ])
    expect(events.map(e => [e.alertType, e.affectedCounties])).toEqual([
      ['D', ['高雄市']],
      ['L', ['臺南市']],
    ])
  })

  it('不同年份的同名事件不會被併成一筆', () => {
    const events = groupDisasterEvents([
      row('0728豪雨', 'D', '2025-07-28', ['花蓮縣']),
      row('0728豪雨', 'D', '2026-07-29', ['臺東縣']),
    ])
    expect(events.map(e => [e.firstDate, e.lastDate, e.days])).toEqual([
      ['2025-07-28', '2025-07-28', 1],
      ['2026-07-29', '2026-07-29', 1],
    ])
  })

  it('依開始日期由舊到新排', () => {
    const events = groupDisasterEvents([
      row('B', 'D', '2026-09-01', ['臺東縣']),
      row('A', 'D', '2026-07-01', ['臺東縣']),
    ])
    expect(events.map(e => e.disasterName)).toEqual(['A', 'B'])
  })
})

describe('alertTypeLabel', () => {
  it('D 是土石流、L 是大規模崩塌', () => {
    expect(alertTypeLabel('D')).toBe('土石流')
    expect(alertTypeLabel('L')).toBe('大規模崩塌')
  })
})
