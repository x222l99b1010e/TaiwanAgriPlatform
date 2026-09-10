<!--
  src/components/NotificationRuleForm.vue
  職責：通知規則的新增／編輯表單。

  這個表單真正的結構問題是「規則型態切換會換掉一半的欄位」：
  數值門檻要問觀測項目、比較方向、門檻值；植物疫情要問作物與起始日期。
  縣市兩種型態都問——它是共用欄位，比對時台／臺兩種寫法都會試。

  切換型態時不清空另一組欄位（使用者切回去還在），送出時才依型態決定哪一組進 payload；
  後端也會把另一組清成 null，所以就算前端漏清也不會存進一組沒有作用的值。

  模式判斷用 `rule` prop 是否為 null，不另外傳 mode——「有沒有現有資料」跟
  「新增還是編輯」本來就是同一件事，分兩個 prop 傳只會多一種對不起來的可能。
-->
<template>
  <section ref="panelRef" class="rule-form-panel">
    <h3 class="form-title">{{ rule === null ? '新增通知規則' : '編輯通知規則' }}</h3>

    <div class="form-grid">
      <div class="field-group span-2">
        <label class="field-label" :for="`${formId}-name`">規則名稱 *</label>
        <input
          :id="`${formId}-name`"
          v-model="form.ruleName"
          class="form-control"
          maxlength="100"
          placeholder="例如：果園高溫警戒"
        />
      </div>

      <div class="field-group span-2">
        <span class="field-label">規則型態</span>
        <div class="segmented">
          <button
            v-for="option in RULE_TYPE_OPTIONS"
            :key="option.value"
            type="button"
            class="segmented__btn"
            :class="{ 'is-active': form.ruleType === option.value }"
            @click="form.ruleType = option.value"
          >{{ option.label }}</button>
        </div>
        <p class="field-hint">{{ ruleTypeHint }}</p>
      </div>

      <!-- 縣市：兩種型態共用。比對時台／臺兩種寫法都會試，所以選哪一種寫法都比對得到 -->
      <CitySelector v-model="form.filterCity" include-all label="縣市" all-label="不限縣市" />

      <!-- ── 數值門檻專用 ── -->
      <template v-if="isNumeric">
        <div class="field-group">
          <label class="field-label" :for="`${formId}-metric`">觀測項目</label>
          <select :id="`${formId}-metric`" v-model="form.metricName" class="form-control">
            <option v-for="option in METRIC_OPTIONS" :key="option.value" :value="option.value">
              {{ option.label }}
            </option>
          </select>
        </div>

        <div class="field-group">
          <label class="field-label" :for="`${formId}-comparison`">比較方向</label>
          <select :id="`${formId}-comparison`" v-model="form.comparison" class="form-control">
            <option v-for="option in COMPARISON_OPTIONS" :key="option.value" :value="option.value">
              {{ option.label }}
            </option>
          </select>
        </div>

        <div class="field-group">
          <label class="field-label" :for="`${formId}-threshold`">門檻值 *（{{ currentUnit }}）</label>
          <input
            :id="`${formId}-threshold`"
            v-model="form.thresholdInput"
            class="form-control"
            type="number"
            step="0.1"
            :min="RULE_LIMITS.thresholdMin"
            :max="RULE_LIMITS.thresholdMax"
            placeholder="例如 32"
          />
          <p class="field-hint">收到小數點後一位</p>
        </div>
      </template>

      <!-- ── 植物疫情專用 ── -->
      <template v-else>
        <div class="field-group">
          <label class="field-label" :for="`${formId}-plant`">作物名稱</label>
          <input
            :id="`${formId}-plant`"
            v-model="form.filterPlantName"
            class="form-control"
            maxlength="50"
            placeholder="留空＝不限作物"
          />
          <p class="field-hint">用包含比對，填「檸檬」會一併命中「廣東檸檬」「無子檸檬」</p>
        </div>

        <div class="field-group">
          <label class="field-label" :for="`${formId}-date-from`">只通知這天之後發布的疫情</label>
          <input
            :id="`${formId}-date-from`"
            v-model="form.filterDateFrom"
            class="form-control"
            type="date"
          />
          <p class="field-hint">留空＝不限發布日</p>
        </div>
      </template>

      <div class="field-group">
        <label class="field-label" :for="`${formId}-expiry`">通知保留天數</label>
        <input
          :id="`${formId}-expiry`"
          v-model="form.expiryDaysInput"
          class="form-control"
          type="number"
          :min="RULE_LIMITS.minExpiryDays"
          :max="expiryLimits.max"
          :placeholder="`預設 ${expiryLimits.defaultValue} 天`"
        />
        <p class="field-hint">{{ RULE_LIMITS.minExpiryDays }}–{{ expiryLimits.max }} 天，超過就自動刪除</p>
      </div>

      <div class="field-group">
        <span class="field-label">狀態</span>
        <label class="checkbox-row">
          <input v-model="form.isActive" type="checkbox" />
          <span>啟用這條規則</span>
        </label>
        <p class="field-hint">停用中的規則不會被評估，但仍佔規則數上限</p>
      </div>
    </div>

    <!-- 編輯時的必要說明：沒有這句話，使用者改完門檻卻沒看到既有通知跟著變，會以為壞掉了 -->
    <HintBox v-if="rule !== null">
      修改後的條件將套用於之後新增的觀測資料，已產生的通知不受影響。
    </HintBox>

    <p v-if="formError" class="error-msg">{{ formError }}</p>
    <p v-if="store.saveError" class="error-msg">{{ store.saveError }}</p>

    <div class="form-actions">
      <Btn :loading="store.isSaving" @click="handleSubmit">
        {{ rule === null ? '建立規則' : '儲存變更' }}
      </Btn>
      <Btn variant="secondary" :disabled="store.isSaving" @click="emit('cancel')">取消</Btn>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, nextTick, onMounted, reactive, ref, useId, watch } from 'vue'
