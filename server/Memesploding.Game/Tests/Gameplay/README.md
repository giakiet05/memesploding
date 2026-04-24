# Memesploding Game Flow Tests

Complete end-to-end test for the gameplay flow: **login → create room → join → ready → start → play game**.

## 📁 Files

| File | Description |
|------|-------------|
| **GameplaySetup.http** | REST setup flow (login, create room, join, set ready, start match, get tickets) |
| **GameplayCommands.md** | WebSocket game commands and message payloads |
| **http-client.env.json** | Environment configuration (matching API server env) |
| **run-gameplay-tests.sh** | Automated bash script (curl + websocat) |

---

## 🚀 Quick Start

### Option 1: Manual Testing with Rider (Recommended for Development)

1. **Setup Phase:**
   - Open `GameplaySetup.http` in Rider
   - Select environment `dev` from dropdown
   - Run requests in order:
     - Phase 1: Alice/Bob/Charlie Login
     - Phase 2: Load CardSets, Create Room, Join
     - Phase 3: Set Ready (all 3 users), Start Match
     - Phase 4: Get Game Tickets (for WebSocket URLs)

2. **Game Phase:**
   - Copy `{{aliceWsUrl}}`, `{{bobWsUrl}}`, `{{charlieWsUrl}}` from Phase 4
   - Use WebSocketKing or wscat to connect
   - Copy-paste commands from `GameplayCommands.md`
   - Test game actions (draw card, play card, nope, defuse, etc.)

**Extract variables example:**
```
aliceToken → used for subsequent Alice requests
roomCode → unique room identifier
aliceWsUrl → full WebSocket URL with token
```

### Option 2: Automated Testing with Bash Script

```bash
# Prerequisites
sudo snap install websocat  # or: cargo install websocat
sudo apt install jq curl

# Make script executable
chmod +x run-gameplay-tests.sh

# Run full test
./run-gameplay-tests.sh

# View results
cat output/gameplay-test-report.txt
```

### Option 3: Manual WebSocket Testing

```bash
# 1. Run GameplaySetup.http to get tickets (in Rider)
# 2. Get aliceWsUrl, bobWsUrl, charlieWsUrl

# 3. Connect and test manually
wscat -c "ws://localhost:5217/ws?access_token=YOUR_TOKEN"

# 4. Send handshake
> {"protocol":"json","version":1}

# 5. Send commands from GameplayCommands.md
```

---

## 📋 Test Flow

### Phase 1: Authentication
- Alice, Bob, Charlie register (device ID)
- Get JWT access tokens
- Extract user IDs and usernames

### Phase 2: Room Setup
- Load available card sets
- Alice creates public room (4 players, cardset)
- Bob joins room
- Charlie joins room
- **Variables**: roomCode, roomId, cardSetId

### Phase 3: Match Preparation
- All 3 users set ready status
- Alice (host) starts the match
- **Status**: All users transition to "playing"

### Phase 4: Get Game Tickets
- Each user gets gameTicket from room details
- Construct WebSocket URLs with tickets
- **URLs**: `ws://localhost:5217/ws?access_token={ticket}`

### Phase 5: Game Actions (WebSocket)
- All 3 clients connect to GameHub
- Handshake: send JSON protocol message
- Simulate turns:
  - Alice draws card
  - Bob plays card (or draws)
  - Charlie plays card or reacts
  - Repeat until someone explodes

---

## 🔄 Environment Configuration

### Using http-client.env.json

File already matches API server format. Supports:

**Dev (Local):**
```json
{
  "baseUrl": "http://localhost:5217/api/v1",
  "gameWsUrl": "ws://localhost:5217/ws"
}
```

**Production:**
```json
{
  "baseUrl": "https://api.memesploding.com/api/v1",
  "gameWsUrl": "wss://api.memesploding.com/ws"
}
```

### Switching Environments
- In Rider: Select from dropdown before running
- In bash script: Edit `HTTP_BASE_URL` and `WS_BASE_URL` variables

---

## 📊 Output

### Rider HTTP Client
- ✅ Green checkmarks for successful requests
- ❌ Red X for failures
- Auto-extracted variables highlighted
- Formatted JSON responses

