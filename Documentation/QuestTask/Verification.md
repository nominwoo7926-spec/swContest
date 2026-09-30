# 검증 범위

Unity 6000.3.25f1의 별도 검증 프로젝트에서 실행한 뒤, 검증된 장면과 생성 에셋을 현재 프로젝트로 반영했습니다. 기존 공장 장면은 보존했습니다.

- C# 컴파일 및 Setup 연속 2회: 성공. 작업 루트 1개, FIFO 4칸, 풀 12개, 활성 카메라 1개, 기존 라이트맵 2개 유지. 참조 및 UI 재질 검사 오류 0개. 상세: `SceneValidation.txt`.
- 계산 검증 12개: 무게·거리·높이·시간·손 이동·반복 증가, 상체 범위, 감소 및 색상. 상세: `MathVerification.txt`.
- 실제 Unity Play 모드 통합 검증 22개: 합성 HMD/양손 입력을 사용한 자동 보정, 공급/대기열, 동시 잡기 방지, 좌우 분리, 추적 손실, 바닥 재집기, 완료 판정, 풀 반환, 점수 감소와 상체 변화. 상세: `PlayVerification.txt`.
- `TaskView.png`와 `PlayView.png`: Unity 렌더 결과를 직접 확인. 패널의 깨진 셰이더 참조를 수정하고 재검증했습니다. `PlayView.png`는 왼손 작업 시점입니다.
- Android IL2CPP ARM64 최종 빌드 성공: 오류 0개, 경고 4개, APK 45,943,174바이트. APK 내부의 앱 ID, ARM64 라이브러리와 VR headtracking 선언도 확인했습니다. 상세는 `AndroidBuild.txt`, APK는 `Builds/QuestTask/NOVA_FactoryTask.apk`입니다. OpenXR 선택적 설정 권고 및 진단 심볼 관련 빌드 경고는 남아 있습니다.
- 현재 프로젝트의 작업 파일과 APK가 검증본과 동일함을 해시로 확인했고, 장면·Prefab·재질의 직렬화된 GUID 참조 누락은 0개였습니다.
- 설치 스크립트는 PowerShell 구문 검사를 통과했습니다. 연결된 Quest가 없어 실제 설치·실행은 하지 않았습니다.

Play 검증 로그에 Unity Editor 검색 인덱서(`UnityEditor.Search.SearchDatabase`)의 `ArgumentOutOfRangeException`이 발생했습니다. 작업 런타임의 예외가 아니며, 22개 흐름 검증은 완료됐습니다. 로그 전체가 무오류였다는 의미는 아닙니다.

실제 Quest 2에서의 양안 화면, Grip/Y 버튼, 바닥 기준, 팔 도달 거리, 착용 편안함, 프레임률은 미검증입니다. 본 점수는 위치·무게·시간을 조합한 상대 지표이며 의료 진단값이나 실제 근력 측정값이 아닙니다.
