using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Network.Websocket
{
    [Serializable]
    public class WsConnectedDto
    {
        public string roomCode;
        public string matchId;
    }

    [Serializable]
    public class WsAckDto
    {
        public long stateVersion;
    }

    [Serializable]
    public class WsErrorDto
    {
        public string message;
    }

    [Serializable]
    public class WsGameplayEventDto
    {
        [JsonProperty("type")]
        public string type;
        public string payload;
        public long stateVersion;

        [JsonIgnore]
        public WsGameplayEventType EventType => WsEventTypeParser.ParseGameplayEvent(type);

        [JsonIgnore]
        public WsGameplayPayloadBase ParsedPayload => WsGameplayPayloadParser.Parse(EventType, payload);
    }

    [Serializable]
    public class WsStateSnapshotDto
    {
        public string matchId;
        public string roomCode;
        public long stateVersion;
        public string phase;
        public DateTime? startedAt;
        public DateTime serverTimeUtc;
        public int turnIndex;
        public int turnCounter;
        public int turnTimerSeconds;
        public DateTime? turnEndsAt;
        public List<WsPlayerPublicStateDto> players;
        public List<string> selfHand;
        public int drawPileCount;
        public List<string> discardPile;
        public string pendingDefuseUserId;
        public string pendingBombOwnerUserId;
        public string pendingBombCardCode;
        public DateTime? defuseWindowEndsAt;
        public DateTime? bombReinsertWindowEndsAt;
        public string pendingReactionUserId;
        public string pendingReactionAction;
        public DateTime? reactionWindowEndsAt;
        public int pendingNopeCount;
        public string pendingFavorRequesterId;
        public string pendingFavorTargetId;
        public DateTime? favorWindowEndsAt;
    }

    [Serializable]
    public class WsPlayerPublicStateDto
    {
        public string userId;
        public string nickname;
        public bool connected;
        public string lifeState;
        public int handCount;
        public int pendingDrawCount;
        public DateTime? pendingReconnectUntil;
    }
}
