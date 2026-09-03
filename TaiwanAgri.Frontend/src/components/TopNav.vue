<template>
  <header class="top-nav">
    <div class="top-nav-inner">
      <!-- 站名是回首頁的標準入口，但「可以點」這件事在畫面上看不出來，
           所以另外在分頁列第一格放一個明寫「首頁」的分頁——兩個入口都要。 -->
      <router-link to="/" class="logo" aria-label="回到首頁">
        <span class="mdi mdi-sprout logo-icon" />
        <span class="logo-text">台灣農業平台</span>
      </router-link>

      <nav class="module-tabs">
        <!-- 首頁不從 navStore 來：那份清單是後端 NavModules 資料表的四個模組，
             首頁不是模組，塞進去會讓「模組有哪些」這個語意變髒。 -->
        <div class="tab-wrapper">
          <router-link to="/" class="tab" :class="{ active: route.path === '/' }">
            <span class="mdi mdi-home-variant-outline" />
            首頁
          </router-link>
        </div>

        <div
          v-for="mod in navStore.modules"
          :key="mod.route"
          class="tab-wrapper"
          @mouseenter="hoveredRoute = mod.route"
          @mouseleave="hoveredRoute = null"
        >
          <router-link :to="mod.route" class="tab" :class="{ active: isActive(mod.route) }">
            <span :class="`mdi ${mod.icon}`" />
            {{ mod.name }}
          </router-link>

          <div
            class="tab-dropdown"
            v-if="mod.children && mod.children.length > 0 && hoveredRoute === mod.route"
          >
            <router-link
              v-for="child in mod.children"
              :key="child.route"
              :to="child.route"
              class="dropdown-item"
              :class="{ active: route.path === child.route }"
            >
              <span :class="`mdi ${child.icon}`" />
              {{ child.name }}
            </router-link>
          </div>
        </div>
      </nav>

      <div class="top-right">
        <NotificationBell />

        <!-- 已登入：顯示名稱 + 登出 -->
        <template v-if="authStore.isLoggedIn">
          <span class="user-name">{{ authStore.displayName }}</span>
          <router-link to="/profile" class="login-btn">農場設定</router-link>
          <router-link to="/watchlist" class="login-btn">監看清單</router-link>
          <button class="login-btn" @click="handleLogout">登出</button>
        </template>

        <!-- 未登入：登入按鈕 -->
        <button v-else class="login-btn login-btn--primary" @click="router.push('/login')">登入</button>
      </div>
    </div>
  </header>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useRoute } from 'vue-router'
import { useRouter } from 'vue-router'
import { useNavStore } from '@/stores/nav'
import { useAuthStore } from '@/stores/authStore'
import NotificationBell from '@/components/NotificationBell.vue'

const router = useRouter()
const route = useRoute()
const navStore = useNavStore()
const authStore = useAuthStore()
const hoveredRoute = ref<string | null>(null)

function isActive(moduleRoute: string) {
  return route.path === moduleRoute || route.path.startsWith(moduleRoute + '/')
}

function handleLogout() {
  authStore.logout()
  router.push('/login')
}
</script>

<style scoped>
/* 底色滿版、內容不滿版：外層只負責背景與高度，實際的排列與左右留白交給
   .top-nav-inner，寬度上限與頁面容器（base.css 的 .page）同一組 token，
   logo 的左邊界因此與各頁頁首標題落在同一條垂直線上。 */
.top-nav {
  height: 56px;
  background: var(--color-deep);
  color: var(--color-on-deep);
  position: relative;
  z-index: var(--z-dropdown);
  box-shadow: var(--shadow-md);
}

.top-nav-inner {
  display: flex;
  align-items: center;
  gap: var(--space-6);
  height: 100%;
  max-width: var(--container-lg);
  margin-inline: auto;
  padding-inline: var(--page-padding-x);
}

/* 站名要看得出來可以點：平常就給一點負留白與圓角當作可點區域的邊界，
   hover 時底色浮出來。只把游標改成手指是不夠的——那要滑過去才知道。 */
