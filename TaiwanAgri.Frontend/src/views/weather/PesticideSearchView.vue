<template>
  <div class="page pesticide-view">

    <PageHeader
      title="農藥查詢"
      subtitle="輸入農藥成分名稱，查詢許可證狀態、適用作物與安全採收期"
    />

    <!-- 說明區塊 -->
    <HintBox title="查詢說明" class="page-hint">
      <ul class="hint-list">
        <li>查的是「有效成分」的名稱（如「亞滅培」），不是商品名（如「冠天下」）</li>
        <li>同一個成分會有多張許可證＝市面上多個廠牌，但核准用途取決於成分、含量與劑型</li>
        <li>名稱為模糊比對，可能一併查到名字相近的其他成分，請確認卡片標題是否為所需藥劑</li>
        <li>預設只顯示未廢止的許可證，可勾選下方選項一併查看已廢止的</li>
      </ul>
      <div class="example-row">
        <span class="example-label">範例：</span>
        <button class="example-chip" @click="fillExample('亞滅培')">
          <span class="mdi mdi-bug chip-icon" />亞滅培（殺蟲劑）
        </button>
        <button class="example-chip" @click="fillExample('撲殺熱')">
          <span class="mdi mdi-mushroom chip-icon" />撲殺熱（殺菌劑）
        </button>
        <button class="example-chip example-chip--danger" @click="fillExample('達馬松')">
          <span class="mdi mdi-cancel chip-icon" />達馬松
          <span class="badge chip-warn">已禁用</span>
        </button>
      </div>
    </HintBox>

    <!-- 搜尋列：中英文兩個獨立欄位 -->
    <div class="search-bar">
      <div class="search-field">
        <label class="field-label">中文成分名</label>
        <input
          v-model="keyword"
          class="search-input"
          placeholder="例如：亞滅培"
          @keyup.enter="handleSearch"
        />
      </div>
      <div class="search-field">
        <label class="field-label">英文成分名</label>
        <input
          v-model="englishName"
          class="search-input"
          :class="{ 'search-input--invalid': englishNameError }"
          placeholder="例如：ACETAMIPRID"
          @keyup.enter="handleSearch"
        />
        <span v-if="englishNameError" class="field-error">{{ englishNameError }}</span>
      </div>
      <Btn
        icon="mdi-magnify"
        :loading="isSearching"
        :disabled="!canSearch"
        @click="handleSearch"
      >{{ isSearching ? '查詢中...' : '查詢' }}</Btn>
    </div>

    <label class="revoked-toggle">
      <input v-model="includeRevoked" type="checkbox" @change="handleToggleRevoked" />
      <span>一併顯示已廢止的許可證</span>
    </label>

    <StateBlock v-if="isSearching" state="loading" message="查詢中..." />
    <StateBlock
      v-else-if="searchError"
      state="error"
      :message="searchError"
      retryable
      @retry="handleSearch"
    />
    <StateBlock
      v-else-if="result && result.ingredients.length === 0"
      state="empty"
      message="查無符合的農藥成分"
      hint="請確認輸入的是成分名稱而非商品名，或試著只輸入前兩個字"
    />

    <!-- 結果 -->
    <div v-else-if="result" class="result-section">

      <div v-if="result.ingredients.length > 1" class="multi-hint">
        <span class="mdi mdi-alert-outline" />
        查詢條件命中 {{ result.ingredients.length }} 種成分，請確認下方是否為所需藥劑——名稱相近的成分是不同的農藥。
      </div>

      <!-- ── 第一層：成分 ── -->
      <div
        v-for="ingredient in result.ingredients"
        :key="ingredient.pesticideCode"
        class="result-card"
      >
        <div class="ingredient-header">
          <div class="ingredient-title">
            <span class="ingredient-name">{{ ingredient.chineseName }}</span>
            <span class="ingredient-en">{{ ingredient.englishName }}</span>
            <span v-if="ingredient.isExactMatch" class="badge badge--exact">完全符合</span>
          </div>
          <div class="ingredient-meta">
            <span v-if="ingredient.category" class="badge badge--category">{{ ingredient.category }}</span>
            <span v-if="ingredient.chemicalType" class="badge badge--type">{{ ingredient.chemicalType }}</span>
            <span class="code-text">{{ ingredient.pesticideCode }}</span>
          </div>
        </div>

        <!-- ── 第二層：劑型分頁 ── -->
        <div class="form-tabs">
          <button
            v-for="(formulation, index) in ingredient.formulations"
            :key="`${formulation.formCode}-${formulation.contents}`"
            class="form-tab"
            :class="{
              'form-tab--active': activeFormIndex(ingredient.pesticideCode) === index,
              'form-tab--technical': formulation.isTechnicalGrade,
            }"
            @click="selectForm(ingredient.pesticideCode, index)"
          >
            <span class="form-tab-name">{{ formulation.formName }}</span>
            <span class="form-tab-contents">{{ formulation.contents }}</span>
            <span class="form-tab-count">{{ formulation.licenses.length }} 張證</span>
          </button>
        </div>

        <!-- 選中的劑型內容 -->
        <template v-for="(formulation, index) in ingredient.formulations" :key="index">
          <div v-if="activeFormIndex(ingredient.pesticideCode) === index" class="form-panel">

            <!-- 原體說明 -->
            <div v-if="formulation.isTechnicalGrade" class="notice-box">
              <span class="mdi mdi-flask-outline" />
              這是「原體」——供加工製造用的高濃度原料，不是農民可直接施用的產品，因此沒有核准用途資料。
            </div>

            <!-- 核准用途抓取失敗 -->
            <div v-else-if="!formulation.usagesAvailable" class="notice-box notice-box--warn">
              <span class="mdi mdi-cloud-alert" />
              核准用途資料這次沒有取得成功（不代表沒有），請稍後重新查詢。
            </div>

            <!-- 核准用途 -->
            <div v-else class="usage-block">
              <div class="block-header">
                <div class="block-title">
                  <span class="mdi mdi-sprout card-icon" />
                  核准用途
                  <span class="block-count">{{ formulation.usages.length }} 筆</span>
                </div>
                <input
                  v-if="formulation.usages.length > 8"
                  v-model="cropFilter"
                  class="filter-input"
                  placeholder="篩選作物或病蟲害，例如：番茄"
                />
              </div>

              <div v-if="formulation.usages.length === 0" class="inline-empty">
                此劑型目前沒有核准用途資料
              </div>

              <div v-else class="table-scroll">
                <table class="data-table data-table--compact usage-table">
                  <thead>
                    <tr>
                      <th>作物</th>
                      <th>病蟲害</th>
                      <th>稀釋倍數</th>
                      <th>每公頃用藥量</th>
                      <th class="col-highlight">安全採收期</th>
                      <th>使用時期</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr v-for="(usage, i) in filterUsages(formulation.usages)" :key="i">
                      <td class="cell-strong">{{ usage.cropName }}</td>
                      <td>{{ usage.pestName }}</td>
                      <td>{{ usage.dilution || '—' }}</td>
                      <td>{{ usage.dosagePerHectare || '—' }}</td>
                      <td class="col-highlight cell-strong">{{ usage.safeHarvestInterval || '—' }}</td>
                      <td class="cell-note">{{ usage.applicationTiming || '—' }}</td>
                    </tr>
                  </tbody>
                </table>
              </div>

              <div
                v-if="formulation.usages.length > 0 && filterUsages(formulation.usages).length === 0"
                class="inline-empty"
              >
                沒有符合「{{ cropFilter }}」的核准用途
              </div>
            </div>

            <!-- ── 第三層：許可證 ── -->
            <div class="license-block">
              <button class="block-toggle" @click="toggleLicenses(ingredient.pesticideCode, index)">
                <span
                  class="mdi"
                  :class="isLicenseOpen(ingredient.pesticideCode, index) ? 'mdi-chevron-down' : 'mdi-chevron-right'"
                />
                市售產品與許可證
                <span class="block-count">{{ formulation.licenses.length }} 張</span>
              </button>

              <div v-if="isLicenseOpen(ingredient.pesticideCode, index)" class="table-scroll">
                <table class="data-table data-table--compact license-table">
                  <thead>
                    <tr>
                      <th>商品名</th>
                      <th>廠商</th>
                      <th>許可證字號</th>
                      <th>有效期限</th>
                      <th>狀態</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr v-for="license in formulation.licenses" :key="license.permit + license.permitNumber">
                      <td class="cell-strong">{{ license.brandName || '—' }}</td>
                      <td>{{ license.vendor || license.foreignMaker || '—' }}</td>
                      <td class="cell-mono">{{ license.permit }}{{ license.permitNumber }}</td>
                      <td class="cell-mono">{{ license.expireDateRoc || '—' }}</td>
                      <td>
                        <!-- 廢止與過期是兩個獨立狀態，可能同時成立，所以不是 v-else -->
                        <span v-if="license.isRevoked" class="badge badge--revoked">
                          {{ license.revocationType }}
                        </span>
                        <span v-if="license.isExpired" class="badge badge--expired">已逾有效期限</span>
                        <span v-if="!license.isRevoked && !license.isExpired" class="badge badge--valid">
                          有效
                        </span>
                      </td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </div>

          </div>
        </template>
      </div>
    </div>

    <!-- 初始提示 -->
    <StateBlock
      v-else
      state="hint"
      icon="mdi-flask-outline"
      message="請輸入農藥成分名稱開始查詢"
      hint="上方有三組範例可以直接點來試"
    />

  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import axios from 'axios'
