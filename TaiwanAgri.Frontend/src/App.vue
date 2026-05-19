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

body {
  font-family: 'Noto Sans TC', -apple-system, BlinkMacSystemFont, 'SF Pro Display', 'Segoe UI', sans-serif;
  font-size: 16px;
  line-height: 1.7;
  background-color: #161c18;
  background-image:
    radial-gradient(ellipse 90% 50% at 50% 100%, rgba(50, 110, 55, 0.22) 0%, transparent 65%),
    radial-gradient(ellipse 50% 35% at 100% 0%, rgba(120, 160, 130, 0.08) 0%, transparent 55%),
    linear-gradient(160deg, rgba(80, 100, 85, 0.06) 0%, transparent 40%);
  color: #ddeedd;
  min-height: 100vh;
  overflow-x: auto;
}

#app { width: 100%; min-height: 100vh; }

.app-layout { display: flex; flex-direction: column; height: 100vh; }
.main-content { flex: 1; overflow-y: auto; padding: 24px; }
</style>