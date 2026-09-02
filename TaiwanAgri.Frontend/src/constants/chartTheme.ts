/**
 * src/constants/chartTheme.ts
 * 職責：Chart.js 圖表的配色、座標軸、提示框、圖例樣式的單一來源。
 *
 * 五個有圖表的畫面（作物行情／毛豬／家禽／病蟲害旬報／雨量）原本各自抄一份幾乎相同的
 * Chart.js 設定與色盤，同一種折線圖的刻度顏色、格線深淺、字級在各頁略有出入。
 * 圖表要改樣式時只改這一個檔。
 *
 * 顏色不寫死在這裡，一律讀 base.css 的 --cat-* 與中性色階：圖表與畫面必須用同一組色，
 * 兩邊各養一份就會分岔。
 *
 * ⚠ 因此下列函式都要在瀏覽器執行期呼叫（buildChart() 當下），不能在模組載入時就取值——
 *   樣式表還沒套上時 getComputedStyle 會回空字串。
 */

/** 分類色的階數，與 base.css 的 --cat-1 ~ --cat-17 對應 */
const CAT_COUNT = 17

function token(name: string): string {
  return getComputedStyle(document.documentElement).getPropertyValue(name).trim()
}

/**
 * 把 token 取到的 #rrggbb 加上透明度。
 * 不用 color-mix() 是因為要傳給 canvas，Chart.js 會把字串直接交給 2D context，
 * 那裡只吃 rgba()。
 */
export function withAlpha(color: string, alpha: number): string {
  const hex = /^#([0-9a-f]{6})$/i.exec(color)
  if (!hex) return color
  const n = Number.parseInt(hex[1]!, 16)
  return `rgba(${(n >> 16) & 255}, ${(n >> 8) & 255}, ${n & 255}, ${alpha})`
}

/** 第 i 條線的顏色（超過分類色階數就從頭循環） */
export function seriesColor(i: number): string {
  return token(`--cat-${(i % CAT_COUNT) + 1}`)
}

/** 面積圖的填色：同色但極淡，才不會蓋掉底下的格線 */
export function seriesFill(i: number): string {
  return withAlpha(seriesColor(i), 0.08)
}

/** 輔助線（例如移動平均虛線）：同色但比主線淡，讀者才分得出哪條是原始資料 */
export function seriesAccent(i: number): string {
  return withAlpha(seriesColor(i), 0.28)
}

/** 資料點外框：比線色淺，點才不會糊成一團 */
export function pointBorderColor(): string {
  return token('--neutral-300')
}

/** 匯出 PNG 時墊在圖表底下的背景（畫布本身是透明的） */
export function exportBackground(): string {
  return token('--neutral-0')
}

/** 座標軸刻度文字 */
export function axisTicks() {
  return { color: token('--neutral-600'), font: { size: 12 } }
}

/** 格線：比軸線更淡，資料才是視覺主角 */
export function axisGrid() {
  return { color: token('--neutral-100') }
}

/** 軸線本身 */
export function axisBorder() {
  return { color: token('--neutral-200') }
}

/** 提示框。標題用最深的一階、內文淺一階，兩行才分得出主從 */
export function tooltipStyle() {
  return {
    backgroundColor: token('--neutral-0'),
    titleColor: token('--neutral-900'),
    bodyColor: token('--neutral-600'),
    borderColor: token('--neutral-200'),
    borderWidth: 1,
    padding: 12,
  }
}

/** 圖例文字 */
export function legendLabels() {
  return {
    color: token('--neutral-700'),
    font: { size: 13 },
    usePointStyle: true,
    pointStyleWidth: 10,
  }
}

/** 事件標註（例如在時間軸上標出災害發生日）用的色 */
export function annotationColor(): string {
  return token('--warning-500')
}
