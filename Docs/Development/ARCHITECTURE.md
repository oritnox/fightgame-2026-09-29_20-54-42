<!-- /Docs/Development/ARCHITECTURE.md -->
# 조립 경계와 기본 단위

| 조립 | 허용 의존 |
|---|---|
| RP.Core | .NET 표준 타입만; noEngineReferences=true |
| RP.Data | RP.Core + UnityEngine |
| RP.UnityRuntime | RP.Core + RP.Data + 실제 필요한 설치 패키지 |
| RP.Editor | Editor 플랫폼; P00은 엔진 에디터 API만, 후속 Core/Data 참조는 필요 시 추가 |
| RP.Tests.EditMode | Editor 전용; 현재 RP.Editor와 TestAssemblies |
| RP.Tests.PlayMode | RP.UnityRuntime, TestAssemblies; 제품 기본 미포함 |

시간 double seconds, 음악 위치 long tick(PPQ=960), 거리 meters, 각도 degrees.
콘텐츠 ID는 3~80자의 ASCII 영문·숫자·밑줄·점·하이픈; 대소문자만 다른 ID 금지.
런타임 ID는 0을 비어있음으로 쓰는 64bit 값; 콘텐츠 ID와 Unity GUID를 혼용하지 않는다.
폴더의 namespace는 RP.Core.*, RP.Data, RP.UnityRuntime.*, RP.Editor, RP.Tests.*다.

레이어 고정안: 8 RP_World, 9 RP_Player, 10 RP_Enemy, 11 RP_Hitbox, 12 RP_Hurtbox, 13 RP_Interactable.
P00 검사기는 충돌을 알리고 기존 레이어를 덮어쓰지 않는다. 충돌 행렬은 P02에서 실제 판정과 함께 확정한다.
P00에 비어 있는 Core/Data/Runtime 폴더를 채우기 위한 가짜 클래스는 추가하지 않는다.