import Btn from '@/components/ui/Btn.vue'
import HintBox from '@/components/ui/HintBox.vue'
import CitySelector from '@/components/CitySelector.vue'
import { useNotificationRuleStore } from '@/stores/notificationRule'
import {
  COMPARISON_OPTIONS,
  METRIC_OPTIONS,
  RULE_LIMITS,
  RULE_TYPE_OPTIONS,
  SOURCE_TABLE_BY_RULE_TYPE,
  hasAllowedThresholdPrecision,
  metricUnit,
} from '@/utils/notificationRule'
import type {
  NotificationComparison,
  NotificationMetricName,
  NotificationRuleDto,
  NotificationRuleRequest,
  NotificationRuleType,
} from '@/api/notificationRule'

const props = defineProps<{
  /** null＝新增模式；帶入現有規則＝編輯模式，欄位用它的值預填 */
  rule: NotificationRuleDto | null
}>()

const emit = defineEmits<{ saved: []; cancel: [] }>()

const store = useNotificationRuleStore()
const formId = useId()
const panelRef = ref<HTMLElement | null>(null)
const formError = ref('')

interface FormState {
  ruleName: string
  ruleType: NotificationRuleType
  filterCity: string
  filterPlantName: string
  filterDateFrom: string
  metricName: NotificationMetricName
  comparison: NotificationComparison
  /** 數字輸入框留成字串：v-model.number 在清空時會給空字串，宣告成 number 是騙自己 */
  thresholdInput: string
  expiryDaysInput: string
  isActive: boolean
}

/** 事件型的起始日預設「今天往前 90 天」，使用者可以自己改 */
const defaultDateFrom = new Date(Date.now() - 90 * 24 * 60 * 60 * 1000).toISOString().split('T')[0]!

function toFormState(rule: NotificationRuleDto | null): FormState {
  if (rule === null) {
    return {
      ruleName: '',
      ruleType: 'Numeric',
      filterCity: '',
      filterPlantName: '',
      filterDateFrom: defaultDateFrom,
      metricName: 'Temperature',
      comparison: 'GreaterThan',
      thresholdInput: '',
      expiryDaysInput: '',
      isActive: true,
    }
  }
  return {
    ruleName: rule.ruleName,
    ruleType: rule.ruleType,
    filterCity: rule.filterCity ?? '',
    filterPlantName: rule.filterPlantName ?? '',
    filterDateFrom: rule.filterDateFrom ?? '',
    metricName: rule.metricName ?? 'Temperature',
    comparison: rule.comparison ?? 'GreaterThan',
    thresholdInput: rule.threshold === null ? '' : String(rule.threshold),
    expiryDaysInput: String(rule.expiryDays),
    isActive: rule.isActive,
  }
}

// 呼叫端的慣例是「開表單時掛一個新實例、關閉時整個卸載」，所以直接用當下的 prop 初始化，
// 不需要 watch 同步 prop 的變化
const form = reactive<FormState>(toFormState(props.rule))

const isNumeric = computed(() => form.ruleType === 'Numeric')
const currentUnit = computed(() => metricUnit(form.metricName))
const expiryLimits = computed(() => RULE_LIMITS.expiryDaysByRuleType[form.ruleType])

