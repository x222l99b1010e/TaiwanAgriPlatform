<!--
  src/views/weather/NotificationRulesView.vue
  職責：通知規則管理頁 /profile/notification-rules。

  這頁補的是整條通知鏈缺掉的第一節：鈴鐺、紅點、分頁、已讀都做好了，但規則沒有任何
  建立管道，所以以前不論等多久都不會有任何一則通知。

  檔案放在 views/weather/ 而路由掛在 /profile 底下，是比照「我的協尋貼文」的既有做法：
  檔案位置跟著它屬於哪個功能領域，路由跟著它是不是個人資料。

  刪除確認做成卡片內的一段，不是彈出對話框：只有這一頁會用到，而彈窗要自己處理
  焦點鎖定與 Esc 關閉，成本遠高於它帶來的差別。
-->
<template>
  <div class="page notification-rules-view">
    <RouterLink to="/profile" class="back-link">
      <span class="mdi mdi-arrow-left" /> 回農場設定
    </RouterLink>

    <QueryLayout
      title="通知規則"
      title-en="NOTIFICATION RULES"
      subtitle="設定什麼情況要通知你。規則每天自動評估一次，命中就把通知送到右上角的鈴鐺。"
      filter-label="立即檢查"
      filter-label-en="RUN NOW"
    >
      <template #filters>
        <p class="run-now-desc">
          排程每天跑一次。剛建好規則想馬上知道結果，按右邊的「立即檢查」——
          它只會評估你自己的規則。
        </p>
      </template>

      <template #actions>
        <Btn
          variant="secondary"
          icon="mdi-refresh"
          :loading="store.isEvaluating"
          @click="store.evaluateNow()"
        >立即檢查</Btn>
        <Btn
          icon="mdi-plus"
          :disabled="!store.canCreate || isFormOpen"
          @click="openCreateForm"
        >新增規則</Btn>
      </template>

      <template #hint>
        <HintBox v-if="store.evaluationError" tone="warning">{{ store.evaluationError }}</HintBox>
        <HintBox v-else-if="evaluationSummary" :tone="evaluationSummary.tone">
          {{ evaluationSummary.message }}
        </HintBox>
        <HintBox v-else-if="!store.canCreate" tone="warning">
          已達規則數上限 {{ RULE_LIMITS.maxRulesPerUser }} 條。要建新的請先刪掉一條——
          停用中的規則也算在內。
        </HintBox>
      </template>

      <template #results>
        <NotificationRuleForm
          v-if="isFormOpen"
          :key="editingRule?.id ?? 'create'"
          :rule="editingRule"
          @saved="handleSaved"
          @cancel="closeForm"
        />

        <StateBlock v-if="store.isLoading" state="loading" message="規則載入中..." />
        <StateBlock
          v-else-if="store.loadError"
          state="error"
          :message="store.loadError"
          retryable
          @retry="store.fetchRules()"
        />
        <StateBlock
          v-else-if="store.rules.length === 0"
          state="empty"
          icon="mdi-bell-plus-outline"
          message="還沒有任何通知規則"
          hint="按上方的「新增規則」建立第一條，例如「臺中市氣溫超過 32 度」"
        />

        <div v-else class="rule-list">
          <article v-for="rule in store.rules" :key="rule.id" class="rule-card">
            <div class="rule-card__head">
              <h3 class="rule-card__name">{{ rule.ruleName }}</h3>
              <span class="badge rule-badge" :class="`rule-badge--${rule.ruleType.toLowerCase()}`">
                {{ ruleTypeLabel(rule.ruleType) }}
              </span>
              <span v-if="!rule.isActive" class="badge rule-badge rule-badge--paused">已停用</span>
            </div>

            <p class="rule-card__condition">{{ ruleConditionSummary(rule) }}</p>

            <p class="rule-card__meta">
              通知保留 {{ rule.expiryDays }} 天・已產生
              <span class="rule-card__count">{{ rule.notificationCount }}</span> 則通知
            </p>

            <!-- 刪除確認：要講清楚會連帶刪掉幾則通知，使用者知道後果才有辦法決定要不要刪 -->
            <div v-if="pendingDeleteId === rule.id" class="rule-card__confirm">
              <p class="rule-card__confirm-text">
                確定要刪除「{{ rule.ruleName }}」嗎？
                <template v-if="rule.notificationCount > 0">
                  它已產生的 {{ rule.notificationCount }} 則通知會一併刪除，而且無法復原。
                </template>
                <template v-else>這條規則還沒有產生任何通知。</template>
              </p>
              <p v-if="store.deleteError" class="error-msg">{{ store.deleteError }}</p>
              <div class="rule-card__actions">
                <Btn
                  variant="danger"
                  size="sm"
                  icon="mdi-delete-outline"
                  :loading="store.isDeleting"
                  @click="confirmDelete(rule.id)"
                >確定刪除</Btn>
                <Btn variant="secondary" size="sm" :disabled="store.isDeleting" @click="pendingDeleteId = null">
                  取消
                </Btn>
              </div>
            </div>

            <div v-else class="rule-card__actions">
              <Btn variant="secondary" size="sm" icon="mdi-pencil-outline" @click="openEditForm(rule)">
                編輯
              </Btn>
              <Btn variant="danger" size="sm" icon="mdi-delete-outline" @click="askDelete(rule.id)">
                刪除
              </Btn>
            </div>
          </article>
        </div>
      </template>
    </QueryLayout>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import QueryLayout from '@/components/layouts/QueryLayout.vue'
