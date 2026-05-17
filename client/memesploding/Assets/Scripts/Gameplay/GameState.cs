using Network.Websocket;
using System;
using System.Collections.Generic;
using Managers;

namespace Gameplay
{
    public class GameState
    {
        public bool IsPlayerTurn
        {
            get
            {
                var player = GameManager.Instance.Player;

                if (player == null || players == null || players.Count == 0)
                    return false;

                if (turnIndex < 0 || turnIndex >= players.Count)
                    return false;

                return player.ID == players[turnIndex].userId;
            }
        }

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

        public void UpdateState(WsStateSnapshotDto state)
        {
            if (state == null) return;

            matchId = state.matchId;
            roomCode = state.roomCode;
            stateVersion = state.stateVersion;
            phase = state.phase;
            startedAt = state.startedAt;
            serverTimeUtc = state.serverTimeUtc;
            turnIndex = state.turnIndex;
            turnCounter = state.turnCounter;
            turnTimerSeconds = state.turnTimerSeconds;
            turnEndsAt = state.turnEndsAt;

            // Replace collections (avoid shared references if mutation is possible)
            players = state.players != null
                ? new List<WsPlayerPublicStateDto>(state.players)
                : new List<WsPlayerPublicStateDto>();

            selfHand = state.selfHand != null ? new List<string>(state.selfHand) : new List<string>();

            drawPileCount = state.drawPileCount;

            discardPile = state.discardPile != null ? new List<string>(state.discardPile) : new List<string>();

            pendingDefuseUserId = state.pendingDefuseUserId;
            pendingBombOwnerUserId = state.pendingBombOwnerUserId;
            pendingBombCardCode = state.pendingBombCardCode;

            defuseWindowEndsAt = state.defuseWindowEndsAt;
            bombReinsertWindowEndsAt = state.bombReinsertWindowEndsAt;

            pendingReactionUserId = state.pendingReactionUserId;
            pendingReactionAction = state.pendingReactionAction;
            reactionWindowEndsAt = state.reactionWindowEndsAt;

            pendingNopeCount = state.pendingNopeCount;

            pendingFavorRequesterId = state.pendingFavorRequesterId;
            pendingFavorTargetId = state.pendingFavorTargetId;
            favorWindowEndsAt = state.favorWindowEndsAt;
        }
    }
}
