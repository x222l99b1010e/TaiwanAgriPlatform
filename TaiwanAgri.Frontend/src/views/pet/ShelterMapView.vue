<template>
  <div class="page shelter-map-view">
    <PageHeader title="收容動物地圖" title-en="SHELTER MAP">
      <template #subtitle>
        全台收容所在養動物。一個標記代表一間收容所，點開可看該所目前的在養動物；
        相鄰的收容所會自動聚合成數字圓圈，拉近後展開
      </template>
    </PageHeader>

    <!-- ⚠ 這一頁刻意不套 MapLayout。那個樣板的形狀是「左地圖、右清單」，
         而這一頁沒有清單——每一間收容所的內容在 popup 與獨立的詳情頁裡，
         右欄會是一整塊空白。樣板是為了讓「同一種頁面長一樣」，
         不是為了讓每一頁都套到一個。共用的是 token 與元件，不是版型。 -->
    <FilterCard>
      <CitySelector v-model="selectedCounty" include-all />

      <div class="field-group">
        <label class="field-label" for="kind-select">動物種類</label>
        <select id="kind-select" class="form-control kind-select" v-model="selectedKind">
          <option v-for="opt in kindOptions" :key="opt.value" :value="opt.value">{{ opt.label }}</option>
        </select>
      </div>

      <div class="stat-bar">
        <span v-if="isLoading" class="stat-loading">
          <span class="loading-spinner-sm" />載入中...
        </span>
        <span v-else-if="errorMsg" class="stat-error">{{ errorMsg }}</span>
        <!--
          改用聚合端點後結果集本身只有約 30 筆，不會再撞上限，不需要截斷提示。
          叢集圓圈只統計「目前可視範圍」內的標記（MarkerCluster 預設不渲染視野外的標記），
          跟這裡的總數本來就不會相等，這點仍要說出來否則使用者會互相對不上。
        -->
        <span v-else class="stat-text">
          符合條件 <b class="stat-num">{{ totalAnimalCount }}</b> 筆
          <span class="stat-muted">
            ｜分佈於 {{ shelterMarkerCount }} 間收容所<template
              v-if="noCoordinateCount > 0">，另 {{ noCoordinateCount }} 間收容所座標未知</template>
            。地圖上一個標記代表一間收容所，點開可看該所的動物
          </span>
        </span>
      </div>
    </FilterCard>

    <!-- 地圖本體：Leaflet 需要一個有明確高度的容器 DOM 元素才能掛載 -->
    <div ref="mapContainer" class="map-container" />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch } from 'vue'
import { useRouter } from 'vue-router'
import L from 'leaflet'
import 'leaflet.markercluster'
import CitySelector from '@/components/CitySelector.vue'
import PageHeader from '@/components/ui/PageHeader.vue'
import FilterCard from '@/components/ui/FilterCard.vue'
import { usePetStore } from '@/stores/pet'
import { animalKindLabel } from '@/utils/shelterAnimal'
import type { AnimalKind, ShelterAnimalSummaryDto } from '@/api/pet'

const router = useRouter()

const store = usePetStore()

// ─── 篩選狀態 ───────────────────────────────────────────────────────────
// '' 代表「全部」，送給後端時要轉成 undefined（QueryDto 的 County/Kind 都是選填）
const selectedCounty = ref('')
const selectedKind = ref<AnimalKind | ''>('')

const kindOptions: { value: AnimalKind | ''; label: string }[] = [
  { value: '',      label: '全部種類' },
  { value: 'Dog',   label: '狗' },
  { value: 'Cat',   label: '貓' },
  { value: 'Other', label: '其他' },
]

const isLoading = computed(() => store.isLoadingShelterAnimalSummary)
const errorMsg = computed(() => store.shelterAnimalSummaryError)

// 座標未知的收容所筆數（Shelter.Latitude/Longitude 是 decimal?，序列化後可能是 null）。
// 語意跟改版前不同：舊版數的是「動物筆數」，聚合後計量單位變成收容所本身，數的是「收容所筆數」
const noCoordinateCount = computed(
  () => store.shelterAnimalSummary.filter(s => s.latitude == null || s.longitude == null).length
)

// 有座標、真正畫得出標記的收容所筆數——聚合端點已經是「一間收容所一筆」，不必再靠座標去重
const shelterMarkerCount = computed(
  () => store.shelterAnimalSummary.filter(s => s.latitude != null && s.longitude != null).length
)

