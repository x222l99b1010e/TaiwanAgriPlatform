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

  /** 取得未讀數（給紅點用，輕量查詢） */
  async function fetchUnreadCount() {
    try {
      const res = await notificationApi.getUnreadCount()
      unreadCount.value = res.count
    } catch {
      // 靜默失敗
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
      notifications.value.push(...res)
      hasMore.value = res.length === 20
      page.value++
    } catch {
      // 靜默失敗
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
    } catch {
      // 靜默失敗
    }
  }

  /** 標記全部已讀 */
  async function markAllAsRead() {
    const unread = notifications.value.filter(n => !n.isRead)
    await Promise.all(unread.map(n => markAsRead(n.id)))
  }

  return {
    unreadCount,
    notifications,
    isLoading,
    hasMore,
    fetchUnreadCount,
    fetchNotifications,
    markAsRead,
    markAllAsRead,
  }
})