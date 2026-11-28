# Git LFS & Binary Asset Governance Recommendations (`Dhaka-GTA`)

## 1. Serialization Mode
Set **Unity Editor → Edit → Project Settings → Editor**:
- **Asset Serialization Mode:** `Force Text` (human-readable YAML for `.unity`, `.prefab`, `.mat`, `.asset`, `.controller`, `.inputactions` enabling clean diffs and merge conflict inspection).
- **Version Control Mode:** `Visible Meta Files` (every file and folder inside `UnityProject/Assets/` and `UnityProject/Packages/` must commit alongside its `.meta` file).

## 2. Generated Folders Never Committed
Both root `.gitignore` and `UnityProject/.gitignore` strictly exclude:
- `UnityProject/Library/`, `Temp/`, `Obj/`, `Build/`, `Builds/`, `Logs/`, `UserSettings/`
- `UnityProject/MemoryCaptures/`, `Recordings/`, `ServerData/`, `Screenshots/`, `Assets/Captures/`
- `UnityProject/Assets/StreamingAssets/aa/`, `Assets/Addressables_Temp/`, `*.csproj`, `*.sln`, `*.slnx`

## 3. Recommended `.gitattributes` for Git LFS

Install Git LFS (`git lfs install`) prior to importing high-resolution HDRP textures, Blender source `.blend` files, FBX character/vehicle meshes, or uncompressed audio stems into `UnityProject/Assets/`. Track binary assets as follows:

```gitattributes
# 3D Source & Exported Models
*.blend filter=lfs diff=lfs merge=lfs -text
*.fbx filter=lfs diff=lfs merge=lfs -text
*.glb filter=lfs diff=lfs merge=lfs -text
*.gltf filter=lfs diff=lfs merge=lfs -text
*.obj filter=lfs diff=lfs merge=lfs -text

# High-Resolution PBR Textures & Skyboxes (HDRP)
*.png filter=lfs diff=lfs merge=lfs -text
*.tga filter=lfs diff=lfs merge=lfs -text
*.tif filter=lfs diff=lfs merge=lfs -text
*.tiff filter=lfs diff=lfs merge=lfs -text
*.psd filter=lfs diff=lfs merge=lfs -text
*.psb filter=lfs diff=lfs merge=lfs -text
*.exr filter=lfs diff=lfs merge=lfs -text
*.hdr filter=lfs diff=lfs merge=lfs -text
*.cubemap filter=lfs diff=lfs merge=lfs -text

# Audio & Video Stems
*.wav filter=lfs diff=lfs merge=lfs -text
*.ogg filter=lfs diff=lfs merge=lfs -text
*.mp3 filter=lfs diff=lfs merge=lfs -text
*.aif filter=lfs diff=lfs merge=lfs -text
*.mp4 filter=lfs diff=lfs merge=lfs -text
*.webm filter=lfs diff=lfs merge=lfs -text

# Compiled / Binary Unity Assets & Bakes
*.lighting filter=lfs diff=lfs merge=lfs -text
*.terrainlayer filter=lfs diff=lfs merge=lfs -text
*.ttf filter=lfs diff=lfs merge=lfs -text
*.otf filter=lfs diff=lfs merge=lfs -text
*.dll filter=lfs diff=lfs merge=lfs -text
*.pdb filter=lfs diff=lfs merge=lfs -text
*.zip filter=lfs diff=lfs merge=lfs -text
```

> **Important Note on Phase 1:** Do not run `git lfs track` on existing `.glb` assets inside `public/models/` unless coordinated with GitHub Pages CI (`actions/checkout@v4` with `lfs: true`). Keep small Phase 1 config/YAML files cleanly in standard Git until LFS is enabled across the team repository.
