<!-- src/views/weather/PestAlertsView.vue -->
<template>
  <div class="page pest-alerts-view">
    <MapLayout
      title="病蟲害警報"
      title-en="PEST ALERTS"
      subtitle="農業部發布的病蟲害警報全文，點地圖上的縣市燈號可篩選右側清單"
      map-label="病蟲害警報地圖"
      list-label="警報清單"
    >
      <!-- 縣市下拉留著當地圖點選以外的備援管道：滑鼠點小圓點對觸控／鍵盤使用者不友善 -->
      <template #actions>
        <CitySelector v-model="selectedCity" include-all />
        <Btn variant="secondary" @click="clearCity">全台</Btn>
      </template>

      <template v-if="selectedCity" #filterSummary>
        <span class="filter-pill">
          目前篩選：{{ selectedCity }}
          <button class="filter-pill__clear" type="button" @click="clearCity" aria-label="清除篩選">✕</button>
        </span>
      </template>

      <template #map>
        <div ref="mapContainer" class="map-container" />
      </template>

      <template #legend>
        <!-- 三級的顏色彼此對比不夠，點徑（5/7/9px）與 lv3 的外環才是真正分得開等級的線索，
             這裡的圖例色點跟地圖上的畫法完全一致，不是另外簡化過的示意 -->
        <button
          v-for="lv in LEVELS"
          :key="lv.level"
          type="button"
          class="legend-item"
          :class="{ 'is-off': !visibleLevels.has(lv.level) }"
          @click="toggleLevel(lv.level)"
        >
          <span class="legend-dot" :class="`legend-dot--lv${lv.level}`" />
          {{ lv.label }}（{{ lv.range }}）
        </button>
      </template>

      <template #list>
        <StateBlock v-if="isLoading" state="loading" message="資料載入中..." />
        <StateBlock
          v-else-if="errorMsg"
          state="error"
          :message="errorMsg"
          retryable
          @retry="fetchAlerts"
        />
        <StateBlock
          v-else-if="alerts.length === 0"
          state="empty"
          message="查無警報資料"
          hint="這個縣市目前沒有生效中的病蟲害警報，可切換縣市或改看全台"
        />

        <div v-else>
          <!-- 警報卡片牆 -->
          <div class="alert-list">
            <div
              class="alert-card"
              v-for="a in alerts"
              :key="a.id"
              :class="{ expanded: expandedId === a.id }"
              @click="toggleExpand(a.id)"
            >
              <!-- 卡片標頭 -->
              <div class="card-top">
                <div class="card-meta">
                  <span class="pub-date">{{ a.pubDate.slice(0, 10) }}</span>
                  <span v-if="a.issue" class="issue-badge">{{ a.issue }}</span>
                </div>
                <span class="expand-icon mdi"
                  :class="expandedId === a.id ? 'mdi-chevron-up' : 'mdi-chevron-down'"
                />
              </div>

              <div class="card-subject">{{ a.subject }}</div>

              <!-- 標籤列 -->
              <div class="tag-row">
                <span
                  class="badge tag city-tag"
                  v-for="c in a.cities"
                  :key="c"
                >{{ c }}</span>
                <span
                  class="badge tag crop-tag"
                  v-for="c in a.crops"
                  :key="c"
                >{{ c }}</span>
              </div>

              <!-- 展開內容 -->
              <div class="card-body" v-if="expandedId === a.id">
                <div class="section-label">警報內文</div>
                <p class="body-text">{{ a.body }}</p>

                <template v-if="a.prescription">
                  <div class="section-label prescription">防治處方</div>
                  <p class="body-text">{{ a.prescription }}</p>
                </template>
              </div>
            </div>
          </div>

          <!-- 分頁控制：沿用 usePagination 共用邏輯 + PagerBar 共用元件。
               只要有結果就顯示，讓「每頁筆數」下拉一直可用（即使只有一頁） -->
          <PagerBar
            v-if="alertsPage && alertsPage.totalCount > 0"
            :current-page="currentPage"
            :total-pages="alertsPage.totalPages"
            :total-count="alertsPage.totalCount"
            :visible-pages="visiblePages"
            :jump-page-input="jumpPageInput"
            :page-size="pageSize"
            :page-size-options="pageSizeOptions"
            @change="changePage"
            @update:jump-page-input="jumpPageInput = $event"
            @update:page-size="setPageSize"
            @jump="handleJumpPage"
          />
        </div>
      </template>
    </MapLayout>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted, onUnmounted } from 'vue'
