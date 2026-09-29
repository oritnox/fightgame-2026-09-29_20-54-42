<!-- /Docs/Development/P01_EXECUTION_RULES.md -->
# P01-A 실행 계약 · 시간·입력 기반

## D01-01 · 개발 착수의 제한적 예외

사용자의 URP17.6.0 변경과 p00-editor-006의 Editor PASS 보고, 계속 진행 요청을 근거로 플랫폼 독립적인 P01-A 작업을 작성한다. 원본 EXIT-P00은 BLOCKED로 보존한다. Windows 빌드·실행을 생략한 채 P00 완료로 바꾸지 않는다. 기존 AGENTS의 일괄 P01 금지에 대한 범위 제한 예외이며 제품 인수 기준을 낮추는 변경이 아니다.

사용자 제공 결과는 USER_REPORTED다. 원본 XML·영수증·로컬 프로젝트 전체는 읽지 않았으며 원격 tree가 사용자 sourceHash와 동일하다고 가정하지 않는다. 증거 파일에는 개인 절대 경로를 넣지 않는다.

원격 기준은 eadfcf0e55120e8e139bd3737d7f496cb3eae715. Packages/manifest.json의 URP17.6.0을 확인했으며 패키지·기존 씬·ProjectSettings·P00 C#/Tools/p00.py·기존 .meta는 덮어쓰지 않았다. P01-A 소스와 현재 Unity 실행 증거는 사용자의 요청에 따라 `codex/p00-unity-integration` 브랜치에 반영한다. `codex/p01-timing-input`은 별도 브랜치로 유지한다.

## 범위

F001 CommonTypes, F006 Validation, F007 TimingContracts, F008 ClockBridge, F009 TempoMap, F010 CombatClock, F011 CalibrationEstimator, F012 TimingRecoveryPolicy, F013 InputContracts, F014 CommandBuffer, F015 InputArbitrator.

각 파일은 이번 범위의 정상·경계·실패 처리가 있는 소스이며 스켈레톤을 넣지 않는다. 컴파일/시험이 미실행인 것을 DONE으로 바꾸지 않는다. F002~F005, Combat/Rhythm/AI/Boss/Progression/Persistence의 상위 계약, UnityClock/Input 어댑터, 실제 오디오 실험, 진단 이벤트는 이후 패킷이다. P01 전체는 미완료다.

C01~C26은 기존 WORKING_RULES를 그대로 적용한다. 새로운 시간/입력 정책:

- 시각은 double seconds, 곡 좌표는 PPQ960 long tick, 물리량은 meters/degrees. ContentId는 3~80 ASCII 문자이며 소문자로 정규화해 대소문자 중복을 차단한다. Unity GUID/파일 경로와 별개다.
- ClockBridge는 최대128개 샘플 기본값, 8개 이상·span1초 이상·slope편차2000ppm 이하·RMS5ms 이하에서 Ready다. 최근 데이터 범위 양끝0.5초 밖으로 외삽하지 않는다. 숫자 시계 범위는 0~1e12초다.
- 정지 샘플은 fit에 넣지 않는다. DSP 역행/불연속은 새 epoch, 관측된 DSP 정지는 일단 suspended로 처리한다. 실제 adapter가 새 epoch로 재시작해야 한다.
- 20ms 또는6RMS 초과 이상치는 제외하고 3연속이면 새 epoch다. 250ms 초과 불연속은 즉시 새 epoch다. 이는 초기 계약값이며 실측 없이 정확도를 보장하지 않는다.
- TempoMap은 불변 복사와 정렬 검사를 한다. 지원 horizon은 1,000,000,000,000tick이며 혼합 BPM의 double 정밀도를 고려한 상한이다. 템포 경계는 새 구간, 박자표 변경은 이전 박자표의 마디 경계만 허용한다. SecondsToTick은 최근접, 정확한 절반은 away-from-zero다. 공격 예약 자체는 integer tick→seconds로 계산한다.
- CombatClock.Current는 처리 목표 시각이다. AdvanceTarget은 bounded step 계획을 반환하고 실제 피해/이동 확정 시각은 이후 CombatSimulation이 별도로 소유한다. 메뉴 시간을 target에 누적하지 않는다. 100ms 초과 catch-up은 복구 요구로 반환하고 시각을 전진시키지 않는다.
- 입력 sequence는 모든 장치를 합친 수신 순서로 중앙에서 단조 발급한다. timestamp는 순서가 뒤집혀도 원본을 보존할 수 있지만 Push 호출은 sequence 발급 순서다. 이후 작은 sequence 재전달은 중복으로 거부한다.
- 입력 큐128, 공격 예약1, 예약 최대120ms. 예약 시각은 원래 MappedMechanicTime 기준이다. 실행 시각으로 판정 timestamp를 바꾸지 않는다. 슬롯/게이지 소비는 하지 않는다.
- 실제 적용 시각은 max(mappedMechanicTime, committedCombatTime). LateDelivery와 원래 판정 시각을 보존하고 과거 HP를 소급 복구하지 않는다.
- 동률은 가장 이른 시각+1ms에 속하는 입력끼리 비교한다. 회피>패링>강>약, 같은 종류는 sequence순이다. 그 이후 입력은 DeferredCount로 남기며 현재 동률 그룹과 함께 버리면 안 된다. 이동·메뉴·release는 별도 입력 경로에서 소비한다.
- 부동소수 경계 오차 흡수는 1e-12초 수준이다. 이것을 사용자 보정이나 넓은 판정 창으로 해석하지 않는다.
- default 값, NaN/Infinity, 잘못된 enum·scope·범위는 거부하거나 명시적 미준비 결과로 반환한다. 검증 실패로 자원을 차감하지 않는다.

