# Adaptive Workbench VR — Quest 2 공장 작업

현재 자동 책상·에이전트 흐름과 검증 상태는 [작업대 안내](AGENT_AND_TABLE_VERIFICATION.md)를 참고하세요. 아래 조작 설명이 최신 APK 기준입니다.

대회 제출 영상·실험·주장 점검은 [제출 체크리스트](CONTEST_SUBMISSION.md)를 참고하세요.
[입력→기대→실제 테스트 표](TEST_CASE_EVIDENCE.md)는 Unity 자동 검증과 아직 남은 실기기 검증을 구분합니다.

사람형 아바타, 부하 표면 색상, PC 제3자 화면과 녹화/재생 방법은 [아바타 시연 안내](../Avatar/README.md)에 정리했습니다. 오른손 B를 1초 누르면 동작 데이터 기록을 중지/다시 시작합니다.

작업 장면: `Assets/Factory/Scenes/SmartFactory_QuestTask.unity`.
원본 `SmartFactory.unity`와 기존 공장 Prefab은 보존했습니다. 기존 표지판 셰이더에는 양안 렌더링 지원만 추가했습니다.

## 실행과 조작

1. Quest의 바닥 높이와 경계를 먼저 설정하고 앱을 실행합니다.
2. **실제 방에서 벽이 아니라 빈 공간을 바라보고** 편하게 서서 양손을 내려 약 2초간 안정적으로 유지합니다. 그 방향이 앱의 작업 정면이 됩니다. 완료 바구니 오른쪽에 고정된 부하 패널의 오른쪽 위 작은 진행 원이 초록색으로 채워지면 보정 완료입니다. 세 추적값과 Floor 기준이 유효해야 보정합니다.
3. 작업대의 **초록색 첫 번째 픽업 표시**에 손 컨트롤러를 가까이 대고 **양손 Grip을 동시에** 누릅니다. 부품에 가까운 손이 잡습니다. 아직 이동 중인 부품이나 뒤쪽 대기 부품은 집지 않습니다.
4. Grip을 유지한 채 오른쪽 **완료 상자**로 옮겨 Grip을 놓습니다. 부품 전체가 상자 내부에 들어온 상태로 0.2초 유지되어야 완료됩니다. 손에 쥔 채 상자에 넣는 것만으로는 완료되지 않습니다.
5. 바닥·다른 위치에 놓은 부품은 다시 양손 Grip을 눌러 집습니다. 완료로 계산하지 않습니다.
6. 방향을 다시 맞추려면 빈손으로 열린 공간을 향해 선 뒤 **왼손 Y 버튼을 약 1초** 누르고 2초간 편히 섭니다. `Y`를 짧게 눌렀다 놓으면 개인화 메뉴가 열리고, 한 번 더 짧게 누르면 닫힙니다.

앱은 양쪽 컨트롤러와 헤드셋이 추적되고 바닥 기준이 설정된 상태에서 약 2초간 안정적으로 서면 자동 보정됩니다. 그 전에는 새 부품 공급이 시작되지 않습니다. 별도 시작 버튼은 없습니다.

벨트→높이 조절 책상→완료 바구니가 왼쪽에서 오른쪽으로 이어집니다. 벨트와 책상 기본 높이는 이전 0.99m에서 **0.82m**로 낮췄고, 첫 집기 지점도 서는 위치에서 약 0.76m로 가까워졌습니다. 부품이 책상으로 이동하면 집어서 오른쪽 바구니에 넣습니다. 조작판은 **작업자 뒤쪽**에 있으므로 몸을 돌려야 볼 수 있습니다. 제3자 녹화 화면에서는 보이지 않습니다. 평소에는 벨트 속도 `−·+` **두 버튼만** 보이고, 무게 선택 버튼은 없습니다. 모든 부품은 가상 5 kg으로 공급됩니다. 아래에 현재 m/s 값이 표시됩니다. 컨트롤러를 버튼에 직접 가까이 대면 한 단계 작동하며, 손을 떼고 다시 대야 반복됩니다. Grip이나 트리거는 필요하지 않습니다. 벨트는 속도와 무관하게 원래 색을 유지하며, 표면 무늬의 이동 속도만 달라집니다. 큰 AI 상태 모니터 대신 조작판 위 작은 `AI` 표시등이 초록(정상)·노랑(관찰/센서 대기)·빨강(개입)을 보여줍니다.

