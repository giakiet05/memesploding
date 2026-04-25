using System.Text.Json.Serialization;
using Memesploding.Game.Application;
using Memesploding.Game.Auth;
using Memesploding.Game.Infrastructure.Configuration;
using Memesploding.Game.Infrastructure.Integration;
using Memesploding.Game.Infrastructure.Runtime;
using Memesploding.Game.Infrastructure.StateStore;
using Memesploding.Game.Workers;
using Memesploding.Shared.Auth;
using Memesploding.Shared.Infrastructure.Cache;
using Memesploding.Shared.Messaging.EventBus;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;

namespace Memesploding.Game.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGameFoundationServices(this IServiceCollection services, IConfiguration configuration)
    {
        var redisConnectionString = configuration.GetConnectionString("RedisConnection");
        if (string.IsNullOrWhiteSpace(redisConnectionString))
        {
            throw new InvalidOperationException("Missing RedisConnection in appsettings.json");
        }

        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnectionString));
        services.AddSingleton<ICacheStore, RedisStore>();
        services.AddSingleton<IEventBus, RedisEventBus>();
        services.AddScoped<ITokenService, TokenService>();

        services.AddSingleton<IGameTicketValidator, GameTicketValidator>();
        services.AddSingleton<IDeckComposer, DeckComposer>();
        services.AddSingleton<IMatchRuntimeManager, MatchRuntimeManager>();
        services.AddSingleton<IGameSnapshotStore, GameSnapshotStore>();
        services.AddSingleton<IGameCommandDispatcher, GameCommandDispatcher>();
        services.Configure<GameplayTimingOptions>(configuration.GetSection(GameplayTimingOptions.SectionName));

        services.AddSignalR().AddJsonProtocol(options =>
        {
            options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        services.AddHostedService<StartMatchConsumer>();
        services.AddHostedService<RuntimeTickWorker>();
        services.AddHostedService<ReconnectTimeoutWorker>();

        services.Configure<HubOptions>(options =>
        {
            options.MaximumReceiveMessageSize = 64 * 1024;
        });

        return services;
    }
}
