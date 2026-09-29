<!-- /Docs/Development/BUILD_PLAYBOOK.md -->
# P00 · 기존 Unity 프로젝트에서 계속하기

루트는 저장소 `.`. 새 프로젝트 생성이나 `adopt`는 필요 없다. 사용자가 올린 **6000.6.3f1**과 초기 자산을 유지한다.

## 1. 에디터 준비

`codex/p00-unity-integration` 브랜치를 체크아웃한다. Unity Hub에서 기존 폴더를 6000.6.3f1로 연다.
Import 중 새 C#/asmdef 및 폴더의 `.meta`는 Unity가 생성한다. 직접 GUID를 넣지 않는다.
컴파일 오류가 있으면 Console의 첫 오류부터 해결하고 이후 단계의 성공을 기록하지 않는다.

씬 편집을 명시적으로 저장/폐기한 뒤 `Resonance > P00 > Prepare project (preserve existing assets)`를 실행한다.
전체 레이어 충돌을 먼저 검사한 후 빈 8~13에 RP 레이어를 배정하고 ForceText를 설정한다.
별도 `Assets/_Game/Scenes/P00_EMPTY.unity`를 만든다. 기존 SampleScene, 패키지, 버전, 전역 빌드 씬 목록은 바꾸지 않는다.
이미 같은 설정이 있으면 재사용하고 기존 RP 씬도 덮어쓰지 않는다.

생성된 `.meta`와 의도한 준비 변경을 Git diff로 검토한다. 메뉴 실행이 P00 인수를 의미하지 않는다.
`Resonance > P00 > Validate editor (no Windows required)`는 실제 Editor 설정만 검사한다.

## 2. Mac 또는 Windows에서 Editor 시험만 실행

Python 3.9 이상에서 저장소 루트 기준으로 실행한다. Python은 Unity 게임 런타임 의존성이 아니라 개발 도구다.
먼저 Unity Editor를 닫는다. 실행 중인 에디터의 lock 파일은 삭제하지 않는다.

`python3 -m unittest discover -s Tools/tests -v`

`python3 Tools/p00.py audit`

M1 Mac의 기본 Hub 설치 경로를 사용하는 경우:

`python3 Tools/p00.py unity --tests-only --editor /Applications/Unity/Hub/Editor/6000.6.3f1/Unity.app/Contents/MacOS/Unity --run-id p00-editor-001`

실제 설치 경로가 다르면 해당 실행 파일 경로를 사용한다. 경로에 공백이 있으면 인용한다.
이 경로는 실행 예시이며 사용자 장치에서 설치를 확인한 결과가 아니다.

Editor-only 실행은 플랫폼을 Windows로 바꾸지 않으며 native Windows BuildProfile도 요구하지 않는다.
RP.Tests.EditMode의 세 모듈(ProjectSetupValidatorTests, BuildCommandTests, P00ProjectSetupTests)을 실행한다.
0건/실패/skip/누락 fixture/변경된 소스는 실패다. 원래 두 모듈만 들어 있는 과거 XML은 재사용하지 않는다.
`Artifacts/Evidence/<run-id>/`에 setup.json, editmode.xml, test-receipt.json, runner.json 및 로그를 남긴다.
run-id를 재사용해 증거를 덮어쓰지 않는다. GUI Test Runner 결과는 별도 수동 증거이며 배치 영수증으로 위조하지 않는다.

## 3. Windows 빈 빌드 준비

이 단계는 Editor-only 시험과 별개다. 실제 Windows 빌드 모듈 및 실행 장치가 필요하다.
현재 manifest에는 Cinemachine과 Animation Rigging이 없다. Package Manager에서 현재 에디터가 제공하는 코어 패키지를 추가하고 등록 버전을 확인한다.
manifest/lock 변경과 의존성 변경을 함께 검토하며 문서에 없는 패키지를 임의로 추가하지 않는다.
기존 URP/Input/Test 버전을 다운그레이드하지 않는다. 필요 없는 초기 패키지도 이 이식에서 자동 삭제하지 않는다.

