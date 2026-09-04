<template>
  <div class="page traceability-view">

    <QueryLayout
      title="農產品追溯查詢"
      title-en="TRACEABILITY"
      subtitle="輸入追溯碼，查詢蔬果、雞蛋、禽肉的產地與生產者資訊"
      filter-label="追溯碼"
      filter-label-en="TRACE CODE"
    >
      <template #actions>
        <Btn
          icon="mdi-magnify"
          :loading="store.isSearching"
          :disabled="!traceCode.trim()"
          @click="handleSearch"
        >{{ store.isSearching ? '查詢中...' : '查詢' }}</Btn>
      </template>

      <template #filters>
        <div class="field-group search-field">
          <label class="field-label" for="trace-code">追溯碼</label>
          <input
            id="trace-code"
            v-model="traceCode"
            class="form-control"
            inputmode="numeric"
            placeholder="請輸入追溯碼，例如：0101000005"
            @keyup.enter="handleSearch"
          />
        </div>
      </template>

      <!-- 說明區塊 -->
      <template #hint>
        <HintBox title="查詢說明" class="content-md">
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
      </template>

      <template #results>
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
      </template>
    </QueryLayout>

  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useFoodSafetyStore } from '@/stores/foodSafety'
import QueryLayout from '@/components/layouts/QueryLayout.vue'
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
/* 顏色全部改用 semantic 層（style tile §九）。
   查詢欄位是單一的 10 碼追溯碼，撐滿 1400px 的容器只會變成一個一千多像素長的輸入框，
   所以在這裡限寬——限的是欄位不是頁面容器（見 base.css .page 的註解）。 */
.search-field { flex: 0 1 var(--container-sm); min-width: 240px; }

/* 條列、範例鈕、chip 圖示已收進 base.css 共用 */
.chip-warn { background: var(--warning-50); color: var(--warning-700); margin-left: var(--space-1); }

/* ── 結果卡片 ── */
.result-section {
  display: flex;
  flex-direction: column;
  gap: var(--space-5);
}

.result-card {
  background: var(--color-surface);
  border: var(--border-width) solid var(--color-border);
  border-radius: var(--radius-xl);
  padding: var(--space-6) var(--space-8);
}

.card-title {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  font-size: var(--text-base);
  font-weight: var(--weight-bold);
  color: var(--color-text);
  margin-bottom: var(--space-5);
  padding-bottom: var(--space-3);
  border-bottom: var(--border-width) solid var(--color-border);
}

.card-icon { font-size: var(--text-lg); color: var(--color-brand); }

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
  color: var(--color-text-dim);
  font-weight: var(--weight-medium);
  padding-top: var(--space-1);
}

.info-value {
  font-size: var(--text-base);
  color: var(--color-text);
  flex: 1;
}

.description { line-height: var(--leading-normal); white-space: pre-wrap; }

/* ── 批次區間 ──
   追溯碼是要跟手上的包裝逐字比對的號碼，所以用等寬數字字型 --font-num，
   不用瀏覽器預設的 monospace（Windows 上是 Courier New，跟全站三套字無關）。 */
.batch-range {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
  font-family: var(--font-num);
  font-variant-numeric: tabular-nums;
  font-size: var(--text-sm);
  background: var(--color-bg-sunken);
  border: var(--border-width) solid var(--color-border);
  border-radius: var(--radius-md);
  padding: var(--space-1) var(--space-3);
  flex: unset;
}

.range-arrow { font-size: var(--text-base); color: var(--color-text-dim); }

/* ── 狀態徽章 ── */
/* 標籤外殼已收進 base.css 的 .badge，這裡只留語意色 */
/* 這一顆長在資訊格裡，要脫離 .info-value 的等寬欄位規則才不會被拉長 */
.status-badge { align-self: flex-start; flex: unset; }

.status-badge.pass { background: var(--color-action-soft-2); color: var(--color-action); }
.status-badge.fail { background: var(--warning-50); color: var(--warning-700); }

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
  background: var(--color-bg-sunken);
  border: var(--border-width) solid var(--color-border);
  border-radius: var(--radius-md);
  padding: var(--space-3) var(--space-4);
  min-width: 100px;
}

.product-name {
  font-size: var(--text-base);
  font-weight: var(--weight-bold);
  color: var(--color-text);
}

.product-place, .product-mark {
  font-size: var(--text-2xs);
  color: var(--color-text-dim);
}

/* ── 蛋農 / 牧場子區塊 ── */
.sub-section {
  margin-top: var(--space-5);
  padding-top: var(--space-4);
  border-top: var(--border-width) solid var(--color-border);
}

.sub-title {
  font-size: var(--text-xs);
  font-weight: var(--weight-medium);
  color: var(--color-text-dim);
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
  background: var(--color-bg-sunken);
  border: var(--border-width) solid var(--color-border);
  border-radius: var(--radius-md);
  padding: var(--space-3) var(--space-4);
  min-width: 140px;
}

.farmer-name {
  font-size: var(--text-base);
  font-weight: var(--weight-bold);
  color: var(--color-text);
}

.farmer-type {
  font-size: var(--text-xs);
  color: var(--color-brand);
  font-weight: var(--weight-medium);
}

.farmer-place {
  font-size: var(--text-2xs);
  color: var(--color-text-dim);
}
</style>