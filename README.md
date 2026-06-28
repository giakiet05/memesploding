# Memesploding

Memesploding is a real-time multiplayer card game inspired by "Exploding Kittens". The project follows a monorepo architecture, integrating a .NET 8 backend with a Unity-based client.

## Technology Stack

### Backend

- Framework: .NET 8 (ASP.NET Core)
- Real-time Communication: SignalR (WebSockets)
- Database: PostgreSQL with Entity Framework Core
- Caching and Messaging: Redis (distributed cache and event bus for inter-service communication)
- Authentication: JWT Bearer Token (supporting Guest and Social authentication providers)
- API Documentation: Scalar (OpenAPI/Swagger)

### Client

- Game Engine: Unity 6 (Version 6000.1.8f1)
- Networking: SignalR C# Client
- Rendering: Universal Render Pipeline (URP)
- UI System: Unity UI Toolkit

### Infrastructure and Tooling

- Containerization: Docker and Docker Compose
- Web Testing: React 19 with Vite (for WebSocket hub verification)
- CI/CD & Testing: xUnit for unit and integration testing

## Project Structure

```text
.
├── client/                 # Unity game project
├── server/                 # .NET solution
│   ├── Memesploding.Api/   # REST API, matchmaking, and application hubs
│   ├── Memesploding.Game/  # Dedicated game server hubs for gameplay logic
│   └── Memesploding.Shared/# Shared entities, enums, and messaging logic
├── test-web/               # React 19 application for SignalR testing
├── memesploding-docs/      # Technical documentation (ADRs, API specifications)
└── docker-compose.yml      # Docker orchestration for the backend stack
```

## Key Features

- User Management: Guest mode and social login integration.
- Social System: Real-time presence tracking, friend lists, and game invitations.
- Matchmaking: Quick match and custom room creation (public or private).
- Real-time Gameplay:
  - Synchronized turn management with configurable timers.
  - Reaction system (Nope mechanism) with resolution windows.
  - Complex card interactions including deck manipulation and targeting.
- Resilience: Automatic reconnection logic and AFK/auto-play handling for disconnected players.

## Documentation

Detailed documentation regarding system design, requirements, and game rules is available in the `memesploding-docs` directory.