import L from 'leaflet'
import { weatherApi, type PestAlertResponseDto, type PagedResult } from '@/api/weather'
import CitySelector from '@/components/CitySelector.vue'
import PagerBar from '@/components/PagerBar.vue'
import { usePagination } from '@/composables/usePagination'
import MapLayout from '@/components/layouts/MapLayout.vue'
import StateBlock from '@/components/ui/StateBlock.vue'
import Btn from '@/components/ui/Btn.vue'

const selectedCity = ref('')
const alertsPage   = ref<PagedResult<PestAlertResponseDto> | null>(null)
const isLoading    = ref(false)
const errorMsg     = ref('')
const expandedId   = ref<number | null>(null)

// template 沿用 alerts 這個名稱迭代卡片，改為由分頁結果投影
const alerts = computed(() => alertsPage.value?.items ?? [])

const {
  currentPage,
  pageSize,
  pageSizeOptions,
  jumpPageInput,
  visiblePages,
  changePage,
  handleJumpPage,
  setPageSize,
} = usePagination({
  storageKey: 'pestAlerts.pageSize',
  // 每頁筆數選項（owner 2026-09-04）：預設 10，把「一頁太長」直接砍半。
  // 這一頁是伺服器分頁（getPestAlerts 收 page/pageSize），改每頁筆數會重打 API 拿新一頁。
  pageSizeOptions: [5, 10, 20, 50],
  defaultPageSize: 10,
  totalPages: () => alertsPage.value?.totalPages,
  onChange: fetchAlerts,
})

function toggleExpand(id: number) {
  expandedId.value = expandedId.value === id ? null : id
}

function clearCity() {
  selectedCity.value = ''
  currentPage.value = 1
  fetchAlerts()
}

async function fetchAlerts() {
  isLoading.value = true
  errorMsg.value = ''
  alertsPage.value = null
  expandedId.value = null
  try {
    alertsPage.value = await weatherApi.getPestAlerts(
      selectedCity.value ? toBackendCityName(selectedCity.value) : undefined,
      currentPage.value,
      pageSize.value
    )
  } catch {
    errorMsg.value = '載入失敗，請稍後再試'
  } finally {
    isLoading.value = false
  }
}

// 切換城市時回到第一頁重查
watch(selectedCity, () => {
  currentPage.value = 1
  fetchAlerts()
})

// ── 地圖：縣市燈號 ───────────────────────────────────────────────────────
// DTO 只有 cities: string[]（這則警報影響哪些縣市），沒有現成的「嚴重度」欄位，
// 後端也沒有「依縣市統計筆數」的端點（style tile 決策：不改後端）。
// 燈號的等級用「目前生效中、影響該縣市的警報則數」當代理指標——
// 這是前端自己定的分級，不是後端給的語意，量級門檻見 LEVELS。
const TAIWAN_CENTER: L.LatLngTuple = [23.7, 121.0]
const DEFAULT_ZOOM = 7

/**
 * 22 縣市的概略中心座標（縣市治所或行政區重心，非精確幾何中心，燈號地圖不需要
 * 測量級精度）。key 一律用「臺」的正式寫法，跟 CitySelector 的縣市清單同一套；
 * ⚠ 農業部警報資料實測是用「台」（台中市／台南市／台東縣），兩者要先正規化
 * 再查表，否則這三個縣市的燈號會直接消失、而且不會噴錯——查表 miss 只是回 undefined。
 */
