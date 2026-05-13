// src/stores/market.ts
// 職責：管理 Market 模組的全局狀態
// 多個元件共享的狀態（marketType、markets 列表、crops 列表）放在這裡

import { defineStore } from 'pinia'
import { ref } from 'vue'
import { marketApi } from '@/api/market'
import type { MarketType, MarketResponseDto, CropResponseDto } from '@/api/market'

export const useMarketStore = defineStore('market', () => {
  // ─── 狀態（State） ────────────────────────────────────────────────────────

  // 使用者目前選擇的類別（Veg / Fruit / Flower）
  const marketType = ref<MarketType>('Veg')

  // 市場下拉選單的選項
  const markets = ref<MarketResponseDto[]>([])

  // 作物下拉選單的選項
  const crops = ref<CropResponseDto[]>([])

  // 使用者目前選擇的市場（null = 全台均價）
  const selectedMarketCode = ref<string | null>(null)

  // 使用者目前選擇的作物（最多 5 個）
  const selectedCropCodes = ref<string[]>([])

  // 載入狀態
  const isLoadingMarkets = ref(false)
  const isLoadingCrops = ref(false)

  // 錯誤訊息
  const error = ref<string | null>(null)

  // ─── 動作（Actions） ──────────────────────────────────────────────────────

  /** 切換 marketType，並自動重新載入 markets 和 crops */
  async function setMarketType(type: MarketType) {
    marketType.value = type
    selectedMarketCode.value = null   // 切換類別時重置選擇
    selectedCropCodes.value = []
    await Promise.all([fetchMarkets(), fetchCrops()])
  }

  /** 載入市場列表 */
  async function fetchMarkets() {
    isLoadingMarkets.value = true
    error.value = null
    try {
      markets.value = await marketApi.getMarkets(marketType.value)
    } catch (e) {
      error.value = '載入市場列表失敗，請稍後再試'
      console.error(e)
    } finally {
      isLoadingMarkets.value = false
    }
  }

  /** 載入作物列表 */
  async function fetchCrops() {
    isLoadingCrops.value = true
    error.value = null
    try {
      crops.value = await marketApi.getCrops(marketType.value)
    } catch (e) {
      error.value = '載入作物列表失敗，請稍後再試'
      console.error(e)
    } finally {
      isLoadingCrops.value = false
    }
  }

  /** 初始化：頁面第一次載入時呼叫 */
  async function initialize() {
    await Promise.all([fetchMarkets(), fetchCrops()])
  }

  return {
    // State
    marketType,
    markets,
    crops,
    selectedMarketCode,
    selectedCropCodes,
    isLoadingMarkets,
    isLoadingCrops,
    error,
    // Actions
    setMarketType,
    fetchMarkets,
    fetchCrops,
    initialize,
  }
})
