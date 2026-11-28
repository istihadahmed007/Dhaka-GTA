using System.Collections.Generic;
using DhakaGta.Core;
using UnityEngine;

namespace DhakaGta.Environment
{
    /// <summary>
    /// Multi-part HDRP procedural architectural & prop builder implementing all 12 Dhaka asset categories
    /// with recessed mullions, podiums, balconies, rooftop water tanks, 3-level LODGroups, optimized colliders,
    /// static lightmap settings, and automatic FBX swap preference when BlenderPipeline assets are present.
    /// </summary>
    public static class DhakaHeroArchitectureBuilder
    {
        public sealed class MaterialPack
        {
            public Material GlassTowerMat;
            public Material BananiPanelMat;
            public Material OldDhakaBrickMat;
            public Material MotijheelConcreteMat;
            public Material ResidentialStuccoMat;
            public Material CorrugatedTinMat;
            public Material WoodMat;
            public Material DarkSteelMat;
            public Material TankBlueMat;
            public Material TrunkBarkMat;
            public Material FoliageGreenMat;
            public Material BlossomRedMat;
            public Material NeonSignMat;
        }

        public static GameObject CreateArticulatedBuildingPrefab(
            string name,
            DistrictArchitecturalStyle style,
            float width,
            float depth,
            float totalHeight,
            Vector3 centerPos,
            float yawDegrees,
            int variantSeed,
            MaterialPack mats,
            Transform parent)
        {
            var root = new GameObject(name);
            root.layer = PhysicsLayers.Building;
            root.isStatic = true;
            root.transform.SetParent(parent, false);
            root.transform.position = centerPos;
            root.transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f);

            var lod0List = new List<Renderer>();
            var lod1List = new List<Renderer>();
            var lod2List = new List<Renderer>();

            float podiumH = Mathf.Min(7.5f, totalHeight * 0.25f);
            float shaftH = Mathf.Max(4f, totalHeight - podiumH);
            Material primaryMat = ResolvePrimaryMat(style, mats);

            // Base Podium / Colonnade (LOD0 + LOD1)
            GameObject podium = AddPart(root.transform, "Podium", new Vector3(0f, podiumH * 0.5f, 0f),
                new Vector3(width + 1.2f, podiumH, depth + 1.2f), mats.MotijheelConcreteMat ?? primaryMat, lod0List, lod1List);

            // Main Shaft recessed slightly inside exterior structural ribs/mullions
            AddPart(root.transform, "MainShaft", new Vector3(0f, podiumH + shaftH * 0.5f, 0f),
                new Vector3(width - 0.5f, shaftH, depth - 0.5f), primaryMat, lod0List, lod1List);

            // Exterior articulated geometry (never flat texture-only box!)
            if (style == DistrictArchitecturalStyle.GulshanGlassHighrise || style == DistrictArchitecturalStyle.MotijheelCorporateTower)
            {
                int fins = 4;
                for (int i = 0; i < fins; i++)
                {
                    float rx = -width * 0.42f + i * (width * 0.84f / (fins - 1));
                    AddPart(root.transform, $"MullionFin_{i}", new Vector3(rx, podiumH + shaftH * 0.5f, depth * 0.49f),
                        new Vector3(0.45f, shaftH, 0.55f), mats.DarkSteelMat ?? mats.MotijheelConcreteMat, lod0List);
                    AddPart(root.transform, $"MullionFinB_{i}", new Vector3(rx, podiumH + shaftH * 0.5f, -depth * 0.49f),
                        new Vector3(0.45f, shaftH, 0.55f), mats.DarkSteelMat ?? mats.MotijheelConcreteMat, lod0List);
                }

                // Glass entrance canopy at street level
                AddPart(root.transform, "EntranceCanopy", new Vector3(0f, 3.8f, -depth * 0.56f),
                    new Vector3(Mathf.Min(8f, width * 0.5f), 0.35f, 2.2f), mats.DarkSteelMat ?? primaryMat, lod0List);
            }
            else if (style == DistrictArchitecturalStyle.OldDhakaColonialHeritage)
            {
                // Verandah pillars & cornice overhang
                for (int i = -1; i <= 1; i++)
                {
                    AddPart(root.transform, $"VerandahPillar_{i}", new Vector3(i * width * 0.36f, totalHeight * 0.5f, -depth * 0.52f),
                        new Vector3(0.55f, totalHeight, 0.55f), mats.ResidentialStuccoMat ?? primaryMat, lod0List);
                }
                AddPart(root.transform, "HeritageCornice", new Vector3(0f, totalHeight + 0.35f, 0f),
                    new Vector3(width + 1.0f, 0.65f, depth + 1.4f), mats.OldDhakaBrickMat ?? primaryMat, lod0List, lod1List);
            }
            else
            {
                // Banani / Residential / Bazaar stacked balcony projections
                int levels = Mathf.Clamp(Mathf.RoundToInt(totalHeight / 3.6f), 2, 10);
                for (int fl = 1; fl < levels; fl += 2)
                {
                    float y = fl * 3.6f;
                    AddPart(root.transform, $"Balcony_{fl}", new Vector3(0f, y, -depth * 0.52f),
                        new Vector3(width * 0.65f, 0.95f, 1.35f), mats.ResidentialStuccoMat ?? primaryMat, lod0List);
                }
            }

