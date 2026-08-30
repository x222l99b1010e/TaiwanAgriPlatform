// src/api/market.ts
// 職責：封裝所有對後端 /api/market/* 的 HTTP 呼叫
// 只負責「打出去、回傳資料」，不管理任何狀態

import apiClient from './apiClient'

// ─── Response DTO 型別（對應後端 ApiResponses/） ───────────────────────────

export interface CropResponseDto {
  cropCode: string
  cropName: string
}

export interface MarketResponseDto {
  marketCode: string
  marketName: string
}

export interface PriceResponseDto {
  transDate: string
  cropCode: string
  cropName: string
  avgPrice: number
  upperPrice: number
  middlePrice: number
  lowerPrice: number
  transQuantity: number
}

// 更新 interface
export interface DisasterResponseDto {
  disasterName: string
  alertType: string
  alertDate: string           // "yyyy-MM-dd"
  affectedCounties: string[]
}

export interface RestDayResponseDto {
  restDate: string
}

export interface PorkResponseDto{
  transDate: string,
  marketName: string,
  excludeFreezerAvgPrice: number,
  excludeFreezerAvgWeight: number,
  excludeFreezerCount: number
}

// W25 家禽行情：長表設計，一筆對應「某天、某指標」的一個資料點
// PriceStatus 七態：Normal／Empty／Closed／NotQuoted／Negotiated／RangeQuote／Unrecognized
// Price 只有 Normal 才有值，其餘為 null；DisplayName 由後端 PoultryMetrics.cs 帶出，
// 前端不需自備中文對照表（單一真相來源在後端）
export interface PoultryResponseDto {
  transDate: string
  metricCode: string
  displayName: string
  price: number | null
  priceStatus: string
  rawValue: string | null
}

// GET /api/market/poultry/metrics 回傳的指標清單（用來畫指標勾選區，查詢前就要有）
export interface PoultryMetricDto {
  metricCode: string
  displayName: string
}

// ─── Request 參數型別 ──────────────────────────────────────────────────────

export type MarketType = 'Veg' | 'Fruit' | 'Flower'

export interface GetPricesParams {
  marketType: MarketType
  cropCodes: string[]       // 必填，最多 5 個
  marketCode?: string       // 選填，null = 全台均價
  startDate?: string        // 選填，yyyy-MM-dd，預設今天 -365 天
  endDate?: string          // 選填，yyyy-MM-dd，預設今天
}

export interface GetDisastersParams {
  counties?: string[]       // 選填
  startDate: string         // 必填，yyyy-MM-dd
  endDate: string           // 必填，yyyy-MM-dd
}

export interface GetRestDaysParams {
  marketCode: string        // 必填
  startDate: string         // 必填，yyyy-MM-dd
  endDate: string           // 必填，yyyy-MM-dd
}

// ─── API 呼叫函式 ──────────────────────────────────────────────────────────

export const marketApi = {
  /** GET /api/market/crops?marketType=Veg */
  getCrops(marketType: MarketType): Promise<CropResponseDto[]> {
    return apiClient
      .get<CropResponseDto[]>('/api/market/crops', { params: { marketType } })
      .then(res => res.data)
  },

  /** GET /api/market/markets?marketType=Veg */
  getMarkets(marketType: MarketType): Promise<MarketResponseDto[]> {
    return apiClient
      .get<MarketResponseDto[]>('/api/market/markets', { params: { marketType } })
      .then(res => res.data)
  },

  /** GET /api/market/prices?marketType=Veg&cropCodes=1&cropCodes=2&... */
  getPrices(params: GetPricesParams): Promise<PriceResponseDto[]> {
    // cropCodes 是陣列，axios 需要用 URLSearchParams 才能產生重複參數
    const searchParams = new URLSearchParams()
    searchParams.append('marketType', params.marketType)
    params.cropCodes.forEach(code => searchParams.append('cropCodes', code))
    if (params.marketCode) searchParams.append('marketCode', params.marketCode)
    if (params.startDate) searchParams.append('startDate', params.startDate)
    if (params.endDate) searchParams.append('endDate', params.endDate)

    return apiClient
      .get<PriceResponseDto[]>('/api/market/prices', { params: searchParams })
      .then(res => res.data)
  },

  /** GET /api/market/disasters?counties=台北市&counties=新北市&... */
  // 更新 getDisasters（counties 選填，不傳 = 全台）
  getDisasters: async (params: {
    startDate: string
    endDate: string
    counties?: string[]
  }): Promise<DisasterResponseDto[]> => {
    const res = await apiClient.get<DisasterResponseDto[]>('/api/market/disasters', { params })
    return res.data
  },

  /** GET /api/market/restdays?marketCode=101&startDate=...&endDate=... */
  getRestDays(params: GetRestDaysParams): Promise<RestDayResponseDto[]> {
    return apiClient
      .get<RestDayResponseDto[]>('/api/market/rest-days', { params })
      .then(res => res.data)
  },

  getPork(params: {
    marketName?: string
    startDate?: string
    endDate?: string
  }): Promise<PorkResponseDto[]>{
    return apiClient
    .get<PorkResponseDto[]>('/api/market/pork', {params})
      .then(res =>res.data)
  },

  /** GET /api/market/poultry?metricCodes=A&metricCodes=B&startDate=...&endDate=... */
  getPoultry(params: {
    metricCodes?: string[]
    startDate?: string
    endDate?: string
  }): Promise<PoultryResponseDto[]> {
    // metricCodes 是陣列，比照 getPrices 的 cropCodes 用 URLSearchParams 才能產生重複參數
    const searchParams = new URLSearchParams()
    params.metricCodes?.forEach(code => searchParams.append('metricCodes', code))
    if (params.startDate) searchParams.append('startDate', params.startDate)
    if (params.endDate) searchParams.append('endDate', params.endDate)

    return apiClient
      .get<PoultryResponseDto[]>('/api/market/poultry', { params: searchParams })
      .then(res => res.data)
  },

  /** GET /api/market/poultry/metrics：指標清單，查詢前就要用來畫勾選區 */
  getPoultryMetrics(): Promise<PoultryMetricDto[]> {
    return apiClient
      .get<PoultryMetricDto[]>('/api/market/poultry/metrics')
      .then(res => res.data)
  },
}
