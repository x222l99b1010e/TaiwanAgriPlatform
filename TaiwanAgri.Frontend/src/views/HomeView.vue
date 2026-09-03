<!--
  src/views/HomeView.vue
  職責：新首頁。style tile §8.1 定案的固定四屏，`/` 不再 redirect 到市場行情。

  固定四屏、不放任何影片、不做長捲動（owner 2026-09-03：「不要塞太多影片，這樣會失焦」）：
  屏 1 深｜hero：一句話 ＋ 中英並排 ＋ CTA；右欄今日節氣牌；底部地平線漸層
  屏 2 淺｜三個今日數字（首頁唯一不能砍的東西——全靜態的首頁是一張海報，
         使用者第二次來會直接跳過；這三個數字本身就是最好的視覺主角）
  屏 3 淺｜四個模組入口卡
  屏 4 深｜資料來源與更新時間

  屏 1 用 EntryLayout（跟四個模組入口頁共用同一個殼），屏 2／3 放進它的預設插槽
  （兩者都是淺底，順著同一個 body 容器排下去就是一般的內容流）；屏 4 是獨立的深底
  footer，EntryLayout 的 body 目前只支援單一淺底區塊，硬塞第二種底色進去意義不大，
  這裡另外寫一小段。

  ⚠ 三個「今日數字」有一個跟 style tile 原始清單不同：收容動物地圖那格
  原本要的是「本週新進隻數」，但後端目前只有「收容所摘要」端點（一間收容所一筆
  彙總數字，沒有逐隻的 openDate 可篩本週），沒有「本週新進」這個聚合可以一次查到。
  逐隻抓全部收容所（約 30 間）再前端過濾本週會變成每次開首頁打 30 支 API，划不來。
  這裡換成「全台在養動物總數」（沿用收容動物地圖已經在用的同一支聚合端點），
  同樣是「這個模組現在有多少事」的即時數字，只是換了一個現成、單一請求就拿得到的量。
-->
<template>
  <div class="home-view">
    <EntryLayout
      title="今天的田，有多少數字"
      title-en="Today's Field, in Numbers"
      title-size="display"
      :eyebrow="`禾 ${solarTerm.current.zh}｜${solarTerm.current.en}`"
      lead="行情、氣象、食安、動物——四個模組一次看，資料一律來自政府開放資料。"
    >
      <template #motif>
        <SeasonMotif :season="solarTerm.current.season" />
        <!-- 底部地平線漸層：hero 收尾在往下一屏（淺底）之前先過渡一段，
             不是直接切一刀——深底跟淺底中間留一段「傍晚天光」的漸層感 -->
        <div class="hero-horizon" />
      </template>

      <template #cta>
        <RouterLink to="/market/prices" class="hero-cta">
          開始查詢
          <span class="mdi mdi-arrow-right" />
        </RouterLink>
      </template>

      <template #aside>
        <div class="term-card">
          <Bilingual zh="今日節氣" en="SOLAR TERM" layout="inline" tone="deep" class="term-card__label" />
          <p class="term-card__zh">{{ solarTerm.current.zh }}</p>
          <p class="term-card__en">{{ solarTerm.current.en }}</p>
          <div class="term-card__divider" />
          <div class="term-card__row">
            <span>下一個節氣</span>
            <span>{{ solarTerm.next.zh }} · {{ nextTermDateLabel }}</span>
          </div>
          <div class="term-card__row">
            <span>距今</span>
            <span>{{ solarTerm.daysUntilNext }} 天</span>
          </div>
        </div>
      </template>

      <!-- 屏 2：三個今日數字 -->
      <section class="stats-screen">
        <h2 class="screen-title">今天的三個數字</h2>
        <div class="stat-grid">
          <div class="stat-tile" v-for="s in statTiles" :key="s.key">
            <span class="stat-tile__label">{{ s.label }}</span>
            <span class="stat-tile__value">
              <template v-if="s.loading">—</template>
              <template v-else>{{ s.display }}<span class="stat-tile__unit">{{ s.unit }}</span></template>
            </span>
            <span class="stat-tile__hint">{{ s.hint }}</span>
          </div>
        </div>
      </section>

      <!-- 屏 3：四個模組入口卡 -->
      <section class="entry-screen">
        <h2 class="screen-title">四個模組</h2>
        <div class="module-grid">
          <RouterLink
            v-for="m in moduleCards"
            :key="m.route"
            :to="m.route"
            class="module-card"
          >
            <span class="mdi module-card__icon" :class="m.icon" />
            <h3 class="module-card__title">
              <Bilingual :zh="m.name" :en="m.nameEn" />
            </h3>
            <p class="module-card__lead">{{ m.lead }}</p>
            <span class="module-card__arrow mdi mdi-arrow-right" />
          </RouterLink>
        </div>
      </section>
    </EntryLayout>

    <!-- 屏 4：資料來源與更新時間 -->
    <footer class="home-footer">
      <p class="home-footer__text">
        資料來源：行政院農業部開放資料。各模組依各自排程更新，不是同一支資料，
        實際時間以各頁查詢結果為準。
      </p>
    </footer>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive } from 'vue'