const CITY_COORDS: Record<string, L.LatLngTuple> = {
  '臺北市': [25.0478, 121.5319],
  '新北市': [25.0170, 121.4627],
  '桃園市': [24.9936, 121.3010],
  '臺中市': [24.1477, 120.6736],
  '臺南市': [22.9999, 120.2269],
  '高雄市': [22.6273, 120.3014],
  '基隆市': [25.1276, 121.7392],
  '新竹市': [24.8138, 120.9675],
  '嘉義市': [23.4801, 120.4491],
  '新竹縣': [24.8387, 121.0177],
  '苗栗縣': [24.5602, 120.8214],
  '彰化縣': [24.0518, 120.5161],
  '南投縣': [23.9157, 120.6870],
  '雲林縣': [23.7092, 120.4313],
  '嘉義縣': [23.4518, 120.2555],
  '屏東縣': [22.5519, 120.5487],
  '宜蘭縣': [24.7021, 121.7377],
  '花蓮縣': [23.9871, 121.6015],
  '臺東縣': [22.7583, 121.1444],
  '澎湖縣': [23.5711, 119.5793],
  '金門縣': [24.4491, 118.3767],
  '連江縣': [26.1608, 119.9297],
}

/** 「台」是「臺」的簡寫，農業部資料兩種都會出現，查表前先統一 */
function normalizeCity(city: string): string {
  return city.replace(/^台/, '臺')
}

/**
 * ⚠ 這裡查出一個跟地圖無關、原本就存在的資料問題：後端 `GetPestAlertsByCityAsync`
 * 是 `CityName == cityName` 精確比對（PestService.cs），而農業部原始資料裡
 * 臺中／臺南／臺東這三個縣市實際存的是「台」的簡寫，不是 CitySelector 選單用的
 * 正式寫法「臺」。所以只要選這三個縣市查詢，後端永遠回傳 0 筆——這個
 * bug 在地圖加進來之前就存在，只是沒人選過這三個縣市去查才沒被發現。
 * 顯示（下拉選單、篩選字條、地圖提示）一律維持「臺」的正式寫法，
 * 只在送出查詢參數的這一刻換成「台」，兩邊各自的正確用途不用互相遷就。
 */
function toBackendCityName(city: string): string {
  return city.replace(/^臺/, '台')
}

type Level = 1 | 2 | 3
const LEVELS: { level: Level; label: string; range: string }[] = [
  { level: 1, label: '較少', range: '1–3 則' },
  { level: 2, label: '中等', range: '4–8 則' },
  { level: 3, label: '較多', range: '9 則以上' },
]
function levelOf(count: number): Level {
  if (count >= 9) return 3
  if (count >= 4) return 2
  return 1
}

const visibleLevels = ref<Set<Level>>(new Set([1, 2, 3]))
function toggleLevel(lv: Level) {
  const next = new Set(visibleLevels.value)
  if (next.has(lv)) next.delete(lv); else next.add(lv)
  visibleLevels.value = next
}

// 縣市燈號要看「全部生效中的警報」，跟右側清單目前選了哪個縣市、第幾頁無關，
// 所以獨立打一次不篩城市、大 pageSize 的請求，只在掛載時抓一次
const cityCounts = ref<Record<string, number>>({})
async function fetchCityCounts() {
  try {
    const all = await weatherApi.getPestAlerts(undefined, 1, 500)
    const counts: Record<string, number> = {}
    for (const item of all.items) {
      for (const raw of item.cities) {
        const city = normalizeCity(raw)
        counts[city] = (counts[city] ?? 0) + 1
      }
    }
    cityCounts.value = counts
  } catch {
    // 燈號讀不到不影響清單本身能不能用，安靜失敗即可，不用另開一個錯誤狀態
    cityCounts.value = {}
  }
}

const mapContainer = ref<HTMLElement | null>(null)
let map: L.Map | null = null
let levelLayers: Record<Level, L.LayerGroup> | null = null
let highlightMarker: L.CircleMarker | null = null

const LEVEL_RADIUS: Record<Level, number> = { 1: 5, 2: 7, 3: 9 }

function levelToken(lv: Level): string {
  return getComputedStyle(document.documentElement).getPropertyValue(`--color-lv${lv}`).trim()
}

