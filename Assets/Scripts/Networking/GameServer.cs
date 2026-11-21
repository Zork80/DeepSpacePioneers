using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using AgentBridge;
using UnityEngine;

namespace Networking
{
    public class ClientSession : IDisposable
    {
        public TcpClient Client { get; private set; }
        public NetworkStream Stream => Client?.GetStream();
        public int PlayerIndex { get; set; } = -1;
        public string PlayerName { get; set; } = "Player";
        public bool IsHost { get; set; }
        public bool IsSpectator { get; set; }
        public bool IsConnected => Client != null && Client.Connected;
        public CancellationTokenSource Cts { get; private set; } = new CancellationTokenSource();

        public ClientSession(TcpClient client, bool isHost = false)
        {
            Client = client;
            IsHost = isHost;
        }

        public async Task SendEnvelopeAsync(NetworkEnvelope envelope)
        {
            if (!IsConnected || Stream == null || envelope == null || Cts == null) return;
            try
            {
                string json = envelope.ToJson();
                byte[] framed = LengthPrefixedFraming.Frame(json);
                CancellationToken token = Cts != null ? Cts.Token : CancellationToken.None;
                await Stream.WriteAsync(framed, 0, framed.Length, token);
                await Stream.FlushAsync(token);
            }
            catch (Exception ex)
            {
                if (IsConnected && Cts != null)
                {
                    Debug.LogWarning($"[ClientSession] Send error to Player {PlayerIndex}: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            try
            {
                Cts?.Cancel();
                Cts?.Dispose();
                Cts = null;
                Client?.Close();
                Client = null;
            }
            catch { }
        }
    }

    /// <summary>
    /// Authoritative TCP Game Server for Deep Space Pioneers.
    /// Manages client sessions, lobby configuration, action dispatching, and Fog-of-War state replication.
    /// </summary>
    public class GameServer : IDisposable
    {
        public int Port { get; private set; }
        public string ServerName { get; private set; }
        public bool IsRunning { get; private set; }
        public bool IsDedicated { get; private set; }
        public bool IsGameStarted { get; private set; }

        private TcpListener _listener;
        private CancellationTokenSource _cts;
        private readonly List<ClientSession> _sessions = new List<ClientSession>();
        private readonly object _sessionLock = new object();

        private LanServerAnnouncer _announcer;
        private LobbyStateDTO _lobbyState;

        public event Action<LobbyStateDTO> OnLobbyChanged;
        public event Action<string> OnServerLog;

        public GameServer(string serverName = "Deep Space Pioneers Server", int port = 7777, bool isDedicated = false)
        {
            ServerName = serverName;
            Port = port;
            IsDedicated = isDedicated;
            InitLobby();
        }

        private void InitLobby()
        {
            _lobbyState = new LobbyStateDTO
            {
                serverName = ServerName,
                canStart = false,
                hostSlotIndex = 0,
                seed = UnityEngine.Random.Range(100000, 999999),
                useFixedSeed = false,
                slots = new List<LobbySlotDTO>()
            };

            for (int i = 0; i < 4; i++)
            {
                _lobbyState.slots.Add(new LobbySlotDTO
                {
                    slotIndex = i,
                    playerName = $"Spieler {i + 1}",
                    playerType = (i == 0 && !IsDedicated) ? "Human" : "AI",
                    aiDifficulty = "Normal",
                    isConnected = false,
                    isHost = i == 0 && !IsDedicated
                });
            }
        }

        public void Start()
        {
            if (IsRunning) return;
            IsRunning = true;
            _cts = new CancellationTokenSource();

            TriMap map = UnityEngine.Object.FindAnyObjectByType<TriMap>();
            if (map != null && map.CurrentBoardSeed != 0)
            {
                _lobbyState.seed = map.CurrentBoardSeed;
                _lobbyState.useFixedSeed = map.IsCurrentBoardSeedFixed;
            }

            try
            {
                _listener = new TcpListener(IPAddress.Any, Port);
                _listener.Start();

                Task.Run(() => AcceptClientsLoopAsync(_cts.Token));

                // Start LAN Announcer
                var announcement = new LanAnnouncementDTO
                {
                    ServerName = ServerName,
                    TcpPort = Port,
                    CurrentPlayers = 0,
                    MaxPlayers = 4,
                    State = "Lobby",
                    Version = "1.0.0"
                };
                _announcer = new LanServerAnnouncer(announcement);
                _announcer.Start();

                // Hook ExternalAgentBridge if available
                HookAgentBridge();

                Log($"Server started on TCP port {Port} (Dedicated={IsDedicated})");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameServer] Failed to start server: {ex.Message}");
                Stop();
            }
        }

        public void HookAgentBridge()
        {
            if (ExternalAgentBridge.Instance != null)
            {
                ExternalAgentBridge.Instance.OnStateObservationChanged -= HandleStateObservationChanged;
                ExternalAgentBridge.Instance.OnStateObservationChanged += HandleStateObservationChanged;
                ExternalAgentBridge.Instance.OnActionResult -= HandleActionResult;
                ExternalAgentBridge.Instance.OnActionResult += HandleActionResult;
            }
        }

        private void UnhookAgentBridge()
        {
            if (ExternalAgentBridge.Instance != null)
            {
                ExternalAgentBridge.Instance.OnStateObservationChanged -= HandleStateObservationChanged;
                ExternalAgentBridge.Instance.OnActionResult -= HandleActionResult;
            }
        }

        private async Task AcceptClientsLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && IsRunning)
            {
                try
                {
                    TcpClient tcpClient = await _listener.AcceptTcpClientAsync();
                    tcpClient.NoDelay = true; // Minimal latency
                    _ = HandleNewConnectionAsync(tcpClient, ct);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (!ct.IsCancellationRequested)
                    {
                        Log($"Accept error: {ex.Message}");
                    }
                }
            }
        }

