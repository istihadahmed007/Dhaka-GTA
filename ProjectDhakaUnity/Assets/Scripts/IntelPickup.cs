using UnityEngine;

namespace ProjectDhaka
{
    /// <summary>
    /// Floating courier intel beacon pickup with trigger collection and hover animation.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class IntelPickup : MonoBehaviour
    {
        [SerializeField] Transform gemTransform;
        [SerializeField] Transform ringTransform;
        [SerializeField] float bobAmplitude = 0.20f;
        [SerializeField] float bobSpeed = 2.6f;
        [SerializeField] float spinSpeed = 95f;

        Vector3 baseGemLocalPos;
        bool collected;
        float phaseOffset;

        public bool IsCollected => collected;

        public void BindVisuals(Transform gem, Transform ring, float phase)
        {
            gemTransform = gem;
            ringTransform = ring;
            phaseOffset = phase;
            if (gemTransform != null) baseGemLocalPos = gemTransform.localPosition;
        }

        void Awake()
        {
            var col = GetComponent<SphereCollider>();
            col.isTrigger = true;
            if (gemTransform != null) baseGemLocalPos = gemTransform.localPosition;
        }

        void Update()
        {
            if (collected) return;

            float t = Time.time + phaseOffset;
            if (gemTransform != null)
            {
                gemTransform.localPosition = baseGemLocalPos + Vector3.up * (Mathf.Sin(t * bobSpeed) * bobAmplitude);
                gemTransform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
            }
            if (ringTransform != null)
            {
                ringTransform.Rotate(Vector3.up, -spinSpeed * 0.55f * Time.deltaTime, Space.Self);
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (collected) return;
            GameLoop loop = GameLoop.Instance;
            if (loop == null || loop.CurrentState != GameLoop.PlayState.Playing) return;

            if (other.GetComponent<PlayerController>() != null)
            {
                Collect();
                loop.OnIntelCollected(this);
            }
        }

        public void Collect()
        {
            if (collected) return;
            collected = true;
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;
            gameObject.SetActive(false);
        }
    }
}
