<!--
  src/components/ui/Btn.vue
  職責：語意性動作按鈕（查詢／重試／送出／取消／刪除／匯出這一類）。

  收斂範圍刻意只到「動作按鈕」為止：分頁按鈕、chip、tab、鈴鐺圖示鈕維持各自的寫法，
  它們有自己的選取態與排列邏輯，塞進同一個元件只會讓 API 同時背兩種職責。

  互動細節集中在這裡，是為了讓後續的視覺精修只要改這一個檔：hover 的變色、
  按下的回饋、鍵盤 focus 的光暈、載入中的轉圈，全部走 base.css 的 --duration-* /
  --ease-* token；使用者若開了「減少動態」，base.css 的全域規則會一併關掉。

  ── P3 這一版跟 P1 初版的三個差別（都不是喜好問題，各有出處）──
  1. **不再是藥丸**：圓角從 --radius-full 改成 --radius-md（8px）。規範是
     「圓角全站收在 4–12px，--radius-full 只給頭像與 chip」；一顆 999px 的按鈕擺在
     8px 的輸入框與 10px 的卡片旁邊，是這一頁最不像同一套設計的東西。
  2. **沒有陰影**：卡片與按鈕不准有 box-shadow，陰影降級成只給浮動層。
     原本 hover 時 --shadow-md ＋ 上浮 1px 的組合，是把按鈕當成會飄起來的卡片在做。
     改成只用顏色深淺與 1px 深色邊界表示層次——底色是暖米白，深綠色塊本身就夠跳。
  3. **高度用 token 鎖住**：實測原本 md 是 41.4px、同一排的日期輸入框是 37.2px，
     底部對齊了仍差 4px。兩者現在都吃 --control-h（40px），差值歸零。
-->
<template>
  <button
    :type="type"
    :class="['btn', `btn--${variant}`, `btn--${size}`, { 'btn--loading': loading }]"
    :disabled="disabled || loading"
  >
    <!-- 載入中時圖示位置換成轉圈，不另外插入元素，避免按鈕寬度跳動 -->
    <span v-if="loading" class="mdi mdi-loading btn-icon btn-icon--spin" />
    <span v-else-if="icon" :class="['mdi', icon, 'btn-icon']" />
    <slot />
  </button>
</template>

<script setup lang="ts">
withDefaults(
  defineProps<{
    /** primary＝頁面主要動作；secondary＝次要／重試；danger＝刪除這類破壞性動作 */
    variant?: 'primary' | 'secondary' | 'danger'
    size?: 'sm' | 'md'
    /** MDI 的 class 名稱，例如 'mdi-magnify' */
    icon?: string
    loading?: boolean
    disabled?: boolean
    /** 預設 button：這些按鈕多半在 form 之外，預設成 submit 會造成非預期的表單送出 */
    type?: 'button' | 'submit' | 'reset'
  }>(),
  {
    variant: 'primary',
    size: 'md',
    icon: undefined,
    loading: false,
    disabled: false,
    type: 'button',
  },
)
</script>

<style scoped>
.btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: var(--space-2);
  border: var(--border-width) solid transparent;
  border-radius: var(--radius-md);
  font-family: inherit;
  font-weight: var(--weight-medium);
  /* 中文在小字級上筆畫容易黏在一起，補一點字距。這是 --tracking-title 那一組的
     同一種補償，只是用在按鈕上不需要到標題那麼多 */
  letter-spacing: 0.02em;
  line-height: 1;
  white-space: nowrap;
  cursor: pointer;
  transition:
    background-color var(--duration-fast) var(--ease-work),
    border-color var(--duration-fast) var(--ease-work),
    color var(--duration-fast) var(--ease-work),
    box-shadow var(--duration-fast) var(--ease-work),
    transform var(--duration-fast) var(--ease-work);
}

/* ── 尺寸 ──
   高度由 --control-h 決定、垂直 padding 一律 0：用 padding 撐高度時，字級一改
   高度就跟著跑，同一排的按鈕與輸入框又會對不齊。 */
.btn--md { min-height: var(--control-h);    padding: 0 var(--space-5); font-size: var(--text-sm); }
.btn--sm { min-height: var(--control-h-sm); padding: 0 var(--space-4); font-size: var(--text-xs); }

.btn-icon { font-size: 1.15em; }
.btn-icon--spin { animation: btn-spin 0.9s linear infinite; }
@keyframes btn-spin { to { transform: rotate(360deg); } }

/* ── primary ──
   深一階的邊框不是裝飾：純色塊直接坐在暖米白上時邊緣會發虛，1px 深色邊界
   讓它有「切出來」的銳利感，也是「卡片邊界只用 1px 邊框」的同一條規則 */
.btn--primary {
  background: var(--color-action);
  border-color: var(--seed-700);
  color: var(--color-on-action);
}
.btn--primary:hover:not(:disabled) {
  background: var(--color-action-hover);
  border-color: var(--seed-800);
}

/* ── secondary（描邊，用在重試、清除、次要動作） ── */
.btn--secondary {
  background: var(--color-surface);
  border-color: var(--color-border-strong);
  color: var(--color-text);
}
.btn--secondary:hover:not(:disabled) {
  border-color: var(--color-action);
  color: var(--color-action);
  background: var(--color-action-soft);
}

/* ── danger ── */
.btn--danger {
  background: var(--color-surface);
  border-color: var(--danger-100);
  color: var(--danger-500);
}
.btn--danger:hover:not(:disabled) {
  background: var(--danger-50);
  border-color: var(--danger-500);
  color: var(--danger-700);
}

/* 按下去往下壓一格。原本是「hover 抬 1px、按下回 0」，那要先 hover 才有落差，
   鍵盤按 Enter 或直接點下去的人完全看不到回饋；改成從原位往下壓，哪一種操作都成立 */
.btn:active:not(:disabled) { transform: translateY(1px); }

/* 只在鍵盤操作時顯示，滑鼠點擊不顯示——滑鼠使用者不需要這個提示，
   但鍵盤使用者沒有它就完全不知道焦點在哪。
   用光暈不用 outline：outline 是硬邊實線，跟這一版按鈕的柔邊調性打架，
   而且 8px 圓角的 outline 在部分瀏覽器不會跟著圓角走。 */
.btn:focus-visible {
  outline: none;
  border-color: var(--color-brand);
  box-shadow: var(--shadow-focus);
}
.btn--danger:focus-visible {
  border-color: var(--danger-500);
  box-shadow: var(--shadow-focus-danger);
}

.btn:disabled { opacity: 0.5; cursor: not-allowed; }
/* 載入中不用 not-allowed：游標形狀在這裡的語意是「不能點」，但載入中其實是
   「正在做你要求的事」，用一般游標比較不會讓人以為按錯了 */
.btn--loading { cursor: progress; }
</style>