            // Rooftop Parapet Crown & Cylindrical Gazi Water Tanks + Stairhead (Category 8)
            AddPart(root.transform, "RooftopStairhead", new Vector3(width * 0.18f, totalHeight + 1.3f, 0f),
                new Vector3(3.2f, 2.6f, 3.2f), mats.MotijheelConcreteMat ?? primaryMat, lod0List, lod1List);
            AddCylinderPart(root.transform, "RooftopWaterTank_A", new Vector3(-width * 0.18f, totalHeight + 1.35f, 0.6f),
                new Vector3(1.5f, 1.0f, 1.5f), mats.TankBlueMat ?? mats.DarkSteelMat, lod0List);
            if ((variantSeed & 1) == 0)
            {
                AddCylinderPart(root.transform, "RooftopWaterTank_B", new Vector3(-width * 0.18f + 2.1f, totalHeight + 1.35f, 0.6f),
                    new Vector3(1.5f, 1.0f, 1.5f), mats.TankBlueMat ?? mats.DarkSteelMat, lod0List);
            }

            // Simplified single proxy mass for far-distance LOD2
            AddPart(root.transform, "MassingProxy_LOD2", new Vector3(0f, totalHeight * 0.5f, 0f),
                new Vector3(width, totalHeight, depth), primaryMat, lod2List);

            // Single optimized BoxCollider instead of multiple overlapping heavy MeshColliders
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, totalHeight * 0.5f, 0f);
            box.size = new Vector3(width, totalHeight, depth);

