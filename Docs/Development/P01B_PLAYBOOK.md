<!-- /Docs/Development/P01B_PLAYBOOK.md -->
# P01-B · Unity input/DSP adapters

선행 증거: `p01-editor-002`의 P01-CORE-EDITOR 80/80 PASS. Windows 검증은 별도이며 NOT_RUN 유지.

## 목표

- `UnityClockAdapter`: `Time.realtimeSinceStartupAsDouble`과 `AudioSettings.dspTime`을 기존 `ClockBridge`/`CombatClock`에 공급한다.
- `UnityInputAdapter`: Input System callback의 `context.time`을 바꾸지 않고 Core `InputCommand`로 변환한다.
- `LocalTraceRecorder`: 외부 전송 없는 bounded ring buffer만 제공한다.
- `TestFixtureBuilder`: 메모리 안에서만 InputAction fixture를 생성한다. 프로젝트 액션 자산을 수정하지 않는다.

Unity의 DSP time은 오디오 샘플 처리에 기반하며 pause/suspend 때 진행하지 않는다. 출력 장치 변경은 `AudioSettings.OnAudioConfigurationChanged`로 복구 요청을 만든다. Input System event time은 realtimeSinceStartup과 같은 선형 시간선을 사용한다. 실제 장치 지연이나 음악 동기 품질을 이 어댑터 단위시험으로 통과 처리하지 않는다.

## 실행

1. `codex/p01-unity-adapters` 브랜치를 체크아웃한다.
2. Unity 6000.6.3f1로 열어 새 파일과 .meta import/compile이 끝나는지 확인한다.
3. Unity를 닫는다.
4. 아래 실행:

`python3 Tools/p01b.py unity --editor /Applications/Unity/Hub/Editor/6000.6.3f1/Unity.app/Contents/MacOS/Unity --run-id p01b-editor-001`

예상 inventory는 UnityClockAdapter 7, UnityInputAdapter 6, LocalTraceRecorder 5, TestFixtureBuilder 2 = 총 20개다.
0건/skip/실패/중복 test name/시험 중 source 변경은 실패다.

## 다음 단계

P01-B PASS 후에는 실제 격투용 InputAction schema와 바인딩을 별도 데이터로 정의하고, `TimingCoordinator`/GameLoop가 adapter 결과를 소비하도록 연결한다.
현재 기본 `Assets/InputSystem_Actions.inputactions`는 수정하지 않는다.

## 아직 PASS로 보지 않는 항목

- 실제 입력장치 이벤트 timestamp 분포와 지연
- 실제 출력 장치의 DSP/청감 정렬
- Windows 빌드와 Windows smoke
- P01 전체 종료
