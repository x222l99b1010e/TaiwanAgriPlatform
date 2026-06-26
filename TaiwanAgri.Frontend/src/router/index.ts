// src/router/index.ts
// 職責：定義前台路由，每個路徑對應一個 View 元件

import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/authStore'

import MarketView      from '@/views/MarketView.vue'
import PricesView      from '@/views/market/PricesView.vue'
import DisastersView   from '@/views/market/DisastersView.vue'
import RestDaysView    from '@/views/market/RestDaysView.vue'
import PorkView        from '@/views/market/PorkView.vue'

import WeatherView     from '@/views/WeatherView.vue'
import StationView     from '@/views/weather/StationView.vue'
import RainfallView    from '@/views/weather/RainfallView.vue'
import PestAlertsView  from '@/views/weather/PestAlertsView.vue'
import PestDecadeView  from '@/views/weather/PestDecadeView.vue'

import LoginView       from '@/views/auth/LoginView.vue'
import ProfileView     from '@/views/ProfileView.vue'
import WatchlistView from '@/views/WatchlistView.vue'
import PlaceholderView from '@/views/PlaceholderView.vue'

import FoodSafetyView  from '@/views/FoodSafetyView.vue'
import TodayVegView    from '@/views/food-safety/TodayVegView.vue'

// ── 路由定義 ───────────────────────────────────────────────────────────────
const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    // 公開路由
    { path: '/login', name: 'login', component: LoginView },
    { path: '/', name: 'home', redirect: '/market/prices' },

    // 市場（公開，巢狀）
    {
      path: '/market',
      component: MarketView,
      children: [
        { path: '',          redirect: '/market/prices' },
        { path: 'prices',    component: PricesView },
        { path: 'disasters', component: DisastersView },
        { path: 'rest-days', component: RestDaysView },
        { path: 'pork',      component: PorkView },
      ]
    },

    // 氣象（公開，巢狀）
    {
      path: '/weather',
      component: WeatherView,
      children: [
        { path: '',            redirect: '/weather/station' },
        { path: 'station',     component: StationView },
        { path: 'rainfall',    component: RainfallView },
        { path: 'pest-alerts', component: PestAlertsView },
        { path: 'pest-decade', component: PestDecadeView },
      ]
    },

    // 其他公開頁（佔位）
    {
      path: '/food-safety',
      component: FoodSafetyView,
      children: [
        { path: '',          redirect: '/food-safety/today-veg' },
        { path: 'today-veg', component: TodayVegView },
      ]
    },
    { path: '/pet',         component: PlaceholderView },

    // ✅ 受保護路由：需登入才能訪問
    { path: '/profile', name: 'profile', component: ProfileView, meta: { requiresAuth: true } },
    { path: '/watchlist', name: 'watchlist', component: WatchlistView, meta: {requiresAuth: true}},
  ]
})

// ── Navigation Guard ───────────────────────────────────────────────────────
// beforeEach：每次路由跳轉前執行，檢查登入狀態
router.beforeEach((to, _from) => {
  // useAuthStore 在此處呼叫（非頂層），確保 Pinia 已初始化
  const authStore = useAuthStore()
  const isAuthenticated = !!authStore.token

  if (to.meta.requiresAuth && !isAuthenticated) {
    // 目標路由需要登入，但尚未登入 → 導向登入頁，並記錄原目標路徑
    return { name: 'login', query: { redirect: to.fullPath } }
  }
  
  if (to.name === 'login' && isAuthenticated) {
    // 已登入卻試圖訪問登入頁 → 導回首頁
    return { name: 'home' }
  }

  // 其他情況一律放行
  return true
})

export default router