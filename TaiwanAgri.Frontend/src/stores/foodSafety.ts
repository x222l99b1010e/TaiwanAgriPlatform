// src/stores/foodSafety.ts
// 職責：管理 FoodSafety 模組的全局狀態
// W21a 今日菜價快覽、W21b 農產品追溯查詢

import { defineStore } from 'pinia'
import { ref } from 'vue'
import { foodSafetyApi } from '@/api/foodSafety'
import type { PriceResponseDto } from '@/api/market'
import type { TraceabilityResponseDto, ViolationResult, PagedResult } from '@/api/foodSafety'
import type { OrganicCertificationResult, OrganicCertificationQueryParams} from '@/api/foodSafety'


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

  const violationsPage = ref<PagedResult<ViolationResult> | null>(null)
  const isLoadingViolations = ref(false)
  const violationsError = ref<string | null>(null)

  // 有機農產品驗證查詢
  const organicCertPage = ref<PagedResult<OrganicCertificationResult> | null>(null)
  const isLoadingOrganicCert = ref(false)
  const organicCertError = ref<string | null>(null)

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

  async function fetchViolations(
      days: number,
      inspectResult: string | undefined,
      page: number,
      pageSize: number
    ) {
      isLoadingViolations.value = true
      violationsError.value = null
      try {
        violationsPage.value = await foodSafetyApi.getViolations(days, inspectResult, page, pageSize)
      } catch (e) {
        violationsError.value = '載入農藥違規資料失敗，請稍後再試'
        console.error(e)
      } finally {
        isLoadingViolations.value = false
      }
    }

    // 用來判斷「這次回應是不是最新一次發出的請求」
    // 避免：使用者連續調整篩選條件時，較慢回來的舊請求覆蓋掉較快回來的新結果
    let organicCertRequestSeq = 0

    async function fetchOrganicCertifications(params: OrganicCertificationQueryParams) {
      const mySeq = ++organicCertRequestSeq
      isLoadingOrganicCert.value = true
      organicCertError.value = null
      try {
        const result = await foodSafetyApi.getOrganicCertifications(params)
        if (mySeq !== organicCertRequestSeq) return
        organicCertPage.value = result
      } catch (e) {
        if (mySeq !== organicCertRequestSeq) return
        organicCertError.value = '載入有機驗證資料失敗，請稍後再試'
        console.error(e)
      } finally {
        if (mySeq === organicCertRequestSeq) {
          isLoadingOrganicCert.value = false
        }
      }
    }

    // 文字篩選條件變化時使用：停止輸入一段時間後才真正發送請求，避免每打一個字就打一次 API
    let organicCertDebounceTimer: ReturnType<typeof setTimeout> | null = null

    function fetchOrganicCertificationsDebounced(
      params: OrganicCertificationQueryParams,
      delay = 400
    ) {
      if (organicCertDebounceTimer) clearTimeout(organicCertDebounceTimer)
      organicCertDebounceTimer = setTimeout(() => {
        fetchOrganicCertifications(params)
      }, delay)
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
    //pesticide-violations
    violationsPage,
    isLoadingViolations,
    violationsError,
    fetchViolations,
    //organicCertification
    organicCertPage,
    isLoadingOrganicCert,
    organicCertError,
    fetchOrganicCertifications,
    fetchOrganicCertificationsDebounced,
  }
})