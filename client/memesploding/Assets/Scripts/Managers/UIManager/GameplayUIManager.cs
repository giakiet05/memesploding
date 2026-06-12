using System;
using System.Collections.Generic;
using Events;
using Events.GameEvents;
using Gameplay;
using Network.API.Models;
using Network.Websocket;
using UI.Gameplay;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
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
        private GameplayInteractionModal _interactionModal;
        private TextMeshProUGUI _turnTimerHud;
        private TextMeshProUGUI _drawPileHud;
        private GameObject _eliminatedOverlay;
        private Button _playCardButton;
        private TextMeshProUGUI _playCardButtonLabel;
        private string _defaultPlayCardButtonText;

        public bool IsChoosingTarget => _selectableTargetIds != null && _selectableTargetIds.Count > 0;

        private static readonly string[] OriginalCardCodes =
        {
            "Attack", "Skip", "Favor", "Shuffle", "SeeTheFuture", "Nope", "Defuse",
            "Cat1", "Cat2", "Cat3", "Cat4", "Cat5", "ExplodingKitten"
        };

        //TODO: Add loading screen

        private void Start()
        {
            EventBus.Subscribe<CardPlayedEventPayload>(EventType.CardPlayedEvent, OnCardPlayed);
            EnsureReactionWindowView();
            EnsureInteractionModal();
            EnsureGameplayHud();
            EnsurePlayCardButton();
        }

        private void Update()
        {
            if (_turnTimerHud != null && GameManager.Instance != null)
            {
                var reactionActive = !string.IsNullOrWhiteSpace(GameManager.Instance.GetGameState()?.pendingReactionAction);
                _turnTimerHud.text = reactionActive
                    ? "LƯỢT: TẠM DỪNG"
                    : $"LƯỢT: {Mathf.CeilToInt((float)GameManager.Instance.GetCurrentTurnTimeLeft().TotalSeconds)}s";
            }
            if (_drawPileHud != null && GameManager.Instance != null)
                _drawPileHud.text = $"CHỒNG RÚT: {GameManager.Instance.GetDrawPileCount()}";
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<CardPlayedEventPayload>(EventType.CardPlayedEvent, OnCardPlayed);
            ClearTargetSelection();
            HideInteractionModal();
        }

        public void ResetUI()
        {
            reactionWindowView?.Hide();
            uiArea.gameObject.SetActive(false);
            cardDisplayer.gameObject.SetActive(false);
            cardSelector.gameObject.SetActive(false);
            drawnCardDisplayer.gameObject.SetActive(false);
            SetFavorButtonMode(false);
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

            if (payload.ComboSize == 3)
            {
                GameManager.Instance.BeginInteraction();
                OpenTargetUserSelector(
                    GameManager.Instance.GetAlivePlayers(),
                    targetUserId =>
                    {
                        EnsureInteractionModal();
                        _interactionModal.ShowOptions(
                            "COMBO 3",
                            "Chọn tên lá bài muốn lấy từ đối thủ.",
                            OriginalCardCodes,
                            null,
                            requestedCardCode => GameManager.Instance.PlayCard(
                                targetUserId: targetUserId,
                                comboSize: payload.ComboSize,
                                cardCodes: cardCodes,
                                requestedCardCode: requestedCardCode),
                            () => CardManager.Instance?.RejectPendingPlay());
                    });
                return;
            }

            if (payload.ComboSize == 5)
            {
                GameManager.Instance.BeginInteraction();
                var discardCards = GameManager.Instance.GetGameState()?.discardPile?
                    .Where(card => !string.IsNullOrWhiteSpace(card))
                    .Distinct()
                    .ToList();
                if (discardCards == null || discardCards.Count == 0)
                {
                    CardManager.Instance?.RejectPendingPlay();
                    Debug.LogWarning("[GameplayUIManager] Combo 5 requires a non-empty discard pile.");
                    return;
                }

                EnsureInteractionModal();
                _interactionModal.ShowOptions(
                    "COMBO 5",
                    "Chọn một lá từ chồng bài bỏ.",
                    discardCards,
                    null,
                    discardCardCode => GameManager.Instance.PlayCard(
                        comboSize: payload.ComboSize,
                        cardCodes: cardCodes,
                        discardCardCode: discardCardCode),
                    () => CardManager.Instance?.RejectPendingPlay());
                return;
            }

            CardManager.Instance?.RejectPendingPlay();
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
            GameManager.Instance?.BeginInteraction();
            ClearTargetSelection();

            var targets = targetUsers?
                .Where(user => user != null && !string.IsNullOrWhiteSpace(user.userId))
                .Select(user => user.userId)
                .Where(userId => _opponentsUI != null && _opponentsUI.ContainsKey(userId))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (targets == null || targets.Count == 0)
            {
                Debug.LogWarning("[GameplayUIManager] No valid target users available.");
                CardManager.Instance?.RejectPendingPlay();
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
        public void OpenBombReinsertWindow(DateTime? endsAtUtc = null)
        {
            int drawPileCount = GameManager.Instance.GetDrawPileCount();
            EnsureInteractionModal();
            _interactionModal.ShowSlider(
                "ĐẶT LẠI BOMB",
                "Chọn vị trí đặt bomb vào chồng bài rút.",
                drawPileCount,
                endsAtUtc,
                position => GameManager.Instance.ChooseBombInsertPosition(position));
        }

        public void ShowDefuseWindow(DateTime? endsAtUtc)
        {
            StartCoroutine(ShowDefuseWindowAfterDrawAnimation(endsAtUtc));
        }

        private System.Collections.IEnumerator ShowDefuseWindowAfterDrawAnimation(DateTime? endsAtUtc)
        {
            yield return new WaitForSecondsRealtime(0.8f);
            EnsureInteractionModal();
            _interactionModal.ShowConfirm(
                "BOMB!",
                "Dùng Defuse trước khi hết thời gian.",
                "DÙNG DEFUSE",
                endsAtUtc,
                () => GameManager.Instance.UseDefuse());
        }
        
        //Draw Card
        public void DisplayDrawnCard(string cardCode)
        {
            uiArea.gameObject.SetActive(true);
            drawnCardDisplayer.gameObject.SetActive(true);
            var deckWorldPosition = opponentDrawCard != null ? opponentDrawCard.transform.position : playingArea.position;
            drawnCardDisplayer.PlayDrawCardAnimation(cardCode, deckWorldPosition);
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
            Debug.Log($"[SeeTheFuture] Displaying cards: {string.Join(", ", cardCodes ?? new List<string>())}");
            uiArea.gameObject.SetActive(true);
            cardDisplayer.gameObject.SetActive(true);

            cardDisplayer.Clear();
            foreach (var cardCode in cardCodes)
            {
                if (string.IsNullOrWhiteSpace(cardCode) || CardManager.Instance.GetCardData(cardCode) == null)
                {
                    Debug.LogWarning($"[GameplayUIManager] Cannot display unknown future card '{cardCode ?? "null"}'.");
                    continue;
                }
                var card = CardManager.Instance.CreateDisplayCard(cardCode, cardDisplayer.transform);
                cardDisplayer.AddCard(card);
            }
        }

        public void CloseCardDisplayer()
        {
            ResetUI();
        }

        public void ShowReactionWindow(
            string userId,
            string cardCode,
            int nopeCount,
            string[] targetUserIds,
            string effectScope,
            bool isNewWindow)
        {
            EnsureReactionWindowView();
            reactionWindowView?.Show(new ReactionWindowModel
            {
                ActorName = ResolvePlayerName(userId),
                TargetText = ResolveReactionTargetText(userId, targetUserIds, effectScope),
                CardCode = cardCode,
                CardData = CardManager.Instance?.GetCardData(cardCode),
                EndsAtUtc = GameManager.Instance?.GetReactionWindowEndsAtUtc(),
                WillActivate = nopeCount % 2 == 0,
                CanNope = GameManager.Instance != null && GameManager.Instance.CanPlayLocalCard("Nope"),
                IsNewWindow = isNewWindow
            });
        }

        public void HideReactionWindow()
        {
            reactionWindowView?.Hide();
        }

        public void HoldReactionWindowForResolution()
        {
            EnsureReactionWindowView();
            reactionWindowView?.HoldForResolution();
        }

        public void ShowReactionResult(bool activated)
        {
            EnsureReactionWindowView();
            reactionWindowView?.ShowResult(activated);
        }

        public void ShowActionToast(string title, string message, string detail = null, string status = null, bool danger = false)
        {
            EnsureReactionWindowView();
            reactionWindowView?.ShowNotification(title, message, detail, status, danger);
        }

        public void ShowPlayerEliminated(string userId, string reason)
        {
            var isSelf = string.Equals(userId, GameManager.Instance?.Player?.ID, StringComparison.OrdinalIgnoreCase);
            if (isSelf)
            {
                mainUserProfile?.SetEliminated(true);
                ClearTargetSelection();
                HideInteractionModal();
                StartCoroutine(ShowEliminatedOverlayAfterDrawAnimation());
            }
            else if (_opponentsUI != null && _opponentsUI.TryGetValue(userId, out var opponent))
            {
                opponent?.SetEliminated(true);
            }

            ShowActionToast("ĐÃ BỊ LOẠI", $"{ResolvePlayerName(userId)} đã bị loại", reason, "DEAD", true);
        }

        private System.Collections.IEnumerator ShowEliminatedOverlayAfterDrawAnimation()
        {
            yield return new WaitForSecondsRealtime(0.8f);
            EnsureEliminatedOverlay();
            _eliminatedOverlay.SetActive(true);
            var group = _eliminatedOverlay.GetComponent<CanvasGroup>();
            var rect = (RectTransform)_eliminatedOverlay.transform;
            group.alpha = 0f;
            rect.localScale = new Vector3(1.18f, 1.18f, 1f);
            for (var elapsed = 0f; elapsed < 0.45f; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.SmoothStep(0f, 1f, elapsed / 0.45f);
                group.alpha = t;
                rect.localScale = Vector3.Lerp(new Vector3(1.18f, 1.18f, 1f), Vector3.one, t);
                yield return null;
            }
            group.alpha = 1f;
            rect.localScale = Vector3.one;
        }

        public void ShowFavorWindow(string requesterId, string targetId)
        {
            if (!GameManager.Instance.IsLocalFavorTarget())
                return;

            HideInteractionModal();
            CardManager.Instance?.HandLayout?.ClearCardSelection();
            SetFavorButtonMode(true);
            ShowActionToast(
                "FAVOR",
                $"Chọn một lá trong tay để đưa cho {ResolvePlayerName(requesterId)}.",
                "Bấm GỬI sau khi chọn bài",
                "CHỌN BÀI");
        }

        public void HideFavorWindow()
        {
            CardManager.Instance?.HandLayout?.ClearCardSelection();
            SetFavorButtonMode(false);
        }

        public void HideInteractionModal()
        {
            if (_interactionModal != null)
                _interactionModal.Hide();
            else
                _interactionModal = null;
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
                ui.SetEliminated(string.Equals(opponent.lifeState, "Eliminated", StringComparison.OrdinalIgnoreCase));
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
                mainUserProfile.SetEliminated(
                    string.Equals(players[myIndex].lifeState, "Eliminated", StringComparison.OrdinalIgnoreCase));
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

        public void PlayLocalCard(string cardCode)
        {
            if (CardManager.Instance == null || opponentPlayArea == null)
                return;

            var card = CardManager.Instance.CreatePlayableCard(cardCode, opponentPlayArea);
            if (card == null)
                return;

            var rect = card.RectTransform;
            rect.localScale = Vector3.zero;
            StartCoroutine(AnimateLocalCard(rect));
        }

        private System.Collections.IEnumerator AnimateLocalCard(RectTransform rect)
        {
            var target = UnityEngine.Random.insideUnitCircle * 80f;
            for (var elapsed = 0f; elapsed < 0.8f; elapsed += Time.deltaTime)
            {
                var t = Mathf.SmoothStep(0f, 1f, elapsed / 0.8f);
                rect.anchoredPosition = Vector2.Lerp(Vector2.zero, target, t);
                rect.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, t);
                yield return null;
            }
            rect.anchoredPosition = target;
            rect.localScale = Vector3.one;
            EventBus.Publish(
                EventType.CardPlayedEvent,
                new CardPlayedEventPayload(card: rect.GetComponent<Gameplay.Card.BaseCard>(), playerID: GameManager.Instance?.Player?.ID, shouldDispatchCommand: false));
        }

        public string GetPlayerDisplayName(string userId) => ResolvePlayerName(userId);

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
            {
                reactionWindowView.ConfigureAsToast(playingArea);
                return;
            }

            reactionWindowView = FindFirstObjectByType<ReactionWindowView>(FindObjectsInactive.Include);

            if (reactionWindowView != null)
            {
                reactionWindowView.ConfigureAsToast(playingArea);
            }
        }

        private void EnsureInteractionModal()
        {
            if (_interactionModal != null)
                return;

            _interactionModal = FindFirstObjectByType<GameplayInteractionModal>(FindObjectsInactive.Include);
            if (_interactionModal == null && canvas != null)
                _interactionModal = GameplayInteractionModal.Create(canvas.transform);
        }

        private void EnsurePlayCardButton()
        {
            if (_playCardButton != null)
                return;

            foreach (var button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (button == null || button.gameObject.name != "PlayCardButton")
                    continue;

                _playCardButton = button;
                _playCardButtonLabel = button.GetComponentInChildren<TextMeshProUGUI>(true);
                _defaultPlayCardButtonText = _playCardButtonLabel != null ? _playCardButtonLabel.text : null;
                break;
            }
        }

        private void SetFavorButtonMode(bool active)
        {
            EnsurePlayCardButton();
            if (_playCardButtonLabel == null)
                return;

            _playCardButtonLabel.text = active
                ? "GỬI"
                : string.IsNullOrWhiteSpace(_defaultPlayCardButtonText) ? "ĐÁNH" : _defaultPlayCardButtonText;
        }

        private void EnsureGameplayHud()
        {
            if (canvas == null || _turnTimerHud != null)
                return;

            var root = new GameObject("GameplayHud", typeof(RectTransform));
            var rect = (RectTransform)root.transform;
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(24f, -24f);
            rect.sizeDelta = new Vector2(260f, 80f);

            var background = root.AddComponent<Image>();
            background.color = new Color(0.09f, 0.07f, 0.06f, 0.85f);
            var layout = root.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 8, 8);
            layout.childControlHeight = true;
            layout.childControlWidth = true;

            _turnTimerHud = CreateHudText("TurnTimer", root.transform);
            _drawPileHud = CreateHudText("DrawPileCount", root.transform);
        }

        private static TextMeshProUGUI CreateHudText(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = 22f;
            text.fontStyle = FontStyles.Bold;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Left;
            return text;
        }

        private void EnsureEliminatedOverlay()
        {
            if (_eliminatedOverlay != null || canvas == null)
                return;

            _eliminatedOverlay = new GameObject("EliminatedOverlay", typeof(RectTransform));
            _eliminatedOverlay.AddComponent<CanvasGroup>();
            var rect = (RectTransform)_eliminatedOverlay.transform;
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var background = _eliminatedOverlay.AddComponent<Image>();
            background.color = Color.clear;
            background.raycastTarget = false;
            var banner = new GameObject("SpectatorBanner", typeof(RectTransform));
            var bannerRect = (RectTransform)banner.transform;
            bannerRect.SetParent(_eliminatedOverlay.transform, false);
            bannerRect.anchorMin = bannerRect.anchorMax = new Vector2(0.5f, 0.62f);
            bannerRect.sizeDelta = new Vector2(760f, 190f);
            banner.AddComponent<Image>().color = new Color(0.08f, 0.035f, 0.03f, 0.94f);
            var outline = banner.AddComponent<Outline>();
            outline.effectColor = new Color(0.85f, 0.1f, 0.08f, 1f);
            outline.effectDistance = new Vector2(7f, -7f);
            var text = CreateHudText("Message", banner.transform);
            text.text = "BẠN ĐÃ BỊ LOẠI\nĐANG XEM TRẬN";
            text.fontSize = 48f;
            text.color = new Color(1f, 0.82f, 0.25f, 1f);
            text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(20f, 16f);
            text.rectTransform.offsetMax = new Vector2(-20f, -16f);
        }

        private string ResolvePlayerName(string userId)
        {
            var player = GameManager.Instance?.GetGameState()?.players?.FirstOrDefault(item =>
                string.Equals(item.userId, userId, StringComparison.OrdinalIgnoreCase));
            return !string.IsNullOrWhiteSpace(player?.nickname) ? player.nickname : "Người chơi";
        }

        private string ResolveReactionTargetText(string actorUserId, string[] targetUserIds, string effectScope)
        {
            switch (effectScope)
            {
                case "self":
                    return "Ảnh hưởng: bản thân";
                case "all_players":
                    return "Ảnh hưởng: tất cả người chơi";
                case "draw_pile":
                    return "Ảnh hưởng: chồng bài rút";
                case "discard_pile":
                    return "Ảnh hưởng: chồng bài bỏ";
            }

            var names = targetUserIds?
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(ResolvePlayerName)
                .Distinct()
                .ToArray();

            return names != null && names.Length > 0
                ? $"Ảnh hưởng: {string.Join(", ", names)}"
                : $"Ảnh hưởng: {ResolvePlayerName(actorUserId)}";
        }

    }
}
