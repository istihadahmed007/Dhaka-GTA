using System.IO;
using DhakaGta.Environment;
using UnityEditor;
using UnityEngine;

namespace DhakaGta.Editor
{
    /// <summary>
    /// Creates HDRP PBR materials bound to Assets/Textures/Environment/T_* PNG maps,
    /// generates DistrictEnvironmentPaletteSO configs and reusable Prefabs, and imports Blender FBXs when present.
    /// </summary>
    public static class EnvironmentPaletteImporter
    {
        private const string TexturesFolder = "Assets/Textures/Environment";
        private const string MaterialsFolder = "Assets/Materials/EnvironmentPbr";
        private const string PalettesFolder = "Assets/ScriptableObjects/Environment";
        private const string PrefabsFolder = "Assets/Prefabs/Environment";

        [MenuItem("Dhaka-GTA/Environment/Build HDRP PBR Materials, Palettes & Reusable Prefabs")]
        public static DhakaHeroArchitectureBuilder.MaterialPack SyncMaterialsPalettesAndPrefabs()
        {
            EnsureFolders();
            DhakaHeroArchitectureBuilder.MaterialPack pack = BuildPbrMaterialPack();
            CreateOrUpdatePalettes(pack);
            BuildReusablePrefabs(pack);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return pack;
        }

        public static DhakaHeroArchitectureBuilder.MaterialPack BuildPbrMaterialPack()
        {
            EnsureFolders();
            return new DhakaHeroArchitectureBuilder.MaterialPack
            {
                GlassTowerMat = BuildHdrpPbrMat("Mat_GulshanCurtainGlass", "GulshanCurtainGlass", Color.white, 0.2f, 0.85f, hasEmission: true),
                BananiPanelMat = BuildHdrpPbrMat("Mat_BananiCommercialPanel", "MotijheelConcrete", new Color(0.82f, 0.88f, 0.94f), 0.1f, 0.55f, hasEmission: true),
                OldDhakaBrickMat = BuildHdrpPbrMat("Mat_OldDhakaBrickPlaster", "OldDhakaBrickPlaster", Color.white, 0f, 0.25f, hasEmission: false),
                MotijheelConcreteMat = BuildHdrpPbrMat("Mat_MotijheelConcrete", "MotijheelConcrete", Color.white, 0f, 0.30f, hasEmission: true),
                ResidentialStuccoMat = BuildHdrpPbrMat("Mat_ResidentialStucco", "OldDhakaBrickPlaster", new Color(0.94f, 0.92f, 0.86f), 0f, 0.28f, hasEmission: false),
                CorrugatedTinMat = BuildHdrpPbrMat("Mat_CorrugatedTin", "CorrugatedTin", Color.white, 0.65f, 0.52f, hasEmission: false),
                WoodMat = BuildSimpleHdrpMat("Mat_TongTimber", new Color(0.38f, 0.24f, 0.14f), 0f, 0.22f),
                DarkSteelMat = BuildSimpleHdrpMat("Mat_DhakaSteel", new Color(0.22f, 0.25f, 0.28f), 0.85f, 0.55f),
                TankBlueMat = BuildSimpleHdrpMat("Mat_GaziTankBlue", new Color(0.06f, 0.26f, 0.60f), 0.05f, 0.58f),
                TrunkBarkMat = BuildSimpleHdrpMat("Mat_TropicalBark", new Color(0.30f, 0.22f, 0.16f), 0f, 0.18f),
                FoliageGreenMat = BuildSimpleHdrpMat("Mat_KrishnachuraLeaf", new Color(0.14f, 0.39f, 0.15f), 0f, 0.30f),
                BlossomRedMat = BuildSimpleHdrpMat("Mat_KrishnachuraBlossom", new Color(0.85f, 0.18f, 0.08f), 0f, 0.38f),
                NeonSignMat = BuildSimpleHdrpMat("Mat_DhakaBillboardSign", new Color(0.95f, 0.72f, 0.22f), 0f, 0.45f, emissive: new Color(1.2f, 0.85f, 0.25f)),
            };
        }

