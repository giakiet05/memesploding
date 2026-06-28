# Game Server Structure (Phase 1)

## 1) Mục tiêu của tài liệu

Tài liệu này mô tả cấu trúc thư mục đề xuất cho `Memesploding.Game` để mày tự implement theo hướng:
- tách lớp rõ ràng,
- dễ test,
- dễ mở rộng luật bài ở phase sau.
- ưu tiên **reuse từ `Memesploding.Shared` trước**, chỉ tạo mới phần game-runtime đặc thù.

Phạm vi ở đây là **khung project + trách nhiệm từng folder**, chưa đi vào code chi tiết.

---

## 2) Cấu trúc thư mục đề xuất

```text
server/
  Memesploding.Game/
    Program.cs
    appsettings.json
    appsettings.Development.json

    Hubs/
      GameHub.cs

    Auth/
      GameTicketValidator.cs
      GameConnectionContext.cs
      // Reuse Memesploding.Shared.Auth (ITokenService/TokenService)

    Messaging/
      Channels/
        GameChannels.cs
      Events/
        GameEvents.cs
      // Reuse Memesploding.Shared.Messaging.EventBus

    DTOs/
      WsClientMessages.cs
      WsServerEvents.cs
      WsCommonDtos.cs

    Application/
      Commands/
        PlayCardCommand.cs
        DrawCardCommand.cs
        UseDefuseCommand.cs
        NopeCommand.cs
      CommandHandlers/
        PlayCardHandler.cs
        DrawCardHandler.cs
        UseDefuseHandler.cs
        NopeHandler.cs
      Validators/
        CommandValidationResult.cs
      GameCommandDispatcher.cs

    Domain/
      MatchRuntime/
        MatchRuntime.cs
        MatchRuntimeState.cs
        MatchRuntimePlayerState.cs
      StateMachine/
        MatchPhase.cs
        MatchTransitionRules.cs
      Cards/
        CardVisibility.cs
      Effects/
        ICardEffectHandler.cs
        AttackEffectHandler.cs
        SkipEffectHandler.cs
        ShuffleEffectHandler.cs
        SeeTheFutureEffectHandler.cs
        FavorEffectHandler.cs
        NopeEffectHandler.cs
        DefuseEffectHandler.cs
      Engine/
        TurnEngine.cs
        ReactionWindowEngine.cs
        ExplosionResolutionEngine.cs

    Infrastructure/
      Runtime/
        MatchRuntimeManager.cs
        RuntimeScheduler.cs
      StateStore/
        GameSnapshotStore.cs
        GameEventStreamStore.cs
      Integration/
        ApiBridgeClient.cs
        StartMatchConsumer.cs
        MatchEndedPublisher.cs
      Time/
        IGameClock.cs
        SystemGameClock.cs
      Serialization/
        GameStateSerializer.cs

    Contracts/
      ReconnectPayload.cs

    Workers/
      RuntimeTickWorker.cs
      ReconnectTimeoutWorker.cs

    Extensions/
      ServiceCollectionExtensions.cs

    Tests/
      Unit/
      Integration/
      Simulation/
```

---

## 2.1) Phần nào reuse từ `Memesploding.Shared`

Không tạo lại các thành phần dưới đây trong `Memesploding.Game`, mà tham chiếu trực tiếp:

1. `Shared.Messaging.EventBus`
   - `IEventBus`
   - `RedisEventBus`

2. `Shared.Infrastructure.Cache`
   - `ICacheStore`
   - `RedisStore`

3. `Shared.Auth`
   - `ITokenService` (đã có `GenerateGameTicket`)
   - Token/JWT config conventions

4. `Shared.Enums`
   - Reuse `CardCode`, `CardType`, `RoomStatus`, `ErrorCode` khi phù hợp

5. `Shared.Entities`
   - Reuse `Match`, `MatchParticipant`, `Card`, `CardSet` cho persistence/contracts cần thiết

---

## 3) Mô tả vai trò từng folder

### `Hubs/`
- Điểm vào SignalR của game server.
- Chỉ làm nhiệm vụ nhận message, lấy context connection, gọi dispatcher.
- Không chứa luật game.

