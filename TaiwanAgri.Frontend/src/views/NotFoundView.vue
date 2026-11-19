<!--
  src/views/NotFoundView.vue
  職責：網址對不到任何路由時顯示的頁面（路由表最後一條兜底規則）。
  沒有這一頁的話，打錯網址只會看到導覽列和頁尾、中間一片空白，像是網站壞了。
-->
<template>
  <div class="page not-found-view">
    <PageHeader title="找不到這一頁" title-en="PAGE NOT FOUND" />
    <StateBlock
      state="empty"
      icon="mdi-map-marker-question-outline"
      message="這個網址沒有對應的頁面"
      hint="可能是網址打錯，或這一頁已經搬家了"
    >
      <template #actions>
        <RouterLink to="/" class="home-link">
          <span class="mdi mdi-home-variant-outline" />回到首頁
        </RouterLink>
      </template>
    </StateBlock>
  </div>
</template>

<script setup lang="ts">
import PageHeader from '@/components/ui/PageHeader.vue'
import StateBlock from '@/components/ui/StateBlock.vue'
</script>

<style scoped>
.home-link {
  display: inline-flex; align-items: center; gap: var(--space-2);
  color: var(--color-action);
  font-weight: var(--weight-medium);
  text-decoration: underline;
}
.home-link:focus-visible { outline: 2px solid var(--color-action); outline-offset: 2px; border-radius: var(--radius-sm); }
</style>