        private static void CreateOrUpdatePalettes(DhakaHeroArchitectureBuilder.MaterialPack pack)
        {
            var defs = new (string id, string label, DistrictArchitecturalStyle style, float tankProb, float teaFreq)[]
            {
                ("gulshan", "Gulshan", DistrictArchitecturalStyle.GulshanGlassHighrise, 0.8f, 0.35f),
                ("hatirjheel", "Hatirjheel", DistrictArchitecturalStyle.GulshanGlassHighrise, 0.65f, 0.65f),
                ("banani", "Banani", DistrictArchitecturalStyle.BananiCommercialBoutique, 0.75f, 0.45f),
                ("old_dhaka", "Old Dhaka", DistrictArchitecturalStyle.OldDhakaColonialHeritage, 0.85f, 0.80f),
                ("chawkbazar", "Chawkbazar", DistrictArchitecturalStyle.BazaarAndShoppingCenter, 0.80f, 0.85f),
                ("motijheel", "Motijheel", DistrictArchitecturalStyle.MotijheelCorporateTower, 0.90f, 0.50f),
                ("dhanmondi", "Dhanmondi", DistrictArchitecturalStyle.DhakaResidentialApartment, 0.88f, 0.55f),
            };

            foreach (var d in defs)
            {
                string path = $"{PalettesFolder}/EnvPalette_{d.id}.asset";
                var so = AssetDatabase.LoadAssetAtPath<DistrictEnvironmentPaletteSO>(path);
                if (so == null)
                {
                    so = ScriptableObject.CreateInstance<DistrictEnvironmentPaletteSO>();
                    AssetDatabase.CreateAsset(so, path);
                }

                so.districtId = d.id;
                so.displayName = d.label;
                so.primaryStyle = d.style;
                so.primaryFacadeMaterial = pack.GlassTowerMat;
                so.secondaryFacadeMaterial = pack.MotijheelConcreteMat;
                so.accentTrimMaterial = pack.DarkSteelMat;
                so.tinRoofMaterial = pack.CorrugatedTinMat;
                so.foliageCanopyMaterial = pack.FoliageGreenMat;
                so.barkMaterial = pack.TrunkBarkMat;
                so.rooftopTankProbability = d.tankProb;
                so.teaStallFrequency = d.teaFreq;
                EditorUtility.SetDirty(so);
            }
        }

        private static void BuildReusablePrefabs(DhakaHeroArchitectureBuilder.MaterialPack pack)
        {
            var tempRoot = new GameObject("_TempPrefabStaging");
            try
            {
                SavePrefabFromBuilder(
                    DhakaHeroArchitectureBuilder.CreateArticulatedBuildingPrefab(
                        "PF_GulshanSkyscraper_A", DistrictArchitecturalStyle.GulshanGlassHighrise, 22f, 18f, 48f, Vector3.zero, 0f, 1, pack, tempRoot.transform),
                    $"{PrefabsFolder}/PF_GulshanSkyscraper_A.prefab");
                SavePrefabFromBuilder(
                    DhakaHeroArchitectureBuilder.CreateArticulatedBuildingPrefab(
                        "PF_BananiCommercial_A", DistrictArchitecturalStyle.BananiCommercialBoutique, 18f, 14f, 26f, Vector3.zero, 0f, 2, pack, tempRoot.transform),
                    $"{PrefabsFolder}/PF_BananiCommercial_A.prefab");
                SavePrefabFromBuilder(
                    DhakaHeroArchitectureBuilder.CreateArticulatedBuildingPrefab(
                        "PF_OldDhakaHeritage_A", DistrictArchitecturalStyle.OldDhakaColonialHeritage, 16f, 12f, 12f, Vector3.zero, 0f, 3, pack, tempRoot.transform),
                    $"{PrefabsFolder}/PF_OldDhakaHeritage_A.prefab");
                SavePrefabFromBuilder(
                    DhakaHeroArchitectureBuilder.CreateArticulatedBuildingPrefab(
                        "PF_MotijheelTower_A", DistrictArchitecturalStyle.MotijheelCorporateTower, 22f, 18f, 52f, Vector3.zero, 0f, 4, pack, tempRoot.transform),
                    $"{PrefabsFolder}/PF_MotijheelTower_A.prefab");
                SavePrefabFromBuilder(
                    DhakaHeroArchitectureBuilder.CreateTeaStall(Vector3.zero, 0f, "PF_DhakaTongTeaStall", pack, tempRoot.transform),
                    $"{PrefabsFolder}/PF_DhakaTongTeaStall.prefab");
                SavePrefabFromBuilder(
                    DhakaHeroArchitectureBuilder.CreateElectricPole(Vector3.zero, 0f, pack, tempRoot.transform),
                    $"{PrefabsFolder}/PF_DhakaElectricPole.prefab");
                SavePrefabFromBuilder(
                    DhakaHeroArchitectureBuilder.CreatePromenadeBench(Vector3.zero, 0f, "PF_HatirjheelBench", pack, tempRoot.transform),
                    $"{PrefabsFolder}/PF_HatirjheelBench.prefab");
                SavePrefabFromBuilder(
                    DhakaHeroArchitectureBuilder.CreateDhakaBillboard(Vector3.zero, 0f, "PF_DhakaBillboard", pack, tempRoot.transform),
                    $"{PrefabsFolder}/PF_DhakaBillboard.prefab");
            }
            finally
            {
                Object.DestroyImmediate(tempRoot);
            }
        }

