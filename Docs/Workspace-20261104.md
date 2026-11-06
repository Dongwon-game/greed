# Codex Greed 작업 기준 — 2026-11-04

## 현재 작업 위치

`C:\Users\ehddn\OneDrive\문서\ChatGPT\greed`

이 Codex 프로젝트와 채팅에서 사용하는 실제 Unity 프로젝트다. 빈 저장소 대신 GitHub Greed의 main 이력과 게임 소스를 복원했고 origin을 `https://github.com/Dongwon-game/greed.git`로 설정했다.

Unity Hub와 GitHub Desktop에서도 이 폴더를 등록해서 사용한다. Unity Hub: 프로젝트 → 추가 → 디스크에서 추가. GitHub Desktop: File → Add local repository. Unity 버전은 6000.4.3f1이다. 시작 장면은 `Assets/Scenes/GreedLast_Boot.unity`다.

이전 복구 사본 `C:\Users\ehddn\Documents\Codex\2026-11-04\so-x20\outputs\greed`는 보존했으나 이후 개발 기준은 현재 폴더다. 같은 이름의 두 폴더에서 번갈아 수정하지 않는다.

## 자료

- 게임 소스/자원/설정: Assets, Packages, ProjectSettings
- 현재 구현 범위와 검증 이력: 이 프로젝트의 Docs
- 옵시디언: `C:\Users\ehddn\OneDrive\문서\GitHub\obsidian-codex-vault`
- ChatGPT 그리드 대화 73개: `.local/ChatGPT-Greed-20261104/00_대화목록.md` (로컬 전용)

게임 저장소는 11월 2일, 복구된 옵시디언의 과거 메모는 5월 20일 기준이다. 최신 사용자 결정은 Windows PC 체험 프로토타입이다. 5월의 재제작 지침이나 옛 모바일 목표를 최신 지시로 적용하지 않는다. 새 컴파일/직접 플레이 검증은 과거 검사 결과와 구분해 기록한다.

## 작업 재개 상태

Unity Hub와 GitHub Desktop에는 현재 폴더를 등록했다. 복원 기준과 앞선 PC 입력 표시 수정은 별도 저장 공간의 검사/빌드로 확인했고 [확인 이력](GreedLast_Prototype_CheckHistory.md)에 구분했다. 초기 컴파일 확인만으로 이후 수정까지 검증됐다고 보지 않는다.

11월 4일 PC 첫 안내/회전 키/진동 메뉴 정리의 게임 입력은 `53c3c31`이다. 이후 검토 후보와 직접 검증은 [11월 5일 확인 기록](Native-Review-20261105.md), 검증을 기다리는 소스 변경은 [11월 6일 PC 안내 후속](PC-Guidance-20261106.md)을 따른다. `Builds/PCReview-current.json`은 검증된 Windows 배포본의 실행 파일과 ZIP을 식별한다.

작업은 기획서의 PC 체험 범위를 유지한다. 실제 화면의 키보드/마우스 확인과 자동 엔진 결과, 사람이 느끼는 소리/손맛은 구분해 기록한다. 변경을 검토한 뒤 작업 브랜치와 Obsidian 보관함을 각각 Commit/Push하며 중간 백업 뒤에도 요청한 작업을 계속한다.
소스 복구 기준: `21b4722a269f1601811db759241e728f6b29af47`. 최초 위치 복구 정리는 프로젝트 위치/연결/문서에 관한 작업이었다. 이후 게임 표시 수정과 실제 검사는 확인 이력의 2026-11-04 후속 항목에 구분했다.

## 이번 위치 변경 확인 결과

- 현재 경로에서 Unity 6000.4.3f1 batchmode 임포트/컴파일 완료, 종료 코드 0 문구 확인. 기존 API의 CS0618 경고는 있으며 컴파일 오류는 없다.
- 로그: `.local/workspace-compile.log` (로컬 전용).
- Unity Hub에 현재 경로를 추가하고 즐겨찾기로 표시했다. 이전 경로의 항목/사본은 보존했다. 앞으로 즐겨찾기한 현재 경로를 연다.
- GitHub Desktop에 현재 경로의 greed 저장소를 등록했다.
- 옵시디언의 현재 경로와 관련 스크립트를 새 위치로 바꾸고 비공개 보관함 저장소에 백업했다.
- 이 절은 최초 위치 변경 당시의 컴파일 확인 기록이다. 이후 현재 입력의 전체 스모크/엔진/Windows 빌드와 실제 창/OS 입력 확인은 `Docs/GreedLast_Prototype_CheckHistory.md`의 PC 첫 안내·설정 항목을 따른다.
