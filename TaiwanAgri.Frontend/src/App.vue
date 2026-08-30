<template>
  <div class="app-layout">
    <TopNav />
    <!-- 今日菜價跑馬燈刻意維持滿版：左右兩端的標籤與日期是有底色的端塊，
         跟著容器內縮會在兩側留下一條不同底色的縫，而跑馬燈本身在畫面中途
         停住也不成立。滿版的元素只有導覽列底色與這一條。 -->
    <VegPriceTicker />
    <main class="main-content">
      <RouterView />
    </main>
  </div>
</template>

<script setup lang="ts">
import { onMounted } from 'vue'
import TopNav from '@/components/TopNav.vue'
import VegPriceTicker from '@/components/VegPriceTicker.vue'
import { useNavStore } from '@/stores/nav'

const navStore = useNavStore()
onMounted(() => navStore.loadModules())
</script>

<style>
* { box-sizing: border-box; margin: 0; padding: 0; }
/* scrollbar-gutter: stable both-edges——捲軸出現與否會改變可用寬度，置中的容器
   會因此左右位移約 15px，換頁時看起來就像頁首在跳。兩側都預留槽位，內容不論
   有沒有捲軸都停在同一個位置，也維持真正置中（只留單邊會整體偏左）。 */
html { width: 100%; scrollbar-gutter: stable both-edges; }
#app { width: 100%; min-height: 100vh; }
.app-layout { display: flex; flex-direction: column; min-height: 100vh; }
/* 這裡不再有 padding——頁面留白統一由 base.css 的 .page 負責。
   原本這層 24px 會跟各頁自己的 36px 56px 疊加成 60px / 80px，
   造成「改了頁面裡的數字，量出來卻是別的值」。 */
.main-content { flex: 1; overflow-y: auto; background: var(--bg); }
</style>