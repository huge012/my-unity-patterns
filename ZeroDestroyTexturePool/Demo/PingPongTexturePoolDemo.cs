using UnityEngine;
using UnityEngine.UI;
using UnityPatterns.ZeroDestroyTexturePool;

namespace UnityPatterns.ZeroDestroyTexturePool.Demo
{
    /// <summary>
    /// 매 프레임 카메라 이미지를 수신해 RawImage에 표시하는 시나리오 시뮬레이션.
    /// 실제 XRCameraSubsystem 대신 Color32 배열로 픽셀을 흉내낸다.
    /// </summary>
    public class PingPongTexturePoolDemo : MonoBehaviour
    {
        [SerializeField] private RawImage _display;

        private const int W = 1280;
        private const int H = 720;

        private PingPongTexturePool _pool;
        private int _frameCount;

        private void Awake()
        {
            _pool = new PingPongTexturePool();
            _pool.PreAllocate(W, H); // 앱 시작 시 1회 — 이후 new Texture2D 호출 없음
        }

        private void Update()
        {
            // 1. 쓰기 대상 버퍼 획득 (할당 없음)
            Texture2D writeTarget = _pool.GetWriteTarget(W, H);
            if (writeTarget == null) return;

            // 2. 픽셀 데이터 쓰기 (실제로는 XRCpuImage.Convert → unsafe 포인터 사용)
            SimulateFrameWrite(writeTarget);

            // 3. GPU 업로드 후 버퍼 교체
            writeTarget.Apply();
            _pool.Flip(W, H);

            // 4. 렌더링에는 읽기 전용 버퍼를 사용
            _display.texture = _pool.GetReadTarget(W, H);

            _frameCount++;
        }

        private void OnDestroy() => _pool.Dispose();

        // 픽셀을 프레임마다 색상을 바꿔 채우는 시뮬레이션
        private void SimulateFrameWrite(Texture2D tex)
        {
            float t = (_frameCount % 60) / 60f;
            Color32 color = Color32.Lerp(new Color32(0, 80, 180, 255),
                                         new Color32(0, 180, 80, 255), t);
            Color32[] pixels = new Color32[W * H];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            tex.SetPixels32(pixels);
        }
    }
}
