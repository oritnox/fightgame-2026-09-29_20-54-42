<!-- /Docs/Development/CONTRACT_RESOLUTION.md -->
# 계약 누락·배치 해결 (T00-06 준비)

원본 해시와 수량은 Docs/Progress/BASELINE_INDEX.json에서 추적한다. 타입 선언 자체는 해당 P01 이후 작업에서 구현한다.

| 타입/계약 | 실제 선언 소유 파일 | 의존 방향 |
|---|---|---|
| ActionDefinition, ActionInstance, HitCandidate, IWorldQuery 및 query 값 | Core/Combat/CombatContracts.cs | Core는 포트만 소유, WorldQueryAdapter가 구현 |
| MusicSegment, PhraseDefinition, BeatSlot, CommittedPhrasePlan | Core/Rhythm/RhythmContracts.cs | 오디오 adapter가 계획 소비, 선택하지 않음 |
| TempoSegment, ClockEpoch, TimingProfile, TransportState | Core/Timing/TimingContracts.cs | UnityClockAdapter → ClockBridge → Core |
| EntityId, ContentId, Float3, TimeRange | Core/Foundation/CommonTypes.cs | 값 타입; Unity 객체 금지 |
| SaveEnvelope, Header, 설정 DTO | Core/Persistence/SaveContracts.cs | 저장 adapter만 파일 접근 |
| ActorSnapshot, 이벤트 | CombatContracts / DomainEvents | 불변 결과 소비; 재귀 피해 정산 금지 |

정의 타입 간 참조는 RP.Core 안에 두며 서비스 순환 의존을 의미하지 않는다.
F131/F139의 초기 검사 DTO는 RP.Editor의 중첩 타입이다. 미래 게임 Core 계약을 앞당겨 가짜로 만들지 않는다.
큰 계약을 새 파일로 분리하면 별도 작업 ID와 파일 색인을 추가한다.
BUILD-01 원본 전체 시험과 P00-EMPTY-BUILD를 분리한다. PLAY-A는 B01이 없으면 NOT_RUN이다.
