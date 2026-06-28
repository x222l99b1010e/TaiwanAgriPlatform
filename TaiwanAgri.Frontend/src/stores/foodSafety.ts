// src/stores/foodSafety.ts
// 職責：管理 FoodSafety 模組的全局狀態
// W21a 今日菜價快覽、W21b 農產品追溯查詢

import { defineStore } from 'pinia'
import { ref } from 'vue'
import { foodSafetyApi } from '@/api/foodSafety'
import type { PriceResponseDto } from '@/api/market'
import type { TraceabilityResponseDto } from '@/api/foodSafety'

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

  // 追溯查詢結果
  const traceabilityResult = ref<TraceabilityResponseDto | null>(null)

  // 追溯查詢載入狀態
  const isSearching = ref(false)

  // 追溯查詢錯誤訊息
  const searchError = ref<string | null>(null)

  // ─── 動作（Actions） ──────────────────────────────────────────────────────

  /** 載入今日蔬菜均價（hasFetched 保護，同一 session 只打一次） */
  async function fetchTodayVegPrices() {
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

  /** 農產品追溯查詢（即時打農業部四支 API） */
  async function searchTraceability(traceCode: string) {
    isSearching.value = true
    searchError.value = null
    traceabilityResult.value = null
    try {
      traceabilityResult.value = await foodSafetyApi.searchTraceability(traceCode)
    } catch (e) {
      searchError.value = '查詢失敗，請確認追溯碼是否正確'
      console.error(e)
    } finally {
      isSearching.value = false
    }
  }

  return {
    // State
    todayVegPrices,
    isLoading,
    error,
    hasFetched,
    traceabilityResult,
    isSearching,
    searchError,
    // Actions
    fetchTodayVegPrices,
    searchTraceability,
  }
})