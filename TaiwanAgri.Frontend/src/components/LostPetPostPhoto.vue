<!--
  src/components/LostPetPostPhoto.vue
  職責：LostPetPost 照片渲染，從 LostPetsView 抽出來給列表卡片與詳情頁共用（不掛週次分支新增）。
  照片是使用者自貼的外部圖床連結，本站只存字串不託管圖片，兩處呼叫端的行為完全一致：
  失效時顯示提示文字、referrerpolicy 保護、點擊開新分頁看原圖。
  crop 這個 prop 是兩處唯一的外觀差異：列表卡片要 4:3 裁切維持格線整齊，
  詳情頁的重點是「看清楚長什麼樣子」，裁切反而會切掉需要辨識的特徵，所以不裁。
-->
<template>
  <div v-if="isDisplayableImageUrl(photoUrl)" class="post-photo">
    <div v-if="failed" class="photo-failed" :class="{ uncropped: !crop }">
      <span class="mdi mdi-image-off-outline" /> 圖片無法載入（外部連結可能已失效）
    </div>
    <a v-else :href="photoUrl!" target="_blank" rel="noopener noreferrer" class="photo-link" title="開新分頁檢視完整原始圖片">
      <img
        :src="photoUrl!"
        :alt="`${title} 的照片`"
        class="photo-img"
        :class="{ uncropped: !crop }"
        loading="lazy"
        referrerpolicy="no-referrer"
        @error="failed = true"
      />
      <span class="photo-zoom-hint"><span class="mdi mdi-magnify-plus-outline" /> 看完整圖片</span>
    </a>
    <p class="photo-note">圖片由張貼者提供、存放於外部網站，非本站託管</p>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { isDisplayableImageUrl } from '@/utils/lostPetPost'

withDefaults(defineProps<{
  photoUrl: string | null | undefined
  title: string
  /** true＝固定 4:3 裁切（列表卡片用，維持格線整齊）；false＝不裁切、依原始比例顯示（詳情頁用） */
  crop?: boolean
}>(), {
  crop: true,
})

// 每個元件實例各自的載入失敗狀態，不需要像列表頁那樣用 Record<id, boolean> 手動管理，
// 這正是抽成獨立元件的好處之一：狀態自然跟著元件實例走
const failed = ref(false)
</script>

<style scoped>
.post-photo { display: flex; flex-direction: column; gap: var(--space-1); }

/*
  用 aspect-ratio 取代固定 height：卡片寬度是 minmax(400px, 1fr)、會隨視窗伸縮，
  固定高度在寬卡片上會變成扁條、在窄卡片上又過高。綁比例才能在任何寬度下都維持一致的構圖。
*/
.photo-img {
  width: 100%; aspect-ratio: 4 / 3; object-fit: cover; display: block;
  border-radius: var(--radius-lg);
  border: var(--border-width) solid var(--color-border);
  background: var(--color-bg-sunken);
}
.photo-img.uncropped {
  aspect-ratio: auto; max-height: 480px; object-fit: contain;
}
.photo-failed {
  display: flex; align-items: center; justify-content: center; gap: var(--space-2);
  width: 100%; aspect-ratio: 4 / 3; border-radius: var(--radius-lg);
  border: var(--border-width) dashed var(--color-border-strong);
  background: var(--color-bg-sunken); color: var(--color-text-dim); font-size: var(--text-sm);
}
.photo-failed.uncropped { aspect-ratio: 16 / 9; }
.photo-note { font-size: var(--text-xs); color: var(--color-text-dim); }

/* 縮圖疊一層「看完整圖片」提示，滑過才浮現，避免常駐蓋住照片內容 */
.photo-link { position: relative; display: block; }
.photo-zoom-hint {
  position: absolute; right: 8px; bottom: 8px;
  display: inline-flex; align-items: center; gap: var(--space-1);
  padding: var(--space-1) var(--space-3); border-radius: var(--radius-full);
  background: var(--black-a60); color: var(--color-on-deep); font-size: var(--text-xs); font-weight: var(--weight-medium);
  opacity: 0; transition: opacity var(--duration-fast) var(--ease-work);
}
.photo-link:hover .photo-zoom-hint { opacity: 1; }
</style>
