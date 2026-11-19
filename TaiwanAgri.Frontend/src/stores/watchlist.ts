// src/stores/watchlist.ts
// 職責：管理 Watchlist 監看清單的全局狀態

import { defineStore } from 'pinia'
import { ref } from 'vue'
import axios from 'axios'
import { watchlistApi } from '@/api/watchlist'
import { badRequestMessage } from '@/api/apiError'
import type { WatchlistEnrichedItemDto, AddWatchlistRequest } from '@/api/watchlist'

export const useWatchlistStore = defineStore('watchlist', () => {
  // ─── 狀態 ──────────────────────────────────────────────────────────────

  const items = ref<WatchlistEnrichedItemDto[]>([])
  const isLoading = ref(false)
  const isSaving = ref(false)
  // 清單載入失敗與新增／刪除失敗分開記：前者要在清單區顯示錯誤與重試，
  // 不能讓清單區顯示「尚無監看項目」——那會讓使用者以為自己的清單不見了
  const loadError = ref<string | null>(null)
  const errorMessage = ref<string | null>(null)

  // ─── 動作 ──────────────────────────────────────────────────────────────

  /** 取得監看清單 */
  async function fetchItems() {
    isLoading.value = true
    loadError.value = null
    try {
      items.value = await watchlistApi.getItems()
    } catch {
      loadError.value = '載入監看清單失敗'
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
        // 400 帶著後端的原因（例如欄位不合法），照實顯示
        errorMessage.value = badRequestMessage(err) ?? '新增失敗，請稍後再試'
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
    loadError,
    errorMessage,
    fetchItems,
    addItem,
    removeItems,
  }
})