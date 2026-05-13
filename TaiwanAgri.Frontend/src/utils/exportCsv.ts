import type { PriceResponseDto } from '@/api/market'

export function exportPricesToCsv(prices: PriceResponseDto[], filename = 'prices.csv') {
  const header = ['日期', '作物代碼', '作物名稱', '均價', '上價', '中價', '下價', '交易量']
  
    const rows = prices.map(p => [
    p.transDate,
    p.cropCode,
    p.cropName,
    p.avgPrice ?? '',
    p.upperPrice ?? '',
    p.middlePrice ?? '',
    p.lowerPrice ?? '',
    p.transQuantity ?? ''
    ])

  const csvContent = [header, ...rows]
    .map(row => row.join(','))
    .join('\n')

  // 加 BOM，讓 Excel 正確辨識 UTF-8 中文
  const blob = new Blob(['\uFEFF' + csvContent], { type: 'text/csv;charset=utf-8;' })
  const url = URL.createObjectURL(blob)

  const a = document.createElement('a')
  a.href = url
  a.download = filename
  a.click()

  URL.revokeObjectURL(url)
}