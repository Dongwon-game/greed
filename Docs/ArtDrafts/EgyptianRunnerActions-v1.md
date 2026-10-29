# 이집트풍 인간 주자 시안 v1

2026-10-29 사용자 변경: 기존 이미지에서 원한 것은 그림체뿐이며, 캐릭터는 고대 이집트에서 볼 법한 사람으로 전환한다. 도깨비의 뿔/초록 피부/동양풍 복식은 새 방향이 아니다.

- 유지: 선명한 외곽선과 채색 방식, 뒤에서 보는 주자, 점프/숙이기/비틀기의 세 동작.
- 새 방향: 인간 여성, 자연스러운 갈색 피부, 검은 땋은 머리, 흰 리넨풍 의상, 금/청금석 장식, 청록 허리띠와 가죽 샌들. 특정 왕조의 고증 복원이 아닌 게임용 이집트풍 디자인이다.
- 상태: 사용자 승인 뒤 같은 인물의 독립 포즈 4장과 입구 배경을 소스에 연결했다. [적용/생성 사양과 검증 범위](EgyptianRunnerIntegration-v1.md)를 따른다. 새 실행본 검증 전까지 기존 13:52 실행본은 보존하며, 이전 도깨비 동작 시안은 적용하지 않는다.
- 적용 전: 세 칸의 실제 그림 경계/여백/일관된 크기를 검증하고, 기본 달리기 이미지도 같은 인간 캐릭터로 맞춘다. 현재 시안을 곧바로 1/3 UV로 연결하지 않는다.
- 파일: `EgyptianRunnerActions-v1.png`. 기존 이미지는 덮어쓰지 않았다.
- 생성: 내장 image_gen, transparent_background=true. 참조는 앞선 동작 시안의 그림체/포즈이며 새 캐릭터로 변경했다.

## 생성 프롬프트 원문

```text
Edit the attached three-action character sprite atlas for a mobile pyramid rhythm-escape game. The user wants ONLY its detailed painterly ink-outline drawing style, NOT the oni/demon identity or East Asian costume. Replace ALL THREE figures with the SAME clearly HUMAN adult ancient-Egyptian female runner, an ordinary athletic woman escaping a pyramid, not a goddess or royalty. Natural warm medium-brown skin, human ears, NO horns, NO green skin, NO supernatural anatomy. Black Egyptian-style braided hair tied back with a narrow lapis-and-gold band, a practical ivory/white linen short-sleeved tunic with an above-knee wrapped skirt and modest fitted shorts underneath for running, a restrained gold and lapis collar, woven teal waist sash with small ochre accents, simple leather sandals. The silhouette must read as ancient Egypt rather than Japanese, Korean, modern fantasy, or an oni girl. Remove the old white puff sleeves, floral red ribbons, horn jewelry, ornate East Asian belt medallions and heavy boots. Keep the same polished illustrated game-art style and coherent costume across all three poses, with athletic functional clothing and no sexualized posing. Preserve the action order LEFT TO RIGHT: airborne jump with tucked bent knees; genuinely LOW crouch/slide with torso folded; evasive twisting sidestep. Keep the camera rear/rear-three-quarter facing forward into an unseen corridor. New wide 2:1 atlas, exactly three equal-width vertical cells in one horizontal row, no lines or labels. Very important: each whole figure, including hair and hands, must fit inside the CENTRAL 82% of its own cell, leaving generous truly transparent gutters; absolutely no limbs or cloth crossing cell boundaries. Consistent anatomical scale, lowest foot baseline at 92% canvas height; the low crouch retains empty transparent space above and is NOT enlarged to fill its cell. No environment, floor, cast shadow, glow, effects, text, grid, watermark or border. Genuine transparent PNG alpha, no baked black/white/checkerboard background.
```
