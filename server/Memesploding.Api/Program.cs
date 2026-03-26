using Microsoft.EntityFrameworkCore;
using Memesploding.Api.Data;
using Memesploding.Shared.Infrastructure.Redis;
using Memesploding.Api.Services;
using Scalar.AspNetCore;
using StackExchange.Redis;

namespace Memesploding.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Setup Entity Framework Core with PostgreSQL
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

        // Setup Redis
        var redisConnectionString = builder.Configuration.GetConnectionString("RedisConnection");
        if (string.IsNullOrEmpty(redisConnectionString))
        {
            throw new InvalidOperationException("RedisConnection string is missing in appsettings.json!");
        }
        
        builder.Services.AddSingleton<IConnectionMultiplexer>(
            ConnectionMultiplexer.Connect(redisConnectionString));
        builder.Services.AddSingleton<IRedisStore, RedisStore>();

        // Thêm DI cho Service rẽ nhánh Bếp Trưởng (Auth)
        // Dùng AddScoped vì AuthService có nhúng tay vào DbContext (vốn nằm ở Scoped)
        builder.Services.AddScoped<IAuthService, AuthService>();

        // Thêm mảng Controller tĩnh (mở nhà hàng tiếp khách)
        builder.Services.AddControllers();

        // Ép toàn bộ đường dẫn API tự động chuyển thành chữ thường (lowercase)
        builder.Services.AddRouting(options => options.LowercaseUrls = true);

        // Add services to the container.
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

        app.UseHttpsRedirection();

        app.UseAuthorization();
        
        // Mapping đường dẫn của tất cả các Class nhãn [ApiController]
        app.MapControllers();

        app.Run();
    }
}