import Btn from '@/components/ui/Btn.vue'
import HintBox from '@/components/ui/HintBox.vue'
import StateBlock from '@/components/ui/StateBlock.vue'
import NotificationRuleForm from '@/components/NotificationRuleForm.vue'
import { useNotificationRuleStore } from '@/stores/notificationRule'
import { useNotificationStore } from '@/stores/notification'
import {
  RULE_LIMITS,
  describeEvaluationOutcome,
  ruleConditionSummary,
  ruleTypeLabel,
} from '@/utils/notificationRule'
import type { NotificationRuleDto } from '@/api/notificationRule'

const store = useNotificationRuleStore()
const notificationStore = useNotificationStore()

const isFormOpen = ref(false)
const editingRule = ref<NotificationRuleDto | null>(null)
const pendingDeleteId = ref<number | null>(null)

const evaluationSummary = computed(() =>
  store.evaluationResult === null
    ? null
    : describeEvaluationOutcome(store.evaluationResult, new Date()),
)

function openCreateForm() {
  editingRule.value = null
  isFormOpen.value = true
  store.saveError = null
}

function openEditForm(rule: NotificationRuleDto) {
  editingRule.value = rule
  isFormOpen.value = true
  pendingDeleteId.value = null
  store.saveError = null
}

function closeForm() {
  isFormOpen.value = false
  editingRule.value = null
}

function handleSaved() {
  closeForm()
}

function askDelete(id: number) {
  pendingDeleteId.value = id
  store.deleteError = null
}

async function confirmDelete(id: number) {
  const deleted = await store.deleteRule(id)
  if (!deleted) return
  pendingDeleteId.value = null
  // 通知被連帶刪掉了，紅點要跟著降。不重抓的話它會停在舊數字，
  // 直到下一次六十秒輪詢——而使用者會以為刪除只刪了一半
  notificationStore.fetchUnreadCount()
}

onMounted(() => {
  store.fetchRules()
})
</script>

<style scoped>
/* 返回連結、欄位外殼、狀態標籤都已收在 base.css，這裡只留這一頁真正不同的部分。 */
.run-now-desc {
  max-width: var(--container-md);
  font-size: var(--text-sm);
  line-height: var(--leading-normal);
  color: var(--color-text-dim);
}

.rule-list {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(340px, 1fr));
  gap: var(--space-5);
}

.rule-card {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
  background: var(--color-surface);
  border: var(--border-width) solid var(--color-border);
  border-radius: var(--radius-lg);
  padding: var(--space-5);
  transition: border-color var(--duration-fast) var(--ease-work);
}
.rule-card:hover { border-color: var(--color-border-strong); }

.rule-card__head {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  flex-wrap: wrap;
}

.rule-card__name {
  font-size: var(--text-lg);
  font-weight: var(--weight-bold);
  color: var(--color-text);
}

/* 標籤外殼走 base.css 的 .badge，這裡只給語意色。
   「數值門檻／植物疫情／已停用」是這個功能的業務語意，不是設計系統的一部分，
   收進全域層會讓 base.css 開始認識這個專案有哪些功能。 */
.rule-badge--numeric { background: var(--color-action-soft-2); color: var(--color-action); }
.rule-badge--event { background: var(--warning-50); color: var(--warning-700); }
.rule-badge--paused { background: var(--color-bg-sunken); color: var(--color-text-dim); }

.rule-card__condition {
  font-size: var(--text-sm);
  line-height: var(--leading-normal);
  color: var(--color-text);
}

.rule-card__meta {
  font-size: var(--text-xs);
  color: var(--color-text-dim);
}

.rule-card__count {
  font-family: var(--font-num);
  font-weight: var(--weight-bold);
  color: var(--color-text);
}

.rule-card__actions {
  display: flex;
  gap: var(--space-2);
  margin-top: auto;
  padding-top: var(--space-3);
}

/* 確認區用危險色的淺底：它是一個「按下去就回不來」的動作，
   跟卡片其他部分看起來一樣的話，使用者會以為只是又一排按鈕 */
.rule-card__confirm {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
  margin-top: auto;
  padding: var(--space-4);
  background: var(--danger-50);
  border: var(--border-width) solid var(--danger-100);
  border-radius: var(--radius-md);
}

.rule-card__confirm-text {
  font-size: var(--text-sm);
  line-height: var(--leading-normal);
  color: var(--danger-700);
}

.rule-card__confirm .rule-card__actions { padding-top: 0; }

.error-msg {
  font-size: var(--text-sm);
  font-weight: var(--weight-medium);
  color: var(--danger-700);
}
</style>
