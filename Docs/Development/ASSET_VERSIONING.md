<!-- /Docs/Development/ASSET_VERSIONING.md -->
# 자산과 버전 관리

소스·asmdef·씬/프리팹·실제 .meta·ProjectSettings·manifest/lock은 추적한다.
Library/Temp/빌드/개인 저장/라이선스/로그는 제외한다. 새 복사본 시험에서 참조 누락을 확인한다.
모델·오디오·텍스처는 Git LFS 대상으로 선언했으나 LFS 설치/용량/원격 다운로드 성공은 아직 NOT_RUN이다.
바이너리 최초 반입 전에 LFS 사용 가능 여부를 확인한다. 유료 용량 구매는 자동 수행하지 않는다.
씬과 프리팹은 Force Text로 저장하고 담당자 한 명이 편집한다. 잠금 기능이 없어도 작업 ID로 소유를 기록한다.

원본 제작물은 SourceAssets/, 런타임 자산은 Assets/_Game/Art·Audio/, 베이크는 Assets/_Game/Content/Motion/Generated/에 둔다.
베이크에는 source GUID/hash를 저장하고 수정 시 재생성한다. 원본을 자동 삭제하지 않는다.
사용자가 올린 초기 자산의 .meta는 이미 존재하며 보존한다. 새 P00 소스·폴더의 .meta만 Unity import 후 생성된 값을 검토·커밋해야 한다.
복구는 이전 정상 커밋의 새 브랜치에서 수행한다. 현재 원본을 삭제하거나 강제 reset으로 사용자 변경을 잃지 않는다.

새 저장소에서는 사용자의 루트 .gitignore/.gitattributes를 그대로 유지한다. 이전 프로젝트의 Git 정책 파일은 덮어쓰지 않는다. Artifacts와 Tools 캐시는 해당 하위 .gitignore로 제외한다.

이번 이식 CI는 초기 커밋 대비 기존 파일 수정/삭제를 금지한다. 이후 Unity 준비로 의도한 TagManager/EditorSettings 변경을 반영할 때는 새 변경 작업에서 보호 검사 범위를 검토해 조정한다. 검사를 몰래 제거하거나 예상한 설정 변경을 임의 실패 예외로 숨기지 않는다.
