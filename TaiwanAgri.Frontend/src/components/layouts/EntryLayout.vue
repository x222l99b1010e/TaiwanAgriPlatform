<!--
  src/components/layouts/EntryLayout.vue
  職責：入口型頁面的外殼——深色頁首帶＋底下的子頁卡片牆。
  四個模組入口頁與新首頁都用這一個。

  為什麼四個模組入口頁都要有深色頁首帶：首頁的視覺衝擊如果只發生一次，
  點進任何一個模組就整個弱掉，內頁會顯得平坦。把那一道幕在四個入口各放一次，
  衝擊就是四次；而導覽列全站維持深色，是首頁與內頁之間唯一連續的東西，
  落差才不是斷崖。

  這一層只負責「幕」本身：底色、光暈、標題、往下的入口。線稿母題是插槽，
  因為它會隨節氣換圖，而換的只有幾何——顏色、字級、動效不會因為季節改變。

  幕的高度用 min-height 而不是固定值：內容比預期長時要能撐開，
  固定高度會讓文字直接溢位到下一段。
-->
<template>
  <div class="entry-layout">
    <header class="entry-layout__band">
      <div class="entry-layout__motif" aria-hidden="true"><slot name="motif" /></div>

      <div class="entry-layout__band-inner">
        <p v-if="eyebrow" class="entry-layout__eyebrow">{{ eyebrow }}</p>
        <h1 class="entry-layout__title">
          <Bilingual :zh="title" :en="titleEn" layout="stacked" tone="deep" />
        </h1>
        <p v-if="lead" class="entry-layout__lead">{{ lead }}</p>
        <div v-if="$slots.stats" class="entry-layout__stats"><slot name="stats" /></div>
      </div>
    </header>

    <div class="entry-layout__body">
      <slot />
    </div>
  </div>
</template>

<script setup lang="ts">
import Bilingual from '@/components/ui/Bilingual.vue'

defineProps<{
  title: string
  titleEn?: string
  /** 標題上方那一行小字，例如節氣或模組分類 */
  eyebrow?: string
  /** 一句話說明這個模組在做什麼 */
  lead?: string
}>()
</script>

<style scoped>
.entry-layout__band {
  position: relative;
  overflow: hidden;
  background: var(--color-deep);
  color: var(--color-on-deep);
  /* 幕約佔視窗三成：夠成為視覺主角，又不會讓使用者得先捲一整屏才看到入口 */
  min-height: 30vh;
  display: flex;
  align-items: flex-end;
  padding: var(--space-16) var(--page-padding-x) var(--space-12);

  /* 兩層極慢漂移的光暈。兩層速度不同，才不會看起來像單純在閃 */
  background-image:
    radial-gradient(60% 70% at 74% 22%, var(--color-glow-1), transparent),
    radial-gradient(52% 64% at 18% 84%, var(--color-glow-2), transparent);
}

.entry-layout__motif {
  position: absolute;
  inset: 0;
  pointer-events: none;
  color: var(--color-motif);
}

.entry-layout__band-inner {
  position: relative;
  width: 100%;
  max-width: var(--container-lg);
  margin-inline: auto;
}

.entry-layout__eyebrow {
  font-family: var(--font-num);
  font-size: var(--text-xs);
  font-weight: var(--weight-medium);
  letter-spacing: var(--tracking-label);
  text-transform: uppercase;
  color: var(--color-action-on-deep);
}

.entry-layout__title {
  margin-top: var(--space-3);
  font-family: var(--font-display);
  font-size: var(--text-4xl);
  font-weight: var(--weight-bold);
  line-height: var(--leading-display);
  /* 襯線壓在深色底上會糊，用字距補償而不是加重字重 */
  letter-spacing: var(--tracking-title-on-deep);
}

.entry-layout__lead {
  margin-top: var(--space-5);
  max-width: 52ch;
  font-size: var(--text-base);
  line-height: var(--leading-loose);
  color: var(--color-on-deep-dim);
}

.entry-layout__stats {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-10);
  margin-top: var(--space-8);
}

.entry-layout__body {
  width: 100%;
  max-width: var(--container-lg);
  margin-inline: auto;
  padding: var(--page-padding-y) var(--page-padding-x);
}

@media (prefers-reduced-motion: no-preference) {
  .entry-layout__band {
    animation: entry-band-in var(--duration-entry) var(--ease-entry) both;
  }
}

@keyframes entry-band-in {
  from {
    opacity: 0;
    transform: translateY(calc(var(--entry-y) / 2));
  }
}
</style>
