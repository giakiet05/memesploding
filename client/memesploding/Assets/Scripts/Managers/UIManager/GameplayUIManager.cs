using System;
using System.Collections.Generic;
using Events;
using Events.GameEvents;
using Gameplay;
using Network.API.Models;
using Network.Websocket;
using UI.Gameplay;
using UnityEngine;
using EventType = Events.EventType;
using System.Linq;

namespace Managers.UIManager
{
    public class GameplayUIManager : MonoBehaviour
    {
        public static GameplayUIManager Instance;
        public static event Action<string> OnOpponentProfileSelected;   

        private void Awake()
        {
            if (Instance != null && Instance != this)
                Destroy(gameObject);
            else
                Instance = this;

            if (mainUserProfile == null)
                Debug.LogError("[GameplayUIManager] MainUserProfile reference is missing.");

            if (opponentProfilePrefab == null)
                Debug.LogError("[GameplayUIManager] OpponentProfile prefab reference is missing.");

            opponentPlayArea ??= playingArea != null ? playingArea.Find("BoardArea") as RectTransform : null;
            if (opponentPlayArea == null)
                Debug.LogError("[GameplayUIManager] OpponentPlayArea reference is missing.");

            opponentDrawCard ??= playingArea != null ? playingArea.Find("OpponenDrawCard")?.GetComponent<OpponentDrawCard>() : null;
            if (opponentDrawCard == null)
                Debug.LogError("[GameplayUIManager] OpponentDrawCard reference is missing.");
        }

        [SerializeField] private Canvas canvas;
        [SerializeField] private RectTransform playingArea;
        [SerializeField] private RectTransform uiArea;

        [SerializeField] private CardDisplayer cardDisplayer;
        [SerializeField] private CardSelector cardSelector;
        [SerializeField] private DrawnCardDisplayer drawnCardDisplayer;
        [SerializeField] private ReactionWindowView reactionWindowView;
        [SerializeField] private MainUserProfile mainUserProfile;
        [SerializeField] private RectTransform opponentPlayArea;
        [SerializeField] private OpponentDrawCard opponentDrawCard;

        [Header("Opponent Organization")]
        [SerializeField] private OpponentProfile opponentProfilePrefab;
        [SerializeField] private RectTransform fanCenter;   // assign in inspector
        [SerializeField] private float radiusX = 400f; // horizontal spread
        [SerializeField] private float radiusY = 200f; // vertical height
        [SerializeField, Range(0f, 360f)] private float totalAngle = 180f;
        [SerializeField] private bool autoCenter = true;
        [SerializeField] private float startAngle = -90f;

        private Dictionary<string, OpponentProfile> _opponentsUI;
        private HashSet<string> _selectableTargetIds;
        private Action<string> _onTargetSelected;

        //TODO: Add loading screen

        private void Start()
        {
            EventBus.Subscribe<CardPlayedEventPayload>(EventType.CardPlayedEvent, OnCardPlayed);
            
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<CardPlayedEventPayload>(EventType.CardPlayedEvent, OnCardPlayed);
            ClearTargetSelection();
        }

        public void ResetUI()
        {
            reactionWindowView?.Hide();
            uiArea.gameObject.SetActive(false);
            cardDisplayer.gameObject.SetActive(false);
            cardSelector.gameObject.SetActive(false);
            drawnCardDisplayer.gameObject.SetActive(false);
        }

        //TODO: Handle player used card effect
        private void OnCardPlayed(CardPlayedEventPayload obj)
        {
            if (obj == null || obj.PlayedCard == null)
                return;

            var localUserId = GameManager.Instance?.Player?.ID;
            if (!string.Equals(obj.PlayerID, localUserId, StringComparison.OrdinalIgnoreCase))
                return;

            if (!obj.ShouldDispatchCommand)
                return;

            if (obj.ComboSize > 1)
            {
                HandleComboPlayed(obj);
                return;
            }

            switch (obj.PlayedCard.Data.cardCode)
            {
                case "Shuffle":
                case "Skip":
                case "SeeTheFuture":
                    GameManager.Instance.PlayCard(
                        cardCodes: new List<string> { obj.PlayedCard.Data.cardCode });
                    break;

                case "Nope":
                    GameManager.Instance.Nope();
                    break;

                case "Defuse":
                    GameManager.Instance.UseDefuse();
                    break;

                case "TargetedAttack":
                case "PersonalAttack":
                case "Favor":
                    OpenTargetUserSelector(
                        GameManager.Instance.GetAlivePlayers(),
                        targetUserId => GameManager.Instance.PlayCard(
                            targetUserId: targetUserId,
                            cardCodes: new List<string> { obj.PlayedCard.Data.cardCode }));
                    break;

                case "Attack":
                    GameManager.Instance.PlayCard(
                        cardCodes: new List<string> { obj.PlayedCard.Data.cardCode });
                    break;

                default:
                    Debug.LogWarning($"Unhandled card: {obj.PlayedCard.Data.cardCode}");
                    break;
            }
        }

