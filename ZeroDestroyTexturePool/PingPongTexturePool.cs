using System.Collections.Generic;
using UnityEngine;

namespace UnityPatterns.ZeroDestroyTexturePool
{
    /// <summary>
    /// 런타임 중 Texture2D의 new/Destroy를 완전히 제거하는 핑퐁 버퍼 풀.
    ///
    /// 출처: ARGlass Launcher V2 (CameraDevice.cs)
    ///
    /// 문제:
    ///   슬립/웨이크업 사이클에서 카메라 프레임마다 new Texture2D() + Destroy()가
    ///   반복되면 드라이버 핸들이 소진됨. 슬립/웨이크업 시 프리뷰 완전 멈춤 + 크래시의
    ///   근본 원인.
    ///
    /// 해결:
    ///   앱 시작 시 해상도별로 Texture2D 2장을 미리 할당(핑퐁 구조).
    ///   이후 런타임에서는 할당/해제 없이 교대로 씀.
    ///   GC 발생 = 0. 드라이버 핸들 소진 = 없음.
    ///
    /// 사용 방법:
    ///   1. PreAllocate(width, height) — 앱 시작 시 1회 호출
    ///   2. GetWriteTarget(width, height) — 매 프레임 쓰기 대상 Texture2D 획득
    ///   3. Flip(width, height) — GPU 업로드(Apply()) 후 호출해 버퍼 교체
    ///   4. GetReadTarget(width, height) — 렌더링에 사용할 최신 Texture2D 획득
    /// </summary>
    public class PingPongTexturePool
    {
        // key = width * 10000 + height (해상도별 독립 풀)
        // 대상 기기 최대 해상도가 4K(3840×2160)이므로 int 범위 내 안전:
        //   3840 * 10000 + 2160 = 38,402,160 (int 상한 약 21억 대비 충분한 여유)
        // 5K 이상 지원이 필요하다면 (long)w << 16 | h 방식으로 교체할 것.
        private readonly Dictionary<int, Texture2D[]> _pool
            = new Dictionary<int, Texture2D[]>();

        private readonly Dictionary<int, int> _writeIndex
            = new Dictionary<int, int>();

        private static int MakeKey(int w, int h) => w * 10000 + h;

        // ── 초기화 ───────────────────────────────────────────────────

        /// <summary>
        /// 앱 시작 시 1회 호출. 이후 런타임에서 new Texture2D는 절대 호출하지 않음.
        /// </summary>
        public void PreAllocate(int width, int height,
                                TextureFormat format = TextureFormat.RGBA32)
        {
            int key = MakeKey(width, height);
            if (_pool.ContainsKey(key)) return;

            _pool[key] = new Texture2D[2]
            {
                new Texture2D(width, height, format, false, false),
                new Texture2D(width, height, format, false, false),
            };
            _pool[key][0].Apply();
            _pool[key][1].Apply();
            _writeIndex[key] = 0;
        }

        // ── 프레임마다 사용 ─────────────────────────────────────────

        /// <summary>이번 프레임에 픽셀을 써야 할 Texture2D를 반환.</summary>
        public Texture2D GetWriteTarget(int width, int height)
        {
            int key = MakeKey(width, height);
            if (!_pool.TryGetValue(key, out var textures)) return null;
            return textures[_writeIndex[key]];
        }

        /// <summary>
        /// GPU 업로드(Apply()) 완료 후 호출. 버퍼 인덱스 교체.
        /// 이후 GetReadTarget()은 방금 업로드된 텍스처를 반환.
        /// </summary>
        public void Flip(int width, int height)
        {
            int key = MakeKey(width, height);
            if (!_writeIndex.ContainsKey(key)) return;
            _writeIndex[key] = (_writeIndex[key] + 1) % 2;
        }

        /// <summary>렌더링에 사용할 최신 Texture2D (Flip 후 이전 write 버퍼) 반환.</summary>
        public Texture2D GetReadTarget(int width, int height)
        {
            int key = MakeKey(width, height);
            if (!_pool.TryGetValue(key, out var textures)) return null;
            int readIndex = (_writeIndex[key] + 1) % 2;
            return textures[readIndex];
        }

        // ── 정리 ────────────────────────────────────────────────────

        /// <summary>앱 종료 시 호출. 유일하게 Destroy가 허용되는 지점.</summary>
        public void Dispose()
        {
            foreach (var textures in _pool.Values)
                foreach (var tex in textures)
                    if (tex != null) Object.Destroy(tex);
            _pool.Clear();
            _writeIndex.Clear();
        }
    }
}
