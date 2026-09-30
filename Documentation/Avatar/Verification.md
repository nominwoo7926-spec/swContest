# 검증 기록

Unity 6000.3.25f1의 별도 검증 프로젝트에서 컴파일·장면 생성·실제 Play 모드 실행을 수행한 뒤, 검증된 생성 에셋과 장면을 `swContest`에 반영했습니다.

- 작업자: 유효한 Unity Humanoid Avatar, 4,799개 정점의 실제 리깅 메시. 원본 Microsoft Rocketbox MIT 라이선스 포함.
- Setup을 연속 2회 실행: 작업 루트와 XR 추적기는 각각 1개. 기존 공장 라이트맵 2개 유지. 관찰 카메라의 XR 렌더링은 꺼져 있고 전용 1920×1080 RenderTexture로 출력.
- 기존 계산 검증 12개 및 Play 모드 통합 검증 37개 통과. 자동 보정, FIFO/풀, 양손 소유권, 놓기/완료, 좌우 부하, 추적 손실, 감소, 상체 변화, 아바타 눈/손 정렬, 표면 색상 전달, 발 고정, 기록/재생/재시작을 포함. 상세는 `PlayVerification.txt`와 `../QuestTask/MathVerification.txt`.
- 검증 자세에서 손 Grip 앵커 오차 약 2mm. 손과 부품 표면 Grip 위치가 12mm 이내로 일치함을 검사했습니다. 이는 해당 합성 입력 자세에 대한 결과이며 모든 체형·도달 범위의 보장은 아닙니다.
- 기록: 12개 풀, 425개 완전한 이진 프레임, 약 14.33초. 목표 30Hz, 이 검증 파일의 실제 평균 약 29.6Hz. 부분 프레임 없음.
- 헤드셋 없는 새 Play 세션에서 기록을 여는 경로 별도 통과: `HeadsetFreeReplay.txt`.
- 제3자 카메라 위치 후보를 직접 렌더해 비교하고 공구판 가림을 피하는 최종 위치를 저장. `Spectator_LeftWork.png`, `Spectator_RightWork.png`, `Spectator_Bending.png`, `Spectator_Replay.png`는 실제 Unity 렌더 캡처입니다.
- `Tools/Pull-QuestSessions.ps1` PowerShell 구문 검사 통과. 실기기 전송은 미검증.

합성 머리·양손 입력으로 작업 기능을 검증했습니다. 실제 Quest 2에서의 컨트롤러 버튼, 양안 화면, Quest Link와 PC 창의 동시 출력, 기기 성능, OBS 영상 녹화는 직접 검증하지 않았습니다. Quest 2가 연결되어 있지 않았습니다.

Unity 자체 검색 인덱서(`UnityEditor.Search.SearchDatabase`)의 `ArgumentOutOfRangeException`이 검증 프로젝트 Editor 로그에 발생했습니다. 작업 코드의 예외는 아니며, 위 Play 검증은 완료됐습니다. 로그 전체가 무오류였다는 뜻은 아닙니다.

최종 Android IL2CPP ARM64 빌드 성공: 오류 0개, 경고 4개, APK 50,308,766바이트. 앱 ID와 ARM64 라이브러리, VR headtracking 선언, APK 내부의 Rocketbox MIT 라이선스/출처 파일을 확인했습니다. 전달 APK는 검증 빌드와 해시가 같습니다. 상세 결과는 `AndroidBuild.txt`입니다.

현재 프로젝트의 작업 파일은 검증본과 해시가 일치하며, 장면·Prefab·재질·Avatar의 GUID 참조 누락은 0개였습니다. 최종 `adb devices` 조회에도 연결된 기기가 없었습니다.
