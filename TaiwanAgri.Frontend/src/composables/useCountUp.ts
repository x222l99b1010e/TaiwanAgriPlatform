// src/composables/useCountUp.ts
// 職責：數字進場的 count-up 動畫。
//
// ⚠ 起手用 setTimeout(…, 0)，不是 requestAnimationFrame——rAF 在背景分頁會被凍結，
// 首屏數字停在 0 不動的風險是真的（原型階段實際遇到）。動畫過程本身
// 仍然用 rAF 逐幀更新（那是動畫平滑度的正常做法，這條規則只管「起手」那一下）。
//
// prefers-reduced-motion 開啟時直接跳到最終值，不跑動畫。

import { ref, type Ref } from 'vue'

export interface UseCountUpOptions {
  /** 動畫時長（毫秒），預設抓 --duration-entry 的量級——首頁只看一次，慢是隆重 */
  duration?: number
}

export function useCountUp(options: UseCountUpOptions = {}) {
  const duration = options.duration ?? 1100
  const value: Ref<number> = ref(0)

  function start(target: number) {
    if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
      value.value = target
      return
    }

    setTimeout(() => {
      const startTime = performance.now()
      const from = value.value

      function tick(now: number) {
        const elapsed = now - startTime
        const progress = Math.min(elapsed / duration, 1)
        // easeOutCubic：快進慢出，數字停下來的那一刻比較不突兀
        const eased = 1 - (1 - progress) ** 3
        value.value = Math.round(from + (target - from) * eased)
        if (progress < 1) requestAnimationFrame(tick)
      }
      requestAnimationFrame(tick)
    }, 0)
  }

  return { value, start }
}
