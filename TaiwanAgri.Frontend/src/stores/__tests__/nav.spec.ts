// src/stores/__tests__/nav.spec.ts
// 職責：模組清單載入失敗時，store 有沒有留下「失敗了」這個訊號。
//
// 為什麼這件事需要測試：失敗時 loaded 是刻意維持 false 的（為了讓下一次呼叫會重試），
// 所以在畫面看來，「載失敗」與「還在載入」是同一個狀態——症狀是永遠轉圈，
// 沒有錯誤訊息、沒有重試鈕，而且主控台只有一句 unhandled rejection。
// 這種錯不會讓任何既有測試變紅，只能在這裡釘住。
//
// 部署之後它會從「偶爾」變成「常態」：主機沒有 Always On、資料庫會自動暫停，
// 閒置後的第一個請求必然撞到冷啟動。

import { describe, it, expect, vi, beforeEach } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

// API 層整支換掉：這裡驗的是 store 的狀態機，不是 HTTP。
vi.mock('@/api/nav', () => ({
  fetchNavModules: vi.fn(),
}))

import { fetchNavModules } from '@/api/nav'
import { useNavStore } from '../nav'
import type { NavModule } from '@/api/nav'

const MARKET: NavModule = {
  name: '市場行情',
  route: '/market',
  icon: 'mdi-chart-line',
  sortOrder: 1,
  children: [],
}

describe('導覽模組 store 的載入狀態', () => {
  beforeEach(() => {
    // vi.fn() 的呼叫紀錄不會自己跨測試重置，「被呼叫幾次」的斷言會把前面的次數一起算進來
    vi.clearAllMocks()
    setActivePinia(createPinia())
    // 失敗路徑會 console.error，讓它不要污染測試輸出
    vi.spyOn(console, 'error').mockImplementation(() => {})
  })

  it('載入成功時設 loaded、不設 loadFailed', async () => {
    vi.mocked(fetchNavModules).mockResolvedValue([MARKET])
    const nav = useNavStore()

    await nav.loadModules()

    expect(nav.loaded).toBe(true)
    expect(nav.loadFailed).toBe(false)
    expect(nav.modules).toHaveLength(1)
  })

  it('載入失敗時不往上拋，而是設 loadFailed', async () => {
    vi.mocked(fetchNavModules).mockRejectedValue(new Error('Network Error'))
    const nav = useNavStore()

    // 這一行如果會拋，畫面端就只剩主控台一句 unhandled rejection
    await nav.loadModules()

    expect(nav.loadFailed).toBe(true)
    expect(nav.loaded).toBe(false)
  })

  it('失敗之後仍然可以重試，而且重試會把失敗訊號清掉', async () => {
    vi.mocked(fetchNavModules).mockRejectedValueOnce(new Error('Network Error'))
    const nav = useNavStore()
    await nav.loadModules()
    expect(nav.loadFailed).toBe(true)

    vi.mocked(fetchNavModules).mockResolvedValueOnce([MARKET])
    await nav.loadModules()

    expect(nav.loadFailed).toBe(false)
    expect(nav.loaded).toBe(true)
    expect(nav.modules).toHaveLength(1)
  })

  it('載入成功之後不會再打一次 API', async () => {
    vi.mocked(fetchNavModules).mockResolvedValue([MARKET])
    const nav = useNavStore()

    await nav.loadModules()
    await nav.loadModules()

    expect(fetchNavModules).toHaveBeenCalledTimes(1)
  })

  it('同時呼叫兩次只會發一個請求', async () => {
    // 有了重試鈕之後，連點兩下就是兩個請求——而重試的情境正好是伺服器很慢的時候
    let release: (value: NavModule[]) => void = () => {}
    vi.mocked(fetchNavModules).mockReturnValue(new Promise(resolve => { release = resolve }))
    const nav = useNavStore()

    const first = nav.loadModules()
    const second = nav.loadModules()
    release([MARKET])
    await Promise.all([first, second])

    expect(fetchNavModules).toHaveBeenCalledTimes(1)
    expect(nav.loaded).toBe(true)
  })
})
