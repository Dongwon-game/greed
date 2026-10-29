# 이집트풍 인간 주자 적용 기록

## 방향과 상태

2026-10-29 승인한 방향은 그림체만 유지하고 인물은 고대 이집트에서 볼 법한 인간 주자로 바꾸는 것이다. 자연스러운 갈색 피부, 검은 땋은 머리, 흰 리넨풍 의상, 청록 허리띠, 금/청금석 장식과 샌들을 사용한다. 특정 왕조의 고증 복원은 아니다. 뿔/초록 피부/동양풍 복식은 사용하지 않는다.

- 소스 적용: 기본 달리기와 점프/숙이기/비틀기, 입구 배경 인물을 같은 방향으로 연결했다.
- 현재 단계: 컴파일과 별도 검증 진행 중. 이 문서 작성 시 기존 13:52 `menu-wheel` 실행본은 그대로이며, 새 실행본 완료 여부는 [빠른 확인](../GreedLast_Prototype_QuickCheck.md)의 최신 식별을 따른다.
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

`GreedLastPrototypeEgyptianArtCheck`는 원본 알파/가져오기 설정/포즈 정책 경계/고정 크기/비율 상쇄/발 피벗/좌우 반전/누락 자원의 기본 포즈 복귀/로비 복귀를 검사한다. 원본 알파는 임시 텍스처로 읽고 즉시 정리하며 실행 자원을 CPU 읽기 가능으로 바꾸지 않는다. 개인 저장/설정/실제 화면 조작은 하지 않는다.

자동 검사는 그림의 역사 고증이나 실기기 가독성/실제 센서/소리 지연/FPS를 보증하지 않는다. 배포 완료 뒤 직접 볼 순서는 첫 화면 인물 -> 로비 -> 연습의 점프 -> 숙이기 -> 좌우 회피 -> 정지/재개 -> 결과/로비 복귀다. 카메라/판정선/함정과 인물이 겹쳐 행동을 읽기 어려운지는 사람의 플레이 확인이 남는다.
