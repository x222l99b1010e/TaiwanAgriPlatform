<template>
  <header class="top-nav">
    <div class="logo">
      <span class="mdi mdi-sprout logo-icon" />
      <span class="logo-text">台灣農業平台</span>
    </div>

    <nav class="module-tabs">
      <!-- tab-wrapper 同時涵蓋 tab 和 dropdown，mouseleave 才不會在間隙觸發 -->
      <div
        v-for="mod in navStore.modules"
        :key="mod.route"
        class="tab-wrapper"
        @mouseenter="hoveredRoute = mod.route"
        @mouseleave="hoveredRoute = null"
      >
        <router-link
          :to="mod.route"
          class="tab"
          :class="{ active: isActive(mod.route) }"
        >
          <span :class="`mdi ${mod.icon}`" />
          {{ mod.name }}
        </router-link>

        <!-- 子選單：有 children 且滑鼠停在此 wrapper 才顯示 -->
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
      <button class="login-btn">登入</button>
    </div>
  </header>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useRoute } from 'vue-router'
import { useNavStore } from '@/stores/nav'
import NotificationBell from '@/components/NotificationBell.vue'

const route = useRoute()
const navStore = useNavStore()
const hoveredRoute = ref<string | null>(null)

function isActive(moduleRoute: string) {
  return route.path === moduleRoute || route.path.startsWith(moduleRoute + '/')
}
</script>

<style scoped>
.top-nav {
  display: flex;
  align-items: center;
  padding: 0 24px;
  height: 56px;
  background: #1b5e20;
  color: white;
  gap: 24px;
  position: relative;
  z-index: 100;
}

.logo { display: flex; align-items: center; gap: 8px; font-size: 18px; font-weight: bold; }
.logo-icon { font-size: 24px; }

.module-tabs { display: flex; gap: 4px; flex: 1; }

/* tab-wrapper 是定位錨點，同時是 hover 事件的邊界 */
.tab-wrapper {
  position: relative;
}

.tab {
  display: flex; align-items: center; gap: 6px;
  padding: 8px 16px; border-radius: 6px;
  color: rgba(255,255,255,0.75); text-decoration: none; font-size: 14px;
  transition: background 0.2s;
  white-space: nowrap;
}
.tab:hover { background: rgba(255,255,255,0.1); }
.tab.active { background: rgba(255,255,255,0.2); color: white; font-weight: 600; }

/* ── Dropdown ──
   關鍵：top: 100% 而非 calc(100% + 4px)
   讓 dropdown 緊貼 tab 底部，消除會觸發 mouseleave 的空隙。
   視覺上的留白改用 padding-top 製造，padding 屬於元素內部，不會有問題。
*/
.tab-dropdown {
  position: absolute;
  top: 100%;
  left: 0;
  min-width: 160px;
  background: #1a3d1f;
  border: 1px solid rgba(255,255,255,0.12);
  border-radius: 10px;
  /* padding-top 取代原本的 gap，視覺上仍有與 tab 的間距感 */
  padding: 4px 6px 6px;
  box-shadow: 0 8px 24px rgba(0,0,0,0.4);
  display: flex;
  flex-direction: column;
  gap: 2px;
  z-index: 200;
}

.dropdown-item {
  display: flex; align-items: center; gap: 8px;
  padding: 9px 14px; border-radius: 7px;
  color: rgba(255,255,255,0.7); text-decoration: none; font-size: 13.5px;
  transition: background 0.15s, color 0.15s;
  white-space: nowrap;
}
.dropdown-item:hover { background: rgba(255,255,255,0.1); color: white; }
.dropdown-item.active { background: rgba(255,255,255,0.18); color: white; font-weight: 600; }

.top-right {
  display: flex;
  align-items: center;
  gap: 8px;
}
.login-btn {
  padding: 6px 16px; border-radius: 6px; border: 1px solid rgba(255,255,255,0.5);
  background: transparent; color: white; cursor: pointer;
}
</style>