# my-unity-patterns

B2B 납품 프로젝트(ARGlass Launcher V2 / AXFactory / ARGlass OPT / NavyECS)에서
반복 적용한 핵심 패턴 모음. 원본 코드는 납품 제품이라 공개 불가, 이 프로젝트는 패턴의 구조와 설계 의도만 담음.

---

## 패턴 목록

| 패턴 | 폴더 | 출처 프로젝트 | 핵심 문제 |
|------|------|--------------|-----------|
| [PacketDispatcher](#1-packetdispatcher) | `PacketDispatcher/` | NavyECS · OPT · AXFactory | 수신·파싱·로직 3계층 분리, 멀티캐스트 구독 |
| [MainThreadDispatcher](#2-mainthreaddispatcher) | `MainThreadDispatcher/` | NavyECS · AXFactory · Launcher V2 | 백그라운드 스레드 → Unity 메인스레드 안전 전달 |
| [ZeroDestroyTexturePool](#3-zerodestroytexturepool) | `ZeroDestroyTexturePool/` | ARGlass Launcher V2 | 드라이버 핸들 소진으로 인한 카메라 크래시 방지 |
| [WatchdogCoroutine](#4-watchdogcoroutine) | `WatchdogCoroutine/` | ARGlass Launcher V2 | OTA 다운로드 무한 정지 자동 감지 및 복구 |
| [HierarchicalScenario](#5-hierarchicalscenario) | `HierarchicalScenario/` | AXFactory | 챕터→퀘스트 계층 구조, 콜백 체인 |
| [TcpStreamDeserializer](#6-tcpstreamdeserializer) | `TcpStreamDeserializer/` | AXFactory | TCP 부분수신 대응, Big Endian 파싱 |
| [TcpConnectionManager](#7-tcpconnectionmanager) | `TcpConnectionManager/` | AXFactory | 연결 실패 시 자동 재시도 (최대 N회, 간격 설정) |
| [PacketReceivePipeline](#8-packetreceivepipeline) | `PacketReceivePipeline/` | AXFactory + NavyECS + Launcher V2 | 수신~캐싱~디스패치 전체 파이프라인 |

> **패턴 조합:** `TcpConnectionManager` → `PacketReceivePipeline` → `PacketDispatcher` 순으로 연결하면
> TCP 연결부터 게임 로직 콜백까지 전체 파이프라인 완성 (#8 참고)

---

## 1. PacketDispatcher

**문제:** 수신 계층과 게임 로직이 섞이면 패킷 종류가 늘수록 코드가 비대해지고,
여러 컴포넌트가 같은 패킷을 독립 구독하기 어려워짐.

**해결:** `Dictionary<TPacketId, Action<byte[]>>` 멀티캐스트 구조로 패킷 ID별 콜백을 독립 등록/해제.
마지막 페이로드를 캐싱해 늦게 구독한 컴포넌트도 즉시 조회 가능.

```csharp
var dispatcher = new PacketDispatcher<MyPacketId>();
dispatcher.Register(MyPacketId.Login, OnLogin);
dispatcher.Register(MyPacketId.Chat,  OnChat);
dispatcher.Register(MyPacketId.Chat,  OnChatLogger); // 같은 ID에 멀티캐스트 등록

dispatcher.Dispatch(MyPacketId.Login, payload);      // 수신 계층에서 호출
var last = dispatcher.GetCached(MyPacketId.Chat);    // 늦게 합류한 컴포넌트의 상태 복원
```

---

## 2. MainThreadDispatcher

**문제:** 소켓 수신 콜백, STT Worker Thread 등 백그라운드 스레드에서 Unity UI API를 직접 호출하면 크래시 발생.

**두 가지 구현:**

| 방식 | 클래스 | 적합한 경우 |
|------|--------|------------|
| Queue | `MainThreadQueue` | 여러 이벤트 / 페이로드 전달, 순서 보장 필요 |
| Flag  | `MainThreadFlag`  | 단순 신호 전달 (GC 없음, 중복 처리 없음) |

```csharp
// Queue 방식 — 백그라운드 스레드에서 (AXFactory 방식)
_mainThreadQueue.Enqueue(() => UpdateUI(data));

// Flag 방식 — 백그라운드 스레드에서 (NavyECS 방식)
_networkNoticeFlag.SetFlag("네트워크가 연결되지 않았습니다.");
```

---

## 3. ZeroDestroyTexturePool

**문제:** 슬립/웨이크업 사이클에서 `new Texture2D()` + `Destroy()`가 반복되면 드라이버 핸들이 소진되면서 카메라 프리뷰가 멈추고 앱이 크래시됨.
*(ARGlass Launcher V2 — 슬립 후 카메라 프리뷰 크래시의 근본 원인)*

**해결:** 앱 시작 시 해상도별 Texture2D 2장 미리 할당(핑퐁).
런타임에서 `new`/`Destroy` 없이 교대로 씀. **런타임 GC = 0.**

```csharp
// 앱 시작 시 1회
_pool.PreAllocate(1280, 720);

// 매 프레임
Texture2D writeTarget = _pool.GetWriteTarget(1280, 720);
// ... 픽셀 쓰기 (unsafe 포인터 또는 SetPixels32) ...
writeTarget.Apply();
_pool.Flip(1280, 720);
rawImage.texture = _pool.GetReadTarget(1280, 720); // 렌더링은 읽기 버퍼
```

---

## 4. WatchdogCoroutine

**문제:** OTA 다운로드 중 네트워크가 단절되면 진행률이 멈추지만 코루틴은 계속 실행됨.
타임아웃이 없으면 사용자는 무한 대기 상태에 빠짐.

**해결:** 진행률 갱신마다 타이머 리셋.
N초 무변화 감지 시 콜백 자동 발동.
복구 후 `HandleProgress()` 재호출 시 Watchdog **자동 재기동**.

```csharp
_watchdog.StartWatchdog(timeoutSec: 60f, onTimeout: ShowNetworkErrorPopup);

// 다운로드 진행률 갱신마다
_watchdog.HandleProgress(currentProgress, onTimeout: ShowNetworkErrorPopup);

// 정상 완료 시
_watchdog.StopWatchdog();
```

---

## 5. HierarchicalScenario

**문제:** 순서가 정해진 단계를 앞/뒤 이동·리셋 포함해 관리해야 함.
단계가 늘어도 코드 수정 없이 확장 가능해야 함.

**계층 구조:**
```
ScenarioManager  (전체 진행 상태 관리)
  └─ ChapterBase (챕터 단위 묶음 — GetComponentsInChildren으로 자동 수집)
       └─ QuestBase (개별 단계 — abstract, override로 확장)
```

**콜백 체인 흐름:**
```
QuestBase.CompleteQuest()
  → ChapterBase.NextQuest()
      → (마지막 퀘스트라면) ScenarioManager.NextChapter()
           → (마지막 챕터라면) CompleteScenario()
```

각 계층은 '다음에 무엇을 할지' 모르고 콜백만 호출 — **느슨한 결합(Loose Coupling)**.

```csharp
// QuestBase 서브클래스
public class ConfirmQuest : QuestBase
{
    public override void QuestEnter() {
        base.QuestEnter();
        _button.onClick.AddListener(CompleteQuest); // 완료 시 체인 자동 진행
    }
}
```

---

## 6. TcpStreamDeserializer

**문제:** TCP는 스트림 기반이라 한 번의 콜백에 패킷이 잘려 오거나 여러 개가 붙어서 올 수 있음.
무시하면 파싱 오류 발생.

**해결:** 수신 바이트를 내부 버퍼에 누적, 헤더 파싱 후 전체 크기 확인.
완성된 패킷만 추출하고 버퍼에서 제거. 여러 패킷이 붙어 있으면 루프로 모두 처리.

```csharp
// 수신 콜백에서 (백그라운드 스레드)
_parser.Feed(receivedBytes);

// 메인스레드에서 — 완성된 패킷 추출
while (_parser.TryDequeue(out var packet))
    _dispatcher.Dispatch((MyPacketId)packet.MessageId, packet.Payload);
```

**Big Endian 직렬화** (서버 ICD 표준 준수):
```csharp
byte[] toSend = TcpPacketParser.Serialize(
    messageId: 0x1000, sourceId: 0x01, destId: 0x02, seqNum: 1,
    payload: Encoding.UTF8.GetBytes("Hello"));
```

---

## 7. TcpConnectionManager

**문제:** HMD가 서버보다 먼저 켜지거나 네트워크가 불안정한 현장 환경에서 재시도 로직이 없으면 매번 수동 재기동이 필요.

**해결:** 연결 실패 시 최대 N회, 설정 간격으로 자동 재시도.
연결 성공/실패/재시도/포기를 이벤트로 노출해 UI 레이어가 독립적으로 반응.

```csharp
_connectionManager.OnConnected    += () => UpdateUI("Connected");
_connectionManager.OnRetrying     += count => UpdateUI($"재시도 {count}/3...");
_connectionManager.OnGaveUp       += () => UpdateUI("연결 실패 — 서버를 확인하세요");

// 수신 데이터는 PacketReceivePipeline으로 직접 투입
_connectionManager.OnReceived += bytes => _pipeline.Feed(bytes);

_connectionManager.Connect("192.168.0.1", 5588);
```

---

## 8. PacketReceivePipeline ★

TCP 연결부터 게임 로직 콜백까지 전체 흐름을 캡슐화.

**내부 구성:**
```
PacketReceivePipeline
  ├─ TcpPacketParser      (버퍼 누적 + 패킷 파싱)
  ├─ MainThreadQueue      (백그라운드 → 메인스레드)
  └─ PacketDispatcher     (ID 기반 콜백 디스패치 + 캐싱)
```

**전체 파이프라인:**
```
[백그라운드 스레드]                    [메인스레드]
TcpConnectionManager                  Unity Update()
  OnReceived(bytes)                      MainThreadQueue 처리
    └─ Pipeline.Feed(bytes)               └─ PacketDispatcher.Dispatch()
         └─ TcpPacketParser.Feed()             └─ 등록된 핸들러 호출
         └─ TryDequeue() 루프                       └─ 게임 로직 실행
         └─ MainThreadQueue.Enqueue()
```

```csharp
// 연결 수신 → 파이프라인 투입 (1줄)
_connectionManager.OnReceived += bytes => _pipeline.Feed(bytes);

// 패킷별 핸들러 등록
_pipeline.Register(PacketId.ObjectInfo,  OnObjectDetected);
_pipeline.Register(PacketId.VoiceCommand, OnVoiceCommand);

// 늦게 합류한 컴포넌트의 상태 복원
var lastKnown = _pipeline.GetCached(PacketId.ObjectInfo);
if (lastKnown != null) OnObjectDetected(lastKnown);
```

---

## 패턴 간 의존 관계

```
TcpConnectionManager
  │  OnReceived(bytes)
  ▼
PacketReceivePipeline ──── TcpStreamDeserializer  (파싱)
  │                   ──── MainThreadQueue        (스레드 전달)
  │                   ──── PacketDispatcher       (디스패치 + 캐싱)
  ▼
게임 로직 핸들러
```

`WatchdogCoroutine` — OTA/다운로드 계층에서 독립적으로 사용  
`ZeroDestroyTexturePool` — 카메라 렌더링 계층에서 독립적으로 사용  
`HierarchicalScenario` — 시나리오 진행 계층에서 독립적으로 사용

---

## 테스트

Edit Mode 단위 테스트가 `Tests/` 폴더에 포함됨.
Unity Test Runner(Window > General > Test Runner) → **EditMode** 탭에서 실행.

| 테스트 파일 | 대상 | 테스트 케이스 수 |
|------------|------|----------------|
| `PacketDispatcherTests.cs` | `PacketDispatcher<T>` | 11개 |
| `TcpPacketParserTests.cs` | `TcpPacketParser` | 10개 |

**다른 패턴에 테스트가 없는 이유:**  
`MainThreadDispatcher`·`WatchdogCoroutine`·`HierarchicalScenario`·`TcpConnectionManager`는 `MonoBehaviour` 생명주기(Update, Coroutine, GetComponentsInChildren)에 의존하고, `ZeroDestroyTexturePool`은 GPU 텍스처 할당이 필요함. 하드웨어 없이 단독 실행이 불가한 패턴들이라 `Demo/` 폴더의 사용 예시로 대체.

---

## 환경

- Unity 2021 LTS 이상
- C# 9.0 이상
- 외부 패키지 의존 없음 (System.Net.Sockets, System.Collections.Generic 사용)
