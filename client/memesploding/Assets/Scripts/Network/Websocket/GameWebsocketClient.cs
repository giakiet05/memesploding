using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Network.Websocket
{
    public enum WebsocketConnectionStatus
    {
        Disconnected,
        Connecting,
        Connected,
        Faulted
    }

    [Serializable]
    public class WebsocketSessionInfo
    {
        public string wsUrl;
        public string wsAccessToken;
        public string roomCode;
        public string matchId;
        public long stateVersion;
        public DateTime? connectedAtUtc;
        public DateTime? lastMessageAtUtc;
        public DateTime? lastHealthCheckAtUtc;
        public bool isHealthy;
        public string lastError;
    }

    public class GameWebsocketClient
    {
        private const char RecordSeparator = '\u001e';

        private static GameWebsocketClient _instance;
        public static GameWebsocketClient Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new GameWebsocketClient();

                return _instance;
            }
        }

        private readonly object _sync = new object();
        private ClientWebSocket _socket;
        private CancellationTokenSource _receiveCts;
        private Task _receiveTask;

        public WebsocketConnectionStatus Status { get; private set; } = WebsocketConnectionStatus.Disconnected;
        public WebsocketSessionInfo Session { get; } = new WebsocketSessionInfo();

        public event Action<WebsocketConnectionStatus> OnStatusChanged;
        public event Action<WsConnectedDto> OnConnectedEvent;
        public event Action<WsAckDto> OnAckEvent;
        public event Action<WsGameplayEventDto> OnGameplayEvent;
        public event Action<WsStateSnapshotDto> OnStateSnapshot;
        public event Action<WsErrorDto> OnErrorEvent;

        private GameWebsocketClient() { }

        public async Task ConnectAsync(
            string wsUrl, 
            string wsAccessToken, 
            string roomCode = null, 
            CancellationToken cancellationToken = default(CancellationToken)
            )
        {
            if (string.IsNullOrWhiteSpace(wsUrl))
                throw new ArgumentException("wsUrl is required", nameof(wsUrl));

            if (string.IsNullOrWhiteSpace(wsAccessToken))
                throw new ArgumentException("wsAccessToken is required", nameof(wsAccessToken));

            if (Status == WebsocketConnectionStatus.Connected || Status == WebsocketConnectionStatus.Connecting)
                return;

            SetStatus(WebsocketConnectionStatus.Connecting);
            Session.wsUrl = wsUrl;
            Session.wsAccessToken = wsAccessToken;
            Session.roomCode = roomCode;
            Session.lastError = null;

            var uri = BuildUriWithAccessToken(wsUrl, wsAccessToken);
            _socket = new ClientWebSocket();
            _socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);

            try
            {
                await _socket.ConnectAsync(uri, cancellationToken);
                await SendRawAsync("{\"protocol\":\"json\",\"version\":1}", cancellationToken);

                _receiveCts = new CancellationTokenSource();
                _receiveTask = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token));
                Session.connectedAtUtc = DateTime.UtcNow;
                SetStatus(WebsocketConnectionStatus.Connected);
            }
            catch (Exception ex)
            {
                Session.lastError = ex.Message;
                Session.isHealthy = false;
                SetStatus(WebsocketConnectionStatus.Faulted);
                throw;
            }
        }

        public async Task DisconnectAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            _receiveCts?.Cancel();

            if (_socket != null && (_socket.State == WebSocketState.Open || _socket.State == WebSocketState.CloseReceived))
            {
                await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnect", cancellationToken);
            }

            if (_receiveTask != null)
            {
                try { await _receiveTask; } catch { }
            }

            Session.isHealthy = false;
            SetStatus(WebsocketConnectionStatus.Disconnected);
        }

        private async Task SendCommandAsync(
            string eventName, 
            object data = null, 
            CancellationToken cancellationToken = default(CancellationToken)
            )
        {
            if (string.IsNullOrWhiteSpace(eventName))
                throw new ArgumentException("eventName is required", nameof(eventName));

            if (_socket == null || _socket.State != WebSocketState.Open)
                throw new InvalidOperationException("Websocket is not connected");

            var command = new WsClientCommand
            {
                @event = eventName,
                data = data ?? new object()
            };

            var invocation = new
            {
                type = 1,
                target = "SendCommand",
                arguments = new object[] { command }
            };

            var payload = JsonConvert.SerializeObject(invocation);
            await SendRawAsync(payload, cancellationToken);
        }

        public Task SendCommandAsync(
            WsClientCommandType commandType, 
            object data = null, 
            CancellationToken cancellationToken = default(CancellationToken)
            )
        {
            var commandName = WsEventTypeParser.ToCommandName(commandType);
            if (string.IsNullOrWhiteSpace(commandName))
                throw new ArgumentException("Unsupported websocket command type", nameof(commandType));

            return SendCommandAsync(commandName, data, cancellationToken);
        }

        public async Task<bool> HealthCheckAsync(int timeoutMs = 5000, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (_socket == null || _socket.State != WebSocketState.Open)
            {
                Session.isHealthy = false;
                return false;
            }

            var before = Session.lastMessageAtUtc;
            Session.lastHealthCheckAtUtc = DateTime.UtcNow;

            await SendCommandAsync(WsClientCommandType.Heartbeat, new object(), cancellationToken);

            var started = DateTime.UtcNow;
            while ((DateTime.UtcNow - started).TotalMilliseconds < timeoutMs)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var hasNewMessage = Session.lastMessageAtUtc.HasValue &&
                                    (!before.HasValue || Session.lastMessageAtUtc.Value > before.Value);
                if (_socket.State == WebSocketState.Open && hasNewMessage)
                {
                    Session.isHealthy = true;
                    return true;
                }

                await Task.Delay(100, cancellationToken);
            }

            Session.isHealthy = false;
            return false;
        }

        private async Task SendRawAsync(string jsonPayload, CancellationToken cancellationToken)
        {
            var framed = jsonPayload + RecordSeparator;
            var bytes = Encoding.UTF8.GetBytes(framed);
            await _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
        }

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[8192];
            var frameBuilder = new StringBuilder();

            try
            {
                while (!cancellationToken.IsCancellationRequested && _socket != null && _socket.State == WebSocketState.Open)
                {
                    var result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Session.isHealthy = false;
                        SetStatus(WebsocketConnectionStatus.Disconnected);
                        return;
                    }

                    frameBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    if (!result.EndOfMessage)
                    {
                        continue;
                    }

                    var chunk = frameBuilder.ToString();
                    frameBuilder.Clear();

                    var messages = chunk.Split(RecordSeparator, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var message in messages)
                    {
                        HandleSignalRMessage(message);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal cancel path.
            }
            catch (Exception ex)
            {
                Session.lastError = ex.Message;
                Session.isHealthy = false;
                SetStatus(WebsocketConnectionStatus.Faulted);
                Debug.LogError($"[WS] Receive loop error: {ex.Message}");
            }
        }

        private void HandleSignalRMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            Session.lastMessageAtUtc = DateTime.UtcNow;
            Session.isHealthy = true;

            if (message == "{}")
                return; // SignalR handshake ACK

            JObject root;
            try
            {
                root = JObject.Parse(message);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WS] Cannot parse message: {ex.Message}");
                return;
            }

            var type = root.Value<int?>("type");
            if (type == 6)
                return; // SignalR ping frame

            if (type == 7)
            {
                Session.lastError = root.Value<string>("error");
                Session.isHealthy = false;
                SetStatus(WebsocketConnectionStatus.Faulted);
                return;
            }

            var target = root.Value<string>("target");
            if (!string.Equals(target, "ReceiveMessage", StringComparison.Ordinal))
                return;

            var args = root["arguments"] as JArray;
            if (args == null || args.Count == 0)
                return;

            var serverEvent = args[0] as JObject;
            if (serverEvent == null)
                return;

            var eventName = serverEvent.Value<string>("event");
            var dataToken = serverEvent["data"];
            if (string.IsNullOrWhiteSpace(eventName) || dataToken == null)
                return;

            var serverEventType = WsEventTypeParser.ParseServerEvent(eventName);
            switch (serverEventType)
            {
                case WsServerEventType.Connected:
                    {
                        var dto = dataToken.ToObject<WsConnectedDto>();
                        if (dto != null)
                        {
                            Session.roomCode = dto.roomCode;
                            Session.matchId = dto.matchId;
                            OnConnectedEvent?.Invoke(dto);
                        }
                        break;
                    }
                case WsServerEventType.Ack:
                    {
                        var dto = dataToken.ToObject<WsAckDto>();
                        if (dto != null)
                        {
                            Session.stateVersion = dto.stateVersion;
                            OnAckEvent?.Invoke(dto);
                        }
                        break;
                    }
                case WsServerEventType.StateSnapshot:
                case WsServerEventType.MatchEnded:
                    {
                        var dto = dataToken.ToObject<WsStateSnapshotDto>();
                        if (dto != null)
                        {
                            Session.stateVersion = dto.stateVersion;
                            Session.roomCode = dto.roomCode;
                            Session.matchId = dto.matchId;
                            OnStateSnapshot?.Invoke(dto);
                        }
                        break;
                    }
                case WsServerEventType.GameplayEvent:
                    {
                        var dto = dataToken.ToObject<WsGameplayEventDto>();
                        if (dto != null)
                        {
                            Session.stateVersion = dto.stateVersion;
                            OnGameplayEvent?.Invoke(dto);
                        }
                        break;
                    }
                case WsServerEventType.Error:
                    {
                        var dto = dataToken.ToObject<WsErrorDto>();
                        if (dto != null)
                        {
                            Session.lastError = dto.message;
                            OnErrorEvent?.Invoke(dto);
                        }
                        break;
                    }
            }
        }

        private static Uri BuildUriWithAccessToken(string wsUrl, string accessToken)
        {
            var uriBuilder = new UriBuilder(wsUrl);
            var tokenParam = $"access_token={Uri.EscapeDataString(accessToken)}";
            var query = uriBuilder.Query;

            if (string.IsNullOrWhiteSpace(query))
            {
                uriBuilder.Query = tokenParam;
            }
            else if (query.IndexOf("access_token=", StringComparison.OrdinalIgnoreCase) < 0)
            {
                uriBuilder.Query = $"{query.TrimStart('?')}&{tokenParam}";
            }

            return uriBuilder.Uri;
        }

        private void SetStatus(WebsocketConnectionStatus status)
        {
            lock (_sync)
            {
                if (Status == status) return;
                Status = status;
            }

            OnStatusChanged?.Invoke(status);
        }
    }
}
