using System.Text;
using DhakaGta.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DhakaGta.UI
{
    /// <summary>
    /// Interactive Multiplayer Lobby, Auth (CallSign/PIN/Reconnect), Peer List, Live Player Chat,
    /// Server Health/Ping Readout, and Anti-Cheat Alert Overlay ([M]).
    /// </summary>
    public sealed class DhakaMultiplayerHudController : MonoBehaviour
    {
        [SerializeField] private GameObject rootPanel;
        [SerializeField] private TextMeshProUGUI statusAndPeersText;
        [SerializeField] private TextMeshProUGUI chatFeedText;
        [SerializeField] private TMP_InputField serverUrlInput;
        [SerializeField] private TMP_InputField callSignInput;
        [SerializeField] private TMP_InputField chatInput;

        private DhakaMultiplayerSessionManager _session;
        private bool _isOpen;

        public bool IsPanelOpen => _isOpen;

        public void ConfigureAndBuildUi(DhakaMultiplayerSessionManager sessionMgr)
        {
            _session = sessionMgr;
            if (rootPanel == null) BuildMultiplayerCanvas();
            SetPanelOpen(false);
        }

        public void SetPanelOpen(bool open)
        {
            _isOpen = open;
            if (rootPanel != null) rootPanel.SetActive(_isOpen);
            if (_isOpen) RefreshDisplay();
        }

        public void TogglePanel() => SetPanelOpen(!_isOpen);

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
            {
                TogglePanel();
            }

            if (_isOpen)
            {
                RefreshDisplay();
            }
        }

        public void ActionConnectLocal()
        {
            string url = serverUrlInput != null && !string.IsNullOrWhiteSpace(serverUrlInput.text)
                ? serverUrlInput.text.Trim()
                : DhakaNetProtocolConstants.DefaultLocalServerUrl;
            string name = callSignInput != null && !string.IsNullOrWhiteSpace(callSignInput.text)
                ? callSignInput.text.Trim()
                : "Tariq_Unity";
            _session?.ConnectAndAuthenticate(url, name);
        }

        public void ActionDisconnect() => _session?.Disconnect();

        public void ActionSendChat()
        {
            if (chatInput == null || string.IsNullOrWhiteSpace(chatInput.text)) return;
            _session?.SendChatMessage(chatInput.text);
            chatInput.text = "";
        }

        private void RefreshDisplay()
        {
            if (_session == null) return;

            if (statusAndPeersText != null)
            {
                string acLine = string.IsNullOrEmpty(_session.LastAntiCheatAlert)
                    ? ""
                    : $"\n<color=#F87171>SECURITY: {_session.LastAntiCheatAlert}</color>";
                statusAndPeersText.text =
                    $"<b>DHAKA MULTIPLAYER ({_session.ConnectionState})</b>\n" +
                    $"{_session.LastStatusMessage}\n" +
                    $"Real Players Online: {_session.ServerPlayerCount}/{DhakaNetProtocolConstants.MaxPlayersPerRoom} | Ping: {_session.CurrentPingMs} ms | Tick: {_session.ServerTick}\n" +
                    $"Server Wallet: ৳{_session.AuthoritativeMoneyBdt:N0} | Owned Vehicles: {_session.OwnedVehicleIds.Count} | Inv Kinds: {_session.ServerInventory.Count}{acLine}";
            }

            if (chatFeedText != null)
            {
                var sb = new StringBuilder();
                var history = _session.ChatHistory;
                int start = Mathf.Max(0, history.Count - 8);
                for (int i = start; i < history.Count; i++)
                {
                    var m = history[i];
                    string col = m.isSystem ? "#FBBF24" : "#93C5FD";
                    sb.AppendLine($"<color={col}>[{m.senderName}]</color> {m.text}");
                }
                chatFeedText.text = sb.ToString();
            }
        }

        private void BuildMultiplayerCanvas()
        {
            var canvasGo = new GameObject("DhakaMultiplayerCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<GraphicRaycaster>();

            rootPanel = new GameObject("MultiplayerLobbyPanel");
            rootPanel.transform.SetParent(canvasGo.transform, false);
            var bg = rootPanel.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.06f, 0.10f, 0.94f);
            bg.rectTransform.anchorMin = new Vector2(0.03f, 0.14f);
            bg.rectTransform.anchorMax = new Vector2(0.42f, 0.72f);
            bg.rectTransform.offsetMin = Vector2.zero;
            bg.rectTransform.offsetMax = Vector2.zero;

            statusAndPeersText = AddTmp(rootPanel.transform, "StatusText", new Vector2(0.04f, 0.74f), new Vector2(0.96f, 0.96f), 14, Color.white);
            chatFeedText = AddTmp(rootPanel.transform, "ChatFeed", new Vector2(0.04f, 0.28f), new Vector2(0.96f, 0.62f), 13, new Color(0.9f, 0.95f, 1f));

            serverUrlInput = AddInputField(rootPanel.transform, "ServerUrl", DhakaNetProtocolConstants.DefaultLocalServerUrl, new Vector2(0.04f, 0.65f), new Vector2(0.56f, 0.72f));
            callSignInput = AddInputField(rootPanel.transform, "CallSign", "Tariq_Dhaka", new Vector2(0.58f, 0.65f), new Vector2(0.96f, 0.72f));
            chatInput = AddInputField(rootPanel.transform, "ChatInput", "", new Vector2(0.04f, 0.17f), new Vector2(0.72f, 0.25f));

            AddButton(rootPanel.transform, "Send", new Vector2(0.74f, 0.17f), new Vector2(0.96f, 0.25f), ActionSendChat);
            AddButton(rootPanel.transform, "Connect", new Vector2(0.04f, 0.04f), new Vector2(0.32f, 0.13f), ActionConnectLocal);
            AddButton(rootPanel.transform, "Buy Cha (৳60)", new Vector2(0.35f, 0.04f), new Vector2(0.65f, 0.13f), () => _session?.SendBuyItem("cha_flask"));
            AddButton(rootPanel.transform, "Disconnect", new Vector2(0.68f, 0.04f), new Vector2(0.96f, 0.13f), ActionDisconnect);
        }

        private static TextMeshProUGUI AddTmp(Transform parent, string name, Vector2 aMin, Vector2 aMax, float fs, Color col)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.fontSize = fs;
            t.color = col;
            if (TMP_Settings.defaultFontAsset != null) t.font = TMP_Settings.defaultFontAsset;
            t.rectTransform.anchorMin = aMin;
            t.rectTransform.anchorMax = aMax;
            t.rectTransform.offsetMin = Vector2.zero;
            t.rectTransform.offsetMax = Vector2.zero;
            return t;
        }

        private static TMP_InputField AddInputField(Transform parent, string name, string defText, Vector2 aMin, Vector2 aMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.10f, 0.14f, 0.20f, 1f);
            img.rectTransform.anchorMin = aMin;
            img.rectTransform.anchorMax = aMax;
            img.rectTransform.offsetMin = Vector2.zero;
            img.rectTransform.offsetMax = Vector2.zero;

            var txt = AddTmp(go.transform, "Text", Vector2.zero, Vector2.one, 13, Color.white);
            var inp = go.AddComponent<TMP_InputField>();
            inp.textComponent = txt;
            inp.text = defText;
            return inp;
        }

        private static void AddButton(Transform parent, string label, Vector2 aMin, Vector2 aMax, UnityEngine.Events.UnityAction act)
        {
            var go = new GameObject($"Btn_{label}");
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.14f, 0.34f, 0.50f, 1f);
            img.rectTransform.anchorMin = aMin;
            img.rectTransform.anchorMax = aMax;
            img.rectTransform.offsetMin = Vector2.zero;
            img.rectTransform.offsetMax = Vector2.zero;
            go.AddComponent<Button>().onClick.AddListener(act);
            var txt = AddTmp(go.transform, "Txt", Vector2.zero, Vector2.one, 13, Color.white);
            txt.alignment = TextAlignmentOptions.Center;
            txt.text = label;
        }
    }
}