import { RouterLink } from 'vue-router'
import EntryLayout from '@/components/layouts/EntryLayout.vue'
import Bilingual from '@/components/ui/Bilingual.vue'
import SeasonMotif from '@/components/SeasonMotif.vue'
import { marketApi } from '@/api/market'
import { weatherApi } from '@/api/weather'
import { petApi } from '@/api/pet'
import { useNavStore } from '@/stores/nav'
import { useCountUp } from '@/composables/useCountUp'
import { getTodaySolarTerm } from '@/utils/solarTerms'

const solarTerm = getTodaySolarTerm()
const nextTermDateLabel = `${solarTerm.next.month}/${solarTerm.next.day}`

const navStore = useNavStore()

// 四個模組卡片的英文定譯與一句話說明——英文全站唯一，集中在這裡；
// 名稱／路由／圖示直接沿用導覽列已經在讀的 navStore.modules（後端種子），
// 不在這裡重複寫一次，換路由或改圖示時只要動後端種子，這裡自動跟著換
const MODULE_EN: Record<string, string> = {
  '市場行情': 'MARKET PRICES',
  '青農戰情室': 'SITUATION ROOM',
  '食安透明網': 'FOOD SAFETY',
  '毛小孩地圖': 'COMPANION ANIMALS',
}
const MODULE_LEAD: Record<string, string> = {
  '市場行情': '作物、毛豬、家禽的產地與批發行情，一次比對',
  '青農戰情室': '氣象站觀測、雨量趨勢與病蟲害警報地圖',
  '食安透明網': '農產追溯、農藥違規與有機驗證查詢',
  '毛小孩地圖': '收容動物地圖與遺失協尋',
}
const moduleCards = computed(() =>
  navStore.modules.map(m => ({
    route: m.route,
    icon: m.icon,
    name: m.name,
    nameEn: MODULE_EN[m.name] ?? '',
    lead: MODULE_LEAD[m.name] ?? '',
  }))
)

// ── 三個今日數字 ─────────────────────────────────────────────────────────
interface StatTile {
  key: string
  label: string
  unit: string
  hint: string
  loading: boolean
  // reactive() 會把巢狀的 Ref 攤平成裸值，所以這裡的型別是 number 不是 Ref<number>——
  // useCountUp() 回傳 Ref 是給元件外部用 .value 存取，包進 reactive() 之後模板直接讀
  // s.display 就是最新值，不用再多一層 .value
  display: number
  start: ReturnType<typeof useCountUp>['start']
}

function makeTile(key: string, label: string, unit: string, hint: string): StatTile {
  const { value, start } = useCountUp()
  return reactive({ key, label, unit, hint, loading: true, display: value, start })
}

const statTiles: StatTile[] = [
  makeTile('egg', '今日雞蛋產地均價', '元', '公斤裝、農業部產地行情'),
  makeTile('pest', '生效中病蟲害警報', '則', '全台縣市加總，不分等級'),
  makeTile('pet', '全台在養動物', '隻', '收容所目前在養總數'),
]

async function loadStats() {
  const [egg, pest, pet] = statTiles

  // 雞蛋產地均價：抓最近 7 天，取最新一筆「正常報價」——蛋價不是每天都報，
  // 抓區間再挑最新，比只查「今天」穩，跟 PoultryView 的容錯邏輯同一個道理
  try {
    const today = new Date().toISOString().split('T')[0]!
    const weekAgo = new Date(Date.now() - 7 * 24 * 60 * 60 * 1000).toISOString().split('T')[0]!
    const rows = await marketApi.getPoultry({ startDate: weekAgo, endDate: today })
    const latest = rows
      .filter(r => r.metricCode === 'Egg_Producer' && r.priceStatus === 'Normal' && r.price != null)
      .sort((a, b) => b.transDate.localeCompare(a.transDate))[0]
    egg!.loading = false
    egg!.start(latest?.price ?? 0)
  } catch {
    egg!.loading = false
  }

  // 生效中的病蟲害警報則數：只要總筆數，pageSize=1 就夠，不用把資料本身撈回來
  try {
    const result = await weatherApi.getPestAlerts(undefined, 1, 1)
    pest!.loading = false
    pest!.start(result.totalCount)
  } catch {
    pest!.loading = false
  }

  // 全台在養動物總數：沿用收容動物地圖同一支聚合端點，summary 每列是一間收容所，
  // totalCount 加總即為全台在養總數
  try {
    const summaries = await petApi.getShelterAnimalSummary({})
    pet!.loading = false
    pet!.start(summaries.reduce((sum, s) => sum + s.totalCount, 0))
  } catch {
    pet!.loading = false
  }
}

onMounted(() => {
  navStore.loadModules()
  loadStats()
})
</script>

<style scoped>
/* ── hero：底部地平線漸層 ───────────────────────────────────────────── */
.hero-horizon {
  position: absolute;
  inset-inline: 0;
  bottom: 0;
  height: 140px;
  /* 目標色是屏 2 的底色 --color-bg：這道漸層存在的理由就是讓深底收尾時
     已經開始靠近下一屏的顏色，兩屏之間才不是硬切一刀 */
  background: linear-gradient(to bottom, transparent, var(--color-bg));
}

