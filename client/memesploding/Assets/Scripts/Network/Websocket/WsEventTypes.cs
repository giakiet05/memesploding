namespace Network.Websocket
{
    public enum WsServerEventType
    {
        Unknown = 0,
        Connected,
        Ack,
        Error,
        GameplayEvent,
        StateSnapshot,
        MatchEnded
    }

    public enum WsGameplayEventType
    {
        Unknown = 0,
        TurnStarted,
        TurnChanged,
        TurnContinues,
        CardDrawn,
        CardPlayed,
        FuturePeeked,
        MatchStarted,
        MatchFinished,
        PlayerEliminated,
        ActionRejected,
        ExplosionTriggered,
        DefuseUsed,
        BombReinserted,
        ReconnectAck
    }

    public enum WsClientCommandType
    {
        Unknown = 0,
        Heartbeat,
        DrawCard,
        DrawFromBottom,
        PlayCard,
        Nope,
        UseDefuse,
        ChooseBombInsertPosition,
        ChooseFavorCard,
        ReconnectMatch,
        RequestStateSnapshot
    }

    public static class WsEventTypeParser
    {
        public static WsServerEventType ParseServerEvent(string eventName)
        {
            if (string.IsNullOrWhiteSpace(eventName))
                return WsServerEventType.Unknown;

            switch (eventName.Trim().ToLowerInvariant())
            {
                case "connected": return WsServerEventType.Connected;
                case "ack": return WsServerEventType.Ack;
                case "error": return WsServerEventType.Error;
                case "gameplay_event": return WsServerEventType.GameplayEvent;
                case "state_snapshot": return WsServerEventType.StateSnapshot;
                case "match_ended": return WsServerEventType.MatchEnded;
                default: return WsServerEventType.Unknown;
            }
        }

        public static WsGameplayEventType ParseGameplayEvent(string eventType)
        {
            if (string.IsNullOrWhiteSpace(eventType))
                return WsGameplayEventType.Unknown;

            switch (eventType.Trim().ToLowerInvariant())
            {
                case "turn_started": return WsGameplayEventType.TurnStarted;
                case "turn_changed": return WsGameplayEventType.TurnChanged;
                case "turn_continues": return WsGameplayEventType.TurnContinues;
                case "card_drawn": return WsGameplayEventType.CardDrawn;
                case "card_played": return WsGameplayEventType.CardPlayed;
                case "future_peeked": return WsGameplayEventType.FuturePeeked;
                case "match_started": return WsGameplayEventType.MatchStarted;
                case "match_finished": return WsGameplayEventType.MatchFinished;
                case "player_eliminated": return WsGameplayEventType.PlayerEliminated;
                case "action_rejected": return WsGameplayEventType.ActionRejected;
                case "explosion_triggered": return WsGameplayEventType.ExplosionTriggered;
                case "defuse_used": return WsGameplayEventType.DefuseUsed;
                case "bomb_reinserted": return WsGameplayEventType.BombReinserted;
                case "reconnect_ack": return WsGameplayEventType.ReconnectAck;
                default: return WsGameplayEventType.Unknown;
            }
        }

        public static string ToCommandName(WsClientCommandType commandType)
        {
            switch (commandType)
            {
                case WsClientCommandType.Heartbeat: return "heartbeat";
                case WsClientCommandType.DrawCard: return "drawcard";
                case WsClientCommandType.DrawFromBottom: return "drawfrombottom";
                case WsClientCommandType.PlayCard: return "playcard";
                case WsClientCommandType.Nope: return "nope";
                case WsClientCommandType.UseDefuse: return "usedefuse";
                case WsClientCommandType.ChooseBombInsertPosition: return "choosebombinsertposition";
                case WsClientCommandType.ChooseFavorCard: return "choosefavorcard";
                case WsClientCommandType.ReconnectMatch: return "reconnectmatch";
                case WsClientCommandType.RequestStateSnapshot: return "requeststatesnapshot";
                default: return string.Empty;
            }
        }
    }
}
