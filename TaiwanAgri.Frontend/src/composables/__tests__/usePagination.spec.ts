import { describe, it, expect, beforeEach, vi } from 'vitest'
import { usePagination } from '../usePagination'

// environment 是 'node'（見 vitest.config.ts），沒有瀏覽器的 localStorage，
// 用最簡單的 Map 實作一份掛到 globalThis，範圍只需要 getItem/setItem
function installFakeLocalStorage() {
  const store = new Map<string, string>()
  globalThis.localStorage = {
    getItem: (key: string) => store.get(key) ?? null,
    setItem: (key: string, value: string) => { store.set(key, value) },
    removeItem: (key: string) => { store.delete(key) },
    clear: () => store.clear(),
    key: () => null,
    get length() { return store.size },
  } as Storage
}

describe('usePagination', () => {
  beforeEach(() => {
    installFakeLocalStorage()
  })

  describe('pageSize 初始值（localStorage 記憶）', () => {
    it('沒有存過值時，用 defaultPageSize', () => {
      const { pageSize } = usePagination({
        storageKey: 'test.pageSize',
        totalPages: () => 10,
        onChange: vi.fn(),
        defaultPageSize: 20,
      })
      expect(pageSize.value).toBe(20)
    })

    it('存過合法值時，讀回上次選擇', () => {
      localStorage.setItem('test.pageSize', '50')
      const { pageSize } = usePagination({
        storageKey: 'test.pageSize',
        totalPages: () => 10,
        onChange: vi.fn(),
        pageSizeOptions: [10, 20, 50, 100],
      })
      expect(pageSize.value).toBe(50)
    })

    it('存的值不在合法選項內（含未存過的 NaN）時，退回 defaultPageSize', () => {
      localStorage.setItem('test.pageSize', '999')
      const { pageSize } = usePagination({
        storageKey: 'test.pageSize',
        totalPages: () => 10,
        onChange: vi.fn(),
        pageSizeOptions: [10, 20, 50, 100],
        defaultPageSize: 20,
      })
      expect(pageSize.value).toBe(20)
    })
  })

  describe('visiblePages（頁碼視窗，預設固定顯示 6 個，貼近頭尾時平移補滿而非縮短）', () => {
    it('目前頁在中間時，視窗以目前頁為中心（偏左，因為 6 是偶數湊不出完全對稱）', () => {
      const { currentPage, visiblePages, changePage } = usePagination({
        storageKey: 'test.window.middle',
        totalPages: () => 10,
        onChange: vi.fn(),
      })
      changePage(5)
      expect(currentPage.value).toBe(5)
      expect(visiblePages.value).toEqual([3, 4, 5, 6, 7, 8])
    })

    it('目前頁在最前面時，視窗往後平移補滿到 6 個，不會縮短成只剩 [1,2,3]', () => {
      const { visiblePages } = usePagination({
        storageKey: 'test.window.start',
        totalPages: () => 10,
        onChange: vi.fn(),
      })
      // 初始 currentPage 就是 1
      expect(visiblePages.value).toEqual([1, 2, 3, 4, 5, 6])
    })

    it('目前頁在最後面時，視窗往前平移補滿到 6 個', () => {
      const { visiblePages, changePage } = usePagination({
        storageKey: 'test.window.end',
        totalPages: () => 10,
        onChange: vi.fn(),
      })
      changePage(10)
      expect(visiblePages.value).toEqual([5, 6, 7, 8, 9, 10])
    })

    it('總頁數不足 6 頁時，視窗大小跟著縮小成總頁數，不會出現不存在的頁碼', () => {
      const { visiblePages } = usePagination({
        storageKey: 'test.window.fewerThanMin',
        totalPages: () => 4,
        onChange: vi.fn(),
      })
      expect(visiblePages.value).toEqual([1, 2, 3, 4])
    })

    it('尚未查詢過（totalPages 回傳 undefined）時，視窗是空陣列', () => {
      const { visiblePages } = usePagination({
        storageKey: 'test.window.unqueried',
        totalPages: () => undefined,
        onChange: vi.fn(),
      })
      expect(visiblePages.value).toEqual([])
    })

    it('minVisibleCount 仍可自訂覆寫預設值（共用邏輯保留彈性，非所有頁面都被綁死 6）', () => {
      const { visiblePages, changePage } = usePagination({
        storageKey: 'test.window.customCount',
        totalPages: () => 10,
        onChange: vi.fn(),
        minVisibleCount: 3,
      })
      changePage(5)
      expect(visiblePages.value).toEqual([4, 5, 6])
    })
  })

  describe('changePage', () => {
    it('合法頁碼：更新 currentPage 並觸發 onChange', () => {
      const onChange = vi.fn()
      const { currentPage, changePage } = usePagination({
        storageKey: 'test.changePage.valid',
        totalPages: () => 5,
        onChange,
      })
      changePage(3)
      expect(currentPage.value).toBe(3)
      expect(onChange).toHaveBeenCalledTimes(1)
    })

    it('超出範圍（小於 1 或大於 totalPages）：不更新、不觸發 onChange', () => {
      const onChange = vi.fn()
      const { currentPage, changePage } = usePagination({
        storageKey: 'test.changePage.outOfRange',
        totalPages: () => 5,
        onChange,
      })
      changePage(0)
      changePage(-1)
      changePage(6)
      expect(currentPage.value).toBe(1) // 維持初始值，沒被任何一次非法呼叫改動
      expect(onChange).not.toHaveBeenCalled()
    })

    it('totalPages 回傳 undefined（尚未查詢過）時，一律不動作', () => {
      const onChange = vi.fn()
      const { currentPage, changePage } = usePagination({
        storageKey: 'test.changePage.unqueried',
        totalPages: () => undefined,
        onChange,
      })
      changePage(2)
      expect(currentPage.value).toBe(1)
      expect(onChange).not.toHaveBeenCalled()
    })
  })

  describe('handleJumpPage', () => {
    it('輸入合法頁碼：換頁、觸發 onChange、清空輸入框', () => {
      const onChange = vi.fn()
      const { currentPage, jumpPageInput, handleJumpPage } = usePagination({
        storageKey: 'test.jump.valid',
        totalPages: () => 10,
        onChange,
      })
      jumpPageInput.value = 7
      handleJumpPage()
      expect(currentPage.value).toBe(7)
      expect(onChange).toHaveBeenCalledTimes(1)
      expect(jumpPageInput.value).toBeNull()
    })

    it('輸入超出範圍的頁碼：夾到合法範圍內（不是被拒絕，是被修正）', () => {
      const { currentPage, jumpPageInput, handleJumpPage } = usePagination({
        storageKey: 'test.jump.clamp',
        totalPages: () => 10,
        onChange: vi.fn(),
      })
      jumpPageInput.value = 999
      handleJumpPage()
      expect(currentPage.value).toBe(10)
    })

    it('輸入框是 null（未填）時，不動作', () => {
      const onChange = vi.fn()
      const { currentPage, handleJumpPage } = usePagination({
        storageKey: 'test.jump.empty',
        totalPages: () => 10,
        onChange,
      })
      handleJumpPage()
      expect(currentPage.value).toBe(1)
      expect(onChange).not.toHaveBeenCalled()
    })
  })

  describe('handlePageSizeChange', () => {
    function fakeChangeEvent(value: string): Event {
      // Node 環境沒有真的 HTMLSelectElement，只需要 .value 這個欄位就滿足程式邏輯的需求
      return { target: { value } } as unknown as Event
    }

    it('shouldRefetch=true（預設）：更新 pageSize、存 localStorage、重置回第一頁、觸發 onChange', () => {
      const onChange = vi.fn()
      const { pageSize, currentPage, changePage, handlePageSizeChange } = usePagination({
        storageKey: 'test.pageSizeChange.refetch',
        totalPages: () => 10,
        onChange,
      })
      changePage(5) // 先跳到非第一頁，確認等一下真的被重置

      handlePageSizeChange(fakeChangeEvent('50'))

      expect(pageSize.value).toBe(50)
      expect(currentPage.value).toBe(1)
      expect(localStorage.getItem('test.pageSizeChange.refetch')).toBe('50')
      expect(onChange).toHaveBeenCalledTimes(2) // changePage(5) 一次 + handlePageSizeChange 一次
    })

    it('shouldRefetch=false：只記住選擇存進 localStorage，不重置頁碼、不觸發 onChange（未查詢前的頁面用這個）', () => {
      const onChange = vi.fn()
      const { pageSize, currentPage, handlePageSizeChange } = usePagination({
        storageKey: 'test.pageSizeChange.noRefetch',
        totalPages: () => undefined, // 尚未查詢過
        onChange,
      })

      handlePageSizeChange(fakeChangeEvent('100'), false)

      expect(pageSize.value).toBe(100)
      expect(currentPage.value).toBe(1) // 沒被動過，本來就是 1
      expect(localStorage.getItem('test.pageSizeChange.noRefetch')).toBe('100')
      expect(onChange).not.toHaveBeenCalled()
    })
  })

  describe('rowNumber（表格序號＝全域排名，不是頁內排名）', () => {
    it('第一頁的序號從 1 開始', () => {
      const { rowNumber } = usePagination({
        storageKey: 'test.rowNumber.page1',
        totalPages: () => 5,
        onChange: vi.fn(),
        defaultPageSize: 20,
      })
      expect(rowNumber(0)).toBe(1)
      expect(rowNumber(19)).toBe(20)
    })

    it('第三頁、每頁 20 筆時，第一筆是全域第 41 筆', () => {
      const { changePage, rowNumber } = usePagination({
        storageKey: 'test.rowNumber.page3',
        totalPages: () => 5,
        onChange: vi.fn(),
        defaultPageSize: 20,
      })
      changePage(3)
      expect(rowNumber(0)).toBe(41)
      expect(rowNumber(4)).toBe(45)
    })
  })
})
