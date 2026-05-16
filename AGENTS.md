# Memesploding — Project Snapshot

Memesploding là monorepo game bài kiểu Mèo Nổ, gồm backend .NET, client Unity, docs thiết kế/API, và một web test harness cho SignalR.

## Kiến trúc chính

- `server/Memesploding.Api`: ASP.NET Core API server cho auth, user profile, friends, notifications, matchmaking, lobby/room lifecycle, card sets, match history. Dùng PostgreSQL qua EF Core, Redis cho room/presence/cache/pub-sub, JWT Bearer auth, SignalR hub `/ws` cho realtime lobby/presence/room events.
- `server/Memesploding.Game`: ASP.NET Core game server authoritative cho match runtime. Client chỉ gửi command/intent qua SignalR `/ws`; server validate turn/rule/state, xử lý deck, card effects, reaction window, defuse/explosion, reconnect snapshot, tick worker và publish kết quả trận về API.
- `server/Memesploding.Shared`: code dùng chung giữa API và Game, gồm entities/enums, token service, Redis cache store, Redis event bus, integration event/channel contracts.
- `client/memesploding`: Unity client. Scripts chính nằm trong `Assets/Scripts`, gồm API service layer, event bus, card/gameplay managers, UI, ScriptableObjects cho card data/database, scenes `MainMenu` và `Gameplay`.
- `test-web`: React/Vite app nhỏ để test API/SignalR manually, có dependency `@microsoft/signalr`.
- `memesploding-docs`: requirements, ADRs, API specs, DB/Redis design, game server design, gameplay flow. Đọc docs này trước khi đổi behavior lớn.

## Data Và Runtime

- Database chính: PostgreSQL với EF Core migrations trong `server/Memesploding.Api/Migrations`.
- Redis dùng cho room state, user-in-room, public rooms, presence, blacklist/cache và pub-sub event bus.
- API sở hữu pre-game room state (`waiting`/`starting`) và persistence lịch sử trận.
- Game server sở hữu in-match state (`playing`) theo mô hình single-writer/runtime actor; snapshot state lưu Redis để reconnect.
- Card sets seed từ `server/Memesploding.Api/Data/Seeds/cards.yaml`.

## Entry Points Quan Trọng

- API bootstrap: `server/Memesploding.Api/Program.cs`.
- API endpoints: controllers trong `server/Memesploding.Api/Controllers`.
- API room orchestration: `server/Memesploding.Api/Services/RoomService.cs`.
- Game bootstrap: `server/Memesploding.Game/Program.cs`.
- Game DI/workers: `server/Memesploding.Game/Extensions/ServiceCollectionExtensions.cs`.
- Game WebSocket hub: `server/Memesploding.Game/Hubs/GameHub.cs`.
- Game command flow: `GameHub` -> `GameCommandDispatcher` -> `MatchRuntimeManager` -> `MatchRuntime`.
- Core game rules/effects: `server/Memesploding.Game/Domain`.

## Working Notes Cho AI

- Không đọc `.env` hoặc các file tương tự; cấu hình mẫu đang nằm trong `appsettings.json`/`appsettings.Development.json`.
- Không tự chạy app sau khi implement; chỉ đưa command cho người dùng chạy.
