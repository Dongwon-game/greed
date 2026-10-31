# Greed 모바일 실기기 준비

현재는 **Windows에서 기획 체감을 확인하는 프로토타입**이다. APK와 휴대폰 검증을 완료한 상태가 아니다. 기존 Windows 실행본·저장·게임 규칙은 그대로 두고, 아래 설치 준비 뒤 별도 사본에서 모바일 빌드를 이어간다. PC 확인은 [빠른 확인](GreedLast_Prototype_QuickCheck.md)을 따른다.

## 10월 31일 실제 확인

**14:38 KST**, 설치된 Unity 6.4 / `6000.4.3f1` API는 `moduleSupported=false`, 번들 검사 `bundledPrerequisitesPresent=false`, 누락 파일 9개였다. 완료된 개인 데이터 분리 사본에서 기존 `GreedLast/Check Android Prerequisites`를 실행했다. Android Build Support와 SDK/NDK/JDK를 준비해야 Android 개발을 진행할 수 있다. [Unity 6.4 공식 환경 안내](https://docs.unity3d.com/6000.4/Documentation/Manual/android-sdksetup.html)

이 검사는 이 프로젝트에 고정된 Windows 호스트/Unity 6000.4.3f1의 **기본 번들 경로**를 확인한다. 누락 9개를 모든 Unity 버전에 공통인 요구 파일 목록으로 해석하지 않는다. 별도 SDK 경로 설정·도구 실행/버전·서명·약관 수락 여부를 확인한 것도 아니다. 검사 성공은 준비 상태를 읽었다는 뜻이며 APK 빌드 성공이 아니다.

사본 설정은 불변이고 소유 에디터는 exit 0으로 종료했다. 원본 프로젝트 실행·빌드 대상 변경·모듈 설치·약관 동의·사용자 화면 조작은 하지 않았다. 읽기 전용 원문은 `Logs/AndroidChecks/cd987114637e45d8a5c69a1a2af7e880/report.json`, SHA256 `BA27EA545EBDE9F0C67C17448CAA5FABD7EDBB6699F1969D68E4ACD2FD9C43E0`다. 도구 원문 SHA256은 `B3CE0F44CDACE2A2F3D863150E616E253795FBEB7905B6EFC9A3D9DDBE72E734`다.

## 준비할 때 누를 순서

1. Unity Hub의 **설치 / Installs**에서 기존 **6000.4.3f1**을 찾는다. 다른 에디터 버전으로 프로젝트를 바꾸지 않는다.
2. **관리 / Manage → 모듈 추가 / Add modules**에서 **Android Build Support**, **Android SDK & NDK Tools**, **OpenJDK**를 선택한다. 기존 설치에 모듈을 추가하는 절차는 [Unity Hub 공식 안내](https://docs.unity.com/en-us/hub/add-modules)를 따른다.
3. 표시된 약관과 설치 항목을 직접 검토해 설치한다. 이 작업에서 자동 설치나 대신 동의는 하지 않았다. 추가 메뉴가 없으면 설치 방식을 먼저 확인하고 프로젝트/에디터를 임의로 삭제하지 않는다.
4. 설치 후 현재 프로젝트를 같은 에디터로 열고 Play가 꺼진 상태에서 **Greed Last → Check Android Prerequisites**를 실행한다. 게임 진행이나 빌드 대상을 바꾸는 메뉴가 아니다.
5. Hub 기본 번들을 썼다면 `module=true`, `bundled prerequisites=true`인지 본다. 누락이 남거나 별도 도구 경로를 쓴다면 도구 설정부터 확인한다. 이 결과만으로 설치 약관·실행 가능한 도구·APK/실기기 성공까지 판정하지 않는다.

## 그다음 작업

설치가 준비되면 **별도 제품 식별자의 사본 빌드 → APK 설치/진입 → 실제 폰 확인** 순서로 진행한다. 개인 PC 진행을 옮기거나 초기화할 필요는 없다. 현재 APK 파일이나 모바일 서명/배포가 준비됐다는 뜻은 아니다.

1. 가로 화면 양쪽 그립에서 위험/판정선/주자와 세 대응 영역이 읽히는지 본다.
2. 점프·숙이기·비틀기와 정확/Good의 점수·집중 차이를 실제 터치로 비교한다.
3. 두 손가락·손 뗌·앱 전환 뒤 새 입력/정지/재개 카운트가 정상인지 본다.
4. 짧은 회전 통로의 중립 복귀와 어지러움을 확인한다. 실제 폰 센서와 Windows 합성 회전 검사를 구분한다.
5. 기기 소리/연결 방식의 지연과 한 판의 프레임 흐름을 확인한다. 자동 카메라 자료나 PC 보정값을 폰의 보정값/FPS로 가져오지 않는다.

게임 코드·그림·현재 Windows 실행본은 이번 준비 확인으로 바뀌지 않았다. SDK 설치 전에도 PC의 대표 일반 런·탈출·한 번 저장·무한 두 번 흐름은 계속 확인할 수 있다.
