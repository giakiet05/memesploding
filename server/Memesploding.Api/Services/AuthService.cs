namespace Memesploding.Api.Services;

using System;
using System.Threading.Tasks;
using Memesploding.Api.Data;
using Memesploding.Api.DTOs;
using Memesploding.Shared.Entities;
using Memesploding.Shared.Enums;

public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _dbContext;

    // Auto DI Injection: Lấy DbContext (Postgres) ra xài
    public AuthService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AuthResponseDto> RegisterGuestAsync(RegisterGuestRequestDto request)
    {
        // 1. Khởi tạo cục Entity User
        var user = new User
        {
            // Identity ID được sinh tự động do EF Core mapping
            Provider = AuthProvider.Guest,
            CreatedAt = DateTime.UtcNow
        };

        // 2. Kẹp thêm cục Entity Profile (nhờ sức mạnh Navigation Property)
        var profile = new Profile
        {
            Nickname = request.Nickname,
            AvatarUrl = "default-avatar.png", 
        };
        
        // Nối Profile vào User. Khi lưu, EF Core sẽ tự hiểu và INSERT vô 2 bảng cùng lúc!
        user.Profile = profile;

        // 3. Mở quyển sổ tay ra chép lệnh nợ vào
        _dbContext.Users.Add(user);
        
        // 4. BẤM NÚT LƯU XUỐNG Ổ CỨNG (I/O) - ĐÂY LÀ CHỖ ĐỤNG CHỮ AWAIT
        await _dbContext.SaveChangesAsync();

        // 5. Sinh ra Token chứa quyền (Phần này sẽ gắn thư viện JWT thật vào sau)
        var dummyAccessToken = "jwt_access_token_cua_" + user.Id;
        var dummyRefreshToken = "jwt_refresh_token_cua_" + user.Id;

        // 6. Nhả "DTO Tờ giấy gói" ra cho Controller quăng về máy Client
        return new AuthResponseDto(
            user.Id,
            profile.Nickname,
            dummyAccessToken,
            dummyRefreshToken
        );
    }
}
