using UnityEngine;

namespace DhakaGta.Character
{
    /// <summary>
    /// AI4Animation-inspired root trajectory predictor maintaining a past history ring buffer
    /// and rolling out future root positions and headings over +0.2s .. +1.0s horizons.
    /// </summary>
    public sealed class MotionTrajectoryPredictor : MonoBehaviour
    {
        public const int PastSampleCount = 6;
        public const int FutureSampleCount = 5;
        public static readonly float[] FutureTimeOffsets = { 0.2f, 0.4f, 0.6f, 0.8f, 1.0f };

        [Header("Exponential / Spring Smoothing")]
        [SerializeField, Range(2f, 24f)] private float velocityResponsiveness = 11f;
        [SerializeField, Range(2f, 24f)] private float facingResponsiveness = 9.5f;
        [SerializeField] private float historyIntervalSeconds = 0.12f;

        private readonly Vector3[] _pastPositions = new Vector3[PastSampleCount];
        private readonly Vector3[] _pastForwards = new Vector3[PastSampleCount];
        private readonly Vector3[] _futurePositions = new Vector3[FutureSampleCount];
        private readonly Vector3[] _futureForwards = new Vector3[FutureSampleCount];

        private Vector3 _smoothedWorldVelocity;
        private Vector3 _smoothedFacing;
        private float _historyTimer;
        private bool _initialized;

        public Vector3 CurrentWorldVelocity => _smoothedWorldVelocity;
        public Vector3 CurrentLocalVelocity { get; private set; }
        public float CurrentHorizontalSpeed => new Vector2(_smoothedWorldVelocity.x, _smoothedWorldVelocity.z).magnitude;
        public float PredictedTurnDegrees04 { get; private set; }

        public Vector3 GetFuturePosition(int index) =>
            index >= 0 && index < FutureSampleCount ? _futurePositions[index] : transform.position;

        public Vector3 GetFutureForward(int index) =>
            index >= 0 && index < FutureSampleCount ? _futureForwards[index] : transform.forward;

        public Vector3 GetPastPosition(int index) =>
            index >= 0 && index < PastSampleCount ? _pastPositions[index] : transform.position;

        private void Awake()
        {
            Initialize(transform.position, transform.forward);
        }

        public void Initialize(Vector3 worldPos, Vector3 worldForward)
        {
            Vector3 fwd = new Vector3(worldForward.x, 0f, worldForward.z);
            if (fwd.sqrMagnitude < 0.0001f)
            {
                fwd = Vector3.forward;
            }
            fwd.Normalize();

            _smoothedWorldVelocity = Vector3.zero;
            _smoothedFacing = fwd;
            CurrentLocalVelocity = Vector3.zero;
            PredictedTurnDegrees04 = 0f;
            _historyTimer = 0f;

            for (int i = 0; i < PastSampleCount; i++)
            {
                _pastPositions[i] = worldPos;
                _pastForwards[i] = fwd;
            }

            for (int i = 0; i < FutureSampleCount; i++)
            {
                _futurePositions[i] = worldPos;
                _futureForwards[i] = fwd;
            }

            _initialized = true;
        }

        /// <summary>
        /// Advances history ring buffer and rolls out predicted future positions & headings.
        /// Can be driven directly from PlayMode frames or deterministic EditMode steps.
        /// </summary>
        public void StepPrediction(
            Vector3 currentPosition,
            Vector3 currentForward,
            Vector3 desiredWorldVelocity,
            Vector3 desiredFacingDirection,
            float deltaTime)
        {
            if (!_initialized)
            {
                Initialize(currentPosition, currentForward);
            }

            float dt = Mathf.Max(0.0001f, deltaTime);
            float velAlpha = 1f - Mathf.Exp(-velocityResponsiveness * dt);
            float faceAlpha = 1f - Mathf.Exp(-facingResponsiveness * dt);

            _smoothedWorldVelocity = Vector3.Lerp(_smoothedWorldVelocity, desiredWorldVelocity, velAlpha);

            Vector3 flatDesiredFace = new Vector3(desiredFacingDirection.x, 0f, desiredFacingDirection.z);
            if (flatDesiredFace.sqrMagnitude < 0.0001f)
            {
                flatDesiredFace = new Vector3(currentForward.x, 0f, currentForward.z);
            }
            if (flatDesiredFace.sqrMagnitude < 0.0001f)
            {
                flatDesiredFace = Vector3.forward;
            }
            flatDesiredFace.Normalize();

            _smoothedFacing = Vector3.Slerp(_smoothedFacing, flatDesiredFace, faceAlpha).normalized;

            Vector3 flatCurrentFwd = new Vector3(currentForward.x, 0f, currentForward.z);
            if (flatCurrentFwd.sqrMagnitude < 0.0001f)
            {
                flatCurrentFwd = Vector3.forward;
            }
            flatCurrentFwd.Normalize();

            // Local velocity projected onto character's current horizontal axes
            Quaternion invYaw = Quaternion.Inverse(Quaternion.LookRotation(flatCurrentFwd, Vector3.up));
            CurrentLocalVelocity = invYaw * _smoothedWorldVelocity;

            // Update past history ring on fixed cadence
            _historyTimer += dt;
            if (_historyTimer >= historyIntervalSeconds)
            {
                _historyTimer = 0f;
                for (int i = 0; i < PastSampleCount - 1; i++)
                {
                    _pastPositions[i] = _pastPositions[i + 1];
                    _pastForwards[i] = _pastForwards[i + 1];
                }
                _pastPositions[PastSampleCount - 1] = currentPosition;
                _pastForwards[PastSampleCount - 1] = flatCurrentFwd;
            }

            // Predict future arc samples with exponential velocity integration towards desired target
            Vector3 integratedPos = currentPosition;
            float prevT = 0f;
            for (int i = 0; i < FutureSampleCount; i++)
            {
                float t = FutureTimeOffsets[i];
                float stepDt = t - prevT;
                float blend = 1f - Mathf.Exp(-velocityResponsiveness * 0.65f * t);
                Vector3 vAtT = Vector3.Lerp(_smoothedWorldVelocity, desiredWorldVelocity, blend);
                integratedPos += vAtT * stepDt;
                _futurePositions[i] = integratedPos;

                float faceBlend = 1f - Mathf.Exp(-facingResponsiveness * 0.65f * t);
                _futureForwards[i] = Vector3.Slerp(flatCurrentFwd, flatDesiredFace, faceBlend).normalized;
                prevT = t;
            }

            // Index 1 corresponds to +0.4s horizon turn prediction
            PredictedTurnDegrees04 = Vector3.SignedAngle(flatCurrentFwd, _futureForwards[1], Vector3.up);
        }

        private void OnDrawGizmosSelected()
        {
            if (!_initialized)
            {
                return;
            }

            Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.85f);
            for (int i = 0; i < PastSampleCount - 1; i++)
            {
                Gizmos.DrawLine(_pastPositions[i] + Vector3.up * 0.05f, _pastPositions[i + 1] + Vector3.up * 0.05f);
            }

            Gizmos.color = new Color(0.15f, 1f, 0.45f, 0.95f);
            Vector3 prev = transform.position + Vector3.up * 0.05f;
            for (int i = 0; i < FutureSampleCount; i++)
            {
                Vector3 p = _futurePositions[i] + Vector3.up * 0.05f;
                Gizmos.DrawLine(prev, p);
                Gizmos.DrawSphere(p, 0.055f);
                prev = p;
            }
        }
    }
}