### `Auth/`
- Verify game ticket (`scope = game_ws`, `roomCode/matchId`, `userId`, expiry).
- Chuẩn hóa context đã xác thực để các layer dưới dùng.
- Reuse logic/khai báo token từ `Memesploding.Shared.Auth`.

### `Messaging/`
- `Channels/` và `Events/` dùng cho:
  - giao tiếp internal trong game server.
- Dùng `IEventBus/RedisEventBus` từ `Memesploding.Shared.Messaging.EventBus`.
- Không đặt message bus adapter riêng trong game nếu đã reuse Shared.
- Các integration events/channels giữa API <-> Game phải đặt ở Shared (không đặt trong `Memesploding.Game/Messaging`).

### `DTOs/`
- DTO giao tiếp WebSocket với client (request/response/event payload).
- Tách riêng khỏi `Messaging` để rõ ranh giới: `DTOs` là client contract, `Messaging` là server-side event bus contract.

### `Application/`
- Chứa command pipeline ở mức use-case.
- Dispatcher route command vào handler tương ứng.
- Validator kiểm tra điều kiện ở mức app (đúng actor, đúng phase, đủ dữ liệu).

### `Domain/`
- Core game logic authoritative.
- Runtime state, state machine, card effects, rule resolver, turn/reaction engines.
- Không phụ thuộc SignalR/Redis/HTTP.
- Không định nghĩa lại enum card nếu đã có trong `Shared.Enums`.

### `Infrastructure/`
- Triển khai kỹ thuật: runtime manager, state store, scheduler, integration adapter.
- Chứa adapter tới hệ ngoài, giữ Domain/Application sạch dependency.
- Reuse `ICacheStore/RedisStore` từ Shared.

### `Contracts/`
- Chỉ giữ contract **internal của game server**.
- Contract ở biên API <-> Game (cross-service) nên đặt trong Shared để hai bên dùng chung schema.
- Ví dụ: `StartMatchRequested`, `MatchStarted`, `MatchEnded`, `RoomUpdated` là integration contract và phải nằm Shared.

### `Workers/`
- Background jobs cho tick loop, reconnect timeout, cleanup runtime.
- Tách khỏi Hub để lifecycle ổn định, dễ scale.

### `Extensions/`
- Gom đăng ký DI/service wiring cho `Program.cs` gọn và dễ đọc.

### `Tests/`
- `Unit`: test effect/state transition nhỏ.
- `Integration`: test hub -> application -> runtime -> broadcaster.
- `Simulation`: test deterministic full match theo seed.

---

## 4) Nguyên tắc phân lớp khi tự code

1. Không cho Hub gọi trực tiếp Domain.
2. Không để Domain tham chiếu SignalR/Redis/HTTP.
3. Mỗi command chỉ mutate state qua runtime queue tuần tự.
4. Mọi thay đổi state quan trọng phải có event + snapshot mốc.
5. Payload gửi client phải tách public/private để tránh lộ bài.

---

## 5) Thứ tự dựng khung khuyến nghị

1. Dựng project + `Program.cs` + DI skeleton.
2. Wire dependency từ Shared (`IEventBus`, `ICacheStore`, `ITokenService`).
3. Dựng `Hubs/`, `Auth/`, `DTOs/` để mở được kết nối và handshake.
4. Dựng `Messaging/Events` + `Messaging/Channels` cho game internal flow.
5. Dựng `Domain/MatchRuntime` rỗng + `Infrastructure/RuntimeManager`.
6. Dựng `Application/GameCommandDispatcher` với 1-2 command đầu tiên (`DrawCard`, `PlayCard`).
7. Thêm snapshot Redis + reconnect worker.
8. Thêm integration với API cho `StartMatch` và `MatchEnded`.

Ghi chú boundary quan trọng:
- Client không có command `StartMatch` vào game server.
- API là bên phát integration event `StartMatchRequested` để game server bắt đầu runtime/match lifecycle.

Khi xong 8 bước này là có khung đủ chắc để nhét luật bài dần mà không phải đập lại kiến trúc.
