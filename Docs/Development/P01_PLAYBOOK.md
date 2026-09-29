<!-- /Docs/Development/P01_PLAYBOOK.md -->
# P01-A 적용·실행

## 기존 프로젝트에 적용

Unity Editor를 닫고 기존 로컬 변경을 먼저 확인한다. URP17.6.0과 사용자가 만든 씬/설정/.meta를 보존한다. 이 묶음은 기존 프로젝트 전체가 아니라 추가·변경 파일 패킷이다. Tools/p00.py와 Packages/ProjectSettings는 포함하지 않는다.

동봉된 Git 패치는 확인한 원격 eadfcf0의 AGENTS 및 이식용 CI를 기준으로 한다. 적용 전에 `git apply --check <패치의 실제 경로>`로 충돌을 검사한다. 충돌이 있으면 강제 적용/원본 삭제하지 않는다. 이 검사를 통과하면 `git apply <패치의 실제 경로>`로 적용한다. ZIP과 패치 중 하나만 적용한다. ZIP 직접 복사는 기존 AGENTS/CI를 덮어쓸 수 있으므로 패치 방식을 권한다.

P01-A 구현은 이 저장소의 `codex/p00-unity-integration` 브랜치에 반영한다. 별도 `codex/p01-timing-input` 브랜치는 자동 갱신하지 않는다. force push는 필요하지 않다.

## 실제 Unity 시험

기존 6000.6.3f1 프로젝트를 열고 새 소스와 asmdef를 import한다. `.meta`는 Unity가 생성한다. Console의 컴파일 오류가 있으면 이후 통과로 기록하지 않는다.

Test Runner → EditMode에 `RP.Tests.Core.EditMode`가 보이는지 확인한다. 기존 P00 `RP.Tests.EditMode`와 별도 assembly다. 이 시험은 이미 새 기본 소스가 함께 컴파일된 상태에서 수행한다. 실제 게임 장치의 소리/접촉 정렬 시험은 아니다.

배치 증거를 남기려면 에디터를 닫은 뒤 저장소 루트에서 실행한다.

`python3 Tools/p01.py audit`

`python3 Tools/p01.py unity --editor /Applications/Unity/Hub/Editor/6000.6.3f1/Unity.app/Contents/MacOS/Unity --run-id p01-editor-001`

위 경로는 사용자가 제공한 에디터 경로다. Windows 플랫폼으로 전환하지 않는다. 다음 결과가 생긴다.

- `Artifacts/Evidence/p01-editor-001/setup.json`
- `Artifacts/Evidence/p01-editor-001/core-editmode.xml`
- `Artifacts/Evidence/p01-editor-001/core-test-receipt.json`
- `Artifacts/Evidence/p01-editor-001/core-runner.json`
- validate.log, core-editmode.log

기존 run-id를 재사용하지 않는다. 0개/실패/skip/fixture 누락/소스 변경/변조된 setup은 실패다. Python 도구가 만든 PASS는 실제 호출한 Unity의 결과 파일 검사를 거친 값이다. 단위시험의 mock 결과를 실제 기록 폴더로 복사하면 안 된다.

## 개발 도구 자체 시험

`python3 -m unittest discover -s Tools/tests -p test_p01.py -v`

이 시험의 Unity subprocess는 mock이다. Python 22개 성공이 C# 시험 성공은 아니다.

## 선택적인 standalone C# 검증

.NET8 SDK가 설치된 별도 개발환경에서 `dotnet test Tools/CoreTests/Tests/RP.Core.Tests.csproj -c Release`로 같은 소스와 같은 NUnit 시험을 실행할 수 있다. 라이브러리는 netstandard2.1, 시험 호스트만 net8.0이다. 패키지는 검증 도구용 csproj에만 있으며 Unity manifest/lock을 바꾸지 않는다.

이 실행은 이 패킷 작성 환경에서 수행되지 않았다. CI workflow도 파일만 작성되었고 원격 실행하지 못했다. 실제 실행될 때 SDK와 restore된 패키지 버전·결과를 별도로 보관한다.

## 다음 패킷

P01-A 실제 컴파일/시험이 확인되면 공통 ID 발급·이벤트 큐·진단 계약과 UnityClock/Input adapter를 연결한다. 현재 패킷만으로 게임 화면이나 캐릭터 움직임이 생기지 않는다. Windows 인수와 전체 EXIT-P01은 계속 분리해서 추적한다.