import { weatherApi, type PesticideSearchResult, type PesticideUsage } from '@/api/weather'
import { useLatestRequest } from '@/composables/useLatestRequest'
import PageHeader from '@/components/ui/PageHeader.vue'
import StateBlock from '@/components/ui/StateBlock.vue'
import Btn from '@/components/ui/Btn.vue'
import HintBox from '@/components/ui/HintBox.vue'

// 模組 2 既有的四個 view（測站／雨量／病蟲害預警／旬密度）都是直接呼叫 weatherApi、
// 不經過 Pinia store——這些畫面的資料是「查一次、只有本頁用、離開就丟」，
// 沒有跨元件共享需求。農藥查詢完全同型，因此跟隨既有慣例不另外開 store。

const keyword = ref('')
const englishName = ref('')
const includeRevoked = ref(false)

const result = ref<PesticideSearchResult | null>(null)
const isSearching = ref(false)
const searchError = ref<string | null>(null)

// 核准用途可能多達 100 多列（實測亞滅培 184 列、105 種作物），
// 沒有篩選的話使用者要在整張表裡目視找自己種的作物。
// 篩選字串共用一份而非每張表各自一份：同一時間只會展開一個劑型面板，
// 拆成多份反而會讓使用者切換劑型時發現篩選條件不見了。
const cropFilter = ref('')

