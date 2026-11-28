using UnityEngine;

namespace DhakaGta.TrafficAI
{
    public enum PedestrianWardrobeArchetype
    {
        PanjabiAndPajama = 0,
        LungiAndGamcha = 1,
        SareeTraditional = 2,
        SalwarKameezAndDupatta = 3,
        DhakaOfficeFormal = 4,
        StreetVendorChaiwala = 5,
        DmpPoliceOfficer = 6
    }

    public sealed class PedestrianRigJoints : MonoBehaviour
    {
        public Transform Hips;
        public Transform Spine;
        public Transform Head;
        public Transform LeftUpperLeg;
        public Transform RightUpperLeg;
        public Transform LeftLowerLeg;
        public Transform RightLowerLeg;
        public Transform LeftUpperArm;
        public Transform RightUpperArm;
        public Transform LeftForearm;
        public Transform RightForearm;
        public Transform VendorBasketProp;
    }

    /// <summary>
    /// Procedural PBR wardrobe & articulated joint builder for authentic Bangladeshi pedestrian NPCs
    /// and Dhaka Metropolitan Police officers (Panjabi, Lungi/Gamcha, Saree drape, Salwar-Kameez, DMP Khaki/Blue).
    /// Supports both Humanoid Animator controllers and lightweight multi-joint LOD articulation.
    /// </summary>
    public static class BangladeshiPedestrianVisualBuilder
    {
        public static PedestrianRigJoints BuildArticulatedBangladeshiNpcVisual(
            Transform parentRoot,
            PedestrianWardrobeArchetype archetype,
            int colorVariantSeed,
            RuntimeAnimatorController optionalHumanoidController = null)
        {
            var rng = new System.Random(colorVariantSeed);
            Color skin = PickSkinTone(rng);
            Color hair = new Color(0.06f, 0.055f, 0.05f);
            Color topColor = PickTopColor(archetype, rng);
            Color bottomColor = PickBottomColor(archetype, rng);
            Color accentColor = PickAccentColor(archetype, rng);

            var joints = parentRoot.gameObject.AddComponent<PedestrianRigJoints>();

            float heightScale = (archetype == PedestrianWardrobeArchetype.SareeTraditional ||
                                 archetype == PedestrianWardrobeArchetype.SalwarKameezAndDupatta)
                ? 0.94f : 1.0f;

            Transform modelRoot = new GameObject($"Visual_{archetype}").transform;
            modelRoot.SetParent(parentRoot, false);
            modelRoot.localScale = Vector3.one * heightScale;

            var hipsGo = new GameObject("Hips");
            hipsGo.transform.SetParent(modelRoot, false);
            hipsGo.transform.localPosition = new Vector3(0f, 0.92f, 0f);
            joints.Hips = hipsGo.transform;

            var spineGo = new GameObject("Spine");
            spineGo.transform.SetParent(joints.Hips, false);
            spineGo.transform.localPosition = new Vector3(0f, 0.14f, 0f);
            joints.Spine = spineGo.transform;

            // Torso Jacket / Panjabi / Blouse / DMP Tunic
            bool isFeminine = archetype == PedestrianWardrobeArchetype.SareeTraditional ||
                              archetype == PedestrianWardrobeArchetype.SalwarKameezAndDupatta;
            float chestWidth = isFeminine ? 0.30f : 0.36f;

            CreateSegmentPart(joints.Spine, "ChestGarment", new Vector3(0f, 0.16f, 0f), new Vector3(chestWidth, 0.42f, 0.21f), topColor, 0.32f);

            // Signature Dhaka cultural garment overlays
            if (archetype == PedestrianWardrobeArchetype.PanjabiAndPajama ||
                archetype == PedestrianWardrobeArchetype.SalwarKameezAndDupatta)
            {
                // Flowing lower Panjabi / Kameez hem tunic over trousers
                CreateSegmentPart(joints.Hips, "TunicSkirtHem", new Vector3(0f, -0.09f, 0f), new Vector3(chestWidth * 1.04f, 0.32f, 0.23f), topColor, 0.28f);
            }

            if (archetype == PedestrianWardrobeArchetype.LungiAndGamcha)
            {
                // Wrapped cylindrical Lungi + red-white checkered Gamcha shoulder scarf
                CreateSegmentPart(joints.Hips, "LungiWrap", new Vector3(0f, -0.34f, 0f), new Vector3(0.32f, 0.68f, 0.26f), bottomColor, 0.22f);
                CreateSegmentPart(joints.Spine, "GamchaShoulderScarf", new Vector3(0.11f, 0.24f, 0.02f), new Vector3(0.10f, 0.42f, 0.24f), accentColor, 0.25f);
            }
            else if (archetype == PedestrianWardrobeArchetype.SareeTraditional)
            {
                // Flowing Saree Pleat Skirt + Diagonal Pallu Anchor over left shoulder
                CreateSegmentPart(joints.Hips, "SareeDrape", new Vector3(0f, -0.36f, 0f), new Vector3(0.34f, 0.74f, 0.26f), accentColor, 0.40f);
                var sash = CreateSegmentPart(joints.Spine, "SareePallu", new Vector3(-0.04f, 0.21f, 0.04f), new Vector3(0.13f, 0.48f, 0.23f), accentColor, 0.42f);
                sash.localRotation = Quaternion.Euler(0f, 0f, -24f);
            }
            else if (archetype == PedestrianWardrobeArchetype.SalwarKameezAndDupatta)
            {
                var dup = CreateSegmentPart(joints.Spine, "DupattaScarf", new Vector3(0.05f, 0.22f, 0.05f), new Vector3(0.11f, 0.44f, 0.22f), accentColor, 0.32f);
                dup.localRotation = Quaternion.Euler(0f, 0f, 22f);
            }
            else if (archetype == PedestrianWardrobeArchetype.DmpPoliceOfficer)
            {
                CreateSegmentPart(joints.Hips, "PoliceUtilityBelt", new Vector3(0f, 0.02f, 0f), new Vector3(0.38f, 0.07f, 0.24f), new Color(0.08f, 0.08f, 0.09f), 0.55f);
                CreateSegmentPart(joints.Spine, "PoliceBadgeChest", new Vector3(-0.10f, 0.24f, 0.115f), new Vector3(0.05f, 0.06f, 0.02f), accentColor, 0.85f);
            }
            else if (archetype == PedestrianWardrobeArchetype.StreetVendorChaiwala)
            {
                var basket = CreateSegmentPart(joints.Spine, "StreetVendorTray", new Vector3(0f, 0.04f, 0.28f), new Vector3(0.36f, 0.08f, 0.26f), new Color(0.45f, 0.30f, 0.15f), 0.35f);
                joints.VendorBasketProp = basket;
            }

            // Neck & Sculpted Head + Cultural Headwear / Hair
            var headGo = new GameObject("Head");
            headGo.transform.SetParent(joints.Spine, false);
            headGo.transform.localPosition = new Vector3(0f, 0.44f, 0f);
            joints.Head = headGo.transform;

            CreateSegmentPart(joints.Head, "NeckCol", new Vector3(0f, -0.02f, 0f), new Vector3(0.10f, 0.09f, 0.10f), skin, 0.45f);
            CreateSegmentPart(joints.Head, "Cranium", new Vector3(0f, 0.09f, 0.01f), new Vector3(0.17f, 0.21f, 0.19f), skin, 0.45f);
            CreateSegmentPart(joints.Head, "NoseBridge", new Vector3(0f, 0.08f, 0.105f), new Vector3(0.032f, 0.055f, 0.035f), skin, 0.42f);
            CreateSegmentPart(joints.Head, "HairCrown", new Vector3(0f, 0.16f, -0.01f), new Vector3(0.18f, 0.10f, 0.20f), hair, 0.50f);

            if (archetype == PedestrianWardrobeArchetype.PanjabiAndPajama)
            {
                CreateSegmentPart(joints.Head, "TupiPrayerCap", new Vector3(0f, 0.195f, 0f), new Vector3(0.17f, 0.06f, 0.17f), Color.white, 0.25f);
            }
            else if (archetype == PedestrianWardrobeArchetype.DmpPoliceOfficer)
            {
                CreateSegmentPart(joints.Head, "DmpCapCrown", new Vector3(0f, 0.195f, 0f), new Vector3(0.20f, 0.065f, 0.21f), new Color(0.10f, 0.22f, 0.38f), 0.45f);
                CreateSegmentPart(joints.Head, "DmpCapVisor", new Vector3(0f, 0.165f, 0.11f), new Vector3(0.15f, 0.02f, 0.08f), Color.black, 0.72f);
            }

            // Articulated Arms
            float shX = isFeminine ? 0.20f : 0.23f;
            (joints.LeftUpperArm, joints.LeftForearm) = CreateArticulatedArm(joints.Spine, new Vector3(-shX, 0.33f, 0f), topColor, skin);
            (joints.RightUpperArm, joints.RightForearm) = CreateArticulatedArm(joints.Spine, new Vector3(shX, 0.33f, 0f), topColor, skin);

            // Articulated Legs
            float legX = 0.095f;
            Color legGarment = archetype == PedestrianWardrobeArchetype.LungiAndGamcha || archetype == PedestrianWardrobeArchetype.SareeTraditional
                ? skin * 0.9f
                : bottomColor;
            (joints.LeftUpperLeg, joints.LeftLowerLeg) = CreateArticulatedLeg(joints.Hips, new Vector3(-legX, -0.02f, 0f), legGarment);
            (joints.RightUpperLeg, joints.RightLowerLeg) = CreateArticulatedLeg(joints.Hips, new Vector3(legX, -0.02f, 0f), legGarment);

            if (optionalHumanoidController != null)
            {
                Animator anim = modelRoot.gameObject.AddComponent<Animator>();
                anim.runtimeAnimatorController = optionalHumanoidController;
                anim.applyRootMotion = false;
            }

            return joints;
        }

