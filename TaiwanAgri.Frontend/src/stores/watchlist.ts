// src/stores/watchlist.ts
// 職責：管理 Watchlist 監看清單的全局狀態

import { defineStore } from 'pinia'
import { ref } from 'vue'
import axios from 'axios'
import { watchlistApi } from '@/api/watchlist'
import type { WatchlistEnrichedItemDto, AddWatchlistRequest } from '@/api/watchlist'

export const useWatchlistStore = defineStore('watchlist', () => {
  // ─── 狀態 ──────────────────────────────────────────────────────────────

  const items = ref<WatchlistEnrichedItemDto[]>([])
  const isLoading = ref(false)
  const isSaving = ref(false)
  const errorMessage = ref<string | null>(null)

  // ─── 動作 ──────────────────────────────────────────────────────────────

  /** 取得監看清單 */
  async function fetchItems() {
    isLoading.value = true
    errorMessage.value = null
    try {
      items.value = await watchlistApi.getItems()
    } catch {
      errorMessage.value = '載入監看清單失敗'
    } finally {
      isLoading.value = false
    }
  }

  /** 新增一筆監看 */
  async function addItem(request: AddWatchlistRequest) {
    isSaving.value = true
    errorMessage.value = null
    try {
      await watchlistApi.addItem(request)
      await fetchItems()
    } catch (err) {
      if (axios.isAxiosError(err) && err.response?.status === 409) {
        errorMessage.value = '此作物與市場組合已在監看清單中'
      } else {
        errorMessage.value = '新增失敗，請稍後再試'
      }
    } finally {
      isSaving.value = false
    }
  }

  /** 刪除多筆監看 */
  async function removeItems(ids: number[]) {
    isSaving.value = true
    errorMessage.value = null
    try {
      await watchlistApi.removeItems(ids)
      await fetchItems()
    } catch {
      errorMessage.value = '刪除失敗，請稍後再試'
    } finally {
      isSaving.value = false
    }
  }

  return {
    items,
    isLoading,
    isSaving,
    errorMessage,
    fetchItems,
    addItem,
    removeItems,
  }
})