        private async Task HandleNewConnectionAsync(TcpClient tcpClient, CancellationToken ct)
        {
            ClientSession session = null;
            try
            {
                // Wait for initial Connect envelope
                string initialMessage = await LengthPrefixedFraming.ReadMessageAsync(tcpClient.GetStream(), ct);
                if (string.IsNullOrEmpty(initialMessage))
                {
                    tcpClient.Close();
                    return;
                }

                NetworkEnvelope envelope = NetworkEnvelope.FromJson(initialMessage);
                if (envelope == null || envelope.Type != NetworkMessageType.Connect)
                {
                    tcpClient.Close();
                    return;
                }

                ConnectRequestDTO req = JsonUtility.FromJson<ConnectRequestDTO>(envelope.Payload) ?? new ConnectRequestDTO();

                int assignedSlot = -1;
                bool isHost = false;
                bool isSpectator = req.AsSpectator;
                string sessionName = !string.IsNullOrEmpty(req.PlayerName) ? req.PlayerName : (isSpectator ? "Spectator" : "Player");

                lock (_sessionLock)
                {
                    if (isSpectator)
                    {
                        assignedSlot = -1;
                        isHost = false;
                    }
                    else if (IsGameStarted)
                    {
                        // Check if reconnecting player matches slot
                        for (int i = 0; i < _lobbyState.slots.Count; i++)
                        {
                            if (_lobbyState.slots[i].playerName == req.PlayerName && !_lobbyState.slots[i].isConnected)
                            {
                                assignedSlot = i;
                                break;
                            }
                        }

                        if (assignedSlot == -1)
                        {
                            // Game in progress and no matching slot
                            _ = SendErrorAsync(tcpClient.GetStream(), "Spiel läuft bereits.");
                            tcpClient.Close();
                            return;
                        }
                    }
                    else
                    {
                        // Assign next free Human slot
                        for (int i = 0; i < _lobbyState.slots.Count; i++)
                        {
                            if (!_lobbyState.slots[i].isConnected && _lobbyState.slots[i].playerType == "Human")
                            {
                                assignedSlot = i;
                                break;
                            }
                        }

                        // If all human slots filled, convert an AI slot
                        if (assignedSlot == -1)
                        {
                            for (int i = 0; i < _lobbyState.slots.Count; i++)
                            {
                                if (!_lobbyState.slots[i].isConnected)
                                {
                                    assignedSlot = i;
                                    _lobbyState.slots[i].playerType = "Human";
                                    break;
                                }
                            }
                        }

                        if (assignedSlot == -1)
                        {
                            _ = SendErrorAsync(tcpClient.GetStream(), "Server ist voll (maximal 4 Spieler).");
                            tcpClient.Close();
                            return;
                        }

                        isHost = assignedSlot == 0 && !IsDedicated;
                        _lobbyState.slots[assignedSlot].playerName = !string.IsNullOrEmpty(req.PlayerName) ? req.PlayerName : $"Spieler {assignedSlot + 1}";
                        _lobbyState.slots[assignedSlot].isConnected = true;
                        _lobbyState.slots[assignedSlot].isHost = isHost;
                        sessionName = _lobbyState.slots[assignedSlot].playerName;
                    }

                    session = new ClientSession(tcpClient, isHost)
                    {
                        PlayerIndex = assignedSlot,
                        PlayerName = sessionName,
                        IsSpectator = isSpectator
                    };
                    _sessions.Add(session);
                    RefreshHostPrivileges();
                    isHost = session.IsHost;
                }

                Log(isSpectator
                    ? $"Spectator '{session.PlayerName}' connected (Mode={_lobbyState.spectatorMode})"
                    : $"Player '{session.PlayerName}' connected and assigned slot {assignedSlot} (Host={isHost})");

                // Send Welcome
                var welcome = new WelcomeResponseDTO
                {
                    AssignedPlayerIndex = assignedSlot,
                    IsHost = isHost,
                    IsSpectator = isSpectator,
                    SpectatorMode = _lobbyState.spectatorMode,
                    ServerVersion = "1.0.0",
                    ServerName = ServerName
                };
                await session.SendEnvelopeAsync(new NetworkEnvelope(NetworkMessageType.Welcome, JsonUtility.ToJson(welcome), assignedSlot));

                UpdateLobbyState();
                UpdateAnnouncer();

                // If game already started, handle player transition or spectator mid-game sync
                if (IsGameStarted)
                {
                    if (isSpectator)
                    {
                        var startedDto = new GameStartedDTO
                        {
                            Seed = _lobbyState.seed,
                            HostPlayerIndex = _lobbyState.hostSlotIndex
                        };
                        await session.SendEnvelopeAsync(new NetworkEnvelope(NetworkMessageType.GameStarted, JsonUtility.ToJson(startedDto)));
                        SendSnapshotToSession(session);
                    }
                    else
                    {
                        NetworkManager.Instance?.EnqueueMainThreadAction(() =>
                        {
                            TriMap map = TriMapInstance;
                            if (map != null && map.Players != null && assignedSlot >= 0 && assignedSlot < map.Players.Length)
                            {
                                Player p = map.Players[assignedSlot];
                                if (p != null)
                                {
                                    p.Type = isHost ? Player.PlayerType.LOCAL : Player.PlayerType.REMOTE;
                                    Log($"Player '{p.PlayerName}' (Slot {assignedSlot}) transitioned to human control ({p.Type}).");
                                }
                            }
                            if (assignedSlot >= 0 && assignedSlot < GameSetupConfig.Slots.Length)
                            {
                                GameSetupConfig.Slots[assignedSlot].Type = isHost ? Player.PlayerType.LOCAL : Player.PlayerType.REMOTE;
                            }
                        });
                        SendSnapshotToSession(session);
                    }
                }

                // Read loop for this client
                await ClientReceiveLoopAsync(session, session.Cts.Token);
            }
            catch (Exception ex)
            {
                Log($"Session error for {session?.PlayerName ?? "Client"}: {ex.Message}");
            }
            finally
            {
                if (session != null)
                {
                    HandleClientDisconnected(session);
                }
            }
        }

