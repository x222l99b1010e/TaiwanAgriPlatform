<!--
  src/components/LostPetPostForm.vue
  職責：LostPetPost 新增／編輯表單，從 LostPetsView 抽出來給列表頁、詳情頁、個人管理頁三處共用
  （owner 2026-08-09 裁定：詳情頁要能原地編輯，不要求使用者跳轉回列表頁；抽共用元件才不會
  變成三處各維護一份表單邏輯、改一個欄位要記得改三次）。

  模式判斷用 `post` prop 是否為 null：null＝新增，帶入既有 LostPetPostResponseDict＝編輯——
  不用另外傳一個 mode prop，因為「有沒有現有資料」跟「新增還是編輯」這兩件事本來就是同一件事，
  分開傳反而多一個「兩個 prop 講的是同一件事、但可能對不起來」的風險（例如 mode='create' 卻
  傳了一個 post 進來，元件該信哪一個？用單一 prop 就不會有這種歧義）。
-->
<template>
  <section class="post-form-panel" ref="panelRef">
    <h3 class="form-title">{{ post == null ? '張貼新的協尋啟事' : '編輯協尋啟事' }}</h3>

    <div class="form-grid">
      <div class="field-group span-2">
        <label class="field-label">標題 *</label>
        <input v-model="form.title" class="field-input" maxlength="100" placeholder="例如：臺中北屯走失黑色米克斯" />
      </div>

      <div class="field-group span-2">
        <label class="field-label">描述 *</label>
        <textarea v-model="form.description" class="field-textarea" maxlength="2000" rows="3"
          placeholder="特徵、走失時間地點、其他協尋資訊" />
      </div>

      <div class="field-group">
        <label class="field-label">縣市</label>
        <CitySelector v-model="form.county" include-all />
      </div>

      <div class="field-group">
        <label class="field-label">電話</label>
        <input v-model="form.phone" class="field-input" maxlength="50" placeholder="0912345678" />
      </div>

      <div class="field-group">
        <label class="field-label">Email</label>
        <input v-model="form.email" class="field-input" maxlength="254" placeholder="you@example.com" />
      </div>

      <div class="field-group">
        <label class="field-label">照片連結（選填）</label>
        <input v-model="form.photoUrl" class="field-input" maxlength="1200"
          placeholder="外部圖床網址，例如 https://i.imgur.com/xxxx.jpg" />
        <p class="field-hint">
          需為 http:// 或 https:// 開頭的完整網址。圖片存放在外部網站、非本站託管，
          連結失效或內容變更本站無法控制。
        </p>
      </div>

      <!-- 只有編輯既有貼文時才能改狀態；新增一律從「協尋中」開始（後端強制，前端表單不提供這個選項） -->
      <div v-if="post != null" class="field-group">
        <label class="field-label">狀態</label>
        <select v-model="form.status" class="field-select">
          <option v-for="opt in editableStatusOptions" :key="opt.value" :value="opt.value">{{ opt.label }}</option>
        </select>
      </div>
    </div>

    <div class="field-group span-2">
      <label class="field-label">走失／拾獲地點座標（選填，點地圖設定）</label>
      <LeafletCoordinatePicker v-model:latitude="form.latitude" v-model:longitude="form.longitude" />
    </div>

    <p v-if="formError" class="error-msg">{{ formError }}</p>
    <p v-if="store.saveLostPetPostError" class="error-msg">{{ store.saveLostPetPostError }}</p>

    <div class="form-actions">
      <Btn :loading="store.isSavingLostPetPost" @click="handleSubmit">
        {{ store.isSavingLostPetPost ? '送出中...' : (post == null ? '送出' : '儲存變更') }}
      </Btn>
      <Btn variant="secondary" :disabled="store.isSavingLostPetPost" @click="emit('cancel')">取消</Btn>
    </div>
  </section>
</template>

<script setup lang="ts">
import Btn from '@/components/ui/Btn.vue'
import { reactive, ref, onMounted, nextTick } from 'vue'
import CitySelector from '@/components/CitySelector.vue'
import LeafletCoordinatePicker from '@/components/LeafletCoordinatePicker.vue'
import { usePetStore } from '@/stores/pet'
import { lostPetPostStatusOptions, isDisplayableImageUrl } from '@/utils/lostPetPost'
import type { LostPetPostResponseDto, LostPetPostStatusValue } from '@/api/pet'

const props = defineProps<{
  /** null＝新增模式；帶入現有貼文＝編輯模式，欄位用它的值預填 */
  post: LostPetPostResponseDto | null
}>()

