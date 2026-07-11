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
}

export function usePagination(options: UsePaginationOptions) {
  const pageSizeOptions = options.pageSizeOptions ?? [10, 20, 50, 100]
  const defaultPageSize = options.defaultPageSize ?? 20

  // 從 localStorage 讀取上次選擇；不在合法選項內（含未存過的 NaN）就用預設值
  const storedPageSize = Number(localStorage.getItem(options.storageKey))
  const pageSize = ref(pageSizeOptions.includes(storedPageSize) ? storedPageSize : defaultPageSize)

  const currentPage = ref(1)
  const jumpPageInput = ref<number | null>(null)

  /** 分頁按鈕：最多顯示 5 個頁碼，以目前頁為中心 */
  const visiblePages = computed(() => {
    const total = options.totalPages() ?? 0
    const current = currentPage.value
    const range = 2
    const start = Math.max(1, current - range)
    const end = Math.min(total, current + range)
    const pages: number[] = []
    for (let i = start; i <= end; i++) pages.push(i)
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
   * 每頁筆數變更處理。
   * 刻意不用 v-model + @change 混用（曾經發生 handler 讀到舊值的時序問題），
   * 改成從原生 change 事件直接取值、手動賦值，確保 pageSize 更新完成後才觸發查詢。
   * 不管會不會重新查詢，都先存進 localStorage 記住這個選擇；
   * shouldRefetch=false 讓「尚未查詢過」的頁面只記住選擇、不打 API（違規牆行為）
   */
  function handlePageSizeChange(event: Event, shouldRefetch = true) {
    const newSize = Number((event.target as HTMLSelectElement).value)
    pageSize.value = newSize
    localStorage.setItem(options.storageKey, String(newSize))
    if (shouldRefetch) {
      currentPage.value = 1
      options.onChange()
    }
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
    handlePageSizeChange,
    rowNumber,
  }
}
