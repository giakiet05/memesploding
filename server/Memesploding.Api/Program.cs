using Microsoft.EntityFrameworkCore;
using Memesploding.Api.Data;
using Memesploding.Api.Infrastructure.Cache;
using Memesploding.Shared.Auth;
using Memesploding.Api.Messaging.Channels;
using Memesploding.Api.Services;
using Memesploding.Api.Workers;
using Memesploding.Api.Middlewares;
using Scalar.AspNetCore;
using StackExchange.Redis;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Memesploding.Shared.Infrastructure.Cache;
using Memesploding.Shared.Messaging.EventBus;
using System.Text;
using System.Text.Json.Serialization;

namespace Memesploding.Api;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Setup Entity Framework Core with PostgreSQL
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

        // Setup Redis
        var redisConnectionString = builder.Configuration.GetConnectionString("RedisConnection");
        if (string.IsNullOrEmpty(redisConnectionString))
            throw new InvalidOperationException("Missing RedisConnection in appsettings.json");

        // Trì hoãn mở kết nối Redis ngay lúc khởi tạo DI Server bằng cách dùng (sp => ...)
        builder.Services.AddSingleton<IConnectionMultiplexer>(sp => 
            ConnectionMultiplexer.Connect(redisConnectionString));
        builder.Services.AddSingleton<ICacheStore, RedisStore>();
        
        // Đăng ký Event Bus
        builder.Services.AddSingleton<IEventBus, RedisEventBus>();

        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyHeader()
                      .AllowAnyMethod()
                      .SetIsOriginAllowed(_ => true)
                      .AllowCredentials();
            });
        });

        // Thêm DI cho Service rẽ nhánh
        builder.Services.AddScoped<ITokenService, TokenService>();
        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddScoped<IUserService, UserService>();
        builder.Services.AddScoped<IFriendshipService, FriendshipService>();
        builder.Services.AddScoped<ICardSetService, CardSetService>();
        builder.Services.AddScoped<IRoomService, RoomService>();
        builder.Services.AddScoped<INotificationService, NotificationService>();
        builder.Services.AddScoped<IMatchService, MatchService>();
        builder.Services.AddScoped<IMatchmakingService, MatchmakingService>();
        builder.Services.AddScoped<IBotTestMatchService, BotTestMatchService>();
        builder.Services.AddScoped<IPresenceService, PresenceService>();
        builder.Services.AddScoped<IInvitationService, InvitationService>();

        // SignalR for WebSocket
        builder.Services.AddSignalR()
            .AddJsonProtocol(options => {
                options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

        // Background services
        builder.Services.AddHostedService<FriendshipEventWorker>();
        builder.Services.AddHostedService<RoomEventWorker>();
        builder.Services.AddHostedService<PresenceEventWorker>();
        builder.Services.AddHostedService<RoomReconnectWorker>();

        // Thêm mảng Controller - camelCase mặc định + Enum ra chữ thay vì số
        builder.Services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

        // Ép toàn bộ đường dẫn API tự động chuyển thành chữ thường (lowercase)
        builder.Services.AddRouting(options => options.LowercaseUrls = true);

        // Add services to the container.
        
        // Thiết lập bộ lọc cửa: Bắt buộc dùng thẻ xông nhà là JWT Bearer
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true, // Phải kiểm tra chữ ký do mình đóng mộc
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
                    ValidateIssuer = true,           // Kiểm tra xem có đúng là Server mình làm ra không
                    ValidIssuer = builder.Configuration["Jwt:Issuer"],
                    ValidateAudience = true,         // Phải chắc chắn cấp cho thằng App nào xài
                    ValidAudience = builder.Configuration["Jwt:Audience"],
                    ValidateLifetime = true,         // Hết hạn Token thì đá đít văng ra
                    ClockSkew = TimeSpan.Zero        // Không cho dây dưa quá hạn 5 phút ảo (đá sấp mặt liền)
                };

                // SignalR: JWT từ query string (WebSocket không support Authorization header)
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        
                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/ws"))
                        {
                            context.Token = accessToken;
                        }
                        
                        return Task.CompletedTask;
                    }
                };
            });

        builder.Services.AddAuthorization();

        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference(); // Bật giao diện Web xịn xò của Scalar lên!
        }

        // === Migration & Database Seeding ===
        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            // Apply pending migrations
            dbContext.Database.Migrate();
            
            // Seed card sets from YAML
            var yamlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Seeds", "cards.yaml");
            await CardSetSeeder.SeedAsync(dbContext, yamlPath);
        }

        app.UseHttpsRedirection();

        app.UseCors();

        // Gắn Lưới Bắt Lỗi Xịn Xò Ngay Cửa Khẩu (Middleware Chặn Mọi Exception)
        app.UseMiddleware<ExceptionHandlingMiddleware>();

        // 2 ông thần An ninh - BẮT BUỘC thằng Authentication (Soi thẻ) phải đứng trước Authorization (Cấp quyền)
        app.UseAuthentication();
        
        // Kiểm tra xem token có nằm trong danh sách đen (blacklist) không
        app.UseMiddleware<TokenBlacklistMiddleware>();
        
        app.UseAuthorization();
        
        // Mapping đường dẫn của tất cả các Class nhãn [ApiController]
        app.MapControllers();

        // Health check endpoint
        app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "Memesploding.Api" }));

        // SignalR WebSocket endpoint
        app.MapHub<Memesploding.Api.Hubs.AppHub>("/ws")
            .RequireAuthorization();

        app.Run();
    }
}
