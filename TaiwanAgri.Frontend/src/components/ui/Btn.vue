<!--
  src/components/ui/Btn.vue
  職責：語意性動作按鈕（查詢／重試／送出／取消／刪除／匯出／清除篩選這一類）。

  收斂範圍刻意只到「淺底上的動作按鈕」為止：分頁按鈕、chip、tab、鈴鐺圖示鈕維持各自的寫法，
  它們有自己的選取態與排列邏輯；深色列上的按鈕（導覽列「登入」、首頁 hero 的「開始查詢」）
  照同一份外形規格各自寫——這裡的四個變體全部是對淺底調的，把深底塞進來等於讓一個元件背兩套底色。

  ── 外形（四個變體共用：藥丸形＋描邊）──
  藥丸形不突兀，靠的是描邊把它框住；主要按鈕右端的圓鈕比按鈕高、上下各凸出一截，把整顆「撐起來」。
  形狀、描邊、凸出圓鈕是一組，拆開來單用就失去重量。
  - 主要：墨色外框 → 一圈實心淡綠間隙（--color-control-gap）→ 左深右淺的漸層填色。圖示一律放在右端圓鈕裡；沒指定圖示時
    放預設箭頭，全站的主要按鈕才長得一致。滑過時漸層淺色那一頭從右往左滑過去當反光，
    只跑一次（動的是漸層本身，規格在 base.css 的 --gradient-action 與 action-sheen）。
    間隙一定是實心、不能透明：透出底色時外框與填色中間會多一圈底色，深底上那一圈是黑的，
    按鈕看起來像散開的兩道線。用填色同色系的淡綠而不是近白：近白跟綠色的明度差太大，突兀。
  - 次要／危險／強調：中間調的外環（border）＋墨色內側細線＋米白底，圖示在左、跟外環同色；
    字是墨色，只有危險是紅字。三者只差外環顏色：次要灰褐、危險紅、強調柿色。
    強調只給「一次清掉整組條件」這類動作（各頁的「清除篩選」）。

  ── 注意事項 ──
  1. 內側細線與焦點光暈都用 box-shadow，但都沒有模糊也沒有位移，不是陰影——「按鈕不用陰影」照樣成立。
  2. 圓鈕凸出按鈕外（md 上下各 4px、右 6px），主要按鈕因此帶一段 margin-inline-end，
     同一排的間距才不會被吃掉；放進 overflow: hidden 的容器時圓鈕會被切掉。
     要撐滿整列的主要按鈕（例如登入頁），寬度記得扣掉 --btn-knob-overhang。
  3. 高度吃 --control-h／--control-h-sm：同一排的日期輸入框吃同一個 token，底部才對得齊。
  4. 反光與轉圈都是 animation，使用者開了「減少動態」時 base.css 的全域規則會一併關掉。
-->
<template>
  <button
    :type="type"
    :class="['btn', `btn--${variant}`, `btn--${size}`, { 'btn--loading': loading }]"
    :disabled="disabled || loading"
  >
    <!-- 主要：文字在填色層、圖示在圓鈕。載入中時圓鈕裡的圖示換成轉圈 -->
    <template v-if="variant === 'primary'">
      <span class="btn-fill"><slot /></span>
      <span class="btn-knob" aria-hidden="true">
        <span :class="['mdi', 'btn-icon', loading ? ['mdi-loading', 'btn-icon--spin'] : (icon ?? DEFAULT_PRIMARY_ICON)]" />
      </span>
    </template>
    <!-- 其餘：圖示在左。載入中時圖示位置換成轉圈，不另外插入元素，避免按鈕寬度跳動 -->
    <template v-else>
      <span v-if="loading" class="mdi mdi-loading btn-icon btn-icon--spin" />
      <span v-else-if="icon" :class="['mdi', icon, 'btn-icon']" />
      <slot />
    </template>
  </button>
</template>

<script setup lang="ts">
/** 主要按鈕沒指定圖示時放在圓鈕裡的圖示 */
const DEFAULT_PRIMARY_ICON = 'mdi-arrow-right'

