import { describe, it, expect } from 'vitest'
import { buildCsv, buildPricesCsv, csvCell } from '../exportCsv'
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

  it('作物名稱有逗號時加引號，不讓整列欄位錯位', () => {
    const csv = buildPricesCsv([createPrice({ cropName: '甘藍,初秋' })])
    expect(csv.split('\n')[1]).toBe('2026-07-10,LA2,"甘藍,初秋",25.5,30,25,20,1234.5')
  })
})

// 原本直接用逗號相接，名稱裡有逗號、引號或換行的那一列會錯位（2026-11 總檢查 3 號 C 類）
describe('csvCell（一格 CSV 的跳脫）', () => {
  it('沒有特殊字元原樣輸出，跟原本的檔案一樣', () => {
    expect(csvCell('臺北一')).toBe('臺北一')
    expect(csvCell(25.5)).toBe('25.5')
    expect(csvCell(0)).toBe('0')
  })

  it('null 與 undefined 輸出空字串', () => {
    expect(csvCell(null)).toBe('')
    expect(csvCell(undefined)).toBe('')
  })

  it('有逗號或換行時用雙引號包起來', () => {
    expect(csvCell('甲,乙')).toBe('"甲,乙"')
    expect(csvCell('甲\n乙')).toBe('"甲\n乙"')
    expect(csvCell('甲\r\n乙')).toBe('"甲\r\n乙"')
  })

  it('裡面的雙引號寫成兩個（RFC 4180）', () => {
    expect(csvCell('說"明"')).toBe('"說""明"""')
  })
})

describe('buildCsv（表頭＋資料列）', () => {
  it('每一格都經過跳脫，列之間用換行', () => {
    expect(buildCsv(['市場', '價格'], [['甲,乙', 10], ['丙', null]]))
      .toBe('市場,價格\n"甲,乙",10\n丙,')
  })
})
