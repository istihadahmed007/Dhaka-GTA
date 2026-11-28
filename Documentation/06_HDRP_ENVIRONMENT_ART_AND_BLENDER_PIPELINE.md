# Dhaka-GTA — HDRP AAA Environment Art & Blender Asset Pipeline (Part 4)

## 1. Blender Authoring Pipeline (`/BlenderPipeline/`)

Because `blender.exe` is not yet installed on this workstation, the complete Python authoring suite is prepared cleanly for deterministic offline FBX export (`1 unit = 1 meter`, `-Z` Forward, `+Y` Up) into `UnityProject/Assets/Models/Environment/`:

| File | Scope |
| :--- | :--- |
| [dhaka_blender_utils.py](../BlenderPipeline/dhaka_blender_utils.py) | Metric stage setup, Principled BSDF PBR material bindings, smart box UV unwrap, FBX export |
| [generate_dhaka_buildings.py](../BlenderPipeline/generate_dhaka_buildings.py) | **Categories 1–6 & 8:** Gulshan glass skyscrapers (`_Var0`, `_Var1` with recessed mullions/podiums/LODs), Banani commercial, Old Dhaka heritage colonnade arches/cornices, Motijheel towers, Residential apartments with balconies & rooftop Gazi tanks, Markets |
| [generate_dhaka_props_and_foliage.py](../BlenderPipeline/generate_dhaka_props_and_foliage.py) | **Categories 7 & 9–12:** Tong tea stalls, electric poles + cross-arms + transformers, rooftop water tank assemblies, Krishnachura scarlet flowering trees, MRT-6 elevated station modules |
| [build_all_dhaka_fbx.py](../BlenderPipeline/build_all_dhaka_fbx.py) | Master headless runner: `blender --background --python BlenderPipeline/build_all_dhaka_fbx.py` |

---

## 2. Generated Seamless HDRP PBR Texture Maps (`Assets/Textures/Environment/`)

20 tileable `512x512` RGBA PNG texture maps (`BaseColor`, tangent-space `Normal`, HDRP packed `_MaskMap` (`R=Metallic`, `G=AO`, `B=Detail`, `A=Smoothness`), and dusk window `Emissive`) were authored natively:

- `T_GulshanCurtainGlass_{BaseColor, Normal, MaskMap, Emissive}`
- `T_MotijheelConcrete_{BaseColor, Normal, MaskMap, Emissive}`
- `T_OldDhakaBrickPlaster_{BaseColor, Normal, MaskMap}` (includes monsoon moisture darkening at lower courses)
- `T_CorrugatedTin_{BaseColor, Normal, MaskMap}` (for Dhaka tong stalls & market roofs)
- `T_WetMonsoonAsphalt_{BaseColor, Normal, MaskMap}` (puddle roughness variation in Alpha channel)
- `T_DhakaSidewalkPaver_{BaseColor, Normal, MaskMap}`

---

## 3. Unity C# Environment Systems (`DhakaGta.Environment`)

- **[DistrictEnvironmentPaletteSO.cs](../UnityProject/Assets/Scripts/Environment/DistrictEnvironmentPaletteSO.cs):** Reusable district ScriptableObjects (`gulshan`, `hatirjheel`, `banani`, `old_dhaka`, `chawkbazar`, `motijheel`, `dhanmondi`).
- **[DhakaHeroArchitectureBuilder.cs](../UnityProject/Assets/Scripts/Environment/DhakaHeroArchitectureBuilder.cs):** Multi-part non-flat articulated prefabs (`LODGroup` 3 tiers, optimized `BoxCollider` / `CapsuleCollider`, static lightmap support, exterior fins/verandah pillars/balconies/rooftop Gazi water tanks).
- **[EnvironmentAssetVariation.cs](../UnityProject/Assets/Scripts/Environment/EnvironmentAssetVariation.cs) & [GpuInstancedPropBatcher.cs](../UnityProject/Assets/Scripts/Environment/GpuInstancedPropBatcher.cs):** Deterministic `MaterialPropertyBlock` tint/smoothness variations + Unity 6 `Graphics.RenderMeshInstanced` (`RenderParams`) batching for tropical Krishnachura/Palm trees (`enableInstancing = true`).
- **[OverheadWireCatenaryBuilder.cs](../UnityProject/Assets/Scripts/Environment/OverheadWireCatenaryBuilder.cs):** Parabolic sagging catenary cables connecting consecutive roadside utility poles.
- **[InteractiveEnvironmentProp.cs](../UnityProject/Assets/Scripts/Environment/InteractiveEnvironmentProp.cs) & [ProtagonistInteractionScanner.cs](../UnityProject/Assets/Scripts/Environment/ProtagonistInteractionScanner.cs):** Genuine player interaction in Gulshan–Hatirjheel (`[E]` drink spiced cha at Tong stalls to heal `+20 HP` & refill stamina, rest on lakeside benches; `[F]` enter/exit nearby `GulshanGT_DrivableCar`).
- **[GulshanHatirjheelEnvironmentPopulator.cs](../UnityProject/Assets/Scripts/Environment/GulshanHatirjheelEnvironmentPopulator.cs) & [EnvironmentPaletteImporter.cs](../UnityProject/Assets/Scripts/Editor/EnvironmentPaletteImporter.cs):** Populates the **Gulshan–Hatirjheel** milestone scene cleanly and exports reusable Prefabs into `Assets/Prefabs/Environment/`.
