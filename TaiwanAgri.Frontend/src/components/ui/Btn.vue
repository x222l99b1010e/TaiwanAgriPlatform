<!--
  src/components/ui/Btn.vue
  職責：語意性動作按鈕（查詢／重試／送出／取消／刪除／匯出／清除篩選這一類）。

  收斂範圍刻意只到「淺底上的動作按鈕」為止：分頁按鈕、chip、tab、鈴鐺圖示鈕維持各自的寫法，
  它們有自己的選取態與排列邏輯；深色列上的按鈕（導覽列「登入」、首頁 hero 的「開始查詢」）
  照同一份外形規格各自寫——這裡的四個變體全部是對淺底調的，把深底塞進來等於讓一個元件背兩套底色。

  ── 外形（2026-10-19 改版「左圓盤」；比喻是手機的「往右滑解鎖」）──
  - 主要：漸層外框 → 米白身體（字是深綠）→ 左端一顆深綠圓盤放圖示，圓盤就是解鎖的把手。
    一顆按鈕只用秧苗綠的深淺（外框從圓盤那頭的深綠往右淡出），不放第二個顏色。
    圖示一律放在圓盤裡；沒指定圖示時放預設箭頭，箭頭指著「往右滑」的方向，全站的主要按鈕才長得一致。
    圓盤跟按鈕本體等高、不凸出，同一排的輸入框才對得齊。
    滑過時：外框與圓盤的亮帶從左往右掃過去一次（action-sheen）、身體變淡綠、圓盤往右推 4px（把手被推了一下）。
  - 次要／危險／強調：一道米色線（跟輸入框同一階）＋米白底，圖示在左。
    顏色只在圖示與字：次要墨字灰圖示、危險紅字紅圖示、強調墨字柿色圖示；滑過時框才變成各自的顏色。
    一排裡只有主要按鈕帶顏色塊，主角才分得出來。強調只給「一次清掉整組條件」這類動作（各頁的「清除篩選」）。
  - 四個變體都有立體底邊：按鈕底下一條實心深色（主要深綠、其餘米色），按下去整顆往下壓、底邊收掉。

  ── 注意事項 ──
  1. 立體底邊是 box-shadow，有往下的位移、沒有模糊——它是按鈕本體的厚度，是全站「不用陰影」唯一的例外
     （base.css 陰影那一段）。焦點光暈也是 box-shadow，兩者要寫在一起，否則有焦點時底邊會不見。
  2. 高度吃 --control-h／--control-h-sm，而且「本體＋底邊」才等於這個值（本體扣掉底邊、margin 補回來）：
     同一排的日期輸入框吃同一個 token，底部才對得齊。要改高度的地方（例如登入頁）在按鈕上設 --btn-h
     （外框粗細設 --btn-ring-w），不要直接寫 min-height——底邊會多出一截。
  3. 停用時換成米色底、灰字，不用半透明：查詢頁剛打開時主要按鈕就是停用，半透明那一眼最難看。
     載入中雖然也帶 disabled（不能重複送出），外觀維持可點的綠、只把圓盤裡的圖示換成轉圈。
  4. 反光、轉圈都是 animation，使用者開了「減少動態」時 base.css 的全域規則會一併關掉。
-->
<template>
  <button
    :type="type"
    :class="['btn', `btn--${variant}`, `btn--${size}`, { 'btn--loading': loading }]"
    :disabled="disabled || loading"
  >
    <!-- 主要：文字在米白身體、圖示在左端圓盤。載入中時圓盤裡的圖示換成轉圈 -->
    <template v-if="variant === 'primary'">
      <span class="btn-fill"><slot /></span>
      <span class="btn-disk" aria-hidden="true">
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
/** 主要按鈕沒指定圖示時放在圓盤裡的圖示（指著「往右滑」的方向） */
const DEFAULT_PRIMARY_ICON = 'mdi-arrow-right'

