// src/views/__tests__/moduleEntry.spec.ts
// 職責：四個模組入口頁共用的那一支元件，有沒有把子頁清單完整畫出來。
//
// ⚠ 這是本專案第一支 View 測試，既有的前端測試打在純函式、共用元件與 store 上。
// 之所以要為這一支破例：它是一份資料（navStore 的子頁清單）與另一份資料
// （navCopy 的說明文案）靠路由字串對起來的，而對不起來時畫面不會壞——
// 只是那張卡片安靜地少一行字。這種錯沒有人會在 code review 看出來。
//
// 沿用 components/layouts/__tests__ 的做法：vue/server-renderer 算成 HTML 字串做結構斷言，
// 不引入 jsdom。要驗 hover 抬升與焦點外框得用實機。

import { describe, it, expect } from 'vitest'
import { createSSRApp, h } from 'vue'
import { renderToString } from 'vue/server-renderer'
import { createRouter, createMemoryHistory } from 'vue-router'
import { createPinia, setActivePinia } from 'pinia'

import ModuleEntryView from '../ModuleEntryView.vue'
import { useNavStore } from '@/stores/nav'
import type { NavModule } from '@/api/nav'

const MARKET: NavModule = {
  name: '市場行情',
  route: '/market',
  icon: 'mdi-chart-line',
  sortOrder: 1,
  children: [
    { name: '行情查詢', route: '/market/prices', icon: 'mdi-chart-areaspline', sortOrder: 1 },
    { name: '天災記錄', route: '/market/disasters', icon: 'mdi-weather-lightning-rainy', sortOrder: 2 },
    { name: '休市日查詢', route: '/market/rest-days', icon: 'mdi-calendar-remove', sortOrder: 3 },
    { name: '畜禽行情', route: '/market/pork', icon: 'mdi-pig', sortOrder: 4 },
    { name: '家禽行情', route: '/market/poultry', icon: 'mdi-bird', sortOrder: 5 },
  ],
}

/**
 * 把入口頁算成 HTML。path 決定它認為自己是哪個模組。
 * 每次都建一個新的 pinia，測試之間不共用 store——隔離是這裡保證的，
 * 不要改成靠 beforeEach，那樣會有兩個地方在做同一件事而其中一個是死的。
 * loaded 預設 false＝「還沒載回來」；要測「載完了但是空的」就自己傳 true。
 */
async function renderAt(path: string, modules: NavModule[], loaded = modules.length > 0) {
  const pinia = createPinia()
  setActivePinia(pinia)
  const nav = useNavStore()
  nav.modules = modules
  nav.loaded = loaded

  const app = createSSRApp({ render: () => h(ModuleEntryView) })
  const router = createRouter({
    history: createMemoryHistory(),
    // 只要能解析 RouterLink 的 to，不需要真的掛元件
    routes: [{ path: '/:pathMatch(.*)*', component: { template: '<div />' } }],
  })
  app.use(pinia)
  app.use(router)
  await router.push(path)
  await router.isReady()
  return renderToString(app)
}

