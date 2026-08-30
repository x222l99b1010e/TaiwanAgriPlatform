// src/api/foodSafety.ts
// 職責：封裝所有對後端 /api/foodsafety/* 的 HTTP 呼叫
// 只負責「打出去、回傳資料」，不管理任何狀態

import apiClient from './apiClient'
import type { PagedResult } from './pagination'
import type { PriceResponseDto } from './market'

// 補充型別定義
export interface AgriProductResult {
  product: string
  place: string
  mark: string
}

export interface AgriProducerResult {
  producer: string
  address: string
  mark: string
  status: string
  description: string
}

export interface WashedEggResult {
  tracenoStart: string
  tracenoEnd: string
  selName: string
  selAddr: string
  selBoss: string
  eggName1: string
  farTownName1: string
  eggName2: string
  farTownName2: string
  eggName3: string
  farTownName3: string
}

export interface PoultryResult {
  tracenoStart: string
  tracenoEnd: string
  kilName: string
  kilAddr: string
  kilBoss: string
  farmersName1: string
  farmersType1: string
  farmersplace1: string
  farmersName2: string
  farmersType2: string
  farmersplace2: string
  cdate: string
}

export interface TraceabilityResponseDto {
  traceCode: string
  agriProducts: AgriProductResult[] | null
  producer: AgriProducerResult | null
  washedEgg: WashedEggResult | null
  poultry: PoultryResult | null
}

export interface ViolationResult {
  number: string
  samplingDate: string       // DateOnly 在 JSON 序列化後是 "2026-04-03" 這種字串
  productName: string
  producerName: string
  samplingLocation: string
  inspectResult: string
  note: string
}

// 分頁契約型別集中於 pagination.ts；re-export 以維持既有 import 路徑
export type { PagedResult }

// ─── 有機農產品驗證查詢：新增型別 ──────────────────────────────────────────

export interface OrganicCertificationQueryParams {
  operatorName?: string
  verificationBodyName?: string
  productKeyword?: string
  page: number
  pageSize: number
}

export interface OrganicCertificationResult {
  id: number
  certOrganicSn: string
  operatorName: string
  address: string
  tel: string
  products: string
  behaviorType: string
  verificationBodyName: string
  effectiveDate: string | null   // DateOnly? 序列化後是 "2026-04-03" 或 null
  status: string
  productScope: string
  mailingAddress: string
  legacyCertNumber: string
  hasAmbiguousProductMapping: boolean
}

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

  searchTraceability(traceCode: string): Promise<TraceabilityResponseDto> {
    return apiClient
      .get<TraceabilityResponseDto>('/api/foodsafety/traceability', {
        params: { traceCode }
      })
      .then(res => res.data)
  },

  getViolations(
    days: number,
    inspectResult: string | undefined,
    page: number,
    pageSize: number
  ): Promise<PagedResult<ViolationResult>> {
    return apiClient
      .get<PagedResult<ViolationResult>>('/api/foodsafety/violations', {
        params: { days, inspectResult, page, pageSize }
      })
      .then(res => res.data)
  },

  getOrganicCertifications(
    params: OrganicCertificationQueryParams
  ): Promise<PagedResult<OrganicCertificationResult>> {
    return apiClient
      .get<PagedResult<OrganicCertificationResult>>('/api/foodsafety/organic-certifications', {
        params
      })
      .then(res => res.data)
  },
}