/* ── hero：CTA 與節氣牌 ─────────────────────────────────────────────── */
.hero-cta {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
  padding: var(--space-3) var(--space-6);
  border-radius: var(--radius-full);
  background: var(--color-action-on-deep);
  color: var(--color-deep);
  font-weight: var(--weight-bold);
  font-size: var(--text-sm);
  text-decoration: none;
  transition: transform var(--duration-fast) var(--ease-work), box-shadow var(--duration-fast) var(--ease-work);
}
.hero-cta:hover { transform: translateY(calc(var(--lift-work) * -1)); box-shadow: var(--shadow-float); }

.term-card {
  width: 280px;
  padding: var(--space-6);
  border-radius: var(--radius-xl);
  background: var(--color-deep-surface);
  border: 1px solid var(--color-deep-border);
}
.term-card__label { color: var(--color-on-deep-dim); }
.term-card__zh {
  margin-top: var(--space-4);
  font-family: var(--font-display);
  font-weight: 700;
  font-size: var(--text-5xl);
  line-height: var(--leading-tight);
  color: var(--color-action-on-deep);
}
.term-card__en {
  margin-top: var(--space-2);
  font-family: var(--font-num);
  font-size: var(--text-sm);
  color: var(--color-on-deep-dim);
}
.term-card__divider { margin: var(--space-5) 0; height: 1px; background: var(--color-deep-border); }
.term-card__row {
  display: flex;
  justify-content: space-between;
  gap: var(--space-4);
  font-size: var(--text-sm);
  color: var(--color-on-deep-dim);
}
.term-card__row + .term-card__row { margin-top: var(--space-2); }
.term-card__row span:last-child { color: var(--color-on-deep); font-weight: var(--weight-medium); }

/* ── 屏 2／3 共用的標題 ─────────────────────────────────────────────── */
.screen-title {
  font-size: var(--text-2xl);
  font-weight: var(--weight-bold);
  color: var(--color-text);
  margin-bottom: var(--space-8);
}
.entry-screen { margin-top: var(--space-16); }

/* ── 屏 2：三個今日數字 ─────────────────────────────────────────────── */
.stat-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: var(--space-6);
}
.stat-tile {
  padding: var(--space-8);
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-xl);
  box-shadow: var(--shadow-sm);
}
.stat-tile__label {
  display: block;
  font-size: var(--text-sm);
  color: var(--color-text-dim);
  font-weight: var(--weight-medium);
}
.stat-tile__value {
  display: block;
  margin-top: var(--space-3);
  font-family: var(--font-num);
  font-size: var(--text-6xl);
  font-weight: var(--weight-bold);
  color: var(--color-brand);
  font-variant-numeric: tabular-nums;
  line-height: 1;
}
.stat-tile__unit { margin-left: var(--space-2); font-size: var(--text-lg); color: var(--color-text-dim); }
.stat-tile__hint { display: block; margin-top: var(--space-3); font-size: var(--text-xs); color: var(--color-text-dim); }

/* ── 屏 3：四個模組入口卡 ───────────────────────────────────────────── */
.module-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: var(--space-6);
}
.module-card {
  position: relative;
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
  padding: var(--space-8) var(--space-6);
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-xl);
  text-decoration: none;
  color: inherit;
  transition: transform var(--duration-base) var(--ease-work), box-shadow var(--duration-base) var(--ease-work), border-color var(--duration-base);
}
.module-card:hover {
  transform: translateY(calc(var(--lift-work) * -1));
  box-shadow: var(--shadow-float);
  border-color: var(--seed-300);
}
.module-card__icon { font-size: var(--text-3xl); color: var(--color-action); }
.module-card__title { font-size: var(--text-lg); font-weight: var(--weight-bold); color: var(--color-text); }
.module-card__lead { font-size: var(--text-sm); color: var(--color-text-dim); line-height: var(--leading-normal); }
.module-card__arrow {
  position: absolute;
  right: var(--space-6);
  bottom: var(--space-6);
  color: var(--color-text-dim);
  transition: transform var(--duration-fast) var(--ease-work);
}
.module-card:hover .module-card__arrow { transform: translateX(var(--lift-work)); color: var(--color-action); }

/* ── 屏 4：資料來源 ─────────────────────────────────────────────────── */
.home-footer {
  background: var(--color-deep);
  color: var(--color-on-deep-dim);
  padding: var(--space-10) var(--page-padding-x);
}
.home-footer__text {
  max-width: var(--container-lg);
  margin-inline: auto;
  font-size: var(--text-xs);
  line-height: var(--leading-normal);
}

@media (max-width: 960px) {
  .stat-grid { grid-template-columns: 1fr; }
  .module-grid { grid-template-columns: repeat(2, 1fr); }
}

@media (max-width: 640px) {
  .module-grid { grid-template-columns: 1fr; }
}
</style>
