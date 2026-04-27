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
        TurnTimeoutAutoDraw,
        BombReinsertAuto,
        SkipApplied,
        ShuffleApplied,
        TurnStarted,
        TurnChanged,
        TurnContinues,
        CardDrawn,
        CardPlayed,
        UnknownCommand,
        ReactionWindowOpened,
        ReactionWindowClosed,
        AttackApplied,
        DrawPileEmpty,
        StreakingHoldsBomb,
        FuturePeeked,
        MatchStarted,
        MatchFinished,
        PlayerEliminated,
        ActionRejected,
        ExplosionTriggered,
        ImplodingReinsertRequired,
        DefuseUsed,
        BombReinserted,
        ReconnectAck,
        ComboPlayed,
        CatComboTwoResolved,
        CatComboThreeMiss,
        CatComboThreeResolved,
        CatComboFiveResolved,
        FavorTargetEmpty,
        FavorWindowOpened,
        FavorResolved,
        BuryResolved,
        IllTakeThatMarked,
        TowerMaskUpdated,
        MarkApplied,
        CatButtCurseApplied,
        SwapTopBottom,
        GarbageCollectionResolved,
        CatomicBombResolved,
        BarkingKittenResolved,
        StreakingKittenActive,
        FeralCatPlayed,
        CardEffectUnhandled
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

            var key = NormalizeEventKey(eventType);
            switch (key)
            {
                case "turnstarted": return WsGameplayEventType.TurnStarted;
                case "turnchanged": return WsGameplayEventType.TurnChanged;
                case "turncontinues": return WsGameplayEventType.TurnContinues;
                case "carddrawn": return WsGameplayEventType.CardDrawn;
                case "cardplayed": return WsGameplayEventType.CardPlayed;
                case "turntimeoutautodraw": return WsGameplayEventType.TurnTimeoutAutoDraw;
                case "bombreinsertauto": return WsGameplayEventType.BombReinsertAuto;
                case "skipapplied": return WsGameplayEventType.SkipApplied;
                case "shuffleapplied": return WsGameplayEventType.ShuffleApplied;
                case "unknowncommand": return WsGameplayEventType.UnknownCommand;
                case "reactionwindowopened": return WsGameplayEventType.ReactionWindowOpened;
                case "reactionwindowclosed": return WsGameplayEventType.ReactionWindowClosed;
                case "attackapplied": return WsGameplayEventType.AttackApplied;
                case "drawpileempty": return WsGameplayEventType.DrawPileEmpty;
                case "streakingholdsbomb": return WsGameplayEventType.StreakingHoldsBomb;
                case "futurepeeked": return WsGameplayEventType.FuturePeeked;
                case "matchstarted": return WsGameplayEventType.MatchStarted;
                case "matchfinished": return WsGameplayEventType.MatchFinished;
                case "playereliminated": return WsGameplayEventType.PlayerEliminated;
                case "actionrejected": return WsGameplayEventType.ActionRejected;
                case "explosiontriggered": return WsGameplayEventType.ExplosionTriggered;
                case "implodingreinsertrequired": return WsGameplayEventType.ImplodingReinsertRequired;
                case "defuseused": return WsGameplayEventType.DefuseUsed;
                case "bombreinserted": return WsGameplayEventType.BombReinserted;
                case "reconnectack": return WsGameplayEventType.ReconnectAck;
                case "comboplayed": return WsGameplayEventType.ComboPlayed;
                case "catcombotworesolved": return WsGameplayEventType.CatComboTwoResolved;
                case "catcombothreemiss": return WsGameplayEventType.CatComboThreeMiss;
                case "catcombothreeresolved": return WsGameplayEventType.CatComboThreeResolved;
                case "catcombofiveresolved": return WsGameplayEventType.CatComboFiveResolved;
                case "favortargetempty": return WsGameplayEventType.FavorTargetEmpty;
                case "favorwindowopened": return WsGameplayEventType.FavorWindowOpened;
                case "favorresolved": return WsGameplayEventType.FavorResolved;
                case "buryresolved": return WsGameplayEventType.BuryResolved;
                case "illtakethatmarked": return WsGameplayEventType.IllTakeThatMarked;
                case "towermaskupdated": return WsGameplayEventType.TowerMaskUpdated;
                case "markapplied": return WsGameplayEventType.MarkApplied;
                case "catbuttcurseapplied": return WsGameplayEventType.CatButtCurseApplied;
                case "swaptopbottom": return WsGameplayEventType.SwapTopBottom;
                case "garbagecollectionresolved": return WsGameplayEventType.GarbageCollectionResolved;
                case "catomicbombresolved": return WsGameplayEventType.CatomicBombResolved;
                case "barkingkittenresolved": return WsGameplayEventType.BarkingKittenResolved;
                case "streakingkittenactive": return WsGameplayEventType.StreakingKittenActive;
                case "feralcatplayed": return WsGameplayEventType.FeralCatPlayed;
                case "cardeffectunhandled": return WsGameplayEventType.CardEffectUnhandled;
                default: return WsGameplayEventType.Unknown;
            }
        }

        private static string NormalizeEventKey(string eventType)
        {
            return eventType
                .Trim()
                .Replace("_", string.Empty)
                .Replace("-", string.Empty)
                .Replace(" ", string.Empty)
                .ToLowerInvariant();
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