// 每個成分各自記住「目前選到第幾個劑型」，用成分代碼當 key。
// 不用陣列索引當 key 的原因：重新查詢後成分順序會變，用索引會讓選取狀態錯位到別的成分上。
const selectedForm = ref<Record<string, number>>({})

// 許可證清單預設收合（多數使用者要看的是能用在什麼作物，不是有哪幾家廠商在賣），
// key 用「成分代碼 + 劑型索引」組合，讓不同劑型的展開狀態互相獨立。
const openLicenses = ref<Record<string, boolean>>({})

// 請求序號防競態：使用者連續改條件按查詢時，較早發出的請求可能較晚回來
const searchRequest = useLatestRequest()

/** 英文欄位的字元白名單，與後端 PesticideSearchQueryDto 同一套規則。
 *  前端擋是為了即時回饋，後端仍會再擋一次——前端驗證是體驗，不是防線。 */
const ENGLISH_NAME_PATTERN = /^(?=.*[A-Za-z])[A-Za-z0-9 +\-,.'()/]+$/

const englishNameError = computed(() => {
  const value = englishName.value.trim()
  if (!value) return ''
  return ENGLISH_NAME_PATTERN.test(value)
    ? ''
    : '只能輸入英文字母、數字與 + - , . \' ( ) / 等符號'
})

const canSearch = computed(() =>
  (keyword.value.trim() !== '' || englishName.value.trim() !== '') && !englishNameError.value,
)

function activeFormIndex(pesticideCode: string): number {
  return selectedForm.value[pesticideCode] ?? 0
}

function selectForm(pesticideCode: string, index: number) {
  selectedForm.value[pesticideCode] = index
  // 切換劑型時清掉作物篩選：上一個劑型的篩選字串套到新劑型多半是空結果，
  // 使用者會誤以為新劑型沒有核准用途
  cropFilter.value = ''
}

function licenseKey(pesticideCode: string, index: number) {
  return `${pesticideCode}#${index}`
}

function isLicenseOpen(pesticideCode: string, index: number): boolean {
  return openLicenses.value[licenseKey(pesticideCode, index)] === true
}

function toggleLicenses(pesticideCode: string, index: number) {
  const key = licenseKey(pesticideCode, index)
  openLicenses.value[key] = !openLicenses.value[key]
}

/** 作物與病蟲害兩個欄位一起比對——使用者可能想查「我種番茄」也可能想查「防治粉蝨」 */
function filterUsages(usages: PesticideUsage[]): PesticideUsage[] {
  const term = cropFilter.value.trim()
  if (!term) return usages
  return usages.filter(u => u.cropName.includes(term) || u.pestName.includes(term))
}

function fillExample(name: string) {
  keyword.value = name
  englishName.value = ''
  handleSearch()
}

function handleToggleRevoked() {
  // 已經查過才重查，否則勾選這個選項會在什麼都還沒輸入時就打一次 API
  if (result.value || searchError.value) handleSearch()
}

async function handleSearch() {
  if (!canSearch.value) return

  const mySeq = searchRequest.next()
  isSearching.value = true
  searchError.value = null
  // 清掉上一次查詢的展開／篩選狀態，避免新結果沿用舊的選取位置
  selectedForm.value = {}
  openLicenses.value = {}
  cropFilter.value = ''

  try {
    const data = await weatherApi.searchPesticides(
      keyword.value.trim(),
      englishName.value.trim(),
      includeRevoked.value,
    )
    if (!searchRequest.isLatest(mySeq)) return
    result.value = data
  } catch (e) {
    if (!searchRequest.isLatest(mySeq)) return
    result.value = null
    // 後端的 400 帶著可以直接顯示給使用者的中文訊息（關鍵字過廣、英文名格式錯誤等），
    // 一律代換成「查詢失敗」會把「你只要把關鍵字打完整就好」這個資訊丟掉
    if (axios.isAxiosError(e) && e.response?.status === 400 && typeof e.response.data === 'string') {
      searchError.value = e.response.data
    } else {
      searchError.value = '查詢失敗，請稍後再試'
    }
    console.error(e)
  } finally {
    if (searchRequest.isLatest(mySeq)) isSearching.value = false
  }
}
</script>

<style scoped>
.page-hint { margin-bottom: var(--space-5); }

/* 條列、範例鈕、chip 圖示已收進 base.css 共用 */
.chip-warn { background: var(--danger-50); color: var(--danger-500); margin-left: var(--space-1); }

/* ── 搜尋列 ── */
.search-bar {
  display: flex;
  gap: var(--space-3);
  align-items: flex-end;
  margin-bottom: var(--space-3);
}

.search-field { flex: 1; display: flex; flex-direction: column; gap: var(--space-1); }

.field-label {
  font-size: var(--text-xs);
  font-weight: var(--weight-bold);
  color: var(--neutral-400);
}

.search-input {
  padding: var(--space-3) var(--space-4);
  border: 1px solid var(--neutral-200);
  border-radius: var(--radius-md);
  font-size: var(--text-base);
  color: var(--neutral-900);
  background: var(--neutral-0);
  outline: none;
  transition: border-color var(--duration-fast), box-shadow var(--duration-fast);
}

.search-input:focus {
  border-color: var(--green-600);
  box-shadow: var(--shadow-focus);
}

.search-input--invalid { border-color: var(--danger-500); }
.search-input--invalid:focus { border-color: var(--danger-500); box-shadow: var(--shadow-focus-danger); }

.field-error { font-size: var(--text-2xs); color: var(--danger-500); }
.revoked-toggle {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
  font-size: var(--text-sm);
  color: var(--neutral-400);
  cursor: pointer;
  margin-bottom: var(--space-6);
}

/* ── 狀態容器 ── */
/* ── 結果 ── */
.result-section { display: flex; flex-direction: column; gap: var(--space-5); }

.multi-hint {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  background: var(--warning-50);
  border: 1px solid var(--warning-100);
  border-radius: var(--radius-lg);
  padding: var(--space-3) var(--space-5);
  font-size: var(--text-sm);
  color: var(--warning-500);
}

.result-card {
  background: var(--neutral-0);
  border: 1px solid var(--neutral-200);
  border-radius: var(--radius-xl);
  padding: var(--space-6) var(--space-8);
  box-shadow: var(--shadow-md);
}

/* ── 成分標頭 ── */
.ingredient-header {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-3);
  padding-bottom: var(--space-4);
  border-bottom: 1px solid var(--neutral-200);
}

