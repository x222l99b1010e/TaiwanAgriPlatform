<template>
  <header class="top-nav">
    <div class="top-nav-inner">
      <div class="logo">
        <span class="mdi mdi-sprout logo-icon" />
        <span class="logo-text">台灣農業平台</span>
      </div>

      <nav class="module-tabs">
        <div
          v-for="mod in navStore.modules"
          :key="mod.route"
          class="tab-wrapper"
          @mouseenter="hoveredRoute = mod.route"
          @mouseleave="hoveredRoute = null"
        >
          <router-link :to="mod.route" class="tab" :class="{ active: isActive(mod.route) }">
            <span :class="`mdi ${mod.icon}`" />
            {{ mod.name }}
          </router-link>

          <div
            class="tab-dropdown"
            v-if="mod.children && mod.children.length > 0 && hoveredRoute === mod.route"
          >
            <router-link
              v-for="child in mod.children"
              :key="child.route"
              :to="child.route"
              class="dropdown-item"
              :class="{ active: route.path === child.route }"
            >
              <span :class="`mdi ${child.icon}`" />
              {{ child.name }}
            </router-link>
          </div>
        </div>
      </nav>

      <div class="top-right">
        <NotificationBell />

        <!-- 已登入：顯示名稱 + 登出 -->
        <template v-if="authStore.isLoggedIn">
          <span class="user-name">{{ authStore.displayName }}</span>
          <router-link to="/profile" class="login-btn">農場設定</router-link>
          <router-link to="/watchlist" class="login-btn">監看清單</router-link>
          <button class="login-btn" @click="handleLogout">登出</button>
        </template>

        <!-- 未登入：登入按鈕 -->
        <button v-else class="login-btn" @click="router.push('/login')">登入</button>
      </div>
    </div>
  </header>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useRoute } from 'vue-router'
import { useRouter } from 'vue-router'
import { useNavStore } from '@/stores/nav'
import { useAuthStore } from '@/stores/authStore'
import NotificationBell from '@/components/NotificationBell.vue'

const router = useRouter()
const route = useRoute()
const navStore = useNavStore()
const authStore = useAuthStore()
const hoveredRoute = ref<string | null>(null)

function isActive(moduleRoute: string) {
  return route.path === moduleRoute || route.path.startsWith(moduleRoute + '/')
}

function handleLogout() {
  authStore.logout()
  router.push('/login')
}
</script>

<style scoped>
/* 底色滿版、內容不滿版：外層只負責背景與高度，實際的排列與左右留白交給
   .top-nav-inner，寬度上限與頁面容器（base.css 的 .page）同一組 token，
   logo 的左邊界因此與各頁頁首標題落在同一條垂直線上。 */
.top-nav {
  height: 56px;
  background: var(--color-deep);
  color: var(--color-on-deep);
  position: relative;
  z-index: var(--z-dropdown);
  box-shadow: var(--shadow-md);
}

.top-nav-inner {
  display: flex;
  align-items: center;
  gap: var(--space-6);
  height: 100%;
  max-width: var(--container-lg);
  margin-inline: auto;
  padding-inline: var(--page-padding-x);
}

.logo { display: flex; align-items: center; gap: var(--space-2); font-size: var(--text-lg); font-weight: bold; }
.logo-icon { font-size: var(--text-xl); }
.module-tabs { display: flex; gap: var(--space-1); flex: 1; }
.tab-wrapper { position: relative; }

.tab {
  display: flex; align-items: center; gap: var(--space-2);
  padding: var(--space-2) var(--space-4); border-radius: var(--radius-md);
  color: var(--color-on-deep-dim); text-decoration: none; font-size: var(--text-base);
  transition: background var(--duration-base);
  white-space: nowrap;
}
.tab:hover { background: var(--white-a12); }
.tab.active { background: var(--white-a20); color: var(--color-on-deep); font-weight: var(--weight-medium); }

/* 下拉選單本身是浮在深色列下面的淺色浮動層，不是深色列的延伸——
   跟頁面其餘的淺色卡片同一組色，才不會兩種底色系統在同一個選單裡混用 */
.tab-dropdown {
  position: absolute;
  top: 100%;
  left: 0;
  min-width: 160px;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  padding: var(--space-1) var(--space-2) var(--space-2);
  box-shadow: var(--shadow-lg);
  display: flex;
  flex-direction: column;
  gap: var(--space-1);
  z-index: var(--z-sticky);
}

.dropdown-item {
  display: flex; align-items: center; gap: var(--space-2);
  padding: var(--space-2) var(--space-4); border-radius: var(--radius-md);
  color: var(--color-text); text-decoration: none; font-size: var(--text-sm);
  transition: background var(--duration-fast), color var(--duration-fast);
  white-space: nowrap;
}
.dropdown-item:hover { background: var(--seed-50); color: var(--color-text); }
.dropdown-item.active { background: var(--seed-100); color: var(--color-action); font-weight: var(--weight-medium); }

.top-right { margin-left: auto; display: flex; align-items: center; gap: var(--space-2); }

.login-btn {
  padding: var(--space-2) var(--space-4); border-radius: var(--radius-md);
  border: 1px solid var(--white-a60);
  background: transparent; color: var(--color-on-deep); cursor: pointer;
  transition: background var(--duration-fast);
}
.login-btn:hover { background: var(--white-a12); }

.user-name {
  font-size: var(--text-base);
  color: var(--color-on-deep-dim);
  font-weight: var(--weight-medium);
}
</style>