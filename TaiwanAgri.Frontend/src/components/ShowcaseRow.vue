<!--
  src/components/ShowcaseRow.vue
  職責：左右交錯的「特寫列」——一塊深色視覺（大號序號＋圖示＋hover 特效）
  加一段文案（柿橙 eyebrow＋襯線大標＋說明＋進入），整列是一個連結。

  為什麼抽成元件：首頁屏 3 與四個模組入口頁要的是同一種列。P3 當時只做了首頁，
  排版與動效直接寫在 HomeView 的 scoped CSS 裡（約 120 行）；補做入口頁時若照抄一份，
  就是這個專案的頭號技術債形態（共用抽象只套一處）再犯一次。

  size 只有兩階，對應資訊層級而不是畫面大小：
  'feature'＝模組本身（首頁用），'compact'＝模組底下的子頁（入口頁用）。
  子頁比模組低一階，列就該小一階——兩邊同樣大會讓層級消失。

  flip 由呼叫端給，不由元件自己算索引：入口頁的錯落還會依奇偶再加縱向位移，
  兩個效果要吃同一個判斷，交錯的依據放在呼叫端才不會各算各的。

  ⚠ hover 特效五選一，而且四個模組各用不同的一種（對照表在 constants/navCopy.ts）。
  理由不是「多做幾個比較炫」：同一款光點放在四個模組上，看久了等於沒有特效——
  它只證明這裡有動畫，沒有說出這一塊在做什麼。改成跟內容有關之後，
  行情是走勢線、氣象是雨絲、食安是掃描、寵物是腳印，特效本身就是那個模組的識別。
  全部只在 hover 時播，平常靜止；`prefers-reduced-motion` 由 base.css 的全域規則統一歸零。
-->
<template>
  <RouterLink :to="to" class="showcase-row" :class="[`showcase-row--${size}`, { 'showcase-row--flip': flip }]">
    <div class="showcase-visual">
      <!-- 序號純粹是版面節奏用的，跟旁邊的特效層一樣要對螢幕閱讀器隱藏，
           否則每個連結都會被念成「01 CROP PRICES 行情查詢……」 -->
      <span class="showcase-visual__num" aria-hidden="true">{{ String(index).padStart(2, '0') }}</span>
      <span class="mdi showcase-visual__icon" :class="icon" />

      <!-- 特效層一律純裝飾，對螢幕閱讀器隱藏 -->
      <span class="fx" aria-hidden="true">
        <!-- 光點：預設款，往上漂再淡出 -->
        <template v-if="effect === 'sparks'">
          <span v-for="(s, i) in SPARKS" :key="i" class="fx-spark" :style="sparkVars(s)" />
        </template>

        <!-- 走勢線（市場行情）：三條淡刻度線＋一條 hover 時自己畫出來的折線 -->
        <!-- viewBox 的 160×90 就是圖塊的 16/9，所以不需要 preserveAspectRatio="none"
             （給了反而會把 stroke-dasharray 一起拉歪，折線在沒 hover 時就先露出一截）。
             pathLength="1" 把路徑長度正規化成 1，dasharray/dashoffset 就不必去算實際長度。 -->
        <svg v-else-if="effect === 'ticks'" class="fx-chart" viewBox="0 0 160 90">
          <path v-for="y in [26, 45, 64]" :key="y" :d="`M0,${y} H160`" class="fx-chart__grid" />
          <path
            d="M4,70 L28,58 L52,63 L76,40 L100,46 L124,22 L156,14"
            class="fx-chart__line"
            pathLength="1"
          />
        </svg>

        <!-- 雨絲（青農戰情室）：斜落的細線，各自的起點與週期不同才不會像整排一起掉 -->
        <template v-else-if="effect === 'rain'">
          <span v-for="(d, i) in RAIN" :key="i" class="fx-rain" :style="rainVars(d)" />
        </template>

        <!-- 掃描（食安透明網）：一道亮線由上往下掃過，對應追溯與抽檢 -->
        <span v-else-if="effect === 'scan'" class="fx-scan" />

        <!-- 腳印（毛小孩地圖）：沿著對角線依序浮現再淡掉，像走過去。
             ⚠ 這一支刻意寫成 v-else-if 而不是 v-else：寫 v-else 的話，日後在
             ShowcaseEffect 加第六種卻忘了加分支，畫面會安靜地掉成腳印，
             型別檢查、lint、測試全都不會有反應。寫成 v-else-if 則是什麼都不畫，
             看得出來漏了。 -->
        <template v-else-if="effect === 'paws'">
          <span
            v-for="(p, i) in PAWS"
            :key="i"
            class="mdi mdi-paw fx-paw"
            :style="pawVars(p, i)"
          />
        </template>
      </span>
    </div>

    <div class="showcase-text">
      <span v-if="eyebrow" class="screen-eyebrow showcase-text__eyebrow">{{ eyebrow }}</span>
      <h3 class="showcase-text__name">{{ name }}</h3>
      <p v-if="lead" class="showcase-text__lead">{{ lead }}</p>
      <span class="showcase-text__go">{{ goLabel }}<span class="mdi mdi-arrow-right" /></span>
    </div>
  </RouterLink>
