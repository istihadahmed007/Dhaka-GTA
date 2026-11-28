using UnityEngine;
using UnityEngine.InputSystem;

namespace DhakaGta.Core
{
    public enum InputContextMode
    {
        MenuUi = 0,
        OnFoot = 1,
        DrivingVehicle = 2,
    }

    /// <summary>
    /// Conflict-free router managing Player, Vehicle, Camera, UI, and Debug InputActionMaps.
    /// Prevents legacy Three.js keybind shadowing by enabling only valid maps for the current mode.
    /// </summary>
    public sealed class InputActionRouter : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;

        private InputActionMap _playerMap;
        private InputActionMap _vehicleMap;
        private InputActionMap _cameraMap;
        private InputActionMap _uiMap;
        private InputActionMap _debugMap;

        public InputContextMode CurrentMode { get; private set; } = InputContextMode.MenuUi;
        public InputActionAsset Asset => inputActions;

        public InputAction MoveAction { get; private set; }
        public InputAction LookAction { get; private set; }
        public InputAction JumpAction { get; private set; }
        public InputAction SprintAction { get; private set; }
        public InputAction CrouchAction { get; private set; }
        public InputAction InteractAction { get; private set; }
        public InputAction AimAction { get; private set; }
        public InputAction AbilityAction { get; private set; }
        public InputAction GestureAction { get; private set; }
        public InputAction SwitchProtagonistAction { get; private set; }
        public InputAction CycleCameraAction { get; private set; }
        public InputAction PauseCancelAction { get; private set; }
        public InputAction CycleQualityTierAction { get; private set; }
        public InputAction ToggleHudOverlayAction { get; private set; }

        public void BindAsset(InputActionAsset asset)
        {
            inputActions = asset;
            CacheMapsAndActions();
        }

        private void Awake()
        {
            CacheMapsAndActions();
        }

        private void OnEnable()
        {
            CacheMapsAndActions();
            SetMode(CurrentMode);
        }

        private void OnDisable()
        {
            DisableAllMaps();
        }

        public void SetMode(InputContextMode mode)
        {
            CurrentMode = mode;
            if (inputActions == null)
            {
                return;
            }

            DisableAllMaps();

            switch (mode)
            {
                case InputContextMode.MenuUi:
                    _uiMap?.Enable();
                    _debugMap?.Enable();
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                    break;

                case InputContextMode.OnFoot:
                    _playerMap?.Enable();
                    _cameraMap?.Enable();
                    _uiMap?.Enable();
                    _debugMap?.Enable();
                    break;

                case InputContextMode.DrivingVehicle:
                    _vehicleMap?.Enable();
                    _cameraMap?.Enable();
                    _uiMap?.Enable();
                    _debugMap?.Enable();
                    break;
            }
        }

        private void CacheMapsAndActions()
        {
            if (inputActions == null)
            {
                return;
            }

            _playerMap = inputActions.FindActionMap("Player", throwIfNotFound: false);
            _vehicleMap = inputActions.FindActionMap("Vehicle", throwIfNotFound: false);
            _cameraMap = inputActions.FindActionMap("Camera", throwIfNotFound: false);
            _uiMap = inputActions.FindActionMap("UI", throwIfNotFound: false);
            _debugMap = inputActions.FindActionMap("Debug", throwIfNotFound: false);

            MoveAction = _playerMap?.FindAction("Move", throwIfNotFound: false);
            LookAction = _playerMap?.FindAction("Look", throwIfNotFound: false);
            JumpAction = _playerMap?.FindAction("Jump", throwIfNotFound: false);
            SprintAction = _playerMap?.FindAction("Sprint", throwIfNotFound: false);
            CrouchAction = _playerMap?.FindAction("Crouch", throwIfNotFound: false);
            InteractAction = _playerMap?.FindAction("Interact", throwIfNotFound: false);
            AimAction = _playerMap?.FindAction("Aim", throwIfNotFound: false);
            AbilityAction = _playerMap?.FindAction("SpecialAbility", throwIfNotFound: false);
            GestureAction = _playerMap?.FindAction("DialogueGesture", throwIfNotFound: false);
            SwitchProtagonistAction = _playerMap?.FindAction("SwitchProtagonist", throwIfNotFound: false);
            CycleCameraAction = _cameraMap?.FindAction("CycleCameraMode", throwIfNotFound: false);
            PauseCancelAction = _uiMap?.FindAction("Cancel", throwIfNotFound: false);
            CycleQualityTierAction = _debugMap?.FindAction("CycleQualityTier", throwIfNotFound: false);
            ToggleHudOverlayAction = _debugMap?.FindAction("ToggleHudOverlay", throwIfNotFound: false);
        }

        private void DisableAllMaps()
        {
            _playerMap?.Disable();
            _vehicleMap?.Disable();
            _cameraMap?.Disable();
            _uiMap?.Disable();
            _debugMap?.Disable();
        }
    }
}