        private async Task ClientReceiveLoopAsync(ClientSession session, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && session.IsConnected)
            {
                string json = await LengthPrefixedFraming.ReadMessageAsync(session.Stream, ct);
                if (json == null) break; // Disconnected

                NetworkEnvelope envelope = NetworkEnvelope.FromJson(json);
                if (envelope == null) continue;

                HandleIncomingEnvelope(session, envelope);
            }
        }

        private void HandleIncomingEnvelope(ClientSession session, NetworkEnvelope envelope)
        {
            switch (envelope.Type)
            {
                case NetworkMessageType.Ping:
                    _ = session.SendEnvelopeAsync(new NetworkEnvelope(NetworkMessageType.Pong, string.Empty, session.PlayerIndex));
                    break;

                case NetworkMessageType.SetSlot:
                    if (session.IsHost || IsDedicated)
                    {
                        SetSlotRequestDTO slotReq = JsonUtility.FromJson<SetSlotRequestDTO>(envelope.Payload);
                        if (slotReq != null && slotReq.SlotIndex >= 0 && slotReq.SlotIndex < _lobbyState.slots.Count)
                        {
                            var slot = _lobbyState.slots[slotReq.SlotIndex];
                            if (!string.IsNullOrEmpty(slotReq.PlayerName)) slot.playerName = slotReq.PlayerName;
                            if (!string.IsNullOrEmpty(slotReq.PlayerType)) slot.playerType = slotReq.PlayerType;
                            if (!string.IsNullOrEmpty(slotReq.AiDifficulty)) slot.aiDifficulty = slotReq.AiDifficulty;
                            UpdateLobbyState();
                        }
                    }
                    break;

                case NetworkMessageType.SetSpectatorMode:
                    if (session.IsHost || IsDedicated)
                    {
                        SetSpectatorModeRequestDTO modeReq = JsonUtility.FromJson<SetSpectatorModeRequestDTO>(envelope.Payload);
                        if (modeReq != null)
                        {
                            _lobbyState.spectatorMode = modeReq.SpectatorMode == "Caster" ? "Caster" : "Fair";
                            Log($"Spectator mode set to '{_lobbyState.spectatorMode}' by '{session.PlayerName}'.");
                            UpdateLobbyState();
                            if (IsGameStarted)
                            {
                                lock (_sessionLock)
                                {
                                    for (int i = 0; i < _sessions.Count; i++)
                                    {
                                        if (_sessions[i].IsSpectator) SendSnapshotToSession(_sessions[i]);
                                    }
                                }
                            }
                        }
                    }
                    break;

                case NetworkMessageType.StartGame:
                    if (session.IsHost || IsDedicated)
                    {
                        StartGameRequestDTO startReq = JsonUtility.FromJson<StartGameRequestDTO>(envelope.Payload) ?? new StartGameRequestDTO();
                        StartGameInternal(startReq);
                    }
                    break;

                case NetworkMessageType.PlayerAction:
                    HandlePlayerAction(session, envelope);
                    break;
            }
        }

