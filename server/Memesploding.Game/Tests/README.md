# Memesploding.Game - Test Suite

Complete test coverage for the game service: setup, gameplay, integration, and simulation.

## 📁 Test Directories

### 1. **Gameplay/** - End-to-End Game Flow Tests ⭐
Full testing from login → create room → join → ready → start → play game.

**Files:**
- `GameplaySetup.http` - REST API setup flow (login, room, join, ready, start, tickets)
- `GameplayCommands.md` - WebSocket game commands and payloads
- `run-gameplay-tests.sh` - Automated bash script (curl + websocat)
- `http-client.env.json` - Environment config
- `README.md` - Complete documentation
- `QUICKSTART.md` - Quick reference guide ← **Start here!**

**Quick Start:**
```bash
# Manual (Rider)
Open GameplaySetup.http in Rider → Select env → Run requests

# Automated (Bash)
chmod +x run-gameplay-tests.sh
./run-gameplay-tests.sh
```

---

### 2. **Integration/** - Integration Tests
(Placeholder for cross-service integration tests)

---

### 3. **Simulation/** - Load & Stress Tests
(Placeholder for simulation/load testing scenarios)

---

### 4. **Unit/** - Unit Tests
(Placeholder for isolated unit tests)

---

## 🎯 Quick Links

| Test Type | Where | How |
|-----------|-------|-----|
| **E2E Game** | `Gameplay/` | Rider HTTP or bash script |
| **WS Commands** | `Gameplay/GameplayCommands.md` | wscat or WebSocketKing |
| **API Setup** | `../Memesploding.Api/HttpTests/` | Reference for API tests |

---

## 📊 Test Coverage Roadmap

```
Phase 1 - Gameplay Setup ✅
├── Authentication (3 users)
├── Room creation & joining
├── Ready status & match start
└── Game tickets generation

Phase 2 - Game Actions (In Progress)
├── Draw card
├── Play card
├── Nope (react)
├── Defuse & bomb position
└── State snapshot

Phase 3 - Multiplayer Scenarios
├── Turn order rotation
├── Explosion handling
├── Winner detection
└── Reconnection

Phase 4 - Integration Tests
├── API + WebSocket flow
├── Multi-room scenarios
└── Concurrent players

Phase 5 - Load Testing
├── Stress testing
├── Connection limits
└── Message throughput
```

---

## 🚀 Running Tests

### Prerequisites
```bash
# Check server status
curl http://localhost:5217/health

# Ensure dependencies
sudo apt install curl jq
sudo snap install websocat
npm install -g wscat
```

### Option 1: Manual Testing (Rider)
```
1. Open GameplaySetup.http
2. Select environment: dev
3. Run requests Phase by Phase
4. Copy game URLs from Phase 4
5. Test WebSocket commands in WebSocketKing
```

### Option 2: Automated Testing (Bash)
```bash
cd Gameplay
chmod +x run-gameplay-tests.sh
./run-gameplay-tests.sh

# View results
cat output/gameplay-test-report.txt
```

### Option 3: WebSocket Manual (wscat)
```bash
wscat -c "ws://localhost:5217/ws?access_token=TOKEN"
# Send commands from GameplayCommands.md
```

---

## 📊 Test Metrics

Current Coverage:
- ✅ Setup Phase: 100% (login, room, join, ready, start)
- ✅ Game Tickets: 100% (all 3 players)
- ✅ WebSocket Handshake: Ready for testing
- 🚧 Game Commands: Documented, ready for automation
- ⏳ Multiplayer Scenarios: Pending
- ⏳ Load Testing: Pending

---

## 📝 Contributing

To add new test scenarios:

1. Add commands to `Gameplay/GameplayCommands.md`
2. Update `Gameplay/run-gameplay-tests.sh` with new test phases
3. Document in appropriate README
4. Run tests to verify

---

## 🔗 Related Resources

- **API Tests:** `../Memesploding.Api/HttpTests/`
- **API Docs:** `../../memesploding-docs/design/api/`
- **Game Entities:** `../Program.cs`
- **Database Schema:** `../../Memesploding.Shared/Entities/`

---

**Status:** Phase 1 Complete ✅ | Phase 2 Ready for Testing 🚀
