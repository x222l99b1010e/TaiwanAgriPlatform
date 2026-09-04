/**
 * src/constants/chartTheme.ts
 * 職責：Chart.js 圖表的配色、座標軸、提示框、圖例、互動行為的單一來源。
 *
 * 五個有圖表的畫面（作物行情／毛豬／家禽／病蟲害旬報／雨量）原本各自抄一份幾乎相同的
 * Chart.js 設定與色盤，同一種折線圖的刻度顏色、格線深淺、字級在各頁略有出入。
 * P2 先把「顏色與各部位樣式」收進來，P3 再把**整個 options 骨架**也收進來（lineChartOptions）
 * ——原本五個檔各自寫一次 `responsive / interaction / scales / plugins`，
 * 差別只有單位字串、x 軸刻度上限、圖例位置這三件事，其餘完全相同。
 * 圖表要改樣式或改互動時只改這一個檔。
 *
 * 顏色不寫死在這裡，一律讀 base.css 的 --cat-* 與語意層：圖表與畫面必須用同一組色，
 * 兩邊各養一份就會分岔。
 *
 * ⚠ 因此下列函式都要在瀏覽器執行期呼叫（buildChart() 當下），不能在模組載入時就取值——
 *   樣式表還沒套上時 getComputedStyle 會回空字串。
 */
import type { ChartOptions, Plugin, Scale } from 'chart.js'

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

/**
 * 第 i 條線的虛線樣式。
 * `--cat-1`～`--cat-3` 彼此的明度差只有 2.56 倍，光靠顏色分不開（色盲使用者更分不開），
 * 所以前三條線各給一種 dash——顏色之外的第二個線索。
 */
export function seriesDash(i: number): number[] {
  const DASHES: number[][] = [[], [6, 3], [2, 3]]
  return DASHES[i % DASHES.length] ?? []
}

/** 資料點外框：比線色淺，點才不會糊成一團 */
export function pointBorderColor(): string {
  return token('--color-border')
}

/** 匯出 PNG 時墊在圖表底下的背景（畫布本身是透明的） */
export function exportBackground(): string {
  return token('--color-surface')
}

/** 事件標註（例如在時間軸上標出災害發生日）用的色 */
export function annotationColor(): string {
  return token('--color-chart-annotation')
}

/** 座標軸刻度文字 */
function axisTicks() {
  return { color: token('--color-text-dim'), font: { size: 12 } }
}

/** 格線：比軸線更淡，資料才是視覺主角 */
function axisGrid() {
  return { color: token('--color-chart-grid') }
}

/** 軸線本身 */
function axisBorder() {
  return { color: token('--color-chart-axis') }
}

/** 提示框。標題用最深的一階、內文淺一階，兩行才分得出主從 */
function tooltipStyle() {
  return {
    backgroundColor: token('--color-surface'),
    titleColor: token('--color-text'),
    bodyColor: token('--color-text-dim'),
    borderColor: token('--color-border'),
    borderWidth: 1,
    padding: 12,
  }
}

/** 圖例文字。usePointStyle 讓圖例的色塊變成圓點，跟線上的資料點是同一個形狀 */
function legendLabels() {
  return {
    color: token('--color-text'),
    font: { size: 13 },
    usePointStyle: true,
    pointStyleWidth: 10,
  }
}

/**
 * 動效時間。查詢頁的重繪屬於「工作檔」，跟著 --duration-base 走，
 * 不用 Chart.js 預設的 1000ms——一天按五十次的東西上，慢就是卡。
 * `prefers-reduced-motion` 開啟時整個關掉。
 */
function chartAnimation(): { duration: number } | false {
  if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return false
  return { duration: Number.parseFloat(token('--duration-base')) || 170 }
}