/** 篩選條件或資料變動時，清空重建三個等級的圖層 */
function rebuildMarkers() {
  if (!map || !levelLayers) return
  for (const lv of [1, 2, 3] as Level[]) levelLayers[lv].clearLayers()

  for (const [city, coord] of Object.entries(CITY_COORDS)) {
    const count = cityCounts.value[city]
    if (!count) continue   // 這個縣市目前沒有生效中的警報，不畫點

    const lv = levelOf(count)
    const color = levelToken(lv)

    const dot = L.circleMarker(coord, {
      radius: LEVEL_RADIUS[lv],
      color,
      fillColor: color,
      fillOpacity: 0.85,
      weight: 1.5,
    })
    dot.bindTooltip(`${city}・生效中警報 ${count} 則`, { direction: 'top' })
    dot.on('click', () => {
      selectedCity.value = city
      currentPage.value = 1
      fetchAlerts()
    })
    levelLayers[lv].addLayer(dot)

    // lv3 多一圈外環：同一個點位再疊一個更大、不填色的圓，跟顏色差距不夠時
    // 靠這一圈額外的線索把「較多」跟「中等」分開，不能只靠顏色深淺
    if (lv === 3) {
      const ring = L.circleMarker(coord, {
        radius: LEVEL_RADIUS[3] + 4,
        color,
        fillOpacity: 0,
        weight: 1.5,
        opacity: 0.5,
        interactive: false,
      })
      levelLayers[lv].addLayer(ring)
    }
  }

  applyLevelVisibility()
  highlightSelected()
}

/** 圖例點擊只切換圖層的顯示與否，不重建點——資料沒變，沒必要重算 */
function applyLevelVisibility() {
  if (!map || !levelLayers) return
  for (const lv of [1, 2, 3] as Level[]) {
    const layer = levelLayers[lv]
    const shouldShow = visibleLevels.value.has(lv)
    const isShown = map.hasLayer(layer)
    if (shouldShow && !isShown) layer.addTo(map)
    if (!shouldShow && isShown) map.removeLayer(layer)
  }
}

/** 目前篩選的縣市在地圖上加一圈提示，讓「清單在看哪個縣市」跟地圖對得起來 */
function highlightSelected() {
  if (!map) return
  if (highlightMarker) { map.removeLayer(highlightMarker); highlightMarker = null }
  const coord = selectedCity.value ? CITY_COORDS[normalizeCity(selectedCity.value)] : undefined
  if (!coord) return
  highlightMarker = L.circleMarker(coord, {
    radius: 15,
    color: getComputedStyle(document.documentElement).getPropertyValue('--color-action').trim(),
    fillOpacity: 0,
    weight: 2,
    dashArray: '3 3',
    interactive: false,
  }).addTo(map)
}

onMounted(async () => {
  if (mapContainer.value) {
    map = L.map(mapContainer.value, { scrollWheelZoom: false }).setView(TAIWAN_CENTER, DEFAULT_ZOOM)
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      attribution: '&copy; OpenStreetMap contributors',
      maxZoom: 19,
    }).addTo(map)
    levelLayers = { 1: L.layerGroup(), 2: L.layerGroup(), 3: L.layerGroup() }
  }

  await fetchCityCounts()
  rebuildMarkers()
  await fetchAlerts()
})

watch(cityCounts, rebuildMarkers)
watch(visibleLevels, applyLevelVisibility)
watch(selectedCity, highlightSelected)

onUnmounted(() => {
  map?.remove()
  map = null
  levelLayers = null
  highlightMarker = null
})
</script>

<style scoped>
.map-container {
  height: 100%;
  width: 100%;
}

/* 圖例：可點的切換鈕，不是純展示——關掉整個等級是這裡的互動重點 */
.legend-item {
  display: inline-flex; align-items: center; gap: var(--space-2);
  background: none; border: none; cursor: pointer; padding: 0;
  font-size: var(--text-sm); color: var(--color-text-dim); font-weight: var(--weight-medium);
  transition: opacity var(--duration-fast);
}
.legend-item.is-off { opacity: 0.4; }
.legend-dot { width: 10px; height: 10px; border-radius: 50%; flex-shrink: 0; }
.legend-dot--lv1 { background: var(--color-lv1); }
.legend-dot--lv2 { background: var(--color-lv2); }
.legend-dot--lv3 { background: var(--color-lv3); }