.ingredient-title { display: flex; align-items: baseline; gap: var(--space-3); flex-wrap: wrap; }

.ingredient-name { font-size: var(--text-lg); font-weight: var(--weight-bold); color: var(--green-800); }
.ingredient-en { font-size: var(--text-sm); color: var(--neutral-400); font-family: monospace; }

.ingredient-meta { display: flex; align-items: center; gap: var(--space-2); }

.code-text { font-size: var(--text-xs); color: var(--neutral-400); font-family: monospace; }

/* 標籤外殼已收進 base.css 的 .badge，這裡只留語意色 */
.badge--exact { background: var(--green-100); color: var(--green-600); }
.badge--category { background: var(--info-50); color: var(--info-500); }
.badge--type { background: var(--purple-50); color: var(--purple-500); }
.badge--valid { background: var(--green-100); color: var(--green-600); }
.badge--revoked { background: var(--danger-50); color: var(--danger-500); margin-right: var(--space-1); }
.badge--expired { background: var(--warning-50); color: var(--warning-500); }

/* ── 劑型分頁 ── */
.form-tabs {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
  margin: var(--space-4) 0 var(--space-5);
}

.form-tab {
  display: flex;
  flex-direction: column;
  gap: var(--space-1);
  align-items: flex-start;
  padding: var(--space-2) var(--space-4);
  border-radius: var(--radius-lg);
  border: 1px solid var(--neutral-200);
  background: var(--neutral-0);
  cursor: pointer;
  transition: all var(--duration-fast);
}

