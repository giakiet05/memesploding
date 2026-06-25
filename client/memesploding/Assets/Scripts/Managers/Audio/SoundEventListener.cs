using Events;
using Events.GameEvents;
using Managers;
using Network.Websocket;
using System;
using UnityEngine;
using EventType = Events.EventType;

namespace Managers.Audio
{
    /// <summary>
    /// Bridges the EventBus to SoundManager.
    /// Subscribes to game events and maps them to SoundEvents.
    ///
    /// Attach to any persistent GameObject (e.g. alongside GameManager).
    /// GameManager.Awake() adds this as a component automatically.
    /// </summary>
    public class SoundEventListener : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            var go = new GameObject("[AudioSystem]");
            DontDestroyOnLoad(go);
            go.AddComponent<SoundEventListener>();
            SoundManager.EnsureInstance();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<WsGameplayEventPayload>(EventType.WsGameplayEvent, OnGameplayEvent);
            EventBus.Subscribe<TurnStartEventPayload>(EventType.TurnStart, OnTurnStart);
            EventBus.Subscribe<SceneChangedEventPayload>(EventType.SceneChanged, OnSceneChanged);
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnUnitySceneLoaded;
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<WsGameplayEventPayload>(EventType.WsGameplayEvent, OnGameplayEvent);
            EventBus.Unsubscribe<TurnStartEventPayload>(EventType.TurnStart, OnTurnStart);
            EventBus.Unsubscribe<SceneChangedEventPayload>(EventType.SceneChanged, OnSceneChanged);
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnUnitySceneLoaded;
        }

        private void Start()
        {
            // BGM will be handled by OnUnitySceneLoaded for the initial scene too, 
            // since sceneLoaded fires when the scene is loaded. However, if this Start 
            // happens after sceneLoaded, we ensure it's playing.
            CheckAndPlayBgm(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }

        private void OnUnitySceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            CheckAndPlayBgm(scene.name);
        }

        private void CheckAndPlayBgm(string toScene)
        {
            if (string.Equals(toScene, "Gameplay", StringComparison.OrdinalIgnoreCase))
            {
                SoundManager.PlaySound(SoundEvent.BgmGameplay);
            }
            else if (IsMenuScene(toScene))
            {
                SoundManager.PlaySound(SoundEvent.BgmMainMenu);
            }
        }

        private void OnSceneChanged(SceneChangedEventPayload payload)
        {
            if (payload == null) return;
            CheckAndPlayBgm(payload.ToScene);
        }

        private bool IsMenuScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;
            return string.Equals(sceneName, "Welcome", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(sceneName, "MainMenu", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(sceneName, "CreateRoom", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(sceneName, "JoinRoom", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(sceneName, "WaitRoom", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(sceneName, "Profile", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(sceneName, "MatchHistory", StringComparison.OrdinalIgnoreCase);
        }

        // ── Turn ──────────────────────────────────────────────────────────────────

        private void OnTurnStart(TurnStartEventPayload payload)
        {
            if (payload == null) return;

            bool isSelf = string.Equals(
                payload.UserID,
                GameManager.Instance?.Player?.ID,
                StringComparison.OrdinalIgnoreCase);

            SoundManager.PlaySound(isSelf ? SoundEvent.TurnStartSelf : SoundEvent.TurnStartOther);
        }

        // ── Gameplay Events ───────────────────────────────────────────────────────

        private void OnGameplayEvent(WsGameplayEventPayload payload)
        {
            if (payload?.Data == null) return;

            switch (payload.EventType)
            {
                // Card
                case WsGameplayEventType.CardDrawn:
                    SoundManager.PlaySound(SoundEvent.CardDraw);
                    break;

                case WsGameplayEventType.CardPlayed:
                    SoundManager.PlaySound(SoundEvent.CardPlay);
                    break;

                case WsGameplayEventType.ComboPlayed:
                    SoundManager.PlaySound(SoundEvent.CardCombo);
                    break;

                case WsGameplayEventType.ShuffleApplied:
                    SoundManager.PlaySound(SoundEvent.CardShuffle);
                    break;

                // Bomb
                case WsGameplayEventType.ExplosionTriggered:
                    SoundManager.PlaySound(SoundEvent.ExplosionTriggered);
                    break;

                case WsGameplayEventType.DefuseUsed:
                    SoundManager.PlaySound(SoundEvent.DefuseUsed);
                    break;

                case WsGameplayEventType.BombReinserted:
                case WsGameplayEventType.BombReinsertAuto:
                    SoundManager.PlaySound(SoundEvent.BombReinserted);
                    break;

                // Reaction
                case WsGameplayEventType.ReactionWindowOpened:
                    SoundManager.PlaySound(SoundEvent.ReactionWindowOpen);
                    break;

                case WsGameplayEventType.NopePlayed:
                    SoundManager.PlaySound(SoundEvent.NopePlayed);
                    break;

                // Turn
                case WsGameplayEventType.TurnTimeoutAutoDraw:
                    SoundManager.PlaySound(SoundEvent.TurnTimeout);
                    break;

                // Special effects
                case WsGameplayEventType.AttackApplied:
                    SoundManager.PlaySound(SoundEvent.AttackApplied);
                    break;

                case WsGameplayEventType.FavorWindowOpened:
                    SoundManager.PlaySound(SoundEvent.FavorRequested);
                    break;

                case WsGameplayEventType.FavorResolved:
                    SoundManager.PlaySound(SoundEvent.FavorResolved);
                    break;

                case WsGameplayEventType.FuturePeeked:
                    HandleFuturePeeked(payload);
                    break;

                // Player status
                case WsGameplayEventType.PlayerEliminated:
                    HandlePlayerEliminated(payload);
                    break;

                case WsGameplayEventType.MatchFinished:
                    HandleMatchFinished(payload);
                    break;
            }
        }

        // ── Contextual Handlers ───────────────────────────────────────────────────

        private void HandleFuturePeeked(WsGameplayEventPayload payload)
        {
            if (payload.ParsedPayload is not WsFuturePeekedPayload peek) return;
            bool isSelf = string.Equals(
                peek.userId,
                GameManager.Instance?.Player?.ID,
                StringComparison.OrdinalIgnoreCase);

            if (isSelf)
                SoundManager.PlaySound(SoundEvent.FuturePeeked);
        }

        private void HandlePlayerEliminated(WsGameplayEventPayload payload)
        {
            if (payload.ParsedPayload is not WsPlayerEliminatedPayload eliminated) return;
            bool isSelf = string.Equals(
                eliminated.userId,
                GameManager.Instance?.Player?.ID,
                StringComparison.OrdinalIgnoreCase);

            SoundManager.PlaySound(isSelf
                ? SoundEvent.PlayerEliminatedSelf
                : SoundEvent.PlayerEliminatedOther);
        }

        private void HandleMatchFinished(WsGameplayEventPayload payload)
        {
            if (payload.ParsedPayload is not WsMatchFinishedPayload finished) return;
            bool isWinner = string.Equals(
                finished.winnerUserId,
                GameManager.Instance?.Player?.ID,
                StringComparison.OrdinalIgnoreCase);

            SoundManager.PlaySound(isWinner ? SoundEvent.MatchVictory : SoundEvent.MatchDefeat);
        }
    }
}
