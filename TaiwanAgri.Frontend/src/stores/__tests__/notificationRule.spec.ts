// src/stores/__tests__/notificationRule.spec.ts
// 職責：規則 store 的動作在成功之後有沒有把「相鄰的狀態」一起帶新。
//
// ⚠ 這是本專案第一支 store 測試，既有的前端測試全部打在純函式上。
// 之所以要另開這一類：這一輪有兩個 bug 都出在「邏輯住在測試搆不到的地方」，
// 而「動作完成後忘了更新另一份狀態」這種錯，純函式測試在定義上就看不到——
// 它不是某個函式算錯，是兩份狀態之間少了一條線。

import { describe, it, expect, vi, beforeEach } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

// API 層整支換掉：這裡要驗的是 store 動作之間的協作，不是 HTTP。
vi.mock('@/api/notificationRule', () => ({
  notificationRuleApi: {
    getRules: vi.fn(),
    evaluateNow: vi.fn(),
  },
}))
vi.mock('@/api/weather', () => ({
  notificationApi: {
    getUnreadCount: vi.fn(),
  },
}))

import { notificationRuleApi } from '@/api/notificationRule'
import { notificationApi } from '@/api/weather'
import { useNotificationRuleStore } from '../notificationRule'
import { useNotificationStore } from '../notification'

describe('規則 store 的 evaluateNow', () => {
  beforeEach(() => {
    // ⚠ 這一行不能省：vi.fn() 的呼叫紀錄不會自己跨測試重置，
    // 少了它，「被呼叫幾次」這種斷言會把前面幾條測試的次數一起算進來
    vi.clearAllMocks()
    setActivePinia(createPinia())
    vi.mocked(notificationRuleApi.getRules).mockResolvedValue({
      items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0,
    })
    vi.mocked(notificationRuleApi.evaluateNow).mockResolvedValue({
      rulesEvaluated: 1,
      notificationsCreated: 3,
      latestObservedAt: '2026-09-10T15:00:00',
      hasFreshObservation: true,
      numericRulesEvaluated: 1,
      numericRulesWithNewObservations: 1,
    })
    vi.mocked(notificationApi.getUnreadCount).mockResolvedValue({ count: 3 })
  })

  /**
   * 這條守的是「畫面說的」與「畫面顯示的」不可以互相矛盾。
   * 評估成功的訊息是「已產生 N 則新通知，點右上角的鈴鐺可以看到」，
   * 而未讀數若沒有跟著重讀，鈴鐺當下是空的——紅點要等最多 60 秒的輪詢才補上。
   */
  it('成功後要把未讀數一起帶新，紅點才會當場出現', async () => {
    const ruleStore = useNotificationRuleStore()
    const notificationStore = useNotificationStore()
    expect(notificationStore.unreadCount).toBe(0)

    await ruleStore.evaluateNow()

    expect(notificationApi.getUnreadCount).toHaveBeenCalledTimes(1)
    expect(notificationStore.unreadCount).toBe(3)
  })

  /**
   * 回報 0 則新增的那一輪也要重讀。
   * 評估的第一件事是刪掉過期通知，所以「沒有新增」不等於「未讀數沒變」——
   * 只在 notificationsCreated > 0 時才重讀的話，過期清理造成的下降會被漏掉。
   */
  it('沒有新增通知的那一輪也要重讀未讀數，因為評估會先刪掉過期的', async () => {
    vi.mocked(notificationRuleApi.evaluateNow).mockResolvedValue({
      rulesEvaluated: 1,
      notificationsCreated: 0,
      latestObservedAt: '2026-09-10T15:00:00',
      hasFreshObservation: true,
      numericRulesEvaluated: 1,
      numericRulesWithNewObservations: 1,
    })
    vi.mocked(notificationApi.getUnreadCount).mockResolvedValue({ count: 1 })

    const ruleStore = useNotificationRuleStore()
    const notificationStore = useNotificationStore()

    await ruleStore.evaluateNow()

    expect(notificationApi.getUnreadCount).toHaveBeenCalledTimes(1)
    expect(notificationStore.unreadCount).toBe(1)
  })

  /**
   * 失敗時不重讀。
   * 少了這條，一個「不管成功失敗都重讀一次」的實作也會讓上面兩條通過，
   * 而那個實作會在被限流（429）時多打一支請求。
   */
  it('評估失敗時不重讀未讀數', async () => {
    vi.mocked(notificationRuleApi.evaluateNow).mockRejectedValue(new Error('429'))
    vi.spyOn(console, 'error').mockImplementation(() => {})

    const ruleStore = useNotificationRuleStore()
    const success = await ruleStore.evaluateNow()

    expect(success).toBe(false)
    expect(notificationApi.getUnreadCount).not.toHaveBeenCalled()
  })
})
