using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Network.Websocket
{
    [Serializable]
    public abstract class WsGameplayPayloadBase { }

    [Serializable]
    public class WsEmptyGameplayPayload : WsGameplayPayloadBase { }

    [Serializable]
    public class WsUserPayload : WsGameplayPayloadBase
    {
        public string userId;
    }

    [Serializable]
    public class WsTurnIndexPayload : WsGameplayPayloadBase
    {
        public int turnIndex;
        public DateTime? turnEndsAt;
        public int turnTimerSeconds;
        public DateTime serverTimeUtc;
    }

    [Serializable]
    public class WsUnknownCommandPayload : WsGameplayPayloadBase
    {
        public string name;
    }

    [Serializable]
    public class WsActionRejectedPayload : WsGameplayPayloadBase
    {
        public string reason;
        public string userId;
        public string cardCode;
        public int required;
    }

    [Serializable]
    public class WsCardActionPayload : WsGameplayPayloadBase
    {
        public string userId;
        public string cardCode;
    }

    [Serializable]
    public class WsReactionWindowClosedPayload : WsGameplayPayloadBase
    {
        public string cardCode;
        public int nopeCount;
    }

    [Serializable]
    public class WsNopeActionPayload : WsGameplayPayloadBase
    {
        public string userId;
        public string cardCode;
        public int nopeCount;
    }

    [Serializable]
    public class WsAttackAppliedPayload : WsGameplayPayloadBase
    {
        [JsonProperty("from")]
        public string fromUserId;

        [JsonProperty("to")]
        public string toUserId;

        public int added;
    }

    [Serializable]
    public class WsTurnContinuesPayload : WsGameplayPayloadBase
    {
        public string userId;
        public int pendingDrawCount;
        public DateTime? turnEndsAt;
        public int turnTimerSeconds;
        public DateTime serverTimeUtc;
    }

    [Serializable]
    public class WsBombReinsertedPayload : WsGameplayPayloadBase
    {
        public string userId;
        public int position;
    }

    [Serializable]
    public class WsPlayerEliminatedPayload : WsGameplayPayloadBase
    {
        public string userId;
        public string reason;
    }

    [Serializable]
    public class WsMatchFinishedPayload : WsGameplayPayloadBase
    {
        public string winnerUserId;
    }

    [Serializable]
    public class WsFuturePeekedPayload : WsGameplayPayloadBase
    {
        public string userId;
        public string cardCode;
        public string[] cards;
    }

    [Serializable]
    public class WsComboPlayedPayload : WsGameplayPayloadBase
    {
        public string userId;
        public int comboSize;
        public string cardCode;
        public string comboCode;
    }

    [Serializable]
    public class WsTransferPayload : WsGameplayPayloadBase
    {
        [JsonProperty("from")]
        public string fromUserId;

        [JsonProperty("to")]
        public string toUserId;

        public string cardCode;
    }

    [Serializable]
    public class WsCatComboThreePayload : WsGameplayPayloadBase
    {
        [JsonProperty("from")]
        public string fromUserId;

        [JsonProperty("to")]
        public string toUserId;

        public string requestedCardCode;
    }

    [Serializable]
    public class WsCatComboFiveResolvedPayload : WsGameplayPayloadBase
    {
        public string userId;
        public string discardCardCode;
    }

    [Serializable]
    public class WsFavorTargetEmptyPayload : WsGameplayPayloadBase
    {
        [JsonProperty("from")]
        public string fromUserId;

        public string target;
    }

    [Serializable]
    public class WsFavorWindowOpenedPayload : WsGameplayPayloadBase
    {
        public string requesterId;
        public string targetId;
    }

    [Serializable]
    public class WsBuryResolvedPayload : WsGameplayPayloadBase
    {
        public string userId;
        public string card;
    }

    [Serializable]
    public class WsIllTakeThatMarkedPayload : WsGameplayPayloadBase
    {
        public string owner;
        public string target;
    }

    [Serializable]
    public class WsTowerMaskUpdatedPayload : WsGameplayPayloadBase
    {
        public string userId;
        public bool active;
    }

    [Serializable]
    public class WsFromTargetPayload : WsGameplayPayloadBase
    {
        [JsonProperty("from")]
        public string fromUserId;

        public string target;
    }

    [Serializable]
    public class WsCatomicBombResolvedPayload : WsGameplayPayloadBase
    {
        public int bombCount;
    }

    public static class WsGameplayPayloadParser
    {
        public static WsGameplayPayloadBase Parse(WsGameplayEventType eventType, string payload)
        {
            if (string.IsNullOrWhiteSpace(payload) || payload == "{}")
                return new WsEmptyGameplayPayload();

            switch (eventType)
            {
                case WsGameplayEventType.TurnTimeoutAutoDraw:
                case WsGameplayEventType.ExplosionTriggered:
                case WsGameplayEventType.ImplodingReinsertRequired:
                case WsGameplayEventType.DefuseUsed:
                case WsGameplayEventType.SkipApplied:
                case WsGameplayEventType.ReconnectAck:
                case WsGameplayEventType.StreakingKittenActive:
                case WsGameplayEventType.FeralCatPlayed:
                    return Deserialize<WsUserPayload>(payload);

                case WsGameplayEventType.TurnStarted:
                case WsGameplayEventType.TurnChanged:
                    return Deserialize<WsTurnIndexPayload>(payload);

                case WsGameplayEventType.UnknownCommand:
                    return Deserialize<WsUnknownCommandPayload>(payload);

                case WsGameplayEventType.ActionRejected:
                    return Deserialize<WsActionRejectedPayload>(payload);

                case WsGameplayEventType.CardPlayed:
                case WsGameplayEventType.ReactionWindowOpened:
                case WsGameplayEventType.CardDrawn:
                case WsGameplayEventType.CardEffectUnhandled:
                    return Deserialize<WsCardActionPayload>(payload);

                case WsGameplayEventType.NopePlayed:
                case WsGameplayEventType.ActionNoped:
                    return Deserialize<WsNopeActionPayload>(payload);

                case WsGameplayEventType.ReactionWindowClosed:
                    return Deserialize<WsReactionWindowClosedPayload>(payload);

                case WsGameplayEventType.AttackApplied:
                    return Deserialize<WsAttackAppliedPayload>(payload);

                case WsGameplayEventType.TurnContinues:
                    return Deserialize<WsTurnContinuesPayload>(payload);

                case WsGameplayEventType.BombReinserted:
                    return Deserialize<WsBombReinsertedPayload>(payload);

                case WsGameplayEventType.PlayerEliminated:
                    return Deserialize<WsPlayerEliminatedPayload>(payload);

                case WsGameplayEventType.MatchFinished:
                    return Deserialize<WsMatchFinishedPayload>(payload);

                case WsGameplayEventType.FuturePeeked:
                    return DeserializeFuturePeeked(payload);

                case WsGameplayEventType.ComboPlayed:
                    return Deserialize<WsComboPlayedPayload>(payload);

                case WsGameplayEventType.CatComboTwoResolved:
                case WsGameplayEventType.FavorResolved:
                case WsGameplayEventType.BarkingKittenResolved:
                    return Deserialize<WsTransferPayload>(payload);

                case WsGameplayEventType.CatComboThreeMiss:
                case WsGameplayEventType.CatComboThreeResolved:
                    return Deserialize<WsCatComboThreePayload>(payload);

                case WsGameplayEventType.CatComboFiveResolved:
                    return Deserialize<WsCatComboFiveResolvedPayload>(payload);

                case WsGameplayEventType.FavorTargetEmpty:
                    return Deserialize<WsFavorTargetEmptyPayload>(payload);

                case WsGameplayEventType.FavorWindowOpened:
                    return Deserialize<WsFavorWindowOpenedPayload>(payload);

                case WsGameplayEventType.BuryResolved:
                    return Deserialize<WsBuryResolvedPayload>(payload);

                case WsGameplayEventType.IllTakeThatMarked:
                    return Deserialize<WsIllTakeThatMarkedPayload>(payload);

                case WsGameplayEventType.TowerMaskUpdated:
                    return Deserialize<WsTowerMaskUpdatedPayload>(payload);

                case WsGameplayEventType.MarkApplied:
                case WsGameplayEventType.CatButtCurseApplied:
                    return Deserialize<WsFromTargetPayload>(payload);

                case WsGameplayEventType.CatomicBombResolved:
                    return Deserialize<WsCatomicBombResolvedPayload>(payload);

                case WsGameplayEventType.BombReinsertAuto:
                case WsGameplayEventType.ShuffleApplied:
                case WsGameplayEventType.MatchStarted:
                case WsGameplayEventType.DrawPileEmpty:
                case WsGameplayEventType.StreakingHoldsBomb:
                case WsGameplayEventType.SwapTopBottom:
                case WsGameplayEventType.GarbageCollectionResolved:
                case WsGameplayEventType.Unknown:
                default:
                    return DeserializeOrEmpty(payload);
            }
        }

        private static WsGameplayPayloadBase DeserializeOrEmpty(string payload)
        {
            try
            {
                return JsonConvert.DeserializeObject<WsEmptyGameplayPayload>(payload) ?? new WsEmptyGameplayPayload();
            }
            catch
            {
                return new WsEmptyGameplayPayload();
            }
        }

        private static WsGameplayPayloadBase Deserialize<T>(string payload) where T : WsGameplayPayloadBase, new()
        {
            try
            {
                return JsonConvert.DeserializeObject<T>(payload) ?? new T();
            }
            catch
            {
                return new T();
            }
        }

        private static WsGameplayPayloadBase DeserializeFuturePeeked(string payload)
        {
            try
            {
                var token = JToken.Parse(payload);
                if (token.Type == JTokenType.Array)
                {
                    return new WsFuturePeekedPayload
                    {
                        cards = token.ToObject<string[]>()
                    };
                }

                return token.ToObject<WsFuturePeekedPayload>() ?? new WsFuturePeekedPayload();
            }
            catch
            {
                return new WsFuturePeekedPayload();
            }
        }
    }
}
