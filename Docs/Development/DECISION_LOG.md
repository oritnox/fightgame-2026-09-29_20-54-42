<!-- /Docs/Development/DECISION_LOG.md -->
# 결정 기록 · P00

| ID | 결정 | 이유와 영향 |
|---|---|---|
| D00-01 | 빈 저장소에 최소 main 커밋 후 codex/p00-foundation 분리 | 기존 코드 없음 확인. force push/자동 병합 금지 |
| D00-02 | 저장소 아래 ResonanceProtocol을 Unity 프로젝트 루트로 사용 | 원본 파일 계약 경로 보존 |
| D00-03 | Unity 6.3 계열은 후보, 패치와 패키지 pin은 실환경에서 확정 | 현재 작업 환경에 Unity/Windows 실행 장치 없음 |
| D00-04 | F131/F139는 P00 독립 도구로 작성; 아직 BLOCKED | T00-02 선행 미충족. 준비 산출물이며 완료/게이트 우회가 아님 |
| D00-05 | Native BuildProfile은 Unity UI에서 생성 | 실제 플랫폼 데이터를 모른 채 ScriptableObject/GUID를 위조하지 않음 |
| D00-06 | P00 Python 정적 검사와 Unity 모듈·빌드 검증 분리 | 정적 CI가 녹색이어도 EXIT-P00 통과 아님 |
| D00-07 | six asmdef의 참조는 이름으로 기록 | 실제 .meta는 첫 Unity import에서 생성해 커밋; 빈 후속 assembly에 가짜 클래스를 넣지 않음 |
| D00-08 | 추가 도구는 Tools/p00.py와 시험만 | F001 등 게임 Core 구현은 P01에 유지. 기존 140개 계약 식별자 유지 |
| D00-09 | 전체 명세 대신 원본 해시·ID 색인만 기록 | 공개 저장소에 필요한 P00 기록만 반입. 공식 스펙 등록 없음 |

변경은 ID·이유·영향 파일·시험·저장/모션/음악 영향·복구 방법을 새 행으로 남긴다.

## 새 저장소 이식 결정 (앞선 D00-02/03의 현재 적용을 대체)

| ID | 결정 | 영향 |
|---|---|---|
| D00-10 | 사용자가 지정한 fightgame-2026-09-29_20-54-42를 현재 작업 저장소로 사용 | 이전 저장소와 PR은 변경하지 않음 |
| D00-11 | Unity 루트를 저장소 .로 변경 | Assets/_Game, Docs, Tools 경로 및 CLI 기본값 이식 |
| D00-12 | 실제 초기 파일의 6000.6.3f1을 정확히 pin | 6000.3으로 자동 다운그레이드 금지; 실행 호환은 미검증 |
| D00-13 | 초기 사용자 파일 전부 보존, 추가 파일만 제안 | SampleScene/URP/Input/Packages/ProjectSettings/.meta/Git 정책 유지 |
| D00-14 | Editor-only 검증과 Windows prebuild 검증 분리 | Mac에서 플랫폼 전환 없이 단위시험; 영수증 scope/schema 분리 |
| D00-15 | Cinemachine/Rigging 누락은 전체 준비 차단, Editor core 검사와 분리 | 기존 manifest/lock 미수정; 실제 UPM 설치/재검증 필요 |
| D00-16 | 명시적 준비 메뉴 추가, import 자동 실행 금지 | 전체 layer conflict 사전 검사, 빈 RP 슬롯 및 별도 씬만 생성 |
| D00-17 | 이전 P00_SESSION/PYTHON_TESTS 기록을 새 증거로 이식하지 않음 | 새 코드의 로컬 결과와 Unity 미실행을 구분 |

복구는 새 작업 브랜치의 추가 파일을 검토하고 되돌리는 방식이다. main 강제 갱신, 원본 초기 자산 삭제, 자동 병합은 하지 않는다.
