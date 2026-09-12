<!--
  src/views/ModuleEntryView.vue
  職責：四個模組的入口頁——深色頁首帶 ＋ 底下左右交錯的子頁特寫列。

  一支元件掛在四條模組路由的空子路徑上（/market、/weather、/food-safety、/pet），
  是哪個模組由當前路徑決定，不是四個檔案各寫一次同樣的排版。

  子頁清單取自 navStore 而不是寫死：導覽列的下拉選單讀的就是這份資料，
  兩邊各寫一份必然漂移；而且它已經帶了角色權限（NavService 依 Role 過濾），
  使用者沒有權限看的子頁不該出現在這裡。

  列本身用共用元件 ShowcaseRow（首頁屏 3 用的是同一支，只差一階尺寸）。
  這一頁另外決定三件事：誰左誰右、橫向的錯落量、以及整頁共用哪一種 hover 特效——
  特效依模組而不是依列，因為它要講的是「這裡是哪個模組」，同一頁換來換去會失去意義。

  navStore.modules 由 App.vue 掛載時載入，直接打網址進到這一頁時會有一小段空窗，
  所以「畫不出模組」要分三種，三種的下一步動作完全不同：還在載入（等一下就好）、
  載完了但一個模組都拿不到（權限問題，找管理員）、載完了但這個路徑對不到（網址打錯）。
  ⚠ 判斷「還在載入」一律看 navStore.loaded，不要看 modules.length——
  權限全被收掉時後端合法回傳空陣列，用長度判斷會讓那種帳號永遠停在轉圈。
-->
<template>
  <EntryLayout v-if="mod" :title="mod.name" :title-en="titleEn" :lead="lead">
    <template #motif>
      <SeasonMotif :season="solarTerm.current.season" />
    </template>

    <p class="screen-eyebrow">FUNCTIONS</p>
    <h2 class="screen-title">功能一覽</h2>

    <!-- 子頁清單是空的並非不可能：NavService 依角色過濾，權限被收掉時這個模組會只剩自己。
         不擋的話「功能一覽」底下會是一整片空白，看起來像頁面載到一半壞掉。 -->
    <StateBlock
      v-if="mod.children.length === 0"
      state="empty"
      message="這個模組目前沒有可用的功能"
      hint="可能是權限設定尚未開放，請聯絡管理員。"
    />

    <div v-else class="child-showcase">
      <ShowcaseRow
        v-for="(child, i) in mod.children"
        :key="child.route"
        class="child-showcase__row"
        :class="{ 'child-showcase__row--inset': i % 2 === 1 }"
        :index="i + 1"
        :to="child.route"
        :icon="child.icon"
        :eyebrow="CHILD_NAME_EN[child.route]"
        :name="child.name"
        :lead="CHILD_LEAD[child.route]"
        go-label="進入"
        :flip="i % 2 === 1"
        :effect="effect"
        size="compact"
      />
    </div>
  </EntryLayout>

  <div v-else class="page">
    <StateBlock v-if="!navStore.loaded" state="loading" message="載入模組中..." />
    <StateBlock
      v-else-if="navStore.modules.length === 0"
      state="empty"
      message="目前沒有可以瀏覽的模組"
      hint="可能是權限設定尚未開放，請聯絡管理員。"
    />
    <StateBlock
      v-else
      state="empty"
      message="找不到這個模組"
      hint="網址可能有誤，請從上方導覽列重新選擇。"
    />
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import EntryLayout from '@/components/layouts/EntryLayout.vue'
import StateBlock from '@/components/ui/StateBlock.vue'
import SeasonMotif from '@/components/SeasonMotif.vue'
import ShowcaseRow from '@/components/ShowcaseRow.vue'
import { useNavStore } from '@/stores/nav'
import { getTodaySolarTerm } from '@/utils/solarTerms'
import {
  MODULE_NAME_EN,
  MODULE_LEAD,
  MODULE_EFFECT,
  CHILD_NAME_EN,
  CHILD_LEAD,
} from '@/constants/navCopy'

const route = useRoute()
const navStore = useNavStore()
const solarTerm = getTodaySolarTerm()

const mod = computed(() => navStore.currentModule(route.path))
const titleEn = computed(() => (mod.value ? MODULE_NAME_EN[mod.value.route] : undefined))
const lead = computed(() => (mod.value ? MODULE_LEAD[mod.value.route] : undefined))
const effect = computed(() => (mod.value && MODULE_EFFECT[mod.value.route]) || 'sparks')

</script>

<style scoped>
/* .screen-eyebrow／.screen-title 走 base.css 的全域分段標題，這裡不再自己寫一份 */
.child-showcase {
  display: flex;
  flex-direction: column;
  gap: var(--space-6);
}

/* 錯落：奇數列整列往內縮一段，讓左右兩側的邊界變成鋸齒而不是兩條直線。
   只縮一側、而且縮的是「圖塊在的那一側的相反邊」，視覺上等於把整列往圖塊那邊推，
   交錯的節奏才會被放大——單純左右對調而邊界對齊，看起來仍然是一疊等寬的方塊。
   用 margin 不用 transform：transform 不佔空間，相鄰列的間距會看起來忽大忽小。 */
.child-showcase__row--inset {
  margin-inline-start: var(--space-16);
}
.child-showcase__row:not(.child-showcase__row--inset) {
  margin-inline-end: var(--space-16);
}

/* 窄螢幕把錯落拿掉：列已經收成上下堆疊，再留縮排只會變成莫名其妙的左邊界不齊 */
@media (max-width: 760px) {
  .child-showcase__row--inset,
  .child-showcase__row:not(.child-showcase__row--inset) {
    margin-inline: 0;
  }
}
</style>
