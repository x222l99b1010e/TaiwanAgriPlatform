<template>
  <div class="page traceability-view">

    <!-- 頁首 -->
    <div class="page-header">
      <h2 class="section-title">農產品追溯查詢</h2>
      <p class="section-subtitle">輸入追溯碼，查詢蔬果、雞蛋、禽肉的產地與生產者資訊</p>
    </div>

    <!-- 說明區塊 -->
    <div class="info-hint">
      <div class="info-hint-header">
        <span class="mdi mdi-information-outline hint-icon" />
        <span>查詢說明</span>
      </div>
      <ul class="hint-list">
        <li>本功能支援農產品、洗選蛋、禽肉三類追溯查詢</li>
        <li>洗選蛋與禽肉可輸入包裝上的任意序號，系統自動比對所屬批次</li>
        <li>禽肉資料由農業部定期更新，部分批次碼可能已不在查詢範圍內</li>
      </ul>
      <div class="example-row">
        <span class="example-label">範例碼：</span>
        <button class="example-chip" @click="fillExample('0101000005')">
          <span class="mdi mdi-sprout chip-icon" />農產品 0101000005
        </button>
        <button class="example-chip" @click="fillExample('0552100105')">
          <span class="mdi mdi-egg chip-icon" />洗選蛋 0552100105
        </button>
        <button class="example-chip example-chip--warn" @click="fillExample('4203824987')">
          <span class="mdi mdi-food-drumstick chip-icon" />禽肉 4203824987
          <span class="chip-warn">資料有時效性</span>
        </button>
      </div>
    </div>

    <!-- 搜尋列 -->
    <div class="search-bar">
      <input
        v-model="traceCode"
        class="search-input"
        placeholder="請輸入追溯碼，例如：0101000005"
        @keyup.enter="handleSearch"
      />
      <button
        class="btn-search"
        :disabled="store.isSearching || !traceCode.trim()"
        @click="handleSearch"
      >
        <span v-if="store.isSearching" class="mdi mdi-loading spin" />
        <span v-else class="mdi mdi-magnify" />
        {{ store.isSearching ? '查詢中...' : '查詢' }}
      </button>
    </div>

    <!-- 錯誤 -->
    <div v-if="store.searchError" class="state-box error-box">
      <span class="mdi mdi-alert-circle" />
      {{ store.searchError }}
    </div>

    <!-- 無結果 -->
    <div v-else-if="store.traceabilityResult && !hasAnyResult" class="state-box">
      <span class="mdi mdi-database-off-outline state-icon" />
      <span class="state-text">查無此追溯碼的相關資料</span>
    </div>

    <!-- 結果區塊 -->
    <div v-else-if="hasAnyResult" class="result-section">

      <!-- 生產者資訊 -->
      <div v-if="store.traceabilityResult!.producer" class="result-card">
        <div class="card-title">
          <span class="mdi mdi-account-cowboy-hat card-icon" />
          生產者資訊
        </div>
        <div class="info-grid">
          <div class="info-row">
            <span class="info-label">生產者</span>
            <span class="info-value">{{ store.traceabilityResult!.producer.producer }}</span>
          </div>
          <div class="info-row">
            <span class="info-label">地址</span>
            <span class="info-value">{{ store.traceabilityResult!.producer.address }}</span>
          </div>
          <div class="info-row">
            <span class="info-label">狀態</span>
            <span
              class="info-value status-badge"
              :class="store.traceabilityResult!.producer.status === '通過' ? 'pass' : 'fail'"
            >
              {{ store.traceabilityResult!.producer.status }}
            </span>
          </div>
          <div v-if="store.traceabilityResult!.producer.mark" class="info-row">
            <span class="info-label">驗證標章</span>
            <span class="info-value">{{ store.traceabilityResult!.producer.mark }}</span>
          </div>
          <div v-if="store.traceabilityResult!.producer.description" class="info-row">
            <span class="info-label">簡介</span>
            <span class="info-value description">{{ store.traceabilityResult!.producer.description }}</span>
          </div>
        </div>
      </div>

      <!-- 農產品清單 -->
      <div v-if="store.traceabilityResult!.agriProducts?.length" class="result-card">
        <div class="card-title">
          <span class="mdi mdi-sprout card-icon" />
          生產農產品
        </div>
        <div class="product-list">
          <div
            v-for="(item, i) in store.traceabilityResult!.agriProducts"
            :key="i"
            class="product-tag"
          >
            <span class="product-name">{{ item.product }}</span>
            <span v-if="item.place" class="product-place">{{ item.place }}</span>
            <span v-if="item.mark" class="product-mark">{{ item.mark }}</span>
          </div>
        </div>
      </div>

      <!-- 洗選蛋 -->
      <div v-if="store.traceabilityResult!.washedEgg" class="result-card">
        <div class="card-title">
          <span class="mdi mdi-egg card-icon" />
          洗選蛋資訊
        </div>
        <div class="info-grid">
          <div class="info-row">
            <span class="info-label">批次區間</span>
            <span class="info-value batch-range">
              {{ store.traceabilityResult!.washedEgg.tracenoStart }}
              <span class="mdi mdi-arrow-right range-arrow" />
              {{ store.traceabilityResult!.washedEgg.tracenoEnd }}
            </span>
          </div>
          <div class="info-row">
            <span class="info-label">通路商</span>
            <span class="info-value">{{ store.traceabilityResult!.washedEgg.selName }}</span>
          </div>
          <div class="info-row">
            <span class="info-label">負責人</span>
            <span class="info-value">{{ store.traceabilityResult!.washedEgg.selBoss }}</span>
          </div>
          <div class="info-row">
            <span class="info-label">所在地</span>
            <span class="info-value">{{ store.traceabilityResult!.washedEgg.selAddr }}</span>
          </div>
        </div>

        <!-- 蛋農列表 -->
        <div class="sub-section">
          <div class="sub-title">來源蛋農</div>
          <div class="farmer-list">
            <div v-if="store.traceabilityResult!.washedEgg.eggName1" class="farmer-tag">
              <span class="farmer-name">{{ store.traceabilityResult!.washedEgg.eggName1 }}</span>
              <span class="farmer-place">{{ store.traceabilityResult!.washedEgg.farTownName1 }}</span>
            </div>
            <div v-if="store.traceabilityResult!.washedEgg.eggName2" class="farmer-tag">
              <span class="farmer-name">{{ store.traceabilityResult!.washedEgg.eggName2 }}</span>
              <span class="farmer-place">{{ store.traceabilityResult!.washedEgg.farTownName2 }}</span>
            </div>
            <div v-if="store.traceabilityResult!.washedEgg.eggName3" class="farmer-tag">
              <span class="farmer-name">{{ store.traceabilityResult!.washedEgg.eggName3 }}</span>
              <span class="farmer-place">{{ store.traceabilityResult!.washedEgg.farTownName3 }}</span>
            </div>
          </div>
        </div>
      </div>

      <!-- 禽肉 -->
      <div v-if="store.traceabilityResult!.poultry" class="result-card">
        <div class="card-title">
          <span class="mdi mdi-food-drumstick card-icon" />
          禽肉電宰資訊
        </div>
        <div class="info-grid">
          <div class="info-row">
            <span class="info-label">批次區間</span>
            <span class="info-value batch-range">
              {{ store.traceabilityResult!.poultry.tracenoStart }}
              <span class="mdi mdi-arrow-right range-arrow" />
              {{ store.traceabilityResult!.poultry.tracenoEnd }}
            </span>
          </div>
          <div class="info-row">
            <span class="info-label">電宰場</span>
            <span class="info-value">{{ store.traceabilityResult!.poultry.kilName }}</span>
          </div>
          <div class="info-row">
            <span class="info-label">負責人</span>
            <span class="info-value">{{ store.traceabilityResult!.poultry.kilBoss }}</span>
          </div>
          <div class="info-row">
            <span class="info-label">地址</span>
            <span class="info-value">{{ store.traceabilityResult!.poultry.kilAddr }}</span>
          </div>
          <div class="info-row">
            <span class="info-label">日期</span>
            <span class="info-value">{{ store.traceabilityResult!.poultry.cdate }}</span>
          </div>
        </div>

        <!-- 來源牧場列表 -->
        <div class="sub-section">
          <div class="sub-title">來源牧場</div>
          <div class="farmer-list">
            <div v-if="store.traceabilityResult!.poultry.farmersName1" class="farmer-tag">
              <span class="farmer-name">{{ store.traceabilityResult!.poultry.farmersName1 }}</span>
              <span class="farmer-type">{{ store.traceabilityResult!.poultry.farmersType1 }}</span>
              <span class="farmer-place">{{ store.traceabilityResult!.poultry.farmersplace1 }}</span>
            </div>
            <div v-if="store.traceabilityResult!.poultry.farmersName2" class="farmer-tag">
              <span class="farmer-name">{{ store.traceabilityResult!.poultry.farmersName2 }}</span>
              <span class="farmer-type">{{ store.traceabilityResult!.poultry.farmersType2 }}</span>
              <span class="farmer-place">{{ store.traceabilityResult!.poultry.farmersplace2 }}</span>
            </div>
          </div>
        </div>
      </div>

    </div>

    <!-- 初始提示（尚未查詢） -->
    <div v-else class="state-box hint-box">
      <span class="mdi mdi-barcode-scan state-icon" />
      <span class="state-text">請輸入追溯碼開始查詢</span>
    </div>

  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useFoodSafetyStore } from '@/stores/foodSafety'

