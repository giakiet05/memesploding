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
            EnsureInputModule();
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

            opponentDrawCard ??= playingArea != null ? playingArea.Find("OpponentDrawCard")?.GetComponent<OpponentDrawCard>() : null;
            if (opponentDrawCard == null)
                Debug.LogError("[GameplayUIManager] OpponentDrawCard reference is missing.");
        }

        [Header("Settings")]
        [SerializeField] private GameObject settingsPopupPrefab; // legacy – no longer required
        private GameObject _settingsPopupInstance;
        private bool _isSettingsVisible;

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
        private TextMeshProUGUI _pendingDrawHud;
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
            EnsureHelpButton();
            BindSettingButton();
        }

        private void BindSettingButton()
        {
            // Strategy 1: search under assigned canvas
            Transform settingBtnTransform = canvas != null ? canvas.transform.Find("Setting Button") : null;

            // Strategy 2: search all canvases in scene
            if (settingBtnTransform == null)
            {
                foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                {
                    settingBtnTransform = c.transform.Find("Setting Button");
                    if (settingBtnTransform != null)
                    {
                        canvas ??= c; // also capture canvas if not already set
                        break;
                    }
                }
            }

            // Strategy 3: find by name anywhere in scene
            if (settingBtnTransform == null)
            {
                var go = GameObject.Find("Setting Button");
                if (go != null) settingBtnTransform = go.transform;
            }

            if (settingBtnTransform != null)
            {
                var settingBtn = settingBtnTransform.GetComponent<Button>();
                if (settingBtn != null)
                {
                    settingBtn.onClick = new Button.ButtonClickedEvent();
                    settingBtn.onClick.AddListener(ToggleSettingsPopup);
                    Debug.Log("[GameplayUIManager] Setting Button bound successfully.");
                }
                else
                {
                    Debug.LogWarning("[GameplayUIManager] 'Setting Button' found but has no Button component.");
                }
            }
            else
            {
                Debug.LogWarning("[GameplayUIManager] 'Setting Button' not found in scene.");
            }
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

            if (_pendingDrawHud != null && GameManager.Instance != null)
            {
                var pendingDraw = GameManager.Instance.GetLocalPendingDrawCount();
                _pendingDrawHud.gameObject.SetActive(pendingDraw > 0);
                if (pendingDraw > 0)
                    _pendingDrawHud.text = $"PHẢI RÚT: {pendingDraw} LÁ";
            }

            if (_playCardButton != null && GameManager.Instance != null)
            {
                var shouldShowPlayButton = GameManager.Instance.CanPlayLocalCombo() || GameManager.Instance.IsLocalFavorTarget();
                if (_playCardButton.gameObject.activeSelf != shouldShowPlayButton)
                    _playCardButton.gameObject.SetActive(shouldShowPlayButton);
            }

            // Toggle Settings Popup on Escape key press
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                ToggleSettingsPopup();
            }
        }

        public void ToggleSettingsPopup()
        {
            if (_settingsPopupInstance == null)
            {
                // Fallback: find canvas if not assigned
                var targetCanvas = canvas != null ? canvas : FindFirstObjectByType<Canvas>();
                if (targetCanvas == null)
                {
                    Debug.LogError("[GameplayUIManager] No Canvas found for Settings popup.");
                    return;
                }

                var prefab = settingsPopupPrefab;
                if (prefab == null)
                {
                    prefab = Resources.Load<GameObject>("PopupsSetting");
                }

                if (prefab != null)
                {
                    _settingsPopupInstance = Instantiate(prefab, targetCanvas.transform);
                    _settingsPopupInstance.name = "PopupsSetting";
                    
                    var controller = _settingsPopupInstance.GetComponent<SettingsPopupController>() 
                                     ?? _settingsPopupInstance.AddComponent<SettingsPopupController>();
                    
                    controller.RefreshUi();
                    _isSettingsVisible = false;
                }
                else
                {
                    Debug.LogError("[GameplayUIManager] Settings popup prefab is null and could not be loaded.");
                    return;
                }
            }

            _isSettingsVisible = !_isSettingsVisible;
            _settingsPopupInstance.SetActive(_isSettingsVisible);
            if (_isSettingsVisible)
            {
                _settingsPopupInstance.transform.SetAsLastSibling();
                var controller = _settingsPopupInstance.GetComponent<SettingsPopupController>();
                if (controller != null)
                {
                    controller.RefreshUi();
                }
            }
        }

        /// <summary>Called by the popup's close/backdrop button to sync the visibility flag.</summary>
        public void OnSettingsPopupClosed()
        {
            _isSettingsVisible = false;
            if (_settingsPopupInstance != null)
                _settingsPopupInstance.SetActive(false);
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

                case "Cat1":
                case "Cat2":
                case "Cat3":
                case "Cat4":
                case "Cat5":
                case "ExplodingKitten":
                case "ImplodingKitten":
                    CardManager.Instance?.RejectPendingPlay();
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
                        _interactionModal.ShowCardOptions(
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
                _interactionModal.ShowCardOptions(
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

        public void ShowMatchFinishedConfirm(string title, string message, Action onExit, bool showCancel = false)
        {
            EnsureInteractionModal();
            _interactionModal.ShowConfirm(
                title,
                message,
                "THOÁT RA MENU",
                null,
                onExit,
                showCancel);
        }
        
        //Draw Card
        public bool DisplayDrawnCard(string cardCode, Action onAnimationFinished = null)
        {
            if (drawnCardDisplayer == null || playingArea == null)
                return false;

            if (drawnCardDisplayer.transform.parent != playingArea)
                drawnCardDisplayer.transform.SetParent(playingArea, false);
            drawnCardDisplayer.gameObject.SetActive(true);
            var deckWorldPosition = opponentDrawCard != null ? opponentDrawCard.transform.position : playingArea.position;
            var handWorldPosition = CardManager.Instance?.HandLayout != null
                ? CardManager.Instance.HandLayout.transform.position
                : playingArea.position;
            return drawnCardDisplayer.PlayDrawCardAnimation(
                cardCode,
                deckWorldPosition,
                handWorldPosition,
                onAnimationFinished);
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

        public void ShowActionToast(string title, string message, string detail = null, string status = null, bool danger = false, DateTime? endsAtUtc = null)
        {
            EnsureReactionWindowView();
            reactionWindowView?.ShowNotification(title, message, detail, status, danger, endsAtUtc);
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

        public void ShowFavorWindow(string requesterId, string targetId, DateTime? endsAtUtc = null)
        {
            if (!GameManager.Instance.IsLocalFavorTarget())
                return;

            CardManager.Instance?.HandLayout?.ClearCardSelection();
            HideInteractionModal();
            SetFavorButtonMode(true);
            ShowActionToast(
                "FAVOR",
                $"Chọn một lá trong tay để đưa cho {ResolvePlayerName(requesterId)}.",
                "Bấm GỬI sau khi chọn bài",
                "CHỌN BÀI",
                false,
                endsAtUtc);
        }

        public void HideFavorWindow()
        {
            CardManager.Instance?.HandLayout?.ClearCardSelection();
            SetFavorButtonMode(false);
            HideInteractionModal();
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
                rect.anchoredPosition = centerLocalPos + new Vector2(x, y - 70f);
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

        public void AnimateFavorTransfer(string cardCode, string targetUserId)
        {
            if (CardManager.Instance == null || playingArea == null ||
                _opponentsUI == null || !_opponentsUI.TryGetValue(targetUserId, out var target) || target == null)
                return;

            var card = CardManager.Instance.CreatePlayableCard(cardCode, playingArea);
            if (card == null)
                return;

            var start = WorldToPlayingAreaPoint(CardManager.Instance.HandLayout.transform.position);
            var end = WorldToPlayingAreaPoint(target.transform.position);
            StartCoroutine(AnimateTransferCard(card.RectTransform, start, end));
        }

        private System.Collections.IEnumerator AnimateTransferCard(RectTransform rect, Vector2 start, Vector2 end)
        {
            rect.anchoredPosition = start;
            rect.localScale = Vector3.one;
            for (var elapsed = 0f; elapsed < 0.55f; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.SmoothStep(0f, 1f, elapsed / 0.55f);
                rect.anchoredPosition = Vector2.Lerp(start, end, t) + Vector2.up * Mathf.Sin(t * Mathf.PI) * 90f;
                rect.localScale = Vector3.Lerp(Vector3.one, new Vector3(0.65f, 0.65f, 1f), t);
                yield return null;
            }
            Destroy(rect.gameObject);
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

        private void EnsureHelpButton()
        {
            if (canvas == null)
                canvas = GetComponentInParent<Canvas>() ?? FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            var settingBtnTransform = canvas.transform.Find("Setting Button");
            if (settingBtnTransform == null)
            {
                foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                {
                    settingBtnTransform = c.transform.Find("Setting Button");
                    if (settingBtnTransform != null)
                    {
                        canvas = c;
                        break;
                    }
                }
            }

            if (settingBtnTransform == null)
            {
                var go = GameObject.Find("Setting Button");
                if (go != null)
                {
                    settingBtnTransform = go.transform;
                    if (go.GetComponentInParent<Canvas>() != null)
                        canvas = go.GetComponentInParent<Canvas>();
                }
            }

            if (settingBtnTransform == null) return;

            var helpBtnTransform = canvas.transform.Find("Help Button");
            if (helpBtnTransform != null) return; // Already exists

            // Instantiate settingBtn as a template for helpBtn
            var helpBtnObj = Instantiate(settingBtnTransform.gameObject, canvas.transform);
            helpBtnObj.name = "Help Button";
            var helpRect = helpBtnObj.GetComponent<RectTransform>();
            var settingRect = settingBtnTransform.GetComponent<RectTransform>();

            // Copy transform settings
            helpRect.anchorMin = settingRect.anchorMin;
            helpRect.anchorMax = settingRect.anchorMax;
            helpRect.pivot = settingRect.pivot;
            
            // Calculate dynamic position next to the Settings Button
            float width = settingRect.rect.width > 0 ? settingRect.rect.width : settingRect.sizeDelta.x;
            if (width <= 0) width = 80f; // fallback
            float offset = -(width + 24f);
            helpRect.anchoredPosition = new Vector2(settingRect.anchoredPosition.x + offset, settingRect.anchoredPosition.y);
            helpRect.sizeDelta = settingRect.sizeDelta;

            // Change the icon to a "?" question mark
            var imageChild = helpBtnObj.transform.Find("Image")?.gameObject;
            if (imageChild != null)
            {
                DestroyImmediate(imageChild);
            }

            // Add text child for "?"
            var textObj = new GameObject("Text (TMP)", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObj.transform.SetParent(helpBtnObj.transform, false);
            var textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var tmpText = textObj.GetComponent<TextMeshProUGUI>();
            tmpText.text = "?";
            tmpText.fontSize = 44f;
            tmpText.color = new Color(0.09f, 0.07f, 0.06f, 1f); // Ink
            tmpText.alignment = TextAlignmentOptions.Center;
            
            // Set font if possible
            var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            foreach (var f in fonts)
            {
                if (f.name.Contains("Bangers"))
                {
                    tmpText.font = f;
                    break;
                }
            }

            // Hook up onClick listener
            var helpBtn = helpBtnObj.GetComponent<Button>();
            if (helpBtn != null)
            {
                helpBtn.onClick = new Button.ButtonClickedEvent(); // Reset completely to avoid opening settings too!
                helpBtn.onClick.AddListener(UI.GameplayGuidePopup.Show);
            }
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
            rect.sizeDelta = new Vector2(285f, 92f);

            var shadow = root.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.09f, 0.07f, 0.06f, 0.75f);
            shadow.effectDistance = new Vector2(7f, -7f);
            var background = root.AddComponent<Image>();
            background.color = new Color(1f, 0.94f, 0.72f, 0.98f);
            var outline = root.AddComponent<Outline>();
            outline.effectColor = new Color(0.09f, 0.07f, 0.06f, 1f);
            outline.effectDistance = new Vector2(3f, -3f);

            var layout = root.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 10, 10);
            layout.spacing = 2f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;

            _turnTimerHud = CreateHudText("TurnTimer", root.transform);
            _drawPileHud = CreateHudText("DrawPileCount", root.transform);
            _pendingDrawHud = CreateHudText("PendingDrawCount", root.transform);
            _turnTimerHud.color = new Color(0.86f, 0.12f, 0.1f, 1f);
            _drawPileHud.color = new Color(0.09f, 0.07f, 0.06f, 1f);
            _pendingDrawHud.color = new Color(0.86f, 0.55f, 0.05f, 1f); // amber — warning color
            _pendingDrawHud.gameObject.SetActive(false);
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
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(24f, -132f);
            rect.sizeDelta = new Vector2(550f, 72f);
            var background = _eliminatedOverlay.AddComponent<Image>();
            background.color = new Color(1f, 0.94f, 0.72f, 0.97f);
            background.raycastTarget = false;
            var shadow = _eliminatedOverlay.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.09f, 0.07f, 0.06f, 0.8f);
            shadow.effectDistance = new Vector2(7f, -7f);
            var outline = _eliminatedOverlay.AddComponent<Outline>();
            outline.effectColor = new Color(0.86f, 0.12f, 0.1f, 1f);
            outline.effectDistance = new Vector2(4f, -4f);

            var text = CreateHudText("Message", _eliminatedOverlay.transform);
            text.text = "BẠN ĐÃ BỊ LOẠI  •  ĐANG XEM TRẬN";
            text.fontSize = 20f;
            text.color = new Color(0.86f, 0.12f, 0.1f, 1f);
            text.alignment = TextAlignmentOptions.Left;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(24f, 10f);
            text.rectTransform.offsetMax = new Vector2(-170f, -10f);

            var buttonGo = new GameObject("ExitButton", typeof(RectTransform));
            var buttonRect = (RectTransform)buttonGo.transform;
            buttonRect.SetParent(_eliminatedOverlay.transform, false);
            buttonRect.anchorMin = new Vector2(1f, 0.5f);
            buttonRect.anchorMax = new Vector2(1f, 0.5f);
            buttonRect.pivot = new Vector2(1f, 0.5f);
            buttonRect.anchoredPosition = new Vector2(-20f, 0f);
            buttonRect.sizeDelta = new Vector2(130f, 44f);

            var buttonImage = buttonGo.AddComponent<Image>();
            buttonImage.color = new Color(0.86f, 0.12f, 0.1f, 1f);
            buttonImage.raycastTarget = true;

            var buttonOutline = buttonGo.AddComponent<Outline>();
            buttonOutline.effectColor = new Color(0.09f, 0.07f, 0.06f, 1f);
            buttonOutline.effectDistance = new Vector2(2f, -2f);

            var btn = buttonGo.AddComponent<Button>();
            btn.onClick.AddListener(() => NavigationManager.Instance.LoadMainMenu());

            var btnText = CreateHudText("Label", buttonGo.transform);
            btnText.text = "THOÁT";
            btnText.fontSize = 18f;
            btnText.fontStyle = FontStyles.Bold;
            btnText.color = Color.white;
            btnText.alignment = TextAlignmentOptions.Center;
            btnText.rectTransform.anchorMin = Vector2.zero;
            btnText.rectTransform.anchorMax = Vector2.one;
            btnText.rectTransform.offsetMin = Vector2.zero;
            btnText.rectTransform.offsetMax = Vector2.zero;
        }

        private string ResolvePlayerName(string userId)
        {
            if (string.Equals(userId, GameManager.Instance?.Player?.ID, StringComparison.OrdinalIgnoreCase))
            {
                return "Bạn";
            }

            var player = GameManager.Instance?.GetGameState()?.players?.FirstOrDefault(item =>
                string.Equals(item.userId, userId, StringComparison.OrdinalIgnoreCase));
            return !string.IsNullOrWhiteSpace(player?.nickname) ? player.nickname : "Người chơi";
        }

        private string ResolveReactionTargetText(string actorUserId, string[] targetUserIds, string effectScope)
        {
            switch (effectScope)
            {
                case "self":
                    return $"Ảnh hưởng: {ResolvePlayerName(actorUserId)}";
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

        private void EnsureInputModule()
        {
            var eventSystem = FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (eventSystem != null)
            {
                var legacyInput = eventSystem.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                if (legacyInput != null)
                {
                    DestroyImmediate(legacyInput);
                    eventSystem.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                    Debug.Log($"[InputHelper] Successfully upgraded EventSystem in scene {gameObject.scene.name} to InputSystemUIInputModule.");
                }
            }
        }
    }
}
