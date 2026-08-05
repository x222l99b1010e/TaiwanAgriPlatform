<template>
  <div class="shelter-map-view">
    <div class="page-header">
      <h2 class="section-title">收容動物地圖</h2>
      <p class="section-subtitle">全台收容所在養動物，地圖標記會依距離自動聚合成數字圓圈，拉近後自動展開</p>
    </div>

    <!-- 篩選列 -->
    <div class="filter-bar">
      <CitySelector v-model="selectedCounty" include-all />

      <div class="field-group">
        <label class="field-label">動物種類</label>
        <select class="kind-select" v-model="selectedKind">
          <option v-for="opt in kindOptions" :key="opt.value" :value="opt.value">{{ opt.label }}</option>
        </select>
      </div>

      <div class="stat-bar">
        <span v-if="isLoading" class="stat-loading">
          <span class="loading-spinner-sm" />載入中...
        </span>
        <span v-else-if="errorMsg" class="stat-error">{{ errorMsg }}</span>
        <span v-else class="stat-text">
          符合條件 {{ store.shelterAnimals.length }} 筆
          <span v-if="noCoordinateCount > 0" class="stat-muted">
            （其中 {{ noCoordinateCount }} 筆收容所座標未知，不會顯示在地圖上）
          </span>
        </span>
      </div>
    </div>

    <!-- 地圖本體：Leaflet 需要一個有明確高度的容器 DOM 元素才能掛載 -->
    <div ref="mapContainer" class="map-container" />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch } from 'vue'
import L from 'leaflet'
import 'leaflet.markercluster'
import CitySelector from '@/components/CitySelector.vue'
import { usePetStore } from '@/stores/pet'
import type { AnimalKind, ShelterAnimalResponseDto } from '@/api/pet'

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

const isLoading = computed(() => store.isLoadingShelterAnimals)
const errorMsg = computed(() => store.shelterAnimalsError)

// 篩選後仍有多少筆「收容所座標未知」（Shelter.Latitude/Longitude 是 decimal?，序列化後可能是 null）
const noCoordinateCount = computed(
  () => store.shelterAnimals.filter(a => a.latitude == null || a.longitude == null).length
)

