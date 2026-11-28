using System.Collections.Generic;
using DhakaGta.Core;
using UnityEngine;

namespace DhakaGta.Character
{
    /// <summary>
    /// Handles water immersion/swimming detection (Hatirjheel & Buriganga canals),
    /// contextual world interaction cues ([E] Tea Stall / Bench / Vehicle),
    /// and protagonist dialogue/street gestures ([G]).
    /// </summary>
    public sealed class CharacterSwimAndDialogueController : MonoBehaviour
    {
        [Header("Water & Swimming")]
        [SerializeField] private float swimEnterSubmersionMeters = 1.12f;
        [SerializeField] private float swimExitSubmersionMeters = 0.85f;
        [SerializeField] private float swimSpeedMetersPerSec = 3.25f;
        [SerializeField] private float swimSprintSpeedMetersPerSec = 4.55f;
        [SerializeField] private float waterBuoyancyStiffness = 9.5f;

        [Header("World Interaction & Dialogue")]
        [SerializeField] private float interactionScanRadius = 2.6f;

        private readonly Collider[] _waterOverlapBuffer = new Collider[8];
        private readonly Collider[] _interactOverlapBuffer = new Collider[16];
        private readonly HashSet<Collider> _enteredWaterTriggers = new HashSet<Collider>();

        private float _activeWaterSurfaceY = float.NegativeInfinity;
        private int _gestureCursor;
        private string _protagonistId = "tariq";

        public bool IsSwimming { get; private set; }
        public float SwimSubmersionDepth { get; private set; }
        public float WaterSurfaceY => _activeWaterSurfaceY;
        public float SwimSpeed => swimSpeedMetersPerSec;
        public float SwimSprintSpeed => swimSprintSpeedMetersPerSec;
        public CharacterGestureKind LastTriggeredGesture { get; private set; }

        public void SetProtagonistId(string protagonistId)
        {
            if (!string.IsNullOrWhiteSpace(protagonistId))
            {
                _protagonistId = protagonistId;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other != null && other.gameObject.layer == PhysicsLayers.Water)
            {
                _enteredWaterTriggers.Add(other);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other != null)
            {
                _enteredWaterTriggers.Remove(other);
            }
        }

        /// <summary>
        /// Evaluates current submersion against PhysicsLayers.Water colliders and toggles swimming state cleanly.
        /// Returns desired vertical velocity when swimming so grounded/gravity movement yields cleanly to buoyancy.
        /// </summary>
        public bool EvaluateWaterState(Vector3 rootPosition, float currentVerticalVel, float deltaTime, out float outBuoyantVerticalVel)
        {
            float surfaceY = SampleWaterSurfaceHeight(rootPosition);
            _activeWaterSurfaceY = surfaceY;

            if (float.IsNegativeInfinity(surfaceY))
            {
                SwimSubmersionDepth = 0f;
                SetSwimming(false);
                outBuoyantVerticalVel = currentVerticalVel;
                return false;
            }

            SwimSubmersionDepth = surfaceY - rootPosition.y;
            if (!IsSwimming && SwimSubmersionDepth >= swimEnterSubmersionMeters)
            {
                SetSwimming(true);
            }
            else if (IsSwimming && SwimSubmersionDepth <= swimExitSubmersionMeters)
            {
                SetSwimming(false);
            }

            if (IsSwimming)
            {
                // Keep torso treading cleanly near chest depth (~1.28m below water surface)
                float targetRootY = surfaceY - 1.28f;
                float errorY = targetRootY - rootPosition.y;
                outBuoyantVerticalVel = Mathf.Clamp(errorY * waterBuoyancyStiffness, -2.5f, 3.2f);
                return true;
            }

            outBuoyantVerticalVel = currentVerticalVel;
            return false;
        }

        public CharacterGestureKind TriggerNextDialogueGesture(MotionMatchingController matcher)
        {
            CharacterGestureKind[] cycle =
            {
                CharacterGestureKind.GreetingWave,
                CharacterGestureKind.PersuadeHandGesture,
                CharacterGestureKind.PointDirection,
                CharacterGestureKind.RadioCheck,
                CharacterGestureKind.InspectItem,
                CharacterGestureKind.FrustratedShrug
            };

            CharacterGestureKind chosen = cycle[_gestureCursor % cycle.Length];
            _gestureCursor++;
            LastTriggeredGesture = chosen;

            matcher?.TriggerDialogueGesture();
            CharacterAbilityEvents.PublishDialogueGesture(_protagonistId, chosen);
            return chosen;
        }

        public bool TryInteractNearest(CharacterIkAndFootLocker ikLocker, MotionMatchingController matcher)
        {
            int count = Physics.OverlapSphereNonAlloc(
                transform.position + Vector3.up * 0.9f,
                interactionScanRadius,
                _interactOverlapBuffer,
                ~0,
                QueryTriggerInteraction.Collide);

            IInteractable best = null;
            float bestDistSq = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider col = _interactOverlapBuffer[i];
                if (col == null || col.gameObject == gameObject)
                {
                    continue;
                }

                IInteractable interactable = col.GetComponentInParent<IInteractable>();
                if (interactable == null || !interactable.CanInteract(gameObject))
                {
                    continue;
                }

                float dSq = (interactable.WorldPosition - transform.position).sqrMagnitude;
                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    best = interactable;
                }
            }

            if (best == null)
            {
                return false;
            }

            Vector3 handTarget = best.WorldPosition;
            ikLocker?.TriggerInteractionReach(handTarget);
            matcher?.TriggerInteract();
            CharacterAbilityEvents.PublishInteractionReach(_protagonistId, handTarget);
            best.Interact(gameObject);
            return true;
        }

        private float SampleWaterSurfaceHeight(Vector3 rootPosition)
        {
            float maxSurfaceY = float.NegativeInfinity;

            foreach (Collider triggerCol in _enteredWaterTriggers)
            {
                if (triggerCol != null && triggerCol.enabled)
                {
                    maxSurfaceY = Mathf.Max(maxSurfaceY, triggerCol.bounds.max.y);
                }
            }

            int waterMask = 1 << PhysicsLayers.Water;
            int hits = Physics.OverlapSphereNonAlloc(
                rootPosition + Vector3.up * 1.1f,
                0.55f,
                _waterOverlapBuffer,
                waterMask,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < hits; i++)
            {
                Collider col = _waterOverlapBuffer[i];
                if (col != null)
                {
                    maxSurfaceY = Mathf.Max(maxSurfaceY, col.bounds.max.y);
                }
            }

            return maxSurfaceY;
        }

        private void SetSwimming(bool next)
        {
            if (IsSwimming == next)
            {
                return;
            }

            IsSwimming = next;
            CharacterAbilityEvents.PublishSwimmingState(_protagonistId, IsSwimming);
        }
    }
}
