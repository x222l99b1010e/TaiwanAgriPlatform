/**
 * 五個共用 UI 元件的結構測試（Bilingual 已在 layouts.spec.ts 涵蓋）。
 *
 * 沿用 layouts.spec.ts 的做法：vue/server-renderer 算成 HTML 字串做結構斷言，
 * 不引入 jsdom 或 @vue/test-utils。要驗的是「prop 有沒有生效、插槽有沒有接對、
 * 預設值對不對」，這些在字串上就看得出來。
 *
 * 這幾個元件現在被二十幾個頁面引用，所以任一 prop 行為改壞都是全站級影響；
 * 而它們壞掉的方式幾乎都不會報錯——按鈕少了 disabled 仍然是一顆按鈕、
 * 提示框拿到錯的圖示仍然是一個提示框。
 */
import { describe, it, expect } from 'vitest'
import { createSSRApp, h, type Component } from 'vue'
import { renderToString } from 'vue/server-renderer'

import Btn from '@/components/ui/Btn.vue'
import StateBlock from '@/components/ui/StateBlock.vue'
import PageHeader from '@/components/ui/PageHeader.vue'
import FilterCard from '@/components/ui/FilterCard.vue'
import HintBox from '@/components/ui/HintBox.vue'

/** 把元件算成 HTML。插槽內容一律包成 <i>，方便在字串裡定位。 */
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
        Object.fromEntries(Object.entries(slots).map(([name, text]) => [name, () => h('i', text)])),
      ),
  })
  return renderToString(app)
}

describe('Btn', () => {
  it('預設是 primary、md 尺寸，型別為 button', async () => {
    // type 預設成 submit 會讓表單外的按鈕造成非預期送出，而那只在特定頁面才顯現
    const html = await render(Btn, {}, { default: '查詢' })
    expect(html).toContain('btn--primary')
    expect(html).toContain('btn--md')
    expect(html).toContain('type="button"')
  })

  it('variant 與 size 會反映到 class', async () => {
    const html = await render(Btn, { variant: 'danger', size: 'sm' })
    expect(html).toContain('btn--danger')
    expect(html).toContain('btn--sm')
  })

  it('載入中時停用按鈕，並把圖示換成轉圈而不是額外插入元素', async () => {
    // 多插一個元素會讓按鈕寬度在載入前後跳動
    const html = await render(Btn, { loading: true, icon: 'mdi-magnify' })
    expect(html).toContain('disabled')
    expect(html).toContain('mdi-loading')
    expect(html).not.toContain('mdi-magnify')
  })

  it('disabled 單獨成立時也要真的停用', async () => {
    const html = await render(Btn, { disabled: true })
    expect(html).toContain('disabled')
  })

  it('沒有 icon 時不渲染圖示元素', async () => {
    const html = await render(Btn, {}, { default: '送出' })
    expect(html).not.toContain('btn-icon')
    expect(html).toContain('送出')
  })
})

describe('StateBlock', () => {
  it('四種狀態各有自己的預設圖示', async () => {
    // 這四個值先前是 switch＋default，新增狀態會安靜地掉進 default 拿到空資料的圖示
    expect(await render(StateBlock, { state: 'empty' })).toContain('mdi-database-off-outline')
    expect(await render(StateBlock, { state: 'error' })).toContain('mdi-alert-circle')
    expect(await render(StateBlock, { state: 'hint' })).toContain('mdi-magnify')
  })

  it('loading 用轉圈元素而不是圖示', async () => {
    const html = await render(StateBlock, { state: 'loading' })
    expect(html).toContain('state-spinner')
    expect(html).not.toContain('state-icon')
  })

  it('empty 與 hint 是不同狀態，class 要分得開', async () => {
    // 兩者要說的話不同：一個是「查過了，沒有」，另一個是「還沒查」。
    // 先前有頁面把兩者都做成空白，使用者分不出差別
    expect(await render(StateBlock, { state: 'empty' })).toContain('state-block--empty')
    expect(await render(StateBlock, { state: 'hint' })).toContain('state-block--hint')
  })

  it('icon 可以覆寫預設值', async () => {
    const html = await render(StateBlock, { state: 'error', icon: 'mdi-wifi-off' })
    expect(html).toContain('mdi-wifi-off')
    expect(html).not.toContain('mdi-alert-circle')
  })

  it('插槽優先於 message prop', async () => {
    const html = await render(StateBlock, { state: 'empty', message: '沒有資料' }, { default: '自訂訊息' })
    expect(html).toContain('自訂訊息')
    expect(html).not.toContain('沒有資料')
  })

  it('retryable 為假時不渲染重試按鈕', async () => {
    const html = await render(StateBlock, { state: 'error', message: '請求失敗' })
    expect(html).not.toContain('mdi-refresh')
  })

  it('retryable 為真時渲染重試按鈕並套用自訂文字', async () => {
    const html = await render(StateBlock, { state: 'error', retryable: true, retryLabel: '再試一次' })
    expect(html).toContain('mdi-refresh')
    expect(html).toContain('再試一次')
  })

  it('帶 role=status 讓螢幕閱讀器讀得到狀態變化', async () => {
    expect(await render(StateBlock, { state: 'loading' })).toContain('role="status"')
  })
})

