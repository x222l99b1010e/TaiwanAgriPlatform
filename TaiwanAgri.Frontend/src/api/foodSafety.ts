// src/api/foodSafety.ts
// 職責：封裝所有對後端 /api/foodsafety/* 的 HTTP 呼叫
// 只負責「打出去、回傳資料」，不管理任何狀態

import axios from 'axios'
import type { PriceResponseDto } from './market'

const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL,
  headers: { 'Content-Type': 'application/json' },
})

// ─── API 呼叫函式 ──────────────────────────────────────────────────────────

export const foodSafetyApi = {
  /** GET /api/foodsafety/today-veg-prices
   *  後端固定回傳：台北一市場、10 種民生蔬菜、今日均價
   *  無需任何參數
   */
  getTodayVegPrices(): Promise<PriceResponseDto[]> {
    return apiClient
      .get<PriceResponseDto[]>('/api/foodsafety/today-veg-prices')
      .then(res => res.data)
  },
}