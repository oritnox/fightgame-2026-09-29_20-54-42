<!-- /Docs/QA/GATE_MATRIX.md -->
# 게이트 규칙

| ID | 요구 | 현재 |
|---|---|---|
| P00-STATIC | 저장소 정책·Python 직접 시험 | 실제 실행 보고서 참조 |
| UNIT-F131 / UNIT-F139 / P00ProjectSetupTests | Unity EditMode 모듈 시험 | NOT_RUN |
| P00-EMPTY-BUILD | 실제 패치/lock/.meta/레이어/프로필 + 빈 Windows 빌드 | BLOCKED |
| P00-WINDOWS-SMOKE | 해당 실행물을 Windows에서 시작·정상 종료; 로그/hash 기록 | BLOCKED |
| EXIT-P00 | T00-01~09,F131,F139 전부 DONE + 위 증거 최신 | BLOCKED |
| BUILD-01 | 전투 포함 fresh checkout 제품 빌드 | P09 이후; P00로 대체 불가 |
| PLAY-A | B01을 기본 격투로 공략 | P06 이후 |
| G1 | P04 선택 공방 + P05 자원 + P06 PLAY-A | P04만으로 완료 불가 |
| G2/G3 | 원본 100개 시험·6개 플레이·최종 자산/플랫폼 | NOT_RUN |

P00 전용 도구는 empty 프로필만 지원한다. training/product는 구현 전 요청하면 거부한다.
사람의 수동 Windows smoke 없이 자동 빌드가 EXIT-P00을 DONE으로 바꾸지 않는다.
