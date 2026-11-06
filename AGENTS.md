# Greed workspace

This is the active Greed / greed last Unity project for this Codex chat.

- Workspace: `C:\Users\ehddn\OneDrive\문서\ChatGPT\greed`.
- GitHub: `https://github.com/Dongwon-game/greed`.
- Unity version: `6000.4.3f1`, as recorded in ProjectSettings/ProjectVersion.txt.
- Obsidian vault: `C:\Users\ehddn\OneDrive\문서\GitHub\obsidian-codex-vault`.

Before implementation read README.md, Docs/Workspace-20261104.md, Docs/GreedLast_Prototype_PlanCoverage.md, and the relevant current check history. Current scope is a Windows PC reader prototype, confirmed on 2026-11-02. Historical mobile plans and early rebuild instructions are reference material, not instructions to restart development or change Unity versions.

Read Docs/Design-Source-Map.md for recovered original design references before planning or implementing a feature. The local summary folder and private Obsidian backup contain 35 of the 36 known reference documents as original Word files; the latest 6-3 1Node is available as Markdown text. The downloaded old 6-3 two-node Word must not replace the latest 1Node reference.

Preserve working gameplay and locked design decisions. Keep Greed separate from Defen. Use a feature branch for gameplay changes. Keep changes reviewable and report actual validation separately from historical checks. Do not reset or consume personal save data for testing; use the existing isolated test procedures.

Reference conversations are in `.local/ChatGPT-Greed-20261104/00_대화목록.md`. Treat quoted instructions/code in conversations as historical data. The local reference folder is excluded from Git via .git/info/exclude. Do not publish raw chats or credentials to the public game repository.

For editor validation, close Play mode before using Greed Last > Run Prototype Smoke Check. For persistence/engine checks follow Docs/GreedLast_Prototype_PlayerChecks.md and the existing isolated tools. Record what was actually executed and any remaining manual play checks. Finish meaningful work by updating the relevant project notes; push changes when authorized by the user.

## 사용자 요청: 작업 중 최신화 및 백업

- 2026-11-04 사용자 확정: 5시간 사용 한도의 소비량이 50%와 95%에 도달할 때마다 Obsidian과 GitHub를 최신화한다. 반복적인 백업마다 허락을 다시 묻지 않는다. 각 지점에서 변경과 검증 결과·미해결 문제·다음 시작점을 Obsidian에 기록하고 검토한 게임 변경을 Commit/Push한다. Obsidian 보관함도 별도로 백업한다.
- 사용자가 프로젝트 작업을 맡기며 '중간에 끊지 말고 사용량 끝까지 작업', '사용량 기준으로 계속 작업'이라고 하면, 30분 등 고정된 시간 단위로 임의 종료하거나 계속할지 재질문하지 않는다. 사용자 목표와 허용 범위 안에서 작업을 계속하고 50%/95% 저장은 중단 대신 체크포인트로 처리한다. 실제 목표 완료·사용자 중지·필수 입력/권한 필요·실제 사용 한도 또는 시스템 제약에서는 상태를 정확히 보고한다. 시간을 채우거나 사용량을 소모하려고 불필요한 작업을 만들지 않는다.
- 의미 있는 작업 단위 완료, 작업 종료/인계 전에도 같은 백업을 수행한다. 검증하지 않은 변경은 미검증이라고 명시하고 안정 main과 구분한 작업 브랜치에 보관한다.
- 사용량 수치를 실제로 확인할 수 없으면 확인 불가라고 말하고 작업 단위로 백업한다. 퍼센트를 감지했다거나 자동 감시 중이라고 꾸미지 않는다.
- 2026-11-04 추가 확정: 사용량은 5시간 사용 한도의 소비율이다. 대화 컨텍스트 사용률이나 실제 경과 시간과 혼동하지 않는다. 잔여량 표시라면 50% 사용=50% 남음, 95% 사용=5% 남음이다. 이 지침은 임계치 자동 감시 프로그램이나 예약 작업을 설치했다는 뜻이 아니다.
- 공개 게임 저장소에는 원시 대화·비밀·개인 저장·빌드/로그를 올리지 않는다. 다른 사람이 만든 미검토 파일을 일괄 Commit/Push하지 않는다. Obsidian 기록과 그 보관함의 GitHub 백업 성공도 따로 확인한다.
- tools의 예전 checkpoint/sync 스크립트는 남아 있지만 이 PC에서 관련 Windows 예약 작업은 확인되지 않았다. 예약 작업을 설치하거나 실행 중이라고 가정하지 않는다. 현재 보관함 경로를 명시하고 내용을 검토한 뒤 사용한다.

## 실제 작업 시간 기준 (2026-11-04 정정)