## 수식

`t_judged = MapRealtime(r_event) - dspSongOrigin - inputOffset`.

`t_apply = max(mappedMechanicTime, committedCombatTime)`.

`t_tick = segmentStartSeconds + (tick-startTick)*60/(960*BPM)`.

보정 오차 중앙값 `m`, `MAD=median(|e-m|)`, `sigmaRobust=1.4826*MAD`, 이상치 경계 `max(0.010,3*sigmaRobust)`초. 워밍업8개 제외, 최소8개 유효표본. 고분산은 자동적용 불가. 하드웨어 지연과 사용자 편향을 분리 측정했다고 주장하지 않는다.

## 시험과 전달

9개 fixture, 선언 기준 80개 case의 동일 C# 시험을 별도 `RP.Tests.Core.EditMode`에서 실행한다. 원본 직접시험 위치를 `Assets/_Game/Tests/EditMode/Core/`로 좁혀 독립 asmdef로 격리했으며 기존 P00 시험 asmdef는 수정하지 않는다.

standalone 프로젝트는 동일 Core 소스를 netstandard2.1로 컴파일하고 동일 NUnit 소스를 net8.0에서 실행한다. Unity 런타임에 .NET8이나 NuGet을 추가하지 않는다. 이 프로젝트는 개발 검증용이며 실제 Unity 호환 검증의 대체가 아니다.

새 .meta는 실제 Unity가 생성한다. 입력action/장면을 새로 만들거나 기존 캐릭터에 자동 부착하지 않는다. 이 패킷은 데이터와 계산 기반이며 아직 화면에 전투가 나타나지는 않는다.

소스 변경 시 Tools/p01-source-manifest.json의 해당 해시와 시험을 같이 갱신한다. 이 원장은 전달 무결성 검사이며 보안 서명/불변 명세 등록이 아니다.

## CI 보호 갱신

최초 커밋 이후 모든 파일 수정 금지였던 이식용 CI를 사용자 최신 커밋 eadfcf0 기준 Packages/ProjectSettings/초기씬/초기설정/Input/Git정책 보호로 좁힌다. 사용자가 이미 검증한 URP17.6.0과 native profile 변경을 오류로 되돌리지 않는다. P01 소스·문서의 정상 추가/수정만 허용한다. 원본을 보호하는 검사를 삭제하지 않는다.

## 기술 근거

- Unity .NET profile: https://docs.unity3d.com/6000.0/Documentation/Manual/dotnet-profile-support.html
- NUnit .NET Standard와 실행 시험 프로젝트 구분: https://docs.nunit.org/articles/nunit/getting-started/dotnet-core-and-dotnet-standard.html
- NUnit adapter: https://docs.nunit.org/articles/vs-test-adapter/Index.html

문서 검토와 시험 작성은 실제 C# 컴파일/장치 시험 통과가 아니다.
