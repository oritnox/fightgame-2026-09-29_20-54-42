<!-- /README.md -->
# fightgame · Resonance Protocol

현재 작업 저장소: `oritnox/fightgame-2026-09-29_20-54-42`.
**이 저장소의 루트가 Unity 프로젝트다. `ResonanceProtocol/` 하위 프로젝트를 다시 만들지 않는다.**

사용자가 올린 `ProjectSettings/ProjectVersion.txt`의 **6000.6.3f1**을 유지한다. 기존 설계의 6000.3 LTS 후보로 다운그레이드하지 않는다.
초기 씬, 입력 자산, URP 설정, manifest/lock, 메타데이터, Git 정책을 이 이식에서 변경하지 않는다.

## 이번 구현

- 기존 P00 소스 패킷 fd4f3a1을 루트의 `Assets/_Game`, `Docs`, `Tools`에 이식.
- `ProjectSetupValidator`: 정확한 기존 버전, 설정·패키지·조립·해시 검사. Editor 검사와 Windows 인수 검사 분리.
- `P00ProjectSetup`: 명시적으로 실행할 때만 빈 RP 레이어와 별도 P00 씬 준비. 기존 레이어 충돌 시 중단.
- `BuildCommand`: 실제 Unity가 만든 native Windows BuildProfile과 같은 소스의 시험 영수증이 있을 때만 빌드.
- `Tools/p00.py unity --tests-only`: Mac의 현재 플랫폼에서 Editor 검사와 EditMode 시험. Windows 빌드 성공으로 표시하지 않음.

## Unity에서 시작

1. `codex/p00-unity-integration` 브랜치를 체크아웃하고, 기존 프로젝트 폴더를 **6000.6.3f1**로 연다.
2. 가져오기와 컴파일이 끝나면 `Resonance > P00 > Prepare project (preserve existing assets)` 실행.
3. `Resonance > P00 > Validate editor (no Windows required)`로 결과를 확인.
4. Test Runner의 EditMode에서 `RP.Tests.EditMode`를 실행한다. 배치 시험/증거 절차는 `Docs/Development/BUILD_PLAYBOOK.md`를 따른다.

패키지 상태: 초기 manifest에는 URP 17.7.0, Input System 1.20.0, Test Framework 1.8.0이 기록돼 있다.
URP의 실제 lock 항목은 builtin 17.6.0이다. 에디터 결합 코어 패키지는 일반 registry pin과 분리 검사하고 차이를 경고에 남긴다. 실제 등록 버전은 Unity setup 보고서로 확인한다.
Cinemachine/Animation Rigging은 아직 없어 Windows 전체 준비 검사에 차단 항목으로 남긴다. 이 커밋에서 패키지 버전이나 lock을 꾸며 넣지 않는다.

**정적 CI/Python 시험 성공 != C# 컴파일 != Unity EditMode 성공 != Windows 실행 성공.**
실제 에디터/Windows 인수 전 `EXIT-P00=BLOCKED`를 유지한다. 현재 동작은 초기화·시험 도구이며 전투 게임은 아직 구현하지 않았다.
