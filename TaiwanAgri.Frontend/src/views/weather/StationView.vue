<!-- src/views/weather/StationView.vue -->
<template>
  <div class="station-view">
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
.station-view {
  padding: 36px 56px;
  min-width: 960px;
  box-sizing: border-box;
}

h1 {
  font-size: 22px;
  font-weight: 700;
  color: rgba(200, 220, 200, 0.9);
  margin-bottom: 24px;
}

/* ── 篩選區 ── */
.filter-section {
  display: flex;
  align-items: flex-end;
  gap: 16px;
  background: rgba(255, 255, 255, 0.04);
  border: 1px solid rgba(255, 255, 255, 0.10);
  border-radius: 14px;
  padding: 24px;
  margin-bottom: 28px;
}

.btn-query {
  padding: 9px 26px;
  border-radius: 999px;
  border: none;
  background: #2e7d32;
  color: #ffffff;
  font-size: 14px;
  font-weight: 600;
  cursor: pointer;
  transition: background 0.18s;
}

.btn-query:hover:not(:disabled) { background: #388e3c; }
.btn-query:disabled { background: rgba(80, 120, 80, 0.4); cursor: not-allowed; }

.error-msg { font-size: 13px; color: rgba(240, 100, 100, 0.85); margin: 0; }
.empty-hint { font-size: 14px; color: rgba(170, 185, 205, 0.5); text-align: center; padding: 40px 0; }

/* ── 卡片 Grid ── */
.card-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
  gap: 16px;
}

.station-card {
  background: rgba(255, 255, 255, 0.04);
  border: 1px solid rgba(255, 255, 255, 0.09);
  border-radius: 14px;
  padding: 20px;
  transition: background 0.2s, border-color 0.2s;
}

.station-card:hover {
  background: rgba(255, 255, 255, 0.07);
  border-color: rgba(125, 216, 160, 0.25);
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  margin-bottom: 16px;
  padding-bottom: 12px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.07);
}

.station-name {
  font-size: 15px;
  font-weight: 700;
  color: rgba(125, 216, 160, 0.9);
}

.town-name {
  font-size: 12px;
  color: rgba(170, 185, 205, 0.45);
}

/* ── 指標 ── */
.card-body {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 14px;
  margin-bottom: 14px;
}

.metric {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 4px;
}

.metric-icon { font-size: 22px; }
.temp  { color: rgba(240, 100,  80, 0.85); }
.humid { color: rgba( 80, 160, 220, 0.85); }
.wind  { color: rgba(100, 200, 130, 0.85); }
.rain  { color: rgba( 80, 200, 210, 0.85); }

.metric-value {
  font-size: 16px;
  font-weight: 700;
  color: rgba(215, 225, 240, 0.88);
}

.metric-label {
  font-size: 11px;
  color: rgba(170, 185, 205, 0.45);
}

/* ── 卡片底部 ── */
.card-footer {
  font-size: 11px;
  color: rgba(170, 185, 205, 0.35);
  text-align: right;
  border-top: 1px solid rgba(255, 255, 255, 0.06);
  padding-top: 10px;
}
</style>