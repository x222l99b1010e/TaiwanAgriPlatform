<!-- src/views/weather/StationView.vue -->
<template>
  <div class="page station-view">
    <QueryLayout
      title="農場氣象"
      title-en="FIELD WEATHER"
      subtitle="各地農業氣象站的即時溫度、濕度、風速與 24 小時累積雨量"
    >
      <template #actions>
        <Btn icon="mdi-magnify" :loading="isLoading" @click="handleQuery">
          {{ isLoading ? '查詢中...' : '查詢' }}
        </Btn>
      </template>

      <template #filters>
        <CitySelector v-model="selectedCity" />
      </template>

      <template #results>
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
          <article class="station-card" v-for="s in stations" :key="s.stationName">
            <header class="card-header">
              <span class="station-name">{{ s.stationName }}</span>
              <span class="town-name">{{ s.townName }}</span>
            </header>

            <!-- 溫度當主角、其餘三項降一階：四個同樣大的數字擺在一起時卡片沒有視覺焦點，
                 每一張都得從頭讀一遍。溫度是最常被掃的那一個，所以由它擔任主數字。 -->
            <div class="temp-row">
              <span class="mdi mdi-thermometer temp-icon" />
              <span class="temp-value">{{ s.temperature ?? '—' }}</span>
              <span class="temp-unit">°C</span>
            </div>

            <dl class="metric-row">
              <div class="metric">
                <dt class="metric-label"><span class="mdi mdi-water-percent" />濕度</dt>
                <dd class="metric-value">{{ s.humidity ?? '—' }}<span class="metric-unit">%</span></dd>
              </div>
              <div class="metric">
                <dt class="metric-label"><span class="mdi mdi-weather-windy" />風速</dt>
                <dd class="metric-value">{{ s.windSpeed ?? '—' }}<span class="metric-unit">m/s</span></dd>
              </div>
              <div class="metric">
                <dt class="metric-label"><span class="mdi mdi-weather-rainy" />24h 雨量</dt>
                <!-- 有下雨才把數字換色：這一格平常是 0，變成非零時才是要注意的事 -->
                <dd class="metric-value" :class="{ 'is-wet': (s.rainfall24h ?? 0) > 0 }">
                  {{ s.rainfall24h ?? '—' }}<span class="metric-unit">mm</span>
                </dd>
              </div>
            </dl>

            <footer class="card-footer">
              更新時間：{{ s.observedAt.replace('T', ' ').slice(0, 16) }}
            </footer>
          </article>
        </div>
      </template>
    </QueryLayout>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { weatherApi, type WeatherStationResponseDto } from '@/api/weather'
import CitySelector from '@/components/CitySelector.vue'
import QueryLayout from '@/components/layouts/QueryLayout.vue'
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
/* 顏色全部改用 semantic 層（style tile §九）；卡片不給陰影，靠 1px 邊框與底色差。
   原本四個指標的圖示各是紅／藍／綠／青四種色相，一張卡片上就用掉四個顏色——
   圖示是標籤不是資料，一律降成次要文字色，強調留給數字本身。 */
.station-view { min-width: 960px; }

.card-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
  gap: var(--space-4);
}

.station-card {
  background: var(--color-surface);
  border: var(--border-width) solid var(--color-border);
  border-radius: var(--radius-lg);
  padding: var(--space-5);
  transition: border-color var(--duration-base) var(--ease-work);
}
.station-card:hover { border-color: var(--color-border-strong); }

.card-header {
  display: flex; justify-content: space-between; align-items: baseline; gap: var(--space-2);
  padding-bottom: var(--space-3);
  border-bottom: var(--border-width) solid var(--color-border);
}
.station-name { font-size: var(--text-base); font-weight: var(--weight-bold); color: var(--color-text); }
.town-name { font-size: var(--text-sm); color: var(--color-text-dim); }

/* 主數字：--text-3xl 是 style tile 給「主視覺數字」的那一階 */
.temp-row {
  display: flex; align-items: baseline; gap: var(--space-2);
  padding-block: var(--space-5) var(--space-4);
}
.temp-icon { font-size: var(--text-xl); color: var(--color-text-dim); align-self: center; }
.temp-value {
  font-family: var(--font-num);
  font-size: var(--text-3xl);
  font-weight: var(--weight-bold);
  line-height: var(--leading-tight);
  color: var(--color-text);
  font-variant-numeric: tabular-nums;
}
.temp-unit { font-size: var(--text-base); color: var(--color-text-dim); }

.metric-row {
  display: grid; grid-template-columns: repeat(3, 1fr);
  gap: var(--space-3);
  padding-block: var(--space-4);
  border-top: var(--border-width) solid var(--color-border);
}
.metric { display: flex; flex-direction: column; gap: var(--space-1); min-width: 0; }
.metric-label {
  display: flex; align-items: center; gap: var(--space-1);
  font-size: var(--text-2xs); color: var(--color-text-dim);
  white-space: nowrap;
}
.metric-value {
  margin: 0;
  font-family: var(--font-num);
  font-size: var(--text-base); font-weight: var(--weight-bold);
  color: var(--color-text);
  font-variant-numeric: tabular-nums;
}
.metric-value.is-wet { color: var(--info-500); }
.metric-unit {
  margin-inline-start: 2px;
  font-family: var(--font-body);
  font-size: var(--text-2xs); font-weight: var(--weight-normal);
  color: var(--color-text-dim);
}

.card-footer {
  font-family: var(--font-num);
  font-size: var(--text-xs);
  color: var(--color-text-dim);
  text-align: right;
  border-top: var(--border-width) solid var(--color-border);
  padding-top: var(--space-3);
}
</style>