### Bash Script Output
```
=========================================
  Memesploding Gameplay Test Suite
=========================================

ℹ️  Server: http://localhost:5217/api/v1
ℹ️  Output: ./output

ℹ️  PHASE 1: Authentication
✅ Alice Login
✅ Bob Login
✅ Charlie Login

ℹ️  PHASE 2: Room Setup
✅ Load Card Sets
✅ Create Room (Code: ABC123)
✅ Bob Join
✅ Charlie Join

ℹ️  PHASE 3: Match Preparation
✅ Alice Ready
✅ Bob Ready
✅ Charlie Ready
✅ Start Match

ℹ️  PHASE 4: Game Tickets
✅ Get Alice Ticket
✅ Get Bob Ticket
✅ Get Charlie Ticket

ℹ️  PHASE 5: WebSocket Game
✅ Alice Connect
✅ Bob Connect
✅ Charlie Connect
✅ Handshake OK
✅ Alice Draw Card
✅ Bob Play Card
✅ Charlie Nope

=========================================
           TEST SUMMARY
=========================================

Total Phases:  5
Passed:        5
Failed:        0

Result: ✅ ALL TESTS PASSED
```

---

## 🛠️ Prerequisites

### Server Requirements
- Memesploding.Api running on `http://localhost:5217`
- PostgreSQL database initialized
- Redis running (default port 6379)
- Memesploding.Game running on `http://localhost:5204` (game engine)

### Client Tools

**For Rider (Recommended):**
- IntelliJ IDEA or Rider with HTTP Client built-in

**For Automation:**
- `curl` - HTTP requests
- `jq` - JSON parsing
- `websocat` - WebSocket client
- `bash` - Script execution

---

## 🐛 Troubleshooting

### "Connection refused" on setup requests
```bash
# Ensure API server is running
cd ../../../Memesploding.Api
dotnet run
```

### "Database connection error"
```bash
# Check PostgreSQL
sudo service postgresql status
# Check connection string in appsettings.Development.json
```

### "Redis connection error"
```bash
# Start Redis
sudo service redis-server start
# Verify: redis-cli ping → PONG
```

### WebSocket connection fails
- Check JWT token is not expired
- Verify game server is running on correct port
- Check firewall allows WebSocket connections
- Try different WS_BASE_URL (ws:// vs wss://)

### "Card set not found"
```bash
# Seed card sets (if needed)
psql -d memesploding -f ../../../Memesploding.Api/Data/Seeds/cards.yaml
```

### User/room already exists
```bash
# Use different device IDs (add timestamp to avoid conflicts)
# Or delete test data:
psql -d memesploding << EOF
DELETE FROM users WHERE username LIKE '%gameplay%';
DELETE FROM rooms WHERE code LIKE '%GAMEPLAY%';
EOF
```

---

## 📝 Notes

### Test Data Cleanup

Tests create users with prefix `gameplay_`:
- `gameplay_alice` + unique timestamp
- `gameplay_bob` + unique timestamp
- `gameplay_charlie` + unique timestamp

**Auto-cleanup after tests:**
```bash
# Script includes cleanup (see run-gameplay-tests.sh)
# Or manual:
psql -d memesploding << EOF
DELETE FROM match_participants WHERE user_id IN (SELECT id FROM users WHERE username LIKE '%gameplay%');
DELETE FROM rooms WHERE created_by_id IN (SELECT id FROM users WHERE username LIKE '%gameplay%');
DELETE FROM users WHERE username LIKE '%gameplay%';
EOF
```

### Timestamps in Device IDs

Using `{{$timestamp}}` in device IDs ensures:
- No duplicate users on re-runs
- Unique identifiers per test execution
- Automatic cleanup by age

---

## ✅ Success Criteria

- ✅ All 3 users login successfully
- ✅ Room created with correct code
- ✅ All 3 users join room
- ✅ Ready status set for all users
- ✅ Match starts without errors
- ✅ Game tickets generated for all users
- ✅ WebSocket connections established
- ✅ Handshake message accepted
- ✅ Game commands received by server
- ✅ Server broadcasts game events
- ✅ No crashes or unhandled exceptions

---

## 📚 Related Files

- **Main Integration Tests:** `../../../Memesploding.Api/HttpTests/IntegrationTests.http`
- **API Room Tests:** `../../../Memesploding.Api/HttpTests/Room.HappyPath.http`
- **API Docs:** `../../../memesploding-docs/design/api/`
- **Database Schema:** `../../../Memesploding.Shared/Entities/`

---

## 🤝 Contributing

To extend tests:

1. Add new game commands to `GameplayCommands.md`
2. Add new WebSocket scenarios to `run-gameplay-tests.sh`
3. Update this README with new test counts
4. Run tests to verify

---

## ✅ Status

**Last Updated:** 2026-04-23

**Test Suite:** Complete and updated for API server compatibility
- Setup flow: ✅ (login, room, join, ready, start)
- Game commands: ✅ (all SignalR targets documented)
- Environment: ✅ (matches API server env)
- Automation: 🚧 (bash script template ready)

**Ready for:** Manual testing, automation with websocat, CI/CD integration
