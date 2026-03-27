using System;
using System.Collections.Generic;

namespace Memesploding.Shared.Entities;

public class Match
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Settings { get; set; } = string.Empty; // Stored as JSON string
    public string Stats { get; set; } = string.Empty; // Stored as JSON string
    public Guid? WinnerId { get; set; }
    
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }

    // Navigation properties
    public User? Winner { get; set; }
    public ICollection<MatchParticipant> Participants { get; set; } = new List<MatchParticipant>();
}
