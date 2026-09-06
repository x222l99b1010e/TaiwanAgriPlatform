// subset-font 沒有隨套件提供型別宣告（package.json 只有 main，沒有 types）。
// 專案的型別檢查是嚴格模式、全案 0 個 any，所以這裡補一份最小可用的宣告，
// 而不是用 @ts-ignore 或把回傳值當 any 放過去。
// 只宣告本專案實際用到的部分：吃字型 Buffer 與要保留的字元集，回傳新的字型 Buffer。
declare module 'subset-font' {
  interface SubsetFontOptions {
    /** 輸出格式；本專案只用 woff2（現代瀏覽器都支援，且壓縮率最好） */
    targetFormat?: 'sfnt' | 'woff' | 'woff2'
    variationAxes?: Record<string, { min?: number; max?: number; default?: number }>
  }

  /**
   * @param font 原始字型檔內容
   * @param text 要保留的字元所組成的字串（本專案傳的是各圖示碼位轉成的字元）
   */
  export default function subsetFont(
    font: Buffer,
    text: string,
    options?: SubsetFontOptions,
  ): Promise<Buffer>
}
