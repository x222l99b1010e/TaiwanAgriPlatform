<!-- src/components/NotificationBell.vue -->
<!-- 職責：鈴鐺圖示 + 未讀紅點 + 通知 dropdown -->

<template>
  <div class="bell-wrapper" ref="wrapperRef">
    <!-- 鈴鐺按鈕 -->
    <button class="bell-btn" @click="toggleDropdown">
      <span class="mdi mdi-bell bell-icon" />
      <span v-if="store.unreadCount > 0" class="badge">
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
  border-radius: 8px;
  border: none;
  background: transparent;
  color: var(--white-a80);
  cursor: pointer;
  transition: background 0.18s, color 0.18s;
}
.bell-btn:hover { background: var(--white-a12); color: var(--neutral-0); }

.bell-icon { font-size: 20px; }

/* 紅點 badge */
.badge {
  position: absolute;
  top: 4px; right: 4px;
  min-width: 16px; height: 16px;
  padding: 0 4px; border-radius: 999px;
  background: var(--danger-500); color: var(--neutral-0);
  font-size: 10px; font-weight: 700;
  line-height: 16px; text-align: center;
  pointer-events: none;
}

/* ── Dropdown（白底）── */
.dropdown {
  position: absolute;
  top: calc(100% + 8px);
  right: 0;
  width: 340px;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 14px;
  box-shadow: 0 8px 32px rgba(0,0,0,0.15);
  overflow: hidden;
  z-index: 300;
}

.dropdown-header {
  display: flex; align-items: center; justify-content: space-between;
  padding: 14px 18px 12px;
  border-bottom: 1px solid var(--border);
}

.dropdown-title {
  font-size: 16px;              /* 從 14px → 16px */
  font-weight: 700;
  color: var(--text-primary);
}

.btn-mark-all {
  font-size: 13px;              /* 從 12px → 13px */
  color: var(--neutral-500);   /* 從 teal → 深灰 */
  background: none; border: none; cursor: pointer; padding: 0;
  font-weight: 600;
  transition: color 0.15s;
}
.btn-mark-all:hover:not(:disabled) { color: var(--green); }
.btn-mark-all:disabled { color: var(--neutral-400); cursor: not-allowed; }

/* 捲動區 */
.dropdown-body { max-height: 400px; overflow-y: auto; }
.dropdown-body::-webkit-scrollbar { width: 4px; }
.dropdown-body::-webkit-scrollbar-track { background: transparent; }
.dropdown-body::-webkit-scrollbar-thumb { background: var(--neutral-300); border-radius: 4px; }

/* 通知項目 */
.notification-item {
  padding: 14px 18px;
  border-bottom: 1px solid var(--border);
  cursor: pointer; transition: background 0.15s;
}
.notification-item:last-child { border-bottom: none; }
.notification-item:hover { background: var(--surface-2); }

/* 未讀 */
.notification-item.unread {
  background: var(--teal-50);
  border-left: 3px solid var(--teal);
  padding-left: 15px;
}
.notification-item.unread:hover { background: var(--teal-100); }

.item-top {
  display: flex; justify-content: space-between;
  align-items: baseline; margin-bottom: 4px;
}

.rule-name { font-size: 12px; font-weight: 600; color: var(--teal); }
.item-time { font-size: 11px; color: var(--text-muted); white-space: nowrap; }
.item-message { font-size: 13px; color: var(--text-secondary); line-height: 1.5; }

/* 提示文字 */
.hint {
  text-align: center;
  padding: 24px 0;
  font-size: 14px;              /* 從 13px → 14px */
  color: var(--neutral-500);   /* 從 text-muted(0.40) → 0.50 */
  font-weight: 500;
}
.end-hint { padding: 12px 0; font-size: 12px; }
</style>