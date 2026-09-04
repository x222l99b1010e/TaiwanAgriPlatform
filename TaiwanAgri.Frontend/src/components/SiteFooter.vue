<!--
  src/components/SiteFooter.vue
  職責：全站企業官網式頁尾。掛在 App.vue 的 <main> 之後，所以每一頁的底部都有同一份
  頁尾——不是只有首頁（owner 2026-09-04：每頁底部都要補，含農場設定與監看清單）。

  原本這段內嵌在 HomeView 的「屏 4」，各頁沒有；抽成共用元件後首頁改用這一份，
  不再自己養一份。sitemap 的連結直接讀 navStore.modules（後端種子），
  換路由或改模組名只要動後端，這裡自動跟著換。

  刻意保留「非官方網站」一行：用企業頁尾的外觀，但不假裝成農業部或真的有合作單位。
-->
<template>
  <footer class="site-footer">
    <div class="site-footer__inner">
      <div class="site-footer__brand">
        <div class="site-footer__logo">
          <span class="mdi mdi-sprout" />
          <span class="site-footer__logo-zh">台灣農業平台</span>
        </div>
        <p class="site-footer__logo-en">TAIWAN AGRI PLATFORM · SINCE 2026</p>
        <p class="site-footer__tagline">把政府開放資料，變成農民看得懂的今日數字。</p>
      </div>

      <nav class="site-footer__col">
        <p class="site-footer__col-title">網站導覽 · SITEMAP</p>
        <RouterLink
          v-for="m in modules"
          :key="m.route"
          :to="m.route"
          class="site-footer__link"
        >{{ m.name }}</RouterLink>
      </nav>

      <div class="site-footer__col">
        <p class="site-footer__col-title">資料來源 · OPEN DATA</p>
        <p class="site-footer__note">
          行情、氣象、食安、動物四個模組的資料，一律取自
          <strong>行政院農業部開放資料平台</strong>。各模組依各自排程更新、
          不是同一支資料，實際時間以各頁查詢結果為準。
        </p>
      </div>
    </div>

    <div class="site-footer__bar">
      <span>© 2026 台灣農業平台　版權所有 · All rights reserved.</span>
      <span class="site-footer__bar-note">本站為個人專案作品，非官方網站。</span>
    </div>
  </footer>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink } from 'vue-router'
import { useNavStore } from '@/stores/nav'

const navStore = useNavStore()
// App.vue 掛載時已經 loadModules()，這裡只讀，不重複觸發載入
const modules = computed(() => navStore.modules)
</script>

<style scoped>
/* 企業官網式頁尾。底色用最深的夜土，跟導覽列同一組深色，讓每頁「頭深、身淺、尾深」
   收在同一種節奏裡。 */
/* 頁尾刻意壓矮：它是「腳」，不該比內容還重（owner 2026-09-04：太高會變頭輕腳重）。
   上緣留白從 64 收到 40、內欄下緣從 40 收到 28、版權列的留白也一起收。 */
.site-footer {
  background: var(--color-deep);
  color: var(--color-on-deep-dim);
  padding: var(--space-10) var(--page-padding-x) var(--space-5);
}
.site-footer__inner {
  max-width: var(--container-lg);
  margin-inline: auto;
  display: grid;
  grid-template-columns: 2fr 1fr 1.6fr;
  gap: var(--space-6) var(--space-10);
  padding-bottom: var(--space-6);
  border-bottom: var(--border-width) solid var(--color-deep-border);
}
.site-footer__logo { display: flex; align-items: center; gap: var(--space-2); }
.site-footer__logo .mdi { font-size: var(--text-2xl); color: var(--color-action-on-deep); }
.site-footer__logo-zh { font-size: var(--text-xl); font-weight: var(--weight-bold); color: var(--color-on-deep); }
.site-footer__logo-en {
  margin-top: var(--space-3);
  font-family: var(--font-num);
  font-size: var(--text-xs);
  letter-spacing: var(--tracking-label);
  color: var(--color-on-deep-dim);
}
.site-footer__tagline {
  margin-top: var(--space-4);
  max-width: 34ch;
  font-size: var(--text-sm);
  line-height: var(--leading-loose);
}
.site-footer__col-title {
  margin-bottom: var(--space-4);
  font-family: var(--font-num);
  font-size: var(--text-xs);
  font-weight: 600;
  letter-spacing: var(--tracking-label);
  text-transform: uppercase;
  color: var(--color-on-deep);
}
.site-footer__link {
  display: block;
  padding: var(--space-1) 0;
  font-size: var(--text-sm);
  color: var(--color-on-deep-dim);
  text-decoration: none;
  transition: color var(--duration-fast) var(--ease-work);
}
.site-footer__link:hover { color: var(--color-action-on-deep); }
.site-footer__note { font-size: var(--text-sm); line-height: var(--leading-loose); }
.site-footer__note strong { color: var(--color-on-deep); font-weight: var(--weight-medium); }
.site-footer__bar {
  max-width: var(--container-lg);
  margin-inline: auto;
  padding-top: var(--space-4);
  display: flex;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: var(--space-2);
  font-size: var(--text-xs);
}
.site-footer__bar-note { opacity: 0.72; }

@media (max-width: 760px) {
  .site-footer__inner { grid-template-columns: 1fr; gap: var(--space-8); }
}
</style>