</template>

<script setup lang="ts">
import { RouterLink } from 'vue-router'
import type { ShowcaseEffect } from '@/constants/navCopy'

withDefaults(
  defineProps<{
    /** 列的序號，從 1 起算；會補成兩位數印在深色塊左上 */
    index: number
    to: string
    /** MDI class 名稱 */
    icon: string
    /** 大標上方那一行小字，沒有就不顯示 */
    eyebrow?: string
    name: string
    lead?: string
    /** 右下角的動作字樣，例如「進入模組」 */
    goLabel: string
    /** 圖塊在右邊（奇偶交錯由呼叫端決定） */
    flip?: boolean
    /** 資訊層級：feature＝模組本身，compact＝模組底下的子頁 */
    size?: 'feature' | 'compact'
    /** hover 特效，四個模組各一種；對照表見 constants/navCopy 的 MODULE_EFFECT */
    effect?: ShowcaseEffect
  }>(),
  { eyebrow: undefined, lead: undefined, flip: false, size: 'feature', effect: 'sparks' },
)

// 位置／週期一律寫成資料，template v-for 出來，CSS 只負責動——
// 這樣要調密度或節奏是改陣列，不是去改一串 nth-child 選擇器。

// 光點刻意集中在圖塊下半（y 偏大），往上漂才有「從地面升起」的感覺
const SPARKS = [
  { x: 14, y: 72, size: 7,  delay: 0,    dur: 2.2 },
  { x: 24, y: 84, size: 5,  delay: 0.35, dur: 2.6 },
  { x: 34, y: 60, size: 9,  delay: 0.15, dur: 2.4 },
  { x: 44, y: 88, size: 4,  delay: 0.6,  dur: 2.8 },
  { x: 52, y: 66, size: 11, delay: 0.05, dur: 2.3 },
  { x: 60, y: 82, size: 6,  delay: 0.45, dur: 2.7 },
  { x: 68, y: 56, size: 9,  delay: 0.25, dur: 2.5 },
  { x: 76, y: 86, size: 5,  delay: 0.7,  dur: 2.9 },
  { x: 86, y: 64, size: 7,  delay: 0.5,  dur: 2.4 },
  { x: 40, y: 76, size: 4,  delay: 0.9,  dur: 3.0 },
  { x: 64, y: 92, size: 6,  delay: 0.8,  dur: 2.6 },
]
function sparkVars(s: (typeof SPARKS)[number]): Record<string, string> {
  return {
    '--x': `${s.x}%`,
    '--y': `${s.y}%`,
    '--spark-size': `${s.size}px`,
    '--fx-delay': `${s.delay}s`,
    '--fx-dur': `${s.dur}s`,
  }
}

// 雨絲：長度與週期一起變，短的落得快，看起來才有遠近
const RAIN = [
  { x: 8,  len: 26, delay: 0,    dur: 0.9 },
  { x: 17, len: 18, delay: 0.45, dur: 1.2 },
  { x: 26, len: 32, delay: 0.15, dur: 0.8 },
  { x: 34, len: 22, delay: 0.7,  dur: 1.1 },
  { x: 43, len: 28, delay: 0.3,  dur: 0.95 },
  { x: 52, len: 16, delay: 0.85, dur: 1.3 },
  { x: 60, len: 30, delay: 0.1,  dur: 0.85 },
  { x: 69, len: 20, delay: 0.55, dur: 1.15 },
  { x: 77, len: 26, delay: 0.25, dur: 1.0 },
  { x: 86, len: 18, delay: 0.65, dur: 1.25 },
  { x: 94, len: 24, delay: 0.4,  dur: 0.9 },
]
function rainVars(d: (typeof RAIN)[number]): Record<string, string> {
  return {
    '--x': `${d.x}%`,
    '--rain-len': `${d.len}px`,
    '--fx-delay': `${d.delay}s`,
    '--fx-dur': `${d.dur}s`,
  }
}

