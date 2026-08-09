<template>
  <div class="shelter-map-view">
    <div class="page-header">
      <h2 class="section-title">收容動物地圖</h2>
      <p class="section-subtitle">
        全台收容所在養動物。一個標記代表一間收容所，點開可看該所目前的在養動物；
        相鄰的收容所會自動聚合成數字圓圈，拉近後展開
      </p>
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
        <!--
          撞到後端 2000 筆上限時「2000」看起來就像「總共只有 2000 筆」，必須講明這是被截斷的。
          指引不能只寫「用縣市篩選」——實測新北市單一縣市就已經超過 2000 筆，光篩縣市解不了，
          必須縣市與種類一起用。另外叢集圓圈只統計「目前可視範圍」內的標記（MarkerCluster 預設
          不渲染視野外的標記），跟這裡的總數本來就不會相等，這點也要說出來否則使用者會互相對不上。
        -->
        <span v-else class="stat-text">
          <template v-if="isAtMarkerLimit">
            <span class="stat-truncated">
              <span class="mdi mdi-alert-outline" />
              資料超過單次顯示上限，目前僅載入 {{ store.shelterAnimals.length }} 筆
            </span>
            <span class="stat-muted">
              ；請同時指定縣市與動物種類縮小範圍（部分縣市單獨篩選仍會超過上限）
            </span>
          </template>
          <template v-else>符合條件 {{ store.shelterAnimals.length }} 筆</template>

          <span class="stat-muted">
            ｜其中 {{ mappableCount }} 筆分佈於 {{ shelterMarkerCount }} 間收容所<template
              v-if="noCoordinateCount > 0">，另 {{ noCoordinateCount }} 筆收容所座標未知</template>
            。地圖上一個標記代表一間收容所，點開可看該所的動物
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
import { useRouter } from 'vue-router'
import L from 'leaflet'
import 'leaflet.markercluster'
import CitySelector from '@/components/CitySelector.vue'
import { usePetStore } from '@/stores/pet'
import { animalKindLabel } from '@/utils/shelterAnimal'
import type { AnimalKind, ShelterAnimalResponseDto } from '@/api/pet'

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

const isLoading = computed(() => store.isLoadingShelterAnimals)
const errorMsg = computed(() => store.shelterAnimalsError)

// 篩選後仍有多少筆「收容所座標未知」（Shelter.Latitude/Longitude 是 decimal?，序列化後可能是 null）
const noCoordinateCount = computed(
  () => store.shelterAnimals.filter(a => a.latitude == null || a.longitude == null).length
)

// 後端 PetService.MapMarkerSafetyLimit 的值。這裡是「偵測有沒有撞到上限」用的，
// 不是前端自己在截斷——回傳筆數剛好等於上限就代表資料很可能被切掉了。
// 兩邊各寫一次是重複，但 API 目前不回傳「總筆數」，前端只能靠這個訊號判斷。
// 是否被截斷由後端的 X-Result-Truncated 標頭直接告知，前端不再自行維護一份上限常數
// （原本前後端各有一個 3000，任一邊調整而另一邊忘了改，提示就會失效或誤報）
const isAtMarkerLimit = computed(() => store.shelterAnimalsTruncated)

// 真正畫得出標記的筆數＝取得筆數扣掉座標未知的，也就是所有叢集圓圈數字的總和。
// 明確算出來讓使用者可以跟畫面上的叢集數字對帳，不必自己推敲兩個數字的關係。
const mappableCount = computed(() => store.shelterAnimals.length - noCoordinateCount.value)

