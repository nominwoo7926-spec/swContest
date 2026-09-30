# 검증 기록

검증 환경: Windows, Unity 6000.3.25f1, 2026-09-30.

- 새 씬 `Assets/Scenes/ErgoTwinLab.unity` 생성 및 에디터 컴파일 성공.
- 이전 작업 월드 4개 제거, 빌드 씬은 새 월드 하나만 등록.
- Kenney 공식 CC0 패키지 실제 다운로드, FBX·텍스처 import와 씬 인스턴스 확인.
- `policy-verification.txt`: 11개 계산·최적화·인터록 검사. 높은 트레이와 작업대 바깥쪽 가장자리까지 간섭 영역에 포함하는지 검증.
- `runtime-verification.txt`: Play 중 비상 정지가 설비·컨베이어를 고정하고, 승인 요청으로 해제되지 않으며, 초기화로 해제되는지 확인. 활성 카메라와 AudioListener 각각 1개.
- `desktop-session.csv`: 헤드셋 없는 자동 작업 시연에서 수집한 실제 런타임 로그. OBSERVE 24개 샘플 평균 58.96, VERIFY 244개 샘플 평균 44.86. 기하학 휴리스틱 점수이며 임상 효과나 실측 인체공학 개선을 입증하는 자료가 아님. 전후 측정 기간 길이가 다르고 작업 주기 위상 영향을 받음.
- `laboratory.png`: Unity 카메라에서 1600×1000으로 캡처한 실행 화면.
- Android 결과는 `build-Android.txt`에 기록. 첫 시도에서 deprecated Oculus Quest feature 오류를 확인하고 현재 Meta Quest feature로 전환함.
- `adb devices` 검사 당시 연결된 기기 없음. Quest 착용·컨트롤러 실물 입력·GPU 프레임 시간은 미검증.

이 앱은 가상 작업 셀의 모델 기반 제어 시연입니다. 실제 설비 인터페이스, 검증된 인체공학 평가, 학습 데이터셋 또는 학습된 신경망은 포함하지 않습니다.
