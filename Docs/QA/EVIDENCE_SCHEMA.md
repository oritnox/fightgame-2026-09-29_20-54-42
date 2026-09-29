<!-- /Docs/QA/EVIDENCE_SCHEMA.md -->
# 증거 규격

자동 로그는 Artifacts/Evidence/<RunId>/ 아래에 보관한다. 원시 로그는 기본 git 제외이며 검토한 작은 결과만 Docs/Evidence/에 넣는다.
증거 필드: schemaVersion, runId, taskId/testId, scope, status, sourceHash, BuildId, editorVersion, packageLockHash, contentHash, OS, deviceProfileId, expected, actual, startedUtc/finishedUtc, logPaths, defects, reviewer.
모르는 값은 null 또는 명시적인 NOT_AVAILABLE로 둔다. 실제 환경을 추측하지 않는다.
상태: NOT_RUN, BLOCKED, FAIL, PASS. 문서/정적검사, Unity EditMode, 빈 Windows 빌드, 실제 Windows 시작·종료는 서로 다른 scope다.

DONE(t) = 구현 완료 AND 필수 선행 DONE AND 필수 시험 PASS AND 최신 sourceHash의 증거 존재 AND 차단 결함 0.
파일·계약 문서 검사 PASS는 게임 시험 PASS가 아니다. 실제 검토하지 않은 reviewer 이름을 넣지 않는다.
체크섬은 변경 검출이며 서명이나 보안 보증이 아니다. 인증 토큰·장치 serial·개인 사용자 경로·환경 전체 목록을 기록하지 않는다.

빌드 도구는 runId/sourceHash와 시험 XML hash가 일치하는 P00 EditMode 영수증만 받는다.
기존 runId의 빌드·증거 덮어쓰기는 거부한다. 소스 수정 후 이전 영수증 재사용은 거부한다.

현재 영수증 schemaVersion은 2다. Editor-only scope P00-EDITOR-TESTS와 Windows build용 P00-WINDOWS-PREBUILD를 분리한다. Windows 빌드는 후자의 최신 영수증과 세 모듈 시험 결과만 허용한다.
