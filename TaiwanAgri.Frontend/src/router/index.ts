// src/router/index.ts
// 職責：定義前台路由，每個路徑對應一個 View 元件

import { createRouter, createWebHistory } from 'vue-router'
import MarketView from '@/views/MarketView.vue'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/',
      redirect: '/market',   // 根路徑自動跳轉到 market
    },
    {
      path: '/market',
      name: 'market',
      component: MarketView,
    },
  ],
})

export default router
