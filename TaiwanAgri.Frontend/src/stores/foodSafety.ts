// src/stores/foodSafety.ts
// 職責：管理 FoodSafety 模組的全局狀態
// W21a 只有今日菜價快覽，後續功能（追溯查詢、農藥違規）會在這裡擴充

import { defineStore } from 'pinia'
import { ref } from 'vue'
import { foodSafetyApi } from '@/api/foodSafety'
import type { PriceResponseDto } from '@/api/market'

export const useFoodSafetyStore = defineStore('foodSafety', () => {
  // ─── 狀態（State） ────────────────────────────────────────────────────────

  // 今日蔬菜均價資料
  const todayVegPrices = ref<PriceResponseDto[]>([])

  // 載入狀態
  const isLoading = ref(false)

  // 錯誤訊息
  const error = ref<string | null>(null)

  // 是否已經載入過（避免重複打 API）
  const hasFetched = ref(false)

  // ─── 動作（Actions） ──────────────────────────────────────────────────────

  async function fetchTodayVegPrices() {
    // 已載入過就不重打（今日資料不會變）
    if (hasFetched.value) return

    isLoading.value = true
    error.value = null
    try {
      todayVegPrices.value = await foodSafetyApi.getTodayVegPrices()
      hasFetched.value = true
    } catch (e) {
      error.value = '載入今日菜價失敗，請稍後再試'
      console.error(e)
    } finally {
      isLoading.value = false
    }
  }

  return {
    todayVegPrices,
    isLoading,
    error,
    hasFetched,
    fetchTodayVegPrices,
  }
})