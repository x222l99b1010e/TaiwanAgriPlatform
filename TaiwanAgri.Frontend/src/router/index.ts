// src/router/index.ts
// 職責：定義前台路由，每個路徑對應一個 View 元件

import { createRouter, createWebHistory } from 'vue-router'
import MarketView from '@/views/MarketView.vue'
import PlaceholderView from '@/views/PlaceholderView.vue'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    { path: '/', redirect: '/market' },
    {
      path: '/market',
      component: MarketView,
      children: [
        { path: 'prices', component: PlaceholderView },
        { path: 'disasters', component: PlaceholderView },
        { path: 'rest-days', component: PlaceholderView },
        { path: 'pork', component: PlaceholderView },
      ]
    },
    {
      path: '/weather',
      component: PlaceholderView,
      children: [
        { path: 'station', component: PlaceholderView },
        { path: 'rainfall', component: PlaceholderView },
        { path: 'pest-alerts', component: PlaceholderView },
        { path: 'pest-decade', component: PlaceholderView },
        { path: 'notifications', component: PlaceholderView },
      ]
    },
    { path: '/food-safety', component: PlaceholderView },
    { path: '/pet', component: PlaceholderView },
  ]
})

export default router