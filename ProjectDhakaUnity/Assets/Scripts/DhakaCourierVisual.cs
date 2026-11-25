using UnityEngine;

namespace ProjectDhaka
{
    /// <summary>
    /// Seven-layer procedural animation rig for the Project Dhaka courier character:
    /// squash-and-stretch, acceleration lean, limb swing, vertical step bob,
    /// head counter-lead, continuous idle breathing, and pickup/hit scale pops.
    /// </summary>
    public class DhakaCourierVisual : MonoBehaviour
    {
        [SerializeField] Transform rigRoot;
        [SerializeField] Transform torsoPivot;
        [SerializeField] Transform headPivot;
        [SerializeField] Transform leftArmPivot;
        [SerializeField] Transform rightArmPivot;
        [SerializeField] Transform leftLegPivot;
        [SerializeField] Transform rightLegPivot;
        [SerializeField] Renderer[] tintedRenderers;

        public Transform RigRoot => rigRoot;
        public Transform TorsoPivot => torsoPivot;
        public Transform HeadPivot => headPivot;
        public Transform LeftArmPivot => leftArmPivot;
        public Transform RightArmPivot => rightArmPivot;
        public Transform LeftLegPivot => leftLegPivot;
        public Transform RightLegPivot => rightLegPivot;

        float stridePhase;
        float pickupPopTimer;
        float hitWobbleTimer;
        float squashVelocity;
        float squashFactor = 1f;
        float turnLean;
        float speedPitch;
        bool isInvulnerable;

        public void BindParts(
            Transform rig,
            Transform torso,
            Transform head,
            Transform leftArm,
            Transform rightArm,
            Transform leftLeg,
            Transform rightLeg)
        {
            rigRoot = rig;
            torsoPivot = torso;
            headPivot = head;
            leftArmPivot = leftArm;
            rightArmPivot = rightArm;
            leftLegPivot = leftLeg;
            rightLegPivot = rightLeg;
            tintedRenderers = GetComponentsInChildren<Renderer>(true);
        }

        public void TriggerLandingSquash()
        {
            squashFactor = 0.78f;
            squashVelocity = 3.2f;
        }

        public void TriggerPickupPop()
        {
            pickupPopTimer = 0.34f;
        }

        public void TriggerHitWobble()
        {
            hitWobbleTimer = 0.45f;
        }

        public void SetInvulnerableVisual(bool active)
        {
            isInvulnerable = active;
            if (!active && tintedRenderers != null)
            {
                for (int i = 0; i < tintedRenderers.Length; i++)
                {
                    if (tintedRenderers[i] != null) tintedRenderers[i].enabled = true;
                }
            }
        }

        public void UpdateMotion(Vector3 planarVelocity, float maxSpeed, float yawRate, bool grounded, float dt)
        {
            if (rigRoot == null || torsoPivot == null) return;

            float speed = new Vector2(planarVelocity.x, planarVelocity.z).magnitude;
            float speedNorm = maxSpeed > 0.01f ? Mathf.Clamp01(speed / maxSpeed) : 0f;

            // Layer 1: Continuous idle breathing (alive even at rest)
            float breathe = Mathf.Sin(Time.time * 2.6f);
            float idleChestScale = 1f + breathe * 0.028f;
            float idleHeadBob = breathe * 0.018f;

            // Layer 2: Stride cycle + vertical step bob
            if (grounded && speedNorm > 0.04f)
            {
                stridePhase += dt * Mathf.Lerp(5.5f, 13.5f, speedNorm);
            }
            else if (speedNorm <= 0.04f)
            {
                stridePhase += dt * 1.4f;
            }

            float swingSin = Mathf.Sin(stridePhase);
            float bobWave = Mathf.Abs(swingSin) * 0.095f * speedNorm;
            float hipSway = swingSin * 3.8f * speedNorm;

            // Layer 3: Acceleration lean and turn bank
            float targetPitch = speedNorm * 16f;
            float targetBank = Mathf.Clamp(-yawRate * 0.08f, -14f, 14f);
            speedPitch = Mathf.Lerp(speedPitch, targetPitch, 1f - Mathf.Exp(-10f * dt));
            turnLean = Mathf.Lerp(turnLean, targetBank, 1f - Mathf.Exp(-10f * dt));

            // Layer 4: Squash & stretch spring + pickup pop
            squashVelocity += (1f - squashFactor) * 42f * dt;
            squashVelocity *= Mathf.Exp(-9f * dt);
            squashFactor = Mathf.Clamp(squashFactor + squashVelocity * dt, 0.72f, 1.28f);

            float airStretch = grounded ? 1f : 1.09f;
            float popBoost = 1f;
            if (pickupPopTimer > 0f)
            {
                pickupPopTimer = Mathf.Max(0f, pickupPopTimer - Time.unscaledDeltaTime);
                float u = 1f - (pickupPopTimer / 0.34f);
                popBoost = 1f + 0.24f * Ease.Punch(u);
            }

            float wobbleRoll = 0f;
            if (hitWobbleTimer > 0f)
            {
                hitWobbleTimer = Mathf.Max(0f, hitWobbleTimer - Time.unscaledDeltaTime);
                wobbleRoll = Mathf.Sin(hitWobbleTimer * 44f) * 14f * (hitWobbleTimer / 0.45f);
            }

            float sy = squashFactor * airStretch * popBoost;
            float sxz = (1f / Mathf.Sqrt(Mathf.Max(0.5f, sy))) * popBoost;
            rigRoot.localScale = new Vector3(sxz, sy, sxz);

            torsoPivot.localPosition = new Vector3(0f, 0.88f + bobWave + idleHeadBob * 0.35f, 0f);
            torsoPivot.localScale = new Vector3(1f, idleChestScale, 1f);
            torsoPivot.localRotation = Quaternion.Euler(speedPitch, hipSway, turnLean + wobbleRoll);

            // Layer 5: Head counter-lead and subtle scan
            if (headPivot != null)
            {
                float headPitch = -speedPitch * 0.45f + breathe * 1.8f;
                float headYaw = -hipSway * 0.65f + Mathf.Clamp(yawRate * 0.05f, -15f, 15f);
                headPivot.localPosition = new Vector3(0f, 0.48f + idleHeadBob, 0f);
                headPivot.localRotation = Quaternion.Euler(headPitch, headYaw, -turnLean * 0.35f);
            }

            // Layer 6: Counter-swinging arms & legs
            float legSwing = swingSin * 42f * speedNorm;
            float armSwing = swingSin * 36f * speedNorm;
            float idleArmSway = Mathf.Sin(Time.time * 2.1f) * 4.5f * (1f - speedNorm);

            if (leftLegPivot != null) leftLegPivot.localRotation = Quaternion.Euler(legSwing, 0f, 0f);
            if (rightLegPivot != null) rightLegPivot.localRotation = Quaternion.Euler(-legSwing, 0f, 0f);
            if (leftArmPivot != null) leftArmPivot.localRotation = Quaternion.Euler(-armSwing + idleArmSway, 0f, 7f);
            if (rightArmPivot != null) rightArmPivot.localRotation = Quaternion.Euler(armSwing - idleArmSway, 0f, -7f);

            // Layer 7: Invulnerability blink
            if (isInvulnerable && tintedRenderers != null)
            {
                bool show = Mathf.FloorToInt(Time.unscaledTime * 16f) % 2 == 0;
                for (int i = 0; i < tintedRenderers.Length; i++)
                {
                    if (tintedRenderers[i] != null) tintedRenderers[i].enabled = show;
                }
            }
        }
    }
}
