using System;
using System.Collections.Concurrent;
#if !UNITY_WEBGL || UNITY_EDITOR
using System.Net.WebSockets;
#endif
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Events;
using Events.GameEvents;
using Managers;
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

#if UNITY_WEBGL && !UNITY_EDITOR
        private NativeWebSocket.WebSocket _socket;
#else
        private ClientWebSocket _socket;
#endif

        private CancellationTokenSource _receiveCts;
        private Task _receiveTask;
        private CancellationTokenSource _reconnectCts;
        private Task _reconnectTask;
        private EventQueueDispatcher _eventDispatcher;
        private bool _disconnectRequested;
        private bool _isReconnecting;
        private int _reconnectAttempt;

        private static readonly TimeSpan InitialReconnectDelay = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan MaxReconnectDelay = TimeSpan.FromSeconds(10);

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

            _disconnectRequested = false;
            CancelReconnectLoop();
            CleanupSocket();
            SetStatus(WebsocketConnectionStatus.Connecting);
            Session.wsUrl = wsUrl;
            Session.wsAccessToken = wsAccessToken;
            Session.roomCode = roomCode;
            Session.lastError = null;
            EnsureEventDispatcher();

            var uri = BuildUriWithAccessToken(wsUrl, wsAccessToken);

#if UNITY_WEBGL && !UNITY_EDITOR
            _socket = new NativeWebSocket.WebSocket(uri.ToString());
            
            _socket.OnOpen += async () =>
            {
                try
                {
                    await SendRawAsync("{\"protocol\":\"json\",\"version\":1}", CancellationToken.None);
                    Session.connectedAtUtc = DateTime.UtcNow;
                    _reconnectAttempt = 0;
                    SetStatus(WebsocketConnectionStatus.Connected);

                    if (_isReconnecting && !string.IsNullOrWhiteSpace(Session.matchId))
                    {
                        await SendReconnectMatchAsync(CancellationToken.None);
                        await SendRequestStateSnapshotAsync(CancellationToken.None);
                    }

                    _isReconnecting = false;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[WS WebGL] Error in OnOpen handler: {ex.Message}");
                }
            };

            _socket.OnMessage += (bytes) =>
            {
                var chunk = Encoding.UTF8.GetString(bytes);
                var messages = chunk.Split(RecordSeparator, StringSplitOptions.RemoveEmptyEntries);
                foreach (var message in messages)
                {
                    HandleSignalRMessage(message);
                }
            };

            _socket.OnError += (errMsg) =>
            {
                Session.lastError = errMsg;
                Session.isHealthy = false;
                SetStatus(WebsocketConnectionStatus.Faulted);
                ScheduleReconnect();
            };

            _socket.OnClose += (closeCode) =>
            {
                Session.isHealthy = false;
                SetStatus(WebsocketConnectionStatus.Disconnected);
                ScheduleReconnect();
            };

            try
            {
                await _socket.Connect();
            }
            catch (Exception ex)
            {
                Session.lastError = ex.Message;
                Session.isHealthy = false;
                SetStatus(WebsocketConnectionStatus.Faulted);
                ScheduleReconnect();
                throw;
            }
#else
            _socket = new ClientWebSocket();
            _socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);

            try
            {
                await _socket.ConnectAsync(uri, cancellationToken);
                await SendRawAsync("{\"protocol\":\"json\",\"version\":1}", cancellationToken);

                _receiveCts = new CancellationTokenSource();
                _receiveTask = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token));
                Session.connectedAtUtc = DateTime.UtcNow;
                _reconnectAttempt = 0;
                SetStatus(WebsocketConnectionStatus.Connected);

                if (_isReconnecting && !string.IsNullOrWhiteSpace(Session.matchId))
                {
                    await SendReconnectMatchAsync(cancellationToken);
                    await SendRequestStateSnapshotAsync(cancellationToken);
                }

                _isReconnecting = false;
            }
            catch (Exception ex)
            {
                Session.lastError = ex.Message;
                Session.isHealthy = false;
                SetStatus(WebsocketConnectionStatus.Faulted);
                ScheduleReconnect();
                throw;
            }
#endif
        }

        public async Task DisconnectAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            _disconnectRequested = true;
            CancelReconnectLoop();
            _receiveCts?.Cancel();

            if (_socket != null)
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                if (_socket.State == NativeWebSocket.WebSocketState.Open || _socket.State == NativeWebSocket.WebSocketState.Closing)
                {
                    await _socket.Close();
                }
#else
                if (_socket.State == WebSocketState.Open || _socket.State == WebSocketState.CloseReceived)
                {
                    await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnect", cancellationToken);
                }
