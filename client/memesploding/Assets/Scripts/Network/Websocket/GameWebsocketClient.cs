using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Events;
using Events.GameEvents;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using EventType = Events.EventType;

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
        private readonly object _dispatcherSync = new object();
        private ClientWebSocket _socket;
        private CancellationTokenSource _receiveCts;
        private Task _receiveTask;
        private EventQueueDispatcher _eventDispatcher;

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
            EnsureEventDispatcher();

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

        public Task SendHeartbeatAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return SendCommandAsync(WsClientCommandType.Heartbeat, new WsEmptyCommandData(), cancellationToken);
        }

        public Task SendDrawCardAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return SendCommandAsync(WsClientCommandType.DrawCard, new WsEmptyCommandData(), cancellationToken);
        }

        public Task SendDrawFromBottomAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return SendCommandAsync(WsClientCommandType.DrawFromBottom, new WsEmptyCommandData(), cancellationToken);
        }

        public Task SendNopeAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return SendCommandAsync(WsClientCommandType.Nope, new WsEmptyCommandData(), cancellationToken);
        }

        public Task SendUseDefuseAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return SendCommandAsync(WsClientCommandType.UseDefuse, new WsEmptyCommandData(), cancellationToken);
        }

        public Task SendReconnectMatchAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return SendCommandAsync(WsClientCommandType.ReconnectMatch, new WsEmptyCommandData(), cancellationToken);
        }

        public Task SendRequestStateSnapshotAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return SendCommandAsync(WsClientCommandType.RequestStateSnapshot, new WsEmptyCommandData(), cancellationToken);
        }

        public Task SendChooseBombInsertPositionAsync(int position, CancellationToken cancellationToken = default(CancellationToken))
        {
            return SendCommandAsync(
                WsClientCommandType.ChooseBombInsertPosition,
                new WsChooseBombInsertPositionData { position = position },
                cancellationToken
            );
        }

        public Task SendChooseFavorCardAsync(string cardCode, CancellationToken cancellationToken = default(CancellationToken))
        {
            return SendCommandAsync(
                WsClientCommandType.ChooseFavorCard,
                new WsChooseFavorCardData { cardCode = cardCode },
                cancellationToken
            );
        }

        public Task SendPlayCardAsync(WsPlayCardData data, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            return SendCommandAsync(WsClientCommandType.PlayCard, data, cancellationToken);
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

            await SendHeartbeatAsync(cancellationToken);

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

            // SignalR handshake ACK
            if (message == "{}")
                return;

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

            // SignalR ping frame
            var type = root.Value<int?>("type");
            if (type == 6)
                return; 

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
                            PublishToEventBus(() =>
                                EventBus.Publish(EventType.WsConnected, new WsConnectedEventPayload(dto)));
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
                            PublishToEventBus(() =>
                                EventBus.Publish(EventType.WsAck, new WsAckEventPayload(dto)));
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
                            PublishToEventBus(() =>
                                EventBus.Publish(
                                    EventType.WsStateSnapshot,
                                    new WsStateSnapshotEventPayload(dto, serverEventType)
                                )
                            );
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
                            PublishToEventBus(() =>
                                EventBus.Publish(EventType.WsGameplayEvent, new WsGameplayEventPayload(dto)));
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
                            PublishToEventBus(() =>
                                EventBus.Publish(EventType.WsError, new WsErrorEventPayload(dto)));
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
            PublishToEventBus(() =>
                EventBus.Publish(EventType.WsStatusChanged, new WsStatusChangedEventPayload(status)));
        }

        private void EnsureEventDispatcher()
        {
            if (_eventDispatcher != null)
                return;

            lock (_dispatcherSync)
            {
                if (_eventDispatcher != null)
                    return;

                _eventDispatcher = EventQueueDispatcher.Instance;
            }
        }

        private void PublishToEventBus(Action publishAction)
        {
            if (publishAction == null)
                return;

            EnsureEventDispatcher();
            _eventDispatcher.Enqueue(publishAction);
        }

        private class EventQueueDispatcher : MonoBehaviour
        {
            private static EventQueueDispatcher _instance;
            private readonly ConcurrentQueue<Action> _queue = new ConcurrentQueue<Action>();

            public static EventQueueDispatcher Instance
            {
                get
                {
                    if (_instance != null)
                        return _instance;

                    var go = new GameObject("[WsEventDispatcher]");
                    _instance = go.AddComponent<EventQueueDispatcher>();
                    DontDestroyOnLoad(go);
                    return _instance;
                }
            }

            public void Enqueue(Action action)
            {
                if (action == null)
                    return;

                _queue.Enqueue(action);
            }

            private void Update()
            {
                while (_queue.TryDequeue(out var action))
                {
                    try
                    {
                        action.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[WS] Event dispatch error: {ex.Message}");
                    }
                }
            }
        }
    }
}
