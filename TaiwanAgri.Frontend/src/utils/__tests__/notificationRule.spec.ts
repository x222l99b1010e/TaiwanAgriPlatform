import { describe, it, expect } from 'vitest'
import {
  describeEvaluationOutcome,
  hasAllowedThresholdPrecision,
  ruleConditionSummary,
} from '../notificationRule'
import type { RuleEvaluationDto } from '@/api/notificationRule'

const NOW = new Date('2026-09-10T12:00:00Z')

function outcome(overrides: Partial<RuleEvaluationDto> = {}): RuleEvaluationDto {
  return {
    rulesEvaluated: 2,
    notificationsCreated: 0,
    latestObservedAt: '2026-09-10T11:30:00Z',
    hasFreshObservation: true,
    ...overrides,
  }
}

describe('describeEvaluationOutcome', () => {
  // 這一組守的是「沒有新通知」的三種成因不可以講成同一句話：
  // 使用者分不出「條件沒命中」與「資料太舊」時，會一直調門檻，
  // 而真正的原因是同步 Worker 沒在跑，調到天亮也不會有通知
  it('有新通知時說產生了幾則並指向鈴鐺', () => {
    const result = describeEvaluationOutcome(outcome({ notificationsCreated: 3 }), NOW)
    expect(result.tone).toBe('success')
    expect(result.message).toContain('3 則')
    expect(result.message).toContain('鈴鐺')
  })

  it('條件沒命中時要說明是評估過了但沒有一條符合', () => {
    const result = describeEvaluationOutcome(outcome(), NOW)
    expect(result.tone).toBe('info')
    expect(result.message).toContain('沒有一條符合條件')
  })

  it('資料太舊時要說出最新觀測是幾天前', () => {
    const result = describeEvaluationOutcome(
      outcome({ hasFreshObservation: false, latestObservedAt: '2026-08-22T12:00:00Z' }),
      NOW,
    )
    expect(result.tone).toBe('warning')
    expect(result.message).toContain('19 天前')
  })

  it('完全沒有觀測資料時不說「幾天前」，那個數字算不出來', () => {
    const result = describeEvaluationOutcome(
      outcome({ hasFreshObservation: false, latestObservedAt: null }),
      NOW,
    )
    expect(result.tone).toBe('warning')
    expect(result.message).toContain('尚未同步')
    expect(result.message).not.toContain('天前')
  })

  it('三種空結果的訊息互不相同', () => {
    // 少了這條，把其中兩種寫成同一句話不會有任何測試變紅——
    // 而那正是這支函式唯一要解決的問題
    const messages = [
      describeEvaluationOutcome(outcome(), NOW).message,
      describeEvaluationOutcome(outcome({ hasFreshObservation: false }), NOW).message,
      describeEvaluationOutcome(outcome({ rulesEvaluated: 0 }), NOW).message,
    ]
    expect(new Set(messages).size).toBe(3)
  })
})

describe('hasAllowedThresholdPrecision', () => {
  it.each([32, 32.5, -12.3, 0, 999.9])('%s 是合法的一位小數', value => {
    expect(hasAllowedThresholdPrecision(value)).toBe(true)
  })

  it.each([32.55, 0.01, -12.345])('%s 超過一位小數', value => {
    expect(hasAllowedThresholdPrecision(value)).toBe(false)
  })

  it('浮點數乘十的尾數不會被誤判成超過一位小數', () => {
    // 32.5 * 10 在 IEEE 754 下不一定是剛好 325，直接比整數會誤判成不合法
    expect(hasAllowedThresholdPrecision(32.5)).toBe(true)
    expect(hasAllowedThresholdPrecision(8.1)).toBe(true)
  })
})

describe('ruleConditionSummary', () => {
  it('數值型摘要要帶得出縣市、項目、方向、門檻與單位', () => {
    const summary = ruleConditionSummary({
      ruleType: 'Numeric',
      filterCity: '臺中市',
      filterPlantName: null,
      filterDateFrom: null,
      metricName: 'Temperature',
      comparison: 'GreaterThan',
      threshold: 32,
    })
    expect(summary).toBe('臺中市｜氣溫 超過 32°C')
  })

  it('事件型摘要要帶得出作物與起始日', () => {
    const summary = ruleConditionSummary({
      ruleType: 'Event',
      filterCity: '臺南市',
      filterPlantName: '檸檬',
      filterDateFrom: '2026-06-12',
      metricName: null,
      comparison: null,
      threshold: null,
    })
    expect(summary).toBe('臺南市｜作物含「檸檬」｜2026-06-12 之後發布')
  })

  it('沒填的條件要顯示成「不限」而不是消失', () => {
    // 空字串或整段不見的話，使用者看不出這條規則是「不限縣市」還是「顯示壞了」
    const summary = ruleConditionSummary({
      ruleType: 'Event',
      filterCity: null,
      filterPlantName: null,
      filterDateFrom: null,
      metricName: null,
      comparison: null,
      threshold: null,
    })
    expect(summary).toBe('不限縣市｜不限作物')
  })
})