        private void HandlePlayerAction(ClientSession session, NetworkEnvelope envelope)
        {
            if (session.IsSpectator || session.PlayerIndex < 0)
            {
                Log($"Ignored PlayerAction from spectator '{session.PlayerName}'.");
                return;
            }

            AgentActionDTO action = JsonUtility.FromJson<AgentActionDTO>(envelope.Payload);
            if (action == null) return;

            _turnTimer = DefaultTurnTimeout;

            if (action.action == "ResetTurnTimer" || action.action == "KeepAlive")
            {
                Log($"Player '{session.PlayerName}' reset turn inactivity timer.");
                return;
            }

            if (action.action == "ForceAiTakeover")
            {
                if (session.IsHost || IsDedicated || session.PlayerIndex == _lastActivePlayerIndex)
                {
                    NetworkManager.Instance?.EnqueueMainThreadAction(() =>
                    {
                        TriMap map = UnityEngine.Object.FindAnyObjectByType<TriMap>();
                        if (map != null && map.Players != null)
                        {
                            int humanCount = map.Players.Count(p => p != null && p.Type != Player.PlayerType.AI);
                            if (humanCount <= 1)
                            {
                                Log($"Ignored ForceAiTakeover for '{map.CurrentPlayer?.PlayerName}': Last human player cannot be replaced by AI.");
                                return;
                            }

                            Player target = map.CurrentPlayer;
                            if (target != null && target.Type != Player.PlayerType.AI)
                            {
                                target.Type = Player.PlayerType.AI;
                                target.Difficulty = Player.AiDifficulty.Normal;
                                Log($"Player '{session.PlayerName}' triggered AI takeover for player '{target.PlayerName}'.");
                                Broadcast(new NetworkEnvelope(NetworkMessageType.GameEvent, $"Zug von {target.PlayerName} an die KI übergeben."));
                                map.GetComponent<AiPlayerController>()?.TriggerAiCheck();
                            }
                        }
                    });
                }
                return;
            }

            // Route execution to Unity Main Thread
            NetworkManager.Instance?.EnqueueMainThreadAction(() =>
            {
                if (ExternalAgentBridge.Instance != null)
                {
                    ExternalAgentBridge.Instance.DispatchAction(action);
                }
            });
        }

