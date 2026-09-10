// src/api/notificationRule.ts
// 職責：通知規則 CRUD 與「立即檢查」的 API 契約與呼叫函式。
//
// 一律走 authClient：規則是使用者自己的資料，身分由 JWT 帶，網址裡不出現 userId。
// 與 weather.ts 的 notificationApi 分成兩個檔：那支讀「通知」，這支管「產生通知的規則」，
// 是兩個不同的資源（後端也是兩支獨立的 controller）。

import authClient from '@/api/authClient'
import type { PagedResult } from './pagination'

export type { PagedResult }

/** 規則型態。數值門檻比氣象觀測的量測值，事件比植物疫情警報 */
export type NotificationRuleType = 'Numeric' | 'Event'

/** 資料來源。與規則型態一對一，後端會驗證兩者相符 */
export type NotificationSourceTable = 'WeatherObservation' | 'PlantEpidemic'

/** 數值型要比對的觀測項目 */
export type NotificationMetricName = 'Temperature' | 'Rainfall24h'

/** 門檻的比較方向 */
export type NotificationComparison = 'GreaterThan' | 'LessThan'

export interface NotificationRuleDto {
  id: number
  ruleName: string
  ruleType: NotificationRuleType
  sourceTable: NotificationSourceTable
  isActive: boolean
  expiryDays: number
  createdAt: string
  filterCity: string | null
  filterPlantName: string | null
  /** ISO 日期字串（yyyy-MM-dd），事件型專用 */
  filterDateFrom: string | null
  metricName: NotificationMetricName | null
  comparison: NotificationComparison | null
  threshold: number | null
  /** 這條規則已產生的通知則數，刪除確認要顯示「會一併刪掉幾則」 */
  notificationCount: number
}

/**
 * 建立與更新共用同一份請求，因為兩者的欄位完全相同。
 * 另一個型態專用的欄位可以留 null，後端一律會清空——所以切換型態時不必先把舊值抹乾淨。
 */
export interface NotificationRuleRequest {
  ruleName: string
  ruleType: NotificationRuleType
  sourceTable: NotificationSourceTable
  isActive: boolean
  /** null＝沿用後端依型態決定的預設值（事件型 30 天、數值型 7 天） */
  expiryDays: number | null
  filterCity: string | null
  filterPlantName: string | null
  filterDateFrom: string | null
  metricName: NotificationMetricName | null
  comparison: NotificationComparison | null
  threshold: number | null
}

/**
 * 一次評估的結果。
 * 除了則數之外還帶四個欄位，是因為「沒有新通知」有三種成因，而使用者需要分得出來：
 * 條件沒命中、同步 Worker 沒在跑（最新觀測已經是好幾天前的）、
 * 以及上次檢查之後根本沒有新的觀測落地（掃描範圍是空的，門檻怎麼改都是 0 則）。
 * 少了它們，三種情況在畫面上長得一模一樣，使用者只能猜是不是壞了。
 */
export interface RuleEvaluationDto {
  rulesEvaluated: number
  notificationsCreated: number
  latestObservedAt: string | null
  hasFreshObservation: boolean
  /** 實際跑過比對的數值型規則數；缺門檻或來源不合法而被跳過的不算 */
  numericRulesEvaluated: number
  /** 上述規則裡，這一輪真的有新觀測可以比對的（水位還沒追到最新落地時刻）規則數 */
  numericRulesWithNewObservations: number
}

export const notificationRuleApi = {
  /** GET /api/NotificationRule?page=1&pageSize=20 */
  getRules(page = 1, pageSize = 20): Promise<PagedResult<NotificationRuleDto>> {
    return authClient
      .get<PagedResult<NotificationRuleDto>>('/api/NotificationRule', { params: { page, pageSize } })
      .then(res => res.data)
  },

  /** POST /api/NotificationRule —— 已達規則數上限時回 400，訊息可直接顯示 */
  createRule(request: NotificationRuleRequest): Promise<NotificationRuleDto> {
    return authClient
      .post<NotificationRuleDto>('/api/NotificationRule', request)
      .then(res => res.data)
  },

  /** PUT /api/NotificationRule/{id} —— 回 204，沒有內容可帶回，呼叫端自行重查 */
  updateRule(id: number, request: NotificationRuleRequest): Promise<void> {
    return authClient.put(`/api/NotificationRule/${id}`, request).then(() => undefined)
  },

  /** DELETE /api/NotificationRule/{id} —— 該規則產生的通知會由資料庫連帶刪除 */
  deleteRule(id: number): Promise<void> {
    return authClient.delete(`/api/NotificationRule/${id}`).then(() => undefined)
  },

  /** POST /api/NotificationRule/evaluate —— 有限流，短時間連按會回 429 */
  evaluateNow(): Promise<RuleEvaluationDto> {
    return authClient
      .post<RuleEvaluationDto>('/api/NotificationRule/evaluate')
      .then(res => res.data)
  },
}
