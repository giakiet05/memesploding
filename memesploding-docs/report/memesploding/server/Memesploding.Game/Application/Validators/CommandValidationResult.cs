namespace Memesploding.Game.Application.Validators;

public record CommandValidationResult(
    bool IsValid,
    string? ErrorMessage = null
);