.filter-pill {
  display: inline-flex; align-items: center; gap: var(--space-2);
  background: var(--seed-100); color: var(--color-action);
  border-radius: var(--radius-full); padding: var(--space-1) var(--space-2) var(--space-1) var(--space-4);
  font-size: var(--text-sm); font-weight: var(--weight-medium);
}
.filter-pill__clear {
  display: inline-flex; align-items: center; justify-content: center;
  width: 20px; height: 20px; border-radius: 50%; border: none; cursor: pointer;
  background: transparent; color: inherit; font-size: var(--text-xs);
}
.filter-pill__clear:hover { background: rgb(0 0 0 / 0.08); }

.pest-alerts-view { min-width: 960px; }
.alert-list { display: flex; flex-direction: column; gap: var(--space-3); margin-bottom: var(--space-6); }

/* 卡片不給陰影（style tile §三），hover 只換邊框顏色 */
.alert-card {
  background: var(--color-surface); border: var(--border-width) solid var(--color-border);
  border-radius: var(--radius-lg); padding: var(--space-5) var(--space-6); cursor: pointer;
  transition: border-color var(--duration-fast) var(--ease-work), background var(--duration-fast) var(--ease-work);
}
.alert-card:hover { border-color: var(--color-border-strong); }
.alert-card.expanded { border-color: var(--color-action); background: var(--color-action-soft); }

.card-top { display: flex; justify-content: space-between; align-items: center; margin-bottom: var(--space-2); }
.card-meta { display: flex; align-items: center; gap: var(--space-3); }

/* 日期 */
.pub-date {
  font-size: var(--text-sm);
  color: var(--color-text-dim);
  font-variant-numeric: tabular-nums;
  font-weight: var(--weight-medium);
}
/* issue badge */
.issue-badge {
  font-size: var(--text-xs);
  padding: var(--space-1) var(--space-3);
  border-radius: var(--radius-full);
  background: var(--seed-100); color: var(--color-action);
  border: 1px solid var(--seed-200);
  font-weight: var(--weight-bold);
}

.expand-icon { font-size: var(--text-lg); color: var(--color-text-dim); transition: color var(--duration-fast); }
.alert-card:hover .expand-icon { color: var(--color-text); }

/* 主旨標題 */
.card-subject {
  font-size: var(--text-lg);
  font-weight: var(--weight-bold);
  color: var(--color-text);
  margin-bottom: var(--space-3);
  line-height: var(--leading-normal);
}

.tag-row { display: flex; flex-wrap: wrap; gap: var(--space-2); }
/* 標籤外殼已收進 base.css 的 .badge，這裡只留語意色 */
.tag { border: var(--border-width) solid; }
.city-tag { background: var(--info-50); border-color: var(--info-100); color: var(--info-500); }
.crop-tag { background: var(--seed-100); border-color: var(--seed-200); color: var(--color-action); }


.card-body { margin-top: var(--space-5); padding-top: var(--space-5); border-top: 1px solid var(--color-border); }

/* section 標籤 */
.section-label {
  font-size: var(--text-lg);
  font-weight: var(--weight-bold);
  color: var(--color-action);
  letter-spacing: 0.08em;
  text-transform: uppercase;
  margin-bottom: var(--space-3);
  padding-bottom: var(--space-2);
  border-bottom: 2px solid var(--seed-200);
  display: block;
}
.section-label.prescription {
  color: var(--warning-700);
  margin-top: var(--space-5);
  border-bottom-color: var(--warning-100);
}

/* 內文 */
.body-text {
  font-size: var(--text-base);
  color: var(--color-text);
  line-height: var(--leading-loose);
  white-space: pre-wrap;
  margin: 0;
}

/* 分頁列的樣式由 PagerBar 元件自帶（scoped），此處不再重複一份 */
</style>