function fetchWithCurrentFilters() {
  store.fetchShelterAnimals({
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

/** 用 DOM API 組 popup 內容，不用字串拼 innerHTML——資料來自政府開放資料，
 *  文字內容不可信任，textContent 賦值天生會做 HTML escape，不會有注入疑慮 */
function buildPopupContent(animal: ShelterAnimalResponseDto): HTMLElement {
  const root = document.createElement('div')
  root.className = 'shelter-popup-content'

  const title = document.createElement('div')
  title.className = 'popup-title'
  title.textContent = `${animal.shelterName}（${animal.county}）`
  root.appendChild(title)

  const address = document.createElement('div')
  address.className = 'popup-address'
  address.textContent = animal.shelterAddress
  root.appendChild(address)

  const rows: [string, string][] = [
    ['編號',   animal.animalSubId],
    ['種類',   kindLabel(animal.kind)],
    ['性別',   sexLabel(animal.sex)],
    ['體型',   animal.bodyType],
    ['年紀',   animal.age],
    ['品種',   animal.variety || '未提供'],
    ['毛色',   animal.colour || '未提供'],
    ['拾獲地點', animal.foundPlace || '未提供'],
  ]
  const table = document.createElement('table')
  table.className = 'popup-table'
  for (const [label, value] of rows) {
    const tr = document.createElement('tr')
    const th = document.createElement('th')
    th.textContent = label
    const td = document.createElement('td')
    td.textContent = value
    tr.append(th, td)
    table.appendChild(tr)
  }
  root.appendChild(table)

  if (/^https?:\/\//i.test(animal.albumFile)) {
    const link = document.createElement('a')
    link.href = animal.albumFile
    link.target = '_blank'
    link.rel = 'noopener noreferrer'
    link.textContent = '查看照片相簿 →'
    link.className = 'popup-link'
    root.appendChild(link)
  }

  return root
}

function kindLabel(kind: string): string {
  return { Dog: '狗', Cat: '貓', Other: '其他' }[kind] ?? kind
}
function sexLabel(sex: string): string {
  return { Male: '公', Female: '母', Other: '其他', Unknown: '不明' }[sex] ?? sex
}

/** 篩選條件或資料變動時，清空重建整個 MarkerCluster 圖層（資料量最多 2000 筆，重建成本不高） */
function rebuildMarkers() {
  if (!clusterGroup || !map) return
  clusterGroup.clearLayers()

  const withCoords = store.shelterAnimals.filter(
    (a): a is ShelterAnimalResponseDto & { latitude: number; longitude: number } =>
      a.latitude != null && a.longitude != null
  )

  const markers = withCoords.map(animal => {
    const marker = L.marker([animal.latitude, animal.longitude])
    marker.bindPopup(buildPopupContent(animal))
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
watch(() => store.shelterAnimals, rebuildMarkers)

onUnmounted(() => {
  map?.remove()
  map = null
  clusterGroup = null
})
</script>

<style scoped>
.shelter-map-view {
  padding: 36px 56px;
  width: 100%;
  box-sizing: border-box;
}

.page-header { margin-bottom: 20px; }
.section-title { font-size: 22px; font-weight: 700; color: var(--text-primary); margin-bottom: 6px; }
.section-subtitle { font-size: 13px; color: var(--text-muted); }

.filter-bar {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-end;
  gap: 20px;
  margin-bottom: 20px;
  padding: 20px 24px;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 14px;
  box-shadow: 0 2px 8px rgba(0,0,0,0.06);
}

.field-group { display: flex; flex-direction: column; gap: 6px; }
.field-label {
  font-size: 12px; color: var(--text-muted); font-weight: 600;
  letter-spacing: 0.05em; text-transform: uppercase;
}

.kind-select {
  padding: 8px 14px; border: 1px solid var(--border); border-radius: 8px;
  background: var(--surface); color: var(--text-primary); font-size: 14px;
  min-width: 140px; cursor: pointer;
}
.kind-select:focus { outline: none; border-color: var(--green); box-shadow: 0 0 0 3px rgba(46,125,50,0.12); }

.stat-bar { margin-left: auto; font-size: 13px; }
.stat-text { color: var(--text-secondary); }
.stat-muted { color: var(--text-muted); }
.stat-error { color: var(--red); font-weight: 600; }
.stat-loading { display: inline-flex; align-items: center; gap: 8px; color: var(--text-muted); }

.loading-spinner-sm {
  width: 14px; height: 14px;
  border: 2px solid #c8e6c9; border-top-color: var(--green);
  border-radius: 50%; animation: spin 0.8s linear infinite;
}
@keyframes spin { to { transform: rotate(360deg); } }

.map-container {
  height: 640px;
  width: 100%;
  border-radius: 16px;
  border: 1px solid var(--border);
  box-shadow: 0 2px 8px rgba(0,0,0,0.06);
}
</style>

<!-- 未加 scoped：Leaflet popup 內容是用 document.createElement 動態插入的 DOM，
     不是 Vue 模板渲染出來的節點，scoped 屬性不會套用到這些節點上，樣式必須寫在全域區塊 -->
<style>
.shelter-popup-content { font-size: 13px; min-width: 200px; }
.shelter-popup-content .popup-title { font-weight: 700; font-size: 14px; color: #1b5e20; margin-bottom: 4px; }
.shelter-popup-content .popup-address { color: rgba(26,40,32,0.65); margin-bottom: 8px; font-size: 12px; }
.shelter-popup-content .popup-table { border-collapse: collapse; width: 100%; }
.shelter-popup-content .popup-table th {
  text-align: left; color: rgba(26,40,32,0.55); font-weight: 600;
  padding: 2px 8px 2px 0; white-space: nowrap; vertical-align: top;
}
.shelter-popup-content .popup-table td { padding: 2px 0; color: #1a2820; }
.shelter-popup-content .popup-link {
  display: inline-block; margin-top: 8px; color: #1565c0; font-size: 12px; font-weight: 600;
}
</style>
