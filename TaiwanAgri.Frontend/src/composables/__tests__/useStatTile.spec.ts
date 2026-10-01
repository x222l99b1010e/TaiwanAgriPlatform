import { describe, it, expect, beforeAll, afterAll } from 'vitest'
import { useStatTile } from '@/composables/useStatTile'

// 測試跑在 node 環境，沒有 window。useCountUp 會先問「減少動態」偏好，
// 這裡讓它回答「是」：數字直接跳到最終值，不必等動畫
const originalWindow = globalThis.window
beforeAll(() => {
  globalThis.window = { matchMedia: () => ({ matches: true }) } as unknown as Window & typeof globalThis
})
afterAll(() => {
  globalThis.window = originalWindow
})

describe('useStatTile', () => {
  it('一開始是載入中', () => {
    const tile = useStatTile(async () => 1)
    expect(tile.state.value).toBe('loading')
  })

  it('取到數值時是 ready，而且停下來的是原值（小數不被四捨五入掉）', async () => {
    const tile = useStatTile(async () => 39.5, { decimals: 1 })
    await tile.load()
    expect(tile.state.value).toBe('ready')
    expect(tile.value.value).toBe(39.5)
  })

  it('0 是真的數值，不是沒有資料', async () => {
    const tile = useStatTile(async () => 0)
    await tile.load()
    expect(tile.state.value).toBe('ready')
  })

  it('回傳 null 代表查過但沒有資料，不能畫成 0', async () => {
    const tile = useStatTile(async () => null)
    await tile.load()
    expect(tile.state.value).toBe('empty')
  })

  it('取值失敗時是 error，不能畫成 0', async () => {
    const tile = useStatTile(async () => {
      throw new Error('連不上伺服器')
    })
    await tile.load()
    expect(tile.state.value).toBe('error')
  })

  it('重試會先回到載入中，成功後變成 ready', async () => {
    let fail = true
    const tile = useStatTile(async () => {
      if (fail) throw new Error('連不上伺服器')
      return 55
    })
    await tile.load()
    expect(tile.state.value).toBe('error')

    fail = false
    const retry = tile.load()
    expect(tile.state.value).toBe('loading')
    await retry
    expect(tile.state.value).toBe('ready')
    expect(tile.value.value).toBe(55)
  })
})
