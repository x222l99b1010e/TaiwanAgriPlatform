// src/stores/__tests__/foodSafety.spec.ts
// 職責：追溯碼查詢的新舊回應不能互蓋。
// 一次查詢要等四支外部 API，查詢中還能點範例碼或再按一次 Enter；先送出的那一次晚回來時，
// 原本會把後查的結果蓋掉（2026-11 總檢查 2、3 號 C 類）。這裡用手動放行的 Promise 排出這個順序

import { describe, it, expect, vi, beforeEach } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

vi.mock('@/api/foodSafety', () => ({
  foodSafetyApi: {
    searchTraceability: vi.fn(),
  },
}))

import { foodSafetyApi } from '@/api/foodSafety'
import type { TraceabilityResponseDto } from '@/api/foodSafety'
import { useFoodSafetyStore } from '../foodSafety'

/** 一個由測試決定什麼時候完成的 Promise */
function deferred<T>() {
  let resolve!: (value: T) => void
  let reject!: (reason: unknown) => void
  const promise = new Promise<T>((res, rej) => { resolve = res; reject = rej })
  return { promise, resolve, reject }
}

/** 只用來分辨是哪一次查詢的回應，其他欄位這裡用不到 */
const response = (code: string) => ({ traceCode: code }) as unknown as TraceabilityResponseDto

describe('食安 store 的 searchTraceability', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    setActivePinia(createPinia())
  })

  it('先送出的查詢晚回來時，不蓋掉後查的結果', async () => {
    const first = deferred<TraceabilityResponseDto>()
    const second = deferred<TraceabilityResponseDto>()
    vi.mocked(foodSafetyApi.searchTraceability)
      .mockReturnValueOnce(first.promise)
      .mockReturnValueOnce(second.promise)
    const store = useFoodSafetyStore()

    const firstSearch = store.searchTraceability('0101000005')
    const secondSearch = store.searchTraceability('0552100105')

    second.resolve(response('0552100105'))
    await secondSearch
    expect(store.isSearching).toBe(false)

    first.resolve(response('0101000005'))
    await firstSearch

    expect(store.traceabilityResult).toEqual(response('0552100105'))
    expect(store.isSearching).toBe(false)
  })

  it('先送出的查詢晚到的失敗，也不把後查的結果換成錯誤訊息', async () => {
    const first = deferred<TraceabilityResponseDto>()
    const second = deferred<TraceabilityResponseDto>()
    vi.mocked(foodSafetyApi.searchTraceability)
      .mockReturnValueOnce(first.promise)
      .mockReturnValueOnce(second.promise)
    const store = useFoodSafetyStore()
    vi.spyOn(console, 'error').mockImplementation(() => {})

    const firstSearch = store.searchTraceability('0101000005')
    const secondSearch = store.searchTraceability('0552100105')

    second.resolve(response('0552100105'))
    await secondSearch
    first.reject(new Error('逾時'))
    await firstSearch

    expect(store.searchError).toBeNull()
    expect(store.traceabilityResult).toEqual(response('0552100105'))
  })

  it('還在等後查的那一次時，先回來的舊回應不把「查詢中」關掉', async () => {
    const first = deferred<TraceabilityResponseDto>()
    const second = deferred<TraceabilityResponseDto>()
    vi.mocked(foodSafetyApi.searchTraceability)
      .mockReturnValueOnce(first.promise)
      .mockReturnValueOnce(second.promise)
    const store = useFoodSafetyStore()

    const firstSearch = store.searchTraceability('0101000005')
    void store.searchTraceability('0552100105')

    first.resolve(response('0101000005'))
    await firstSearch

    expect(store.isSearching).toBe(true)
    expect(store.traceabilityResult).toBeNull()
  })
})
