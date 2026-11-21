using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using AgentBridge;
using UnityEngine;

namespace Networking
{
    /// <summary>
    /// Network Client connecting to a Deep Space Pioneers authoritative Game Server.
    /// Handles asynchronous socket streaming, action forwarding, and event dispatching to the Unity Main Thread.
    /// </summary>
    public class NetworkClient : IDisposable
    {
        public bool IsConnected => _client != null && _client.Connected && !_isDisposed;
        public int LocalPlayerIndex { get; private set; } = -1;
        public bool IsHost { get; private set; }
        public bool IsSpectator { get; private set; }
        public string SpectatorMode { get; private set; } = "Fair";
        public string ServerName { get; private set; }
        public string PlayerName { get; private set; } = "Player";
        public string LastError { get; private set; }
        public GameStateObservationDTO LatestState { get; private set; }

        private TcpClient _client;
        private NetworkStream _stream;
        private CancellationTokenSource _cts;
        private bool _isDisposed;
        private readonly object _sendLock = new object();

        // Events dispatched to Main Thread via NetworkManager
        public event Action<WelcomeResponseDTO> OnConnected;
        public event Action<string> OnDisconnected;
        public event Action<LobbyStateDTO> OnLobbyUpdated;
        public event Action<GameStartedDTO> OnGameStarted;
        public event Action<GameStateObservationDTO> OnGameStateReceived;
        public event Action<AgentResultDTO> OnActionResultReceived;
        public event Action<GameEventNotificationDTO> OnGameEventReceived;
        public event Action<string> OnErrorReceived;