        private void HandleComboPlayed(CardPlayedEventPayload payload)
        {
            var cardCodes = payload.CardCodes ?? new List<string>();

            if (payload.ComboSize == 2)
            {
                OpenTargetUserSelector(
                    GameManager.Instance.GetAlivePlayers(),
                    targetUserId => GameManager.Instance.PlayCard(
                        targetUserId: targetUserId,
                        comboSize: payload.ComboSize,
                        cardCodes: cardCodes));
                return;
            }

            Debug.LogWarning($"Combo size {payload.ComboSize} is not wired to card-selection UI yet.");
        }
        public void HandleProfileClicked(string userID)
        {
            Debug.Log($"Profile clicked: {userID}");
            OnOpponentProfileSelected?.Invoke(userID);
            if (_selectableTargetIds == null || !_selectableTargetIds.Contains(userID))
                return;

            var callback = _onTargetSelected;
            ClearTargetSelection();
            callback?.Invoke(userID);
        }

        public void OpenTargetUserSelector(List<WsPlayerPublicStateDto> targetUsers, Action<string> onSelected)
        {
            ClearTargetSelection();

            var targets = targetUsers?
                .Where(user => user != null && !string.IsNullOrWhiteSpace(user.userId))
                .Select(user => user.userId)
                .Where(userId => _opponentsUI != null && _opponentsUI.ContainsKey(userId))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (targets == null || targets.Count == 0)
            {
                Debug.LogWarning("[GameplayUIManager] No valid target users available.");
                return;
            }

            _selectableTargetIds = targets;
            _onTargetSelected = onSelected;

            foreach (var item in _opponentsUI)
            {
                item.Value?.SetArrowActive(_selectableTargetIds.Contains(item.Key));
            }
        }

        public void ClearTargetSelection()
        {
            if (_opponentsUI != null)
            {
                foreach (var opponent in _opponentsUI.Values)
                {
                    opponent?.SetArrowActive(false);
                }
            }

            _selectableTargetIds = null;
            _onTargetSelected = null;
        }
        public void OpenBombReinsertWindow()
        {
            int drawPileCount = GameManager.Instance.GetDrawPileCount();
            // TODO: Mở slider để chọn vị trí đặt bomb (từ 0 đến drawPileCount)
            int selectedPosition = 0; // Lấy giá trị từ slider

            //Sau khi chọn vị trí, gọi API để đặt bomb vào vị trí đã chọn
            GameManager.Instance.ChooseBombInsertPosition(selectedPosition); 
        }
        
        //Draw Card
        public void DisplayDrawnCard(string cardCode)
        {
            uiArea.gameObject.SetActive(true);
            drawnCardDisplayer.gameObject.SetActive(true);
            drawnCardDisplayer.PlayDrawCardAnimation(cardCode);
        }

        public void DisplayOpponentDraw(string userID)
        {
            if (opponentDrawCard == null || playingArea == null)
                return;

            if (_opponentsUI == null || !_opponentsUI.TryGetValue(userID, out var opponent) || opponent == null)
                return;

            var drawAnimation = Instantiate(opponentDrawCard, opponentDrawCard.transform.parent);
            drawAnimation.gameObject.SetActive(false);

            var startPosition = WorldToPlayingAreaPoint(opponentDrawCard.transform.position);
            var targetPosition = WorldToPlayingAreaPoint(opponent.transform.position);

            drawAnimation.Play(startPosition, targetPosition, destroyOnFinish: true);
        }

        //Card Displayer
        public void DisplayCards(List<string> cardCodes)
        {
            uiArea.gameObject.SetActive(true);
            cardDisplayer.gameObject.SetActive(true);

            cardDisplayer.Clear();
            foreach (var cardCode in cardCodes)
            {
                CardManager.Instance.CreateDisplayCard(cardCode, cardDisplayer.transform);
            }
        }

        public void CloseCardDisplayer()
        {
            ResetUI();
        }

        public void ShowReactionWindow(string userId, string cardCode, int nopeCount)
        {
            EnsureReactionWindowView();
            if (uiArea != null)
                uiArea.gameObject.SetActive(true);

            var canNope = GameManager.Instance != null && GameManager.Instance.CanPlayLocalCard("Nope");
            var timeLeft = GameManager.Instance?.GetReactionWindowTimeLeft() ?? TimeSpan.FromSeconds(5);
            reactionWindowView?.Show(userId, cardCode, nopeCount, timeLeft, canNope);
        }

        public void HideReactionWindow()
        {
            reactionWindowView?.Hide();
        }

        public void ShowActionNoped(string cardCode, int nopeCount)
        {
            EnsureReactionWindowView();
            if (uiArea != null)
                uiArea.gameObject.SetActive(true);

            reactionWindowView?.ShowNoped(cardCode, nopeCount);
        }

        public void ShowFavorWindow(string requesterId, string targetId)
        {
            Debug.Log($"[GameplayUIManager] Favor window requesterId={requesterId ?? "null"} targetId={targetId ?? "null"}");
        }

        public void HideFavorWindow()
        {
            Debug.Log("[GameplayUIManager] Favor window closed.");
        }