`Y`를 짧게 누르면 평소 버튼 대신 같은 위치에 **보고서가 먼저** 나타납니다. `HEIGHT`에서 키(cm)·팔 도달 길이(cm)를 `−10/−1/+1/+10/NEXT`로 입력할 수 있습니다. 처음 값은 추정치이므로 본인의 측정값으로 고치는 것이 좋습니다. `RATE`에서 실제 느낀 불편함을 `1~5`로 평가합니다(1 낮음, 5 높음). 앱에는 구간 종료 알림이나 최소 시간 제한이 없으므로 실험에서는 외부 타이머로 90~120초 작업한 뒤 평가하세요. 보고서에는 상대부하, 모델/규칙 상태, 높이·속도 제안이 보입니다. `APPLY`를 터치해야 제안이 **가상 설비에** 적용되고, `SKIP`은 거절합니다. 실제 전동 책상·벨트는 아직 연결되지 않았으며 화면에도 `Physical device: NOT CONNECTED`로 표시됩니다. [데이터 수집 절차](EXPERIMENT_RUNBOOK.md)와 [가져오기 안내](DATA_EXPORT.md)를 참고하세요.

왼손·오른손 완료 횟수는 `XRGrabTaskTracker.CompletedLeft`, `CompletedRight`, `CompletedTotal`에서 구분합니다. 바구니 오른쪽에 고정된 신체 패널에는 요구한 부위 수치, 들고 있는 무게, 7개 부위 평균만 표시합니다. 고개를 돌려도 패널은 따라오지 않습니다. 한 번에 부품 하나를 집습니다.

산호색 부품은 모두 가상 5 kg으로 표시됩니다. 이 값은 부하 모델용이며 Rigidbody 질량은 1로 동일합니다. 실제 힘·무게 저항은 재현하지 않습니다.

## 구성

- `ConveyorController`: 기본 0.38 m/s, VR 버튼과 에이전트가 0.15~0.75 m/s 범위에서 조절.
- `PartSpawner`: 3.5초 간격, 16개 재사용 풀, 모든 신규 부품을 가상 5 kg으로 공급.
- `PickupQueue`: 책상 위 3칸과 벨트 위 대기 9칸의 선입선출. 총 12칸이 차면 새 공급만 보류하며 벨트 자체는 계속 움직임.
- `AdjustableWorktable`: 0.55~1.20m 상판과 앞쪽 집기 위치를 함께 이동.
- `ConveyorPart`: 풀/이동/대기/잡힘/놓임 상태와 손 소유권. 이동·잡힘은 kinematic, 놓은 뒤만 단순 BoxCollider 물리 사용.
- `XRGrabTaskTracker`: Grip 가장자리 감지, 가까운 부품 선택, 중복 소유 방지, 상자 전체 포함 판정과 좌우 완료 횟수.
- `XRTrackingProvider`: OpenXR + Input System의 HMD 및 양손 포즈, Grip, Y 입력. 추적 손실 때 손에 든 부품과 유지 시간을 멈춤.
- `UserCalibration`: 바닥 기준, 안정 자세 평균, 작업 위치·정면 정렬, 키 비례 어깨 위치 추정.
- `BodyLoadEstimator`: 좌우 독립 점수·60초 반복 이력·상체 굽힘 추정·완만한 감소.
- `BodyLoadVisualizer`: 7개 숫자와 초록→노랑→빨강 도식. 글자는 래스터 Sprite라 외부 한글 폰트를 설치할 필요 없음.
- `WorkerProfile` + `PersonalizationConsole`: 익명 로컬 프로필, 키·팔 도달 길이 입력, 작업 구간 불편감 평가, 가상 조절 제안 승인/거절.
- `ManufacturingDataCollector` + `ErgonomicsRiskModel`: 입력값·센서 요약·제안/승인/결과를 CSV로 저장. 검증된 실측 모델 JSON이 있을 때만 ML 위험 예측을 사용하고 없으면 규칙 기반으로 제안.

풀의 16개가 모두 대기·손·바닥에 있으면 추가 생성하지 않습니다. 바닥 부품은 자동 완료·자동 삭제하지 않으며 회수해 완료해야 공급이 다시 진행됩니다. 바닥 아래로 빠지는 예외에만 픽업 근처로 복구하고 완료 처리하지 않습니다.

## 상대부하 모델

의료·인체공학 평가 척도의 검증된 진단 점수가 아닌 **이 프로젝트의 휴리스틱 추정 상대부하(0–100)**입니다. 실제 팔꿈치·손목 각도나 근전도를 측정하지 않습니다.

`clamp01`로 각 입력을 0–1 범위로 제한합니다.

- 수평 도달 `R = 거리 / 0.7m`, 전체 도달 `D = 거리 / 0.9m`
- 손 높이 `H = (손 높이 − 추정 어깨 높이 + 0.25m) / 0.75m`
- 낮은 손 위치 `L = (추정 어깨 높이 − 손 높이 − 0.35m) / 0.55m`
- 무게 `W = kg / 5`, 유지 시간 `T = 초 / 30`
- 손 이동거리 `M = m / 5`, 해당 손 최근 60초 완료 횟수 `F = 횟수 / 12`
- 어깨: `100 × (0.30R + 0.17H + 0.16L + 0.29W + 0.08F)`
- 팔: `100 × (0.31D + 0.19L + 0.21T + 0.22F + 0.07W)`
- 손목: `100 × (0.45W + 0.25T + 0.20M + 0.10F)`
- 상체: `100 × clamp01(0.6 × 머리 하강량/0.4m + 0.4 × 정면 전진량/0.45m)`; 뒤로 이동하거나 키가 올라가는 양은 0으로 취급.

