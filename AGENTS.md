<!-- /AGENTS.md -->
# fightgame 작업 규칙

먼저 `Docs/Development/WORKING_RULES.md`, `Docs/Development/DECISION_LOG.md`, `Docs/Progress/P00_STATUS.json`을 읽는다.

- 현재 저장소는 `oritnox/fightgame-2026-09-29_20-54-42`, Unity 루트는 `.`이다. 이전 `ResonanceProtocol/` 경로를 중첩 생성하지 않는다.
- 실제 기존 버전 `6000.6.3f1`을 유지한다. 원본 설계의 6000.3 후보와 다른 것은 결정 기록으로 추적하며 자동 업·다운그레이드하지 않는다.
- 범위는 P00 기반 통합이다. EXIT-P00이 BLOCKED이면 P01 기능을 임의로 시작하지 않는다.
- 기존 파일 전체를 읽고 수정한다. 코드 첫 줄에 저장소 상대 경로 주석, 공용 변경은 두 번째 줄에 `공용코드 수정`과 영향을 쓴다. JSON/asmdef에 주석을 넣지 않는다.
- 사용자 초기 ProjectSettings/Packages/씬/에셋/.meta/Git 정책은 보존한다. Unity GUID·잠금 파일·씬 YAML을 추측해 생성하지 않는다.
- 준비 메뉴는 사용자가 명시적으로 실행할 때만 빈 레이어/별도 시험 씬을 만든다. import 시 자동 수정·설치·네트워크 호출은 금지한다.
- Core는 UnityEngine을 참조하지 않는다. 빈 후속 assembly를 채우기 위해 가짜 클래스를 만들지 않는다.
- 검증: `python3 -m unittest discover -s Tools/tests -v`, `python3 Tools/p00.py audit`. 실제 Unity 절차는 BUILD_PLAYBOOK을 따른다.
- Editor-only 영수증은 Windows prebuild 영수증이 아니다. 미실행 C#/Unity/Windows 검증은 NOT_RUN으로 둔다.
- 비밀키·개인 저장·전체 환경 출력·라이선스 원본·사용자 절대 경로를 커밋하지 않는다. 공식 명세 등록·전체 기획 공개·구매는 하지 않는다.
- force push/자동 병합/기존 파일 삭제는 금지한다. 변경 파일 전체, 시험 결과, 남은 차단 사항을 제공한다.
