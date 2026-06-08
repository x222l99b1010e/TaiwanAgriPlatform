// src/router/index.ts
// 職責：定義前台路由，每個路徑對應一個 View 元件

import { createRouter, createWebHistory } from 'vue-router'
import MarketView from '@/views/MarketView.vue'
import PricesView from '@/views/market/PricesView.vue'
import DisastersView from '@/views/market/DisastersView.vue'
import RestDaysView from '@/views/market/RestDaysView.vue'
import PlaceholderView from '@/views/PlaceholderView.vue'
import WeatherView from '@/views/WeatherView.vue'
import StationView from '@/views/weather/StationView.vue'
import RainfallView from '@/views/weather/RainfallView.vue'
import PestAlertsView from '@/views/weather/PestAlertsView.vue'
import PestDecadeView from '@/views/weather/PestDecadeView.vue'
import PorkView from '@/views/market/PorkView.vue'
import LoginView from '@/views/auth/LoginView.vue'
import ProfileView from '@/views/ProfileView.vue'


const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    { path: '/login', component: LoginView },
    { path: '/', redirect: '/market/prices' },
    {
      path: '/market',
      component: MarketView,
      children: [
        { path: 'prices', component: PricesView },
        { path: 'disasters', component: DisastersView },
        { path: 'rest-days', component: RestDaysView },
        { path: 'pork', component: PorkView },
      ]
    },
    {
      path: '/weather',
      component: WeatherView,
      children: [
        { path: 'station', component: StationView },
        { path: 'rainfall', component: RainfallView },
        { path: 'pest-alerts', component: PestAlertsView },
        { path: 'pest-decade', component: PestDecadeView },
        // { path: 'notifications', component: PlaceholderView },
      ]
    },
    { path: '/food-safety', component: PlaceholderView },
    { path: '/pet', component: PlaceholderView },

    { path: '/profile', component: ProfileView },
  ]
})

export default router