const ruleTypeHint = computed(() =>
  isNumeric.value
    ? '比對自動氣象站的即時觀測，命中就通知。只看上次檢查之後才進來的觀測，不會把歷史一次灌出來。'
    : '比對農業部發布的植物疫情警報，符合縣市與作物就通知。',
)

// 兩種型態的保留天數上限不同（事件型 180、數值型 30）。切到上限較小的那一邊時，
// 把超過的值清掉改用預設值，比讓使用者按下送出才吃一個 400 好
watch(
  () => form.ruleType,
  ruleType => {
    const max = RULE_LIMITS.expiryDaysByRuleType[ruleType].max
    const current = Number(form.expiryDaysInput)
    if (form.expiryDaysInput !== '' && Number.isFinite(current) && current > max) {
      form.expiryDaysInput = ''
    }
  },
)

onMounted(() => {
  nextTick(() => panelRef.value?.scrollIntoView({ behavior: 'smooth', block: 'start' }))
})

/**
 * 送出前的檢查只管使用者體驗，不算防護——真正的界限在後端，繞過畫面直接打 API 一樣會被擋。
 * 這裡先擋是為了讓「門檻沒填」「小數點太多位」當場講出來，而不是送一趟才知道。
 */
function validate(): string {
  if (form.ruleName.trim() === '') return '規則名稱必填'

  if (!isNumeric.value) return ''

  if (form.thresholdInput.trim() === '') return '數值門檻規則必須填門檻值'

  const threshold = Number(form.thresholdInput)
  if (!Number.isFinite(threshold)) return '門檻值必須是數字'
  if (threshold < RULE_LIMITS.thresholdMin || threshold > RULE_LIMITS.thresholdMax) {
    return `門檻值必須介於 ${RULE_LIMITS.thresholdMin} 與 ${RULE_LIMITS.thresholdMax} 之間`
  }
  if (!hasAllowedThresholdPrecision(threshold)) {
    return `門檻值只收到小數點後 ${RULE_LIMITS.thresholdDecimals} 位`
  }
  return ''
}

async function handleSubmit() {
  formError.value = validate()
  store.saveError = null
  if (formError.value !== '') return

  const numeric = isNumeric.value
  const payload: NotificationRuleRequest = {
    ruleName: form.ruleName.trim(),
    ruleType: form.ruleType,
    // 來源由型態推出來，不讓使用者選：兩者是一對一的，配錯只會建出一條永遠不觸發的規則
    sourceTable: SOURCE_TABLE_BY_RULE_TYPE[form.ruleType],
    isActive: form.isActive,
    expiryDays: form.expiryDaysInput.trim() === '' ? null : Number(form.expiryDaysInput),
    filterCity: form.filterCity === '' ? null : form.filterCity,
    filterPlantName: numeric ? null : form.filterPlantName.trim() || null,
    filterDateFrom: numeric ? null : form.filterDateFrom || null,
    metricName: numeric ? form.metricName : null,
    comparison: numeric ? form.comparison : null,
    threshold: numeric ? Number(form.thresholdInput) : null,
  }

  const success =
    props.rule === null
      ? await store.createRule(payload)
      : await store.updateRule(props.rule.id, payload)

  if (success) emit('saved')
}
</script>

<style scoped>
/* 外殼與欄位樣式走 base.css 的 .field-group／.field-label／.form-control／.segmented，
   這裡只留這個表單自己的排版。 */
.rule-form-panel {
  display: flex;
  flex-direction: column;
  gap: var(--space-5);
  background: var(--color-surface);
  border: var(--border-width) solid var(--color-border);
  border-radius: var(--radius-lg);
  padding: var(--space-6) var(--space-8);
  margin-bottom: var(--space-6);
}

.form-title {
  font-size: var(--text-base);
  font-weight: var(--weight-bold);
  color: var(--color-text);
}

.form-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: var(--space-4) var(--space-5);
  align-items: start;
}

.field-group.span-2 { grid-column: span 2; }

/* 三欄在窄螢幕會把日期與數字輸入框壓到讀不出來，降成單欄 */
@media (max-width: 720px) {
  .form-grid { grid-template-columns: 1fr; }
  .field-group.span-2 { grid-column: span 1; }
}

.field-hint {
  font-size: var(--text-2xs);
  color: var(--color-text-dim);
  line-height: var(--leading-normal);
}

.checkbox-row {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  min-height: var(--control-h);
  font-size: var(--text-sm);
  color: var(--color-text);
  cursor: pointer;
}

.form-actions { display: flex; gap: var(--space-3); }

.error-msg {
  font-size: var(--text-sm);
  font-weight: var(--weight-medium);
  color: var(--danger-700);
}
</style>