#endif
            }

            if (_receiveTask != null)
            {
                try { await _receiveTask; } catch { }
            }

            Session.isHealthy = false;
            CleanupSocket();
            SetStatus(WebsocketConnectionStatus.Disconnected);
        }

        public void ResetSession()
        {
            _disconnectRequested = false;
            _isReconnecting = false;
            _reconnectAttempt = 0;
            Session.wsUrl = null;
            Session.wsAccessToken = null;
            Session.roomCode = null;
            Session.matchId = null;
            Session.stateVersion = 0;
            Session.connectedAtUtc = null;
            Session.lastMessageAtUtc = null;
            Session.lastHealthCheckAtUtc = null;
            Session.isHealthy = false;
            Session.lastError = null;
        }

        private async Task SendCommandAsync(
            string eventName, 
            object data = null, 
            CancellationToken cancellationToken = default(CancellationToken)
            )
        {
            if (string.IsNullOrWhiteSpace(eventName))
                throw new ArgumentException("eventName is required", nameof(eventName));

#if UNITY_WEBGL && !UNITY_EDITOR
            if (_socket == null || _socket.State != NativeWebSocket.WebSocketState.Open)
#else
            if (_socket == null || _socket.State != WebSocketState.Open)
#endif
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
#if UNITY_WEBGL && !UNITY_EDITOR
            if (_socket == null || _socket.State != NativeWebSocket.WebSocketState.Open)
#else
            if (_socket == null || _socket.State != WebSocketState.Open)
#endif
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
#if UNITY_WEBGL && !UNITY_EDITOR
                if (_socket.State == NativeWebSocket.WebSocketState.Open && hasNewMessage)
#else
                if (_socket.State == WebSocketState.Open && hasNewMessage)
#endif
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
            Debug.Log($"[GameWS →] {jsonPayload}");
            var framed = jsonPayload + RecordSeparator;
#if UNITY_WEBGL && !UNITY_EDITOR
            if (_socket != null && _socket.State == NativeWebSocket.WebSocketState.Open)
            {
                await _socket.SendText(framed);
            }
#else
            var bytes = Encoding.UTF8.GetBytes(framed);
            await _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
#endif
        }

#if !UNITY_WEBGL || UNITY_EDITOR
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
                        ScheduleReconnect();
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
                ScheduleReconnect();
                Debug.LogError($"[WS] Receive loop error: {ex.Message}");
            }
        }
#endif

        private void HandleSignalRMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            Debug.Log($"[GameWS ←] {message}");
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
            {
                _ = SendRawAsync("{\"type\":6}", CancellationToken.None);
                return; 
            }

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

        private void ScheduleReconnect()
        {
            if (_disconnectRequested || !CanReconnect())
                return;

            if (HasReconnectWindowExpired())
            {
                HandleReconnectWindowExpired();
                return;
            }

            if (_reconnectTask != null && !_reconnectTask.IsCompleted)
                return;

            CancelReconnectLoop();
            _reconnectCts = new CancellationTokenSource();
            _reconnectTask = Task.Run(() => ReconnectLoopAsync(_reconnectCts.Token));
        }

        private async Task ReconnectLoopAsync(CancellationToken cancellationToken)
        {
            _isReconnecting = true;

            while (!cancellationToken.IsCancellationRequested && !_disconnectRequested && CanReconnect())
            {
                if (HasReconnectWindowExpired())
                {
                    HandleReconnectWindowExpired();
                    return;
                }

                _reconnectAttempt++;
                var delay = GetReconnectDelay(_reconnectAttempt);

                try
                {
                    await Task.Delay(delay, cancellationToken);
                    await ConnectAsync(Session.wsUrl, Session.wsAccessToken, Session.roomCode, cancellationToken);

                    if (Status == WebsocketConnectionStatus.Connected)
                        return;
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    Session.lastError = ex.Message;
                    Session.isHealthy = false;
                    SetStatus(WebsocketConnectionStatus.Faulted);

                    if (HasReconnectWindowExpired())
                    {
                        HandleReconnectWindowExpired();
                        return;
                    }
                }
            }
        }

        private void CancelReconnectLoop()
        {
            if (_reconnectCts != null)
            {
                _reconnectCts.Cancel();
                _reconnectCts.Dispose();
                _reconnectCts = null;
            }

            _reconnectTask = null;
            _reconnectAttempt = 0;
        }

        private bool CanReconnect()
        {
            return !string.IsNullOrWhiteSpace(Session.wsUrl) &&
                   !string.IsNullOrWhiteSpace(Session.wsAccessToken);
        }

        private bool HasReconnectWindowExpired()
        {
            return GameManager.Instance != null && GameManager.Instance.HasReconnectWindowExpired();
        }

        private void HandleReconnectWindowExpired()
        {
            _disconnectRequested = true;
            Session.isHealthy = false;
            CancelReconnectLoop();
            CleanupSocket();
            SetStatus(WebsocketConnectionStatus.Disconnected);
            PublishToEventBus(() =>
            {
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.HandleReconnectWindowExpired();
                    return;
                }

                if (NavigationManager.Instance != null)
                {
                    NavigationManager.Instance.LoadWelcome();
                    return;
                }

                UnityEngine.SceneManagement.SceneManager.LoadScene("Welcome");
            });
        }

        private void CleanupSocket()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            _receiveCts?.Dispose();
            _receiveCts = null;
#endif

            if (_socket != null)
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                try { _socket.Close(); } catch {}
#else
                _socket.Dispose();
#endif
                _socket = null;
            }

#if !UNITY_WEBGL || UNITY_EDITOR
            _receiveTask = null;
#endif
        }

        private static TimeSpan GetReconnectDelay(int attempt)
        {
            var seconds = Math.Min(InitialReconnectDelay.TotalSeconds * Math.Pow(2, Math.Max(0, attempt - 1)), MaxReconnectDelay.TotalSeconds);
            return TimeSpan.FromSeconds(seconds);
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
