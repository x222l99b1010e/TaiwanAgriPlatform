// src/stores/notification.ts
// 職責：管理通知未讀數與通知列表，供 NotificationBell 元件使用

import { defineStore } from 'pinia'
import { ref } from 'vue'
import { notificationApi, type UserNotificationDto } from '@/api/weather'

export const useNotificationStore = defineStore('notification', () => {
  const unreadCount = ref(0)
  const notifications = ref<UserNotificationDto[]>([])
  const isLoading = ref(false)
  const page = ref(1)
  const hasMore = ref(true)
  /**
   * 最近一次操作的錯誤訊息。
   * 這裡刻意不再靜默吞掉失敗：紅點停在舊數字、列表停在半截，跟「真的沒有新通知」
   * 長得一模一樣，使用者無從分辨，也不會想到要重試。
   * 後端自己的原則就寫著「安靜回空是最難查的錯誤」，前端沒有理由用另一套。
   */
  const errorMessage = ref<string | null>(null)

  /** 取得未讀數（給紅點用，輕量查詢） */
  async function fetchUnreadCount() {
    try {
      const res = await notificationApi.getUnreadCount()
      unreadCount.value = res.count
      errorMessage.value = null
    } catch {
      errorMessage.value = '通知未讀數載入失敗'
    }
  }

  /** 取得通知列表（開啟 dropdown 時呼叫） */
  async function fetchNotifications(reset = false) {
    if (reset) {
      page.value = 1
      hasMore.value = true
      notifications.value = []
    }
    if (!hasMore.value || isLoading.value) return

    isLoading.value = true
    try {
      const res = await notificationApi.getList(page.value)
      notifications.value.push(...res.items)
      // hasMore 由後端回答，不從「這頁是不是滿的」反推——
      // 反推在總筆數剛好是每頁筆數倍數時會多給一次載入更多，點了拿到空陣列
      hasMore.value = res.hasMore
      page.value++
      errorMessage.value = null
    } catch {
      errorMessage.value = '通知載入失敗，請稍後再試'
    } finally {
      isLoading.value = false
    }
  }

  /** 標記單筆已讀，同步更新本地狀態 */
  async function markAsRead(id: number) {
    try {
      await notificationApi.markAsRead(id)
      const target = notifications.value.find(n => n.id === id)
      if (target && !target.isRead) {
        target.isRead = true
        unreadCount.value = Math.max(0, unreadCount.value - 1)
      }
      errorMessage.value = null
    } catch {
      errorMessage.value = '標記已讀失敗，請稍後再試'
    }
  }

  /** 標記全部已讀：一次請求，不是每筆各送一次 PATCH */
  async function markAllAsRead() {
    try {
      await notificationApi.markAllAsRead()
      notifications.value.forEach(n => { n.isRead = true })
      unreadCount.value = 0
      errorMessage.value = null
    } catch {
      errorMessage.value = '全部標記已讀失敗，請稍後再試'
    }
  }

  return {
    unreadCount,
    notifications,
    isLoading,
    hasMore,
    errorMessage,
    fetchUnreadCount,
    fetchNotifications,
    markAsRead,
    markAllAsRead,
  }
})