/**
 * hover 時的垂直指示線。
 * Chart.js 沒有內建，但 `interaction.mode: 'index'` 已經在整欄上取點，
 * 缺的只是「那一欄在哪裡」的視覺線索——尤其是資料點被壓在圖表邊緣時。
 *
 * ⚠ 只有單一序列時才畫水平線。多序列的 index 模式一次命中好幾個 y，
 *   水平線要嘛畫錯一條、要嘛畫一堆，兩種都比不畫更糟。
 */
export const crosshairPlugin: Plugin<'line'> = {
  id: 'crosshair',
  beforeDatasetsDraw(chart) {
    // 用 chart.getActiveElements()（Chart 自己的公開 API），不是 chart.tooltip 的同名方法：
    // 前者不依賴 Tooltip 外掛有沒有註冊，這個外掛就能單獨用在關掉提示框的圖上。
    const active = chart.getActiveElements()
    const first = active[0]
    if (!first) return

    const { ctx, chartArea } = chart
    ctx.save()
    ctx.beginPath()
    ctx.setLineDash([3, 3])
    ctx.lineWidth = 1
    ctx.strokeStyle = token('--color-chart-crosshair')
    ctx.moveTo(first.element.x, chartArea.top)
    ctx.lineTo(first.element.x, chartArea.bottom)
    if (active.length === 1) {
      ctx.moveTo(chartArea.left, first.element.y)
      ctx.lineTo(chartArea.right, first.element.y)
    }
    ctx.stroke()
    ctx.restore()
  },
}

export interface LineOptionsSpec {
  /** y 軸刻度與提示框的單位，例如「元」「mm」。不給就不加單位 */
  unit?: string
  /** x 軸最多幾個刻度。日期軸太密會疊字，預設 12 */
  maxTicksLimit?: number
  /** 圖例位置；false ＝ 不顯示 */
  legend?: 'top' | 'bottom' | false
  /**
   * y 軸貼著資料範圍：上下各留這麼多再取整，取代 Chart.js 從 0 附近起跳的預設。
   * **價格型圖表一定要開**——七日之內幾個百分點的變化，軸從 0 起跳會被壓成三條直線，
   * 讀者看到的是「沒有變化」，但實際上有。不給就維持 Chart.js 的自動範圍。
   */
  fitY?: number
}

/**
 * 折線圖的共用 options。五個畫面的差別只有 spec 裡這四項，其餘一律相同。
 * 回傳的是新物件，呼叫端可以再覆蓋個別欄位（例如 PriceChart 的災害標註）。
 */
export function lineChartOptions(spec: LineOptionsSpec = {}): ChartOptions<'line'> {
  const { unit = '', maxTicksLimit = 12, legend = 'top', fitY } = spec
  const suffix = unit ? ` ${unit}` : ''

  return {
    responsive: true,
    maintainAspectRatio: false,
    animation: chartAnimation(),
    interaction: { mode: 'index', intersect: false },
    scales: {
      x: {
        ticks: {
          maxTicksLimit,
          ...axisTicks(),
          callback(this: Scale, val, index) {
            return this.getLabelForValue(index) ?? String(val)
          },
        },
        grid: axisGrid(),
        border: axisBorder(),
      },
      y: {
        ticks: {
          ...axisTicks(),
          callback: (val) => `${val}${suffix}`,
        },
        grid: axisGrid(),
        border: axisBorder(),
        ...(fitY === undefined
          ? {}
          : {
              afterDataLimits(scale: Scale) {
                if (!Number.isFinite(scale.min) || !Number.isFinite(scale.max)) return
                scale.min = Math.floor(scale.min - fitY)
                scale.max = Math.ceil(scale.max + fitY)
              },
            }),
      },
    },
    plugins: {
      tooltip: {
        ...tooltipStyle(),
        callbacks: {
          label: (ctx) =>
            ctx.parsed.y !== null ? ` ${ctx.dataset.label}：${ctx.parsed.y}${suffix}` : '',
        },
      },
      legend:
        legend === false
          ? { display: false }
          : { position: legend, labels: legendLabels() },
    },
  }
}
