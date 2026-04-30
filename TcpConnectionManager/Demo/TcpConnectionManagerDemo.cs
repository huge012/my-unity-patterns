using UnityEngine;
using UnityEngine.UI;
using UnityPatterns.MainThreadDispatcher;
namespace UnityPatterns.Demo
{
    /// <summary>
    /// TcpConnectionManager 사용 예시.
    ///
    /// OnReceived는 백그라운드 스레드에서 발동되므로,
    /// MainThreadQueue를 통해 Unity UI 업데이트를 메인스레드로 넘긴다.
    /// 이것이 실제 프로젝트에서 두 패턴이 함께 쓰이는 전형적인 조합이다.
    /// </summary>
    public class TcpConnectionManagerDemo : MonoBehaviour
    {
        [SerializeField] private TcpConnectionManager _connectionManager;
        [SerializeField] private MainThreadQueue      _mainThread;
        [SerializeField] private Text _statusText;
        [SerializeField] private Text _logText;

        private void Awake()
        {
            _connectionManager.OnConnected    += () => _mainThread.Enqueue(() =>
            {
                _statusText.text = "Connected";
                Log("서버에 연결됐습니다.");
            });

            _connectionManager.OnDisconnected += () => _mainThread.Enqueue(() =>
            {
                _statusText.text = "Disconnected";
                Log("연결이 끊겼습니다.");
            });

            _connectionManager.OnRetrying     += count => _mainThread.Enqueue(() =>
                Log($"재시도 중... {count}/3"));

            _connectionManager.OnGaveUp       += () => _mainThread.Enqueue(() =>
            {
                _statusText.text = "Failed";
                Log("연결에 실패했습니다. 서버를 확인하세요.");
            });

            // 수신 데이터는 PacketReceivePipeline으로 넘기는 것이 일반적
            _connectionManager.OnReceived += bytes => _mainThread.Enqueue(() =>
                Log($"수신: {bytes.Length} bytes"));
        }

        public void OnClickConnect()  => _connectionManager.Connect("192.168.0.1", 5588);
        public void OnClickDisconnect() => _connectionManager.Disconnect();

        private void Log(string msg)
        {
            _logText.text = $"[{System.DateTime.Now:HH:mm:ss}] {msg}\n" + _logText.text;
        }
    }
}
