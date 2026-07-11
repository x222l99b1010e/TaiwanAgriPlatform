// src/stores/foodSafety.ts
// 職責：管理 FoodSafety 模組的全局狀態
// W21a 今日菜價快覽、W21b 農產品追溯查詢

import { defineStore } from 'pinia'
import { ref } from 'vue'
import { foodSafetyApi } from '@/api/foodSafety'
import { useLatestRequest } from '@/composables/useLatestRequest'
import type { PriceResponseDto } from '@/api/market'
import type { TraceabilityResponseDto, ViolationResult, PagedResult } from '@/api/foodSafety'
import type { OrganicCertificationResult, OrganicCertificationQueryParams} from '@/api/foodSafety'


export const useFoodSafetyStore = defineStore('foodSafety', () => {
  // ─── 狀態（State） ────────────────────────────────────────────────────────

  // 今日蔬菜均價資料
  // 狀態命名統一 todayVeg 前綴，與違規牆（violations*）／有機驗證（organicCert*）一致
  const todayVegPrices = ref<PriceResponseDto[]>([])

  // 載入狀態
  const isLoadingTodayVeg = ref(false)

  // 錯誤訊息
  const todayVegError = ref<string | null>(null)

  // 是否已經載入過（避免重複打 API）
  // 搭配 lastFetchedAt 做 TTL：頁面開著跨過資料更新時間時，重新進頁仍能拿到新資料
  const todayVegHasFetched = ref(false)
  const TODAY_VEG_TTL_MS = 10 * 60 * 1000
  let todayVegLastFetchedAt = 0

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

  /** 載入今日蔬菜均價（todayVegHasFetched + TTL 保護，10 分鐘內不重複打 API） */
  async function fetchTodayVegPrices() {
    if (todayVegHasFetched.value && Date.now() - todayVegLastFetchedAt < TODAY_VEG_TTL_MS) return

    isLoadingTodayVeg.value = true
    todayVegError.value = null
    try {
      todayVegPrices.value = await foodSafetyApi.getTodayVegPrices()
      todayVegHasFetched.value = true
      todayVegLastFetchedAt = Date.now()
    } catch (e) {
      todayVegError.value = '載入今日菜價失敗，請稍後再試'
      console.error(e)
    } finally {
      isLoadingTodayVeg.value = false
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

    // 請求序號防競態：舊回應不覆蓋新結果（違規牆與有機驗證各自獨立計數）
    const violationsRequest = useLatestRequest()
    const organicCertRequest = useLatestRequest()

  async function fetchViolations(
      days: number,
      inspectResult: string | undefined,
      page: number,
      pageSize: number
    ) {
      const mySeq = violationsRequest.next()
      isLoadingViolations.value = true
      violationsError.value = null
      try {
        const result = await foodSafetyApi.getViolations(days, inspectResult, page, pageSize)
        if (!violationsRequest.isLatest(mySeq)) return
        violationsPage.value = result
      } catch (e) {
        if (!violationsRequest.isLatest(mySeq)) return
        violationsError.value = '載入農藥違規資料失敗，請稍後再試'
        console.error(e)
      } finally {
        if (violationsRequest.isLatest(mySeq)) {
          isLoadingViolations.value = false
        }
      }
    }

    async function fetchOrganicCertifications(params: OrganicCertificationQueryParams) {
      const mySeq = organicCertRequest.next()
      isLoadingOrganicCert.value = true
      organicCertError.value = null
      try {
        const result = await foodSafetyApi.getOrganicCertifications(params)
        if (!organicCertRequest.isLatest(mySeq)) return
        organicCertPage.value = result
      } catch (e) {
        if (!organicCertRequest.isLatest(mySeq)) return
        organicCertError.value = '載入有機驗證資料失敗，請稍後再試'
        console.error(e)
      } finally {
        if (organicCertRequest.isLatest(mySeq)) {
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
    isLoadingTodayVeg,
    todayVegError,
    todayVegHasFetched,
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