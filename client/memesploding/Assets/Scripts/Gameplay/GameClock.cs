using System;
using Network.Websocket;

namespace Gameplay
{
    public class GameClock
    {
        private TimeSpan _serverOffset = TimeSpan.Zero;
        public DateTime? StartedAtUtc { get; private set; }
        public DateTime? TurnEndsAtUtc { get; private set; }
        public int TurnTimerSeconds { get; private set; }

        public TimeSpan CurrentTurnTimeLeft
        {
            get
            {
                if (!TurnEndsAtUtc.HasValue)
                    return TimeSpan.Zero;

                var remaining = TurnEndsAtUtc.Value - ServerNowUtc;
                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
        }

        public TimeSpan OverallPlaytime
        {
            get
            {
                if (!StartedAtUtc.HasValue)
                    return TimeSpan.Zero;

                var elapsed = ServerNowUtc - StartedAtUtc.Value;
                return elapsed > TimeSpan.Zero ? elapsed : TimeSpan.Zero;
            }
        }

        public DateTime ServerNowUtc => DateTime.UtcNow + _serverOffset;

        public void ApplySnapshot(WsStateSnapshotDto snapshot)
        {
            if (snapshot == null)
                return;

            SyncServerTime(snapshot.serverTimeUtc);
            StartedAtUtc = NormalizeUtc(snapshot.startedAt);
            UpdateTurn(snapshot.turnEndsAt, snapshot.turnTimerSeconds, snapshot.serverTimeUtc);
        }

        public void UpdateTurn(DateTime? turnEndsAtUtc, int turnTimerSeconds, DateTime serverTimeUtc)
        {
            SyncServerTime(serverTimeUtc);
            TurnEndsAtUtc = NormalizeUtc(turnEndsAtUtc);
            TurnTimerSeconds = Math.Max(0, turnTimerSeconds);
        }

        private void SyncServerTime(DateTime serverTimeUtc)
        {
            if (serverTimeUtc == default)
                return;

            _serverOffset = NormalizeUtc(serverTimeUtc)!.Value - DateTime.UtcNow;
        }

        private DateTime? NormalizeUtc(DateTime? value)
        {
            if (!value.HasValue)
                return null;

            return NormalizeUtc(value.Value);
        }

        private DateTime? NormalizeUtc(DateTime value)
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