        public void StartGameInternal(StartGameRequestDTO startReq)
        {
            if (IsGameStarted) return;
            IsGameStarted = true;

            _lobbyState.seed = startReq.Seed != 0 ? startReq.Seed : _lobbyState.seed;
            _lobbyState.useFixedSeed = startReq.UseFixedSeed;

            Log($"Starting game with seed {_lobbyState.seed} (Fixed={_lobbyState.useFixedSeed})...");

            // Broadcast GameStarted to all clients
            var startedDto = new GameStartedDTO
            {
                Seed = _lobbyState.seed,
                HostPlayerIndex = _lobbyState.hostSlotIndex
            };
            Broadcast(new NetworkEnvelope(NetworkMessageType.GameStarted, JsonUtility.ToJson(startedDto)));

            // Launch on server's TriMap on main thread
            NetworkManager.Instance?.EnqueueMainThreadAction(() =>
            {
                TriMap map = UnityEngine.Object.FindAnyObjectByType<TriMap>();
                if (map != null)
                {
                    ApplyLobbyConfigurationToTriMap(map);
                    map.StartGameFromSetup();
                }
            });

            UpdateAnnouncer("InGame");
        }

        private void ApplyLobbyConfigurationToTriMap(TriMap map)
        {
            if (map == null || _lobbyState == null) return;

            if (map.PlayerTypes == null || map.PlayerTypes.Length != 4)
            {
                map.PlayerTypes = new Player.PlayerType[4];
            }

            for (int i = 0; i < _lobbyState.slots.Count && i < 4; i++)
            {
                var slot = _lobbyState.slots[i];
                Player.PlayerType pType;
                if (slot.playerType == "AI")
                {
                    pType = Player.PlayerType.AI;
                }
                else
                {
                    pType = (i == 0 && !IsDedicated) ? Player.PlayerType.LOCAL : Player.PlayerType.REMOTE;
                }
                map.PlayerTypes[i] = pType;

                if (map.Players != null && i < map.Players.Length && map.Players[i] != null)
                {
                    map.Players[i].Type = pType;
                    map.Players[i].PlayerName = slot.playerName;
                    if (slot.playerType == "AI")
                    {
                        Enum.TryParse(slot.aiDifficulty, out Player.AiDifficulty diff);
                        map.Players[i].Difficulty = diff;
                    }
                }

                if (i < GameSetupConfig.Slots.Length)
                {
                    GameSetupConfig.Slots[i].Name = slot.playerName;
                    GameSetupConfig.Slots[i].Type = pType;
                    if (slot.playerType == "AI")
                    {
                        Enum.TryParse(slot.aiDifficulty, out Player.AiDifficulty diff);
                        GameSetupConfig.Slots[i].Difficulty = diff;
                    }
                }
            }

            if (_lobbyState.useFixedSeed)
            {
                map.CurrentBoardSeed = _lobbyState.seed;
            }
        }

