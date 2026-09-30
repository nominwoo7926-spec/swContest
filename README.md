# ERGO / TWIN — 작업자 중심 피지컬 AI 연구실

Unity 6000.3.25f1 / URP / OpenXR / Meta Quest Android ARM64.

기존 월드 4개를 제거하고 **Assets/Scenes/ErgoTwinLab.unity** 하나로 재구성한 가상 조립 작업 셀입니다. Unity 프로젝트 루트는 이 폴더 그대로입니다.

## 바로 실행

Unity에서 `Assets/Scenes/ErgoTwinLab.unity`를 열고 **Play**를 누릅니다. 헤드셋 없이 자동 작업 시연이 시작됩니다. 약 10–20초 동안 고부하 자세를 관찰한 뒤 작업대·공급 트레이를 조정하고 이후 작업 주기의 평균을 비교합니다. 상태 패널과 바닥 위 그래프에서 결과를 확인합니다.

| 입력 | 기능 |
|---|---|
| 1 / 2 | 자동 작업 시연 / 수동 손 조작 |
| A | 자동 에이전트 활성화·해제 |
| E | 설비 최적화 요청 |
| Space | 비상 정지, R로 해제 |
| R | 측정 창 초기화. 설비를 순간 이동하지 않고 현재 위치 유지 |
| 방향키 / Page Up, Down | 수동 모드 손 수평 이동 / 높이 |
| G | 공급 트레이 근처에서 집기, 작업대 근처에서 놓기 |
| 마우스 오른쪽 버튼 + WASD / Q,E | 시점 회전 + 이동 / 하강·상승 |

처음의 불리한 작업 조건으로 돌아가려면 Play를 껐다 켭니다. 카메라 이동 중 A/E는 에이전트 명령으로 처리하지 않습니다.

## Quest

`Tools > Ergo Twin > Build Quest APK`로 `Builds/Quest/ErgoTwin.apk`를 생성합니다. Android Build Support, SDK, NDK, OpenJDK가 필요하며 현재 설치된 Unity에 포함되어 있습니다.

1. Quest 개발자 모드 활성화 후 USB 연결, 헤드셋 안에서 USB 디버깅 허용.
2. `tools/Install-Quest.ps1` 실행. Android SDK의 adb를 사용해 APK 설치 및 앱 실행.
3. 앱은 바닥 기준 추적 공간에서 시작합니다. 실제 장애물이 없는 공간에서 컨트롤러 양손을 추적합니다.
4. 오른손 **grip**으로 트레이 근처 부품 집기, 작업대에서 놓기. 오른손 **A** 조정 요청, **B** 비상 정지. 왼손 **X** 초기화, **Y** 자동 제어 전환.

헤드셋과 양손 추적이 모두 유효할 때만 제어합니다. 손이 이동 구역에 들어가거나 부품을 들고 있으면 설비 조정이 보류됩니다. 추적 손실 시 설비와 컨베이어를 정지합니다. Quest 독립 실행이 기본이며 PC 에디터는 헤드셋 없는 실행이 기본입니다.

## 구현 범위

- 공장 셸, 조립대, 승강 액추에이터, 공급 트레이, 롤러 컨베이어, 로봇 형상, 안전 표시, 월드 공간 관제판.
- 실제 다운로드한 Kenney rigged FBX 작업자와 텍스처. 작업 중 양팔은 분석적 2-bone IK로 손 목표를 따라갑니다. 원본 idle 애니메이션도 프로젝트에 포함합니다.
- 머리·손 좌표에서 도달 거리, 팔 방향각, 추정 몸통 기울기 및 물체 중량에 따른 부하 모멘트 계산.
- 높이·거리 후보의 비용을 비교하는 결정론적 최적화 에이전트. 상한·하한, 속도 제한, 양손 간섭, 물체 보유, 추적 손실, 비상 정지 조건 적용.
- 4 Hz CSV 로그: `Application.persistentDataPath/ErgoTwin-날짜-시간.csv`. Windows 기본 경로는 `%USERPROFILE%/AppData/LocalLow/ErgoTwin Research/Ergo Twin - Adaptive Assembly`입니다.
- 자동 시연의 센서 값은 **SIM**, Quest에서는 **XR estimated body**로 구분합니다.

이 버전의 Physical AI는 **가상 환경에서 관찰→예측→제어→검증하는 모델 기반 에이전트**입니다. 학습된 신경망이나 실제 설비 통신을 구현한 제품이 아닙니다. 부하 점수는 공개된 자체 기하학 휴리스틱이며 RULA/REBA 정식 평가나 임상적으로 검증된 판정이 아닙니다. Quest의 머리·손 추적만으로 골반·어깨·전신 관절을 직접 측정하지 않으며, 표시되는 신체 자세와 몸통 각도는 추정입니다. 안전 인터록 또한 가상 데모용입니다.

## 검증·재생성

- `Tools > Ergo Twin > Run policy verification`: 부하 순서, 최적화 비용 감소, 설비 한계, 양손·보유·추적 및 트레이·작업대 가장자리 조건 11개 검사.
- `Tools > Ergo Twin > Create new laboratory`: 월드 재생성. 현재 씬을 새 씬으로 교체하므로 편집 내용을 먼저 저장합니다.
- `Tools > Ergo Twin > Build Windows demo`: Windows 실행 파일 생성.
- 검증 결과와 실행 화면은 `Documentation/`에 보관합니다.
- `Assets/ErgoTwin/Runtime/ErgoPolicy.cs`: 계산·후보 탐색.
- `Assets/ErgoTwin/Runtime/ErgoTwinLab.cs`: 월드, 입력, 시뮬레이션, 인터록, UI, 기록.
- `Assets/ErgoTwin/Editor/ErgoTwinProject.cs`: 씬 생성, 플랫폼 설정, 빌드, 검사.

기존 소스 코드는 보존했으며 새 씬은 `ErgoTwin` 구성 요소만 사용합니다.

## 에셋·문서 출처

Kenney [Animated Characters Protagonists](https://kenney.nl/assets/animated-characters-protagonists), CC0. 원본 라이선스: `Assets/ErgoTwin/Art/Characters/License.txt`. 원본 ZIP: `Downloads/kenney-characters.zip`. 산업 환경 기하 모델은 이 프로젝트에서 직접 제작했습니다.

플랫폼 구성 참고: [Unity Meta Quest build profile](https://docs.unity.com/en-us/engine/6000.3/manual/xr/configuring-project-for/meta-quest-develop/build-profile), [Meta OpenXR Quest settings](https://developers.meta.com/horizon/documentation/unity/unity-openxr-settings-quest/).