Unity Build Profiles UI에서 Windows x64 native profile을 만들고 `Assets/Settings/Build Profiles/Windows.asset` 위치에 저장한다. 빌드 출력은 프로젝트 루트의 `Artifacts/Builds/P00/<run-id>/`에 생성하며 `Assets` 안에 출력하지 않는다.
해당 프로필을 활성화하고 Override Global Scene List를 사용하며 P00_EMPTY 씬 하나만 활성화한다.
프로필 전용 scripting defines는 비워둔다. 이 설정은 기존 전역 SampleScene 목록을 덮어쓰는 것이 아니다.

Editor를 닫은 뒤 `python3 Tools/p00.py unity --editor <실제-Unity-실행파일> --run-id p00-win-001` 실행.
검사 → 같은 소스의 EditMode → schemaVersion 2, scope P00-WINDOWS-PREBUILD 영수증 → 빈 Windows 빌드 순이다.
P00-EDITOR-TESTS 영수증을 복사해 Windows 빌드를 승인할 수 없다.

## 4. 실제 실행과 종료

생성된 실행물을 실제 Windows 장치에서 열어 화면 표시·시작·정상 종료를 확인한다.
OS/CPU/GPU/RAM/디스플레이/실행물 hash/로그/관찰 결과를 기록한다. 개인 절대 경로와 라이선스는 공개하지 않는다.
빈 실행물에는 전투가 없다. 이 성공은 원본 BUILD-01의 전투 포함 인수나 게임 완성이 아니다.
모든 P00 증거 검토 후에만 P00_STATUS와 종료 게이트를 갱신한다.

## 실패 처리

누락 패키지·다른 버전·사용 중 레이어는 오류로 보고하고 기존 자산을 보존한다.
엔진 실행 파일이 없으면 BLOCKED다. package lock/source가 시험 중 변경되면 생성 파일을 검토하고 새 run-id로 다시 실행한다.
P00 전체 인수를 막는 Cinemachine/Rigging 부재는 정적 코드 결함과 구분한다. Editor 시험에는 현재 core 패키지 3개를 사용한다.

## 참고한 공식 API

- https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Build.Profile.BuildProfile.html
- https://docs.unity3d.com/6000.0/Documentation/ScriptReference/BuildPlayerWithProfileOptions.html
- https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AssetDatabase.SaveAssetIfDirty.html

API 문서 검토는 6000.6.3f1에서의 실제 컴파일/실행 검증이 아니다.

## 에디터 결합 패키지 검사 보완

초기 URP manifest 값 17.7.0과 lock의 builtin 17.6.0이 서로 다르다. Unity 6000.6 공식 문서에서 URP 등 코어 패키지는 에디터에 묶인 버전으로 배포된다. 따라서 `source=builtin`인 명시된 코어 패키지의 차이는 일반 registry mismatch와 구분해 경고로 보고한다. 원본 파일을 맞춰 쓴 것이 아니며 해당 조합의 실행 성공을 뜻하지 않는다.
누락 lock·잘못된 버전 형식·registry 불일치·허용하지 않은 builtin 출처는 여전히 실패다. 실제 Unity 보고서의 registeredPackages(name/version/source)와 설치 상태를 확인하고, 시험 중 lock이 달라졌다면 검토 후 다시 시험한다.
근거: https://docs.unity.com/en-us/engine/6000.6/manual/packages-list/packages-all/pack-core

초기 CI checkout에서 TutorialInfo/Icons/URP.png의 LFS pointer 경고도 관측했다. 이 이식은 사용자 파일을 변환하지 않는다. 이후 T00-03에서 Git LFS 추적 정책과 기존 바이너리의 저장 방식을 확인하며 원본 자산을 자동 재작성하지 않는다.
