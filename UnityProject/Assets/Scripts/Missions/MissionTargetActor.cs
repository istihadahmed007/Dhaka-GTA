using System;
using DhakaGta.Combat;
using DhakaGta.Core;
using DhakaGta.TrafficAI;
using UnityEngine;

namespace DhakaGta.Missions
{
    /// <summary>
    /// Hostile Padma Syndicate enforcer or rival NPC spawned during EliminateTargets mission steps.
    /// Integrates HealthComponent (IHealthTarget) and simple combat pursuit toward active protagonist.
    /// </summary>
    [RequireComponent(typeof(HealthComponent))]
    public sealed class MissionTargetActor : MonoBehaviour
    {
        [SerializeField] private float maxHealth = 90f;
        [SerializeField] private float moveSpeedMps = 3.2f;
        [SerializeField] private float attackRangeMeters = 14f;
        [SerializeField] private float attackCooldownSeconds = 1.6f;
        [SerializeField] private float damagePerAttack = 8f;

        private Transform _playerTarget;
        private HealthComponent _health;
        private float _attackTimer;
        private bool _eliminated;

        public event Action<MissionTargetActor> OnEliminated;
        public bool IsEliminated => _eliminated;

        public void InitializeTarget(Transform playerTransform, float hp = 90f, int seed = 101)
        {
            _playerTarget = playerTransform;
            maxHealth = Mathf.Max(20f, hp);
            _eliminated = false;

            gameObject.layer = PhysicsLayers.Npc;
            _health = GetComponent<HealthComponent>() ?? gameObject.AddComponent<HealthComponent>();

            if (GetComponent<CapsuleCollider>() == null)
            {
                var col = gameObject.AddComponent<CapsuleCollider>();
                col.height = 1.8f;
                col.radius = 0.35f;
                col.center = new Vector3(0f, 0.9f, 0f);
            }

            if (GetComponent<PedestrianRigJoints>() == null)
            {
                BangladeshiPedestrianVisualBuilder.BuildArticulatedBangladeshiNpcVisual(
                    transform, PedestrianWardrobeArchetype.DhakaOfficeFormal, seed);
            }

            // Initialize health cleanly using runtime temp ScriptableObject cap when no profile is bound
            var tempProf = ScriptableObject.CreateInstance<DhakaGta.Data.ProtagonistProfileSO>();
            tempProf.maxHealth = maxHealth;
            _health.InitializeFromProfile(tempProf);
            Destroy(tempProf);

            _health.OnDied -= HandleDied;
            _health.OnDied += HandleDied;
        }

        private void OnDestroy()
        {
            if (_health != null)
            {
                _health.OnDied -= HandleDied;
            }
        }

        private void Update()
        {
            if (_eliminated || _playerTarget == null) return;

            Vector3 toPlayer = _playerTarget.position - transform.position;
            toPlayer.y = 0f;
            float dist = toPlayer.magnitude;
            if (dist < 0.01f) return;

            Vector3 dir = toPlayer / dist;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir, Vector3.up), 360f * Time.deltaTime);

            if (dist > 4.5f && dist < 48f)
            {
                transform.position += dir * (moveSpeedMps * Time.deltaTime);
            }

            _attackTimer -= Time.deltaTime;
            if (dist <= attackRangeMeters && _attackTimer <= 0f)
            {
                _attackTimer = attackCooldownSeconds;
                var playerHp = _playerTarget.GetComponent<HealthComponent>();
                if (playerHp != null && playerHp.IsAlive)
                {
                    playerHp.ApplyDamage(damagePerAttack, _playerTarget.position + Vector3.up);
                    CityDangerBus.RaiseDangerEvent(CityDangerKind.Gunshot, transform.position, 28f, 0.7f, gameObject, escalateWanted: false);
                }
            }
        }

        public void ForceDefeatForTest()
        {
            _health?.ApplyDamage(maxHealth * 2f, transform.position);
        }

        private void HandleDied()
        {
            if (_eliminated) return;
            _eliminated = true;
            OnEliminated?.Invoke(this);
            Destroy(gameObject, 0.25f);
        }
    }
}