withDefaults(
  defineProps<{
    /** primary＝頁面主要動作；secondary＝次要／重試；danger＝刪除這類破壞性動作；
     *  accent＝一次清掉整組條件（清除篩選） */
    variant?: 'primary' | 'secondary' | 'danger' | 'accent'
    size?: 'sm' | 'md'
    /** MDI 的 class 名稱，例如 'mdi-magnify'。主要按鈕放進左端圓盤，其餘放在文字左邊 */
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
  --btn-lip-color: var(--color-border-strong);
  position: relative;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: var(--space-2);
  /* 本體高度＝高度扣掉底邊（底邊用 margin 補回來）。圓盤的寬、身體左側的留白都吃這個值 */
  --btn-body: calc(var(--btn-h, var(--btn-h-base)) - var(--btn-lip));
  min-height: var(--btn-body);
  margin-block-end: var(--btn-lip);
  /* 清掉瀏覽器給 <button> 的預設框與底色（2px 灰框）：主要按鈕的外框是自己的漸層背景，
     不清的話外框外面還會多一圈灰；次要三種在下面另外寫自己的框 */
  border: 0;
  background-color: transparent;
  border-radius: var(--radius-full);
  box-shadow: 0 var(--btn-lip) 0 var(--btn-lip-color);
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
   高度由 token 決定、垂直 padding 一律 0：用 padding 撐高度時，字級一改
   高度就跟著跑，同一排的按鈕與輸入框又會對不齊。外框、底邊與圓盤的尺寸跟著尺寸走。
   尺寸 class 只給預設值（-base），--btn-h／--btn-ring-w 留給使用的頁面覆寫（例如登入頁）：
   兩邊若寫同一個變數，誰贏要看哪一份 CSS 後載入，打包後的順序不保證。 */
.btn--md {
  --btn-h-base: var(--control-h);
  --btn-lip: var(--control-lip);
  --btn-ring-w-base: var(--control-ring-w);
  font-size: var(--text-sm);
}
.btn--sm {
  --btn-h-base: var(--control-h-sm);
  --btn-lip: var(--control-lip-sm);
  --btn-ring-w-base: var(--control-ring-w-sm);
  font-size: var(--text-xs);
}

.btn-icon { font-size: 1.15em; }
.btn-icon--spin { animation: btn-spin 0.9s linear infinite; }
@keyframes btn-spin { to { transform: rotate(360deg); } }

/* ── primary：漸層外框 → 米白身體 ＋ 左端圓盤 ──
   外框是按鈕自己的背景（padding 露出來的那一圈），身體疊在上面。 */
.btn--primary {
  --btn-lip-color: var(--color-action-lip);
  align-items: stretch;
  padding: var(--btn-ring-w, var(--btn-ring-w-base));
  background-image: var(--gradient-action);
  background-size: var(--gradient-action-size);
  color: var(--color-action-ink);
}

.btn-fill {
  position: relative;
  z-index: 1;
  flex: 1;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  /* 左側留出圓盤的寬度（圓盤＝本體高度），文字才不會鑽到圓盤底下 */
  padding-inline: calc(var(--btn-body) + var(--space-2)) var(--space-5);
  border-radius: inherit;
  background: var(--color-surface);
  transition: background-color var(--duration-fast) var(--ease-work);
}
.btn--sm .btn-fill { padding-inline-end: var(--space-4); }

/* 圓盤：跟本體等高的正圓，蓋住左端的外框。寬度直接寫本體高度、不用 aspect-ratio：
   絕對定位＋上下貼齊時，aspect-ratio 推不推得出寬度要看瀏覽器版本 */
.btn-disk {
  position: absolute;
  z-index: 2;
  inset-block: 0;
  inset-inline-start: 0;
  width: var(--btn-body);
  display: grid;
  place-items: center;
  border-radius: 50%;
  background-image: var(--gradient-disk-gloss), var(--gradient-action-disk);
  background-size: 100% 100%, var(--gradient-action-disk-size);
  /* 一圈極淡的米白內緣，圓盤在深色底上才不會跟外框黏成一塊。零模糊零位移，是框不是陰影 */
  box-shadow: inset 0 0 0 2px var(--white-a12);
  color: var(--color-on-action);
  transition: transform var(--duration-fast) var(--ease-work);
}
.btn-disk .btn-icon { font-size: calc(var(--btn-body) * 0.48); }

/* 滑過：外框與圓盤的亮帶一起從左往右掃一次、身體變淡綠、圓盤往右推 4px */
.btn--primary:hover:not(:disabled) { animation: action-sheen var(--duration-sheen) var(--ease-in-out) 1; }
.btn--primary:hover:not(:disabled) .btn-fill { background: var(--color-action-soft); }
.btn--primary:hover:not(:disabled) .btn-disk {
  transform: translateX(4px);
  animation: action-sheen var(--duration-sheen) var(--ease-in-out) 1;
}

/* ── secondary／danger／accent：一道米色線＋米白底 ──
   三者只差字、圖示與滑過時框的顏色，由各自的變數決定；hover 改變數而不是改屬性，transition 才接得住 */
.btn--secondary,
.btn--danger,
.btn--accent {
  padding: 0 var(--space-5);
  border: var(--border-width) solid var(--btn-line);
  background: var(--color-surface);
  color: var(--btn-ink);
}
.btn--sm.btn--secondary,
.btn--sm.btn--danger,
.btn--sm.btn--accent { padding: 0 var(--space-4); }
.btn--secondary .btn-icon,
.btn--danger .btn-icon,
.btn--accent .btn-icon { color: var(--btn-icon); transition: color var(--duration-fast) var(--ease-work); }

.btn--secondary { --btn-line: var(--color-border-strong); --btn-ink: var(--color-text); --btn-icon: var(--color-text-dim); }
.btn--secondary:hover:not(:disabled) {
  --btn-line: var(--color-action);
  --btn-ink: var(--color-action);
  --btn-icon: var(--color-action);
  background: var(--color-action-soft);
}

.btn--danger { --btn-line: var(--color-border-strong); --btn-ink: var(--danger-500); --btn-icon: var(--danger-500); }
.btn--danger:hover:not(:disabled) { --btn-line: var(--danger-500); background: var(--danger-50); }

.btn--accent { --btn-line: var(--color-border-strong); --btn-ink: var(--color-text); --btn-icon: var(--color-accent-2); }
.btn--accent:hover:not(:disabled) { --btn-line: var(--color-accent-2-ring); background: var(--color-accent-2-soft); }

/* 按下去整顆往下壓一個底邊的高度、底邊收掉。從原位往下壓，滑鼠、鍵盤 Enter、觸控哪一種操作都看得到回饋 */
.btn:active:not(:disabled) {
  transform: translateY(var(--btn-lip));
  box-shadow: 0 0 0 var(--btn-lip-color);
}

/* 焦點光暈只在鍵盤操作時出現。用光暈不用 outline：outline 是硬邊實線，
   跟外框疊在一起分不出哪一圈是焦點。底邊也是 box-shadow，兩者要寫在一起 */
.btn:focus-visible { outline: none; box-shadow: 0 var(--btn-lip) 0 var(--btn-lip-color), var(--shadow-focus); }
.btn--danger:focus-visible { box-shadow: 0 var(--btn-lip) 0 var(--btn-lip-color), var(--shadow-focus-danger); }

/* 停用：米色底、灰字，不用半透明（理由見檔頭注意事項 3）。載入中不套這組 */
.btn:disabled { cursor: not-allowed; }
.btn:disabled:not(.btn--loading) { --btn-lip-color: var(--color-border); }
.btn--primary:disabled:not(.btn--loading) { background-image: none; background-color: var(--color-border); color: var(--color-text-dim); }
.btn--primary:disabled:not(.btn--loading) .btn-fill { background: var(--color-bg-sunken); }
.btn--primary:disabled:not(.btn--loading) .btn-disk { background-image: none; background-color: var(--color-border); box-shadow: none; color: var(--color-border-strong); }
.btn--secondary:disabled:not(.btn--loading),
.btn--danger:disabled:not(.btn--loading),
.btn--accent:disabled:not(.btn--loading) {
  --btn-line: var(--color-border);
  --btn-ink: var(--color-text-dim);
  --btn-icon: var(--color-border-strong);
  background: var(--color-bg-sunken);
}
/* 載入中不用 not-allowed：游標形狀在這裡的語意是「不能點」，但載入中其實是
   「正在做你要求的事」，用一般游標比較不會讓人以為按錯了 */
.btn--loading { cursor: progress; }
</style>
