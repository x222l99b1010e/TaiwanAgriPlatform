import './assets/main.css'

import { createApp } from 'vue'
import { createPinia } from 'pinia'

import App from './App.vue'
import router from './router'
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

app.mount('#app')
