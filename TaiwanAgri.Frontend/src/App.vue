<template>
  <div class="app-layout">
    <TopNav />
    <main class="main-content">
      <RouterView />
    </main>
  </div>
</template>

<script setup lang="ts">
import { onMounted } from 'vue'
import TopNav from '@/components/TopNav.vue'
import { useNavStore } from '@/stores/nav'

const navStore = useNavStore()
onMounted(() => navStore.loadModules())
</script>

<style>
* { box-sizing: border-box; margin: 0; padding: 0; }
html { width: 100%; }
#app { width: 100%; min-height: 100vh; }
.app-layout { display: flex; flex-direction: column; min-height: 100vh; }
.main-content { flex: 1; overflow-y: auto; padding: 24px; background: var(--bg); }
</style>