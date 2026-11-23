import type { PriceResponseDto } from '@/api/market'

export type CsvCell = string | number | null | undefined

/**
 * 一格 CSV。內容有逗號、雙引號或換行時用雙引號包起來，裡面的雙引號寫成兩個（RFC 4180）；
 * null／undefined 輸出空字串。原本直接用逗號相接，名稱裡有逗號的那一列整列欄位會錯位
 * （2026-11 總檢查 3 號 C 類；現有資料的名稱沒有逗號，但市場、指標名稱都是上游給的）。
 * 沒有特殊字元的格子原樣輸出，跟原本的檔案一模一樣
 */
export function csvCell(value: CsvCell): string {
  if (value === null || value === undefined) return ''
  const text = String(value)
  return /[",\r\n]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text
}

/** 表頭＋資料列組成 CSV 字串（純函式）。行情、毛豬、家禽三頁共用 */
export function buildCsv(header: string[], rows: CsvCell[][]): string {
  return [header, ...rows].map(row => row.map(csvCell).join(',')).join('\n')
}

/** 觸發瀏覽器下載 CSV。加 BOM，讓 Excel 正確辨識 UTF-8 中文 */
export function downloadCsv(csvContent: string, filename: string) {
  const blob = new Blob(['\uFEFF' + csvContent], { type: 'text/csv;charset=utf-8;' })
  const url = URL.createObjectURL(blob)

  const a = document.createElement('a')
  a.href = url
  a.download = filename
  a.click()

  URL.revokeObjectURL(url)
}

/** CSV 內容組裝（純函式）：抽出來讓字串邏輯與瀏覽器下載觸發分離、可單元測試 */
export function buildPricesCsv(prices: PriceResponseDto[]): string {
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

  return buildCsv(header, rows)
}

export function exportPricesToCsv(prices: PriceResponseDto[], filename = 'prices.csv') {
  downloadCsv(buildPricesCsv(prices), filename)
}
