using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Networking
{
    [Serializable]
    public class LanAnnouncementDTO
    {
        public string ServerName = "Deep Space Pioneers Server";
        public int TcpPort = 7777;
        public int CurrentPlayers = 1;
        public int MaxPlayers = 4;
        public string State = "Lobby"; // "Lobby" or "InGame"
        public string Version = "1.0.0";
    }

    public class DiscoveredServerInfo
    {
        public LanAnnouncementDTO Announcement;
        public string RemoteIp;
        public int TcpPort => Announcement != null ? Announcement.TcpPort : 7777;
        public DateTime LastSeenUtc;

        public string EndpointString => $"{RemoteIp}:{TcpPort}";
        public bool IsExpired => (DateTime.UtcNow - LastSeenUtc).TotalSeconds > 4.5;
    }

    /// <summary>
    /// Periodically broadcasts UDP announcement packets across the LAN to announce an active server.
    /// </summary>
    public class LanServerAnnouncer : IDisposable
    {
        public const int DiscoveryPort = 7776;
        private UdpClient _udpClient;
        private CancellationTokenSource _cts;
        private LanAnnouncementDTO _announcement;
        private readonly object _lock = new object();

        public LanServerAnnouncer(LanAnnouncementDTO announcement)
        {
            _announcement = announcement ?? new LanAnnouncementDTO();
        }

        public void UpdatePlayerCount(int count, string state = null)
        {
            lock (_lock)
            {
                if (_announcement != null)
                {
                    _announcement.CurrentPlayers = count;
                    if (!string.IsNullOrEmpty(state))
                    {
                        _announcement.State = state;
                    }
                }
            }
        }

        public void Start()
        {
            Stop();
            _cts = new CancellationTokenSource();

            try
            {
                _udpClient = new UdpClient();
                _udpClient.EnableBroadcast = true;
                Task.Run(() => BroadcastLoopAsync(_cts.Token));
                Debug.Log($"[LanServerAnnouncer] Started broadcasting on UDP port {DiscoveryPort}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LanServerAnnouncer] Failed to start announcer: {ex.Message}");
            }
        }

        private async Task BroadcastLoopAsync(CancellationToken ct)
        {
            IPEndPoint broadcastEndpoint = new IPEndPoint(IPAddress.Broadcast, DiscoveryPort);

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    string json;
                    lock (_lock)
                    {
                        json = JsonUtility.ToJson(_announcement);
                    }

                    byte[] bytes = Encoding.UTF8.GetBytes(json);
                    await _udpClient.SendAsync(bytes, bytes.Length, broadcastEndpoint);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[LanServerAnnouncer] Broadcast warning: {ex.Message}");
                }

                try
                {
                    await Task.Delay(1500, ct);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        public void Stop()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }

            if (_udpClient != null)
            {
                try
                {
                    _udpClient.Close();
                }
                catch { }
                _udpClient = null;
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }

    /// <summary>
    /// Listens for UDP announcement packets on the LAN and maintains an active server list.
    /// </summary>
    public class LanDiscoveryListener : IDisposable
    {
        public const int DiscoveryPort = 7776;
        private UdpClient _udpClient;
        private CancellationTokenSource _cts;
        private readonly Dictionary<string, DiscoveredServerInfo> _discoveredServers = new Dictionary<string, DiscoveredServerInfo>();
        private readonly object _lock = new object();

        public event Action<List<DiscoveredServerInfo>> OnServersChanged;

        public bool IsListening => _udpClient != null;

        public void Start()
        {
            Stop();
            _cts = new CancellationTokenSource();

            try
            {
                // Reusable socket binding allowing multiple clients on same machine/LAN
                _udpClient = new UdpClient();
                _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));

                Task.Run(() => ListenLoopAsync(_cts.Token));
                Task.Run(() => PruneLoopAsync(_cts.Token));
                Debug.Log($"[LanDiscoveryListener] Listening for LAN servers on UDP port {DiscoveryPort}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LanDiscoveryListener] Failed to start discovery listener: {ex.Message}");
            }
        }

        private async Task ListenLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    UdpReceiveResult result = await _udpClient.ReceiveAsync();
                    string json = Encoding.UTF8.GetString(result.Buffer);
                    LanAnnouncementDTO announcement = JsonUtility.FromJson<LanAnnouncementDTO>(json);

                    if (announcement != null)
                    {
                        string remoteIp = result.RemoteEndPoint.Address.ToString();
                        string key = $"{remoteIp}:{announcement.TcpPort}";

                        bool listChanged = false;
                        lock (_lock)
                        {
                            if (!_discoveredServers.TryGetValue(key, out DiscoveredServerInfo info))
                            {
                                info = new DiscoveredServerInfo
                                {
                                    RemoteIp = remoteIp,
                                    Announcement = announcement,
                                    LastSeenUtc = DateTime.UtcNow
                                };
                                _discoveredServers[key] = info;
                                listChanged = true;
                            }
                            else
                            {
                                info.Announcement = announcement;
                                info.LastSeenUtc = DateTime.UtcNow;
                            }
                        }

                        if (listChanged)
                        {
                            NotifyServersChanged();
                        }
                    }
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (!ct.IsCancellationRequested)
                    {
                        Debug.LogWarning($"[LanDiscoveryListener] Receive warning: {ex.Message}");
                    }
                }
            }
        }

        private async Task PruneLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(2000, ct);
                }
                catch (TaskCanceledException)
                {
                    break;
                }

                bool pruned = false;
                lock (_lock)
                {
                    List<string> expiredKeys = new List<string>();
                    foreach (var pair in _discoveredServers)
                    {
                        if (pair.Value.IsExpired)
                        {
                            expiredKeys.Add(pair.Key);
                        }
                    }

                    for (int i = 0; i < expiredKeys.Count; i++)
                    {
                        _discoveredServers.Remove(expiredKeys[i]);
                        pruned = true;
                    }
                }

                if (pruned)
                {
                    NotifyServersChanged();
                }
            }
        }

        private void NotifyServersChanged()
        {
            List<DiscoveredServerInfo> currentList;
            lock (_lock)
            {
                currentList = new List<DiscoveredServerInfo>(_discoveredServers.Values);
            }

            // Fire on main thread if possible
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.EnqueueMainThreadAction(() => OnServersChanged?.Invoke(currentList));
            }
            else
            {
                OnServersChanged?.Invoke(currentList);
            }
        }

        public List<DiscoveredServerInfo> GetCurrentServers()
        {
            lock (_lock)
            {
                return new List<DiscoveredServerInfo>(_discoveredServers.Values);
            }
        }

        public void Stop()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }

            if (_udpClient != null)
            {
                try
                {
                    _udpClient.Close();
                }
                catch { }
                _udpClient = null;
            }

            lock (_lock)
            {
                _discoveredServers.Clear();
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}

