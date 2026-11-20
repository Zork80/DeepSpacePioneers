using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;

namespace Networking
{
    /// <summary>
    /// Central manager coordinating Server, Client, and LAN Discovery lifecycles.
    /// Provides main-thread action dispatching and command-line dedicated server initialization.
    /// </summary>
    public class NetworkManager : MonoBehaviour
    {
        public static NetworkManager Instance { get; private set; }

        public GameServer Server { get; private set; }
        public NetworkClient Client { get; private set; }
        public LanDiscoveryListener Discovery { get; private set; }

        public bool IsServerRunning => Server != null && Server.IsRunning;
        public bool IsClientConnected => Client != null && Client.IsConnected;
        public bool IsHost => Server != null && Server.IsRunning && !Server.IsDedicated;
        public bool IsDedicatedServer => Server != null && Server.IsRunning && Server.IsDedicated;
        public bool IsClient => IsClientConnected && !IsHost;
        public bool IsServer => IsServerRunning;

        public string LastConnectionError => Client?.LastError;

        public int LocalPlayerIndex => Client != null ? Client.LocalPlayerIndex : -1;

        public event Action<AgentBridge.GameStateObservationDTO> OnClientGameStateReceived;
        public event Action<AgentBridge.AgentResultDTO> OnClientActionResultReceived;

        private readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = 60;

            if (Instance == null)
            {
                GameObject go = new GameObject("NetworkManager");
                Instance = go.AddComponent<NetworkManager>();
                DontDestroyOnLoad(go);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            Discovery = new LanDiscoveryListener();

            CheckCommandLineFlags();
        }

        private void Update()
        {
            while (_mainThreadQueue.TryDequeue(out Action action))
            {
                try
                {
                    action?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[NetworkManager] Main thread dispatch error: {ex}");
                }
            }

            if (Server != null && Server.IsRunning)
            {
                Server.Update(Time.deltaTime);
            }
        }

        public void EnqueueMainThreadAction(Action action)
        {
            if (action == null) return;
            _mainThreadQueue.Enqueue(action);
        }

        private void CheckCommandLineFlags()
        {
            string[] args = Environment.GetCommandLineArgs();
            bool isServerFlag = false;
#if UNITY_SERVER
            isServerFlag = true;
#endif
            int port = 7777;
            string serverName = "Deep Space Pioneers Server";

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i].ToLowerInvariant();
                if (arg == "-server" || arg == "-dedicated-server" || Application.isBatchMode)
                {
                    isServerFlag = true;
                }
                else if (arg.StartsWith("-port:") && int.TryParse(arg.Substring(6), out int p))
                {
                    port = p;
                }
                else if (arg.StartsWith("-name:"))
                {
                    serverName = args[i].Substring(6);
                }
            }

            if (isServerFlag)
            {
                Debug.Log($"[NetworkManager] Command line dedicated server detected. Launching on port {port}...");
                StartDedicatedServer(serverName, port);
            }
        }

        public async Task<bool> StartHostAsync(string serverName, int tcpPort, string hostPlayerName)
        {
            DisconnectAll();

            Server = new GameServer(serverName, tcpPort, isDedicated: false);
            Server.Start();

            Client = new NetworkClient();
            Client.OnGameStateReceived += (s) => OnClientGameStateReceived?.Invoke(s);
            Client.OnActionResultReceived += (r) => OnClientActionResultReceived?.Invoke(r);
            bool connected = await Client.ConnectAsync("127.0.0.1", tcpPort, hostPlayerName);
            if (!connected)
            {
                Debug.LogError("[NetworkManager] Host client failed to connect to local server.");
                Server.Stop();
                Server = null;
                return false;
            }

            return true;
        }

        public void StartDedicatedServer(string serverName, int tcpPort)
        {
            DisconnectAll();
            Server = new GameServer(serverName, tcpPort, isDedicated: true);
            Server.Start();
        }

        public async Task<bool> ConnectToServerAsync(string hostIp, int tcpPort, string playerName)
        {
            DisconnectAll();

            Client = new NetworkClient();
            Client.OnGameStateReceived += (s) => OnClientGameStateReceived?.Invoke(s);
            Client.OnActionResultReceived += (r) => OnClientActionResultReceived?.Invoke(r);
            Client.OnGameEventReceived += HandleGameEventReceived;
            bool connected = await Client.ConnectAsync(hostIp, tcpPort, playerName);
            return connected;
        }

        private void HandleGameEventReceived(GameEventNotificationDTO evt)
        {
            if (evt != null && !string.IsNullOrEmpty(evt.Description))
            {
                RuntimeLog.WriteLine("[NetEvent] " + evt.Description);
                PlayerHandler.ShowToast(evt.Description, isWarning: false);
            }
        }

        public void StartDiscovery()
        {
            if (Discovery != null && !Discovery.IsListening)
            {
                Discovery.Start();
            }
        }

        public void StopDiscovery()
        {
            if (Discovery != null && Discovery.IsListening)
            {
                Discovery.Stop();
            }
        }

        public void DisconnectAll()
        {
            if (Client != null)
            {
                Client.Disconnect();
                Client = null;
            }

            if (Server != null)
            {
                Server.Stop();
                Server = null;
            }
        }

        public static string GetLocalIPAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    {
                        return ip.ToString();
                    }
                }
            }
            catch { }
            return "127.0.0.1";
        }

        private void OnApplicationQuit()
        {
            DisconnectAll();
            if (Discovery != null)
            {
                Discovery.Stop();
            }
        }
    }
}