        private void HandleStateObservationChanged(GameStateObservationDTO observation)
        {
            if (!IsRunning || !IsGameStarted || observation == null) return;

            bool maskSpectator = _lobbyState.spectatorMode != "Caster";

            // Broadcast Fog-of-War masked snapshots to each client session individually
            lock (_sessionLock)
            {
                for (int i = 0; i < _sessions.Count; i++)
                {
                    ClientSession session = _sessions[i];
                    // Skip local Host session - Host state is already authoritative in memory
                    if (session.IsConnected && !session.IsHost && (session.PlayerIndex > 0 || session.IsSpectator))
                    {
                        int recipient = session.IsSpectator ? -1 : session.PlayerIndex;
                        GameStateObservationDTO masked = observation.CreateMaskedCopy(recipient, maskSpectator);
                        masked.spectatorMode = _lobbyState.spectatorMode;
                        string payload = JsonUtility.ToJson(masked);
                        _ = session.SendEnvelopeAsync(new NetworkEnvelope(NetworkMessageType.GameStateSnapshot, payload, -1));
                    }
                }
            }
        }

        private void HandleActionResult(AgentResultDTO result)
        {
            if (!IsRunning || result == null) return;
            string payload = JsonUtility.ToJson(result);
            Broadcast(new NetworkEnvelope(NetworkMessageType.ActionResult, payload, -1, result.actionId));
        }

        public void SendSnapshotToSession(ClientSession session)
        {
            if (session == null || !session.IsConnected || session.IsHost || (!session.IsSpectator && session.PlayerIndex <= 0)) return;

            bool maskSpectator = _lobbyState.spectatorMode != "Caster";

            NetworkManager.Instance?.EnqueueMainThreadAction(() =>
            {
                if (ExternalAgentBridge.Instance != null)
                {
                    GameStateObservationDTO obs = ExternalAgentBridge.Instance.BuildObservation();
                    if (obs != null)
                    {
                        int recipient = session.IsSpectator ? -1 : session.PlayerIndex;
                        GameStateObservationDTO masked = obs.CreateMaskedCopy(recipient, maskSpectator);
                        masked.spectatorMode = _lobbyState.spectatorMode;
                        string payload = JsonUtility.ToJson(masked);
                        _ = session.SendEnvelopeAsync(new NetworkEnvelope(NetworkMessageType.GameStateSnapshot, payload, -1));
                    }
                }
            });
        }

        public void BroadcastEvent(string eventType, string description, int playerIndex = -1, string detailsJson = "")
        {
            var eventDto = new GameEventNotificationDTO
            {
                EventType = eventType,
                PlayerIndex = playerIndex,
                Description = description,
                DetailsJson = detailsJson
            };
            Broadcast(new NetworkEnvelope(NetworkMessageType.GameEvent, JsonUtility.ToJson(eventDto), playerIndex));
        }

        public void BroadcastToast(string message, bool isWarning = false, float duration = 4.5f)
        {
            if (!IsRunning || string.IsNullOrEmpty(message)) return;
            var dto = new ToastNotificationDTO
            {
                message = message,
                isWarning = isWarning,
                duration = duration
            };
            Broadcast(new NetworkEnvelope(NetworkMessageType.ToastMessage, JsonUtility.ToJson(dto)));
        }

        public void Broadcast(NetworkEnvelope envelope)
        {
            lock (_sessionLock)
            {
                for (int i = 0; i < _sessions.Count; i++)
                {
                    if (_sessions[i].IsConnected)
                    {
                        _ = _sessions[i].SendEnvelopeAsync(envelope);
                    }
                }
            }
        }

        private void UpdateLobbyState()
        {
            // Evaluate canStart & spectatorCount
            int humanConnected = 0;
            for (int i = 0; i < _lobbyState.slots.Count; i++)
            {
                if (_lobbyState.slots[i].isConnected) humanConnected++;
            }
            int spectators = 0;
            lock (_sessionLock)
            {
                for (int i = 0; i < _sessions.Count; i++)
                {
                    if (_sessions[i].IsConnected && _sessions[i].IsSpectator) spectators++;
                }
            }
            _lobbyState.spectatorCount = spectators;
            _lobbyState.canStart = humanConnected >= (IsDedicated ? 1 : 1);

            string json = JsonUtility.ToJson(_lobbyState);
            Broadcast(new NetworkEnvelope(NetworkMessageType.LobbyUpdate, json, -1));

            NetworkManager.Instance?.EnqueueMainThreadAction(() => OnLobbyChanged?.Invoke(_lobbyState));
        }