// 符合條件的動物總數：聚合端點不再逐隻回傳動物，靠各收容所 totalCount 加總取代
const totalAnimalCount = computed(
  () => store.shelterAnimalSummary.reduce((sum, s) => sum + s.totalCount, 0)
)

function fetchWithCurrentFilters() {
  store.fetchShelterAnimalSummary({
    county: selectedCounty.value || undefined,
    kind: selectedKind.value || undefined,
  })
}

// ─── Leaflet 地圖 ───────────────────────────────────────────────────────

const TAIWAN_CENTER: L.LatLngTuple = [23.6978, 120.9605]
const DEFAULT_ZOOM = 8

const mapContainer = ref<HTMLElement | null>(null)
let map: L.Map | null = null
let clusterGroup: L.MarkerClusterGroup | null = null

/**
 * 用 DOM API 組 popup 內容，不用字串拼 innerHTML——資料來自政府開放資料，
 * 文字內容不可信任，textContent 賦值天生會做 HTML escape，不會有注入疑慮。
 *
 * 聚合端點改版：資料到手時已經是「一間收容所一筆、內含 Dog/Cat/Other 拆分計數」，
 * 不需要再像舊版那樣拿到逐隻動物清單後在前端分組、迴圈算數量——這裡只負責組字串。
 * popup 只放摘要＋「查看全部→」連結，完整動物清單交給獨立的收容所詳情頁用 PagerBar 呈現
 * （popup 回歸「快速預覽」定位，這個設計從不掛週次分支改版沿用至今）。
 */
function buildShelterPopupContent(summary: ShelterAnimalSummaryDto): HTMLElement {
  const root = document.createElement('div')
  root.className = 'shelter-popup-content'

  const title = document.createElement('div')
  title.className = 'popup-title'
  title.textContent = `${summary.shelterName}（${summary.county}）`
  root.appendChild(title)

  const address = document.createElement('div')
  address.className = 'popup-address'
  address.textContent = summary.shelterAddress
  root.appendChild(address)

  // 摘要：總數與種類分佈，這是「一間收容所一個標記」之後最先要回答的問題
  const summaryEl = document.createElement('div')
  summaryEl.className = 'popup-summary'
  const parts = [`共 ${summary.totalCount} 隻`]
  if (summary.dogCount) parts.push(`${animalKindLabel('Dog')} ${summary.dogCount}`)
  if (summary.catCount) parts.push(`${animalKindLabel('Cat')} ${summary.catCount}`)
  if (summary.otherCount) parts.push(`${animalKindLabel('Other')} ${summary.otherCount}`)
  summaryEl.textContent = parts.join('・')
  root.appendChild(summaryEl)

  // href 保留正常連結行為（可右鍵開新分頁／可被爬蟲或無障礙工具讀到是個連結），
  // click 再攔截改用 router.push 做 SPA 內部導頁，避免整頁重新載入
  const viewAllLink = document.createElement('a')
  viewAllLink.href = `/pet/shelter-map/${summary.shelterPkId}`
  viewAllLink.className = 'popup-view-all'
  viewAllLink.textContent = `查看全部 ${summary.totalCount} 隻 →`
  viewAllLink.addEventListener('click', (e) => {
    e.preventDefault()
    router.push(`/pet/shelter-map/${summary.shelterPkId}`)
  })
  root.appendChild(viewAllLink)

  return root
}

/** 篩選條件或資料變動時，清空重建整個 MarkerCluster 圖層（約 30 個標記，重建成本極低） */
function rebuildMarkers() {
  if (!clusterGroup || !map) return
  clusterGroup.clearLayers()

  // 座標未知的收容所無法畫標記，濾掉（noCoordinateCount 另外統計，不會因為濾掉而消失不見）
  const summaries = store.shelterAnimalSummary.filter(
    (s): s is ShelterAnimalSummaryDto & { latitude: number; longitude: number } =>
      s.latitude != null && s.longitude != null
  )

  const markers = summaries.map(summary => {
    const marker = L.marker([summary.latitude, summary.longitude])
    // popup 內容改用 function 延遲建立：只有真的打開那一間才組 DOM，
    // 不必在建標記當下就先產生約 30 份含摘要的節點
    marker.bindPopup(() => buildShelterPopupContent(summary), { maxWidth: 360 })
    // 滑過即可看到是哪一間、有幾隻，不必先點開
    marker.bindTooltip(`${summary.shelterName}・${summary.totalCount} 隻`, { direction: 'top' })
    return marker
  })

  clusterGroup.addLayers(markers)

  // 有資料時自動縮放到剛好framing所有標記；篩選到查無資料時保持原視角，不強制跳回全台
  if (markers.length > 0) {
    map.fitBounds(clusterGroup.getBounds(), { padding: [32, 32], maxZoom: 14 })
  }
}

