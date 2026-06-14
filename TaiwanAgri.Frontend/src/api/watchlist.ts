// src/api/watchlist.ts
// 職責：封裝所有對後端 /api/watchlist/* 的 HTTP 呼叫
// 需要登入，使用帶 JWT header 的 authClient

import authClient from './authClient'

// ─── DTO 型別 ──────────────────────────────────────────────────────────────

export type MarketType = 'Veg' | 'Fruit' | 'Flower'

export interface WatchlistItemDto {
  id: number
  cropCode: string
  cropName: string
  marketCode: string | null
  marketName: string | null
  marketType: MarketType
}

export interface WatchlistEnrichedItemDto {
  id: number
  cropCode: string
  cropName: string
  marketCode: string | null
  marketName: string | null
  marketType: MarketType
  avgPrice: number | null
  transDate: string | null   // DateOnly → JSON 序列化後是字串
}

export interface AddWatchlistRequest {
  cropCode: string
  cropName: string
  marketCode?: string | null
  marketName?: string | null
  marketType: MarketType
}

// ─── API 呼叫函式 ──────────────────────────────────────────────────────────

export const watchlistApi = {
  /** GET /api/watchlist */
  getItems(): Promise<WatchlistEnrichedItemDto[]> {
    return authClient
      .get<WatchlistEnrichedItemDto[]>('/api/watchlist')
      .then(res => res.data)
  },

  /** POST /api/watchlist */
  addItem(request: AddWatchlistRequest): Promise<void> {
    return authClient
      .post('/api/watchlist', request)
      .then(() => undefined)
  },

  /** DELETE /api/watchlist?ids=1&ids=2&ids=3 */
  removeItems(ids: number[]): Promise<void> {
    const params = new URLSearchParams()
    ids.forEach(id => params.append('ids', String(id)))
    return authClient
      .delete('/api/watchlist', { params })
      .then(() => undefined)
  },
}