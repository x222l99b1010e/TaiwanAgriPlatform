import { describe, it, expect } from 'vitest'
import { cropSuggestions, nextActiveIndex } from '@/utils/cropAutocomplete'

const pool = [
  { cropCode: 'FJ3', cropName: '番茄-牛番茄' },
  { cropCode: 'FJ1', cropName: '番茄-黑柿' },
  { cropCode: 'LA1', cropName: '甘藍-初秋' },
]

describe('cropSuggestions', () => {
  it('名稱包含輸入文字的才列出', () => {
    expect(cropSuggestions(pool, '番茄').map(c => c.cropCode)).toEqual(['FJ3', 'FJ1'])
  })

  it('已選的作物不再出現在建議裡', () => {
    expect(cropSuggestions(pool, '番茄', ['FJ3']).map(c => c.cropCode)).toEqual(['FJ1'])
  })

  it('空白輸入不給建議（不然一聚焦就跳出整份清單）', () => {
    expect(cropSuggestions(pool, '   ')).toEqual([])
  })

  it('最多 limit 筆', () => {
    expect(cropSuggestions(pool, '-', [], 2)).toHaveLength(2)
  })
})

describe('nextActiveIndex', () => {
  it('還沒反白時，↓ 到第一筆、↑ 到最後一筆', () => {
    expect(nextActiveIndex(-1, 3, 1)).toBe(0)
    expect(nextActiveIndex(-1, 3, -1)).toBe(2)
  })

  it('到頭繞回另一端', () => {
    expect(nextActiveIndex(2, 3, 1)).toBe(0)
    expect(nextActiveIndex(0, 3, -1)).toBe(2)
  })

  it('沒有建議時不反白任何一筆', () => {
    expect(nextActiveIndex(-1, 0, 1)).toBe(-1)
  })
})
