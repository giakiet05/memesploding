namespace Memesploding.Game.Auth;

public interface IGameTicketValidator
{
    bool TryValidate(string token, out GameConnectionContext? context);
}
