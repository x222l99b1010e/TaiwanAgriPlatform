import './assets/main.css'

import { createApp } from 'vue'
import { createPinia } from 'pinia'

import App from './App.vue'
import router from './router'
import { setUnauthorizedHandler } from '@/api/httpBase'
import { useAuthStore } from '@/stores/authStore'
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

// 任一請求收到 401（多半是 token 過期）時，把登入狀態清乾淨並導向登入頁。
// 註冊在這裡而不是寫進 httpBase：那支若直接 import router 與 store 會形成循環引用。
setUnauthorizedHandler(() => {
  useAuthStore().logout()
  if (router.currentRoute.value.name !== 'login') {
    router.push({ name: 'login', query: { redirect: router.currentRoute.value.fullPath } })
  }
})

app.mount('#app')
