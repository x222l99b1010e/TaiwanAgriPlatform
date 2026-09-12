import { defineStore } from 'pinia'
import { ref } from 'vue'
import { fetchNavModules, type NavModule } from '@/api/nav'

export const useNavStore = defineStore('nav', () => {
  const modules = ref<NavModule[]>([])
  /**
   * 「載過了沒」要自己記一個旗標，不能拿 modules.length 代替。
   * 角色的模組權限被全部收掉時後端是合法地回傳空陣列，用長度判斷會把
   * 「載完了，你一個模組都看不到」讀成「還在載入」——畫面會永遠停在轉圈，
   * 而且 loadModules 的早退條件永遠不成立，每次呼叫都重打一次 API。
   * 失敗時不設旗標，所以下一次仍然會重試。
   */
  const loaded = ref(false)

  async function loadModules() {
    if (loaded.value) return
    modules.value = await fetchNavModules()
    loaded.value = true
  }

  function currentModule(path: string) {
    return modules.value.find(m => path === m.route || path.startsWith(m.route + '/')) ?? null
  }

  return { modules, loaded, loadModules, currentModule }
})