        private static (Transform upper, Transform lower) CreateArticulatedArm(Transform spine, Vector3 pivotPos, Color sleeveCol, Color skinCol)
        {
            var ua = new GameObject("UpperArm").transform;
            ua.SetParent(spine, false);
            ua.localPosition = pivotPos;
            CreateSegmentPart(ua, "Sleeve", new Vector3(0f, -0.14f, 0f), new Vector3(0.095f, 0.28f, 0.095f), sleeveCol, 0.30f);

            var fa = new GameObject("Forearm").transform;
            fa.SetParent(ua, false);
            fa.localPosition = new Vector3(0f, -0.27f, 0f);
            CreateSegmentPart(fa, "SkinArm", new Vector3(0f, -0.13f, 0f), new Vector3(0.075f, 0.26f, 0.075f), skinCol, 0.44f);
            return (ua, fa);
        }

        private static (Transform thigh, Transform shin) CreateArticulatedLeg(Transform hips, Vector3 pivotPos, Color legColor)
        {
            var thigh = new GameObject("UpperLeg").transform;
            thigh.SetParent(hips, false);
            thigh.localPosition = pivotPos;
            CreateSegmentPart(thigh, "Thigh", new Vector3(0f, -0.22f, 0f), new Vector3(0.125f, 0.44f, 0.13f), legColor, 0.25f);

            var shin = new GameObject("LowerLeg").transform;
            shin.SetParent(thigh, false);
            shin.localPosition = new Vector3(0f, -0.44f, 0f);
            CreateSegmentPart(shin, "Shin", new Vector3(0f, -0.20f, 0f), new Vector3(0.098f, 0.40f, 0.105f), legColor, 0.25f);
            CreateSegmentPart(shin, "Shoe", new Vector3(0f, -0.42f, 0.035f), new Vector3(0.095f, 0.075f, 0.21f), new Color(0.11f, 0.09f, 0.08f), 0.48f);
            return (thigh, shin);
        }

