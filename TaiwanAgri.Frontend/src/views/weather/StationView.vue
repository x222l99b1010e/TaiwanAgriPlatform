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
              <span class="town-pill">{{ s.townName }}</span>
            </header>

            <!-- 溫度當主角：大數字＋依溫度分級的色籤（冷涼/涼爽/舒適/溫暖/炎熱）。
                 四張卡不再只是同一個大數字換值，色籤與下方量尺讓每張卡各有樣子。 -->
            <div class="temp-hero">
              <div class="temp-main">
                <span class="temp-value">{{ s.temperature ?? '—' }}</span>
                <span class="temp-unit">°C</span>
              </div>
              <span class="temp-band" :class="`band--${tempBand(s.temperature).key}`">
                <span class="mdi" :class="tempBand(s.temperature).icon" />{{ tempBand(s.temperature).label }}
              </span>
            </div>

            <!-- 日內高低溫量尺：把目前溫度標在「當日最低—最高」之間。每張卡的標記點
                 落點都不一樣，一排卡片就有了節奏；同時用上原本沒顯示的日高/低溫兩欄。 -->
            <div v-if="hasRange(s)" class="temp-gauge">
              <span class="gauge-end">{{ s.dailyMinTemp }}°</span>
              <div class="gauge-track">
                <span class="gauge-marker" :style="{ left: markerLeft(s) }" />
              </div>
              <span class="gauge-end">{{ s.dailyMaxTemp }}°</span>
            </div>

            <dl class="metric-row">
              <div class="metric">
                <dt class="metric-label"><span class="mdi mdi-water-percent metric-badge" />濕度</dt>
                <dd class="metric-value">{{ s.humidity ?? '—' }}<span class="metric-unit">%</span></dd>
              </div>
              <div class="metric">
                <dt class="metric-label"><span class="mdi mdi-weather-windy metric-badge" />風速</dt>
                <dd class="metric-value">{{ s.windSpeed ?? '—' }}<span class="metric-unit">m/s</span></dd>
              </div>
              <div class="metric">
                <dt class="metric-label"><span class="mdi mdi-weather-rainy metric-badge" />24h 雨量</dt>
                <!-- 有下雨才把數字換色：這一格平常是 0，變成非零時才是要注意的事 -->
                <dd class="metric-value" :class="{ 'is-wet': (s.rainfall24h ?? 0) > 0 }">
                  {{ s.rainfall24h ?? '—' }}<span class="metric-unit">mm</span>
                </dd>
              </div>
            </dl>

            <footer class="card-footer">
              <span class="mdi mdi-clock-outline" />
              {{ s.observedAt.replace('T', ' ').slice(0, 16) }}
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

// 溫度分級：色籤的顏色與文字都由這裡決定，門檻依台灣農業體感（<16 冷涼…>32 炎熱）。
// 溫度是「越高越熱」的量，用暖色代表熱、冷色代表涼是自然的對應，不是隨手上色。
function tempBand(t: number | null) {
  if (t == null) return { key: 'none', label: '無資料', icon: 'mdi-thermometer-off' }
  if (t < 16)  return { key: 'cold', label: '冷涼', icon: 'mdi-snowflake' }
  if (t < 22)  return { key: 'cool', label: '涼爽', icon: 'mdi-weather-partly-cloudy' }
  if (t < 28)  return { key: 'mild', label: '舒適', icon: 'mdi-weather-sunny' }
  if (t < 32)  return { key: 'warm', label: '溫暖', icon: 'mdi-white-balance-sunny' }
  return { key: 'hot', label: '炎熱', icon: 'mdi-weather-sunny-alert' }
}

/** 日高/低溫都有、且高溫確實大於低溫時才畫量尺，否則除以零或倒著畫 */
function hasRange(s: WeatherStationResponseDto): boolean {
  return s.temperature != null && s.dailyMinTemp != null && s.dailyMaxTemp != null
    && s.dailyMaxTemp > s.dailyMinTemp
}

