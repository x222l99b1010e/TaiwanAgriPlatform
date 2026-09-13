<template>
  <div class="foodsafety-layout">
    <RouterView />
  </div>
</template>

<script setup lang="ts">
</script>

<style scoped>
/* 不給 min-width：理由同 MarketView——外殼撐寬會讓響應式的入口頁失效。
   min-height 用 100% 不用 100vh：這一層住在 App.vue 的 .main-content 裡（flex: 1），
   寫 100vh 等於要求它自己就佔滿一個視窗高度，頁首與頁尾的高度會再疊上去，
   內容不長的頁面因此多出一條捲軸、頁尾被推到摺線以下。另外三個模組外殼都是 100%。 */
.foodsafety-layout {
  width: 100%;
  box-sizing: border-box;
  min-height: 100%;
}
</style>