        //Opponent UI
        public void InitOpponentUI(List<WsPlayerPublicStateDto> players)
        {
            if (players == null || players.Count == 0) 
                return;

            if (opponentProfilePrefab == null)
                return;

            _opponentsUI ??= new Dictionary<string, OpponentProfile>();
            ClearOpponentUI();

            string myId = GameManager.Instance.Player.ID;
            int myIndex = players.FindIndex(p => p.userId == myId);
            if (myIndex == -1) return;

            int count = players.Count;
            int currentTurnIndex = ResolveCurrentTurnIndex(players);
            var currentTurnUserId = currentTurnIndex >= 0 && currentTurnIndex < players.Count
                ? players[currentTurnIndex].userId
                : null;

            Debug.Log(
                $"[TurnTrace] UI init myIndex={myIndex} myUserId={myId} currentTurnIndex={currentTurnIndex} " +
                $"currentTurnUserId={currentTurnUserId ?? "null"} mainUserRef={(mainUserProfile != null)} players={count}");

            var rects = new List<RectTransform>();

            for (int offset = 1; offset < count; offset++)
            {
                int index = (myIndex + offset) % count;
                var opponent = players[index];
                var participant = ResolveParticipant(opponent.userId);
                var isCurrentTurn = index == currentTurnIndex;

                var ui = Instantiate(opponentProfilePrefab, playingArea.transform);
                ui.SetPlayArea(opponentPlayArea);
                ui.Init(opponent, participant?.AvatarUrl, IsBotParticipant(participant));
                ui.SetCurrentTurn(isCurrentTurn);
                ui.SetArrowActive(false);
                ui.OnProfileClickedEvent += HandleProfileClicked;
                _opponentsUI[opponent.userId] = ui;

                Debug.Log(
                    $"[TurnTrace] UI opponent userId={opponent.userId} nickname={opponent.nickname} " +
                    $"index={index} isCurrentTurn={isCurrentTurn}");

                rects.Add(ui.GetComponent<RectTransform>());
            }

            LayoutOpponent(rects);
            if (mainUserProfile != null)
            {
                mainUserProfile.SetCurrentTurn(myIndex == currentTurnIndex);
                Debug.Log($"[TurnTrace] UI main userId={myId} isCurrentTurn={myIndex == currentTurnIndex}");
            }
        }

        private void LayoutOpponent(List<RectTransform> items)
        {
            if (items == null || items.Count == 0) return;

            int count = items.Count;

            float effectiveStart = autoCenter
                ? -totalAngle * 0.5f
                : startAngle;

            float step = count > 1 ? totalAngle / (count - 1) : 0f;

            // Convert center
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                playingArea,
                RectTransformUtility.WorldToScreenPoint(null, fanCenter.position),
                null,
                out var centerLocalPos
            );

            for (int i = 0; i < count; i++)
            {
                float angle = effectiveStart + step * i;
                float rad = angle * Mathf.Deg2Rad;

                // Key difference from circle:
                float x = Mathf.Sin(rad) * radiusX;
                float y = Mathf.Cos(rad) * radiusY;

                var rect = items[i];
                rect.SetParent(playingArea, false);
                rect.anchoredPosition = centerLocalPos + new Vector2(x, y);
            }
        }

        public void PlayOpponentCard(string userID, string cardCode)
        {
            if (_opponentsUI == null)
                return;

            if (!_opponentsUI.TryGetValue(userID, out var opponent))
                return;

            if (opponent == null)
                return;

            opponent.PlayCard(cardCode);
        }

        private Vector2 WorldToPlayingAreaPoint(Vector3 worldPosition)
        {
            var screenPoint = RectTransformUtility.WorldToScreenPoint(null, worldPosition);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(playingArea, screenPoint, null, out var localPoint);
            return localPoint;
        }

        private void ClearOpponentUI()
        {
            if (_opponentsUI == null || _opponentsUI.Count == 0)
                return;

            foreach (var item in _opponentsUI.Values)
            {
                if (item != null)
                {
                    item.OnProfileClickedEvent -= HandleProfileClicked;
                    Destroy(item.gameObject);
                }
            }

            _opponentsUI.Clear();
            ClearTargetSelection();
        }

        private int ResolveCurrentTurnIndex(List<WsPlayerPublicStateDto> players)
        {
            var turnIndex = GameManager.Instance?.GetCurrentTurnIndex() ?? -1;
            return turnIndex >= 0 && turnIndex < players.Count ? turnIndex : -1;
        }

        private RoomParticipantDto ResolveParticipant(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return null;

            var participants = RoomManager.Instance?.CurrentRoom?.CurrentParticipants;
            return participants?.FirstOrDefault(item =>
                string.Equals(item.UserId, userId, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsBotParticipant(RoomParticipantDto participant)
        {
            return participant != null &&
                   string.Equals(participant.Role, "bot", StringComparison.OrdinalIgnoreCase);
        }

        private void EnsureReactionWindowView()
        {
            if (reactionWindowView != null)
                return;

            reactionWindowView = uiArea != null
                ? uiArea.GetComponentInChildren<ReactionWindowView>(true)
                : FindFirstObjectByType<ReactionWindowView>(FindObjectsInactive.Include);
        }
    }
}
