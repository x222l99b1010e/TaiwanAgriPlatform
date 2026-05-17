import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { fetchNavModules, type NavModule } from '@/api/nav'
import { useRoute } from 'vue-router'

export const useNavStore = defineStore('nav', () => {
  const modules = ref<NavModule[]>([])

  async function loadModules() {
    if (modules.value.length > 0) return
    modules.value = await fetchNavModules()
  }

  function currentModule(path: string) {
    return modules.value.find(m => path === m.route || path.startsWith(m.route + '/')) ?? null
  }

  return { modules, loadModules, currentModule }
})