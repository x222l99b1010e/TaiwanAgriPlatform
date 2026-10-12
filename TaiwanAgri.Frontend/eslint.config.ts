import { globalIgnores } from 'eslint/config'
import { defineConfigWithVueTs, vueTsConfigs } from '@vue/eslint-config-typescript'
import pluginVue from 'eslint-plugin-vue'
import pluginOxlint from 'eslint-plugin-oxlint'
import skipFormatting from 'eslint-config-prettier/flat'

// To allow more languages other than `ts` in `.vue` files, uncomment the following lines:
// import { configureVueProject } from '@vue/eslint-config-typescript'
// configureVueProject({ scriptLangs: ['ts', 'tsx'] })
// More info at https://github.com/vuejs/eslint-config-typescript/#advanced-setup

export default defineConfigWithVueTs(
  {
    name: 'app/files-to-lint',
    files: ['**/*.{vue,ts,mts,tsx}'],
  },

  globalIgnores(['**/dist/**', '**/dist-ssr/**', '**/coverage/**']),

  ...pluginVue.configs['flat/essential'],
  vueTsConfigs.recommended,

  // 共用基礎元件（src/components/ui）允許單字命名。
  // vue/multi-word-component-names 的用意是避免元件與現有或未來的原生 HTML 元素撞名，
  // 而這個資料夾裡的名稱（Btn）不對應任何原生元素、也不會出現在自訂元素註冊路徑上。
  // 這幾個是全站每一頁都會用到的基礎元件，短名稱在使用端可讀性明顯較好，
  // 例外範圍因此限定在這一個資料夾，其餘檔案仍受規則約束。
  {
    name: 'app/ui-primitives-allow-single-word-names',
    files: ['src/components/ui/**/*.vue'],
    rules: { 'vue/multi-word-component-names': 'off' },
  },

  // 「今天是哪一天」一律問 src/utils/taiwanDate。toISOString().split('T')[0] 拿到的是 UTC 日期，
  // 台灣時間 00:00–08:00 之間會變成昨天，白天測試完全看不出來——所以交給 lint 擋，不靠記性。
  {
    name: 'app/dates-via-taiwan-date',
    files: ['src/**/*.{vue,ts}'],
    rules: {
      'no-restricted-syntax': [
        'error',
        {
          selector: "CallExpression[callee.property.name='split'][callee.object.callee.property.name='toISOString']",
          message: '這是 UTC 日期，台灣凌晨會變成昨天。改用 @/utils/taiwanDate 的 taiwanDateString／taiwanDateDaysAgo。',
        },
      ],
    },
  },

  ...pluginOxlint.buildFromOxlintConfigFile('.oxlintrc.json'),

  skipFormatting,
)
