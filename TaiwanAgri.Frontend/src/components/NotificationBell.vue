<!-- src/components/NotificationBell.vue -->
<!-- 職責：鈴鐺圖示 + 未讀紅點 + 通知 dropdown -->

<template>
  <div class="bell-wrapper" ref="wrapperRef">
    <!-- 鈴鐺按鈕 -->
    <button class="bell-btn" @click="toggleDropdown">
      <span class="mdi mdi-bell bell-icon" />
      <span v-if="store.unreadCount > 0" class="bell-badge">
        {{ store.unreadCount > 99 ? '99+' : store.unreadCount }}
      </span>
    </button>

    <!-- Dropdown -->
    <div class="dropdown" v-if="isOpen">
      <div class="dropdown-header">
        <span class="dropdown-title">通知</span>
        <button
          class="btn-mark-all"
          :disabled="store.unreadCount === 0"
          @click="store.markAllAsRead()"
        >全部已讀</button>
      </div>

      <div class="dropdown-body" ref="bodyRef" @scroll="handleScroll">
        <div v-if="store.isLoading && store.notifications.length === 0" class="hint">
          載入中...
        </div>
        <div v-else-if="store.notifications.length === 0" class="hint">
          目前沒有通知
        </div>
        <div
          v-else
          v-for="n in store.notifications"
          :key="n.id"
          class="notification-item"
          :class="{ unread: !n.isRead }"
          @click="store.markAsRead(n.id)"
        >
          <div class="item-top">
            <span class="rule-name">{{ n.ruleName }}</span>
            <span class="item-time">{{ formatTime(n.triggeredAt) }}</span>
          </div>
          <div class="item-message">{{ n.message }}</div>
        </div>

        <div v-if="store.isLoading && store.notifications.length > 0" class="hint">
          載入中...
        </div>
        <div v-if="!store.hasMore && store.notifications.length > 0" class="hint end-hint">
          已顯示全部通知
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import { useNotificationStore } from '@/stores/notification'

const store = useNotificationStore()
const isOpen = ref(false)
const wrapperRef = ref<HTMLElement | null>(null)
const bodyRef = ref<HTMLElement | null>(null)

// ── 開關 dropdown ──────────────────────────────────────
async function toggleDropdown() {
  isOpen.value = !isOpen.value
  if (isOpen.value) {
    await store.fetchNotifications(true)
  }
}

// ── 無限捲動：捲到底自動載入下一頁 ───────────────────────
function handleScroll() {
  const el = bodyRef.value
  if (!el) return
  const nearBottom = el.scrollTop + el.clientHeight >= el.scrollHeight - 40
  if (nearBottom && store.hasMore && !store.isLoading) {
    store.fetchNotifications()
  }
}

// ── 點外部關閉 dropdown ────────────────────────────────
function handleOutsideClick(e: MouseEvent) {
  if (wrapperRef.value && !wrapperRef.value.contains(e.target as Node)) {
    isOpen.value = false
  }
}

// ── 時間格式化 ─────────────────────────────────────────
function formatTime(iso: string) {
  const d = new Date(iso)
  const now = new Date()
  const diff = now.getTime() - d.getTime()
  const minutes = Math.floor(diff / 60000)
  if (minutes < 1)   return '剛剛'
  if (minutes < 60)  return `${minutes} 分鐘前`
  const hours = Math.floor(minutes / 60)
  if (hours < 24)    return `${hours} 小時前`
  const days = Math.floor(hours / 24)
  if (days < 7)      return `${days} 天前`
  return d.toLocaleDateString('zh-TW', { month: 'numeric', day: 'numeric' })
}

onMounted(() => {
  store.fetchUnreadCount()
  // 每 60 秒輪詢一次未讀數
  const timer = setInterval(() => store.fetchUnreadCount(), 60000)
  document.addEventListener('click', handleOutsideClick)
  onUnmounted(() => {
    clearInterval(timer)
    document.removeEventListener('click', handleOutsideClick)
  })
})
</script>

<style scoped>
.bell-wrapper { position: relative; }