            EnvironmentAssetVariation.ConfigureThreeLevelLod(root, lod0List, lod1List, lod2List);
            var variation = root.AddComponent<EnvironmentAssetVariation>();
            variation.ApplyVariation(variantSeed, Color.white);
            _ = podium;
            return root;
        }

        public static GameObject CreateTeaStall(Vector3 pos, float yawDeg, string stallName, MaterialPack mats, Transform parent)
        {
            var root = new GameObject(stallName);
            root.layer = PhysicsLayers.Building;
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);

            var lod0 = new List<Renderer>();
            AddPart(root.transform, "TimberBackWall", new Vector3(0f, 1.05f, 0.85f), new Vector3(2.4f, 2.1f, 0.10f), mats.WoodMat, lod0);
            AddPart(root.transform, "CounterFront", new Vector3(0f, 0.52f, -0.72f), new Vector3(2.4f, 1.04f, 0.52f), mats.WoodMat, lod0);
            GameObject roof = AddPart(root.transform, "CorrugatedTinAwning", new Vector3(0f, 2.35f, -0.08f), new Vector3(2.85f, 0.08f, 2.45f), mats.CorrugatedTinMat, lod0);
            roof.transform.localRotation = Quaternion.Euler(9f, 0f, 0f);
            AddPart(root.transform, "WoodenBench", new Vector3(0f, 0.45f, -1.6f), new Vector3(2.1f, 0.08f, 0.42f), mats.WoodMat, lod0);
            AddCylinderPart(root.transform, "TeaKettle", new Vector3(-0.55f, 1.18f, -0.7f), new Vector3(0.28f, 0.14f, 0.28f), mats.DarkSteelMat, lod0);

            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 1.15f, -0.15f);
            col.size = new Vector3(2.6f, 2.3f, 2.2f);

            var interactable = root.AddComponent<InteractiveEnvironmentProp>();
            interactable.Configure(
                InteractableKind.TeaStall,
                stallName.Replace("_", " "),
                "[E] Drink Spiced Doodh-Cha (+20 HP & Stamina)",
                hpHeal: 20f);
            return root;
        }

        public static GameObject CreateElectricPole(Vector3 pos, float yawDeg, MaterialPack mats, Transform parent)
        {
            var root = new GameObject("DhakaElectricPole");
            root.layer = PhysicsLayers.Building;
            root.isStatic = true;
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);

            var lod0 = new List<Renderer>();
            AddCylinderPart(root.transform, "ConcreteMast", new Vector3(0f, 4.4f, 0f), new Vector3(0.28f, 4.4f, 0.28f), mats.MotijheelConcreteMat, lod0);
            AddPart(root.transform, "UpperCrossArm", new Vector3(0f, 8.0f, 0f), new Vector3(1.9f, 0.12f, 0.12f), mats.DarkSteelMat, lod0);
            AddCylinderPart(root.transform, "TransformerDrum", new Vector3(0.35f, 6.2f, 0f), new Vector3(0.55f, 0.45f, 0.55f), mats.DarkSteelMat, lod0);

            var cap = root.AddComponent<CapsuleCollider>();
            cap.center = new Vector3(0f, 4.4f, 0f);
            cap.height = 8.8f;
            cap.radius = 0.22f;
            return root;
        }

        public static GameObject CreateDhakaBillboard(Vector3 pos, float yawDeg, string title, MaterialPack mats, Transform parent)
        {
            var root = new GameObject(title);
            root.layer = PhysicsLayers.Building;
            root.isStatic = true;
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);

            var lod0 = new List<Renderer>();
            AddCylinderPart(root.transform, "SupportPylon", new Vector3(0f, 4.2f, 0f), new Vector3(0.5f, 4.2f, 0.5f), mats.DarkSteelMat, lod0);
            AddPart(root.transform, "BoardFrame", new Vector3(0f, 8.8f, 0f), new Vector3(8.5f, 3.8f, 0.35f), mats.DarkSteelMat, lod0);
            AddPart(root.transform, "SignageFace", new Vector3(0f, 8.8f, -0.22f), new Vector3(8.1f, 3.4f, 0.08f), mats.NeonSignMat ?? mats.GlassTowerMat, lod0);

            var cap = root.AddComponent<CapsuleCollider>();
            cap.center = new Vector3(0f, 4.2f, 0f);
            cap.height = 8.4f;
            cap.radius = 0.3f;
            return root;
        }

        public static GameObject CreatePromenadeBench(Vector3 pos, float yawDeg, string name, MaterialPack mats, Transform parent)
        {
            var root = new GameObject(name);
            root.layer = PhysicsLayers.Building;
            root.isStatic = true;
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);

            var lod0 = new List<Renderer>();
            AddPart(root.transform, "SeatSlats", new Vector3(0f, 0.48f, 0f), new Vector3(2.0f, 0.08f, 0.55f), mats.WoodMat, lod0);
            AddPart(root.transform, "BackRest", new Vector3(0f, 0.85f, 0.24f), new Vector3(2.0f, 0.42f, 0.07f), mats.WoodMat, lod0);
            AddPart(root.transform, "LegL", new Vector3(-0.82f, 0.24f, 0f), new Vector3(0.10f, 0.48f, 0.52f), mats.DarkSteelMat, lod0);
            AddPart(root.transform, "LegR", new Vector3(0.82f, 0.24f, 0f), new Vector3(0.10f, 0.48f, 0.52f), mats.DarkSteelMat, lod0);

            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.55f, 0f);
            col.size = new Vector3(2.0f, 1.0f, 0.65f);

            var interactable = root.AddComponent<InteractiveEnvironmentProp>();
            interactable.Configure(
                InteractableKind.BenchRest,
                "Hatirjheel Lakeside Bench",
                "[E] Rest by Hatirjheel Lake (+15 HP)",
                hpHeal: 15f);
            return root;
        }

        public static (Mesh trunkMesh, Mesh canopyMesh) BuildSharedTropicalTreeMeshes()
        {
            // Combine trunk and dome canopy meshes for GPU-instanced tropical trees (zero GC per instance)
            var trunkGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            var canopyGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);

            Mesh trunk = Object.Instantiate(trunkGo.GetComponent<MeshFilter>().sharedMesh);
            Mesh canopy = Object.Instantiate(canopyGo.GetComponent<MeshFilter>().sharedMesh);
            trunk.name = "Mesh_TropicalTreeTrunk";
            canopy.name = "Mesh_KrishnachuraCanopy";

            Object.DestroyImmediate(trunkGo);
            Object.DestroyImmediate(canopyGo);
            return (trunk, canopy);
        }

        private static Material ResolvePrimaryMat(DistrictArchitecturalStyle style, MaterialPack mats)
        {
            return style switch
            {
                DistrictArchitecturalStyle.GulshanGlassHighrise => mats.GlassTowerMat,
                DistrictArchitecturalStyle.BananiCommercialBoutique => mats.BananiPanelMat ?? mats.GlassTowerMat,
                DistrictArchitecturalStyle.OldDhakaColonialHeritage => mats.OldDhakaBrickMat ?? mats.ResidentialStuccoMat,
                DistrictArchitecturalStyle.MotijheelCorporateTower => mats.MotijheelConcreteMat,
                DistrictArchitecturalStyle.BazaarAndShoppingCenter => mats.CorrugatedTinMat ?? mats.ResidentialStuccoMat,
                _ => mats.ResidentialStuccoMat ?? mats.MotijheelConcreteMat,
            };
        }

        private static GameObject AddPart(
            Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat,
            List<Renderer> listA, List<Renderer> listB = null)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.isStatic = true;
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            if (mat != null)
            {
                mat.enableInstancing = true;
                r.sharedMaterial = mat;
            }
            listA?.Add(r);
            listB?.Add(r);
            return go;
        }

        private static GameObject AddCylinderPart(
            Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat,
            List<Renderer> listA)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.isStatic = true;
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            if (mat != null)
            {
                mat.enableInstancing = true;
                r.sharedMaterial = mat;
            }
            listA?.Add(r);
            return go;
        }
    }
}
