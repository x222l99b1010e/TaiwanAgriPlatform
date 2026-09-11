// src/utils/notificationRule.ts
// 職責：通知規則畫面用得到的顯示標籤、輸入界限，以及「立即檢查」結果要說什麼。

import type {
  NotificationComparison,
  NotificationMetricName,
  NotificationRuleRequest,
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

/**
 * 數字輸入框的狀態型別。
 *
 * `<input type="number">` 搭 `v-model` 時 Vue 會**自動**套用 `.number`（不必自己寫修飾詞），
 * 所以有值時綁回來的是 `number`、清空時是空字串——`number | ''` 這兩個成員就是全部的執行期狀態。
 * ⚠ 不要為了好寫而宣告成 `string` 或 `number`：任一種都會讓 `vue-tsc` 看著一個假的型別放行，
 * 而錯誤要等使用者按下送出才在瀏覽器裡炸出來（實例：宣告成 `string` 時 `.trim()` 編譯得過、執行必炸）。
 */
export type NumberInput = number | ''

/** 規則表單的完整狀態。放在這裡而不是元件內，驗證與組請求才能當純函式測。 */
export interface RuleFormState {
  ruleName: string
  ruleType: NotificationRuleType
  filterCity: string
  filterPlantName: string
  filterDateFrom: string
  metricName: NotificationMetricName
  comparison: NotificationComparison
  thresholdInput: NumberInput
  expiryDaysInput: NumberInput
  isActive: boolean
}

/**
 * 送出前的檢查只管使用者體驗，不算防護——真正的界限在後端，繞過畫面直接打 API 一樣會被擋。
 * 這裡先擋是為了讓「門檻沒填」「小數點太多位」當場講出來，而不是送一趟才知道。
 * 回空字串代表通過。
 */
export function validateRuleForm(form: RuleFormState): string {
  if (form.ruleName.trim() === '') return '規則名稱必填'

  if (form.ruleType !== 'Numeric') return ''

  if (form.thresholdInput === '') return '數值門檻規則必須填門檻值'

  const threshold = form.thresholdInput
  // 輸入框擋得住字母，但擋不住 `1e999`——那個 parseFloat 出來是 Infinity，不是 NaN
  if (!Number.isFinite(threshold)) return '門檻值必須是數字'
  if (threshold < RULE_LIMITS.thresholdMin || threshold > RULE_LIMITS.thresholdMax) {
    return `門檻值必須介於 ${RULE_LIMITS.thresholdMin} 與 ${RULE_LIMITS.thresholdMax} 之間`
  }
  if (!hasAllowedThresholdPrecision(threshold)) {
    return `門檻值只收到小數點後 ${RULE_LIMITS.thresholdDecimals} 位`
  }
  return ''
}

/**
 * 表單狀態組成送出的請求。
 * 另一個型態專用的欄位一律送 null——後端本來就會清空，前端先送對的形狀可以少一次「這欄怎麼有值」的疑惑。
 * 保留天數留空時送 null，代表沿用後端依型態決定的預設值，而不是 0。
 */
export function buildRuleRequest(form: RuleFormState): NotificationRuleRequest {
  const numeric = form.ruleType === 'Numeric'
  return {
    ruleName: form.ruleName.trim(),
    ruleType: form.ruleType,
    sourceTable: SOURCE_TABLE_BY_RULE_TYPE[form.ruleType],
    isActive: form.isActive,
    expiryDays: form.expiryDaysInput === '' ? null : form.expiryDaysInput,
    filterCity: form.filterCity === '' ? null : form.filterCity,
    filterPlantName: numeric ? null : form.filterPlantName.trim() || null,
    filterDateFrom: numeric ? null : form.filterDateFrom || null,
    metricName: numeric ? form.metricName : null,
    comparison: numeric ? form.comparison : null,
    threshold: numeric && form.thresholdInput !== '' ? form.thresholdInput : null,
  }
}

export type EvaluationTone = 'info' | 'success' | 'warning'

/**
 * 「立即檢查」按完要說什麼。
 *
 * 這支存在的唯一理由是四種結果必須講得不一樣：真的有新通知、條件沒命中、資料太舊所以
 * 根本評估不了、以及上次檢查之後根本沒有新的觀測落地。後兩種若跟「條件沒命中」說一樣的話，
 * 使用者會一直調門檻，而真正的原因分別是同步 Worker 沒在跑、與掃描範圍是空的
 * ——兩種都調到天亮也不會有通知。
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

  // 數值型規則全都沒有新觀測可比：這一輪的掃描範圍是空的，不是條件太嚴。
  // 這種情況最常發生在「剛改完條件立刻檢查」與「連按兩次檢查」，而它跟「條件沒命中」
  // 講成同一句話的話，使用者會去調門檻——調到天亮也不會有通知
  if (outcome.numericRulesEvaluated > 0 && outcome.numericRulesWithNewObservations === 0) {
    const eventRules = outcome.rulesEvaluated - outcome.numericRulesEvaluated
    const tail =
      eventRules > 0 ? `另外 ${eventRules} 條植物疫情規則已比對過，沒有符合條件的。` : ''
    return {
      tone: 'info',
      message:
        '上次檢查之後還沒有新的氣象觀測落地（氣象站每小時更新一批），' +
        `數值門檻規則這一輪沒有資料可以比對。改條件不會回頭重算已經看過的觀測。${tail}`,
    }
  }

  return {
    tone: 'info',
    message: `已評估 ${outcome.rulesEvaluated} 條規則，沒有一條符合條件，所以沒有新通知。`,
  }
}

/**
 * 表單上方那句「改了之後會發生什麼事」。
 *
 * 這句話是實測長出來的：兩種型態對「改條件」的反應完全相反，而畫面原本只在編輯模式的
 * 表單底部寫一句共用的說明，實際使用時擋不住誤解——改完數值門檻的條件按「立即檢查」
 * 得到 0 則，看起來就像功能壞了。
 *
 * 差別的根源是水位：數值型只比對「上次檢查之後才落地」的觀測，改條件不重置水位；
 * 事件型沒有水位，每次都重新比對全部疫情警報，只靠去重擋掉已經通知過的那幾則。
 */
export function ruleTimingHint(ruleType: NotificationRuleType, isEditing: boolean): string {
  if (ruleType === 'Numeric') {
    return isEditing
      ? '改條件只對之後才落地的觀測生效——已經檢查過的觀測不會用新條件重算，' +
          '所以改完按「立即檢查」多半是 0 則，要等下一批觀測進來（每小時一批）。' +
          '想立刻用新條件掃一次最近的觀測，請改為新增一條規則。'
      : '建立後從最近一批氣象觀測開始比對，可以馬上按「立即檢查」看結果；' +
          '之後只看新落地的觀測，不會回頭掃歷史。'
  }

  return isEditing
    ? '改條件後按「立即檢查」就會補上符合新條件的疫情警報——' +
        '事件型每次都重新比對全部警報，只有已經通知過的那幾則不會再來一次。'
    : '建立後會比對全部疫情警報，符合條件的都會通知；同一則警報只通知一次。'
}

/** 相差幾天，無條件捨去；同一天內回 0 */
function daysBetween(earlier: Date, later: Date): number {
  const millisecondsPerDay = 86_400_000
  return Math.max(0, Math.floor((later.getTime() - earlier.getTime()) / millisecondsPerDay))
}
