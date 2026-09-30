# Factory verification

- Unity 6000.3.25f1 compiled the final Editor authoring source successfully.
- Built and saved Assets/Factory/Scenes/SmartFactory.unity and 13 reusable Prefabs.
- Generated 20 Materials with URP-compatible surfaces and a depth-tested static lettering shader.
- Baked 15 ceiling area lights into 2 lightmaps; baked 90 light probe positions and a 128 px reflection cubemap.
- Scene validation: 0 missing scripts, 0 broken materials, 0 missing/empty meshes, all 8 required root groups present, 0 custom runtime behaviours.
- Inspected actual Unity camera captures for whole room, workbench detail and central aisle. Corrected ceiling occlusion, lettering depth/atlas persistence, flat mesh normals, signage fit and lighting levels.
- Reloaded the saved scene in Unity. Final interactive Editor log confirms SmartFactory.unity loaded successfully.
- Quest 2 hardware performance was not tested. No runtime interaction or simulation features are included.

Images: Factory_Presentation.png, Factory_Workstation.png, Factory_Aisle.png.