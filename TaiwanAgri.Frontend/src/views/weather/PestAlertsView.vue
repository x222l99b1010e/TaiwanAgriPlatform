<!-- src/views/weather/PestAlertsView.vue -->
<template>
  <div class="pest-alerts-view">
    <h1>病蟲害警報</h1>

    <section class="filter-section">
      <CitySelector v-model="selectedCity" />
      <button class="btn-clear" @click="clearCity">全台</button>
      <p v-if="errorMsg" class="error-msg">{{ errorMsg }}</p>
    </section>

    <!-- 載入中 -->
    <div v-if="isLoading" class="loading-hint">載入中...</div>

    <div v-else>
      <p v-if="alerts.length === 0" class="empty-hint">查無警報資料</p>

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
                class="tag city-tag"
                v-for="c in a.cities"
                :key="c"
              >{{ c }}</span>
              <span
                class="tag crop-tag"
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

        <!-- 分頁 -->
        <div class="pagination">
          <button
            class="page-btn"
            :disabled="page === 1"
            @click="changePage(page - 1)"
          >‹ 上一頁</button>
          <span class="page-info">第 {{ page }} 頁</span>
          <button
            class="page-btn"
            :disabled="alerts.length < 20"
            @click="changePage(page + 1)"
          >下一頁 ›</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch, onMounted } from 'vue'
import { weatherApi, type PestAlertResponseDto } from '@/api/weather'
import CitySelector from '@/components/CitySelector.vue'

const selectedCity = ref('臺北市')
const alerts       = ref<PestAlertResponseDto[]>([])
const isLoading    = ref(false)
const errorMsg     = ref('')
const page         = ref(1)
const expandedId   = ref<number | null>(null)

function toggleExpand(id: number) {
  expandedId.value = expandedId.value === id ? null : id
}

function clearCity() {
  selectedCity.value = ''
  page.value = 1
  fetchAlerts()
}

async function fetchAlerts() {
  isLoading.value = true
  errorMsg.value = ''
  alerts.value = []
  expandedId.value = null
  try {
    alerts.value = await weatherApi.getPestAlerts(
      selectedCity.value || undefined,
      page.value
    )
  } catch {
    errorMsg.value = '載入失敗，請稍後再試'
  } finally {
    isLoading.value = false
  }
}

function changePage(p: number) {
  page.value = p
  fetchAlerts()
}

// 切換城市時回到第一頁重查
watch(selectedCity, () => {
  page.value = 1
  fetchAlerts()
})

onMounted(fetchAlerts)
</script>

<style scoped>
.pest-alerts-view {
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
  gap: 12px;
  flex-wrap: wrap;
  background: rgba(255, 255, 255, 0.04);
  border: 1px solid rgba(255, 255, 255, 0.10);
  border-radius: 14px;
  padding: 24px;
  margin-bottom: 28px;
}

.btn-clear {
  padding: 8px 20px;
  border-radius: 999px;
  border: 1px solid rgba(255, 255, 255, 0.15);
  background: rgba(255, 255, 255, 0.05);
  color: rgba(170, 185, 205, 0.75);
  font-size: 13.5px;
  cursor: pointer;
  transition: all 0.15s;
}
.btn-clear:hover {
  background: rgba(255, 255, 255, 0.10);
  color: rgba(210, 225, 230, 0.9);
}

.error-msg    { font-size: 13px; color: rgba(240, 100, 100, 0.85); margin: 0; }
.empty-hint   { font-size: 14px; color: rgba(170, 185, 205, 0.5); text-align: center; padding: 40px 0; }
.loading-hint { font-size: 14px; color: rgba(170, 185, 205, 0.5); text-align: center; padding: 40px 0; }

/* ── 警報卡片 ── */
.alert-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
  margin-bottom: 24px;
}

.alert-card {
  background: rgba(255, 255, 255, 0.04);
  border: 1px solid rgba(255, 255, 255, 0.09);
  border-radius: 14px;
  padding: 20px 24px;
  cursor: pointer;
  transition: background 0.18s, border-color 0.18s;
}

.alert-card:hover {
  background: rgba(255, 255, 255, 0.07);
  border-color: rgba(125, 216, 160, 0.2);
}

.alert-card.expanded {
  border-color: rgba(125, 216, 160, 0.35);
  background: rgba(125, 216, 160, 0.04);
}

/* 卡片標頭 */
.card-top {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 8px;
}

.card-meta {
  display: flex;
  align-items: center;
  gap: 10px;
}

.pub-date {
  font-size: 12px;
  color: rgba(170, 185, 205, 0.45);
  font-variant-numeric: tabular-nums;
}

.issue-badge {
  font-size: 11px;
  padding: 2px 8px;
  border-radius: 999px;
  background: rgba(125, 216, 160, 0.12);
  color: rgba(125, 216, 160, 0.75);
  border: 1px solid rgba(125, 216, 160, 0.2);
}

.expand-icon {
  font-size: 18px;
  color: rgba(170, 185, 205, 0.35);
  transition: color 0.15s;
}
.alert-card:hover .expand-icon { color: rgba(170, 185, 205, 0.7); }

/* 主旨 */
.card-subject {
  font-size: 15px;
  font-weight: 600;
  color: rgba(215, 230, 220, 0.9);
  margin-bottom: 12px;
  line-height: 1.5;
}

/* 標籤 */
.tag-row {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

.tag {
  font-size: 11.5px;
  padding: 3px 10px;
  border-radius: 999px;
  border: 1px solid;
}

.city-tag {
  background: rgba(100, 170, 220, 0.08);
  border-color: rgba(100, 170, 220, 0.25);
  color: rgba(100, 170, 220, 0.8);
}

.crop-tag {
  background: rgba(110, 190, 140, 0.08);
  border-color: rgba(110, 190, 140, 0.25);
  color: rgba(110, 190, 140, 0.8);
}

/* 展開內文 */
.card-body {
  margin-top: 18px;
  padding-top: 18px;
  border-top: 1px solid rgba(255, 255, 255, 0.07);
}

.section-label {
  font-size: 11px;
  font-weight: 600;
  color: rgba(125, 216, 160, 0.6);
  letter-spacing: 0.08em;
  text-transform: uppercase;
  margin-bottom: 8px;
}

.section-label.prescription {
  color: rgba(255, 190, 80, 0.65);
  margin-top: 16px;
}

.body-text {
  font-size: 13.5px;
  color: rgba(200, 215, 210, 0.75);
  line-height: 1.8;
  white-space: pre-wrap;
  margin: 0;
}

/* ── 分頁 ── */
.pagination {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 16px;
  padding: 8px 0 4px;
}

.page-btn {
  padding: 7px 20px;
  border-radius: 8px;
  border: 1px solid rgba(255, 255, 255, 0.12);
  background: rgba(255, 255, 255, 0.04);
  color: rgba(170, 185, 205, 0.7);
  font-size: 13.5px;
  cursor: pointer;
  transition: all 0.15s;
}
.page-btn:hover:not(:disabled) {
  background: rgba(255, 255, 255, 0.09);
  color: rgba(210, 225, 230, 0.9);
}
.page-btn:disabled {
  opacity: 0.3;
  cursor: not-allowed;
}

.page-info {
  font-size: 13px;
  color: rgba(170, 185, 205, 0.45);
}
</style>