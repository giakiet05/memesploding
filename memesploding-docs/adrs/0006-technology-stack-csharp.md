# ADR 0006: Backend Language - C# + ASP.NET Core

## Trạng thái (Status)
Đã chấp thuận (Accepted)

## Bối cảnh (Context)
Cần quyết định ngôn ngữ lập trình cho API Server và Game Server. Yêu cầu: real-time WebSocket, game logic phức tạp, type-safe, và có giá trị cho CV (internship).

## Quyết định (Decision)
**Chọn C# + ASP.NET Core 8+ cho cả API Server và Game Server**

Stack:
- **Framework:** ASP.NET Core 8+
- **WebSocket:** SignalR (built-in, production-ready)
- **Database:** PostgreSQL + EF Core
- **Caching:** Redis + StackExchange.Redis
- **Testing:** xUnit + Moq

## Lý do (Rationale)

1. **SignalR:** Gold standard cho real-time games
   - WebSocket + fallback (long polling, SSE)
   - Automatic reconnection + grouping/broadcasting
   - Tích hợp authentication
   - ADR 0004, 0005 đều dựa trên WebSocket pub/sub → SignalR perfect fit

2. **Type Safety:** Game logic yêu cầu type safety cao
   - Catch logic bugs ở compile time
   - Clear contracts (API Server ↔ Game Server)
   - vs Go: interface{}, reflection → refactor khó

3. **Async/Await:** Clean concurrency model cho game state
   - Hơn Node.js callback hell
   - Hơn Python GIL (single-threaded)

4. **CV Value:** C# enterprise language
   - ASP.NET Core trending (open-source, modern)
   - Real-time game interesting project
   - Khác với Go projects (CV diversity)

## Lựa chọn khác (Alternatives)

| Ngôn ngữ | Ưu điểm | Nhược điểm | Verdict |
|----------|--------|----------|--------|
| **Go** | Lightweight, goroutines | WebSocket library đơn sơ, yếu type safety, đã có projects | ❌ Loại |
| **Node.js** | Socket.io ecosystem | Single-threaded (game CPU-bound bottleneck) | ❌ Loại |
| **Python** | Syntax simple | GIL, slow game logic | ❌ Loại |
| **Java** | Type-safe, performance | Boilerplate, JVM startup slow | ❌ Loại |

## Hệ quả (Consequences)

**Ưu điểm:**
- ✅ WebSocket features built-in & robust
- ✅ Type safety → fewer bugs
- ✅ Async/await clean code
- ✅ Performance tốt
- ✅ CV-worthy for internship

**Nhược điểm:**
- ❌ Docker image lớn (~500MB vs 10MB Go)
- ❌ Startup chậm hơn Go (JIT warmup)
- ❌ Steeper learning curve nếu chưa quen C#

## Ghi chú
Communication API Server ↔ Game Server: gRPC hoặc REST (TBD)
