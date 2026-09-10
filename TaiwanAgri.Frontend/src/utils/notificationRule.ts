// src/utils/notificationRule.ts
// 職責：通知規則畫面用得到的顯示標籤、輸入界限，以及「立即檢查」結果要說什麼。

import type {
  NotificationComparison,
  NotificationMetricName,
  NotificationRuleType,
  NotificationSourceTable,
  RuleEvaluationDto,
} from '@/api/notificationRule'

/**
 * 型態與來源是一對一的。表單只讓使用者選型態，來源由這裡推出來——
 * 讓使用者選兩個彼此決定的欄位，只會多一種「配錯就永遠不會觸發」的填法，
 * 而後端會為此回 400。
 */
export const SOURCE_TABLE_BY_RULE_TYPE: Record<NotificationRuleType, NotificationSourceTable> = {
  Numeric: 'WeatherObservation',
  Event: 'PlantEpidemic',
}

/**
 * 顯示標籤。後端沒有提供這幾組值的中文對照（它只在通知訊息裡自己組字串），
 * 所以這裡自行命名不算重造一份真相來源——判準是「會不會分岔」，
 * 後端有唯一來源的東西前端不能再造一份，後端根本沒有的則不構成重複。
 */
export const RULE_TYPE_OPTIONS: { value: NotificationRuleType; label: string }[] = [
  { value: 'Numeric', label: '數值門檻' },
  { value: 'Event', label: '植物疫情' },
]

export const METRIC_OPTIONS: { value: NotificationMetricName; label: string; unit: string }[] = [
  { value: 'Temperature', label: '氣溫', unit: '°C' },
  { value: 'Rainfall24h', label: '24 小時雨量', unit: 'mm' },
]

export const COMPARISON_OPTIONS: { value: NotificationComparison; label: string }[] = [
  { value: 'GreaterThan', label: '超過' },
  { value: 'LessThan', label: '低於' },
]

/**
 * 輸入界限。
 * ⚠ 這幾個數字後端也有一份，而且**後端那份才是防護**——前端這份只用來把輸入框的
 * min／max 標出來、少讓使用者白填一次。兩份不一致時以後端為準（它會回 400）。
 * 之所以仍然複製：沒有它，使用者要按下送出才知道 200 天不行，而那是可以當場講的事。
 */
export const RULE_LIMITS = {
  maxRulesPerUser: 7,
  minExpiryDays: 1,
  expiryDaysByRuleType: {
    Numeric: { defaultValue: 7, max: 30 },
    Event: { defaultValue: 30, max: 180 },
  } satisfies Record<NotificationRuleType, { defaultValue: number; max: number }>,
  thresholdMin: -999.9,
  thresholdMax: 999.9,
  /** 門檻值收到小數點後幾位；資料庫欄位精度就是這個 */
  thresholdDecimals: 1,
} as const

export function ruleTypeLabel(ruleType: NotificationRuleType): string {
  return RULE_TYPE_OPTIONS.find(o => o.value === ruleType)?.label ?? ruleType
}

export function metricLabel(metricName: NotificationMetricName | null): string {
  if (metricName === null) return ''
  return METRIC_OPTIONS.find(o => o.value === metricName)?.label ?? metricName
}

export function metricUnit(metricName: NotificationMetricName | null): string {
  if (metricName === null) return ''
  return METRIC_OPTIONS.find(o => o.value === metricName)?.unit ?? ''
}

export function comparisonLabel(comparison: NotificationComparison | null): string {
  if (comparison === null) return ''
  return COMPARISON_OPTIONS.find(o => o.value === comparison)?.label ?? comparison
}

/** 門檻值是不是只有一位小數。後端會擋，這裡先擋是為了讓使用者當場知道 */
export function hasAllowedThresholdPrecision(threshold: number): boolean {
  const scaled = threshold * 10 ** RULE_LIMITS.thresholdDecimals
  // 浮點數乘完會出現 325.00000000000006 這種尾巴，所以比的是「離最近的整數夠不夠近」
  return Math.abs(scaled - Math.round(scaled)) < 1e-9
}

/** 一條規則的條件摘要，顯示在卡片上讓使用者一眼看出它在比什麼 */
export function ruleConditionSummary(rule: {
  ruleType: NotificationRuleType
  filterCity: string | null
  filterPlantName: string | null
  filterDateFrom: string | null
  metricName: NotificationMetricName | null
  comparison: NotificationComparison | null
  threshold: number | null
}): string {
  const parts: string[] = [rule.filterCity ?? '不限縣市']

  if (rule.ruleType === 'Numeric') {
    parts.push(
      `${metricLabel(rule.metricName)} ${comparisonLabel(rule.comparison)} ${rule.threshold}${metricUnit(rule.metricName)}`,
    )
  } else {
    parts.push(rule.filterPlantName ? `作物含「${rule.filterPlantName}」` : '不限作物')
    if (rule.filterDateFrom) parts.push(`${rule.filterDateFrom} 之後發布`)
  }

  return parts.join('｜')
}

export type EvaluationTone = 'info' | 'success' | 'warning'

/**
 * 「立即檢查」按完要說什麼。
 *
 * 這支存在的唯一理由是三種結果必須講得不一樣：真的有新通知、條件沒命中、以及
 * 資料太舊所以根本評估不了。第三種若跟第二種說一樣的話，使用者會一直調門檻，
 * 而真正的原因是同步 Worker 沒在跑，調到天亮也不會有通知。
 *
 * now 由呼叫端傳入而不是在這裡讀時鐘，測試才不必配合真實時間。
 */
export function describeEvaluationOutcome(
  outcome: RuleEvaluationDto,
  now: Date,
): { tone: EvaluationTone; message: string } {
  if (outcome.notificationsCreated > 0) {
    return {
      tone: 'success',
      message: `已產生 ${outcome.notificationsCreated} 則新通知，點右上角的鈴鐺可以看到。`,
    }
  }

  if (outcome.latestObservedAt === null) {
    return {
      tone: 'warning',
      message: '氣象觀測資料尚未同步，數值門檻規則這一輪無法評估。',
    }
  }

  if (!outcome.hasFreshObservation) {
    const days = daysBetween(new Date(outcome.latestObservedAt), now)
    return {
      tone: 'warning',
      message: `目前最新的氣象觀測是 ${days} 天前，尚無足夠新的資料可評估。`,
    }
  }

  if (outcome.rulesEvaluated === 0) {
    return {
      tone: 'info',
      message: '沒有啟用中的規則可以評估。把規則切成啟用，或先建立一條。',
    }
  }

  return {
    tone: 'info',
    message: `已評估 ${outcome.rulesEvaluated} 條規則，沒有一條符合條件，所以沒有新通知。`,
  }
}

/** 相差幾天，無條件捨去；同一天內回 0 */
function daysBetween(earlier: Date, later: Date): number {
  const millisecondsPerDay = 86_400_000
  return Math.max(0, Math.floor((later.getTime() - earlier.getTime()) / millisecondsPerDay))
}
