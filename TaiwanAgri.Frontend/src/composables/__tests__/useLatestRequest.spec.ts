import { describe, it, expect } from 'vitest'
import { useLatestRequest } from '../useLatestRequest'

describe('useLatestRequest', () => {
  it('單一請求：自己就是最新', () => {
    const req = useLatestRequest()
    const seq = req.next()
    expect(req.isLatest(seq)).toBe(true)
  })

  it('連續請求：舊序號失效、新序號有效（防舊回應蓋掉新結果）', () => {
    const req = useLatestRequest()
    const first = req.next()
    const second = req.next()

    // 模擬「先發出的請求較晚回來」：first 的回應抵達時已不是最新
    expect(req.isLatest(first)).toBe(false)
    expect(req.isLatest(second)).toBe(true)
  })

  it('三連發：只有最後一次有效', () => {
    const req = useLatestRequest()
    const seqs = [req.next(), req.next(), req.next()]

    expect(seqs.map(s => req.isLatest(s))).toEqual([false, false, true])
  })

  it('不同實例各自獨立計數（違規牆與有機驗證互不干擾）', () => {
    const a = useLatestRequest()
    const b = useLatestRequest()

    const seqA = a.next()
    b.next()
    b.next()

    // b 的請求不影響 a 的最新判定
    expect(a.isLatest(seqA)).toBe(true)
  })
})
