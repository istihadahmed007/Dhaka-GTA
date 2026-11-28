using DhakaGta.Character;
using DhakaGta.Core;
using DhakaGta.Missions;
using DhakaGta.Vehicles;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DhakaGta.UI
{
    public enum SmartphoneTab
    {
        MissionsAndJobs = 0,
        ShopAndInventory = 1,
        ContactsAndGarage = 2,
        SafehouseAndSave = 3
    }

    /// <summary>
    /// Interactive "Sonar Dhaka OS" in-game Smartphone ([P]) providing Story Missions, CNG/Taxi Fares,
    /// Courier Deliveries, Vehicle Contracts, Street Races, bKash Wallet & Inventory Shop,
    /// Protagonist Switching, Bilingual Contacts, Garage Call, and Save/Load.
    /// </summary>
    public sealed class DhakaSmartphoneController : MonoBehaviour
    {
        [SerializeField] private GameObject phoneRootPanel;
        [SerializeField] private TextMeshProUGUI phoneHeaderText;
        [SerializeField] private TextMeshProUGUI phoneBodyText;

        private DhakaMissionEngine _missionEngine;
        private PlayerEconomyAndInventory _economy;
        private DhakaSaveSystem _saveSystem;
        private ProtagonistController _protagonist;
        private ProtagonistSwitcher _switcher;
        private DhakaGarageManager _garage;

        private SmartphoneTab _activeTab = SmartphoneTab.MissionsAndJobs;
        private bool _isOpen;

        public bool IsPhoneOpen => _isOpen;
        public SmartphoneTab ActiveTab => _activeTab;

        public void ConfigureAndBuildUi(
            DhakaMissionEngine missions,
            PlayerEconomyAndInventory econ,
            DhakaSaveSystem saver,
            ProtagonistController protagonist,
            ProtagonistSwitcher switcher,
            DhakaGarageManager garage)
        {
            _missionEngine = missions;
            _economy = econ;
            _saveSystem = saver;
            _protagonist = protagonist;
            _switcher = switcher;
            _garage = garage;

            if (phoneRootPanel == null)
            {
                BuildSmartphoneCanvas();
            }

            SetPhoneOpen(false);
        }

        public void SetPhoneOpen(bool open)
        {
            _isOpen = open;
            if (phoneRootPanel != null)
            {
                phoneRootPanel.SetActive(_isOpen);
            }
            if (_isOpen)
            {
                RefreshPhoneDisplay();
            }
        }

        public void TogglePhone() => SetPhoneOpen(!_isOpen);

        public void SelectTab(SmartphoneTab tab)
        {
            _activeTab = tab;
            RefreshPhoneDisplay();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
            {
                TogglePhone();
            }

            if (_isOpen)
            {
                RefreshPhoneDisplay();
            }
        }

        public void ActionStartFirstAvailableStory()
        {
            if (_missionEngine == null) return;
            foreach (var m in _missionEngine.Catalog)
            {
                if (m != null && !_completedContains(m.missionId) && _missionEngine.IsMissionUnlocked(m))
                {
                    _missionEngine.StartMissionAsset(m);
                    SetPhoneOpen(false);
                    return;
                }
            }
        }

        public void ActionLaunchTaxiFare()
        {
            Vector3 p = _protagonist != null ? _protagonist.transform.position : Vector3.zero;
            _missionEngine?.StartDynamicCngOrTaxiFare(p);
            SetPhoneOpen(false);
        }

        public void ActionLaunchCourierDelivery()
        {
            Vector3 p = _protagonist != null ? _protagonist.transform.position : Vector3.zero;
            _missionEngine?.StartDynamicDeliveryJob(p);
            SetPhoneOpen(false);
        }

        public void ActionLaunchVehicleContract()
        {
            Vector3 p = _protagonist != null ? _protagonist.transform.position : Vector3.zero;
            _missionEngine?.StartDynamicVehicleContractJob(p);
            SetPhoneOpen(false);
        }

        public void ActionLaunchStreetRace()
        {
            Vector3 p = _protagonist != null ? _protagonist.transform.position : Vector3.zero;
            _missionEngine?.StartDynamicStreetRace(p);
            SetPhoneOpen(false);
        }

        public void ActionBuyAndUseCha()
        {
            if (_economy == null) return;
            if (_economy.PurchaseCatalogItem("cha_flask", 1))
            {
                _economy.UseInventoryItem("cha_flask", _protagonist != null ? _protagonist.gameObject : null);
            }
            RefreshPhoneDisplay();
        }

        public void ActionCallMayaContact()
        {
            MissionObjectiveEvents.RaiseDialogue(new BilingualDialogueLine
            {
                Speaker = "Maya Rahman (মায়া)",
                TextBangla = "তারেক, আমি গুলশান ও হাতিরঝিলের রেডিও চ্যানেল মনিটর করছি। দরকার হলে [Tab] চেপে আমাকে ডেকো!",
                TextEnglish = "Tariq, I'm scanning Gulshan & Hatirjheel comms. Switch to me [Tab] whenever we need Focus Reflex!",
                DisplaySeconds = 5.0f
            });
            RefreshPhoneDisplay();
        }

        public void ActionQuickSave()
        {
            _saveSystem?.SaveToDisk();
            RefreshPhoneDisplay();
        }

        public void ActionQuickLoad()
        {
            _saveSystem?.LoadFromDisk();
            RefreshPhoneDisplay();
        }

        private bool _completedContains(string id)
        {
            if (_missionEngine == null) return false;
            foreach (string c in _missionEngine.CompletedMissionIds)
            {
                if (c == id) return true;
            }
            return false;
        }

        private void RefreshPhoneDisplay()
        {
            if (phoneHeaderText == null || phoneBodyText == null) return;

            string activeId = _protagonist != null && _protagonist.Profile != null
                ? $"{_protagonist.Profile.fullName} ({_protagonist.Profile.banglaCallsign})"
                : "Tariq Hasan";

            int bdt = _economy != null ? _economy.CurrentBdt : 0;
            string bond = _economy != null ? _economy.TariqMayaBondTierLabel : "";
            phoneHeaderText.text = $"<b>SONAR DHAKA OS</b> · ৳{bdt:N0}\n<size=12>{activeId} | {bond}</size>";

            switch (_activeTab)
            {
                case SmartphoneTab.MissionsAndJobs:
                    string curM = _missionEngine != null && _missionEngine.ActiveMission != null
                        ? $"{_missionEngine.ActiveMission.title} ({_missionEngine.ActiveMission.banglaTitle})"
                        : "Free Roam — Select Story or Hustle below";
                    phoneBodyText.text =
                        $"<b>ACTIVE:</b> {curM}\n\n" +
                        $"Completed Stories: {(_missionEngine != null ? _missionEngine.CompletedMissionIds.Count : 0)} / {(_missionEngine != null ? _missionEngine.Catalog.Count : 0)}\n" +
                        $"Fares: {(_economy != null ? _economy.FaresCompletedCount : 0)} | Deliveries: {(_economy != null ? _economy.DeliveriesCompletedCount : 0)}\n" +
                        $"Contracts: {(_economy != null ? _economy.ContractsCompletedCount : 0)} | Races Won: {(_economy != null ? _economy.RacesWonCount : 0)}";
                    break;

                case SmartphoneTab.ShopAndInventory:
                    string invLines = $"<b>bKash Balance:</b> ৳{bdt:N0}\n<b>Inventory:</b>\n";
                    if (_economy != null)
                    {
                        foreach (var slot in _economy.Items)
                        {
                            invLines += $" • {slot.nameEnglish} ({slot.nameBangla}) x{slot.count}\n";
                        }
                    }
                    phoneBodyText.text = invLines;
                    break;

                case SmartphoneTab.ContactsAndGarage:
                    int ownedCount = _garage != null ? _garage.OwnedFleet.Count : 0;
                    phoneBodyText.text =
                        $"<b>CONTACTS</b>\n" +
                        $" • Maya Rahman (মায়া) — Tech & Recon\n" +
                        $" • Rafiq Bhai (রফিক ভাই) — Chawkbazar & Tong Intel\n\n" +
                        $"<b>OWNED GARAGE FLEET ({ownedCount})</b>\n" +
                        $"Active District Parking & Repair Ready.";
                    break;

                case SmartphoneTab.SafehouseAndSave:
                    int shCount = _economy != null ? _economy.UnlockedSafehouses.Count : 1;
                    phoneBodyText.text =
                        $"<b>SAFEHOUSES UNLOCKED:</b> {shCount}\n" +
                        $"Clock: {WorldSurfaceState.CurrentTimeOfDayHours:F1}h\n" +
                        $"Save File: {DhakaSaveSystem.DefaultSaveFileName}";
                    break;
            }
        }

        private void BuildSmartphoneCanvas()
        {
            var canvasGo = new GameObject("DhakaSmartphoneCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 25;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<GraphicRaycaster>();

            phoneRootPanel = new GameObject("SonarDhakaPhonePanel");
            phoneRootPanel.transform.SetParent(canvasGo.transform, false);
            var bg = phoneRootPanel.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.08f, 0.12f, 0.94f);
            bg.rectTransform.anchorMin = new Vector2(0.75f, 0.04f);
            bg.rectTransform.anchorMax = new Vector2(0.98f, 0.72f);
            bg.rectTransform.offsetMin = Vector2.zero;
            bg.rectTransform.offsetMax = Vector2.zero;

            var hdrGo = new GameObject("PhoneHeader");
            hdrGo.transform.SetParent(phoneRootPanel.transform, false);
            phoneHeaderText = hdrGo.AddComponent<TextMeshProUGUI>();
            phoneHeaderText.fontSize = 16;
            phoneHeaderText.color = new Color(0.22f, 0.90f, 0.65f);
            if (TMP_Settings.defaultFontAsset != null) phoneHeaderText.font = TMP_Settings.defaultFontAsset;
            phoneHeaderText.rectTransform.anchorMin = new Vector2(0.05f, 0.85f);
            phoneHeaderText.rectTransform.anchorMax = new Vector2(0.95f, 0.97f);
            phoneHeaderText.rectTransform.offsetMin = Vector2.zero;
            phoneHeaderText.rectTransform.offsetMax = Vector2.zero;

            var bodyGo = new GameObject("PhoneBody");
            bodyGo.transform.SetParent(phoneRootPanel.transform, false);
            phoneBodyText = bodyGo.AddComponent<TextMeshProUGUI>();
            phoneBodyText.fontSize = 14;
            phoneBodyText.color = new Color(0.92f, 0.95f, 0.98f);
            if (TMP_Settings.defaultFontAsset != null) phoneBodyText.font = TMP_Settings.defaultFontAsset;
            phoneBodyText.rectTransform.anchorMin = new Vector2(0.05f, 0.45f);
            phoneBodyText.rectTransform.anchorMax = new Vector2(0.95f, 0.84f);
            phoneBodyText.rectTransform.offsetMin = Vector2.zero;
            phoneBodyText.rectTransform.offsetMax = Vector2.zero;

            CreateButton(phoneRootPanel.transform, "Story Mission", new Vector2(0.05f, 0.36f), new Vector2(0.48f, 0.43f), ActionStartFirstAvailableStory);
            CreateButton(phoneRootPanel.transform, "CNG/Taxi Fare", new Vector2(0.52f, 0.36f), new Vector2(0.95f, 0.43f), ActionLaunchTaxiFare);
            CreateButton(phoneRootPanel.transform, "Courier Run", new Vector2(0.05f, 0.27f), new Vector2(0.48f, 0.34f), ActionLaunchCourierDelivery);
            CreateButton(phoneRootPanel.transform, "Vehicle Contract", new Vector2(0.52f, 0.27f), new Vector2(0.95f, 0.34f), ActionLaunchVehicleContract);
            CreateButton(phoneRootPanel.transform, "Street Race", new Vector2(0.05f, 0.18f), new Vector2(0.48f, 0.25f), ActionLaunchStreetRace);
            CreateButton(phoneRootPanel.transform, "Buy+Drink Cha (৳60)", new Vector2(0.52f, 0.18f), new Vector2(0.95f, 0.25f), ActionBuyAndUseCha);
            CreateButton(phoneRootPanel.transform, "Call Maya", new Vector2(0.05f, 0.09f), new Vector2(0.48f, 0.16f), ActionCallMayaContact);
            CreateButton(phoneRootPanel.transform, "Switch Tariq/Maya", new Vector2(0.52f, 0.09f), new Vector2(0.95f, 0.16f), () => _switcher?.ToggleProtagonist());
            CreateButton(phoneRootPanel.transform, "Save Game", new Vector2(0.05f, 0.01f), new Vector2(0.48f, 0.07f), ActionQuickSave);
            CreateButton(phoneRootPanel.transform, "Load Save", new Vector2(0.52f, 0.01f), new Vector2(0.95f, 0.07f), ActionQuickLoad);
        }

        private static void CreateButton(Transform parent, string label, Vector2 aMin, Vector2 aMax, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject($"Btn_{label}");
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.14f, 0.24f, 0.36f, 1f);
            img.rectTransform.anchorMin = aMin;
            img.rectTransform.anchorMax = aMax;
            img.rectTransform.offsetMin = Vector2.zero;
            img.rectTransform.offsetMax = Vector2.zero;

            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(onClick);

            var txtGo = new GameObject("Txt");
            txtGo.transform.SetParent(go.transform, false);
            var tmp = txtGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 13;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
            tmp.rectTransform.anchorMin = Vector2.zero;
            tmp.rectTransform.anchorMax = Vector2.one;
            tmp.rectTransform.offsetMin = Vector2.zero;
            tmp.rectTransform.offsetMax = Vector2.zero;
        }
    }
}
