using System.Collections;
using DhakaGta.Core;
using UnityEngine;

namespace DhakaGta.Vehicles
{
    /// <summary>
    /// Handles Right-Hand-Drive driver seat positioning, passenger seat occupancy,
    /// animated enter/exit door-mount transitions, and first-person cockpit eye mounting.
    /// </summary>
    public sealed class DhakaVehicleSeatsAndInteract : MonoBehaviour, IInteractable
    {
        [Header("Seat Transforms")]
        [SerializeField] private Transform driverSeatAnchor;
        [SerializeField] private Transform[] passengerSeatAnchors = System.Array.Empty<Transform>();
        [SerializeField] private Transform firstPersonCockpitEye;
        [SerializeField] private Transform enterExitDoorPoint;

        [Header("Animation Timings")]
        [SerializeField] private float enterExitDurationSeconds = 0.35f;
        [SerializeField] private float interactionRadiusMeters = 4.2f;

        private GameObject _currentDriver;
        private readonly GameObject[] _passengers = new GameObject[8];

        public InteractableKind Kind => InteractableKind.VehicleEntry;
        public string PromptText => _currentDriver == null ? $"[F] Drive {gameObject.name.Replace("_", " ")}" : "[F] Exit Vehicle";
        public Vector3 WorldPosition => enterExitDoorPoint != null ? enterExitDoorPoint.position : transform.position;
        public float InteractionRadiusMeters => interactionRadiusMeters;
        public Transform FirstPersonCockpitEye => firstPersonCockpitEye != null ? firstPersonCockpitEye : driverSeatAnchor;
        public int SeatCount => 1 + (passengerSeatAnchors != null ? passengerSeatAnchors.Length : 0);
        public bool IsTransitioning { get; private set; }
        public GameObject CurrentDriver => _currentDriver;

        public void ConfigureSeats(Transform driver, Transform[] passengers, Transform cockpitEye, Transform doorAnchor)
        {
            driverSeatAnchor = driver;
            passengerSeatAnchors = passengers ?? System.Array.Empty<Transform>();
            firstPersonCockpitEye = cockpitEye;
            enterExitDoorPoint = doorAnchor;
        }

        public bool CanInteract(GameObject interactor)
        {
            return interactor != null && !IsTransitioning &&
                   Vector3.Distance(interactor.transform.position, WorldPosition) <= interactionRadiusMeters;
        }

        public void Interact(GameObject interactor)
        {
            if (_currentDriver == null)
            {
                BeginAnimatedEnterDriver(interactor);
            }
            else if (_currentDriver == interactor)
            {
                BeginAnimatedExitDriver();
            }
        }

        public void BeginAnimatedEnterDriver(GameObject character)
        {
            if (character == null || IsTransitioning) return;
            StartCoroutine(AnimateSeatTransition(character, driverSeatAnchor, entering: true));
        }

        public void BeginAnimatedExitDriver()
        {
            if (_currentDriver == null || IsTransitioning) return;
            StartCoroutine(AnimateSeatTransition(_currentDriver, enterExitDoorPoint, entering: false));
        }

        public bool TryBoardPassenger(GameObject npcOrPlayer, out int assignedSeatIndex)
        {
            assignedSeatIndex = -1;
            if (npcOrPlayer == null || passengerSeatAnchors == null) return false;

            for (int i = 0; i < passengerSeatAnchors.Length && i < _passengers.Length; i++)
            {
                if (_passengers[i] == null)
                {
                    _passengers[i] = npcOrPlayer;
                    assignedSeatIndex = i;
                    if (passengerSeatAnchors[i] != null)
                    {
                        npcOrPlayer.transform.SetParent(passengerSeatAnchors[i], false);
                        npcOrPlayer.transform.localPosition = Vector3.zero;
                        npcOrPlayer.transform.localRotation = Quaternion.identity;
                    }
                    return true;
                }
            }
            return false;
        }

        private IEnumerator AnimateSeatTransition(GameObject character, Transform targetAnchor, bool entering)
        {
            IsTransitioning = true;
            Vector3 startPos = character.transform.position;
            Quaternion startRot = character.transform.rotation;

            Vector3 endPos = targetAnchor != null ? targetAnchor.position : (transform.position + transform.right * (entering ? 0.35f : 2.1f));
            Quaternion endRot = targetAnchor != null ? targetAnchor.rotation : transform.rotation;

            CharacterAbilityEvents.PublishVehicleBoarding(new VehicleBoardingEventData
            {
                ProtagonistId = "active",
                IsEntering = entering,
                UsesLeftDoor = true,
                SeatWorldPosition = endPos,
                SeatWorldRotation = endRot
            });

            var cc = character.GetComponent<CharacterController>();
            if (entering && cc != null) cc.enabled = false;
            if (!entering)
            {
                character.transform.SetParent(null, true);
                character.SetActive(true);
            }

            float elapsed = 0f;
            float dur = Mathf.Max(0.05f, enterExitDurationSeconds);
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / dur));
                // Arc slightly upward for realistic cabin step-in / step-out
                Vector3 arcPos = Vector3.Lerp(startPos, endPos, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.25f);
                character.transform.position = arcPos;
                character.transform.rotation = Quaternion.Slerp(startRot, endRot, t);
                yield return null;
            }

            if (entering)
            {
                _currentDriver = character;
                if (driverSeatAnchor != null)
                {
                    character.transform.SetParent(driverSeatAnchor, false);
                    character.transform.localPosition = Vector3.zero;
                    character.transform.localRotation = Quaternion.identity;
                }
                GetComponent<IVehicleDrivable>()?.EnterDriverSeat();
                InteractionEvents.RaiseFeedback($"Boarded {gameObject.name}");
            }
            else
            {
                if (cc != null) cc.enabled = true;
                GetComponent<IVehicleDrivable>()?.ExitDriverSeat();
                _currentDriver = null;
                InteractionEvents.RaiseFeedback($"Exited {gameObject.name}");
            }

            IsTransitioning = false;
        }
    }
}