// 腳印：左右各一排交錯往右上走，delay 依序拉開就是「一步一步」
const PAWS = [
  { x: 12, y: 74, rotate: -18 },
  { x: 26, y: 60, rotate: -8 },
  { x: 40, y: 70, rotate: -14 },
  { x: 54, y: 54, rotate: -4 },
  { x: 68, y: 64, rotate: -12 },
  { x: 82, y: 48, rotate: 2 },
]
function pawVars(p: (typeof PAWS)[number], i: number): Record<string, string> {
  return {
    '--x': `${p.x}%`,
    '--y': `${p.y}%`,
    '--paw-rotate': `${p.rotate}deg`,
    '--fx-delay': `${i * 0.16}s`,
    '--fx-dur': '2.4s',
  }
}
</script>

<style scoped>
.showcase-row {
  display: flex;
  align-items: center;
  gap: var(--space-12);
  padding: var(--space-6);
  border: var(--border-width) solid transparent;
  border-radius: var(--radius-xl);
  text-decoration: none;
  color: inherit;
  transition:
    background var(--duration-base) var(--ease-work),
    border-color var(--duration-base) var(--ease-work);
}
.showcase-row:hover { background: var(--color-surface); border-color: var(--color-border); }
.showcase-row:focus-visible { outline: 2px solid var(--color-action); outline-offset: 2px; }
/* 奇偶列左右對調＝交錯 */
.showcase-row--flip { flex-direction: row-reverse; }

.showcase-visual {
  flex: 0 0 38%;
  position: relative;
  aspect-ratio: 16 / 9;
  border-radius: var(--radius-lg);
  overflow: hidden;
  display: flex;
  align-items: center;
  justify-content: center;
  /* 深色視覺塊：帶一層綠光暈，讓交錯的圖塊自己就是畫面上的節奏 */
  background:
    radial-gradient(120% 120% at 78% 18%, var(--color-glow-1), transparent),
    linear-gradient(135deg, var(--color-deep-surface), var(--color-deep));
  transition: box-shadow var(--duration-base) var(--ease-work);
}
/* 深色圖塊在 hover 時整塊透出一圈綠光，把「這一格被選到」講得更明顯。 */
.showcase-row:hover .showcase-visual {
  box-shadow: inset 0 0 70px rgb(79 176 136 / 0.28);
}

.showcase-visual__icon {
  font-size: 88px;
  color: var(--color-action-on-deep);
  position: relative;
  z-index: 1;
  transition: transform var(--duration-base) var(--ease-work);
}
.showcase-row:hover .showcase-visual__icon { transform: scale(1.14); }

/* hover 一次性光掃：一道斜向亮線從左掃過整個深色圖塊，呼應「由左邊進入」的動線想法。
   平常在畫面外（translateX -130%），hover 時播一次。四種特效共用這一層。 */
.showcase-visual::after {
  content: '';
  position: absolute;
  inset: 0;
  background: linear-gradient(115deg, transparent 36%, var(--white-a30) 50%, transparent 64%);
  transform: translateX(-130%);
  z-index: 1;
  pointer-events: none;
}
.showcase-row:hover .showcase-visual::after {
  animation: showcase-sweep var(--duration-slow) var(--ease-out);
}
@keyframes showcase-sweep {
  from { transform: translateX(-130%); }
  to   { transform: translateX(130%); }
}

.showcase-visual__num {
  position: absolute;
  top: var(--space-2);
  inset-inline-start: var(--space-5);
  font-family: var(--font-num);
  font-size: var(--text-6xl);
  font-weight: var(--weight-bold);
  line-height: 1;
  color: var(--white-a12);
}

/* ── 特效層（四種擇一，平常全部靜止，只有 hover 才播） ──────────────── */
.fx { position: absolute; inset: 0; z-index: 2; pointer-events: none; }

