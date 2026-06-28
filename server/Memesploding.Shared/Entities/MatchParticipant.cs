using System;

namespace Memesploding.Shared.Entities;

public class MatchParticipant
{
    public Guid MatchId { get; set; }
    public Guid UserId { get; set; }
    public int FinalRank { get; set; }
    public int XpEarned { get; set; } = 0;
    public int ScoreChange { get; set; } = 0;

    // Navigation properties
    public Match? Match { get; set; }
    public User? User { get; set; }
}
