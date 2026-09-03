<template>
  <div class="app-layout">
    <TopNav />
    <!-- 今日菜價跑馬燈刻意維持滿版：它是導覽列深色區的下半截，深色底一路接到內容
         才不會在兩側留下一條縫；跑馬燈本身在畫面中途停住也不成立。
         左右端的標籤與日期改吃 --page-padding-x，文字仍與頁面容器對齊。
         滿版的元素只有導覽列底色與這一條。 -->
    <VegPriceTicker />
    <main class="main-content">
      <RouterView />
    </main>
    <!-- 企業官網式頁尾：掛在這裡而不是各頁裡面，每一頁的底部才都有同一份頁尾。
         .main-content 是 flex:1，內容不夠長時會把頁尾推到視窗底部（sticky footer）。 -->
    <SiteFooter />
  </div>
</template>

<script setup lang="ts">
import { onMounted } from 'vue'
import TopNav from '@/components/TopNav.vue'
import VegPriceTicker from '@/components/VegPriceTicker.vue'
import SiteFooter from '@/components/SiteFooter.vue'
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
/* ⚠ 這裡曾經是 overflow-y: auto，但 .app-layout 是 min-height: 100vh 不是 height:
   100vh，內容一旦超過一屏就會把 .app-layout 撐高，捲動的實際上一直是 body／視窗，
   這個屬性從未真正生效過（.main-content 自己永遠沒有機會出現捲軸）。
   問題是 CSS 規格認定「有沒有 overflow」不看「現在有沒有捲軸」，只看這個屬性值——
   所以它雖然沒在做事，卻會讓底下任何 position: sticky 的元素改認它當捲動容器，
   而它自己又不捲動，sticky 就整個失效（QueryLayout 的吸頂工具列查出這個坑）。 */
.main-content { flex: 1; background: var(--color-bg); }
</style>