/* 光點：預設款。每三顆換成更亮的淺綠，讓光點群有層次不是一片同色。 */
.fx-spark {
  position: absolute;
  left: var(--x);
  top: var(--y);
  width: var(--spark-size);
  height: var(--spark-size);
  border-radius: var(--radius-full);
  background: var(--color-action-on-deep);
  box-shadow: 0 0 14px 3px rgb(79 176 136 / 0.75);
  opacity: 0;
}
.fx-spark:nth-child(3n) { background: var(--seed-300); box-shadow: 0 0 16px 3px rgb(124 195 166 / 0.8); }
.showcase-row:hover .fx-spark {
  animation: fx-spark-drift var(--fx-dur) var(--fx-delay) var(--ease-work) infinite;
}
@keyframes fx-spark-drift {
  0%   { transform: translateY(14px) scale(0.3); opacity: 0; }
  30%  { opacity: 1; }
  70%  { opacity: 0.9; }
  100% { transform: translateY(-46px) scale(1.2); opacity: 0; }
}

/* 走勢線：折線用 dasharray 從左往右畫出來，刻度線同時淡入當背景。
   viewBox 的 160×90 就是圖塊的 16/9，所以維持預設的 preserveAspectRatio 即可，
   不要加 "none"（理由見 template 那一段：它會把 stroke-dasharray 一起拉歪）。 */
.fx-chart { position: absolute; inset: 0; width: 100%; height: 100%; }
.fx-chart__grid {
  stroke: var(--white-a12);
  stroke-width: 1;
  vector-effect: non-scaling-stroke;
  opacity: 0;
}
/* ⚠ 折線這一條不能用 vector-effect: non-scaling-stroke。
   那個屬性會讓 stroke-dasharray 改用螢幕像素計算，而 pathLength="1" 把長度正規化成
   使用者單位的 1——兩者一起用，dasharray 就變成「1 像素的虛線」，看起來是一整條實線，
   沒 hover 也全露出來。改成讓筆畫跟著縮放，用 1 個使用者單位換算後兩種尺寸都約 2px。 */
.fx-chart__line {
  fill: none;
  stroke: var(--color-action-on-deep);
  stroke-width: 1;
  stroke-linecap: round;
  stroke-linejoin: round;
  filter: drop-shadow(0 0 6px rgb(79 176 136 / 0.6));
  stroke-dasharray: 1;
  stroke-dashoffset: 1;
}
.showcase-row:hover .fx-chart__grid { animation: fx-fade-in var(--duration-slow) var(--ease-out) forwards; }
.showcase-row:hover .fx-chart__line { animation: fx-draw 1.5s var(--ease-out) forwards; }
@keyframes fx-fade-in { to { opacity: 1; } }
@keyframes fx-draw { to { stroke-dashoffset: 0; } }

/* 雨絲：細線從圖塊上緣外面斜落到下緣外面。傾斜用 rotate，不是把線畫斜，
   這樣長度（--rain-len）改起來不會連角度一起變。
   ⚠ 落下的距離用 top 的百分比而不是 translateY 的像素：圖塊在 feature 與 compact
   兩階的高度差了快一倍（實測 235px／138px），寫死像素會在小的那階衝出圖塊、
   在大的那階又落不到底。百分比是相對於圖塊高度，兩階都剛好。 */
.fx-rain {
  position: absolute;
  left: var(--x);
  top: -20%;
  width: 1px;
  height: var(--rain-len);
  background: linear-gradient(to bottom, transparent, var(--color-action-on-deep));
  transform: rotate(14deg);
  opacity: 0;
}
.showcase-row:hover .fx-rain {
  animation: fx-rain-fall var(--fx-dur) var(--fx-delay) linear infinite;
}
@keyframes fx-rain-fall {
  0%   { top: -20%; opacity: 0; }
  15%  { opacity: 0.85; }
  85%  { opacity: 0.85; }
  100% { top: 110%; opacity: 0; }
}

