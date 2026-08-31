<!--
  src/components/LeafletCoordinatePicker.vue
  職責：內嵌在表單裡的小地圖，點擊地圖取得座標（不做地址→座標的地理編碼，
  這是模組 3 的既有設計前提：Nominatim 對台灣地址實測不可行，見 DevLog 條目 281）

  用法（雙 v-model，父層各自綁 latitude/longitude）：
    <LeafletCoordinatePicker v-model:latitude="form.latitude" v-model:longitude="form.longitude" />
-->
<template>
  <div class="coordinate-picker">
    <div class="picker-header">
      <span class="picker-hint">
        <span class="mdi mdi-cursor-default-click-outline" />
        點擊地圖上的位置，設定走失／拾獲地點座標
      </span>
      <Btn v-if="hasCoordinate" variant="danger" size="sm" icon="mdi-close-circle-outline" @click="clearCoordinate">
        清除座標
      </Btn>
    </div>

    <div ref="mapContainer" class="map-container" />

    <p class="picker-coords">
      <span v-if="hasCoordinate">
        目前座標：{{ latitude!.toFixed(6) }}, {{ longitude!.toFixed(6) }}
      </span>
      <span v-else class="coords-empty">尚未設定座標（可略過，不是必填欄位）</span>
    </p>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted, watch, computed } from 'vue'
import L from 'leaflet'
import Btn from '@/components/ui/Btn.vue'

const props = defineProps<{
  latitude: number | null
  longitude: number | null
}>()

const emit = defineEmits<{
  'update:latitude': [value: number | null]
  'update:longitude': [value: number | null]
}>()

// 台灣地理中心，尚未有座標時的地圖預設視角
const TAIWAN_CENTER: L.LatLngTuple = [23.6978, 120.9605]
const DEFAULT_ZOOM = 7
const SELECTED_ZOOM = 13

const hasCoordinate = computed(() => props.latitude != null && props.longitude != null)

const mapContainer = ref<HTMLElement | null>(null)
let map: L.Map | null = null
let marker: L.Marker | null = null

function placeMarker(lat: number, lng: number) {
  if (!map) return
  if (marker) {
    marker.setLatLng([lat, lng])
  } else {
    marker = L.marker([lat, lng]).addTo(map)
  }
}

function removeMarker() {
  if (marker && map) {
    map.removeLayer(marker)
    marker = null
  }
}

function clearCoordinate() {
  emit('update:latitude', null)
  emit('update:longitude', null)
  removeMarker()
}

onMounted(() => {
  if (!mapContainer.value) return

  const initialCenter: L.LatLngTuple = hasCoordinate.value
    ? [props.latitude!, props.longitude!]
    : TAIWAN_CENTER

  map = L.map(mapContainer.value).setView(initialCenter, hasCoordinate.value ? SELECTED_ZOOM : DEFAULT_ZOOM)

  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    attribution: '&copy; OpenStreetMap contributors',
    maxZoom: 19,
  }).addTo(map)

  if (hasCoordinate.value) {
    placeMarker(props.latitude!, props.longitude!)
  }

  // 核心邏輯：點地圖直接取得經緯度，完全不做地址地理編碼
  map.on('click', (e: L.LeafletMouseEvent) => {
    const { lat, lng } = e.latlng
    emit('update:latitude', lat)
    emit('update:longitude', lng)
    placeMarker(lat, lng)
  })
})

// 表單被外部重置（例如切換「新增」/「編輯」不同筆資料）時，地圖標記要跟著同步
watch(
  () => [props.latitude, props.longitude],
  ([lat, lng]) => {
    if (lat != null && lng != null) {
      placeMarker(lat, lng)
    } else {
      removeMarker()
    }
  }
)

// 全域副作用（Leaflet 地圖實例）不會隨 Vue 元件卸載自動清除，元件卸載時必須手動 destroy，
// 否則切換路由再切回來會疊出第二個地圖實例、記憶體也會持續累積（既有慣例，見 SA/SD §11.2）
onUnmounted(() => {
  map?.remove()
  map = null
  marker = null
})
</script>

<style scoped>
.coordinate-picker {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.picker-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  flex-wrap: wrap;
}

.picker-hint {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  color: var(--text-muted);
}

.map-container {
  height: 600px;
  width: 100%;
  border-radius: 12px;
  border: 1px solid var(--border);
  /* Leaflet 內部用絕對定位排版圖磚，容器沒有明確高度地圖會整個塌陷看不到 */
}

.picker-coords {
  font-size: 12px;
  color: var(--text-secondary);
  font-family: monospace;
}

.coords-empty {
  color: var(--text-muted);
  font-family: inherit;
}
</style>