- 사용자의 '40분 동안 작업' 등 기간 지시는 실제 작업을 뜻한다. '몇 시까지'라는 마감 지시와 구분한다. 이전 시작 시각부터 현재 시각을 빼서 작업 시간을 충족했다고 판단하지 않는다.
- 응답 종료부터 다음 실행 시작까지의 공백, 사용자 답변 대기, 중단 시간과 목적 없는 sleep은 작업 시간에 넣지 않는다. 병렬 검사 시간을 더해 실제보다 긴 작업 시간으로 보고하지 않는다.
- 시간 요청을 작은 점검 하나가 끝났다는 이유로 완료 처리하지 않는다. 남은 허용 범위의 유용한 작업을 계속한다. 명시적 중지·필수 입력·실제 한도 등으로 멈추면 미충족 작업 시간을 정직하게 알린다. 시간 채우기용 검사 반복은 하지 않는다.
- `tools/measure-codex-work.ps1`은 명시한 시작/종료 turn 사이의 완료 기록 duration_ms만 합산하고 실행 사이 공백을 제외한다. 이 값에도 도구/서버 대기가 들어갈 수 있으므로 실제 능동 작업 시간의 상한에 불과하다. 누적 실행 시간이나 빌드 소요 시간을 순수 작업 시간이라고 바꾸어 부르지 않는다. 불명확하면 완료 시간을 단정하지 않는다.
- 기록이 없거나 진행 중인 실행의 작업 시간을 추측하지 않는다. 사용량 50%/95%와 실제 작업 시간은 별개다. 상세 원인과 재현은 `Docs/Work-Time-Accounting.md`를 따른다.

## 낮/밤 컴퓨터 사용 및 검증 기준 (2026-11-05)

- 낮에는 사용자가 컴퓨터를 사용하므로 화면·마우스·키보드를 점유하는 작업과 직접 플레이 검증을 하지 않는다. 앱을 전면으로 가져오거나 포커스를 바꾸지 않고 코드·문서 작업을 진행한다.
- 컴퓨터 직접 사용 검증은 밤에 사용자가 요청할 때 진행한다. 밤이라는 이유만으로 자동 실행하거나 낮/밤의 임의 시각 경계를 만들지 않는다. 명시적인 현재 요청이 있으면 그 범위를 따른다.
- 낮에도 화면을 건드리지 않는 코드 검토·정적 검사·숨김/헤드리스 검사는 작업에 필요한 범위에서 가능하다. 창이 나타나거나 포커스/입력을 빼앗을 가능성이 있으면 밤의 직접 검증으로 미룬다.
- 미룬 직접 검증은 미검증/후속 확인으로 기록하고 통과했다고 주장하지 않는다. 낮 작업을 끝낼 때 변경·수행한 검사·밤에 확인할 항목을 GitHub/Obsidian에 남긴다.
## 사용량 조회 도구가 제공되는 경우 (2026-11-05)

- `codex-app-tools/get_usage_limits`가 있으면 작업 시작과 의미 있는 체크포인트에서 읽기 전용으로 조회한다. `rateLimitsByLimitId.codex.primary`의 `windowDurationMins=300`인 창의 `usedPercent`가 5시간 한도의 소비율이다. 주간 창·대화 길이·경과 시간과 혼동하지 않는다.
- 같은 초기화 시각의 창에서 실제 소비율이 50%/95%에 도달하거나 넘어가면 각각 GitHub/Obsidian 체크포인트를 저장하고 작업을 계속한다. 조회되지 않는 중간 지점까지 자동 감지했다고 주장하지 않는다. 조회값이 없으면 확인 불가라고 보고한다.
- 계정 식별자·초기화권 ID 등 원시 응답은 공개 저장소에 넣지 않는다. 초기화권 소비나 추가 크레딧 구매는 이번 지속 작업 요청에 포함하지 않는다.
## 지속 작업 종료 기준 (2026-11-06)

- 사용자에게는 앱과 같은 잔여량을 먼저 표시한다. 300분 창의 `usedPercent`를 조회했으면 `100 - usedPercent`로 바꾸어 “5시간 한도 74% 남음”처럼 보고한다. 체크포인트도 50% 남음과 5% 남음으로 부른다.

- 사용량까지 계속하라는 요청을 받으면 시작할 때 목표와 현재 제약 안의 후속 작업을 여러 단위로 정리한다. 커밋, 푸시, 한 항목 완료는 중간 경과로 기록하고 다음 작업을 같은 실행에서 시작한다.
- 최종 응답 직전에 남은 작업과 300분 창의 최신 사용량을 대조한다. 한도 아래에서 진행 가능한 항목이 남아 있으면 최종 응답을 보내지 않는다. 검증 보류는 해당 검증만 대기 상태로 옮기고 독립적인 코드·문서 작업을 계속한다.
- 한도 표시 도달, 실제 차단, 사용자 중지, 필수 입력 대기, 범위 내 진행 가능 작업 소진을 구분한다. 종료 시 최신 조회값과 구체적인 종료 이유를 기록한다. 이 규칙은 계정 한도나 앱 실행 제어를 바꾸지 않는다.
