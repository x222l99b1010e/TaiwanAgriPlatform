// src/composables/usePagination.ts
// 職責：後端分頁的前端控制邏輯共用化
//
// ViolationWallView 與 OrganicCertView 的分頁控制（頁碼視窗、換頁、跳頁、
// 每頁筆數）原本逐字重複，抽到這裡；pageSize 一律記憶 localStorage，
// 統一兩頁原本不一致的記憶行為。純邏輯不含 UI，方便單元測試。

import { ref, computed } from 'vue'

export interface UsePaginationOptions {
  /** pageSize 記憶在 localStorage 的 key（各頁面自取，如 'violationWall.pageSize'） */
  storageKey: string
  /** 取得目前總頁數；尚未查詢（無分頁結果）時回傳 undefined */
  totalPages: () => number | undefined
  /** 頁碼或每頁筆數確定變更後觸發的查詢 */
  onChange: () => void
  pageSizeOptions?: number[]
  defaultPageSize?: number
  /**
   * 頁碼按鈕視窗至少顯示幾個（頁數足夠時），預設 6。貼近頭尾時往另一側平移補滿到這個數字，
   * 不是縮短（例如目前在第 1 頁、總共 10 頁，會顯示 [1,2,3,4,5,6] 而不是舊版的 [1,2,3]）。
   * 這是共用邏輯，改這裡全部消費端（ViolationWallView／OrganicCertView／LostPetsView／
   * LegalBusinessView）都會一起套用，owner 2026-08-06 要求「至少顯示 6 頁」時裁示維持共用、
   * 不要拆成新舊模組兩套行為。
   */
  minVisibleCount?: number
}

export function usePagination(options: UsePaginationOptions) {
  const pageSizeOptions = options.pageSizeOptions ?? [10, 20, 50, 100]
  const defaultPageSize = options.defaultPageSize ?? 20

  // 從 localStorage 讀取上次選擇；不在合法選項內（含未存過的 NaN）就用預設值
  const storedPageSize = Number(localStorage.getItem(options.storageKey))
  const pageSize = ref(pageSizeOptions.includes(storedPageSize) ? storedPageSize : defaultPageSize)

  const currentPage = ref(1)
  const jumpPageInput = ref<number | null>(null)

  const minVisibleCount = options.minVisibleCount ?? 6

  /**
   * 分頁按鈕視窗：視窗大小固定（頁數足夠時），貼近頭尾時往另一側平移補滿，不是縮短。
   * 例如目前在第 1 頁、總共 10 頁、minVisibleCount=6，顯示 [1,2,3,4,5,6]；
   * 目前在最後一頁，顯示會平移到 [5,6,7,8,9,10]，而不是只剩 [8,9,10]。
   */
  const visiblePages = computed(() => {
    const total = options.totalPages() ?? 0
    const current = currentPage.value
    if (total <= 0) return []

    const windowSize = Math.min(minVisibleCount, total)
    let start = current - Math.floor((windowSize - 1) / 2)
    start = Math.max(1, Math.min(start, total - windowSize + 1))
    const pages: number[] = []
    for (let i = 0; i < windowSize; i++) pages.push(start + i)
    return pages
  })

  function changePage(p: number) {
    const total = options.totalPages()
    if (total == null) return
    if (p < 1 || p > total) return
    currentPage.value = p
    options.onChange()
  }

  function handleJumpPage() {
    const total = options.totalPages()
    if (total == null || !jumpPageInput.value) return
    changePage(Math.min(Math.max(1, jumpPageInput.value), total))
    jumpPageInput.value = null
  }

  /**
   * 每頁筆數變更。
   * 刻意不用 v-model + @change 混用（曾經發生 handler 讀到舊值的時序問題），
   * 改成由呼叫端把新的值直接交進來、手動賦值，確保 pageSize 更新完成後才觸發查詢。
   * 不管會不會重新查詢，都先存進 localStorage 記住這個選擇；
   * shouldRefetch=false 讓「尚未查詢過」的頁面只記住選擇、不打 API（違規牆行為）
   */
  function setPageSize(newSize: number, shouldRefetch = true) {
    pageSize.value = newSize
    localStorage.setItem(options.storageKey, String(newSize))
    if (shouldRefetch) {
      currentPage.value = 1
      options.onChange()
    }
  }

  /**
   * 上面那一支的原生事件版本。
   * PagerBar 收斂完成後，畫面上已經沒有頁面直接掛 @change 了——保留是因為它是
   * 「從 DOM 事件取值」與「改狀態」的分界點，之後若有非 PagerBar 的地方要用，
   * 不必再寫一次 `Number((e.target as HTMLSelectElement).value)` 這串轉型。
   */
  function handlePageSizeChange(event: Event, shouldRefetch = true) {
    setPageSize(Number((event.target as HTMLSelectElement).value), shouldRefetch)
  }

  /**
   * 計算表格序號：不是「這一頁裡的第幾筆」，而是「在全部符合條件的資料中排第幾筆」。
   * 例如 pageSize=20，目前在第 2 頁，這一頁第 1 筆（index=0）就是全域第 21 筆。
   */
  function rowNumber(index: number): number {
    return (currentPage.value - 1) * pageSize.value + index + 1
  }

  return {
    pageSizeOptions,
    pageSize,
    currentPage,
    jumpPageInput,
    visiblePages,
    changePage,
    handleJumpPage,
    setPageSize,
    handlePageSizeChange,
    rowNumber,
  }
}
