namespace Memesploding.Shared.Enums;

public enum AuthProvider 
{
    Guest,
    Google,
    Facebook
}

public enum FriendshipStatus 
{
    Pending,
    Accepted,
    Blocked
}

public enum RelationshipType
{
    None,            // Người lạ
    PendingSent,     // Lời mời do mình gửi
    PendingReceived, // Lời mời do đối phương gửi
    Accepted,        // Đã là bạn
    Blocked          // Đã block hoặc bị block
}

public enum CardType 
{
    Action,
    Bomb,
    Defuse,
    Cat
}

public enum NotificationType 
{
    SystemAlert,
    LevelUp,
    MatchEnded
}

public enum RoomStatus
{
    Waiting,
    Playing,
    Finished
}

public enum CardCode
{
    // Original (Bộ cơ bản)
    ExplodingKitten,
    Defuse,
    Skip,
    Attack,
    Favor,
    Shuffle,
    SeeTheFuture,
    Nope,
    Cat1,
    Cat2,
    Cat3,
    Cat4,
    Cat5,

    // Imploding (Nổ Sấp)
    ImplodingKitten,
    AlterTheFuture,
    TargetedAttack,
    DrawFromBottom,
    Reverse,
    FeralCat,

    // Barking (Mèo Sủa)
    BarkingKitten,
    AlterTheFutureNow,
    Bury,
    PersonalAttack,
    IllTakeThat,
    TowerMask,

    // Streaking (Mèo Mặc Quần Xì)
    StreakingKitten,
    SuperSkip,
    AlterTheFuture5,
    SwapTopAndBottom,
    GarbageCollection,
    CatomicBomb,
    Mark,
    CurseOfTheCatButt
}