        private static Transform CreateSegmentPart(Transform parent, string name, Vector3 localPos, Vector3 scale, Color color, float smoothness)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                Shader sh = Shader.Find("HDRP/Lit") ?? Shader.Find("Standard");
                var mat = new Material(sh);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                else mat.color = color;
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
                mat.enableInstancing = true;
                mr.sharedMaterial = mat;
            }
            return go.transform;
        }

        private static Color PickSkinTone(System.Random r)
        {
            Color[] tones = {
                new Color(0.76f, 0.55f, 0.41f),
                new Color(0.68f, 0.47f, 0.34f),
                new Color(0.83f, 0.63f, 0.49f)
            };
            return tones[r.Next(tones.Length)];
        }

        private static Color PickTopColor(PedestrianWardrobeArchetype a, System.Random r)
        {
            if (a == PedestrianWardrobeArchetype.DmpPoliceOfficer) return new Color(0.14f, 0.30f, 0.46f);
            if (a == PedestrianWardrobeArchetype.PanjabiAndPajama)
            {
                Color[] c = { new Color(0.92f, 0.92f, 0.90f), new Color(0.76f, 0.85f, 0.92f), new Color(0.88f, 0.82f, 0.68f) };
                return c[r.Next(c.Length)];
            }
            if (a == PedestrianWardrobeArchetype.SareeTraditional)
            {
                Color[] c = { new Color(0.65f, 0.14f, 0.22f), new Color(0.08f, 0.42f, 0.26f), new Color(0.82f, 0.56f, 0.12f) };
                return c[r.Next(c.Length)];
            }
            Color[] generic = { new Color(0.86f, 0.88f, 0.91f), new Color(0.24f, 0.42f, 0.64f), new Color(0.18f, 0.50f, 0.38f), new Color(0.55f, 0.22f, 0.26f) };
            return generic[r.Next(generic.Length)];
        }

        private static Color PickBottomColor(PedestrianWardrobeArchetype a, System.Random r)
        {
            if (a == PedestrianWardrobeArchetype.DmpPoliceOfficer) return new Color(0.10f, 0.14f, 0.20f);
            if (a == PedestrianWardrobeArchetype.LungiAndGamcha)
            {
                Color[] lungis = { new Color(0.18f, 0.26f, 0.48f), new Color(0.42f, 0.18f, 0.18f), new Color(0.16f, 0.36f, 0.24f) };
                return lungis[r.Next(lungis.Length)];
            }
            return r.NextDouble() < 0.5 ? new Color(0.14f, 0.16f, 0.20f) : new Color(0.24f, 0.22f, 0.20f);
        }

        private static Color PickAccentColor(PedestrianWardrobeArchetype a, System.Random r)
        {
            if (a == PedestrianWardrobeArchetype.LungiAndGamcha) return new Color(0.78f, 0.16f, 0.18f);
            if (a == PedestrianWardrobeArchetype.DmpPoliceOfficer) return new Color(0.88f, 0.72f, 0.18f);
            Color[] vib = { new Color(0.74f, 0.14f, 0.24f), new Color(0.08f, 0.48f, 0.34f), new Color(0.85f, 0.62f, 0.12f), new Color(0.16f, 0.42f, 0.65f) };
            return vib[r.Next(vib.Length)];
        }
    }
}
