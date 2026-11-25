using System.Collections;
using UnityEngine;
using UnityEngine.Pool;

namespace ProjectDhaka
{
    /// <summary>
    /// Object-pooled URP particle bursts (pickup sparks, milestone confetti, dust puffs)
    /// and a gentle monsoon drizzle curtain around the arena.
    /// </summary>
    public class VfxManager : MonoBehaviour
    {
        public static VfxManager Instance { get; private set; }

        public ParticleSystem LastBurstSystem { get; private set; }

        UnityEngine.Pool.ObjectPool<ParticleSystem> burstPool;
        Material particleMaterial;
        Material rainMaterial;
        ParticleSystem rainSystem;

        void Awake()
        {
            Instance = this;
            BuildMaterials();
            BuildPool();
            BuildMonsoonDrizzle();
        }

        void BuildMaterials()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            Sprite softDisc = Ease.CreateSoftDiscSprite(32);

            particleMaterial = new Material(shader)
            {
                name = "DhakaParticleMat"
            };
            if (particleMaterial.HasProperty("_BaseMap")) particleMaterial.SetTexture("_BaseMap", softDisc.texture);
            if (particleMaterial.HasProperty("_MainTex")) particleMaterial.SetTexture("_MainTex", softDisc.texture);
            if (particleMaterial.HasProperty("_BaseColor")) particleMaterial.SetColor("_BaseColor", Palette.TextLight);

            rainMaterial = new Material(shader)
            {
                name = "DhakaRainMat"
            };
            if (rainMaterial.HasProperty("_BaseMap")) rainMaterial.SetTexture("_BaseMap", softDisc.texture);
            if (rainMaterial.HasProperty("_MainTex")) rainMaterial.SetTexture("_MainTex", softDisc.texture);
            if (rainMaterial.HasProperty("_BaseColor")) rainMaterial.SetColor("_BaseColor", Palette.TextMuted);
        }

        void BuildPool()
        {
            burstPool = new UnityEngine.Pool.ObjectPool<ParticleSystem>(
                createFunc: CreateBurstSystem,
                actionOnGet: ps => ps.gameObject.SetActive(true),
                actionOnRelease: ps =>
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    ps.gameObject.SetActive(false);
                },
                actionOnDestroy: ps =>
                {
                    if (ps != null) Destroy(ps.gameObject);
                },
                collectionCheck: false,
                defaultCapacity: 8,
                maxSize: 20);
        }

        ParticleSystem CreateBurstSystem()
        {
            var go = new GameObject("PooledBurstVfx");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.65f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 7.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.28f);
            main.gravityModifier = 0.65f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = Palette.AccentBright;

            var emission = ps.emission;
            emission.enabled = false;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.35f;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(1f, 0f)));

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = particleMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            go.SetActive(false);
            return ps;
        }

        void BuildMonsoonDrizzle()
        {
            var go = new GameObject("MonsoonDrizzle");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(0f, 14f, 0f);
            rainSystem = go.AddComponent<ParticleSystem>();
            rainSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = rainSystem.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = 1.15f;
            main.startSpeed = 16f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.028f);
            Color drizzleColor = Palette.TextMuted;
            drizzleColor.a = 0.11f;
            main.startColor = drizzleColor;
            main.maxParticles = 220;

            var emission = rainSystem.emission;
            emission.rateOverTime = 140f;

            var shape = rainSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(44f, 0.5f, 44f);
            shape.rotation = new Vector3(95f, 0f, 6f);

            var renderer = rainSystem.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.05f;
            renderer.lengthScale = 5.5f;
            renderer.sharedMaterial = rainMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            rainSystem.Play();
        }

        public void SpawnPickupBurst(Vector3 position)
        {
            SpawnBurst(position + Vector3.up * 0.6f, Palette.AccentBright, 22, 6.2f, 0.26f);
        }

        public void SpawnHitBurst(Vector3 position)
        {
            SpawnBurst(position + Vector3.up * 0.6f, Palette.Danger, 20, 6.5f, 0.28f);
        }

        public void SpawnDustPuff(Vector3 position)
        {
            SpawnBurst(position + Vector3.up * 0.12f, Palette.CharacterTrim, 8, 2.5f, 0.18f);
        }

        public void SpawnMilestoneConfetti(Vector3 position)
        {
            SpawnBurst(position + Vector3.up * 1.4f, Palette.AccentBright, 32, 8.5f, 0.30f);
            SpawnBurst(position + Vector3.up * 1.4f, Palette.CharacterSecondary, 24, 7.5f, 0.26f);
        }

        public void SpawnBurst(Vector3 position, Color color, int count, float speed, float size)
        {
            if (burstPool == null) return;
            ParticleSystem ps = burstPool.Get();
            ps.transform.position = position;
            var main = ps.main;
            main.startColor = color;
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.55f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            ps.Emit(count);
            LastBurstSystem = ps;
            StartCoroutine(ReleaseAfterDelay(ps, 0.75f));
        }

        IEnumerator ReleaseAfterDelay(ParticleSystem ps, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            if (ps != null && ps.gameObject.activeSelf && burstPool != null)
            {
                burstPool.Release(ps);
            }
        }
    }
}
