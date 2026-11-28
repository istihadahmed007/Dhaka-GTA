using DhakaGta.Core;
using DhakaGta.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DhakaGta.Character
{
    /// <summary>
    /// Allows runtime switching between Tariq Hasan and Maya Rahman in-scene ([Tab]),
    /// swapping active visual rigged character child prefabs, rebinding Humanoid Animators,
    /// and updating protagonist stats cleanly.
    /// </summary>
    public sealed class ProtagonistSwitcher : MonoBehaviour
    {
        [SerializeField] private ProtagonistController controller;
        [SerializeField] private InputActionRouter inputRouter;
        [SerializeField] private ProtagonistProfileSO tariqProfile;
        [SerializeField] private ProtagonistProfileSO mayaProfile;
        [SerializeField] private GameObject tariqVisualRig;
        [SerializeField] private GameObject mayaVisualRig;

        private bool _isMayaActive;

        public string ActiveProtagonistId => _isMayaActive ? "maya" : "tariq";

        public void Configure(
            ProtagonistController protagonistController,
            InputActionRouter router,
            ProtagonistProfileSO tariq,
            ProtagonistProfileSO maya,
            GameObject tariqRig,
            GameObject mayaRig)
        {
            controller = protagonistController;
            inputRouter = router;
            tariqProfile = tariq;
            mayaProfile = maya;
            tariqVisualRig = tariqRig;
            mayaVisualRig = mayaRig;
            ApplyActiveSelection(false);
        }

        private void Update()
        {
            if (GameBootstrap.CurrentState == GameState.Paused || controller == null || controller.InVehicle)
            {
                return;
            }

            bool switchPressed =
                (inputRouter != null && inputRouter.SwitchProtagonistAction != null && inputRouter.SwitchProtagonistAction.WasPressedThisFrame()) ||
                (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame);

            if (switchPressed)
            {
                ToggleProtagonist();
            }
        }

        public void ToggleProtagonist()
        {
            ApplyActiveSelection(!_isMayaActive);
        }

        public void SelectById(string protagonistId)
        {
            bool selectMaya = string.Equals(protagonistId, "maya", System.StringComparison.OrdinalIgnoreCase);
            ApplyActiveSelection(selectMaya);
        }

        private void ApplyActiveSelection(bool useMaya)
        {
            _isMayaActive = useMaya;
            ProtagonistProfileSO chosenProfile = _isMayaActive ? mayaProfile : tariqProfile;

            if (tariqVisualRig != null)
            {
                tariqVisualRig.SetActive(!_isMayaActive);
            }
            if (mayaVisualRig != null)
            {
                mayaVisualRig.SetActive(_isMayaActive);
            }

            GameObject activeVisual = _isMayaActive ? mayaVisualRig : tariqVisualRig;
            Animator activeAnimator = activeVisual != null ? activeVisual.GetComponentInChildren<Animator>(true) : null;

            if (controller != null && chosenProfile != null)
            {
                controller.Configure(chosenProfile, inputRouter, Camera.main != null ? Camera.main.transform : null);
                if (activeAnimator != null)
                {
                    controller.BindActiveVisualRig(activeAnimator);
                }
            }

            CharacterAbilityEvents.PublishProtagonistSwitched(ActiveProtagonistId);
        }
    }
}
