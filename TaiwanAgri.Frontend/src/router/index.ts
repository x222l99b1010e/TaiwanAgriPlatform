// src/router/index.ts
// 職責：定義前台路由，每個路徑對應一個 View 元件

import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/authStore'

// ── View 動態載入 ─────────────────────────────────────────────────────────
// 每個 View 用 () => import(...) 而非頂層 import：Vite 會把它們各自切成獨立 chunk，
// 進到那條路由才下載。靜態 import 會讓 Leaflet、MarkerCluster、Chart.js 這些只有特定
// 頁面用得到的函式庫全部打進首屏那一包——改動前實測單一 chunk 701 kB，
// 只想看首頁的人也得先下載整個地圖與圖表函式庫。
const HomeView = () => import('@/views/HomeView.vue')
const MarketView = () => import('@/views/MarketView.vue')
const PricesView = () => import('@/views/market/PricesView.vue')
const DisastersView = () => import('@/views/market/DisastersView.vue')
const RestDaysView = () => import('@/views/market/RestDaysView.vue')
const PorkView = () => import('@/views/market/PorkView.vue')
const PoultryView = () => import('@/views/market/PoultryView.vue')
const WeatherView = () => import('@/views/WeatherView.vue')
const StationView = () => import('@/views/weather/StationView.vue')
const RainfallView = () => import('@/views/weather/RainfallView.vue')
const PestAlertsView = () => import('@/views/weather/PestAlertsView.vue')
const PestDecadeView = () => import('@/views/weather/PestDecadeView.vue')
const PesticideSearchView = () => import('@/views/weather/PesticideSearchView.vue')
const LoginView = () => import('@/views/auth/LoginView.vue')
const ProfileView = () => import('@/views/ProfileView.vue')
const WatchlistView = () => import('@/views/WatchlistView.vue')
const FoodSafetyView = () => import('@/views/FoodSafetyView.vue')
const TodayVegView = () => import('@/views/food-safety/TodayVegView.vue')
const TraceabilityView = () => import('@/views/food-safety/TraceabilityView.vue')
const ViolationWallView = () => import('@/views/food-safety/ViolationWallView.vue')
const OrganicCertView = () => import('@/views/food-safety/OrganicCertView.vue')
const PetView = () => import('@/views/PetView.vue')
const ShelterMapView = () => import('@/views/pet/ShelterMapView.vue')
const ShelterDetailView = () => import('@/views/pet/ShelterDetailView.vue')
const AnimalDetailView = () => import('@/views/pet/AnimalDetailView.vue')
const LostPetsView = () => import('@/views/pet/LostPetsView.vue')
const LostPetDetailView = () => import('@/views/pet/LostPetDetailView.vue')
const LegalBusinessView = () => import('@/views/pet/LegalBusinessView.vue')
const MyLostPetsView = () => import('@/views/pet/MyLostPetsView.vue')

// ── 路由定義 ───────────────────────────────────────────────────────────────
const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    // 公開路由
    { path: '/login', name: 'login', component: LoginView },
    { path: '/', name: 'home', component: HomeView },

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
        { path: 'poultry',   component: PoultryView },
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
        { path: 'pesticides',  component: PesticideSearchView },
      ]
    },

    // 食安（公開，巢狀）
    {
      path: '/food-safety',
      component: FoodSafetyView,
      children: [
        { path: '',              redirect: '/food-safety/traceability' },
        { path: 'today-veg',    component: TodayVegView },
        { path: 'traceability', component: TraceabilityView },
        { path: 'pest-violation', component: ViolationWallView },
        { path: 'organic-certifications', component: OrganicCertView },
      ]
    },

    // 寵物（模組 3：毛小孩守護地圖，公開，巢狀）
    {
      path: '/pet',
      component: PetView,
      children: [
        { path: '',               redirect: '/pet/shelter-map' },
        { path: 'shelter-map',    component: ShelterMapView },
        // 動態路由的 :xxx 參數在 route.params 裡永遠是字串（就算網址長得像數字）。
        // 用 props 函式模式在進入元件前就轉成 number，元件收到的是乾淨的 number prop，
        // 不用自己 import useRoute() 再手動 Number(route.params.xxx) 轉型——
        // 這是 Vue Router 讓元件跟路由解耦的標準寫法：元件只認 props，不用知道自己活在路由裡。
        // 這裡不加 vue-router 3/4 慣用的 `:id(\\d+)` 自訂正則──實測這個專案裝的 vue-router 5
        // 新版 matcher 不吃這個語法（route 完全比對不到，靜靜 404），不是網路上舊教學那樣通用。
        // 非數字 id 交給後端擋：Controller 端點已經是 `{id:int}` 路由約束，格式不對直接 404，
        // 前端這邊本來就有處理 404 的錯誤畫面，不需要在前端重複同一層防禦。
        {
          path: 'shelter-map/:shelterId',
          component: ShelterDetailView,
          props: route => ({ shelterId: Number(route.params.shelterId) }),
        },
        // 動物詳情頁刻意放在 shelter-map/animals/ 這個固定字首下、不帶 shelterId
        // （選定方案）：跟上面 lost-pets/:id 同一種風格，用 animalId 自己
        // 就能定位資料，不需要在網址裡重複描述牠屬於哪間收容所
        {
          path: 'shelter-map/animals/:animalId',
          component: AnimalDetailView,
          props: route => ({ animalId: Number(route.params.animalId) }),
        },
        { path: 'lost-pets',      component: LostPetsView },
        {
          path: 'lost-pets/:id',
          component: LostPetDetailView,
          props: route => ({ id: Number(route.params.id) }),
        },
        { path: 'legal-business', component: LegalBusinessView },
      ]
    },

    // ✅ 受保護路由：需登入才能訪問
    { path: '/profile',   name: 'profile',   component: ProfileView,   meta: { requiresAuth: true } },
    { path: '/watchlist', name: 'watchlist', component: WatchlistView, meta: { requiresAuth: true } },
    // 「我的協尋貼文」個人管理頁（不掛週次分支新增，放在 /profile 底下＋
    // pet 模組內也放一個連結過去，兩處都要）
    { path: '/profile/lost-pets', name: 'my-lost-pets', component: MyLostPetsView, meta: { requiresAuth: true } },
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