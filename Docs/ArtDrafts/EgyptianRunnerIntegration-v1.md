# 이집트풍 인간 주자 적용 기록

## 방향과 상태

2026-10-29 승인한 방향은 그림체만 유지하고 인물은 고대 이집트에서 볼 법한 인간 주자로 바꾸는 것이다. 자연스러운 갈색 피부, 검은 땋은 머리, 흰 리넨풍 의상, 청록 허리띠, 금/청금석 장식과 샌들을 사용한다. 특정 왕조의 고증 복원은 아니다. 뿔/초록 피부/동양풍 복식은 사용하지 않는다.

- 소스 적용: 기본 달리기와 점프/숙이기/비틀기, 입구 배경 인물을 같은 방향으로 연결했다.
- 현재 실행본: 2026-10-29 **19:48 / egyptian-runner-checks**, 게임·Editor/빌드 입력 `bfcc183`이다. 승인한 그림 5종과 상시 연결 검사를 포함한 전용·두 방식 엔진·전체 스모크/Windows/ZIP/초기 구동을 통과했다. 이전 19:23 실행본/ZIP은 보존했으며 최신 식별과 직접 볼 네 단계는 [빠른 확인](../GreedLast_Prototype_QuickCheck.md)을 따른다.
- 기존 이미지를 덮어쓰거나 이전 프로젝트 코드를 가져오지 않았다. 기존 PNG는 이력으로 보존하며 새 캐릭터 경로를 로드한다.
- 주행/판정/보상/저장 1회/실제 시작 2회 규칙은 변경하지 않았다.

## Unity 자원

모두 `Assets/Resources/GreedLast/`에 있다. 내장 image_gen으로 생성/편집했고 CLI/API 우회나 픽셀 후처리를 하지 않았다. 원본과 복사본의 SHA-256을 확인했다. 이미지의 투명 알파는 그대로 유지했다.

| 자원 | 용도 | SHA-256 |
| --- | --- | --- |
| `PrototypeEgyptianRunner.png` | 기본 달리기 | `AD5FCE7E932F6E18573B4F5B9B6A0CCD1006873FE3CD1F5C49C52069892E527C` |
| `PrototypeEgyptianRunnerJump.png` | 점프 | `F880307050CF945B774767D87827D833458A71D1861EB06D1E7E668DB2E8A76C` |
| `PrototypeEgyptianRunnerSlide.png` | 숙이기/슬라이드 | `5E7DB4B051320AAD554EB214D1CC56B656C95D9FF446EBD757DA8B769842D5A9` |
| `PrototypeEgyptianRunnerTwist.png` | 비틀기/옆 회피 | `211F39E1BEFF2162748684CC2C659AC7622535DE4CB3242742DC5FA412FD54B1` |
| `PrototypeEgyptianEntrance.png` | 입구의 작은 주자 교체 | `16538F7609DA3A4DD766DC21FE4C7FA9627943252861D2945A7C8419183D55E2` |

동작 시안의 그림이 정확한 1/3 경계를 넘으므로 시트를 단순 분할하지 않았다. 네 포즈는 각각 1024x1536 원본이며, Unity 최대 1024/밉맵 없음/CPU 읽기 없음/Clamp로 가져온다. 138x207 고정 캔버스와 포즈별 발 피벗으로 크기 변동과 바닥 미끄러짐을 줄였다. 기존 이동 궤적은 유지하고 그림에만 과도한 절차적 찌그러짐을 상쇄한다. 숙이기는 기존 하강 오프셋을 상쇄하며, 비틀기만 기존 회피 방향에 맞춰 반전한다.

## 생성 요청 사양

아래는 개별 생성 요청의 재현용 요약이다. 도구에 전달한 프롬프트 원문을 그대로 전사한 것은 아니다. 최초 승인 시안의 원문은 [시안 v1](EgyptianRunnerActions-v1.md)에 보존했다.

공통: polished painterly game illustration with clear ink outlines; the same adult human Egyptian female runner, warm brown skin, black tied braids, ivory linen running outfit, modest shorts, teal sash, restrained gold/lapis ornaments, leather sandals. Rear/rear-three-quarter camera facing down an unseen corridor. Full body with transparent margins; no horns, green skin, East Asian costume, text, watermark, floor, cast shadow or effects. Single 1024x1536 transparent canvas, matching anatomical scale across poses.

1. 달리기: 승인 시안과 같은 인물/복식의 후면 달리기, 발과 머리카락까지 온전히 포함한 독립 이미지. `transparent_background=true`.
2. 점프: 같은 인물의 무릎을 접은 공중 점프, 캔버스를 채우도록 확대하지 않고 기본 포즈와 신체 비례 유지. `transparent_background=true`.
3. 숙이기: 같은 인물의 몸을 충분히 낮춘 회피, 위쪽은 투명 여백으로 남기고 인물을 확대하지 않음. `transparent_background=true`.
4. 비틀기: 같은 인물의 옆으로 몸을 틀어 피하는 자세, 손/발/허리띠가 모두 프레임 안에 있도록 함. `transparent_background=true`.
5. 입구 편집: 기존 입구 그림의 작은 인물만 위 인간 주자로 교체. 통로 원근/횃불/문/구도/조명은 유지하고 텍스트나 새로운 화면 요소를 넣지 않음. `transparent_background=false`.

## 검증 범위와 직접 확인

### 최종 상시 검사 포함 배포