/* 掃描：一條亮線由上往下掃過整塊，帶一層淡淡的拖影。對應追溯碼與抽檢。 */
.fx-scan {
  position: absolute;
  inset-inline: 0;
  top: 0;
  height: 2px;
  background: var(--color-action-on-deep);
  box-shadow: 0 0 18px 4px rgb(79 176 136 / 0.55);
  opacity: 0;
}
.fx-scan::before {
  content: '';
  position: absolute;
  inset-inline: 0;
  bottom: 0;
  height: 46px;
  background: linear-gradient(to top, rgb(79 176 136 / 0.22), transparent);
}
/* 掃描的行程同樣用 top 的百分比，理由與雨絲那一段相同 */
.showcase-row:hover .fx-scan {
  animation: fx-scan-sweep 1.8s var(--ease-in-out) infinite;
}
@keyframes fx-scan-sweep {
  0%   { top: 0; opacity: 0; }
  12%  { opacity: 1; }
  88%  { opacity: 1; }
  100% { top: 100%; opacity: 0; }
}

/* 腳印：依序浮現再淡掉，delay 一顆比一顆晚，看起來像一步一步走過去。 */
.fx-paw {
  position: absolute;
  left: var(--x);
  top: var(--y);
  font-size: 22px;
  line-height: 1;
  color: var(--color-action-on-deep);
  transform: rotate(var(--paw-rotate)) scale(0.6);
  opacity: 0;
}
.showcase-row:hover .fx-paw {
  animation: fx-paw-step var(--fx-dur) var(--fx-delay) var(--ease-work) infinite;
}
@keyframes fx-paw-step {
  0%   { transform: rotate(var(--paw-rotate)) scale(0.6); opacity: 0; }
  14%  { transform: rotate(var(--paw-rotate)) scale(1); opacity: 0.9; }
  56%  { opacity: 0.9; }
  100% { transform: rotate(var(--paw-rotate)) scale(1); opacity: 0; }
}

/* ── 文案欄 ─────────────────────────────────────────────────────────── */
.showcase-text { flex: 1; min-width: 0; }
/* 走 base.css 的 .screen-eyebrow，這裡只覆寫兩件事：顏色（特寫列用柿橙第二強調，
   分段標題用動作綠），以及把下緣間距交還給標題自己的 margin-top，不要兩邊各加一次。
   先前這裡整組宣告自己寫了一份，跟 .screen-eyebrow 只差一個顏色——那正是這一輪
   要消滅的重複形態。 */
.showcase-text__eyebrow {
  color: var(--color-accent-2);
  margin-bottom: 0;
}
.showcase-text__name {
  margin-top: var(--space-2);
  font-family: var(--font-display);
  font-size: var(--text-4xl);
  font-weight: var(--weight-bold);
  line-height: var(--leading-display);
  letter-spacing: var(--tracking-title);
  color: var(--color-text);
}
.showcase-text__lead {
  margin-top: var(--space-4);
  max-width: 46ch;
  font-size: var(--text-lg);
  line-height: var(--leading-loose);
  color: var(--color-text-dim);
}
.showcase-text__go {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
  margin-top: var(--space-5);
  color: var(--color-action);
  font-weight: var(--weight-medium);
}
.showcase-text__go .mdi { transition: transform var(--duration-fast) var(--ease-work); }
.showcase-row:hover .showcase-text__go .mdi { transform: translateX(var(--lift-work)); }

/* ── compact：子頁那一階 ────────────────────────────────────────────────
   只縮四個量（圖塊佔比、圖示、序號、標題與說明字級），版型與動效完全不動——
   層級差別要靠尺寸表達，不是靠換一套排版，換了就變成兩種列而不是同一種列的兩階。 */
.showcase-row--compact { gap: var(--space-10); }
.showcase-row--compact .showcase-visual { flex-basis: 30%; }
.showcase-row--compact .showcase-visual__icon { font-size: 60px; }
.showcase-row--compact .showcase-visual__num { font-size: var(--text-4xl); }
.showcase-row--compact .fx-paw { font-size: 16px; }
.showcase-row--compact .showcase-text__name { font-size: var(--text-2xl); }
.showcase-row--compact .showcase-text__lead {
  margin-top: var(--space-3);
  font-size: var(--text-base);
}
.showcase-row--compact .showcase-text__go { margin-top: var(--space-4); }

@media (max-width: 760px) {
  .showcase-row,
  .showcase-row--flip { flex-direction: column; align-items: stretch; gap: var(--space-6); }
  .showcase-visual { flex-basis: auto; width: 100%; }
  .showcase-row--compact .showcase-visual { flex-basis: auto; }
}
</style>
