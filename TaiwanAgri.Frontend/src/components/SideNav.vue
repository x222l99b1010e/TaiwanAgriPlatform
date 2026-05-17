<template>
  <aside class="side-nav" v-if="currentMod && currentMod.children.length > 0">
    <div class="side-title">
      <span :class="`mdi ${currentMod.icon}`" />
      {{ currentMod.name }}
    </div>
    <router-link
      v-for="child in currentMod.children"
      :key="child.route"
      :to="child.route"
      class="side-item"
      :class="{ active: route.path === child.route }"
    >
      <span :class="`mdi ${child.icon}`" />
      {{ child.name }}
    </router-link>
  </aside>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { useNavStore } from '@/stores/nav'

const route = useRoute()
const navStore = useNavStore()

const currentMod = computed(() => navStore.currentModule(route.path))
</script>

<style scoped>
.side-nav {
  width: 200px; min-height: 100%;
  background: #f5f5f5; padding: 16px 8px;
  display: flex; flex-direction: column; gap: 4px;
}
.side-title {
  display: flex; align-items: center; gap: 8px;
  font-size: 13px; font-weight: 600; color: #555;
  padding: 8px 12px; margin-bottom: 4px;
}
.side-item {
  display: flex; align-items: center; gap: 10px;
  padding: 10px 12px; border-radius: 6px;
  color: #333; text-decoration: none; font-size: 14px;
  transition: background 0.2s;
}
.side-item:hover { background: #e0e0e0; }
.side-item.active { background: #c8e6c9; color: #1b5e20; font-weight: 600; }
</style>