        private static void SavePrefabFromBuilder(GameObject instance, string prefabPath)
        {
            if (instance == null) return;
            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
        }

        private static Material BuildHdrpPbrMat(
            string matName, string texPrefix, Color tint, float metallic, float smoothness, bool hasEmission)
        {
            Material mat = BuildSimpleHdrpMat(matName, tint, metallic, smoothness);

            Texture2D baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesFolder}/T_{texPrefix}_BaseColor.png");
            Texture2D maskTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesFolder}/T_{texPrefix}_MaskMap.png");
            Texture2D normTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesFolder}/T_{texPrefix}_Normal.png");

            EnsureNormalMapImporter($"{TexturesFolder}/T_{texPrefix}_Normal.png");

            if (baseTex != null && mat.HasProperty("_BaseColorMap")) mat.SetTexture("_BaseColorMap", baseTex);
            if (maskTex != null && mat.HasProperty("_MaskMap")) mat.SetTexture("_MaskMap", maskTex);
            if (normTex != null && mat.HasProperty("_NormalMap")) mat.SetTexture("_NormalMap", normTex);

            if (hasEmission)
            {
                Texture2D emTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesFolder}/T_{texPrefix}_Emissive.png");
                if (emTex != null && mat.HasProperty("_EmissiveColorMap"))
                {
                    mat.SetTexture("_EmissiveColorMap", emTex);
                    if (mat.HasProperty("_EmissiveColor")) mat.SetColor("_EmissiveColor", Color.white * 1.6f);
                }
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material BuildSimpleHdrpMat(string matName, Color tint, float metallic, float smoothness, Color? emissive = null)
        {
            string path = $"{MaterialsFolder}/{matName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("HDRP/Lit");
                mat = new Material(shader != null ? shader : Shader.Find("Standard"));
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.enableInstancing = true;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
            else mat.color = tint;
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (emissive.HasValue && mat.HasProperty("_EmissiveColor")) mat.SetColor("_EmissiveColor", emissive.Value);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void EnsureNormalMapImporter(string texAssetPath)
        {
            if (AssetImporter.GetAtPath(texAssetPath) is TextureImporter importer && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
            }
        }

        private static void EnsureFolders()
        {
            string[] folders =
            {
                "Assets/Materials",
                MaterialsFolder,
                "Assets/ScriptableObjects",
                PalettesFolder,
                "Assets/Prefabs",
                PrefabsFolder,
            };
            foreach (string f in folders)
            {
                string parent = Path.GetDirectoryName(f)?.Replace("\\", "/");
                string leaf = Path.GetFileName(f);
                if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(f))
                {
                    AssetDatabase.CreateFolder(parent, leaf);
                }
            }
        }
    }
}