const store = useFoodSafetyStore()
const traceCode = ref('')

const hasAnyResult = computed(() => {
  const r = store.traceabilityResult
  if (!r) return false
  return !!(r.agriProducts?.length || r.producer || r.washedEgg || r.poultry)
})

function fillExample(code: string) {
  traceCode.value = code
  store.searchTraceability(code)
}

function handleSearch() {
  if (!traceCode.value.trim()) return
  store.searchTraceability(traceCode.value.trim())
}
</script>

<style scoped>
/* 查詢區是單一輸入框＋一顆按鈕，撐滿頁面容器只會讓 10 碼追溯碼的輸入框長達
   一千多像素。頁面容器不縮，改由說明、查詢列與狀態框自己限寬並靠左
   （見 base.css .page）；查詢結果卡片維持全寬，因為裡面是多欄資訊格。 */
.info-hint,
.search-bar,
.state-box { max-width: var(--container-sm); }

/* ── 頁首 ── */
.page-header { margin-bottom: 24px; }

.section-title {
  font-size: 22px;
  font-weight: 700;
  color: var(--text-primary);
  margin-bottom: 6px;
}

.section-subtitle {
  font-size: 13px;
  color: var(--text-muted);
}

/* ── 說明區塊 ── */
.info-hint {
  background: #e3f2fd;
  border: 1px solid rgba(21, 101, 192, 0.20);
  border-radius: 12px;
  padding: 16px 20px;
  margin-bottom: 20px;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.info-hint-header {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 13px;
  font-weight: 700;
  color: #1565c0;
}

.hint-icon { font-size: 17px; }

.hint-list {
  margin: 0;
  padding-left: 20px;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.hint-list li {
  font-size: 13px;
  color: #1565c0;
  line-height: 1.6;
}

.example-row {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
  margin-top: 2px;
}

.example-label {
  font-size: 12px;
  font-weight: 700;
  color: #1565c0;
  white-space: nowrap;
}

.example-chip {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  padding: 5px 12px;
  border-radius: 999px;
  border: 1px solid rgba(21, 101, 192, 0.30);
  background: rgba(255, 255, 255, 0.70);
  color: #1565c0;
  font-size: 12px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.15s;
}

.example-chip:hover {
  background: #fff;
  border-color: #1565c0;
  box-shadow: 0 1px 4px rgba(21, 101, 192, 0.15);
}

.example-chip--warn {
  border-color: rgba(230, 81, 0, 0.30);
  color: #e65100;
}

.example-chip--warn:hover {
  border-color: #e65100;
  box-shadow: 0 1px 4px rgba(230, 81, 0, 0.15);
}

.chip-icon { font-size: 14px; }

.chip-warn {
  font-size: 10px;
  font-weight: 700;
  background: rgba(230, 81, 0, 0.12);
  color: #e65100;
  padding: 1px 6px;
  border-radius: 999px;
  margin-left: 2px;
}

/* ── 搜尋列 ── */
.search-bar {
  display: flex;
  gap: 12px;
  margin-bottom: 28px;
}

.search-input {
  flex: 1;
  padding: 10px 16px;
  border: 1px solid var(--border);
  border-radius: 8px;
  font-size: 14px;
  color: var(--text-primary);
  background: var(--surface);
  outline: none;
  transition: border-color 0.15s, box-shadow 0.15s;
}

.search-input:focus {
  border-color: var(--green);
  box-shadow: 0 0 0 3px rgba(46, 125, 50, 0.12);
}

.btn-search {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 9px 26px;
  border-radius: 999px;
  border: 1px solid #1a5220;
  background: linear-gradient(180deg, #4caf50 0%, #2e7d32 40%, #1b5e20 100%);
  color: white;
  font-size: 14px;
  font-weight: 700;
  cursor: pointer;
  white-space: nowrap;
  box-shadow: inset 0 1px 0 rgba(255,255,255,0.35), inset 0 -2px 4px rgba(0,0,0,0.25), 0 2px 6px rgba(0,0,0,0.20);
  transition: all 0.15s;
}

.btn-search:hover:not(:disabled) {
  background: linear-gradient(180deg, #66bb6a 0%, #388e3c 40%, #2e7d32 100%);
}

.btn-search:active:not(:disabled) {
  background: linear-gradient(180deg, #1b5e20 0%, #2e7d32 60%, #388e3c 100%);
  box-shadow: inset 0 2px 6px rgba(0,0,0,0.35), 0 1px 3px rgba(0,0,0,0.15);
}

.btn-search:disabled {
  background: #c8d8c8;
  color: #999;
  border-color: #b0c8b0;
  box-shadow: none;
  cursor: not-allowed;
}

@keyframes spin { to { transform: rotate(360deg); } }
.spin { display: inline-block; animation: spin 0.8s linear infinite; }

/* ── 狀態容器 ── */
.state-box {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 12px;
  padding: 56px 32px;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 16px;
}

.state-icon { font-size: 36px; color: #aaa; }
.state-text { font-size: 15px; color: var(--text-muted); }

.error-box {
  background: #fff5f5;
  border-color: #ffcdd2;
  color: #c62828;
  font-size: 14px;
}

.hint-box .state-icon { color: #c8e6c9; }

/* ── 結果卡片 ── */
.result-section {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.result-card {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 16px;
  padding: 24px 28px;
  box-shadow: 0 2px 8px rgba(46, 125, 50, 0.06);
}

.card-title {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 16px;
  font-weight: 700;
  color: #1b5e20;
  margin-bottom: 20px;
  padding-bottom: 12px;
  border-bottom: 1px solid var(--border);
}

.card-icon { font-size: 20px; color: #43a047; }

/* ── 資訊列 ── */
.info-grid { display: flex; flex-direction: column; gap: 12px; }

.info-row {
  display: flex;
  gap: 16px;
  align-items: flex-start;
}

.info-label {
  width: 90px;
  flex-shrink: 0;
  font-size: 13px;
  color: var(--text-muted);
  font-weight: 600;
  padding-top: 2px;
}

.info-value {
  font-size: 14px;
  color: var(--text-primary);
  flex: 1;
}

.description { line-height: 1.7; white-space: pre-wrap; }

/* ── 批次區間 ── */
.batch-range {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-family: monospace;
  font-size: 13px;
  background: #f1f8f1;
  border: 1px solid var(--border);
  border-radius: 6px;
  padding: 3px 10px;
  flex: unset;
}

.range-arrow { font-size: 14px; color: var(--text-muted); }

/* ── 狀態徽章 ── */
.status-badge {
  display: inline-block;
  align-self: flex-start;
  padding: 3px 12px;
  border-radius: 999px;
  font-size: 12px;
  font-weight: 700;
  flex: unset;
}

.status-badge.pass { background: #e8f5e9; color: #2e7d32; }
.status-badge.fail { background: #fff3e0; color: #e65100; }

/* ── 農產品標籤 ── */
.product-list {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
}

.product-tag {
  display: flex;
  flex-direction: column;
  gap: 2px;
  background: #f1f8f1;
  border: 1px solid var(--border);
  border-radius: 10px;
  padding: 10px 16px;
  min-width: 100px;
}

.product-name {
  font-size: 14px;
  font-weight: 700;
  color: #1b5e20;
}

.product-place, .product-mark {
  font-size: 11px;
  color: var(--text-muted);
}

/* ── 蛋農 / 牧場子區塊 ── */
.sub-section {
  margin-top: 20px;
  padding-top: 16px;
  border-top: 1px solid var(--border);
}

.sub-title {
  font-size: 13px;
  font-weight: 700;
  color: var(--text-muted);
  margin-bottom: 12px;
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.farmer-list {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
}

.farmer-tag {
  display: flex;
  flex-direction: column;
  gap: 3px;
  background: #f1f8f1;
  border: 1px solid var(--border);
  border-radius: 10px;
  padding: 10px 16px;
  min-width: 140px;
}

.farmer-name {
  font-size: 14px;
  font-weight: 700;
  color: #1b5e20;
}

.farmer-type {
  font-size: 12px;
  color: #43a047;
  font-weight: 600;
}

.farmer-place {
  font-size: 11px;
  color: var(--text-muted);
}
</style>