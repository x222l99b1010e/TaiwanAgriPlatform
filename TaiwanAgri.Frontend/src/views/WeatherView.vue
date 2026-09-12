<template>
  <div class="weather-view">
    <RouterView />
  </div>
</template>

<script setup lang="ts">
</script>

<style scoped>
/* 不給 min-width：理由同 MarketView——外殼撐寬會讓響應式的入口頁失效 */
.weather-view {
  width: 100%;
  box-sizing: border-box;
  min-height: 100%;
}
</style>