// 標記數＝有座標的「相異收容所」數（不是動物數）——地圖改成一間收容所一個標記後，
// 這兩個數字差距很大（例如 2000 隻動物只對應約 30 間收容所），必須分開講清楚
const shelterMarkerCount = computed(
  () => new Set(
    store.shelterAnimals
      .filter(a => a.latitude != null && a.longitude != null)
      .map(a => `${a.latitude},${a.longitude}`)
  ).size
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

/**
 * 一間收容所的動物集合。
 * 資料形狀的關鍵事實：上千筆動物其實只落在約 30 個收容所座標上（同一間收容所的所有動物
 * 共用該收容所的經緯度）。原本「一隻動物一個標記」的做法會讓臺北市動物之家這種大所
 * 產生上千個完全重疊的標記，MarkerCluster 只能把它們蜘蛛狀攤開成一團誰也點不準的針，
 * 而且點中了也沒有意義——它們全是同一間收容所。改成「一間收容所一個標記」才符合資料形狀。
 */
interface ShelterGroup {
  shelterPkId: number
  latitude: number
  longitude: number
  shelterName: string
  shelterAddress: string
  county: string
  animals: ShelterAnimalResponseDto[]
}

function groupByShelter(animals: ShelterAnimalResponseDto[]): ShelterGroup[] {
  const groups = new Map<string, ShelterGroup>()
  for (const a of animals) {
    if (a.latitude == null || a.longitude == null) continue
    // 用座標而非收容所名稱當鍵：名稱可能有全形/空白差異，座標才是標記真正的落點
    const key = `${a.latitude},${a.longitude}`
    let g = groups.get(key)
    if (!g) {
      g = {
        shelterPkId: a.shelterPkId,
        latitude: a.latitude, longitude: a.longitude,
        shelterName: a.shelterName, shelterAddress: a.shelterAddress, county: a.county,
        animals: [],
      }
      groups.set(key, g)
    }
    g.animals.push(a)
  }
  return [...groups.values()]
}

/**
 * 用 DOM API 組 popup 內容，不用字串拼 innerHTML——資料來自政府開放資料，
 * 文字內容不可信任，textContent 賦值天生會做 HTML escape，不會有注入疑慮。
 *
 * 不掛週次分支改版：popup 只放摘要＋「查看全部→」連結，原本內嵌的完整動物清單
 * （曾用 POPUP_ANIMAL_LIMIT=50 截斷）整段移除，改到獨立的收容所詳情頁用 PagerBar 呈現。
 * owner 的原話：「寫 50 隻跟寫總數沒差異，因為都只看得到前 50 隻」——調高那個數字
 * 解決不了問題，只要清單有限就一定有人撞到底，popup 應該回歸「快速預覽」定位。
 */
function buildShelterPopupContent(group: ShelterGroup): HTMLElement {
  const root = document.createElement('div')
  root.className = 'shelter-popup-content'

  const title = document.createElement('div')
  title.className = 'popup-title'
  title.textContent = `${group.shelterName}（${group.county}）`
  root.appendChild(title)

  const address = document.createElement('div')
  address.className = 'popup-address'
  address.textContent = group.shelterAddress
  root.appendChild(address)

  // 摘要：總數與種類分佈，這是「一間收容所一個標記」之後最先要回答的問題
  const counts = { Dog: 0, Cat: 0, Other: 0 } as Record<string, number>
  for (const a of group.animals) counts[a.kind] = (counts[a.kind] ?? 0) + 1

  const summary = document.createElement('div')
  summary.className = 'popup-summary'
  const parts = [`共 ${group.animals.length} 隻`]
  for (const k of ['Dog', 'Cat', 'Other']) {
    if (counts[k]) parts.push(`${animalKindLabel(k)} ${counts[k]}`)
  }
  summary.textContent = parts.join('・')
  root.appendChild(summary)

  // href 保留正常連結行為（可右鍵開新分頁／可被爬蟲或無障礙工具讀到是個連結），
  // click 再攔截改用 router.push 做 SPA 內部導頁，避免整頁重新載入
  const viewAllLink = document.createElement('a')
  viewAllLink.href = `/pet/shelter-map/${group.shelterPkId}`
  viewAllLink.className = 'popup-view-all'
  viewAllLink.textContent = `查看全部 ${group.animals.length} 隻 →`
  viewAllLink.addEventListener('click', (e) => {
    e.preventDefault()
    router.push(`/pet/shelter-map/${group.shelterPkId}`)
  })
  root.appendChild(viewAllLink)

  return root
}

/** 篩選條件或資料變動時，清空重建整個 MarkerCluster 圖層（分組後最多約 30 個標記，重建成本極低） */
function rebuildMarkers() {
  if (!clusterGroup || !map) return
  clusterGroup.clearLayers()

  const groups = groupByShelter(store.shelterAnimals)

  const markers = groups.map(group => {
    const marker = L.marker([group.latitude, group.longitude])
    // popup 內容改用 function 延遲建立：只有真的打開那一間才組 DOM，
    // 不必在建標記當下就先產生 30 份含清單的節點
    marker.bindPopup(() => buildShelterPopupContent(group), { maxWidth: 360 })
    // 滑過即可看到是哪一間、有幾隻，不必先點開
    marker.bindTooltip(`${group.shelterName}・${group.animals.length} 隻`, { direction: 'top' })
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
.stat-truncated {
  display: inline-flex; align-items: center; gap: 4px; margin-left: 8px;
  color: #e65100; font-weight: 600;
}
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
.shelter-popup-content { font-size: 13px; min-width: 260px; }
.shelter-popup-content .popup-title { font-weight: 700; font-size: 14px; color: #1b5e20; margin-bottom: 4px; }
.shelter-popup-content .popup-address { color: rgba(26,40,32,0.65); margin-bottom: 8px; font-size: 12px; }

.shelter-popup-content .popup-summary {
  font-weight: 700; color: #1a2820; padding: 6px 0;
  border-top: 1px solid rgba(26,40,32,0.12); border-bottom: 1px solid rgba(26,40,32,0.12);
}

/* popup 只放摘要＋這顆連結，完整清單在獨立詳情頁（不掛週次分支改版） */
.shelter-popup-content .popup-view-all {
  display: block; margin-top: 8px; color: #1565c0; font-size: 13px; font-weight: 700;
  text-decoration: none;
}
.shelter-popup-content .popup-view-all:hover { text-decoration: underline; }
</style>
