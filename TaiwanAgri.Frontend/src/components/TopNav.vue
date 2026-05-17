<template>
  <header class="top-nav">
    <div class="logo">
      <span class="mdi mdi-sprout logo-icon" />
      <span class="logo-text">台灣農業平台</span>
    </div>

    <nav class="module-tabs">
      <router-link
        v-for="mod in navStore.modules"
        :key="mod.route"
        :to="mod.route"
        class="tab"
        :class="{ active: isActive(mod.route) }"
      >
        <span :class="`mdi ${mod.icon}`" />
        {{ mod.name }}
      </router-link>
    </nav>

    <div class="top-right">
      <button class="login-btn">登入</button>
    </div>
  </header>
</template>

<script setup lang="ts">
import { useRoute } from 'vue-router'
import { useNavStore } from '@/stores/nav'

const route = useRoute()
const navStore = useNavStore()

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
}
.logo { display: flex; align-items: center; gap: 8px; font-size: 18px; font-weight: bold; }
.logo-icon { font-size: 24px; }
.module-tabs { display: flex; gap: 4px; flex: 1; }
.tab {
  display: flex; align-items: center; gap: 6px;
  padding: 8px 16px; border-radius: 6px;
  color: rgba(255,255,255,0.75); text-decoration: none; font-size: 14px;
  transition: background 0.2s;
}
.tab:hover { background: rgba(255,255,255,0.1); }
.tab.active { background: rgba(255,255,255,0.2); color: white; font-weight: 600; }
.top-right { margin-left: auto; }
.login-btn {
  padding: 6px 16px; border-radius: 6px; border: 1px solid rgba(255,255,255,0.5);
  background: transparent; color: white; cursor: pointer;
}
</style>