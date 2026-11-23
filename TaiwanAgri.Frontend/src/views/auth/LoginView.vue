<template>
  <div class="login-page">
    <div class="login-card">
      <!-- Logo 區 -->
      <div class="card-header">
        <img class="logo-icon brand-mark" src="/favicon.svg" alt="" width="48" height="48">
        <h1 class="card-title brand-name">田野‧農時</h1>
        <p class="card-subtitle">請登入以繼續</p>
      </div>

      <!-- Tab 切換：登入 / 註冊。用 base.css 的 .segmented，並補一個 --full 修飾子
           讓它撐滿卡片寬——這一張卡片裡只有兩個選項，縮成內容寬會在右邊留一大塊空白 -->
      <div class="segmented segmented--full tab-group">
        <button
          class="segmented__btn"
          :class="{ 'is-active': mode === 'login' }"
          @click="switchMode('login')"
        >登入</button>
        <button
          class="segmented__btn"
          :class="{ 'is-active': mode === 'register' }"
          @click="switchMode('register')"
        >註冊</button>
      </div>

      <!-- 表單 -->
      <div class="form-body">
        <div class="field-group">
          <label class="field-label" for="auth-email">電子信箱</label>
          <input
            id="auth-email"
            class="form-control field-input"
            type="email"
            v-model="email"
            placeholder="your@email.com"
            :disabled="isLoading"
          />
        </div>

        <div class="field-group">
          <label class="field-label" for="auth-password">密碼</label>
          <input
            id="auth-password"
            class="form-control field-input"
            type="password"
            v-model="password"
            placeholder="••••••••"
            :disabled="isLoading"
            :aria-describedby="mode === 'register' ? 'auth-password-rules' : undefined"
            @keyup.enter="handleSubmit"
          />
          <!-- 註冊時先講規則（後端 ASP.NET Core Identity 的預設密碼規則），
               不然要送出被擋才知道，而且原本一次只說一條 -->
          <p v-if="mode === 'register'" id="auth-password-rules" class="field-hint">
            至少 6 個字元，要有大寫與小寫英文字母、數字和特殊符號（如 !@#$）
          </p>
        </div>

        <!-- 註冊額外欄位 -->
        <template v-if="mode === 'register'">
          <div class="field-group">
            <label class="field-label" for="auth-confirm-password">確認密碼</label>
            <input
              id="auth-confirm-password"
              class="form-control field-input"
              type="password"
              v-model="confirmPassword"
              placeholder="請再輸入一次密碼"
              :disabled="isLoading"
              @keyup.enter="handleSubmit"
            />
            <!-- 只在使用者已經開始輸入這欄之後才提示：一開始兩欄都空、必然「相等」，
                 這時就跳錯誤字樣會在使用者根本還沒動作時就先罵人，體感很差 -->
            <p v-if="confirmPassword && password !== confirmPassword" class="field-error">
              兩次輸入的密碼不一致
            </p>
          </div>

          <div class="field-group">
            <label class="field-label" for="auth-display-name">顯示名稱（選填）</label>
            <input
              id="auth-display-name"
              class="form-control field-input"
              type="text"
              v-model="displayName"
              placeholder="例如：大安區自耕農阿志頭"
              :disabled="isLoading"
            />
          </div>

          <div class="field-group">
            <label class="field-label" for="auth-user-type">身份類型</label>
            <select id="auth-user-type" class="form-control field-input" v-model="userType" :disabled="isLoading">
              <option value="Farmer">農民</option>
              <option value="Consumer">消費者</option>
              <option value="Researcher">研究員</option>
            </select>
          </div>
        </template>

        <!-- 錯誤訊息 -->
        <div v-if="errorLines.length > 0" class="error-box" role="alert">
          <span class="mdi mdi-alert-circle error-icon" />
          <div class="error-content">
            <p v-for="(line, i) in errorLines" :key="i">{{ line }}</p>
          </div>
        </div>

        <!-- 提交按鈕 -->
        <Btn
          class="login-submit"
          :loading="isLoading"
          :disabled="formProblem !== null"
          @click="handleSubmit"
        >{{ isLoading ? '處理中...' : (mode === 'login' ? '登入' : '註冊並登入') }}</Btn>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import Btn from '@/components/ui/Btn.vue'
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/authStore'
import { translateIdentityErrors } from '@/utils/identityErrors'
import { redirectAfterLogin } from '@/utils/authRedirect'
import { loginFormProblem } from '@/utils/loginForm'

const router = useRouter()
const authStore = useAuthStore()

const route = useRoute()
const redirect = redirectAfterLogin(route.query.redirect)

// ─── 表單狀態 ─────────────────────────────────────────
const mode = ref<'login' | 'register'>('login')
const email = ref('')
const password = ref('')
const confirmPassword = ref('')
const displayName = ref('')
const userType = ref('Farmer')
const isLoading = ref(false)
const errorLines = ref<string[]>([])

/** 還不能送出的原因（null＝可以送出）。按鈕的停用與按 Enter 送出用同一份條件 */
const formProblem = computed(() =>
  loginFormProblem(mode.value, email.value, password.value, confirmPassword.value),
)