        private void UpdateAnnouncer(string state = null)
        {
            if (_announcer != null)
            {
                int count = 0;
                lock (_sessionLock)
                {
                    for (int i = 0; i < _sessions.Count; i++)
                    {
                        if (_sessions[i].IsConnected) count++;
                    }
                }
                _announcer.UpdatePlayerCount(count, state);
            }
        }

        private TriMap _cachedTriMap;
        public TriMap TriMapInstance
        {
            get
            {
                if (_cachedTriMap == null)
                {
                    _cachedTriMap = UnityEngine.Object.FindAnyObjectByType<TriMap>();
                }
                return _cachedTriMap;
            }
        }

        public const float DefaultTurnTimeout = 180f;
        public const float WarningCountdownThreshold = 90f;
        private float _turnTimer = DefaultTurnTimeout;
        private int _lastActivePlayerIndex = -1;
        private GameTurnPhase _lastActivePhase = GameTurnPhase.Production;

        public void Update(float deltaTime)
        {
            if (!IsRunning || !IsGameStarted) return;

            TriMap map = TriMapInstance;
            if (map == null || map.IsGameOver) return;

            // Do not run turn timeout during the initial founding phase!
            if (map.CurrentFoundingPhase != FoundingPhase.Complete)
            {
                _turnTimer = DefaultTurnTimeout;
                _lastActivePlayerIndex = -1;
                return;
            }

            // Reset timer on turn or phase transition
            if (map.CurrentPlayerIndex != _lastActivePlayerIndex || map.CurrentPhase != _lastActivePhase)
            {
                _lastActivePlayerIndex = map.CurrentPlayerIndex;
                _lastActivePhase = map.CurrentPhase;
                _turnTimer = DefaultTurnTimeout;
            }

            Player current = map.CurrentPlayer;
            if (current != null && current.Type != Player.PlayerType.AI)
            {
                int humanCount = map.Players != null ? map.Players.Count(p => p != null && p.Type != Player.PlayerType.AI) : 0;
                if (humanCount <= 1)
                {
                    _turnTimer = DefaultTurnTimeout;
                    return;
                }

                _turnTimer -= deltaTime;
                if (_turnTimer <= 0f)
                {
                    _turnTimer = DefaultTurnTimeout;
                    Log($"Turn timer expired for player '{current.PlayerName}' (Slot {map.CurrentPlayerIndex}). Converting to AI.");
                    current.Type = Player.PlayerType.AI;
                    current.Difficulty = Player.AiDifficulty.Normal;
                    Broadcast(new NetworkEnvelope(NetworkMessageType.GameEvent, $"Zeitlimit überschritten: {current.PlayerName} wird nun von der KI gesteuert."));
                    map.GetComponent<AiPlayerController>()?.TriggerAiCheck();
                }
            }
        }

        public void RefreshHostPrivileges()
        {
            lock (_sessionLock)
            {
                if (!IsDedicated)
                {
                    _lobbyState.hostSlotIndex = 0;
                    for (int i = 0; i < _lobbyState.slots.Count; i++)
                    {
                        _lobbyState.slots[i].isHost = (i == 0);
                    }
                    foreach (var s in _sessions)
                    {
                        s.IsHost = (s.PlayerIndex == 0);
                    }
                    return;
                }

                // Dedicated server: lowest connected slot is Host / Room Leader
                ClientSession newHost = null;
                int lowestSlot = int.MaxValue;
                foreach (var s in _sessions)
                {
                    if (s.PlayerIndex >= 0 && s.PlayerIndex < lowestSlot)
                    {
                        lowestSlot = s.PlayerIndex;
                        newHost = s;
                    }
                }

                int newHostIndex = newHost != null ? newHost.PlayerIndex : -1;
                _lobbyState.hostSlotIndex = newHostIndex;

                for (int i = 0; i < _lobbyState.slots.Count; i++)
                {
                    _lobbyState.slots[i].isHost = (i == newHostIndex);
                }

                foreach (var s in _sessions)
                {
                    bool wasHost = s.IsHost;
                    s.IsHost = (s == newHost);
                    if (!wasHost && s.IsHost)
                    {
                        Log($"[HostMigration] Room leader assigned to '{s.PlayerName}' (Slot {s.PlayerIndex})");
                        var welcome = new WelcomeResponseDTO
                        {
                            AssignedPlayerIndex = s.PlayerIndex,
                            IsHost = true,
                            ServerVersion = "1.0.0",
                            ServerName = ServerName
                        };
                        _ = s.SendEnvelopeAsync(new NetworkEnvelope(NetworkMessageType.Welcome, JsonUtility.ToJson(welcome), s.PlayerIndex));
                    }
                }
            }
        }

