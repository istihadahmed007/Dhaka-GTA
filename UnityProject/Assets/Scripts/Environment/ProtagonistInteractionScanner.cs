using DhakaGta.Core;
using DhakaGta.Vehicles;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DhakaGta.Environment
{
    /// <summary>
    /// Scans nearby IInteractable props (Tea Stalls, Benches, Repair Bays) and all IVehicleDrivable vehicles
    /// allowing seamless [E] World Interaction AND [F] Enter/Exit Vehicle switching in Gulshan-Hatirjheel.
    /// </summary>
    public sealed class ProtagonistInteractionScanner : MonoBehaviour
    {
        [SerializeField] private GameObject protagonistObject;
        [SerializeField] private Transform cameraFollowProxy;
        [SerializeField] private InputActionRouter inputRouter;
        [SerializeField] private float scanIntervalSeconds = 0.2f;

        private float _nextScanTime;
        private IInteractable _nearestInteractable;
        private MonoBehaviour _currentVehicleBehaviour;
        private IVehicleDrivable _currentVehicle;
        private bool _isDriving;

        public string ActivePrompt { get; private set; } = string.Empty;
        public string LastFeedback { get; private set; } = string.Empty;
        public bool IsDrivingVehicle => _isDriving;
        public MonoBehaviour ActiveVehicleBehaviour => _isDriving ? _currentVehicleBehaviour : null;
        public IVehicleDrivable ActiveVehicle => _isDriving ? _currentVehicle : null;
        public Vector3 ActiveSubjectPosition =>
            (_isDriving && _currentVehicleBehaviour != null)
                ? _currentVehicleBehaviour.transform.position
                : (protagonistObject != null ? protagonistObject.transform.position : Vector3.zero);

        public void Configure(GameObject playerObj, Transform followProxy, InputActionRouter router)
        {
            protagonistObject = playerObj;
            cameraFollowProxy = followProxy;
            inputRouter = router;
        }

        private void OnEnable() => InteractionEvents.OnInteractionFeedback += HandleFeedback;
        private void OnDisable() => InteractionEvents.OnInteractionFeedback -= HandleFeedback;

        private void LateUpdate()
        {
            if (cameraFollowProxy != null && protagonistObject != null)
            {
                Transform tracked = (_isDriving && _currentVehicleBehaviour != null)
                    ? _currentVehicleBehaviour.transform
                    : protagonistObject.transform;
                cameraFollowProxy.position = tracked.position;
                cameraFollowProxy.rotation = tracked.rotation;
            }
        }

        private void Update()
        {
            if (protagonistObject == null) return;

            if (Time.unscaledTime >= _nextScanTime)
            {
                _nextScanTime = Time.unscaledTime + scanIntervalSeconds;
                RefreshNearestTargets();
            }

            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame && !_isDriving)
            {
                TryTriggerNearestInteractable();
            }

            if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            {
                TryToggleVehicleSeat();
            }
        }

        public bool TryTriggerNearestInteractable()
        {
            RefreshNearestTargets();
            if (_nearestInteractable != null && _nearestInteractable.CanInteract(protagonistObject))
            {
                _nearestInteractable.Interact(protagonistObject);
                return true;
            }
            return false;
        }

        public bool TryToggleVehicleSeat()
        {
            if (_isDriving && _currentVehicleBehaviour != null)
            {
                _currentVehicle?.ExitDriverSeat();
                var seatComp = _currentVehicleBehaviour.GetComponent<DhakaVehicleSeatsAndInteract>();
                if (seatComp != null && seatComp.CurrentDriver == protagonistObject)
                {
                    seatComp.BeginAnimatedExitDriver();
                }
                else
                {
                    protagonistObject.transform.position = _currentVehicleBehaviour.transform.position - _currentVehicleBehaviour.transform.right * 2.2f + Vector3.up * 0.25f;
                    protagonistObject.SetActive(true);
                }
                _isDriving = false;
                inputRouter?.SetMode(InputContextMode.OnFoot);
                InteractionEvents.RaiseFeedback($"Exited {_currentVehicleBehaviour.gameObject.name}");
                return true;
            }

            MonoBehaviour[] allBehaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            MonoBehaviour closestBehaviour = null;
            IVehicleDrivable closestVehicle = null;
            float best = 5.0f;

            foreach (MonoBehaviour mb in allBehaviours)
            {
                if (mb is IVehicleDrivable v)
                {
                    float d = Vector3.Distance(protagonistObject.transform.position, mb.transform.position);
                    if (d < best)
                    {
                        best = d;
                        closestBehaviour = mb;
                        closestVehicle = v;
                    }
                }
            }

            if (closestVehicle != null && closestBehaviour != null)
            {
                _currentVehicleBehaviour = closestBehaviour;
                _currentVehicle = closestVehicle;
                _currentVehicle.EnterDriverSeat();
                var seatComp = closestBehaviour.GetComponent<DhakaVehicleSeatsAndInteract>();
                if (seatComp != null)
                {
                    seatComp.BeginAnimatedEnterDriver(protagonistObject);
                }
                else
                {
                    protagonistObject.SetActive(false);
                }
                _isDriving = true;
                inputRouter?.SetMode(InputContextMode.DrivingVehicle);
                InteractionEvents.RaiseFeedback($"Driving {closestBehaviour.gameObject.name} (W/S/A/D, L Lights, H Horn, F Exit)");
                return true;
            }

            return false;
        }

        private void RefreshNearestTargets()
        {
            _nearestInteractable = null;
            ActivePrompt = string.Empty;

            if (_isDriving)
            {
                ActivePrompt = "[F] Exit Vehicle";
                return;
            }

            Vector3 origin = protagonistObject.transform.position;
            float bestDist = float.MaxValue;
            MonoBehaviour[] allBehaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);

            foreach (MonoBehaviour mb in allBehaviours)
            {
                if (mb is IVehicleDrivable && Vector3.Distance(origin, mb.transform.position) <= 5.0f)
                {
                    ActivePrompt = $"[F] Drive {mb.gameObject.name.Replace("_", " ")}";
                    return;
                }
            }

            foreach (MonoBehaviour mb in allBehaviours)
            {
                if (mb is IInteractable interactable && interactable.Kind != InteractableKind.VehicleEntry)
                {
                    float d = Vector3.Distance(origin, interactable.WorldPosition);
                    if (d <= interactable.InteractionRadiusMeters && d < bestDist)
                    {
                        bestDist = d;
                        _nearestInteractable = interactable;
                    }
                }
            }

            if (_nearestInteractable != null)
            {
                ActivePrompt = _nearestInteractable.PromptText;
            }
        }

        private void HandleFeedback(string msg) => LastFeedback = msg;
    }
}
