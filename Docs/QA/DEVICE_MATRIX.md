<!-- /Docs/QA/DEVICE_MATRIX.md -->
# 장치 행렬

| ID | 용도 | CPU/GPU/RAM/OS | 화면·입력·출력 | 상태 | 담당/증거 |
|---|---|---|---|---|---|
| STATIC-LINUX | 저장소·Python 검사만 | 실행 보고서에 최소 정보 | 실제 게임 장치 아님 | 사용 가능 | 구현 작업/Docs/Evidence |
| EDITOR-6000-6-3 | 실제 Unity 6000.6.3f1 import·컴파일·EditMode | 파일 pin만 확인, 장치 실행 미검증 | 실제 패치와 package lock 기록 | BLOCKED | 개발자/Artifacts/Evidence |
| WINDOWS-BASE | 빈 x64 빌드 시작·정상 종료 | 미확정 | 패드·키보드·유선 출력·1080p | BLOCKED | QA/Windows smoke |
| WINDOWS-MATRIX | 후속 게임 시험 | 미확정 | 30/60/120/144fps·초광폭·무선 출력 | NOT_RUN | P09 |

M1/GB10 보유 정보로 Windows 최소사양을 추정하지 않는다. Windows 빌드 생성과 Windows에서 실행은 별도 증거다.
실제 장치 인수 시 CPU/GPU/RAM/OS 버전·디스플레이 refresh·출력 방식·입력 장치·실행자를 채운다.
