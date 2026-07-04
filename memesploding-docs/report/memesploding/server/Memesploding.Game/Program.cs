using Memesploding.Game.Extensions;
using Memesploding.Game.Hubs;

namespace Memesploding.Game;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddGameFoundationServices(builder.Configuration);
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyHeader()
                      .AllowAnyMethod()
                      .SetIsOriginAllowed(_ => true) // Allow any origin
                      .AllowCredentials(); // Required for SignalR
            });
        });

        builder.Services.AddAuthorization();
        builder.Services.AddOpenApi();

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseCors();
        app.UseAuthorization();

        app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "Memesploding.Game" }));
        app.MapHub<GameHub>("/ws");

        app.Run();
    }
}
