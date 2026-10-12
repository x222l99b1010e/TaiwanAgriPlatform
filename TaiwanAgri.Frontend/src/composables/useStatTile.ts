// src/composables/useStatTile.ts
// 職責：首頁「今日數字」單一格的狀態——載入中／有值／查過但沒有資料／抓不到。
//
// 為什麼要四種狀態：只用「載入中」一個布林時，抓不到資料與沒有資料都會落到
// 「不在載入中、數字停在 0」，畫面寫著「今日雞蛋產地均價 0 元」。0 是一個真的數值
// （例如生效中的警報可以真的是 0 則），所以「沒有值」不能用 0 表示。
//
// 取值函式回傳 null＝查過但這段期間沒有資料；丟例外＝抓不到。
// 數字進場的動畫沿用 useCountUp。

import { ref } from 'vue'
import { useCountUp, type UseCountUpOptions } from '@/composables/useCountUp'

export type StatState = 'loading' | 'ready' | 'empty' | 'error'

export function useStatTile(fetchValue: () => Promise<number | null>, countUp: UseCountUpOptions = {}) {
  const state = ref<StatState>('loading')
  const { value, start } = useCountUp(countUp)

  async function load() {
    state.value = 'loading'
    try {
      const result = await fetchValue()
      if (result === null) {
        state.value = 'empty'
        return
      }
      state.value = 'ready'
      start(result)
    } catch {
      state.value = 'error'
    }
  }

  return { state, value, load }
}