`bfcc183`의 상시 검사까지 포함해 19:48 배포를 완료했다. 원문/전체 해시는 [확인 이력](../GreedLast_Prototype_CheckHistory.md)의 같은 시점 항목을 따른다. 최종 배포 보고서는 `Logs/PlayerReleaseChecks/15f49353eeec40e6abebd1a8773d8f7e/report.json`/SHA256 `0AB4ABD4CAE34348849A22D5E6364D5DC031B340DEBEC4E2B07D80E3C6DA34FC`이며 DLL은 `03AFEE2F927118B3AF52846B57FFD0799087C5E42516DE6542177100E300E97F`다. 아래 19:23 및 처음 실패/수정한 도우미 기록은 그때의 이력이며 현재 미완료가 아니다.

### 10월 29일 19시 23분 배포 완료

- 보고서 `Logs/PlayerReleaseChecks/6291eff4596947c78aabeaa946c53b32/report.json`, SHA256 `AF35DBBA5AF3360595F3EACA57C85D1CCC869371D355DE30651EF558825DCC65`.
- 전체 스모크·138입력의 Git 정규 blob 일치·비개발 Windows·Editor/시험 타입 제외·154파일 ZIP 전체 대조·20초 초기 구동(예외 0)을 통과했다. 새 자원 경로/포함 이름 5개와 실제 선택/적용 호출을 DLL/Resources에서 읽기만 해 확인했다.
- DLL SHA256 `DDE4F33CE5678E43370480CE37B0ED7EB9EB2E59BC42E5A029B540C7012EC2FD`. ZIP `Builds/GreedLastPrototype-verified-20261029-egyptian-runner.zip`, 66,442,089바이트, SHA256 `E83E537A0161BDAC5E09042093E14CD86E0A023643387C5F2A0F23E0A42B76BF`. 원문 UTC `2026-10-29T10:23:30.1461749Z`.
- 보충 도우미는 공개 코어 입력의 13결과·7정지·6실제 선택 게이트와 로비 복귀, 선택 없는 무한의 정확/Good 비틀기 그림을 확인했다. Edit 모드 UI 표본이며 그림 응답 시계만 중간으로 위치시켰다. 실제 엔진 프레임/OS 입력·화면 촬영으로 세지 않는다. 이 도우미를 상시 검사로 옮긴 `bfcc183`의 별도 전용 검사도 통과했고 두 방식 엔진은 진행 중이다.
- 이전 13:52 실행본 전체는 위 보고서 폴더의 `previous-release`에 보존했고 이전 ZIP은 불변이다. 개인/원본/시험 사본 설정은 보존했으며 빌드·초기 구동 소유 프로세스는 종료했다.

### 10월 29일 소스 검증 완료

- 게임/자원 `bfc0741`, 엔진 입력 `91fd361`은 문서 변경만 다르고 `Assets/Packages/ProjectSettings`는 같다.
- 전용 비공개 검사 `Logs/EngineLoopChecks/85a7c6e873da42e29f59bd9497908b52/report.json`, SHA256 `0C7CCEA02C9C7711BDCE757AE2A5D9F1A9EBC93C42D282480725EAB3C586A3AF`: 4종 알파/가져오기·480정책·36변환·누락 3종/로비 복귀 통과.
- 전체 엔진 `Logs/EngineLoopChecks/cbeeb79edaac4ee897f89b3769b401a5/report.json`, SHA256 `FCC1F268E19C03746A877EA68F1032C827156364F4A06984ECB39E74FACA43BE`. 원시 결과 SHA256 `ECF3B58B8CBB997359F3D5E780E8944771D8BDE406E944ED9B9E19ADAF479368`.
- 두 재로딩 방식 각각 479.39/479.77초·28,297/28,310프레임, 일반 15판정/8선택·한 번 저장·정비 취소 무소모·실제 무한 두 번/자동 비움과 대기 휠 취소/새 휠 복구를 통과했다. 원본 개인 저장/프로젝트 설정은 불변이고 소유 엔진 프로세스는 종료했다. 실제 사용자 입력/새 화면 촬영이나 실기기 확인은 아니다.
- 추가 배포 도우미의 첫 시도 `Logs/PlayerReleaseChecks/b600ff6f05f34f629132292d96d0a778`는 장 마감 뒤 선택 화면에서도 비틀기 그림을 기대한 검사 준비 오류로 중단됐다. 선택 화면에서 기본 포즈로 돌아온 동작은 정상이다. 실패 원본 도우미/로그/보고서를 보존했으며 게임 소스나 사용자 실행본을 고쳐서 통과시키지 않았다. 후속 검사는 실제 선택 게이트와 선택 없는 무한 비틀기를 구분한다.

`GreedLastPrototypeEgyptianArtCheck`는 원본 알파/가져오기 설정/포즈 정책 경계/고정 크기/비율 상쇄/발 피벗/좌우 반전/누락 자원의 기본 포즈 복귀/로비 복귀를 검사한다. 원본 알파는 임시 텍스처로 읽고 즉시 정리하며 실행 자원을 CPU 읽기 가능으로 바꾸지 않는다. 개인 저장/설정/실제 화면 조작은 하지 않는다.

자동 검사는 그림의 역사 고증이나 실기기 가독성/실제 센서/소리 지연/FPS를 보증하지 않는다. 배포 완료 뒤 직접 볼 순서는 첫 화면 인물 -> 로비 -> 연습의 점프 -> 숙이기 -> 좌우 회피 -> 정지/재개 -> 결과/로비 복귀다. 카메라/판정선/함정과 인물이 겹쳐 행동을 읽기 어려운지는 사람의 플레이 확인이 남는다.
