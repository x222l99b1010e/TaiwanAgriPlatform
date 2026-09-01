<!-- src/views/weather/StationView.vue -->
<template>
  <div class="page station-view">
    <PageHeader
      title="農場氣象"
      subtitle="各地農業氣象站的即時溫度、濕度、風速與 24 小時累積雨量"
    />

    <FilterCard>
      <CitySelector v-model="selectedCity" />
      <Btn icon="mdi-magnify" :loading="isLoading" @click="handleQuery">
        {{ isLoading ? '查詢中...' : '查詢' }}
      </Btn>
    </FilterCard>

    <StateBlock v-if="!hasQueried" state="hint" message="請選擇縣市後按下查詢" />
    <StateBlock v-else-if="isLoading" state="loading" message="資料載入中..." />
    <StateBlock
      v-else-if="errorMsg"
      state="error"
      :message="errorMsg"
      retryable
      @retry="handleQuery"
    />
    <StateBlock
      v-else-if="stations.length === 0"
      state="empty"
      message="查無資料"
      hint="這個縣市目前沒有可用的氣象站觀測值"
    />

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
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { weatherApi, type WeatherStationResponseDto } from '@/api/weather'
import CitySelector from '@/components/CitySelector.vue'
import PageHeader from '@/components/ui/PageHeader.vue'
import FilterCard from '@/components/ui/FilterCard.vue'
import StateBlock from '@/components/ui/StateBlock.vue'
import Btn from '@/components/ui/Btn.vue'

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

.card-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
  gap: var(--space-4);
}

.station-card {
  background: var(--surface); border: 1px solid var(--border);
  border-radius: 14px; padding: var(--space-5);
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
  margin-bottom: var(--space-4); padding-bottom: var(--space-3);
  border-bottom: 1px solid var(--border);
}
/* 站名也加深 */
.station-name {
  font-size: 16px;        /* 從 15px → 16px */
  font-weight: var(--weight-bold);
  color: var(--green);
}

.town-name {
  font-size: var(--text-sm);        /* 從 12px → 13px */
  color: var(--neutral-500);  /* 從 text-muted → 深一點 */
}

.card-body {
  display: grid; grid-template-columns: 1fr 1fr;
  gap: 14px; margin-bottom: 14px;
}

.metric { display: flex; flex-direction: column; align-items: center; gap: var(--space-1); }
.metric-icon { font-size: var(--text-xl); }
.temp  { color: var(--danger-500); }
.humid { color: var(--info-500); }
.wind  { color: var(--green-500); }
.rain  { color: var(--teal-600); }

/* 數值加大加深加粗 */
.metric-value {
  font-size: 20px;        /* 從 16px → 20px */
  font-weight: var(--weight-bold);
  color: var(--neutral-900);         /* 直接用最深色，不透明 */
}

.metric-label {
  font-size: var(--text-sm);        /* 從 11px → 13px */
  color: var(--neutral-500);  /* 從 text-muted(0.40) → 0.60 */
  font-weight: 600;
}

/* 更新時間也深一點 */
.card-footer {
  font-size: var(--text-xs);        /* 從 11px → 12px */
  color: var(--neutral-500);  /* 從 0.35 → 0.50 */
  text-align: right;
  border-top: 1px solid var(--border);
  padding-top: 10px;
}
</style>