const emit = defineEmits<{
  /**
   * 新增／編輯成功後觸發。PUT 端點只回傳 204 No Content，編輯情境沒有「更新後的完整 DTO」
   * 可以帶出來，所以這裡不強行塞一個值進來源——呼叫端收到這個事件後自己決定要不要重新查詢，
   * 三個呼叫端（列表頁重查清單、詳情頁重查單筆、管理頁重查清單）本來就各自需要不同的重查方式，
   * 硬要統一成同一個 payload 反而綁死用法
   */
  saved: []
  cancel: []
}>()

const store = usePetStore()
const editableStatusOptions = lostPetPostStatusOptions.filter(
  (o): o is { value: LostPetPostStatusValue; label: string } => o.value !== ''
)

interface FormState {
  title: string
  description: string
  county: string
  phone: string
  email: string
  photoUrl: string
  latitude: number | null
  longitude: number | null
  status: LostPetPostStatusValue
}

function toFormState(post: LostPetPostResponseDto | null): FormState {
  if (post == null) {
    return {
      title: '', description: '', county: '', phone: '', email: '', photoUrl: '',
      latitude: null, longitude: null, status: 'Searching',
    }
  }
  return {
    title: post.title, description: post.description, county: post.county,
    phone: post.phone, email: post.email, photoUrl: post.photoUrl,
    latitude: post.latitude, longitude: post.longitude, status: post.status,
  }
}

// 直接用 props.post 的當下值初始化即可，不需要 watch 同步：這個元件的呼叫端慣例是「開啟表單時
// 掛載一個新的元件實例、關閉時整個卸載」（v-if 控制，不是常駐元件切換 post prop），
// 所以每次看到的都是一個全新實例，不會有「同一個實例、post prop 中途換掉」需要響應的情境
const form = reactive<FormState>(toFormState(props.post))
const formError = ref('')
const panelRef = ref<HTMLElement | null>(null)

onMounted(() => {
  nextTick(() => panelRef.value?.scrollIntoView({ behavior: 'smooth', block: 'start' }))
})

async function handleSubmit() {
  formError.value = ''
  store.saveLostPetPostError = null

  if (!form.title.trim() || !form.description.trim()) {
    formError.value = '標題與描述為必填欄位'
    return
  }
  if (!form.phone.trim() && !form.email.trim()) {
    formError.value = '電話與 Email 至少填一項，才能讓拾獲者聯絡到你'
    return
  }
  // 這個欄位的語意就是「圖片網址」，存進非網址的字串沒有任何用途（渲染端一定會忽略它），
  // 與其讓使用者以為存好了，不如在送出當下就講清楚
  if (form.photoUrl.trim() && !isDisplayableImageUrl(form.photoUrl.trim())) {
    formError.value = '照片連結必須是完整網址，請以 http:// 或 https:// 開頭'
    return
  }

  const payload = {
    title: form.title,
    description: form.description,
    county: form.county || undefined,
    phone: form.phone || undefined,
    email: form.email || undefined,
    photoUrl: form.photoUrl || undefined,
    latitude: form.latitude,
    longitude: form.longitude,
  }

  if (props.post == null) {
    const created = await store.createLostPetPost(payload)
    if (created) emit('saved')
  } else {
    const success = await store.updateLostPetPost(props.post.id, { ...payload, status: form.status })
    if (success) emit('saved')
  }
}

defineExpose({ panelRef })
</script>

<style scoped>
.post-form-panel {
  background: var(--surface); border: 1px solid var(--border); border-radius: 14px;
  padding: 24px 28px; margin-bottom: 24px;
  box-shadow: 0 2px 8px rgba(0,0,0,0.06);
  display: flex; flex-direction: column; gap: 16px;
}
.form-title { font-size: 15px; font-weight: 700; color: var(--text-primary); }

.form-grid {
  display: grid; grid-template-columns: repeat(2, 1fr); gap: 16px 20px;
}

.field-group { display: flex; flex-direction: column; gap: 6px; }
.field-group.span-2 { grid-column: span 2; }
.field-label {
  font-size: 12px; color: var(--text-muted); font-weight: 600;
  letter-spacing: 0.05em; text-transform: uppercase;
}

.field-input, .field-select, .field-textarea {
  padding: 8px 14px; border: 1px solid var(--border); border-radius: 8px;
  background: var(--surface); color: var(--text-primary); font-size: 14px;
  font-family: inherit; transition: border-color 0.18s, box-shadow 0.18s;
}
.field-textarea { resize: vertical; min-height: 72px; }
.field-input:focus, .field-select:focus, .field-textarea:focus {
  outline: none; border-color: var(--green); box-shadow: 0 0 0 3px rgba(46,125,50,0.12);
}
.field-select { cursor: pointer; }
.field-hint { font-size: 11.5px; color: var(--text-muted); line-height: 1.5; }

.form-actions { display: flex; gap: 10px; }
.error-msg { font-size: 13px; color: var(--red); font-weight: 600; }
</style>