        public async Task<bool> ConnectAsync(string host, int port, string playerName, bool asSpectator = false)
        {
            Disconnect();
            _isDisposed = false;
            IsSpectator = asSpectator;
            PlayerName = playerName ?? string.Empty;
            _cts = new CancellationTokenSource();

            try
            {
                _client = new TcpClient();
                _client.NoDelay = true;

                Debug.Log($"[NetworkClient] Connecting to {host}:{port} (Spectator={asSpectator})...");
                await _client.ConnectAsync(host, port);
                _stream = _client.GetStream();

                // Send initial Connect handshake
                var connectDto = new ConnectRequestDTO
                {
                    PlayerName = PlayerName,
                    ClientVersion = "1.0.0",
                    AsSpectator = asSpectator
                };
                await SendEnvelopeAsync(new NetworkEnvelope(NetworkMessageType.Connect, JsonUtility.ToJson(connectDto)));

                // Start background receive loop
                _ = Task.Run(() => ReceiveLoopAsync(_cts.Token));
                LastError = null;
                return true;
            }
            catch (System.Net.Sockets.SocketException sex)
            {
                LastError = sex.SocketErrorCode == System.Net.Sockets.SocketError.ConnectionRefused
                    ? "Kein Server unter dieser Adresse erreichbar (Connection refused)."
                    : $"Netzwerkfehler: {sex.SocketErrorCode}";
                Debug.LogWarning($"[NetworkClient] Connection to {host}:{port} failed: {sex.SocketErrorCode} ({sex.Message})");
                Disconnect();
                return false;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Debug.LogWarning($"[NetworkClient] Connection to {host}:{port} failed: {ex.Message}");
                Disconnect();
                return false;
            }
        }

        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested && IsConnected)
                {
                    string json = await LengthPrefixedFraming.ReadMessageAsync(_stream, ct);
                    if (json == null)
                    {
                        // Clean disconnect from server
                        break;
                    }

                    NetworkEnvelope envelope = NetworkEnvelope.FromJson(json);
                    if (envelope != null)
                    {
                        DispatchEnvelope(envelope);
                    }
                }
            }
            catch (Exception ex)
            {
                if (!ct.IsCancellationRequested)
                {
                    Debug.LogWarning($"[NetworkClient] Receive loop exception: {ex.Message}");
                }
            }
            finally
            {
                NetworkManager.Instance?.EnqueueMainThreadAction(() => OnDisconnected?.Invoke("Verbindung zum Server getrennt."));
            }
        }

        private void DispatchEnvelope(NetworkEnvelope envelope)
        {
            switch (envelope.Type)
            {
                case NetworkMessageType.Welcome:
                    WelcomeResponseDTO welcome = JsonUtility.FromJson<WelcomeResponseDTO>(envelope.Payload);
                    if (welcome != null)
                    {
                        LocalPlayerIndex = welcome.AssignedPlayerIndex;
                        IsHost = welcome.IsHost;
                        IsSpectator = welcome.IsSpectator || welcome.AssignedPlayerIndex < 0;
                        if (!string.IsNullOrEmpty(welcome.SpectatorMode)) SpectatorMode = welcome.SpectatorMode;
                        ServerName = welcome.ServerName;
                        NetworkManager.Instance?.EnqueueMainThreadAction(() => OnConnected?.Invoke(welcome));
                    }
                    break;

                case NetworkMessageType.LobbyUpdate:
                    LobbyStateDTO lobby = JsonUtility.FromJson<LobbyStateDTO>(envelope.Payload);
                    if (lobby != null)
                    {
                        if (!string.IsNullOrEmpty(lobby.spectatorMode)) SpectatorMode = lobby.spectatorMode;
                        NetworkManager.Instance?.EnqueueMainThreadAction(() => OnLobbyUpdated?.Invoke(lobby));
                    }
                    break;

                case NetworkMessageType.GameStarted:
                    GameStartedDTO started = JsonUtility.FromJson<GameStartedDTO>(envelope.Payload);
                    if (started != null)
                    {
                        NetworkManager.Instance?.EnqueueMainThreadAction(() => OnGameStarted?.Invoke(started));
                    }
                    break;

                case NetworkMessageType.GameStateSnapshot:
                    GameStateObservationDTO state = JsonUtility.FromJson<GameStateObservationDTO>(envelope.Payload);
                    if (state != null)
                    {
                        if (!string.IsNullOrEmpty(state.spectatorMode)) SpectatorMode = state.spectatorMode;
                        LatestState = state;
                        NetworkManager.Instance?.EnqueueMainThreadAction(() => OnGameStateReceived?.Invoke(state));
                    }
                    break;

                case NetworkMessageType.ActionResult:
                    AgentResultDTO result = JsonUtility.FromJson<AgentResultDTO>(envelope.Payload);
                    if (result != null)
                    {
                        NetworkManager.Instance?.EnqueueMainThreadAction(() => OnActionResultReceived?.Invoke(result));
                    }
                    break;

                case NetworkMessageType.ToastMessage:
                    ToastNotificationDTO toastDto = JsonUtility.FromJson<ToastNotificationDTO>(envelope.Payload);
                    if (toastDto != null && !string.IsNullOrEmpty(toastDto.message))
                    {
                        NetworkManager.Instance?.EnqueueMainThreadAction(() =>
                        {
                            ToastNotificationUI.Show(toastDto.message, toastDto.isWarning, toastDto.duration);
                        });
                    }
                    break;

                case NetworkMessageType.GameEvent:
                    string evtText = string.Empty;
                    GameEventNotificationDTO gameEvt = null;
                    if (!string.IsNullOrEmpty(envelope.Payload))
                    {
                        if (envelope.Payload.TrimStart().StartsWith("{"))
                        {
                            try
                            {
                                gameEvt = JsonUtility.FromJson<GameEventNotificationDTO>(envelope.Payload);
                                evtText = gameEvt?.Description;
                            }
                            catch { }
                        }
                        if (string.IsNullOrEmpty(evtText))
                        {
                            evtText = envelope.Payload;
                        }
                    }
                    if (gameEvt == null && !string.IsNullOrEmpty(evtText))
                    {
                        gameEvt = new GameEventNotificationDTO { Description = evtText };
                    }
                    if (gameEvt != null)
                    {
                        NetworkManager.Instance?.EnqueueMainThreadAction(() => OnGameEventReceived?.Invoke(gameEvt));
                    }
                    if (!string.IsNullOrEmpty(evtText))
                    {
                        NetworkManager.Instance?.EnqueueMainThreadAction(() =>
                        {
                            ToastNotificationUI.Show(evtText, isWarning: false, duration: 4.5f);
                        });
                    }
                    break;

                case NetworkMessageType.Error:
                    string err = envelope.Payload;
                    NetworkManager.Instance?.EnqueueMainThreadAction(() => OnErrorReceived?.Invoke(err));
                    break;
            }
        }

        public async Task SendEnvelopeAsync(NetworkEnvelope envelope)
        {
            if (!IsConnected || _stream == null) return;

            byte[] framed;
            lock (_sendLock)
            {
                string json = envelope.ToJson();
                framed = LengthPrefixedFraming.Frame(json);
            }

            try
            {
                await _stream.WriteAsync(framed, 0, framed.Length);
                await _stream.FlushAsync();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NetworkClient] Send error: {ex.Message}");
            }
        }

        public void SendAction(AgentActionDTO action)
        {
            if (action == null) return;
            if (string.IsNullOrEmpty(action.actionId))
            {
                action.actionId = Guid.NewGuid().ToString("N");
            }

            var env = new NetworkEnvelope(NetworkMessageType.PlayerAction, JsonUtility.ToJson(action), LocalPlayerIndex, action.actionId);
            _ = SendEnvelopeAsync(env);
        }

        public void SendSetSlot(int slotIndex, string playerName, string playerType, string aiDifficulty, int colorIndex = -1)
        {
            var req = new SetSlotRequestDTO
            {
                SlotIndex = slotIndex,
                PlayerName = playerName,
                PlayerType = playerType,
                AiDifficulty = aiDifficulty,
                ColorIndex = colorIndex
            };
            var env = new NetworkEnvelope(NetworkMessageType.SetSlot, JsonUtility.ToJson(req), LocalPlayerIndex);
            _ = SendEnvelopeAsync(env);
        }

        public void SendSetSpectatorMode(string spectatorMode)
        {
            var req = new SetSpectatorModeRequestDTO
            {
                SpectatorMode = spectatorMode == "Caster" ? "Caster" : "Fair"
            };
            var env = new NetworkEnvelope(NetworkMessageType.SetSpectatorMode, JsonUtility.ToJson(req), LocalPlayerIndex);
            _ = SendEnvelopeAsync(env);
        }

        public void SendStartGame(int seed, bool useFixedSeed)
        {
            var req = new StartGameRequestDTO
            {
                Seed = seed,
                UseFixedSeed = useFixedSeed
            };
            var env = new NetworkEnvelope(NetworkMessageType.StartGame, JsonUtility.ToJson(req), LocalPlayerIndex);
            _ = SendEnvelopeAsync(env);
        }

        public void Disconnect()
        {
            _isDisposed = true;
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }

            if (_stream != null)
            {
                try { _stream.Close(); } catch { }
                _stream = null;
            }

            if (_client != null)
            {
                try { _client.Close(); } catch { }
                _client = null;
            }

            LocalPlayerIndex = -1;
            IsHost = false;
            IsSpectator = false;
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}