describe('PageHeader', () => {
  it('標題透過 Bilingual 渲染，中英並排', async () => {
    const html = await render(PageHeader, { title: '市場行情', titleEn: 'Market Prices' })
    expect(html).toContain('市場行情')
    expect(html).toContain('Market Prices')
  })

  it('沒有副標也沒有副標插槽時不渲染副標段落', async () => {
    const html = await render(PageHeader, { title: '市場行情' })
    expect(html).not.toContain('page-subtitle')
  })

  it('副標插槽優先於 subtitle prop', async () => {
    // 有幾頁要在說明裡塞動態內容（最新交易日、查詢區間天數）
    const html = await render(PageHeader, { title: '行情', subtitle: '靜態副標' }, { subtitle: '動態副標' })
    expect(html).toContain('動態副標')
    expect(html).not.toContain('靜態副標')
  })

  it('沒有 actions 插槽時不渲染動作區塊', async () => {
    const html = await render(PageHeader, { title: '行情' })
    expect(html).not.toContain('page-header-actions')
  })

  it('有 actions 插槽時渲染動作區塊', async () => {
    const html = await render(PageHeader, { title: '行情' }, { actions: '匯出' })
    expect(html).toContain('page-header-actions')
    expect(html).toContain('匯出')
  })
})

describe('FilterCard', () => {
  it('預設是橫排', async () => {
    expect(await render(FilterCard, {}, { default: '篩選條件' })).toContain('filter-card--row')
  })

  it('layout 為 stack 時改直排', async () => {
    expect(await render(FilterCard, { layout: 'stack' })).toContain('filter-card--stack')
  })

  it('渲染插槽內容', async () => {
    expect(await render(FilterCard, {}, { default: '篩選條件' })).toContain('篩選條件')
  })
})

describe('HintBox', () => {
  it('三種語氣各有自己的預設圖示', async () => {
    expect(await render(HintBox, {})).toContain('mdi-information-outline')
    expect(await render(HintBox, { tone: 'success' })).toContain('mdi-check-circle-outline')
    expect(await render(HintBox, { tone: 'warning' })).toContain('mdi-alert-outline')
  })

  it('預設語氣是 info', async () => {
    expect(await render(HintBox, {}, { default: '說明' })).toContain('hint-box--info')
  })

  it('icon 可以覆寫預設值', async () => {
    const html = await render(HintBox, { tone: 'warning', icon: 'mdi-clock-outline' })
    expect(html).toContain('mdi-clock-outline')
    expect(html).not.toContain('mdi-alert-outline')
  })

  it('沒有 title 時不渲染標題段落', async () => {
    // 有標題是「說明區塊」，沒有是一句話的行內提示，兩者版面不同
    const html = await render(HintBox, {}, { default: '一句話提示' })
    expect(html).not.toContain('hint-box-title')
    expect(html).toContain('一句話提示')
  })

  it('有 title 時渲染標題並保留插槽內容', async () => {
    const html = await render(HintBox, { title: '查詢說明' }, { default: '條列內容' })
    expect(html).toContain('hint-box-title')
    expect(html).toContain('查詢說明')
    expect(html).toContain('條列內容')
  })
})
