<!-- /Docs/Development/P01B_PLAYBOOK.md -->
# P01-B · Unity 입력·DSP 어댑터 검토본

## 범위와 선행 증거

D01-02: 사용자의 `p01-editor-002` 결과(2026-09-29T15:54:42Z 시작, 코어 80/80 PASS)와 연속 구현 요청에 따라 P01-A 다음의 입력·시계·진단 어댑터를 진행한다. 이는 제한된 P01 개발 범위이며 EXIT-P00의 Windows 인수나 P01 전체 완료를 뜻하지 않는다.

사용자 결과의 sourceHash는 `23f9dea9c4a226c640d42c7e4f72720ad4ee2660f87695755397c6c5be663725`이다. 제공된 결과 요약을 USER_REPORTED로 기록한다. 원본 XML·영수증을 직접 읽거나 현재 브랜치와 동일한 전체 프로젝트라고 인증한 것은 아니다.

현재 작업 브랜치는 `codex/p01-unity-adapters`다. 이 브랜치에 이미 존재한 어댑터 구현을 검토하고 경계조건을 보완했다. 코어 11개 파일, 패키지, 초기 씬·설정·.meta, Tools/p00.py와 Tools/p01.py를 변경하지 않는다. 새 .meta는 실제 Unity에서 생성한다.

## 구성

- UnityClockAdapter: Unity의 realtime/DSP 값을 기존 ClockBridge/CombatClock에 전달한다. 복구가 필요한 상태는 요청만 발행하고 자동으로 전투를 재개하지 않는다.
- UnityInputAdapter: 기본 Button의 performed 시점에서 context.time을 그대로 기록한다. started는 아날로그 press threshold 이전일 수 있으므로 공격 누름으로 취급하지 않는다.
- LocalTraceRecorder: 외부 전송 없는 용량 제한 메모리 기록이다.
- TestFixtureBuilder: 시험용 InputActionAsset을 메모리에만 생성한다. 사용자의 기존 입력 자산은 수정하지 않는다.

## 검토에서 강화한 계약

1. Button과 Vector2 Value만 지원한다. action 또는 binding에 Hold/Tap/Press 등의 custom interaction이 있으면 Bind에서 거부한다. 해당 의미를 무시하거나 performed 시간을 과거의 시작 시간으로 꾸미지 않는다. 향후 충전·홀드 기술은 별도 계약·시험으로 추가한다.
2. Binding을 유지한 채 action type·interaction·binding override를 변경하지 않는다. 변경 시 호출자가 안전 지점에서 기존 adapter를 Dispose하고 설정 후 재결합한다. action 활성화/비활성화와 수명은 호출자가 소유한다.
3. 트리거가 임계값에 도달하지 않고 원점으로 돌아오면 누름과 해제를 모두 발생시키지 않는다. 실제 누름 뒤 해제는 한 번만 전달한다.
4. 동일 DSP 값을 짧게 반복해서 읽으면 Duplicate로 처리하며 Core의 정지 detector에 넣지 않는다. 100ms 이상 진전이 없을 때만 정체 복구를 요청한다. 이 값은 시험 시작값이며 모든 장치의 지연 보증이 아니다.
5. 복구 요청과 새 epoch 시작은 이전 song DSP origin을 무효화한다. 후속 transport/TimingCoordinator는 새 원점을 명시적으로 설정해야 한다. 새 epoch 이전의 늦은 입력을 새 epoch의 명령으로 바꾸지 않는다.
6. HasSongOrigin=false 또는 Quality가 미준비이면 강화 리듬 슬롯을 발행하지 않는 것은 후속 구절/게임 루프의 책임이다. 기본 입력 매핑의 JudgedSongTime=0을 성공 슬롯으로 취급하지 않는다.
7. 이 어댑터의 public API와 Core 연결은 메인 스레드 소유다. 스레드 안전한 전체 게임 스케줄러라고 주장하지 않는다.

## 실제 Unity 시험

로컬 변경(특히 생성된 .meta와 프로젝트 설정)을 먼저 검토·보존한다. 강제 checkout/reset/clean은 사용하지 않는다.
현재 브랜치와 실행환경은 그대로 두고 실제 작업 브랜치만 선택한다. Unity 실행 중에는 브랜치를 바꾸거나 배치 시험을 동시에 실행하지 않는다.

1. `codex/p01-unity-adapters`의 최신 변경을 받는다.
2. Unity 6000.6.3f1에서 import/compile을 완료하고 새 .meta를 생성한다.
3. Unity를 닫는다.
4. 기존 코어 회귀를 확인하려면 `python3 Tools/p01.py unity --editor /Applications/Unity/Hub/Editor/6000.6.3f1/Unity.app/Contents/MacOS/Unity --run-id p01-editor-003`을 사용한다.
5. 이번 어댑터 시험: `python3 Tools/p01b.py unity --editor /Applications/Unity/Hub/Editor/6000.6.3f1/Unity.app/Contents/MacOS/Unity --run-id p01b-editor-001`.

각 run ID는 한 번만 사용한다. `--tests-only`는 p01.py/p01b.py의 인자가 아니다. Windows로 전환하지 않는다.

시험 inventory: UnityClockAdapterTests 7, UnityInputAdapterTests 6, LocalTraceRecorderTests 5, TestFixtureBuilderTests 2, AdapterBoundaryTests 10 = 총 30개.
이전 20개 결과, 부분 실행, skip/실패/중복 시험 이름, 시험 중 소스 변경은 새 영수증을 만들 수 없다.
예상 scope는 P01-UNITY-ADAPTERS-EDITOR이며 adapter-runner.json, adapter-editmode.xml, adapter-test-receipt.json과 로그가 Artifacts/Evidence/<run-id>/에 생성된다.

## 완료 경계

30개는 아직 작성된 시험 수다. 어댑터 시험의 가상 입력 장치와 가짜 시계는 실제 사용자의 키보드·패드·스피커 측정이 아니다. 실제 Unity 컴파일·시험은 실행 결과가 있을 때만 PASS로 기록한다. 실제 입력 지연·음악 청감·Windows 빌드·Windows 시작/종료와 EXIT-P01은 별도다.

다음에는 실제 입력 이벤트·출력 장치의 시간 분포를 측정하고, 남은 공통 ID/이벤트 계약과 진단 연결을 완성한다. TimingCoordinator/GameLoop의 제품 통합은 원래 후속 단계와 연결해 진행하며 이번 패킷에 캐릭터 전투 화면을 포함했다고 하지 않는다.

## 참고 근거

Input System의 기본 Button started/performed 구분: https://docs.unity.cn/Packages/com.unity.inputsystem@1.12/manual/Interactions.html
DSP 시간의 샘플 기반 의미: https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AudioSettings-dspTime.html
참고 문서는 동작 의미의 근거이며, 현재 1.20/6000.6.3f1에서 실제 실행한 시험을 대신하지 않는다.