/** 切換登入／註冊時清掉上一個分頁的錯誤，不然登入失敗的訊息會留在註冊表單上 */
function switchMode(next: 'login' | 'register') {
  mode.value = next
  errorLines.value = []
}

// ─── 提交 ─────────────────────────────────────────────
async function handleSubmit() {
  errorLines.value = []

  // 送出按鈕已經用 :disabled 擋掉填不完整的表單，但 @keyup.enter 是直接呼叫這個函式、
  // 不會經過按鈕的 disabled 狀態，所以這裡用同一組條件再擋一次，並說出缺的是什麼
  if (formProblem.value) {
    errorLines.value = [formProblem.value]
    return
  }

  isLoading.value = true

  try {
    if (mode.value === 'login') {
      await authStore.login({ email: email.value, password: password.value })
    } else {
      await authStore.register({
        email: email.value,
        password: password.value,
        displayName: displayName.value || undefined,
        userType: userType.value,
      })
    }
    router.push(redirect)
  } catch (err: unknown) {
    // axios 錯誤：嘗試取出後端回傳的文字
    if (
      err &&
      typeof err === 'object' &&
      'response' in err &&
      err.response &&
      typeof err.response === 'object' &&
      'data' in err.response
    ) {
      const data = (err.response as { data: unknown }).data
      if (typeof data === 'string' && data.trim()) {
        errorLines.value = translateIdentityErrors(data)
      } else {
        errorLines.value = ['操作失敗，請稍後再試']
      }
    } else if (err instanceof Error) {
      errorLines.value = [err.message]
    } else {
      errorLines.value = ['操作失敗，請稍後再試']
    }
  } finally {
    isLoading.value = false
  }
}
</script>

<style scoped>
/* 顏色全部改用 semantic 層；欄位外殼走 base.css 的
   .field-group／.field-label／.form-control／.field-error，分段控制器走 .segmented。

   ⚠ 原本是 `min-height: 100vh`，但這一頁活在 TopNav（56px）與菜價橫幅（40px）
   底下——100vh 會讓整頁一定比視窗高 96px，登入卡片被推到偏下、還多一條捲軸。
   扣掉那兩層之後卡片才真的在視覺中央。 */
.login-page {
  min-height: calc(100vh - 56px - var(--control-h));
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--color-bg-sunken);
  padding: var(--space-8) var(--space-6);
}

/* 卡片不給陰影。頁面底改用比卡片深一階的 --color-bg-sunken，
   靠明度差把卡片浮起來——這正是「平坦的成因是底色與卡片一樣亮」的解法。 */
.login-card {
  width: 100%;
  max-width: 420px;
  background: var(--color-surface);
  border: var(--border-width) solid var(--color-border);
  border-radius: var(--radius-xl);
  padding: var(--space-10);
}

/* Header */
.card-header {
  text-align: center;
  margin-bottom: var(--space-8);
}

/* 網站標誌，跟分頁圖示是同一張（favicon.svg）。48px＝原稿 16 格的 3 倍，每條邊仍落在整數像素 */
.logo-icon {
  display: block;
  margin: 0 auto var(--space-3);
}

.card-title {
  font-size: var(--text-xl);
  font-weight: var(--weight-bold);
  color: var(--color-text);
  margin-bottom: var(--space-2);
}

.card-subtitle {
  font-size: var(--text-base);
  color: var(--color-text-dim);
}

.tab-group { margin-bottom: var(--space-6); }

/* 表單 */
.form-body {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
}

/* 欄位下方的說明（密碼規則） */
.field-hint {
  margin: 0;
  font-size: var(--text-xs);
  color: var(--color-text-dim);
  line-height: var(--leading-normal);
}

/* 錯誤訊息 */
.error-box {
  display: flex;
  align-items: flex-start;
  gap: var(--space-3);
  padding: var(--space-4) var(--space-5);
  background: var(--danger-50);
  border: var(--border-width) solid var(--danger-100);
  border-radius: var(--radius-md);
}

.error-icon {
  font-size: var(--text-lg);
  color: var(--danger-500);
  flex-shrink: 0;
  margin-top: var(--space-1);
}

.error-content {
  display: flex;
  flex-direction: column;
  gap: var(--space-1);
}

.error-content p {
  font-size: var(--text-sm);
  color: var(--danger-700);
  line-height: var(--leading-normal);
  margin: 0;
}

/* 提交按鈕：外觀走共用的 Btn，這裡只補登入卡片特有的滿寬與高度——
   登入頁只有一個動作，按鈕撐滿卡片寬度是這個版面的刻意設計，不是通用樣式。
   高度也比一般動作按鈕高一階：它是整張卡片唯一的送出動作。
   高度改 Btn 的 --btn-h（本體＋立體底邊），不要直接寫 min-height：底邊會多出一截。
   外框跟著加粗一階、圓盤跟著本體變大，跟首頁「開始查詢」同一個尺寸。 */
.login-submit {
  --btn-h: calc(var(--control-h) + var(--space-2));
  --btn-ring-w: var(--control-ring-w-lg);
  width: 100%;
  margin-top: var(--space-2);
  font-size: var(--text-base);
}
</style>