using UnityEngine;

namespace DhakaGta.Environment
{
    public enum DistrictArchitecturalStyle
    {
        GulshanGlassHighrise = 0,
        BananiCommercialBoutique = 1,
        OldDhakaColonialHeritage = 2,
        MotijheelCorporateTower = 3,
        DhakaResidentialApartment = 4,
        BazaarAndShoppingCenter = 5,
    }

    /// <summary>
    /// ScriptableObject defining PBR architectural themes, vegetation mixes, rooftop clutter density,
    /// billboard accents, and street furniture quotas per Dhaka district.
    /// </summary>
    [CreateAssetMenu(fileName = "EnvPalette_", menuName = "Dhaka-GTA/Environment/District Environment Palette")]
    public sealed class DistrictEnvironmentPaletteSO : ScriptableObject
    {
        public string districtId = "gulshan";
        public string displayName = "Gulshan & Hatirjheel";
        public DistrictArchitecturalStyle primaryStyle = DistrictArchitecturalStyle.GulshanGlassHighrise;

        [Header("HDRP Materials")]
        public Material primaryFacadeMaterial;
        public Material secondaryFacadeMaterial;
        public Material accentTrimMaterial;
        public Material tinRoofMaterial;
        public Material foliageCanopyMaterial;
        public Material barkMaterial;

        [Header("Procedural Variation & Density")]
        public Color[] facadeTintPalette =
        {
            new Color(0.86f, 0.91f, 0.95f),
            new Color(0.76f, 0.84f, 0.90f),
            new Color(0.92f, 0.89f, 0.83f),
        };
        [Range(0f, 1f)] public float rooftopTankProbability = 0.72f;
        [Range(0f, 1f)] public float teaStallFrequency = 0.45f;
        [Range(5f, 80f)] public float treeSpacingMeters = 24f;
        [Range(15f, 100f)] public float electricPoleSpacingMeters = 38f;
        [Range(0f, 1f)] public float billboardDensity = 0.35f;

        [Header("Reusable Prefab Hooks (Populated by Blender FBX or DhakaHeroArchitectureBuilder)")]
        public GameObject[] buildingVariantPrefabs;
        public GameObject[] treeVariantPrefabs;
        public GameObject[] streetPropPrefabs;
    }
}