/* ── 鈴鐺按鈕（在深色 TopNav 上，保持白色）── */
.bell-btn {
  position: relative;
  display: flex;
  align-items: center;
  justify-content: center;
  width: 36px;
  height: 36px;
  border-radius: var(--radius-md);
  border: none;
  background: transparent;
  color: var(--white-a80);
  cursor: pointer;
  transition: background var(--duration-fast), color var(--duration-fast);
}
.bell-btn:hover { background: var(--white-a12); color: var(--neutral-0); }

.bell-icon { font-size: var(--text-lg); }

/* 未讀紅點。刻意不用共用的 .badge：它是絕對定位疊在鈴鐺上的計數點，
   跟頁面裡那種行內的狀態標籤不是同一種東西 */
.bell-badge {
  position: absolute;
  top: 4px; right: 4px;
  min-width: 16px; height: 16px;
  padding: 0 var(--space-1); border-radius: var(--radius-full);
  background: var(--danger-500); color: var(--neutral-0);
  font-size: var(--text-2xs); font-weight: var(--weight-bold);
  line-height: 16px; text-align: center;
  pointer-events: none;
}

/* ── Dropdown（白底）── */
.dropdown {
  position: absolute;
  top: calc(100% + 8px);
  right: 0;
  width: 340px;
  background: var(--neutral-0);
  border: 1px solid var(--neutral-200);
  border-radius: var(--radius-lg);
  box-shadow: var(--shadow-lg);
  overflow: hidden;
  z-index: var(--z-overlay);
}

.dropdown-header {
  display: flex; align-items: center; justify-content: space-between;
  padding: var(--space-4) var(--space-5) var(--space-3);
  border-bottom: 1px solid var(--neutral-200);
}

.dropdown-title {
  font-size: var(--text-base);              /* 從 14px → 16px */
  font-weight: var(--weight-bold);
  color: var(--neutral-900);
}

.btn-mark-all {
  font-size: var(--text-sm);              /* 從 12px → 13px */
  color: var(--neutral-500);   /* 從 teal → 深灰 */
  background: none; border: none; cursor: pointer; padding: 0;
  font-weight: var(--weight-medium);
  transition: color var(--duration-fast);
}
.btn-mark-all:hover:not(:disabled) { color: var(--green-600); }
.btn-mark-all:disabled { color: var(--neutral-400); cursor: not-allowed; }

/* 捲動區 */
.dropdown-body { max-height: 400px; overflow-y: auto; }
.dropdown-body::-webkit-scrollbar { width: 4px; }
.dropdown-body::-webkit-scrollbar-track { background: transparent; }
.dropdown-body::-webkit-scrollbar-thumb { background: var(--neutral-300); border-radius: var(--radius-sm); }

/* 通知項目 */
.notification-item {
  padding: var(--space-4) var(--space-5);
  border-bottom: 1px solid var(--neutral-200);
  cursor: pointer; transition: background var(--duration-fast);
}
.notification-item:last-child { border-bottom: none; }
.notification-item:hover { background: var(--neutral-50); }

/* 未讀 */
.notification-item.unread {
  background: var(--teal-50);
  border-left: 3px solid var(--teal-500);
  padding-left: var(--space-4);
}
.notification-item.unread:hover { background: var(--teal-100); }

.item-top {
  display: flex; justify-content: space-between;
  align-items: baseline; margin-bottom: var(--space-1);
}

.rule-name { font-size: var(--text-xs); font-weight: var(--weight-medium); color: var(--teal-500); }
.item-time { font-size: var(--text-2xs); color: var(--neutral-400); white-space: nowrap; }
.item-message { font-size: var(--text-sm); color: var(--neutral-500); line-height: var(--leading-normal); }

/* 提示文字 */
.hint {
  text-align: center;
  padding: var(--space-6) 0;
  font-size: var(--text-base);              /* 從 13px → 14px */
  color: var(--neutral-500);   /* 從 text-muted(0.40) → 0.50 */
  font-weight: var(--weight-medium);
}
.end-hint { padding: var(--space-3) 0; font-size: var(--text-xs); }
</style>