withDefaults(
  defineProps<{
    /** primary＝頁面主要動作；secondary＝次要／重試；danger＝刪除這類破壞性動作；
     *  accent＝一次清掉整組條件（清除篩選） */
    variant?: 'primary' | 'secondary' | 'danger' | 'accent'
    size?: 'sm' | 'md'
    /** MDI 的 class 名稱，例如 'mdi-magnify'。主要按鈕放進右端圓鈕，其餘放在文字左邊 */
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
  --btn-line: var(--color-text);
  position: relative;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: var(--space-2);
  border-radius: var(--radius-full);
  font-family: inherit;
  font-weight: var(--weight-bold);
  /* 中文在小字級上筆畫容易黏在一起，補一點字距（--tracking-title 那一組的同一種補償） */
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
   高度就跟著跑，同一排的按鈕與輸入框又會對不齊。描邊與圓鈕的尺寸跟著尺寸走。 */
.btn--md {
  --btn-line-w: var(--control-line-w);
  --btn-ring-w: var(--control-ring-w);
  --btn-gap: 4px;
  --btn-knob: 48px;
  --btn-knob-overhang: 6px;
  min-height: var(--control-h);
  font-size: var(--text-sm);
}
.btn--sm {
  --btn-line-w: var(--control-line-w-sm);
  --btn-ring-w: var(--control-ring-w-sm);
  --btn-gap: 3px;
  --btn-knob: 38px;
  --btn-knob-overhang: 5px;
  min-height: var(--control-h-sm);
  font-size: var(--text-xs);
}

.btn-icon { font-size: 1.15em; }
.btn-icon--spin { animation: btn-spin 0.9s linear infinite; }
@keyframes btn-spin { to { transform: rotate(360deg); } }

/* ── primary：外框 → 淡綠間隙 → 漸層填色 ＋ 右端凸出的圓鈕 ── */
.btn--primary {
  align-items: stretch;
  padding: var(--btn-gap);
  margin-inline-end: var(--btn-knob-overhang);
  border: var(--btn-line-w) solid var(--btn-line);
  background: var(--color-control-gap);
  color: var(--color-on-action);
}

.btn-fill {
  flex: 1;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  /* 右側留出被圓鈕蓋住的寬度，文字才不會鑽到圓鈕底下 */
  padding-inline: var(--space-5) calc(var(--btn-knob) - var(--btn-knob-overhang) - var(--btn-gap) + var(--space-2));
  border-radius: inherit;
  background-image: var(--gradient-action);
  background-size: var(--gradient-action-size);
}
.btn--sm .btn-fill { padding-inline-start: var(--space-4); }

.btn-knob {
  position: absolute;
  inset-block-start: 50%;
  inset-inline-end: calc(var(--btn-knob-overhang) * -1);
  width: var(--btn-knob);
  height: var(--btn-knob);
  margin-block-start: calc(var(--btn-knob) / -2);
  display: grid;
  place-items: center;
  border: var(--btn-line-w) solid var(--btn-line);
  border-radius: 50%;
  background: var(--color-control-gap);
  color: var(--color-action);
  transition: color var(--duration-fast) var(--ease-work), border-color var(--duration-fast) var(--ease-work);
}
.btn-knob .btn-icon { font-size: calc(var(--btn-knob) * 0.46); }

/* 滑過：填色整體壓深一階，同時淺色那一頭從右往左滑過去一次 */
.btn--primary:hover:not(:disabled) .btn-fill {
  background-image: var(--gradient-action-hover);
  animation: action-sheen var(--duration-sheen) var(--ease-in-out) 1;
}
.btn--primary:hover:not(:disabled) .btn-knob { color: var(--color-action-hover); }

/* ── secondary／danger／accent：中間調外環＋墨色內側細線＋米白底 ──
   三者只差外環與字的顏色，由各自的變數決定；hover 改變數而不是改屬性，
   transition 才接得住（box-shadow 的顏色跟著變數一起過渡）。
   外環要是中間調：太淺的外環（例如強邊框那一階）會跟底色糊在一起，雙線只剩一道 */
.btn--secondary,
.btn--danger,
.btn--accent {
  --btn-inner: var(--btn-line);
  padding: 0 var(--space-5);
  border: var(--btn-ring-w) solid var(--btn-ring);
  box-shadow: inset 0 0 0 var(--btn-line-w) var(--btn-inner);
  background: var(--color-surface);
  color: var(--btn-ink);
}
.btn--sm.btn--secondary,
.btn--sm.btn--danger,
.btn--sm.btn--accent { padding: 0 var(--space-4); }
/* 圖示跟外環同色：外環說「這是哪一種按鈕」，圖示重複一次，字維持墨色好讀 */
.btn--secondary .btn-icon,
.btn--danger .btn-icon,
.btn--accent .btn-icon { color: var(--btn-ring); transition: color var(--duration-fast) var(--ease-work); }

.btn--secondary { --btn-ring: var(--color-control-ring); --btn-ink: var(--color-text); }
.btn--secondary:hover:not(:disabled) {
  --btn-inner: var(--color-action);
  --btn-ink: var(--color-action);
  background: var(--color-action-soft);
}
.btn--secondary:hover:not(:disabled) .btn-icon { color: var(--color-action); }

.btn--danger { --btn-ring: var(--danger-500); --btn-ink: var(--danger-500); }
.btn--danger:hover:not(:disabled) {
  --btn-ring: var(--danger-700);
  --btn-ink: var(--danger-700);
  background: var(--danger-50);
}

.btn--accent { --btn-ring: var(--color-accent-2-ring); --btn-ink: var(--color-text); }
.btn--accent:hover:not(:disabled) { background: var(--color-accent-2-soft); }

/* 按下去往下壓一格。從原位往下壓，滑鼠、鍵盤 Enter、觸控哪一種操作都看得到回饋 */
.btn:active:not(:disabled) { transform: translateY(1px); }

/* 焦點光暈只在鍵盤操作時出現。用光暈不用 outline：outline 是硬邊實線，
   跟描邊疊在一起分不出哪一圈是焦點。描邊型的內側細線也是 box-shadow，兩者要寫在一起 */
.btn:focus-visible { outline: none; }
.btn--primary:focus-visible { box-shadow: var(--shadow-focus); }
.btn--primary:focus-visible .btn-knob { border-color: var(--color-brand); }
.btn--secondary:focus-visible,
.btn--accent:focus-visible { box-shadow: inset 0 0 0 var(--btn-line-w) var(--btn-inner), var(--shadow-focus); }
.btn--danger:focus-visible { box-shadow: inset 0 0 0 var(--btn-line-w) var(--btn-inner), var(--shadow-focus-danger); }

.btn:disabled { opacity: 0.5; cursor: not-allowed; }
/* 載入中不用 not-allowed：游標形狀在這裡的語意是「不能點」，但載入中其實是
   「正在做你要求的事」，用一般游標比較不會讓人以為按錯了 */
.btn--loading { cursor: progress; }
</style>
