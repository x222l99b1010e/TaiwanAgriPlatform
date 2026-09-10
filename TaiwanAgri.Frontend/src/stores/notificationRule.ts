// src/stores/notificationRule.ts
// 職責：通知規則清單的狀態、CRUD 動作，以及「立即檢查」的結果。

import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import axios from 'axios'
import {
  notificationRuleApi,
  type NotificationRuleDto,
  type NotificationRuleRequest,
  type RuleEvaluationDto,
} from '@/api/notificationRule'
import { RULE_LIMITS } from '@/utils/notificationRule'
import { useNotificationStore } from '@/stores/notification'

/**
 * 後端的 400 有兩種形狀，都帶著寫給使用者看的中文訊息，所以要抓出來顯示，
 * 不要一律換成「操作失敗，請稍後再試」——那會把「已達規則數上限 7 條」
 * 這種講得清清楚楚的原因蓋掉，使用者只能重試到放棄。
 *   ① [ApiController] 的請求驗證：{ errors: { 欄位: ["訊息"] } }
 *   ② 服務層的業務規則：回應本體直接是一個字串
 */
function extractErrorMessage(error: unknown, fallback: string): string {
  if (!axios.isAxiosError(error)) return fallback

  if (error.response?.status === 429) {
    return '操作過於頻繁，請稍等一下再試。'
  }

  const data = error.response?.data
  if (typeof data === 'string' && data.trim() !== '') return data

  if (data !== null && typeof data === 'object' && 'errors' in data) {
    const messages = Object.values((data as { errors: Record<string, string[]> }).errors).flat()
    if (messages.length > 0) return messages.join('；')
  }

  return fallback
}

export const useNotificationRuleStore = defineStore('notificationRule', () => {
  const rules = ref<NotificationRuleDto[]>([])
  const isLoading = ref(false)
  const loadError = ref<string | null>(null)

  const isSaving = ref(false)
  const saveError = ref<string | null>(null)

  const isDeleting = ref(false)
  const deleteError = ref<string | null>(null)

  const isEvaluating = ref(false)
  const evaluationResult = ref<RuleEvaluationDto | null>(null)
  const evaluationError = ref<string | null>(null)

  /** 還能不能再建一條。上限由後端強制，這裡只是先把按鈕收起來，少讓使用者白填一次表單 */
  const canCreate = computed(() => rules.value.length < RULE_LIMITS.maxRulesPerUser)

  /**
   * 讀規則清單。
   * 端點是分頁的（走全站共用的分頁參數），但這裡固定只取第一頁也不畫分頁列：
   * 一個使用者最多七條規則，永遠塞得進一頁。
   */
  async function fetchRules() {
    isLoading.value = true
    loadError.value = null
    try {
      const page = await notificationRuleApi.getRules()
      rules.value = page.items
    } catch (e) {
      loadError.value = extractErrorMessage(e, '載入通知規則失敗，請稍後再試')
      console.error(e)
    } finally {
      isLoading.value = false
    }
  }

  /** 建立規則；成功回 true。失敗時 saveError 會帶著後端的訊息（例如已達上限） */
  async function createRule(request: NotificationRuleRequest): Promise<boolean> {
    isSaving.value = true
    saveError.value = null
    try {
      await notificationRuleApi.createRule(request)
      await fetchRules()
      return true
    } catch (e) {
      saveError.value = extractErrorMessage(e, '建立規則失敗，請稍後再試')
      console.error(e)
      return false
    } finally {
      isSaving.value = false
    }
  }

  async function updateRule(id: number, request: NotificationRuleRequest): Promise<boolean> {
    isSaving.value = true
    saveError.value = null
    try {
      await notificationRuleApi.updateRule(id, request)
      await fetchRules()
      return true
    } catch (e) {
      saveError.value = extractErrorMessage(e, '儲存規則失敗，請稍後再試')
      console.error(e)
      return false
    } finally {
      isSaving.value = false
    }
  }

  async function deleteRule(id: number): Promise<boolean> {
    isDeleting.value = true
    deleteError.value = null
    try {
      await notificationRuleApi.deleteRule(id)
      await fetchRules()
      return true
    } catch (e) {
      deleteError.value = extractErrorMessage(e, '刪除規則失敗，請稍後再試')
      console.error(e)
      return false
    } finally {
      isDeleting.value = false
    }
  }

  /**
   * 立即評估自己的規則。
   * 成功後要重讀清單：評估會推進數值型規則的水位，也可能改變每條規則的通知則數，
   * 而刪除確認的「會一併刪掉 N 則」就是拿那個數字顯示的。
   *
   * 也要重讀未讀數。少了這一步，畫面會說「已產生 N 則新通知，點右上角的鈴鐺可以看到」，
   * 而鈴鐺當下是空的——紅點要等最多 60 秒的輪詢才補上，使用者當場看到的是自相矛盾的兩件事。
   * ⚠ 不是只有「產生了新通知」才要重讀：評估的第一件事是刪掉過期通知，
   * 所以回報 0 則新增的那一輪，未讀數同樣可能變少。
   */
  async function evaluateNow(): Promise<boolean> {
    isEvaluating.value = true
    evaluationError.value = null
    evaluationResult.value = null
    try {
      evaluationResult.value = await notificationRuleApi.evaluateNow()
      await Promise.all([fetchRules(), useNotificationStore().fetchUnreadCount()])
      return true
    } catch (e) {
      evaluationError.value = extractErrorMessage(e, '立即檢查失敗，請稍後再試')
      console.error(e)
      return false
    } finally {
      isEvaluating.value = false
    }
  }

  return {
    rules,
    isLoading,
    loadError,
    isSaving,
    saveError,
    isDeleting,
    deleteError,
    isEvaluating,
    evaluationResult,
    evaluationError,
    canCreate,
    fetchRules,
    createRule,
    updateRule,
    deleteRule,
    evaluateNow,
  }
})
