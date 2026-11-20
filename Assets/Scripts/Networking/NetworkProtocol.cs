using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Networking
{
    /// <summary>
    /// Constants and data envelopes for the Deep Space Pioneers LAN protocol.
    /// </summary>
    public static class NetworkMessageType
    {
        public const string Connect = "Connect";
        public const string Welcome = "Welcome";
        public const string Disconnect = "Disconnect";
        public const string LobbyUpdate = "LobbyUpdate";
        public const string SetSlot = "SetSlot";
        public const string StartGame = "StartGame";
        public const string GameStarted = "GameStarted";
        public const string GameStateSnapshot = "GameStateSnapshot";
        public const string PlayerAction = "PlayerAction";
        public const string ActionResult = "ActionResult";
        public const string GameEvent = "GameEvent";
        public const string ToastMessage = "ToastMessage";
        public const string Ping = "Ping";
        public const string Pong = "Pong";
        public const string Error = "Error";
    }

    [Serializable]
    public class NetworkEnvelope
    {
        public string Type;
        public int SenderPlayer = -1;
        public string ActionId = string.Empty;
        public string Payload = string.Empty;

        public NetworkEnvelope() { }

        public NetworkEnvelope(string type, string payload = "", int senderPlayer = -1, string actionId = "")
        {
            Type = type;
            Payload = payload ?? string.Empty;
            SenderPlayer = senderPlayer;
            ActionId = actionId ?? string.Empty;
        }

        public string ToJson()
        {
            return JsonUtility.ToJson(this);
        }

        public static NetworkEnvelope FromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                return JsonUtility.FromJson<NetworkEnvelope>(json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NetworkProtocol] Failed to parse envelope JSON: {ex.Message}");
                return null;
            }
        }
    }

    [Serializable]
    public class ConnectRequestDTO
    {
        public string PlayerName;
        public string ClientVersion;
    }

    [Serializable]
    public class WelcomeResponseDTO
    {
        public int AssignedPlayerIndex;
        public bool IsHost;
        public string ServerVersion;
        public string ServerName;
    }

    [Serializable]
    public class SetSlotRequestDTO
    {
        public int SlotIndex;
        public string PlayerName;
        public string PlayerType;     // "Human" or "AI"
        public string AiDifficulty;   // "Easy", "Normal", "Hard"
    }

    [Serializable]
    public class StartGameRequestDTO
    {
        public int Seed;
        public bool UseFixedSeed;
    }

    [Serializable]
    public class GameStartedDTO
    {
        public int Seed;
        public int HostPlayerIndex;
    }

    [Serializable]
    public class GameEventNotificationDTO
    {
        public string EventType;
        public int PlayerIndex;
        public string Description;
        public string DetailsJson;
    }

    [Serializable]
    public class ToastNotificationDTO
    {
        public string message;
        public bool isWarning;
        public float duration;
    }

    /// <summary>
    /// Framing utilities for streaming over TCP with a 4-byte big-endian length prefix.
    /// </summary>
    public static class LengthPrefixedFraming
    {
        public const int MaxMessageSize = 4 * 1024 * 1024; // 4 MB safety limit

        public static byte[] Frame(string json)
        {
            byte[] payloadBytes = Encoding.UTF8.GetBytes(json);
            int length = payloadBytes.Length;
            byte[] framed = new byte[4 + length];

            framed[0] = (byte)((length >> 24) & 0xFF);
            framed[1] = (byte)((length >> 16) & 0xFF);
            framed[2] = (byte)((length >> 8) & 0xFF);
            framed[3] = (byte)(length & 0xFF);

            Buffer.BlockCopy(payloadBytes, 0, framed, 4, length);
            return framed;
        }

        public static async Task<string> ReadMessageAsync(Stream stream, CancellationToken ct)
        {
            byte[] lengthBuffer = new byte[4];
            int readTotal = 0;
            while (readTotal < 4)
            {
                int read = await stream.ReadAsync(lengthBuffer, readTotal, 4 - readTotal, ct);
                if (read == 0) return null; // End of stream / disconnected
                readTotal += read;
            }

            int length = (lengthBuffer[0] << 24) |
                         (lengthBuffer[1] << 16) |
                         (lengthBuffer[2] << 8) |
                          lengthBuffer[3];

            if (length <= 0 || length > MaxMessageSize)
            {
                throw new InvalidDataException($"Invalid network frame length: {length} bytes");
            }

            byte[] payloadBuffer = new byte[length];
            readTotal = 0;
            while (readTotal < length)
            {
                int read = await stream.ReadAsync(payloadBuffer, readTotal, length - readTotal, ct);
                if (read == 0) return null; // Disconnected midway
                readTotal += read;
            }

            return Encoding.UTF8.GetString(payloadBuffer);
        }
    }
}

