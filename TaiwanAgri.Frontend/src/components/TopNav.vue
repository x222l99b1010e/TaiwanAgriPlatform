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
  background: #1b5e20;
  color: white;
  position: relative;
  z-index: 100;
  box-shadow: 0 2px 8px rgba(0,0,0,0.15);
}

.top-nav-inner {
  display: flex;
  align-items: center;
  gap: 24px;
  height: 100%;
  max-width: var(--container-lg);
  margin-inline: auto;
  padding-inline: var(--page-padding-x);
}

.logo { display: flex; align-items: center; gap: 8px; font-size: 18px; font-weight: bold; }
.logo-icon { font-size: 24px; }
.module-tabs { display: flex; gap: 4px; flex: 1; }
.tab-wrapper { position: relative; }

.tab {
  display: flex; align-items: center; gap: 6px;
  padding: 8px 16px; border-radius: 6px;
  color: rgba(255,255,255,0.80); text-decoration: none; font-size: 14px;
  transition: background 0.2s;
  white-space: nowrap;
}
.tab:hover { background: rgba(255,255,255,0.12); }
.tab.active { background: rgba(255,255,255,0.20); color: white; font-weight: 600; }

.tab-dropdown {
  position: absolute;
  top: 100%;
  left: 0;
  min-width: 160px;
  background: #ffffff;
  border: 1px solid rgba(0,0,0,0.12);
  border-radius: 10px;
  padding: 4px 6px 6px;
  box-shadow: 0 8px 24px rgba(0,0,0,0.15);
  display: flex;
  flex-direction: column;
  gap: 2px;
  z-index: 200;
}

.dropdown-item {
  display: flex; align-items: center; gap: 8px;
  padding: 9px 14px; border-radius: 7px;
  color: #3a4a40; text-decoration: none; font-size: 13.5px;
  transition: background 0.15s, color 0.15s;
  white-space: nowrap;
}
.dropdown-item:hover { background: #f0f4f0; color: #1a2820; }
.dropdown-item.active { background: #e8f5e9; color: #2e7d32; font-weight: 600; }

.top-right { margin-left: auto; display: flex; align-items: center; gap: 8px; }

.login-btn {
  padding: 6px 16px; border-radius: 6px;
  border: 1px solid rgba(255,255,255,0.6);
  background: transparent; color: white; cursor: pointer;
  transition: background 0.15s;
}
.login-btn:hover { background: rgba(255,255,255,0.12); }

.user-name {
  font-size: 14px;
  color: rgba(255, 255, 255, 0.85);
  font-weight: 600;
}
</style>