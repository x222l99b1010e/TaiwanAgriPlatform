<template>
  <div class="market-view">
    <RouterView />
  </div>
</template>

<script setup lang="ts">
</script>

<style scoped>
/* 不給 min-width：模組入口頁（ModuleEntryView）是響應式的，外殼一旦撐到 960px，
   窄螢幕看到的就會是整頁縮放版。各子頁自己仍有 min-width: 960px，
   那是那幾頁的資料表格需要的寬度，與這一層無關。 */
.market-view {
  width: 100%;
  box-sizing: border-box;
  min-height: 100%;
}
</style>