/** 目前溫度在「最低—最高」區間的百分比位置，夾在 0–100 之間避免超出量尺 */
function markerLeft(s: WeatherStationResponseDto): string {
  const min = s.dailyMinTemp!, max = s.dailyMaxTemp!, cur = s.temperature!
  const pct = ((cur - min) / (max - min)) * 100
  return `${Math.min(100, Math.max(0, pct))}%`
}

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
/* 顏色全部改用 semantic 層；卡片不給陰影，靠 1px 邊框與底色差。
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
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
  transition: border-color var(--duration-base) var(--ease-work),
              transform var(--duration-base) var(--ease-work);
}
.station-card:hover { border-color: var(--color-border-strong); transform: translateY(calc(-1 * var(--lift-work))); }

.card-header {
  display: flex; justify-content: space-between; align-items: center; gap: var(--space-2);
  padding-bottom: var(--space-3);
  border-bottom: var(--border-width) solid var(--color-border);
}
.station-name { font-size: var(--text-base); font-weight: var(--weight-bold); color: var(--color-text); }
/* 鄉鎮改成小藥丸標籤：比純文字多一個「這是分類」的視覺提示 */
.town-pill {
  flex-shrink: 0;
  font-size: var(--text-2xs); font-weight: var(--weight-medium);
  color: var(--color-text-dim);
  background: var(--color-bg-sunken);
  border-radius: var(--radius-full);
  padding: 2px var(--space-2);
}

/* 主數字＋色籤同一列：--text-3xl 是給「主視覺數字」那一階 */
.temp-hero { display: flex; align-items: center; justify-content: space-between; gap: var(--space-3); }
.temp-main { display: flex; align-items: baseline; gap: var(--space-1); }
.temp-value {
  font-family: var(--font-num);
  font-size: var(--text-3xl);
  font-weight: var(--weight-bold);
  line-height: 1;
  color: var(--color-text);
  font-variant-numeric: tabular-nums;
}
.temp-unit { font-size: var(--text-base); color: var(--color-text-dim); }

/* 溫度色籤：暖熱、冷涼，中段舒適用綠。顏色是資料的一部分（越熱越暖色），不是裝飾 */
.temp-band {
  display: inline-flex; align-items: center; gap: var(--space-1);
  flex-shrink: 0;
  font-size: var(--text-2xs); font-weight: var(--weight-bold);
  padding: var(--space-1) var(--space-2);
  border-radius: var(--radius-full);
}
.temp-band .mdi { font-size: var(--text-sm); }
.band--none { color: var(--color-text-dim); background: var(--color-bg-sunken); }
.band--cold { color: var(--info-700);    background: var(--info-50); }
.band--cool { color: var(--info-500);    background: var(--info-50); }
.band--mild { color: var(--color-action);background: var(--color-action-soft); }
.band--warm { color: var(--warning-700); background: var(--warning-50); }
.band--hot  { color: var(--danger-500);  background: var(--danger-50); }

/* 日內高低溫量尺 */
.temp-gauge { display: flex; align-items: center; gap: var(--space-2); }
.gauge-end {
  font-family: var(--font-num); font-size: var(--text-2xs);
  color: var(--color-text-dim); font-variant-numeric: tabular-nums; flex-shrink: 0;
}
.gauge-track {
  position: relative; flex: 1; height: 6px; border-radius: var(--radius-full);
  /* 冷→暖的漸層：軌道本身就示意「左邊冷、右邊熱」，標記點落在哪就知道今天偏涼或偏熱 */
  background: linear-gradient(to right, var(--info-100), var(--seed-100), var(--warning-100));
}
.gauge-marker {
  position: absolute; top: 50%; transform: translate(-50%, -50%);
  width: 12px; height: 12px; border-radius: var(--radius-full);
  background: var(--color-surface);
  border: 2px solid var(--color-brand);
}

.metric-row {
  display: grid; grid-template-columns: repeat(3, 1fr);
  gap: var(--space-3);
  padding-top: var(--space-4);
  border-top: var(--border-width) solid var(--color-border);
}
.metric { display: flex; flex-direction: column; gap: var(--space-2); min-width: 0; }
.metric-label {
  display: flex; align-items: center; gap: var(--space-1);
  font-size: var(--text-2xs); color: var(--color-text-dim);
  white-space: nowrap;
}
/* 指標圖示改成有底色的小圓徽章：一排三個小圓，比三個裸圖示更有結構感 */
.metric-badge {
  display: inline-flex; align-items: center; justify-content: center;
  width: 18px; height: 18px; border-radius: var(--radius-full);
  background: var(--color-bg-sunken); color: var(--color-text-dim);
  font-size: var(--text-xs);
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
  display: flex; align-items: center; gap: var(--space-1);
  justify-content: flex-end;
  font-family: var(--font-num);
  font-size: var(--text-xs);
  color: var(--color-text-dim);
  border-top: var(--border-width) solid var(--color-border);
  padding-top: var(--space-3);
}
.card-footer .mdi { font-size: var(--text-sm); }
</style>
