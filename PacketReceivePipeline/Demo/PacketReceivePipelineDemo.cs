using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityPatterns.PacketReceivePipeline;
using UnityPatterns;

namespace UnityPatterns.PacketReceivePipeline.Demo
{
    /// <summary>
    /// TcpConnectionManager + PacketReceivePipeline 조합 데모.
    ///
    /// 이 두 클래스의 조합이 실제 프로젝트(AXFactory)에서
    /// 네트워크 수신 전체를 처리하던 구조다:
    ///
    ///   TcpConnectionManager          → 연결 관리 (재시도 포함)
    ///   PacketReceivePipeline.Feed()  → 수신 데이터를 파이프라인에 투입
    ///   Register()                    → 패킷 ID별 게임 로직 연결
    ///   GetCached()                   → 늦게 합류한 컴포넌트의 상태 복원
    /// </summary>
    public class PacketReceivePipelineDemo : MonoBehaviour
    {
        [Header("네트워크")]
        [SerializeField] private TcpConnectionManager       _connectionManager;
        [SerializeField] private PacketReceivePipeline<DemoPacketId> _pipeline;

        [Header("UI")]
        [SerializeField] private Text _objectNameText;
        [SerializeField] private Text _commandText;
        [SerializeField] private Text _statusText;

        private void Awake()
        {
            // TcpConnectionManager 수신 → Pipeline으로 투입
            // OnReceived는 백그라운드 스레드에서 발동, Feed() 내부가 메인스레드로 전달
            _connectionManager.OnReceived += bytes => _pipeline.Feed(bytes);

            _connectionManager.OnConnected    += () => _statusText.text = "Connected";
            _connectionManager.OnDisconnected += () => _statusText.text = "Disconnected";
            _connectionManager.OnGaveUp       += () => _statusText.text = "Failed — 서버 확인 필요";

            // 패킷 ID별 핸들러 등록
            _pipeline.Register(DemoPacketId.ObjectInfo,  OnObjectInfo);
            _pipeline.Register(DemoPacketId.VoiceCommand, OnVoiceCommand);
        }

        private void Start()
        {
            _connectionManager.Connect();

            // 이미 이전에 수신된 캐시가 있으면 즉시 표시 (늦게 합류한 경우)
            var cachedObj = _pipeline.GetCached(DemoPacketId.ObjectInfo);
            if (cachedObj != null) OnObjectInfo(cachedObj);
        }

        private void OnDestroy()
        {
            _pipeline.Unregister(DemoPacketId.ObjectInfo,  OnObjectInfo);
            _pipeline.Unregister(DemoPacketId.VoiceCommand, OnVoiceCommand);
        }

        // ── 패킷 핸들러 (메인스레드에서 호출됨) ─────────────────────

        private void OnObjectInfo(byte[] payload)
        {
            // 실제로는 바이너리 파싱 (AXFactory의 RspObjectInfoNPU 구조)
            // 여기서는 단순화를 위해 UTF8 문자열로 처리
            _objectNameText.text = $"인식: {Encoding.UTF8.GetString(payload)}";
        }

        private void OnVoiceCommand(byte[] payload)
        {
            _commandText.text = $"명령: {Encoding.UTF8.GetString(payload)}";
        }
    }

    public enum DemoPacketId
    {
        ObjectInfo   = 0x2000,  // NPU 사물인식 결과
        VoiceCommand = 0x2001,  // 음성 명령어
        Hotword      = 0x2002,  // 시동어 감지
        OcrResult    = 0x2003,  // OCR 결과
    }
}