.form-tab:hover { border-color: var(--green-400); background: var(--green-50); }

.form-tab--active {
  border-color: var(--green-600);
  background: var(--green-50);
  box-shadow: var(--shadow-focus);
}

.form-tab--technical { opacity: 0.75; }

.form-tab-name { font-size: var(--text-sm); font-weight: var(--weight-bold); color: var(--green-800); }
.form-tab-contents { font-size: var(--text-2xs); color: var(--neutral-400); font-family: monospace; }
.form-tab-count { font-size: var(--text-2xs); color: var(--neutral-400); }

/* ── 劑型面板 ── */
.form-panel { display: flex; flex-direction: column; gap: var(--space-5); }

.notice-box {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  background: var(--neutral-100);
  border: 1px solid var(--neutral-200);
  border-radius: var(--radius-lg);
  padding: var(--space-4) var(--space-5);
  font-size: var(--text-sm);
  color: var(--neutral-400);
}

.notice-box--warn { background: var(--warning-50); border-color: var(--warning-100); color: var(--warning-500); }

.block-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-3);
  flex-wrap: wrap;
  margin-bottom: var(--space-3);
}

.block-title {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  font-size: var(--text-base);
  font-weight: var(--weight-bold);
  color: var(--green-800);
}

.card-icon { font-size: var(--text-lg); color: var(--green-500); }

.block-count {
  font-size: var(--text-2xs);
  font-weight: var(--weight-medium);
  color: var(--neutral-400);
  background: var(--green-50);
  border-radius: var(--radius-full);
  padding: var(--space-1) var(--space-2);
}

.filter-input {
  padding: var(--space-2) var(--space-3);
  border: 1px solid var(--neutral-200);
  border-radius: var(--radius-md);
  font-size: var(--text-sm);
  min-width: 220px;
  outline: none;
  background: var(--neutral-0);
  color: var(--neutral-900);
}

.filter-input:focus { border-color: var(--green-600); }

.inline-empty {
  font-size: var(--text-sm);
  color: var(--neutral-400);
  padding: var(--space-4) 0;
  text-align: center;
}

/* ── 表格 ── */
.table-scroll { overflow-x: auto; }

/* 表格外殼已收進 base.css 的 .data-table，這裡只留這一頁真正不同的部分 */

.cell-strong { font-weight: var(--weight-medium); }
.cell-mono { font-family: monospace; font-size: var(--text-xs); white-space: nowrap; }
.cell-note { color: var(--neutral-400); font-size: var(--text-xs); line-height: var(--leading-normal); }

/* 安全採收期是本功能最關鍵的欄位（用錯會農藥殘留超標），視覺上獨立標示 */
.col-highlight { background: var(--green-50); }

/* ── 許可證區塊 ── */
.license-block { border-top: 1px solid var(--neutral-200); padding-top: var(--space-4); }

.block-toggle {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  background: none;
  border: none;
  padding: 0 0 var(--space-3);
  font-size: var(--text-base);
  font-weight: var(--weight-bold);
  color: var(--green-800);
  cursor: pointer;
}

.block-toggle .mdi { font-size: var(--text-lg); }
</style>
