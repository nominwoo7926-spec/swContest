# NOVA / Smart Factory

Unity 6000.3.25f1 / Universal Render Pipeline / 1 Unity unit = 1 metre.

## Quest 2 작업
`Assets/Factory/Scenes/SmartFactory_QuestTask.unity`를 열면 기존 공장에 컨베이어 집기 작업과 좌우 상대부하 패널이 연결되어 있습니다. 사용 방법과 계산 기준은 [Quest 작업 안내](Documentation/QuestTask/README.md)를 참고하세요. APK는 `Builds/QuestTask/NOVA_FactoryTask.apk`입니다.

사람형 작업자 아바타의 신체 표면 부하 색상과 제3자 촬영 기능도 포함되어 있습니다. `Tools > Smart Factory > Avatar Demo > Open Spectator Window`로 PC 촬영 창을 엽니다. Quest 단독 작업 기록을 헤드셋 없이 재생할 수도 있습니다. [연결·조작·OBS 녹화·아바타 교체 안내](Documentation/Avatar/README.md).

## 외부 자산 · 라이선스
| 자산 | 출처 | 라이선스 / 비고 |
|---|---|---|
| 작업자 아바타 `Construction_Male_05` | Microsoft Rocketbox | MIT (`Assets/FactoryTask/Avatar/Rocketbox/LICENSE.md`) |
| 한글 폰트 `NotoSansKR.ttf` | Google Noto / Adobe | SIL OFL 1.1 (`Assets/FactoryTask/Art/Fonts/OFL.txt`) |
| 대기 애니메이션 `Worker_Idle.fbx` | Adobe Mixamo | 약관상 원본 파일 재배포 불가 → **저장소에 포함하지 않음** |
| Meta XR SDK (Core·Interaction·Movement) | Meta | Unity 패키지로 설치 (Meta SDK 라이선스) |

**처음 받은 경우:** Mixamo(https://www.mixamo.com)에서 서 있는 Idle 애니메이션을 *FBX for Unity* 형식으로 받아 `Assets/FactoryTask/Avatar/Animations/Worker_Idle.fbx` 이름으로 넣은 뒤 Unity를 여세요. 함께 들어 있는 `.meta` 파일 덕분에 장면의 참조가 그대로 연결됩니다.

개인 식별 정보, 작업 기록(`.fvr`), 부하 데이터(CSV), 녹화 영상은 저장소에 포함하지 않습니다. 작업 데이터는 헤드셋 내부에만 저장되고 `Tools/Pull-LoadData.cmd`로만 PC에 복사됩니다.

아래 설명은 보존된 정적 전시 장면 `SmartFactory.unity`에 대한 안내입니다.

## Open
Open `Assets/Factory/Scenes/SmartFactory.unity`. The saved scene is complete outside Play mode. Select Game view for the fixed presentation camera. There are no gameplay, interaction, UI, worker, agent, measurement, or conveyor animation scripts.

## Assets
- `Assets/Factory/Prefabs`: production lines A/B, 9 m conveyor, telescopic workbench, rack, cart, parts bin, pallet, carton, cabinet, bollard and ceiling fixture.
- `Assets/Factory/Materials`: URP materials for painted metal, steel, rubber, concrete, plastic, glass, timber and signage.
- `Assets/Factory/Meshes`: shared chamfered geometry, cylinders and cone mesh.
- `Assets/Factory/Textures`: generated microtextures and baked reflection.
- `Assets/Factory/Editor/FactoryWorldBuilder.cs`: authoring only; excluded from player builds.
- `Documentation`: actual Unity camera renders and validation report.

## Regenerate
`Tools > Smart Factory > Build Complete Factory` creates and saves the complete scene and Prefabs, then bakes lighting and exports the presentation images. This replaces the generated scene and generated Prefabs; save custom variations elsewhere first. `Bake Lighting and Capture` bakes illumination and reflections and exports three camera images. `Validate Factory` checks scene materials and missing scripts.

## Layout and future extension
The 25 x 18 x 7 m interior has two parallel 9 m conveyors at Z = +/-3.05 m and a 2.6 m central pedestrian aisle. Infeed is west, inspection is central, assembly is east, and finished goods staging is beside the loading bay. Storage stays outside the central aisle.

The hierarchy has `Factory_Architecture`, `Production_Line_A`, `Production_Line_B`, `Workstations`, `Safety_Equipment`, `Factory_Props`, `Lighting`, and `Cameras`.

- Conveyor: `Belt_FutureSpeedTarget`, `Frame_Static`, `Rollers`.
- Workbench: `Tabletop_FutureHeightTarget`, `Lower_Frame_Static`. The moving column sections, pegboard, task fixture, tray and button housing belong to the tabletop group.

Everything is static in this version. Before future animation, remove static flags from the affected renderers and revise their baked lighting. Only the floor has a collision surface; future interaction colliders are outside this environment-only scope.

The scene uses baked ceiling area lights, a single soft-shadow directional light, light probes, one baked box-projected reflection probe, shared materials and meshes, and no post-processing. Quest 2 device performance has not been measured; this is an environment asset, not a certified mobile performance build.

All new geometry and texture assets are generated locally by the included Editor source; no external 3D art packages are required. Sign lettering in the static factory scene is stored as a raster atlas. The Quest task UI uses the bundled Noto Sans KR font (SIL OFL 1.1, see the license table above).