onMounted(() => {
  if (!mapContainer.value) return

  map = L.map(mapContainer.value).setView(TAIWAN_CENTER, DEFAULT_ZOOM)

  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    attribution: '&copy; OpenStreetMap contributors',
    maxZoom: 19,
  }).addTo(map)

  // leaflet.markercluster 是 side-effect import，掛在 L 命名空間下提供 L.markerClusterGroup
  clusterGroup = L.markerClusterGroup({ maxClusterRadius: 60 })
  map.addLayer(clusterGroup)

  fetchWithCurrentFilters()
})

// 篩選條件變動 → 重新打 API；store 內部用 useLatestRequest 防競態，
// 快速切換縣市時不會被較早發出、較晚回應的舊請求蓋掉結果
watch([selectedCounty, selectedKind], fetchWithCurrentFilters)

// API 回應資料變動 → 重畫地圖標記（這條 watch 讓 Leaflet 這個「Vue 響應式系統外」的世界，
// 跟著 store 的狀態變化同步更新，兩者不會自動連動）
watch(() => store.shelterAnimalSummary, rebuildMarkers)

onUnmounted(() => {
  map?.remove()
  map = null
  clusterGroup = null
})
</script>

<style scoped>
/* 欄位外殼走 base.css 的 .field-group／.field-label／.form-control，
   顏色全部改用 semantic 層（style tile §九）。 */
.kind-select { min-width: 140px; }

/* 這一段是「目前篩出幾筆」的說明，限寬才不會在寬螢幕上拉成一整條長行 */
.stat-bar { margin-left: auto; font-size: var(--text-sm); max-width: 46ch; }
.stat-text { color: var(--color-text-dim); }
.stat-num { font-family: var(--font-num); color: var(--color-text); font-variant-numeric: tabular-nums; }
.stat-muted { color: var(--color-text-dim); }
.stat-error { color: var(--danger-700); font-weight: var(--weight-medium); }
.stat-loading { display: inline-flex; align-items: center; gap: var(--space-2); color: var(--color-text-dim); }

.loading-spinner-sm {
  width: 14px; height: 14px;
  border: 2px solid var(--seed-200); border-top-color: var(--color-action);
  border-radius: var(--radius-full); animation: spin 0.8s linear infinite;
}
@keyframes spin { to { transform: rotate(360deg); } }

/* 高度改用視窗比例，跟 MapLayout 同一條規則：固定 640px 在筆電上會佔掉整個畫面，
   而在大螢幕上又浪費下半屏。 */
.map-container {
  height: min(68vh, 680px);
  width: 100%;
  border-radius: var(--radius-lg);
  border: var(--border-width) solid var(--color-border);
  overflow: hidden;
}
</style>

<!-- 未加 scoped：Leaflet popup 內容是用 document.createElement 動態插入的 DOM，
     不是 Vue 模板渲染出來的節點，scoped 屬性不會套用到這些節點上，樣式必須寫在全域區塊 -->
<style>
.shelter-popup-content { font-size: var(--text-sm); min-width: 260px; font-family: var(--font-body); }
.shelter-popup-content .popup-title { font-weight: var(--weight-bold); font-size: var(--text-base); color: var(--color-text); margin-bottom: var(--space-1); }
.shelter-popup-content .popup-address { color: var(--color-text-dim); margin-bottom: var(--space-2); font-size: var(--text-xs); }

.shelter-popup-content .popup-summary {
  font-weight: var(--weight-bold); color: var(--color-text); padding: var(--space-2) 0;
  border-top: var(--border-width) solid var(--color-border);
  border-bottom: var(--border-width) solid var(--color-border);
}

/* popup 只放摘要＋這顆連結，完整清單在獨立詳情頁（不掛週次分支改版）。
   連結用動作色而不是藍：藍在這一版沒有「可點」的語意，動作綠才有。 */
.shelter-popup-content .popup-view-all {
  display: block; margin-top: var(--space-2);
  color: var(--color-action); font-size: var(--text-sm); font-weight: var(--weight-bold);
  text-decoration: none;
}
.shelter-popup-content .popup-view-all:hover { text-decoration: underline; }
</style>