describe('ModuleEntryView', () => {
  it('模組名稱與英文定譯進到深色頁首帶', async () => {
    const html = await renderAt('/market', [MARKET])
    expect(html).toContain('entry-layout__band')
    expect(html).toContain('市場行情')
    expect(html).toContain('MARKET PRICES')
    expect(html).toContain('作物、毛豬、家禽的產地與批發行情，一次比對')
  })

  it('每個子頁各一張卡片，說明文案要對到自己那一頁', async () => {
    const html = await renderAt('/market', [MARKET])
    for (const child of MARKET.children) {
      expect(html).toContain(child.name)
      expect(html).toContain(`href="${child.route}"`)
    }
    // 對錯路由的話說明會整句換人，所以驗「這一句」而不是「有沒有說明」
    expect(html).toContain('蔬菜、水果、花卉的每日批發交易均價')
    expect(html).toContain('雞、鴨、鵝與雞蛋的產地價與批發價')
  })

  it('查不到說明的子頁仍然有一列，只是少掉說明那一行', async () => {
    const withUnknown: NavModule = {
      ...MARKET,
      children: [{ name: '智慧提示', route: '/market/not-in-copy', icon: 'mdi-bell-ring', sortOrder: 9 }],
    }
    const html = await renderAt('/market', [withUnknown])
    expect(html).toContain('智慧提示')
    expect(html).toContain('href="/market/not-in-copy"')
    expect(html).not.toContain('showcase-text__lead')
    expect(html).not.toContain('showcase-text__eyebrow')
  })

  it('奇偶列左右交錯，而且交錯的那一側跟錯落的縮排是同一個判斷', async () => {
    const html = await renderAt('/market', [MARKET])
    // 五列裡第 2、4 列翻面，翻面的同時吃到內縮
    expect(html.match(/showcase-row--flip/g)).toHaveLength(2)
    expect(html.match(/child-showcase__row--inset/g)).toHaveLength(2)
  })

  it('hover 特效依模組決定，一頁之內不換', async () => {
    // 市場行情＝走勢線，整頁五列都是同一種
    const market = await renderAt('/market', [MARKET])
    expect(market.match(/fx-chart__line/g)).toHaveLength(MARKET.children.length)
    expect(market).not.toContain('fx-paw')

    // 換一個模組就換一種：寵物＝腳印
    const pet: NavModule = {
      name: '毛小孩地圖',
      route: '/pet',
      icon: 'mdi-paw',
      sortOrder: 4,
      children: [{ name: '收容動物地圖', route: '/pet/shelter-map', icon: 'mdi-map-marker-radius', sortOrder: 1 }],
    }
    const petHtml = await renderAt('/pet', [pet])
    expect(petHtml).toContain('fx-paw')
    expect(petHtml).not.toContain('fx-chart__line')
  })

  it('模組不在特效對照表裡時退回預設光點，不是掉成別的模組那一種', async () => {
    // 日後新增第五個模組而忘了補 MODULE_EFFECT，畫面不會壞、也不該偷穿別人的特效
    const unknown: NavModule = {
      ...MARKET,
      route: '/not-in-effect-table',
      children: [{ name: '某個子頁', route: '/not-in-effect-table/x', icon: 'mdi-help', sortOrder: 1 }],
    }
    const html = await renderAt('/not-in-effect-table', [unknown])
    expect(html).toContain('fx-spark')
    expect(html).not.toContain('fx-chart__line')
    expect(html).not.toContain('fx-paw')
  })

  it('模組存在但一個子頁都沒有時要說明，不是留一片空白', async () => {
    // NavService 依角色過濾子頁，權限被收掉時這個模組會只剩自己
    const html = await renderAt('/market', [{ ...MARKET, children: [] }])
    expect(html).toContain('市場行情')
    expect(html).toContain('這個模組目前沒有可用的功能')
    expect(html).not.toContain('showcase-row')
  })

  it('路徑對不到任何模組時要說「找不到」，不是畫成空白', async () => {
    const html = await renderAt('/not-a-module', [MARKET])
    expect(html).toContain('找不到這個模組')
    expect(html).not.toContain('entry-layout__band')
  })

  it('清單還沒載回來時顯示載入中，而不是「找不到」', async () => {
    const html = await renderAt('/market', [], false)
    expect(html).toContain('載入模組中')
    expect(html).not.toContain('找不到這個模組')
  })

  it('載完了但一個模組都拿不到，要講權限，不是永遠轉圈', async () => {
    // 角色的模組權限全被收掉時後端是合法地回傳空陣列。
    // 用 modules.length 判斷「還在載入」的話，這種帳號會永遠停在轉圈。
    const html = await renderAt('/market', [], true)
    expect(html).toContain('目前沒有可以瀏覽的模組')
    expect(html).not.toContain('載入模組中')
    expect(html).not.toContain('找不到這個模組')
  })
})
