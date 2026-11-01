# Greed 모바일 실기기 준비

현재는 **Windows에서 기획 체감을 확인하는 프로토타입**이다. APK와 휴대폰 검증을 완료한 상태가 아니다. 기존 Windows 실행본·저장·게임 규칙은 그대로 두고, 아래 설치 준비 뒤 별도 사본에서 모바일 빌드를 이어간다. PC 확인은 [빠른 확인](GreedLast_Prototype_QuickCheck.md)을 따른다.

## 10월 31일 실제 확인

**14:38 KST**, 설치된 Unity 6.4 / `6000.4.3f1` API는 `moduleSupported=false`, 번들 검사 `bundledPrerequisitesPresent=false`, 누락 파일 9개였다. 완료된 개인 데이터 분리 사본에서 기존 `Greed Last → Check Android Prerequisites`를 실행했다. Android Build Support와 SDK/NDK/JDK를 준비해야 Android 개발을 진행할 수 있다. [Unity 6.4 공식 환경 안내](https://docs.unity3d.com/6000.4/Documentation/Manual/android-sdksetup.html)

이 검사는 이 프로젝트에 고정된 Windows 호스트/Unity 6000.4.3f1의 **기본 번들 경로**를 확인한다. 누락 9개를 모든 Unity 버전에 공통인 요구 파일 목록으로 해석하지 않는다. 별도 SDK 경로 설정·도구 실행/버전·서명·약관 수락 여부를 확인한 것도 아니다. 검사 성공은 준비 상태를 읽었다는 뜻이며 APK 빌드 성공이 아니다.

사본 설정은 불변이고 소유 에디터는 exit 0으로 종료했다. 원본 프로젝트 실행·빌드 대상 변경·모듈 설치·약관 동의·사용자 화면 조작은 하지 않았다. 읽기 전용 원문은 `Logs/AndroidChecks/cd987114637e45d8a5c69a1a2af7e880/report.json`, SHA256 `BA27EA545EBDE9F0C67C17448CAA5FABD7EDBB6699F1969D68E4ACD2FD9C43E0`다. 도구 원문 SHA256은 `B3CE0F44CDACE2A2F3D863150E616E253795FBEB7905B6EFC9A3D9DDBE72E734`다.

## 준비할 때 누를 순서

1. Unity Hub의 **설치 / Installs**에서 기존 **6000.4.3f1**을 찾는다. 다른 에디터 버전으로 프로젝트를 바꾸지 않는다.
2. **관리 / Manage → 모듈 추가 / Add modules**에서 **Android Build Support**, **Android SDK & NDK Tools**, **OpenJDK**를 선택한다. 기존 설치에 모듈을 추가하는 절차는 [Unity Hub 공식 안내](https://docs.unity.com/en-us/hub/add-modules)를 따른다.
3. 표시된 약관과 설치 항목을 직접 검토해 설치한다. 이 작업에서 자동 설치나 대신 동의는 하지 않았다. 추가 메뉴가 없으면 설치 방식을 먼저 확인하고 프로젝트/에디터를 임의로 삭제하지 않는다.
4. 설치 후 현재 프로젝트를 같은 에디터로 열고 Play가 꺼진 상태에서 **Greed Last → Check Android Prerequisites**를 실행한다. 게임 진행이나 빌드 대상을 바꾸는 메뉴가 아니다.
5. Hub 기본 번들을 썼다면 `module=true`, `bundled prerequisites=true`인지 본다. 누락이 남거나 별도 도구 경로를 쓴다면 도구 설정부터 확인한다. 이 결과만으로 설치 약관·실행 가능한 도구·APK/실기기 성공까지 판정하지 않는다.

## 그다음 작업

설치가 준비되면 **별도 제품 식별자의 사본 빌드 → APK 설치/진입 → 실제 폰 확인** 순서로 진행한다. 개인 PC 진행을 옮기거나 초기화할 필요는 없다. 아래 격리 빌드 도구는 추가했지만 **Android 모듈이 없어 실제 APK 빌드 성공은 아직 검증하지 않았다.** APK 파일이나 모바일 배포가 준비됐다는 뜻이 아니다.

### 원본을 바꾸지 않는 점검

PowerShell에서 아래 명령은 커밋된 소스를 `Logs/AndroidPreviewBuilds/<고유값>/Project`에 내보낸다. **사본의 Editor 회사/제품명만 먼저 고유값으로 분리하고**, 새 Unity 6000.4.3f1 에디터가 그 식별자를 불러왔는지 확인한 뒤 준비 상태를 읽는다. 미설치로 멈춘 사본을 나중에 열어도 원본 Editor의 저장 공간을 공유하지 않도록 한 것이다. 원본 설정/실행이나 Android 대상 전환은 하지 않으며 화면/마우스/키보드도 조작하지 않는다.

```powershell
& 'C:\Projectunity\greed last\tools\build-android-preview.ps1'
```

- 미커밋 게임·자원·설정이 있으면 내보내기 전에 거부한다. `-WhatIf`는 사본/보고서도 만들지 않는다.
- `Status=PrerequisitesMissing`는 준비되지 않았다는 뜻이다. 기본 점검의 `Succeeded=true`는 미설치 상태를 정상적으로 읽었다는 의미이며 APK 성공이 아니다.
- `Status=ReadyForExplicitBuild`여도 파일 존재와 Unity 모듈 지원만 확인했다. SDK 약관·도구 실행/버전·서명·기기 검증은 별도다.
- 원본/체험판의 Greed 저장·설정, 원본 PlayerSettings와 현재 Windows DLL을 전후 비교하고 소유 에디터만 종료한다. 이전 사본·로그·실행본을 지우거나 덮어쓰지 않는다.

### 준비 검토 뒤 명시적 APK 요청

위 설치 항목과 약관/도구를 직접 검토한 뒤에만 아래 두 옵션을 함께 사용한다. 대신 동의하는 옵션이나 SDK 설치 명령이 아니다. Unity 도구가 추가 설치/약관 처리를 요구하면 먼저 그 범위를 검토하며 자동 승인하지 않는다.

```powershell
& 'C:\Projectunity\greed last\tools\build-android-preview.ps1' -BuildApk -PrerequisitesReviewed
```

준비가 없으면 Android 설정 변경·대상 전환·APK 빌드 전에 중단한다. 사본 Editor 식별자 분리는 기본 점검에서도 먼저 수행하며 원본 설정 변경이 아니다. 준비된 경로는 별도 사본/고유 Editor 제품에서 스모크 후 ARM64 IL2CPP 비개발 APK를 만들도록 구현했다. 패키지는 `com.greedlast.prototype.mobilepreview`, 제품명은 `Greed Last Mobile Preview`이며 기본 디버그 서명을 사용한다. 상점 배포 서명이 아니다. 원본과 별개이며 PC 진행도를 가져오지 않는다. 글꼴 고지 사본도 시험 프로젝트의 StreamingAssets에 넣도록 했다.

성공한 경우에만 같은 고유 폴더의 `Player/GreedLastMobilePreview.apk`와 `build.json`/해시를 확인한다. `ApkBuilt=true`와 APK 해시를 함께 확인해야 하며, 폴더나 파일 일부가 있다는 이유로 성공으로 판단하지 않는다. 자동 폰 설치/실행·adb 호출은 하지 않는다. 향후 같은 모바일 프리뷰를 갱신할 때도 패키지/서명이 달라 설치가 거부되면 기존 앱을 임의로 삭제해 해결하지 않는다.

### 11월 1일 확인한 범위

새 도구의 편집기 컴파일·격리 경로 정책, 작은 Git 사본의 미커밋 입력/WhatIf 27조건, 잘못된 검토 옵션과 직접 준비/빌드 호출 등 거부 5조건을 확인했다. 기본 점검의 최신 완료는 **12:27 KST / `152322a`**이며 새 에디터의 고유 식별자 로딩과 실제 미설치 상태를 확인했다. 같은 소스의 직접 호출 거부 5조건도 통과했다. 앞선 12:12 기본 점검/12:13 명시적 요청과 구분한다. 테스트 입력의 검토 옵션은 실제 SDK 약관 동의로 세지 않는다. 개인 저장·원본 설정·Windows 실행본은 그대로이며 원문/소스별 범위는 [확인 이력](GreedLast_Prototype_CheckHistory.md)에 남겼다.

초기 두 시험 사본도 원래 ZIP/완료 보고서를 그대로 보존하면서 고유 Editor 식별자로 전환하고 새 에디터 로딩까지 확인했다. 이 사후 보완은 사본 설정의 의도적 변경이며 앞선 읽기 점검 보고서를 덮어쓰지 않았다. 게임·보관할 조합이나 개인 진행을 초기화한 것은 아니다.

12:19에는 새 고유 제품 사본의 전체 편집기 스모크도 통과했다. 추가로 편집기 API 메타데이터를 참조한 Android 조건부 C# 대조를 통과했지만 **실제 Android 타깃 컴파일은 아니다.** Windows 플레이어 참조에 모바일 전용 Handheld가 없는 한계도 별도로 기록했다. 출력 시험 DLL은 실행하지 않았고 진동/기기 성공으로 세지 않는다. [Handheld의 플랫폼 범위](https://docs.unity3d.com/6000.4/Documentation/ScriptReference/Handheld.html)는 Unity 6.4 공식 API를 따른다.

**준비된 환경의 대상 전환·IL2CPP/Gradle·APK 생성·서명/설치·실기기 동작은 아직 미검증이다.** 이 도구의 거부 검사 통과를 휴대폰 빌드/플레이 성공으로 바꾸어 적지 않는다.

### 실제 폰에서 확인

폰 준비 전 화면 비율을 먼저 볼 때는 [이집트 인물의 태블릿/여백 미리보기](ArtDrafts/EgyptianRunnerMobilePreview-20261101.md)를 연다. 14개 합성 카메라 표본이며 아래 기기 확인을 대신하지 않는다.

1. 가로 화면 양쪽 그립에서 위험/판정선/주자와 세 대응 영역이 읽히는지 본다.
2. 점프·숙이기·비틀기와 정확/Good의 점수·집중 차이를 실제 터치로 비교한다.
3. 두 손가락·손 뗌·앱 전환 뒤 새 입력/정지/재개 카운트가 정상인지 본다.
4. 짧은 회전 통로의 중립 복귀와 어지러움을 확인한다. 실제 폰 센서와 Windows 합성 회전 검사를 구분한다.
5. 기기 소리/연결 방식의 지연과 한 판의 프레임 흐름을 확인한다. 자동 카메라 자료나 PC 보정값을 폰의 보정값/FPS로 가져오지 않는다.

게임 코드·그림·현재 Windows 실행본은 이번 준비 확인으로 바뀌지 않았다. SDK 설치 전에도 PC의 대표 일반 런·탈출·한 번 저장·무한 두 번 흐름은 계속 확인할 수 있다.
