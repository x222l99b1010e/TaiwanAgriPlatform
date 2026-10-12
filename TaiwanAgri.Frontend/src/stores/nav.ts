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
   */
  const loaded = ref(false)

  /**
   * 「載失敗了」要跟「還在載入」分成兩個旗標，不能只靠 loaded 的反面。
   * 失敗時 loaded 刻意維持 false（下一次呼叫才會重試），所以只看 loaded 的話
   * 失敗與載入中長得一模一樣，畫面會永遠停在轉圈、沒有錯誤訊息也沒有重試鈕。
   *
   * 這件事在部署之後才會常態發生：主機沒有 Always On、資料庫會自動暫停，
   * 閒置之後的第一個請求必然撞到冷啟動，逾時就是這條路徑。
   */
  const loadFailed = ref(false)

  /**
   * 同一時間只發一次請求。有了重試鈕之後，連點兩下就是兩個請求——
   * 而重試的情境正好是「伺服器現在很慢」，重複請求只會讓它更慢。
   * 用一般變數不用 ref：畫面不需要知道它，做成響應式只會多一個沒人讀的狀態。
   */
  let inFlight: Promise<void> | null = null

  async function loadModules() {
    if (loaded.value) return
    if (inFlight) return inFlight

    loadFailed.value = false
    inFlight = (async () => {
      try {
        modules.value = await fetchNavModules()
        loaded.value = true
      } catch (err) {
        // 這裡吞掉例外而不是往上拋：呼叫端只有 onMounted，
        // 拋出去的 Promise 沒有人接，結果是主控台一句 unhandled rejection、
        // 畫面上什麼都沒有。狀態留在 loadFailed，由畫面決定要說什麼
        loadFailed.value = true
        console.error('[nav] 模組清單載入失敗', err)
      } finally {
        inFlight = null
      }
    })()

    return inFlight
  }

  function currentModule(path: string) {
    return modules.value.find(m => path === m.route || path.startsWith(m.route + '/')) ?? null
  }

  return { modules, loaded, loadFailed, loadModules, currentModule }
})