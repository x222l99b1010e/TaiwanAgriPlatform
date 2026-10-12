/**
 * 「伺服器喚醒中」監看器的測試。
 *
 * 時鐘與健康檢查都由測試注入：時間用變數 t 推進，tick 由測試自己呼叫，
 * 健康檢查回傳一個由測試決定何時、以什麼結果結束的 promise——
 * 這樣「等了幾秒、哪一支先回來」都能逐步控制，不必真的等。
 */
import { describe, it, expect, beforeEach } from 'vitest'
import { createSSRApp, h } from 'vue'
import { renderToString } from 'vue/server-renderer'
import {
  createServerWakeMonitor,
  describeWake,
  serverWake,
  wakePhaseOf,
  SLOW_AFTER_MS,
  type ProbePath,
} from '@/api/serverWake'
import ServerWakeNotice from '@/components/ServerWakeNotice.vue'

interface PendingProbe {
  path: ProbePath
  signal: AbortSignal
  resolve: (ok: boolean) => void
}

let t = 0
let probes: PendingProbe[] = []

function makeMonitor() {
  return createServerWakeMonitor({
    now: () => t,
    startTicking: () => () => {},
    retryMs: 5_000,
    probe: (path, signal) =>
      new Promise<boolean>(resolve => {
        probes.push({ path, signal, resolve })
      }),
  })
}

/** 讓 probe 的 then 回呼跑完 */
const flush = () => new Promise(resolve => setTimeout(resolve, 0))

beforeEach(() => {
  t = 0
  probes = []
})

describe('wakePhaseOf', () => {
  it('/health 還沒過＝伺服器還在啟動', () => {
    expect(wakePhaseOf('unknown', 'unknown')).toBe('server')
    expect(wakePhaseOf('failed', 'unknown')).toBe('server')
  })
  it('/health 過了、/health/ready 還沒過＝資料庫還在醒', () => {
    expect(wakePhaseOf('ok', 'unknown')).toBe('database')
    expect(wakePhaseOf('ok', 'failed')).toBe('database')
  })
  it('兩支都過＝只是查詢本身比較久', () => {
    expect(wakePhaseOf('ok', 'ok')).toBe('query')
  })
})

describe('describeWake', () => {
  it('伺服器那一段的標題是「伺服器喚醒中」', () => {
    expect(describeWake('server').title).toBe('伺服器喚醒中')
  })
  it('三個階段的標題都不一樣', () => {
    const titles = new Set((['server', 'database', 'query'] as const).map(p => describeWake(p).title))
    expect(titles.size).toBe(3)
  })
})

describe('createServerWakeMonitor', () => {
  it('請求等不到門檻時不說明、也不打健康檢查', () => {
    const m = makeMonitor()
    m.requestStarted()
    t = SLOW_AFTER_MS - 1
    m.tick()
    expect(m.state.phase).toBe('idle')
    expect(probes).toHaveLength(0)
  })

  it('等超過門檻：先說「伺服器喚醒中」並問 /health', () => {
    const m = makeMonitor()
    m.requestStarted()
    t = SLOW_AFTER_MS
    m.tick()
    expect(m.state.phase).toBe('server')
    expect(m.state.waitedSeconds).toBe(SLOW_AFTER_MS / 1_000)
    expect(probes.map(p => p.path)).toEqual(['/health'])
  })

  it('/health 回來 → 改說資料庫、接著問 /health/ready；也回來 → 改說查詢中', async () => {
    const m = makeMonitor()
    m.requestStarted()
    t = SLOW_AFTER_MS
    m.tick()
    probes[0]!.resolve(true)
    await flush()
    expect(m.state.phase).toBe('database')

    t += 1_000
    m.tick()
    expect(probes.map(p => p.path)).toEqual(['/health', '/health/ready'])
    probes[1]!.resolve(true)
    await flush()
    expect(m.state.phase).toBe('query')

    t += 1_000
    m.tick()
    expect(probes).toHaveLength(2) // 兩支都過了就不再問
  })

  it('健康檢查沒過：隔一段時間才再問，不會每一秒都打', async () => {
    const m = makeMonitor()
    m.requestStarted()
    t = SLOW_AFTER_MS
    m.tick()
    probes[0]!.resolve(false)
    await flush()

    t += 4_000
    m.tick()
    expect(probes).toHaveLength(1)
    t += 1_000
    m.tick()
    expect(probes.map(p => p.path)).toEqual(['/health', '/health'])
  })

  it('健康檢查 reject（例如被 CORS 擋下）當成沒過，畫面停在伺服器那一段', async () => {
    const m = createServerWakeMonitor({
      now: () => t,
      startTicking: () => () => {},
      probe: () => Promise.reject(new Error('Network Error')),
    })
    m.requestStarted()
    t = SLOW_AFTER_MS
    m.tick()
    await flush()
    expect(m.state.phase).toBe('server')
  })

  it('請求都回來了：收起說明、取消還在跑的健康檢查，晚到的結果不算數', async () => {
    const m = makeMonitor()
    const end = m.requestStarted()
    t = SLOW_AFTER_MS
    m.tick()
    end()
    expect(m.state.phase).toBe('idle')
    expect(probes[0]!.signal.aborted).toBe(true)

    probes[0]!.resolve(true)
    await flush()
    expect(m.state.phase).toBe('idle')

    // 晚到的「/health 過了」不能留到下一次：下一次等太久時要從 /health 重新問起，
    // 否則伺服器明明又被收起來了，畫面卻說「伺服器已啟動」
    m.requestStarted()
    t += SLOW_AFTER_MS
    m.tick()
    expect(m.state.phase).toBe('server')
    expect(probes.map(p => p.path)).toEqual(['/health', '/health'])
  })

  it('等最久的那個回來、剩下的都還很新：先收起說明', () => {
    const m = makeMonitor()
    const endOld = m.requestStarted()
    t = SLOW_AFTER_MS - 2_000
    m.requestStarted()
    t = SLOW_AFTER_MS
    m.tick()
    expect(m.state.phase).toBe('server')

    endOld()
    m.tick()
    expect(m.state.phase).toBe('idle')
  })

  it('結束函式重複呼叫無害，不會把別的請求一起結掉', () => {
    const m = makeMonitor()
    const endA = m.requestStarted()
    m.requestStarted()
    endA()
    endA()
    t = SLOW_AFTER_MS
    m.tick()
    expect(m.state.phase).toBe('server')
  })
})

describe('ServerWakeNotice', () => {
  async function render() {
    return renderToString(createSSRApp({ render: () => h(ServerWakeNotice) }))
  }

  it('沒有在等的時候什麼都不畫', async () => {
    serverWake.state.phase = 'idle'
    expect(await render()).not.toContain('wake-notice')
  })

  it('在等的時候畫出標題與已等待秒數，秒數不放在報讀區塊裡', async () => {
    serverWake.state.phase = 'server'
    serverWake.state.waitedSeconds = 12
    const html = await render()
    expect(html).toContain('伺服器喚醒中')
    expect(html).toContain('已等待 12 秒')
    expect(html).toMatch(/role="status"[^>]*>伺服器喚醒中</)
    serverWake.state.phase = 'idle'
    serverWake.state.waitedSeconds = 0
  })
})