어깨 중심은 현재 머리에서 키의 약 14% 아래, 정면 반대쪽으로 4.5cm에 두고 좌우 반폭은 키의 약 11.5%로 추정합니다. 머리를 돌린 것만으로 어깨 방향을 바꾸지 않고 보정한 정면을 사용합니다.

들지 않은 손은 신규 부하 목표가 0입니다. 점수 상승은 0.28초, 감소는 10초 시정수의 지수 평활을 적용해 잔여 부담을 유지합니다. 화면은 0.05초 간격 8표본 이동평균(약 0.4초)을 거쳐 10Hz로 갱신합니다. 최근 반복 횟수는 손별로 독립적이며 60초 후 사라집니다. 표시 평균은 7개 부위의 산술평균입니다.

## Editor 메뉴

`Tools > Smart Factory > Quest Task`

- `Setup or Repair Quest Task`: 작업 장면·부품 Prefab·입력·XR·패널을 자동 연결. 여러 번 실행해도 `Quest_Task` 루트는 하나입니다. 재실행하면 생성된 작업 오브젝트는 다시 구성되므로 커스텀 배치는 별도 복사본에 보관하세요.
- `Validate Task`: 연결, 대기열, 풀, 카메라, 재질 검사.
- `Verify Play Mode`: 실제 Play 모드에서 합성 머리·손 입력으로 전체 흐름 검증. 기기 입력 검증이 아니며 Android 빌드에는 테스트 코드가 포함되지 않습니다.
- `Build Quest APK`: `Builds/QuestTask/Adaptive_Workbench_VR.apk` 생성.

## Quest 기기 확인

USB로 Quest 2를 연결하고 헤드셋에서 USB 디버깅을 허용한 뒤, 프로젝트 폴더의 PowerShell에서 `powershell -ExecutionPolicy Bypass -File Tools/Install-QuestTask.ps1`을 실행하면 APK를 설치하고 실행을 요청합니다. Quest의 컨트롤러 확인 창은 헤드셋에서 통과해야 합니다. 앱 목록에서 새 버전은 `Adaptive Workbench VR`로 표시됩니다. 예전 `NOVA Factory Task` 앱이 별도로 설치되어 있을 수 있습니다.

PC Unity에서 직접 플레이하려면 Meta Quest Link 연결 및 활성 OpenXR 런타임이 필요합니다. 독립 실행 APK에는 Quest Link가 필요하지 않습니다.

Quest의 1인칭 MP4를 USB로 복사하려면 `powershell -ExecutionPolicy Bypass -File Tools/Pull-QuestVideo.ps1`을 실행합니다. 최신 앱 영상 한 개가 `Recordings/Quest/VideoShots`에 저장됩니다. `-All`을 붙이면 이 앱의 영상 전체를 복사합니다. 파일 탐색기에서 Quest 저장소의 `Oculus/VideoShots`를 직접 열어도 됩니다. 이 MP4는 1인칭입니다.

제3자 시점은 APK 안에서 바로 MP4로 녹화되지 않습니다. 보정 뒤 자동 기록된 `.fvr` 동작 데이터를 `powershell -ExecutionPolicy Bypass -File Tools/Pull-QuestSessions.ps1`로 PC에 복사하고, Unity의 `Tools > Smart Factory > Avatar Demo > Replay Session File (No Headset)`에서 `Recordings/Quest/FactorySessions`의 파일을 선택합니다. 열린 `Factory Spectator` 창을 OBS의 윈도우 캡처로 녹화합니다. `.fvr`은 영상이 아니라 재생용 작업 데이터입니다.

수동 설치 시 `adb devices`에 `device`로 표시되는지 확인하고 `adb install -r Builds/QuestTask/Adaptive_Workbench_VR.apk`로 설치합니다. 앱 라이브러리에서 반드시 `Adaptive Workbench VR`을 실행합니다.

실기기에서는 바닥 기준 보정, 좌우 Grip/Y 매핑, 양안 표지판·패널, 슬롯 도달 거리, 내려놓기, 추적 손실·복구를 확인해야 합니다. 사람마다 도달 범위가 달라 시작 자세에서 첫 슬롯과 완료 상자를 무리 없이 잡을 수 있는지도 확인하세요. 기기가 연결되지 않은 상태에서 실제 Quest 실행이나 프레임 성능을 확인했다고 간주하면 안 됩니다.

공식 설정 참고: [Unity OpenXR 입력](https://docs.unity.cn/Packages/com.unity.xr.openxr@1.14/manual/input.html), [Meta Quest OpenXR 설정](https://developers.meta.com/horizon/documentation/unity/unity-openxr-settings-quest/).
