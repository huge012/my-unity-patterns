using System;
using System.Collections;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace UnityPatterns
{
    /// <summary>
    /// TCP 연결 시도 / 재시도 / 해제를 관리하는 컴포넌트.
    ///
    /// 출처: AXFactory — TCPPacketReceiver.cs
    ///   InitTCPClient() / AttemptRetry() / ConnectToServer() 구조를 직접 추상화.
    ///
    /// 설계 배경:
    ///   현장 환경에서 HMD가 NPU 서버에 먼저 접속해야 하는 구조였음.
    ///   전원 켤 때 서버가 아직 준비되지 않은 경우, 연결 실패 시 최대 3회까지
    ///   1초 간격으로 자동 재시도, 실패하면 포기하는 로직이 필요했음.
    ///
    /// 핵심 결정:
    ///   - 재시도는 Unity Coroutine(Invoke)으로 메인스레드에서 처리 — Thread.Sleep 없음
    ///   - 연결 성공/실패/데이터 수신을 이벤트로 노출 — 상위 레이어가 구독
    ///   - 연결 상태를 명시적 enum으로 관리 — bool IsConnected 대신
    ///   - 수신은 백그라운드 스레드, Unity API 호출은 반드시 onReceive 콜백을
    ///     MainThreadQueue를 통해 전달할 것 (이 클래스는 수신만 담당)
    /// </summary>
    public class TcpConnectionManager : MonoBehaviour
    {
        // ── 설정 ────────────────────────────────────────────────────
        [Header("서버 접속 정보")]
        [SerializeField] private string _host = "192.168.0.1";
        [SerializeField] private int    _port = 5588;

        [Header("재시도 설정")]
        [SerializeField] private int   _maxRetries      = 3;
        [SerializeField] private float _retryInterval   = 1.0f;
        [SerializeField] private int   _connectTimeoutMs = 3000; // 연결 시도 1회 최대 대기 ms

        // ── 이벤트 ──────────────────────────────────────────────────
        public event Action          OnConnected;
        public event Action          OnDisconnected;
        public event Action<int>     OnRetrying;    // 현재 시도 횟수
        public event Action          OnGaveUp;      // 최대 재시도 초과
        public event Action<byte[]>  OnReceived;    // 수신 데이터 (백그라운드 스레드에서 발동)

        // ── 상태 ────────────────────────────────────────────────────
        public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

        private TcpClient             _client;
        private NetworkStream         _stream;
        private Thread                _receiveThread;
        private CancellationTokenSource _cts;
        private int                   _retryCount;

        // ── 외부 인터페이스 ─────────────────────────────────────────

        /// <summary>연결 시작. 이미 연결된 상태면 무시.</summary>
        public void Connect(string host = null, int port = 0)
        {
            if (State == ConnectionState.Connected ||
                State == ConnectionState.Connecting) return;

            if (!string.IsNullOrEmpty(host)) _host = host;
            if (port > 0) _port = port;

            _retryCount = 0;
            StartCoroutine(ConnectCoroutine());
        }

        /// <summary>연결 종료.</summary>
        public void Disconnect()
        {
            StopAllCoroutines();
            CloseConnection();
            State = ConnectionState.Disconnected;
            OnDisconnected?.Invoke();
        }

        /// <summary>연결된 상태에서 데이터 송신.</summary>
        public void Send(byte[] data)
        {
            if (State != ConnectionState.Connected || _stream == null) return;
            try { _stream.Write(data, 0, data.Length); }
            catch (Exception e) { Debug.LogWarning($"[TcpConnectionManager] Send failed: {e.Message}"); }
        }

        // ── 연결 코루틴 ─────────────────────────────────────────────

        private IEnumerator ConnectCoroutine()
        {
            State = ConnectionState.Connecting;

            while (_retryCount <= _maxRetries)
            {
                bool success = false;
                bool done    = false;

                // 비동기 연결 시도 (Thread로 메인스레드 블로킹 방지)
                // _connectTimeoutMs 이내에 연결되지 않으면 실패 처리.
                int timeoutMs = _connectTimeoutMs;
                new Thread(() =>
                {
                    try
                    {
                        var client = new TcpClient();
                        var task = client.ConnectAsync(_host, _port);
                        if (!task.Wait(timeoutMs))
                            throw new TimeoutException($"연결 타임아웃 ({timeoutMs}ms)");
                        _client  = client;
                        _stream  = client.GetStream();
                        success  = true;
                    }
                    catch { success = false; }
                    finally { done = true; }
                }).Start();

                yield return new WaitUntil(() => done);

                if (success)
                {
                    State = ConnectionState.Connected;
                    OnConnected?.Invoke();
                    StartReceiveThread();
                    yield break;
                }

                // 실패 처리
                _retryCount++;
                if (_retryCount > _maxRetries)
                {
                    State = ConnectionState.Failed;
                    OnGaveUp?.Invoke();
                    Debug.LogError($"[TcpConnectionManager] 최대 재시도({_maxRetries}회) 초과. 연결 포기.");
                    yield break;
                }

                OnRetrying?.Invoke(_retryCount);
                Debug.Log($"[TcpConnectionManager] {_retryCount}/{_maxRetries} 재시도 중...");
                yield return new WaitForSeconds(_retryInterval);
            }
        }

        // ── 수신 스레드 ─────────────────────────────────────────────

        private void StartReceiveThread()
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _receiveThread = new Thread(() => ReceiveLoop(token)) { IsBackground = true };
            _receiveThread.Start();
        }

        private void ReceiveLoop(CancellationToken ct)
        {
            byte[] buffer = new byte[4096];
            try
            {
                while (!ct.IsCancellationRequested && _stream != null && _stream.CanRead)
                {
                    int bytesRead = _stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break; // 서버가 연결을 끊음

                    byte[] received = new byte[bytesRead];
                    Array.Copy(buffer, received, bytesRead);

                    // 수신 이벤트는 백그라운드 스레드에서 발동됨.
                    // 구독자는 반드시 MainThreadQueue를 통해 Unity API를 호출할 것.
                    OnReceived?.Invoke(received);
                }
            }
            catch (Exception e)
            {
                if (!ct.IsCancellationRequested && State == ConnectionState.Connected)
                    Debug.LogWarning($"[TcpConnectionManager] 수신 중단: {e.Message}");
            }
            finally
            {
                // 수신 루프 종료 = 연결 끊김 (취소 요청이 아닌 경우만 이벤트 발동)
                if (!ct.IsCancellationRequested && State == ConnectionState.Connected)
                {
                    State = ConnectionState.Disconnected;
                    // OnDisconnected는 메인스레드에서 호출해야 하므로 MainThreadQueue 사용 권장
                    // 여기서는 단순화를 위해 직접 호출 (Demo 수준)
                    OnDisconnected?.Invoke();
                }
            }
        }

        // ── 정리 ────────────────────────────────────────────────────

        private void CloseConnection()
        {
            _cts?.Cancel();
            try { _stream?.Close(); } catch { /* ignored */ }
            try { _client?.Close(); } catch { /* ignored */ }
            _stream = null;
            _client = null;
            _receiveThread?.Join(1000); // 최대 1초 대기 후 포기
            _receiveThread = null;
            _cts?.Dispose();
            _cts = null;
        }

        private void OnDestroy() => CloseConnection();
    }

    public enum ConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Failed,
    }
}
