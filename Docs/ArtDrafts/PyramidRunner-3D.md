# 피라미드의 3D 주자

기초 연습과 Windows 일반 런의 주행 시점에 자연스러운 비율의 인간 주자를 배치한다. 전용 통로 카메라가 주자와 함정을 함께 렌더링한다. 리넨 의복, 청록 허리띠, 구리색 장식과 땋은 머리로 기존 이집트풍 인간 주자의 디자인을 이어간다.

인체 바탕, 관절 기준점과 기본 가중치는 MakeHuman의 CC0 자료 세 파일에서 가져왔다. 출처 커밋은 `a8bc2d54ff0ac92e78ff71431b1023eda42bf482`다. 자산 생성 도구는 인체를 미터 단위로 바꾸고 의복·땋은 머리와 색을 입힌다. 게임에 들어가는 데이터는 이 결과의 정점·삼각형·바인드 자세·최대 네 관절 가중치다. [원본 커밋](https://github.com/makehumancommunity/makehuman/tree/a8bc2d54ff0ac92e78ff71431b1023eda42bf482/makehuman/data) · [MakeHuman 자산 라이선스](https://github.com/makehumancommunity/makehuman/blob/a8bc2d54ff0ac92e78ff71431b1023eda42bf482/LICENSE.md#c-the-license-for-the-bundled-assets)

재생성에 필요한 `base.obj`, `default.mhskel`, `default_weights.mhw`를 한 작업 폴더에 모아 `node tools/bake-greed-runner.mjs <작업 폴더> Assets/Resources/GreedLast/PyramidRunner.json`을 실행한다. 게임 실행 중에는 주행 거리로 발 디딤을 맞추고, 기존 판정에서 발행되는 동작 신호에 따라 점프·숙이기·비틀기를 재생한다.
