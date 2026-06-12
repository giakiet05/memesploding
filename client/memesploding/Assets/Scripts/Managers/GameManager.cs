using Events;
using Events.GameEvents;
using Gameplay;
using Managers.Audio;
using Models;
using Network.API.Models;
using Network.API.Services;
using Network.Websocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Managers.UIManager;
using UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using EventType = Events.EventType;

namespace Managers
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance;

        private const string AccessTokenKey = "memesploding.access_token";
        private const string RefreshTokenKey = "memesploding.refresh_token";
        private const string UserIdKey = "memesploding.user_id";
        private const string UsernameKey = "memesploding.username";
        private const string AvatarUrlKey = "memesploding.avatar_url";
        private const string BioKey = "memesploding.bio";
        private const string LevelKey = "memesploding.level";
        private const string ScoreKey = "memesploding.score";
        private const string DefaultUsername = "Player";
        private const string DefaultBio = "Ready to play.";
        private const string DefaultDisconnectMessage = "Connection lost. Trying to recover.";
        private const string ReconnectExpiredMessage = "Reconnect window expired. Returning to welcome.";
        private const string WelcomeSceneName = "Welcome";
        private const string LogoutSuccessMessage = "Logged out.";

        private bool _hasShownDisconnectPopup;
        private bool _isReturningToWelcome;
        private bool _isLocalDrawPending;

        public static GameManager EnsureInstance()
        {
            if (Instance != null)
                return Instance;

            var gameObject = new GameObject(nameof(GameManager));
            return gameObject.AddComponent<GameManager>();
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
            LoadCachedSession();
            AudioManager.EnsureInstance();
        }

        public Player Player { get; set; }
        public string AccessToken { get; private set; }
        public string RefreshToken { get; private set; }
        public bool IsAuthenticated => !string.IsNullOrWhiteSpace(AccessToken) && Player != null && !string.IsNullOrWhiteSpace(Player.ID);

        private GameSession _session;

        private void Start()
        {
            EventBus.Subscribe<WsConnectedEventPayload>(EventType.WsConnected, OnWsConnected);
            EventBus.Subscribe<WsAckEventPayload>(EventType.WsAck, OnWsAck);
            EventBus.Subscribe<WsGameplayEventPayload>(EventType.WsGameplayEvent, OnWsGameplayEvent);
            EventBus.Subscribe<WsStateSnapshotEventPayload>(EventType.WsStateSnapshot, OnWsStateSnapshot);
            EventBus.Subscribe<WsErrorEventPayload>(EventType.WsError, OnWsError);
            EventBus.Subscribe<WsStatusChangedEventPayload>(EventType.WsStatusChanged, OnWsStatusChanged);
       
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<WsConnectedEventPayload>(EventType.WsConnected, OnWsConnected);
            EventBus.Unsubscribe<WsAckEventPayload>(EventType.WsAck, OnWsAck);
            EventBus.Unsubscribe<WsGameplayEventPayload>(EventType.WsGameplayEvent, OnWsGameplayEvent);
            EventBus.Unsubscribe<WsStateSnapshotEventPayload>(EventType.WsStateSnapshot, OnWsStateSnapshot);
            EventBus.Unsubscribe<WsErrorEventPayload>(EventType.WsError, OnWsError);
            EventBus.Unsubscribe<WsStatusChangedEventPayload>(EventType.WsStatusChanged, OnWsStatusChanged);
        }

        private void OnWsConnected(WsConnectedEventPayload payload)
        {
            if (payload?.Data == null)
                return;

            _session = new GameSession(payload.Data.matchId, payload.Data.roomCode);
            _isReturningToWelcome = false;
        }

        private void OnWsAck(WsAckEventPayload payload)
        {
            if (payload?.Data == null)
                return;

            Debug.Log($"[GameManager] WS ACK stateVersion={payload.Data.stateVersion}");
        }

        private void OnWsGameplayEvent(WsGameplayEventPayload payload)
        {
            if (payload?.Data == null)
                return;

            if (_session == null)
            {
                Debug.LogWarning("Game session is null");
                return;
            }

            _session.HandleGameplayEvent(payload);
        }

        private void OnWsStateSnapshot(WsStateSnapshotEventPayload payload)
        {
            if (payload?.Data == null)
                return;

            if (_session == null)
            {
                Debug.LogWarning("Game session is null");
                return;
            }

            _session.ApplySnapshot(payload.Data);
            GameplayUIManager.Instance.InitOpponentUI(_session.GameState.players);
        }

        private void OnWsError(WsErrorEventPayload payload)
        {
            if (payload?.Data == null)
                return;

            Debug.LogError($"[GameManager] WS error: {payload.Data.message}");
            UniversalPopup.ShowError(string.IsNullOrWhiteSpace(payload.Data.message) ? "A network error occurred." : payload.Data.message);
        }

        private void OnWsStatusChanged(WsStatusChangedEventPayload payload)
        {
            Debug.Log($"[GameManager] WS status={payload?.Status}");

            if (payload == null)
                return;

            switch (payload.Status)
            {
                case WebsocketConnectionStatus.Connected:
                    if (_hasShownDisconnectPopup)
                    {
                        UniversalPopup.ShowSuccess("Reconnected to the game server.");
                        _hasShownDisconnectPopup = false;
                    }
                    break;
                case WebsocketConnectionStatus.Disconnected:
                case WebsocketConnectionStatus.Faulted:
                    if (_hasShownDisconnectPopup)
                        return;

                    var message = GameWebsocketClient.Instance.Session.lastError;
                    UniversalPopup.ShowError(string.IsNullOrWhiteSpace(message) ? DefaultDisconnectMessage : message);
                    _hasShownDisconnectPopup = true;
                    break;
            }
        }

        public void SetAuthenticatedUser(MeDto user, string accessToken, string refreshToken)
        {
            AccessToken = accessToken ?? string.Empty;
            RefreshToken = refreshToken ?? string.Empty;
            SetPlayer(user);
        }

        public void SetPlayer(MeDto user)
        {
            if (user == null)
            {
                Player = null;
                return;
            }

            Player = new Player
            {
                ID = user.Id,
                Username = string.IsNullOrWhiteSpace(user.Username) ? DefaultUsername : user.Username,
                AvatarUrl = user.AvatarUrl,
                Bio = string.IsNullOrWhiteSpace(user.Bio) ? DefaultBio : user.Bio,
                Level = user.Level <= 0 ? 1 : user.Level,
                Score = user.Score
            };
        }

        private void LoadCachedSession()
        {
            AccessToken = PlayerPrefs.GetString(AccessTokenKey, string.Empty);
            RefreshToken = PlayerPrefs.GetString(RefreshTokenKey, string.Empty);

            var userId = PlayerPrefs.GetString(UserIdKey, string.Empty);
            if (string.IsNullOrWhiteSpace(userId))
            {
                Player = null;
                return;
            }

            Player = new Player
            {
                ID = userId,
                Username = DefaultIfEmpty(PlayerPrefs.GetString(UsernameKey, string.Empty), DefaultUsername),
                AvatarUrl = PlayerPrefs.GetString(AvatarUrlKey, string.Empty),
                Bio = DefaultIfEmpty(PlayerPrefs.GetString(BioKey, string.Empty), DefaultBio),
                Level = Mathf.Max(1, PlayerPrefs.GetInt(LevelKey, 1)),
                Score = PlayerPrefs.GetInt(ScoreKey, 0)
            };
        }

        private static string DefaultIfEmpty(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }


        public void PlayCard(
            string targetUserId = null,
            int comboSize = 0,
            List<string> cardCodes = null,
            string requestedCardCode = null,
            string discardCardCode = null)
        {
            var cardCode = cardCodes != null && cardCodes.Count > 0 ? cardCodes[0] : null;
            NetworkManager.Instance.SendPlayCardCommand(
                cardCode: cardCode,
                targetUserId: targetUserId,
                comboSize: comboSize,
                cardCodes: cardCodes,
                requestedCardCode: requestedCardCode,
                discardCardCode: discardCardCode
            );
        }

        public void DrawCard()
        {
            if (_isLocalDrawPending)
            {
                Debug.LogWarning("[DrawTrace] Draw blocked: a draw command is already pending.");
                return;
            }

            if (_session?.GameState == null)
            {
                Debug.LogWarning("[DrawTrace] Draw blocked: game state is not ready.");
                return;
            }

            if (!_session.GameState.IsPlayerTurn)
            {
                Debug.LogWarning(
                    $"[DrawTrace] Draw blocked: not local turn. turnIndex={_session.GameState.turnIndex} " +
                    $"localUserId={Player?.ID ?? "null"}");
                return;
            }

            if (_session.GameState.drawPileCount <= 0)
            {
                Debug.LogWarning("[DrawTrace] Draw blocked: draw pile is empty.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(_session.GameState.pendingReactionAction) ||
                !string.IsNullOrWhiteSpace(_session.GameState.pendingBombCardCode) ||
                !string.IsNullOrWhiteSpace(_session.GameState.pendingDefuseUserId) ||
                !string.IsNullOrWhiteSpace(_session.GameState.pendingFavorTargetId))
            {
                Debug.LogWarning("[DrawTrace] Draw blocked: another card effect is still resolving.");
                return;
            }

            Debug.Log($"[DrawTrace] Sending draw command. turnIndex={_session.GameState.turnIndex} localUserId={Player?.ID ?? "null"}");
            _isLocalDrawPending = true;
            EventBus.Publish(EventType.TurnEnd, new TurnEndEventPayload(Player?.ID));
            //UIManager.Instance.DrawCard();
            NetworkManager.Instance.SendDrawCardCommand();
            //TODO: Using loading screen
        }

        public void NotifyLocalDrawResolved()
        {
            _isLocalDrawPending = false;
        }

        public bool CanDrawLocalCard()
        {
            return _session?.GameState != null &&
                   !_isLocalDrawPending &&
                   _session.GameState.IsPlayerTurn &&
                   IsLocalPlayerAlive() &&
                   _session.GameState.drawPileCount > 0 &&
                   string.IsNullOrWhiteSpace(_session.GameState.pendingReactionAction) &&
                   string.IsNullOrWhiteSpace(_session.GameState.pendingBombCardCode) &&
                   string.IsNullOrWhiteSpace(_session.GameState.pendingDefuseUserId) &&
                   string.IsNullOrWhiteSpace(_session.GameState.pendingFavorTargetId) &&
                   !(GameplayUIManager.Instance?.IsChoosingTarget ?? false);
        }

        public bool IsLocalPlayerAlive()
        {
            return _session?.GameState?.players?.Any(player =>
                string.Equals(player.userId, Player?.ID, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(player.lifeState, "Alive", StringComparison.OrdinalIgnoreCase)) == true;
        }

        public bool CanSelectLocalCard(string cardCode)
        {
            if (!IsLocalPlayerAlive() || _session?.GameState == null)
                return false;

            if (!string.IsNullOrWhiteSpace(_session.GameState.pendingReactionAction))
                return string.Equals(cardCode, "Nope", StringComparison.OrdinalIgnoreCase);

            if (IsLocalFavorTarget())
                return true;

            return !(GameplayUIManager.Instance?.IsChoosingTarget ?? false) &&
                   string.IsNullOrWhiteSpace(_session.GameState.pendingBombCardCode) &&
                   string.IsNullOrWhiteSpace(_session.GameState.pendingDefuseUserId) &&
                   string.IsNullOrWhiteSpace(_session.GameState.pendingFavorTargetId);
        }

        public void BeginInteraction()
        {
            if (_session?.GameState?.IsPlayerTurn == true)
                NetworkManager.Instance.SendBeginInteractionCommand();
        }
        public int GetDrawPileCount() => _session?.GameState?.drawPileCount ?? 0;
        public int GetCurrentTurnIndex() => _session?.GameState?.turnIndex ?? -1;
        public TimeSpan GetCurrentTurnTimeLeft() => _session?.Clock.CurrentTurnTimeLeft ?? TimeSpan.Zero;
        public TimeSpan GetOverallPlaytime() => _session?.Clock.OverallPlaytime ?? TimeSpan.Zero;

        public bool CanPlayLocalCard(string cardCode)
        {
            if (_session?.GameState == null || string.IsNullOrWhiteSpace(cardCode))
                return false;

            if (string.Equals(cardCode, "Nope", StringComparison.OrdinalIgnoreCase))
            {
                return !string.IsNullOrWhiteSpace(_session.GameState.pendingReactionAction) &&
                       _session.GameState.selfHand != null &&
                       _session.GameState.selfHand.Any(card =>
                           string.Equals(card, "Nope", StringComparison.OrdinalIgnoreCase));
            }

            if (string.Equals(cardCode, "Defuse", StringComparison.OrdinalIgnoreCase))
            {
                return !string.IsNullOrWhiteSpace(Player?.ID) &&
                       string.Equals(_session.GameState.pendingDefuseUserId, Player.ID, StringComparison.OrdinalIgnoreCase);
            }

            if (cardCode is "Cat1" or "Cat2" or "Cat3" or "Cat4" or "Cat5" or
                "ExplodingKitten" or "ImplodingKitten")
            {
                return false;
            }

            return _session.GameState.IsPlayerTurn &&
                   string.IsNullOrWhiteSpace(_session.GameState.pendingReactionAction) &&
                   string.IsNullOrWhiteSpace(_session.GameState.pendingBombCardCode) &&
                   string.IsNullOrWhiteSpace(_session.GameState.pendingDefuseUserId) &&
                   string.IsNullOrWhiteSpace(_session.GameState.pendingFavorTargetId);
        }

        public bool IsLocalFavorTarget()
        {
            return !string.IsNullOrWhiteSpace(Player?.ID) &&
                   string.Equals(_session?.GameState?.pendingFavorTargetId, Player.ID, StringComparison.OrdinalIgnoreCase);
        }

        public bool CanPlayLocalCombo()
        {
            return _session?.GameState != null &&
                   _session.GameState.IsPlayerTurn &&
                   string.IsNullOrWhiteSpace(_session.GameState.pendingReactionAction) &&
                   string.IsNullOrWhiteSpace(_session.GameState.pendingBombCardCode) &&
                   string.IsNullOrWhiteSpace(_session.GameState.pendingDefuseUserId) &&
                   string.IsNullOrWhiteSpace(_session.GameState.pendingFavorTargetId);
        }

        public TimeSpan? GetReactionWindowTimeLeft()
        {
            if (_session?.GameState?.reactionWindowEndsAt == null)
                return null;

            var endsAtUtc = NormalizeUtc(_session.GameState.reactionWindowEndsAt.Value);
            if (!endsAtUtc.HasValue)
                return null;

            var remaining = endsAtUtc.Value - GetServerNowUtc();
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        public DateTime? GetReactionWindowEndsAtUtc()
        {
            return _session?.GameState?.reactionWindowEndsAt;
        }

        public GameState GetGameState()
        {
            return _session?.GameState;
        }

        public void ChooseBombInsertPosition(int position)
        {
            NetworkManager.Instance.SendChooseBombInsertPositionCommand(position);
        }

        public void Nope()
        {
            NetworkManager.Instance.SendNopeCommand();
        }

        public void UseDefuse()
        {
            NetworkManager.Instance.SendUseDefuseCommand();
        }

        public void ChooseFavorCard(string cardCode)
        {
            NetworkManager.Instance.SendChooseFavorCardCommand(cardCode);
        }

        public List<WsPlayerPublicStateDto> GetAlivePlayers()
        {
            return _session?.GameState?.players?.Where(p => p.lifeState == "Alive" && p.userId != Player?.ID).ToList();
        }

        public async Task LogoutAsync()
        {
            var accessToken = AccessToken;
            var refreshToken = RefreshToken;

            try
            {
                if (!string.IsNullOrWhiteSpace(accessToken) && !string.IsNullOrWhiteSpace(refreshToken))
                {
                    await AuthService.Instance.LogoutAsync(
                        new RefreshTokenRequestDto { RefreshToken = refreshToken },
                        accessToken
                    );
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GameManager] Logout API failed: {ex.Message}");
            }

            try
            {
                await GameWebsocketClient.Instance.DisconnectAsync();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GameManager] WS disconnect during logout failed: {ex.Message}");
            }

            GameWebsocketClient.Instance.ResetSession();
            ClearSession();
            UniversalPopup.ShowSuccess(LogoutSuccessMessage);

            if (NavigationManager.Instance != null)
            {
                NavigationManager.Instance.LoadWelcome();
                return;
            }

            SceneManager.LoadScene(WelcomeSceneName);
        }

        public bool HasReconnectWindowExpired()
        {
            if (!TryGetReconnectDeadlineUtc(out var reconnectDeadlineUtc))
                return false;

            return GetServerNowUtc() >= reconnectDeadlineUtc;
        }

        public void HandleReconnectWindowExpired()
        {
            if (_isReturningToWelcome)
                return;

            _isReturningToWelcome = true;
            _hasShownDisconnectPopup = false;
            UniversalPopup.ShowError(ReconnectExpiredMessage);

            if (NavigationManager.Instance != null)
            {
                NavigationManager.Instance.LoadWelcome();
                return;
            }

            SceneManager.LoadScene(WelcomeSceneName);
        }

        private bool TryGetReconnectDeadlineUtc(out DateTime reconnectDeadlineUtc)
        {
            reconnectDeadlineUtc = default;

            var selfId = Player?.ID;
            var players = _session?.GameState?.players;
            if (string.IsNullOrWhiteSpace(selfId) || players == null || players.Count == 0)
                return false;

            var self = players.FirstOrDefault(player =>
                string.Equals(player.userId, selfId, StringComparison.OrdinalIgnoreCase));
            if (self?.pendingReconnectUntil == null)
                return false;

            var normalized = NormalizeUtc(self.pendingReconnectUntil.Value);
            if (!normalized.HasValue)
                return false;

            reconnectDeadlineUtc = normalized.Value;
            return true;
        }

        private DateTime GetServerNowUtc()
        {
            return _session?.Clock?.ServerNowUtc ?? DateTime.UtcNow;
        }

        private void ClearSession()
        {
            AccessToken = string.Empty;
            RefreshToken = string.Empty;
            Player = null;
            _session = null;
            _hasShownDisconnectPopup = false;
            _isReturningToWelcome = false;

            PlayerPrefs.DeleteKey(AccessTokenKey);
            PlayerPrefs.DeleteKey(RefreshTokenKey);
            PlayerPrefs.DeleteKey(UserIdKey);
            PlayerPrefs.DeleteKey(UsernameKey);
            PlayerPrefs.DeleteKey(AvatarUrlKey);
            PlayerPrefs.DeleteKey(BioKey);
            PlayerPrefs.DeleteKey(LevelKey);
            PlayerPrefs.DeleteKey(ScoreKey);
            PlayerPrefs.Save();
        }

        private static DateTime? NormalizeUtc(DateTime value)
        {
            if (value == default)
                return null;

            if (value.Kind == DateTimeKind.Utc)
                return value;

            if (value.Kind == DateTimeKind.Local)
                return value.ToUniversalTime();

            return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }
    }
}
