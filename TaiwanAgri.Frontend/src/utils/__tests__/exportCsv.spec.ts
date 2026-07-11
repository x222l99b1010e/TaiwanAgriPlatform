import { describe, it, expect } from 'vitest'
import { buildPricesCsv } from '../exportCsv'
import type { PriceResponseDto } from '@/api/market'

const HEADER = '日期,作物代碼,作物名稱,均價,上價,中價,下價,交易量'

function createPrice(overrides: Partial<PriceResponseDto> = {}): PriceResponseDto {
  return {
    transDate: '2026-07-10',
    cropCode: 'LA2',
    cropName: '甘藍',
    avgPrice: 25.5,
    upperPrice: 30,
    middlePrice: 25,
    lowerPrice: 20,
    transQuantity: 1234.5,
    ...overrides,
  }
}

describe('buildPricesCsv', () => {
  it('空陣列：只輸出表頭列', () => {
    expect(buildPricesCsv([])).toBe(HEADER)
  })

  it('單筆資料：表頭 + 一列，欄位順序與資料對應', () => {
    const csv = buildPricesCsv([createPrice()])

    expect(csv).toBe(
      HEADER + '\n' + '2026-07-10,LA2,甘藍,25.5,30,25,20,1234.5'
    )
  })

  it('多筆資料：每筆一列、以換行分隔', () => {
    const csv = buildPricesCsv([
      createPrice(),
      createPrice({ transDate: '2026-07-09', cropCode: 'SE1', cropName: '菠菜' }),
    ])

    const lines = csv.split('\n')
    expect(lines).toHaveLength(3)
    expect(lines[1]).toContain('LA2')
    expect(lines[2]).toContain('SE1')
  })

  it('價格欄位為 null 時輸出空字串，不輸出 "null"', () => {
    // DTO 型別上是 number，但實際 API 可能回 null（?? 的存在理由），用 cast 模擬
    const price = createPrice({
      avgPrice: null as unknown as number,
      transQuantity: null as unknown as number,
    })

    const csv = buildPricesCsv([price])
    const dataLine = csv.split('\n')[1]

    expect(dataLine).toBe('2026-07-10,LA2,甘藍,,30,25,20,')
    expect(dataLine).not.toContain('null')
  })
})
