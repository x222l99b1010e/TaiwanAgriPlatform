<!-- src/views/weather/StationView.vue -->
<template>
  <div class="page station-view">
    <h1>農場氣象</h1>

    <section class="filter-section">
      <CitySelector v-model="selectedCity" />
      <button class="btn-query" :disabled="isLoading" @click="handleQuery">
        {{ isLoading ? '查詢中...' : '查詢' }}
      </button>
      <p v-if="errorMsg" class="error-msg">{{ errorMsg }}</p>
    </section>

    <div v-if="hasQueried && !isLoading">
      <p v-if="stations.length === 0" class="empty-hint">查無資料</p>
      <div v-else class="card-grid">
        <div class="station-card" v-for="s in stations" :key="s.stationName">
          <div class="card-header">
            <span class="station-name">{{ s.stationName }}</span>
            <span class="town-name">{{ s.townName }}</span>
          </div>
          <div class="card-body">
            <div class="metric">
              <span class="mdi mdi-thermometer metric-icon temp" />
              <span class="metric-value">{{ s.temperature ?? '—' }} °C</span>
              <span class="metric-label">溫度</span>
            </div>
            <div class="metric">
              <span class="mdi mdi-water-percent metric-icon humid" />
              <span class="metric-value">{{ s.humidity ?? '—' }} %</span>
              <span class="metric-label">濕度</span>
            </div>
            <div class="metric">
              <span class="mdi mdi-weather-windy metric-icon wind" />
              <span class="metric-value">{{ s.windSpeed ?? '—' }} m/s</span>
              <span class="metric-label">風速</span>
            </div>
            <div class="metric">
              <span class="mdi mdi-weather-rainy metric-icon rain" />
              <span class="metric-value">{{ s.rainfall24h ?? '—' }} mm</span>
              <span class="metric-label">24h雨量</span>
            </div>
          </div>
          <div class="card-footer">
            更新時間：{{ s.observedAt.replace('T', ' ').slice(0, 16) }}
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { weatherApi, type WeatherStationResponseDto } from '@/api/weather'
import CitySelector from '@/components/CitySelector.vue'

const selectedCity = ref('臺北市')
const stations = ref<WeatherStationResponseDto[]>([])
const isLoading = ref(false)
const hasQueried = ref(false)
const errorMsg = ref('')

async function handleQuery() {
  isLoading.value = true
  hasQueried.value = true
  errorMsg.value = ''
  stations.value = []
  try {
    stations.value = await weatherApi.getStations(selectedCity.value)
  } catch {
    errorMsg.value = '查詢失敗，請稍後再試'
  } finally {
    isLoading.value = false
  }
}
</script>

<style scoped>
.station-view { min-width: 960px; }

h1 { font-size: 22px; font-weight: 700; color: var(--text-primary); margin-bottom: 24px; }

.filter-section {
  display: flex; align-items: flex-end; gap: 16px;
  background: var(--surface); border: 1px solid var(--border);
  border-radius: 14px; padding: 24px; margin-bottom: 28px;
  box-shadow: 0 2px 8px rgba(0,0,0,0.06);
}

.btn-query {
  padding: 9px 26px; border-radius: 999px;
  border: 1px solid #1a5220;
  background: linear-gradient(
    180deg,
    #4caf50 0%,
    #2e7d32 40%,
    #1b5e20 100%
  );
  color: white;
  font-size: 14px; font-weight: 700; cursor: pointer;
  box-shadow:
    inset 0 1px 0 rgba(255,255,255,0.35),
    inset 0 -2px 4px rgba(0,0,0,0.25),
    inset 2px 0 6px rgba(255,255,255,0.08),
    0 2px 6px rgba(0,0,0,0.20);
  transition: all 0.15s;
}
.btn-query:hover:not(:disabled) {
  background: linear-gradient(
    180deg,
    #66bb6a 0%,
    #388e3c 40%,
    #2e7d32 100%
  );
  box-shadow:
    inset 0 1px 0 rgba(255,255,255,0.45),
    inset 0 -2px 4px rgba(0,0,0,0.20),
    inset 2px 0 6px rgba(255,255,255,0.10),
    0 3px 10px rgba(0,0,0,0.22);
}
.btn-query:active:not(:disabled) {
  background: linear-gradient(
    180deg,
    #1b5e20 0%,
    #2e7d32 60%,
    #388e3c 100%
  );
  box-shadow:
    inset 0 2px 6px rgba(0,0,0,0.35),
    inset 0 -1px 0 rgba(255,255,255,0.15),
    0 1px 3px rgba(0,0,0,0.15);
}
.btn-query:disabled { background: #c8d8c8; color: #999; border-color: #b0c8b0; box-shadow: none; cursor: not-allowed; }

.error-msg  { font-size: 13px; color: var(--red); margin: 0; }
.empty-hint { font-size: 14px; color: var(--text-muted); text-align: center; padding: 40px 0; }

.card-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
  gap: 16px;
}

.station-card {
  background: var(--surface); border: 1px solid var(--border);
  border-radius: 14px; padding: 20px;
  box-shadow: 0 1px 4px rgba(0,0,0,0.05);
  transition: box-shadow 0.2s, border-color 0.2s;
}
.station-card:hover { box-shadow: 0 4px 16px rgba(0,0,0,0.10); border-color: rgba(46,125,50,0.25); }
.station-name { color: var(--green); }
.town-name  { color: var(--text-muted); }
.card-footer { color: var(--text-muted); border-top: 1px solid var(--border); }
.metric-value { color: var(--text-primary); }
.metric-label { color: var(--text-muted); }

.card-header {
  display: flex; justify-content: space-between; align-items: baseline;
  margin-bottom: 16px; padding-bottom: 12px;
  border-bottom: 1px solid var(--border);
}
/* 站名也加深 */
.station-name {
  font-size: 16px;        /* 從 15px → 16px */
  font-weight: 700;
  color: var(--green);
}

.town-name {
  font-size: 13px;        /* 從 12px → 13px */
  color: rgba(26,40,32,0.55);  /* 從 text-muted → 深一點 */
}

.card-body {
  display: grid; grid-template-columns: 1fr 1fr;
  gap: 14px; margin-bottom: 14px;
}

.metric { display: flex; flex-direction: column; align-items: center; gap: 4px; }
.metric-icon { font-size: 22px; }
.temp  { color: #e53935; }
.humid { color: #1e88e5; }
.wind  { color: #43a047; }
.rain  { color: #00acc1; }

/* 數值加大加深加粗 */
.metric-value {
  font-size: 20px;        /* 從 16px → 20px */
  font-weight: 700;
  color: #1a2820;         /* 直接用最深色，不透明 */
}

.metric-label {
  font-size: 13px;        /* 從 11px → 13px */
  color: rgba(26,40,32,0.60);  /* 從 text-muted(0.40) → 0.60 */
  font-weight: 600;
}

/* 更新時間也深一點 */
.card-footer {
  font-size: 12px;        /* 從 11px → 12px */
  color: rgba(26,40,32,0.50);  /* 從 0.35 → 0.50 */
  text-align: right;
  border-top: 1px solid var(--border);
  padding-top: 10px;
}
</style>