.logo {
  display: flex; align-items: center; gap: var(--space-2);
  font-size: var(--text-lg); font-weight: bold;
  color: var(--color-on-deep); text-decoration: none;
  margin-inline-start: calc(var(--space-3) * -1);
  padding: var(--space-2) var(--space-3);
  border-radius: var(--radius-md);
  transition: background var(--duration-fast) var(--ease-work);
}
.logo:hover { background: var(--white-a12); }
.logo:focus-visible { outline: 2px solid var(--color-action-on-deep); outline-offset: 2px; }
.logo-icon { font-size: var(--text-xl); color: var(--color-action-on-deep); }
.module-tabs { display: flex; gap: var(--space-1); flex: 1; }
.tab-wrapper { position: relative; }

.tab {
  display: flex; align-items: center; gap: var(--space-2);
  padding: var(--space-2) var(--space-4); border-radius: var(--radius-md);
  color: var(--color-on-deep-dim); text-decoration: none; font-size: var(--text-base);
  transition: background var(--duration-base);
  white-space: nowrap;
}
.tab:hover { background: var(--white-a12); }
.tab.active { background: var(--white-a20); color: var(--color-on-deep); font-weight: var(--weight-medium); }

/* 下拉選單本身是浮在深色列下面的淺色浮動層，不是深色列的延伸——
   跟頁面其餘的淺色卡片同一組色，才不會兩種底色系統在同一個選單裡混用 */
.tab-dropdown {
  position: absolute;
  top: 100%;
  left: 0;
  min-width: 160px;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  padding: var(--space-1) var(--space-2) var(--space-2);
  box-shadow: var(--shadow-lg);
  display: flex;
  flex-direction: column;
  gap: var(--space-1);
  z-index: var(--z-sticky);
}

.dropdown-item {
  display: flex; align-items: center; gap: var(--space-2);
  padding: var(--space-2) var(--space-4); border-radius: var(--radius-md);
  color: var(--color-text); text-decoration: none; font-size: var(--text-sm);
  transition: background var(--duration-fast), color var(--duration-fast);
  white-space: nowrap;
}
.dropdown-item:hover { background: var(--seed-50); color: var(--color-text); }
.dropdown-item.active { background: var(--seed-100); color: var(--color-action); font-weight: var(--weight-medium); }

.top-right { margin-left: auto; display: flex; align-items: center; gap: var(--space-2); }

/* 深色列上的按鈕不共用 Btn 元件：Btn 的三個變體全部是對「淺底」調的，
   把深底當第四個變體塞進去，等於讓那個元件同時背兩套底色系統。
   共用的是規格不是程式碼——高度、圓角、字級與 Btn 同一組 token。 */
.login-btn {
  display: inline-flex; align-items: center;
  min-height: var(--control-h-sm); padding: 0 var(--space-4);
  border-radius: var(--radius-md);
  border: 1px solid var(--color-deep-border-strong);
  background: transparent; color: var(--color-on-deep);
  font-family: inherit; font-size: var(--text-sm); font-weight: var(--weight-medium);
  letter-spacing: 0.02em; text-decoration: none; white-space: nowrap; cursor: pointer;
  transition:
    background var(--duration-fast) var(--ease-work),
    border-color var(--duration-fast) var(--ease-work);
}
.login-btn:hover { background: var(--white-a12); border-color: var(--color-on-deep-dim); }
.login-btn:focus-visible { outline: 2px solid var(--color-action-on-deep); outline-offset: 2px; }

/* 未登入時「登入」是這一列唯一的主要動作，給它實心的動作色；
   登入之後那三顆（農場設定／監看清單／登出）沒有主從之分，維持描邊。 */
.login-btn--primary {
  background: var(--color-action-on-deep);
  border-color: var(--color-action-on-deep);
  color: var(--color-deep);
  font-weight: var(--weight-bold);
}
.login-btn--primary:hover { background: var(--seed-300); border-color: var(--seed-300); }

.user-name {
  font-size: var(--text-base);
  color: var(--color-on-deep-dim);
  font-weight: var(--weight-medium);
}
</style>