        private void HandleClientDisconnected(ClientSession session)
        {
            lock (_sessionLock)
            {
                _sessions.Remove(session);
                if (session.PlayerIndex >= 0 && session.PlayerIndex < _lobbyState.slots.Count)
                {
                    _lobbyState.slots[session.PlayerIndex].isConnected = false;
                }
            }

            Log(session.IsSpectator
                ? $"Spectator '{session.PlayerName}' disconnected"
                : $"Player '{session.PlayerName}' (Slot {session.PlayerIndex}) disconnected");
            session.Dispose();

            if (session.IsSpectator)
            {
                if (!IsGameStarted) UpdateLobbyState();
                UpdateAnnouncer();
                return;
            }

            if (!IsGameStarted)
            {
                RefreshHostPrivileges();
                UpdateLobbyState();
            }
            else
            {
                // In-game disconnect: convert player to AI and advance if it was their turn
                NetworkManager.Instance?.EnqueueMainThreadAction(() =>
                {
                    TriMap map = UnityEngine.Object.FindAnyObjectByType<TriMap>();
                    if (map != null && map.Players != null && session.PlayerIndex >= 0 && session.PlayerIndex < map.Players.Length)
                    {
                        Player p = map.Players[session.PlayerIndex];
                        if (p != null)
                        {
                            p.Type = Player.PlayerType.AI;
                            p.Difficulty = Player.AiDifficulty.Normal;
                            Log($"Player '{p.PlayerName}' (Slot {session.PlayerIndex}) converted to AI due to disconnect.");
                            Broadcast(new NetworkEnvelope(NetworkMessageType.GameEvent, $"Spieler {p.PlayerName} hat die Verbindung getrennt. Eine KI hat übernommen."));
                            if (map.CurrentPlayerIndex == session.PlayerIndex)
                            {
                                map.GetComponent<AiPlayerController>()?.TriggerAiCheck();
                            }
                        }
                    }
                });

                RefreshHostPrivileges();
            }
            UpdateAnnouncer();
        }

        private static async Task SendErrorAsync(NetworkStream stream, string error)
        {
            try
            {
                var env = new NetworkEnvelope(NetworkMessageType.Error, error);
                byte[] bytes = LengthPrefixedFraming.Frame(env.ToJson());
                await stream.WriteAsync(bytes, 0, bytes.Length);
            }
            catch { }
        }

        private void Log(string msg)
        {
            Debug.Log($"[GameServer] {msg}");
            NetworkManager.Instance?.EnqueueMainThreadAction(() => OnServerLog?.Invoke(msg));
        }

        public void Stop()
        {
            if (!IsRunning) return;
            IsRunning = false;
            _cachedTriMap = null;

            UnhookAgentBridge();

            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }

            if (_announcer != null)
            {
                _announcer.Stop();
                _announcer = null;
            }

            lock (_sessionLock)
            {
                for (int i = 0; i < _sessions.Count; i++)
                {
                    _sessions[i].Dispose();
                }
                _sessions.Clear();
            }

            if (_listener != null)
            {
                try
                {
                    _listener.Stop();
                }
                catch { }
                _listener = null;
            }

            Log("Server stopped.");
        }

        public void Dispose()
        {
            Stop();
        }
    }
}

