using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DhakaGta.Networking
{
    /// <summary>
    /// Native .NET ClientWebSocket transport for Unity 6 targeting the authoritative Node.js server.
    /// Runs socket I/O off the main thread and dispatches JSON frames safely via ConcurrentQueue.
    /// </summary>
    public sealed class DhakaWebSocketTransport : MonoBehaviour, IMultiplayerSessionTransport
    {
        private readonly ConcurrentQueue<string> _inboundJson = new ConcurrentQueue<string>();
        private readonly ConcurrentQueue<string> _outboundJson = new ConcurrentQueue<string>();
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);

        private ClientWebSocket _ws;
        private CancellationTokenSource _cts;
        private bool _connecting;
        private int _localTick;

        public event Action OnSocketOpened;
        public event Action<string> OnSocketClosed;
        public event Action<string> OnSocketError;
        public event Action<string> OnRawJsonMessage;

        public bool IsConnected => _ws != null && _ws.State == WebSocketState.Open;
        public bool IsConnecting => _connecting;
        public int LocalTick => _localTick;
        public string ServerUrl { get; private set; }

        public void SendPlayerSnapshot(in DhakaNetSnapshot snapshot)
        {
            _localTick = snapshot.tick;
            Vector3 fwd = snapshot.rotation * Vector3.forward;
            float headingRad = Mathf.Atan2(fwd.x, fwd.z);
            SendPacketObject(new CInputPacket
            {
                seq = Mathf.Max(1, snapshot.tick),
                dt = 0.05f,
                moveX = 0f,
                moveZ = snapshot.speedMetersPerSec > 0.1f ? 1f : 0f,
                heading = headingRad,
                sprint = snapshot.speedMetersPerSec > 5.5f,
                jump = false
            });
        }

        public void Connect(string serverUrl)
        {
            if (IsConnected || _connecting)
            {
                CloseImmediate();
            }

            ServerUrl = string.IsNullOrWhiteSpace(serverUrl)
                ? DhakaNetProtocolConstants.DefaultLocalServerUrl
                : serverUrl.Trim();

            _connecting = true;
            _cts = new CancellationTokenSource();
            _ws = new ClientWebSocket();
            _ = ConnectAndRunAsync(ServerUrl, _cts.Token);
        }

        public void SendPacketObject<T>(T packet)
        {
            if (packet == null || !IsConnected) return;
            string json = JsonUtility.ToJson(packet);
            _outboundJson.Enqueue(json);
            _ = FlushSendQueueAsync();
        }

        public void PumpMainThreadMessages()
        {
            while (_inboundJson.TryDequeue(out string json))
            {
                try
                {
                    DispatchSyntheticRawFrame(json);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[DhakaWebSocketTransport] Handler exception: {ex.Message}");
                }
            }
        }

        public void CloseImmediate()
        {
            _connecting = false;
            try
            {
                _cts?.Cancel();
            }
            catch { }

            if (_ws != null)
            {
                try
                {
                    if (_ws.State == WebSocketState.Open)
                    {
                        _ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Client Closing", CancellationToken.None).Wait(250);
                    }
                    _ws.Dispose();
                }
                catch { }
                _ws = null;
            }
        }

        private void Update()
        {
            _localTick++;
            PumpMainThreadMessages();
        }

        private void OnDestroy()
        {
            CloseImmediate();
            _sendLock.Dispose();
        }

        private async Task ConnectAndRunAsync(string url, CancellationToken token)
        {
            try
            {
                await _ws.ConnectAsync(new Uri(url), token);
                _connecting = false;
                _inboundJson.Enqueue("__INTERNAL_OPEN__");

                byte[] buffer = new byte[65536];
                var sb = new StringBuilder(4096);

                while (!token.IsCancellationRequested && _ws != null && _ws.State == WebSocketState.Open)
                {
                    sb.Clear();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            _inboundJson.Enqueue($"__INTERNAL_CLOSE__:{result.CloseStatusDescription ?? "Closed by server"}");
                            return;
                        }
                        sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    }
                    while (!result.EndOfMessage);

                    if (sb.Length > 0)
                    {
                        _inboundJson.Enqueue(sb.ToString());
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on clean close
            }
            catch (Exception ex)
            {
                _connecting = false;
                _inboundJson.Enqueue($"__INTERNAL_ERR__:{ex.Message}");
            }
            finally
            {
                _connecting = false;
            }
        }

        private async Task FlushSendQueueAsync()
        {
            if (_ws == null || _ws.State != WebSocketState.Open) return;
            await _sendLock.WaitAsync();
            try
            {
                while (_ws != null && _ws.State == WebSocketState.Open && _outboundJson.TryDequeue(out string json))
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(json);
                    await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts.Token);
                }
            }
            catch (Exception ex)
            {
                _inboundJson.Enqueue($"__INTERNAL_ERR__:{ex.Message}");
            }
            finally
            {
                _sendLock.Release();
            }
        }

        internal void DispatchSyntheticRawFrame(string json)
        {
            if (json == "__INTERNAL_OPEN__")
            {
                OnSocketOpened?.Invoke();
            }
            else if (json.StartsWith("__INTERNAL_CLOSE__:", StringComparison.Ordinal))
            {
                OnSocketClosed?.Invoke(json.Substring("__INTERNAL_CLOSE__:".Length));
            }
            else if (json.StartsWith("__INTERNAL_ERR__:", StringComparison.Ordinal))
            {
                OnSocketError?.Invoke(json.Substring("__INTERNAL_ERR__:".Length));
            }
            else
            {
                OnRawJsonMessage?.Invoke(json);
            }
        }
    }
}
