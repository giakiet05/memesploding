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

<!-- gitnexus:start -->
# GitNexus — Code Intelligence

This project is indexed by GitNexus as **memesploding** (5340 symbols, 11568 relationships, 300 execution flows). Use the GitNexus MCP tools to understand code, assess impact, and navigate safely.

> If any GitNexus tool warns the index is stale, run `npx gitnexus analyze` in terminal first.

## Always Do

- **MUST run impact analysis before editing any symbol.** Before modifying a function, class, or method, run `gitnexus_impact({target: "symbolName", direction: "upstream"})` and report the blast radius (direct callers, affected processes, risk level) to the user.
- **MUST run `gitnexus_detect_changes()` before committing** to verify your changes only affect expected symbols and execution flows.
- **MUST warn the user** if impact analysis returns HIGH or CRITICAL risk before proceeding with edits.
- When exploring unfamiliar code, use `gitnexus_query({query: "concept"})` to find execution flows instead of grepping. It returns process-grouped results ranked by relevance.
- When you need full context on a specific symbol — callers, callees, which execution flows it participates in — use `gitnexus_context({name: "symbolName"})`.

## Never Do

- NEVER edit a function, class, or method without first running `gitnexus_impact` on it.
- NEVER ignore HIGH or CRITICAL risk warnings from impact analysis.
- NEVER rename symbols with find-and-replace — use `gitnexus_rename` which understands the call graph.
- NEVER commit changes without running `gitnexus_detect_changes()` to check affected scope.

## Resources

| Resource | Use for |
|----------|---------|
| `gitnexus://repo/memesploding/context` | Codebase overview, check index freshness |
| `gitnexus://repo/memesploding/clusters` | All functional areas |
| `gitnexus://repo/memesploding/processes` | All execution flows |
| `gitnexus://repo/memesploding/process/{name}` | Step-by-step execution trace |

## CLI

| Task | Read this skill file |
|------|---------------------|
| Understand architecture / "How does X work?" | `.claude/skills/gitnexus/gitnexus-exploring/SKILL.md` |
| Blast radius / "What breaks if I change X?" | `.claude/skills/gitnexus/gitnexus-impact-analysis/SKILL.md` |
| Trace bugs / "Why is X failing?" | `.claude/skills/gitnexus/gitnexus-debugging/SKILL.md` |
| Rename / extract / split / refactor | `.claude/skills/gitnexus/gitnexus-refactoring/SKILL.md` |
| Tools, resources, schema reference | `.claude/skills/gitnexus/gitnexus-guide/SKILL.md` |
| Index, status, clean, wiki CLI commands | `.claude/skills/gitnexus/gitnexus-cli/SKILL.md` |

<!-- gitnexus:end -->
