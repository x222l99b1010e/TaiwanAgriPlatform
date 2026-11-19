import './assets/main.css'

import { createApp } from 'vue'
import { createPinia } from 'pinia'

import App from './App.vue'
import router from './router'
import { setUnauthorizedHandler } from '@/api/httpBase'
import { useAuthStore } from '@/stores/authStore'
import { shouldRedirectToLogin } from '@/utils/authRedirect'
import '@mdi/font/css/materialdesignicons.css'

// Leaflet 地圖（模組 3：毛小孩守護地圖）樣式，全域載入一次即可
import 'leaflet/dist/leaflet.css'
import 'leaflet.markercluster/dist/MarkerCluster.css'
import 'leaflet.markercluster/dist/MarkerCluster.Default.css'
import { fixLeafletDefaultIcon } from '@/utils/leafletIconFix'

fixLeafletDefaultIcon()

const app = createApp(App)

app.use(createPinia())
app.use(router)

// 任一請求收到 401（多半是 token 過期）時，把登入狀態清乾淨；
// 在要登入的頁面才導向登入頁，公開頁留在原地（理由見 shouldRedirectToLogin）。
// 註冊在這裡而不是寫進 httpBase：那支若直接 import router 與 store 會形成循環引用。
setUnauthorizedHandler(async () => {
  useAuthStore().logout()
  // 直接開網址（書籤、重新整理）時，401 可能在第一次導頁完成之前就回來，那時 currentRoute
  // 還停在起始的 "/"：拿它判斷「這頁要不要登入」會判錯，記成 redirect 的話登入後也會回到首頁。
  // 等第一次導頁完成再讀（已經完成的話 isReady 立刻 resolve）；第一次導頁本身失敗時照樣往下走
  await router.isReady().catch(() => {})
  const current = router.currentRoute.value
  if (shouldRedirectToLogin(current)) {
    router.push({ name: 'login', query: { redirect: current.fullPath } })
  }
})

app.mount('#app')
