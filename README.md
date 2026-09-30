# NOVA / Smart Factory

Unity 6000.3.25f1 / Universal Render Pipeline / 1 Unity unit = 1 metre.

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

All new geometry and texture assets are generated locally by the included Editor source; no external 3D art packages are required. Sign lettering is stored as a raster atlas; no font file is distributed.
