/**
 * 四個頁面樣板的結構測試。
 *
 * 用 vue/server-renderer 直接算成 HTML 字串，不引入 jsdom 或 @vue/test-utils——
 * 這幾條要驗的是「插槽有沒有接對、區塊的先後順序對不對」，那些在字串上就看得出來，
 * 為此多兩個測試相依不划算。要驗互動（sticky 的陰影、地圖點選）得用實機。
 *
 * 順序之所以要測：這幾個樣板的價值有一半在「同一件事在每頁的位置一樣」，
 * 而位置錯了不會壞掉、只會變得跟以前一樣各頁不同，自動檢查抓不到就沒人會發現。
 */
import { describe, it, expect } from 'vitest'
import { createSSRApp, h, type Component } from 'vue'
import { renderToString } from 'vue/server-renderer'
import { createRouter, createMemoryHistory } from 'vue-router'

import Bilingual from '@/components/ui/Bilingual.vue'
import QueryLayout from '@/components/layouts/QueryLayout.vue'
import DetailLayout from '@/components/layouts/DetailLayout.vue'
import MapLayout from '@/components/layouts/MapLayout.vue'
import EntryLayout from '@/components/layouts/EntryLayout.vue'

/** 把元件算成 HTML。DetailLayout 用到 RouterLink，所以一律掛一個記憶體路由。 */
async function render(
  component: Component,
  props: Record<string, unknown> = {},
  slots: Record<string, string> = {},
) {
  const app = createSSRApp({
    render: () =>
      h(
        component,
        props,
        Object.fromEntries(
          Object.entries(slots).map(([name, text]) => [name, () => h('i', text)]),
        ),
      ),
  })
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [{ path: '/', component: { template: '<div />' } }],
  })
  app.use(router)
  await router.push('/')
  await router.isReady()
  return renderToString(app)
}

/** 兩個標記字串在 HTML 裡的先後。用來驗區塊順序。 */
function before(html: string, first: string, second: string) {
  const a = html.indexOf(first)
  const b = html.indexOf(second)
  expect(a).toBeGreaterThan(-1)
  expect(b).toBeGreaterThan(-1)
  return a < b
}

describe('Bilingual', () => {
  it('英文是裝飾，一律對螢幕閱讀器隱藏', async () => {
    const html = await render(Bilingual, { zh: '市場行情', en: 'Market Prices' })
    expect(html).toContain('市場行情')
    expect(html).toMatch(/aria-hidden="true"[^>]*>Market Prices|Market Prices/)
    expect(html).toContain('aria-hidden="true"')
  })

  it('沒給英文時不留空節點', async () => {
    const html = await render(Bilingual, { zh: '市場行情' })
    expect(html).toContain('市場行情')
    expect(html).not.toContain('bilingual__en')
  })

  it('stacked 與 inline 是兩種排法，不是同一個 class', async () => {
    const stacked = await render(Bilingual, { zh: '病蟲害警報', en: 'Pest Alerts', layout: 'stacked' })
    const inline = await render(Bilingual, { zh: '病蟲害警報', en: 'Pest Alerts' })
    expect(stacked).toContain('bilingual--stacked')
    expect(inline).toContain('bilingual--inline')
  })
})

describe('QueryLayout', () => {
  it('動作按鈕排在查詢條件列的尾端，不是頂部', async () => {
    // P3 改版：查詢鈕落在條件的最後一格、跟日期同一列（owner 要求「選完日期
    // 查詢鈕就在旁邊」），所以 HTML 順序是 filters 在前、actions 在後。
    const html = await render(
      QueryLayout,
      { title: '家禽行情', titleEn: 'Poultry Prices' },
      { actions: '查詢價格', filters: '日期區間' },
    )
    expect(before(html, '日期區間', '查詢價格')).toBe(true)
  })

  it('查詢條件在結果上方，分頁在結果下方', async () => {
    const html = await render(
      QueryLayout,
      { title: '家禽行情' },
      { filters: '日期區間', results: '一張表', pager: '第 2 頁' },
    )
    expect(before(html, '日期區間', '一張表')).toBe(true)
    expect(before(html, '一張表', '第 2 頁')).toBe(true)
  })

  it('沒給插槽的區塊不會留下空殼', async () => {
    const html = await render(QueryLayout, { title: '家禽行情' })
    expect(html).not.toContain('query-layout__hint')
    expect(html).not.toContain('query-layout__pager')
    expect(html).not.toContain('query-filters__actions')
  })

  it('篩選卡不再吸頂，所以不會出現黏住態的標記', async () => {
    // P3 改版：移除吸頂——高的篩選卡捲動時會蓋住結果（owner 回報），改成一般卡片。
    const html = await render(QueryLayout, { title: '家禽行情' })
    expect(html).not.toContain('is-stuck')
    expect(html).not.toContain('query-layout__sentinel')
  })
})

describe('DetailLayout', () => {
  it('返回列排在標題上方——看完才找返回等於要按瀏覽器上一頁', async () => {
    const html = await render(
      DetailLayout,
      { title: '虎斑幼貓', backTo: '/', backLabel: '返回收容動物地圖' },
      { default: '內文區' },
    )
    expect(before(html, '返回收容動物地圖', '虎斑幼貓')).toBe(true)
  })

  it('摘要在整寬區塊之前，整寬區塊在內文之前', async () => {
    const html = await render(
      DetailLayout,
      { title: '虎斑幼貓', backTo: '/' },
      { summary: '摘要區', wide: '照片牆', default: '內文區' },
    )
    expect(before(html, '摘要區', '照片牆')).toBe(true)
    expect(before(html, '照片牆', '內文區')).toBe(true)
  })
})

describe('MapLayout', () => {
  it('地圖排在清單之前——先看得到地圖才知道能點', async () => {
    const html = await render(
      MapLayout,
      { title: '病蟲害警報' },
      { map: '台灣地圖', list: '警報清單' },
    )
    expect(before(html, '台灣地圖', '警報清單')).toBe(true)
  })

  it('圖例貼在地圖下方，沒給就不畫', async () => {
    const withLegend = await render(
      MapLayout,
      { title: '病蟲害警報' },
      { map: '台灣地圖', legend: '三級燈號', list: '警報清單' },
    )
    expect(before(withLegend, '台灣地圖', '三級燈號')).toBe(true)
    expect(before(withLegend, '三級燈號', '警報清單')).toBe(true)

    const without = await render(MapLayout, { title: '病蟲害警報' }, { map: '台灣地圖' })
    expect(without).not.toContain('map-layout__legend')
  })
})

describe('EntryLayout', () => {
  it('深色頁首帶用堆疊式中英並排，且吃深色底那組文字色', async () => {
    const html = await render(EntryLayout, { title: '市場行情', titleEn: 'Market Prices' })
    expect(html).toContain('bilingual--stacked')
    expect(html).toContain('bilingual--deep')
    expect(html).toContain('entry-layout__band')
  })

  it('母題是插槽，因為它會隨節氣換圖', async () => {
    const html = await render(EntryLayout, { title: '市場行情' }, { motif: '垂穗線稿' })
    expect(html).toContain('垂穗線稿')
    expect(before(html, '垂穗線稿', '市場行情')).toBe(true)
  })

  it('子頁卡片牆排在幕之後', async () => {
    const html = await render(
      EntryLayout,
      { title: '市場行情', lead: '五個子頁' },
      { default: '卡片牆' },
    )
    expect(before(html, '五個子頁', '卡片牆')).toBe(true)
  })
})
