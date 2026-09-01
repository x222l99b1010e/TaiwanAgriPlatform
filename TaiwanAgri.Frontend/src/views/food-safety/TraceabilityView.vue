<template>
  <div class="page traceability-view">

    <PageHeader
      title="農產品追溯查詢"
      subtitle="輸入追溯碼，查詢蔬果、雞蛋、禽肉的產地與生產者資訊"
    />

    <!-- 說明區塊 -->
    <HintBox title="查詢說明" class="content-sm page-hint">
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
          <span class="badge chip-warn">資料有時效性</span>
        </button>
      </div>
    </HintBox>

    <!-- 搜尋列 -->
    <div class="search-bar">
      <input
        v-model="traceCode"
        class="search-input"
        placeholder="請輸入追溯碼，例如：0101000005"
        @keyup.enter="handleSearch"
      />
      <Btn
        icon="mdi-magnify"
        :loading="store.isSearching"
        :disabled="!traceCode.trim()"
        @click="handleSearch"
      >{{ store.isSearching ? '查詢中...' : '查詢' }}</Btn>
    </div>

    <StateBlock v-if="store.isSearching" class="content-sm" state="loading" message="查詢中..." />
    <StateBlock
      v-else-if="store.searchError"
      class="content-sm"
      state="error"
      :message="store.searchError"
      retryable
      @retry="handleSearch"
    />
    <StateBlock
      v-else-if="store.traceabilityResult && !hasAnyResult"
      class="content-sm"
      state="empty"
      message="查無此追溯碼的相關資料"
      hint="請確認號碼是否正確；禽肉批次碼有時效性，較舊的批次可能已不在查詢範圍內"
    />

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
              class="info-value badge status-badge"
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
    <StateBlock
      v-else
      class="content-sm"
      state="hint"
      icon="mdi-barcode-scan"
      message="請輸入追溯碼開始查詢"
      hint="上方有三組範例碼可以直接點來試"
    />

  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useFoodSafetyStore } from '@/stores/foodSafety'
import PageHeader from '@/components/ui/PageHeader.vue'
import StateBlock from '@/components/ui/StateBlock.vue'
import Btn from '@/components/ui/Btn.vue'
import HintBox from '@/components/ui/HintBox.vue'

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
.page-hint,
.search-bar { max-width: var(--container-sm); }

.page-hint { margin-bottom: var(--space-5); }

/* 條列、範例鈕、chip 圖示已收進 base.css 共用 */
.chip-warn { background: var(--warning-50); color: var(--warning-500); margin-left: var(--space-1); }

/* ── 搜尋列 ── */
.search-bar {
  display: flex;
  gap: var(--space-3);
  margin-bottom: var(--space-8);
}

.search-input {
  flex: 1;
  padding: var(--space-3) var(--space-4);
  border: 1px solid var(--border);
  border-radius: var(--radius-md);
  font-size: var(--text-base);
  color: var(--text-primary);
  background: var(--surface);
  outline: none;
  transition: border-color var(--duration-fast), box-shadow var(--duration-fast);
}

.search-input:focus {
  border-color: var(--green);
  box-shadow: var(--shadow-focus);
}
/* ── 狀態容器 ── */
/* ── 結果卡片 ── */
.result-section {
  display: flex;
  flex-direction: column;
  gap: var(--space-5);
}

.result-card {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--radius-xl);
  padding: var(--space-6) var(--space-8);
  box-shadow: var(--shadow-md);
}

.card-title {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  font-size: var(--text-base);
  font-weight: var(--weight-bold);
  color: var(--green-800);
  margin-bottom: var(--space-5);
  padding-bottom: var(--space-3);
  border-bottom: 1px solid var(--border);
}

.card-icon { font-size: var(--text-lg); color: var(--green-500); }

/* ── 資訊列 ── */
.info-grid { display: flex; flex-direction: column; gap: var(--space-3); }

.info-row {
  display: flex;
  gap: var(--space-4);
  align-items: flex-start;
}

.info-label {
  width: 90px;
  flex-shrink: 0;
  font-size: var(--text-sm);
  color: var(--text-muted);
  font-weight: var(--weight-medium);
  padding-top: var(--space-1);
}

.info-value {
  font-size: var(--text-base);
  color: var(--text-primary);
  flex: 1;
}

.description { line-height: var(--leading-normal); white-space: pre-wrap; }

/* ── 批次區間 ── */
.batch-range {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
  font-family: monospace;
  font-size: var(--text-sm);
  background: var(--green-50);
  border: 1px solid var(--border);
  border-radius: var(--radius-md);
  padding: var(--space-1) var(--space-3);
  flex: unset;
}

.range-arrow { font-size: var(--text-base); color: var(--text-muted); }

/* ── 狀態徽章 ── */
/* 標籤外殼已收進 base.css 的 .badge，這裡只留語意色 */
/* 這一顆長在資訊格裡，要脫離 .info-value 的等寬欄位規則才不會被拉長 */
.status-badge { align-self: flex-start; flex: unset; }

.status-badge.pass { background: var(--green-100); color: var(--green-600); }
.status-badge.fail { background: var(--warning-50); color: var(--warning-500); }

/* ── 農產品標籤 ── */
.product-list {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-3);
}

.product-tag {
  display: flex;
  flex-direction: column;
  gap: var(--space-1);
  background: var(--green-50);
  border: 1px solid var(--border);
  border-radius: var(--radius-lg);
  padding: var(--space-3) var(--space-4);
  min-width: 100px;
}

.product-name {
  font-size: var(--text-base);
  font-weight: var(--weight-bold);
  color: var(--green-800);
}

.product-place, .product-mark {
  font-size: var(--text-2xs);
  color: var(--text-muted);
}

/* ── 蛋農 / 牧場子區塊 ── */
.sub-section {
  margin-top: var(--space-5);
  padding-top: var(--space-4);
  border-top: 1px solid var(--border);
}

.sub-title {
  font-size: var(--text-sm);
  font-weight: var(--weight-bold);
  color: var(--text-muted);
  margin-bottom: var(--space-3);
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.farmer-list {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-3);
}

.farmer-tag {
  display: flex;
  flex-direction: column;
  gap: var(--space-1);
  background: var(--green-50);
  border: 1px solid var(--border);
  border-radius: var(--radius-lg);
  padding: var(--space-3) var(--space-4);
  min-width: 140px;
}

.farmer-name {
  font-size: var(--text-base);
  font-weight: var(--weight-bold);
  color: var(--green-800);
}

.farmer-type {
  font-size: var(--text-xs);
  color: var(--green-500);
  font-weight: var(--weight-medium);
}

.farmer-place {
  font-size: var(--text-2xs);
  color: var(--text-muted);
}
</style>