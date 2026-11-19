import { describe, it, expect } from 'vitest'
import { formatLostPetPostDate } from '@/utils/lostPetPost'

describe('formatLostPetPostDate', () => {
  it('台灣凌晨張貼的啟事（UTC 還是前一天）顯示台灣的日期', () => {
    // 2026-11-13 04:53 台灣時間＝2026-11-12 20:53 UTC；切前 10 個字會拿到 11-12
    expect(formatLostPetPostDate('2026-11-12T20:53:41.4405544Z')).toBe('2026-11-13')
  })

  it('台灣白天張貼的日期不變', () => {
    expect(formatLostPetPostDate('2026-08-05T04:00:00Z')).toBe('2026-08-05')
  })
})
