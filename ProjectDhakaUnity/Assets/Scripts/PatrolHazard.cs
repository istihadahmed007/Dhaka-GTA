using UnityEngine;

namespace ProjectDhaka
{
    /// <summary>
    /// Patrolling Dhaka curfew blockade vehicle that moves along street waypoints
    /// and damages the courier on trigger contact.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class PatrolHazard : MonoBehaviour
    {
        [SerializeField] Vector3[] waypoints;
        [SerializeField] float moveSpeed = 5.6f;
        [SerializeField] float turnSpeed = 8f;
        [SerializeField] Transform beaconLightTransform;

        int nextWaypointIndex;

        public void ConfigureWaypoints(Vector3[] points, float speed, Transform beacon)
        {
            waypoints = points;
            moveSpeed = speed;
            beaconLightTransform = beacon;
            nextWaypointIndex = points != null && points.Length > 1 ? 1 : 0;
        }

        void Awake()
        {
            var box = GetComponent<BoxCollider>();
            box.isTrigger = true;
        }

        void Update()
        {
            GameLoop loop = GameLoop.Instance;
            if (loop == null || loop.CurrentState != GameLoop.PlayState.Playing)
            {
                if (beaconLightTransform != null)
                {
                    float pulse = 0.85f + 0.35f * Mathf.Sin(Time.unscaledTime * 5f);
                    beaconLightTransform.localScale = Vector3.one * pulse;
                }
                return;
            }

            if (waypoints == null || waypoints.Length < 2) return;

            Vector3 target = waypoints[nextWaypointIndex];
            Vector3 toTarget = target - transform.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude < 0.09f)
            {
                nextWaypointIndex = (nextWaypointIndex + 1) % waypoints.Length;
                return;
            }

            Vector3 dir = toTarget.normalized;
            transform.position += dir * (moveSpeed * Time.deltaTime);
            Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1f - Mathf.Exp(-turnSpeed * Time.deltaTime));

            if (beaconLightTransform != null)
            {
                float flash = 0.85f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 9f));
                beaconLightTransform.localScale = Vector3.one * flash;
            }
        }

        void OnTriggerEnter(Collider other)
        {
            GameLoop loop = GameLoop.Instance;
            if (loop == null || loop.CurrentState != GameLoop.PlayState.Playing) return;

            if (other.GetComponent<PlayerController>() != null)
            {
                loop.OnPlayerHitHazard(transform.position);
            }
        }
    }
}
