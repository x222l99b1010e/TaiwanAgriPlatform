<!--
  src/components/ui/Btn.vue
  職責：語意性動作按鈕（查詢／重試／送出／取消／刪除／匯出這一類）。

  收斂範圍刻意只到「動作按鈕」為止：分頁按鈕、chip、tab、鈴鐺圖示鈕維持各自的寫法，
  它們有自己的選取態與排列邏輯，塞進同一個元件只會讓 API 同時背兩種職責。

  原本這類按鈕在各頁各寫一份（btn-query 7 處、btn-retry 10 處、btn-search／btn-clear／
  btn-submit／btn-post／btn-edit／btn-delete…），高度、圓角、字級、hover 行為都不一樣。

  互動細節集中在這裡，是為了讓後續的視覺精修只要改這一個檔：hover 的位移與陰影、
  按下的回饋、鍵盤 focus 的外框、載入中的轉圈，全部走 base.css 的 --duration-* /
  --ease-* token；使用者若開了「減少動態」，base.css 的全域規則會一併關掉。
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
  border-radius: var(--radius-full);
  font-family: inherit;
  font-weight: var(--weight-medium);
  line-height: 1;
  white-space: nowrap;
  cursor: pointer;
  transition:
    background-color var(--duration-fast) var(--ease-out),
    border-color var(--duration-fast) var(--ease-out),
    color var(--duration-fast) var(--ease-out),
    box-shadow var(--duration-fast) var(--ease-out),
    transform var(--duration-fast) var(--ease-out);
}

/* ── 尺寸 ── */
.btn--md { padding: var(--space-3) var(--space-6); font-size: var(--text-sm); }
.btn--sm { padding: var(--space-2) var(--space-4); font-size: var(--text-xs); }

.btn-icon { font-size: 1.15em; }
.btn-icon--spin { animation: btn-spin 0.9s linear infinite; }
@keyframes btn-spin { to { transform: rotate(360deg); } }

/* ── primary ── */
.btn--primary {
  background: var(--green-600);
  color: var(--neutral-0);
  box-shadow: var(--shadow-sm);
}
.btn--primary:hover:not(:disabled) {
  background: var(--green-700);
  box-shadow: var(--shadow-md);
  transform: translateY(-1px);
}

/* ── secondary（描邊，用在重試、清除、次要動作） ── */
.btn--secondary {
  background: var(--neutral-0);
  border-color: var(--neutral-300);
  color: var(--neutral-700);
}
.btn--secondary:hover:not(:disabled) {
  border-color: var(--green-600);
  color: var(--green-700);
  background: var(--green-50);
}

/* ── danger ── */
.btn--danger {
  background: var(--neutral-0);
  border-color: var(--danger-100);
  color: var(--danger-500);
}
.btn--danger:hover:not(:disabled) {
  background: var(--danger-50);
  border-color: var(--danger-500);
}

/* 按下時往回壓一格。hover 是 -1px、按下是 0，兩者相減就是「被壓下去」的感覺 */
.btn:active:not(:disabled) { transform: translateY(0); box-shadow: var(--shadow-sm); }

/* 只在鍵盤操作時顯示外框，滑鼠點擊不顯示——滑鼠使用者不需要這個提示，
   但鍵盤使用者沒有它就完全不知道焦點在哪 */
.btn:focus-visible {
  outline: 2px solid var(--green-600);
  outline-offset: 2px;
}

.btn:disabled { opacity: 0.5; cursor: not-allowed; }
/* 載入中不用 not-allowed：游標形狀在這裡的語意是「不能點」，但載入中其實是
   「正在做你要求的事」，用一般游標比較不會讓人以為按錯了 */
.btn--loading { cursor: progress; }
</style>
