# Memesploding API - Integration Test Suite

Complete integration test suite covering **all 30 REST endpoints + WebSocket interactions**.

## 📁 Files

| File | Description | Tool |
|------|-------------|------|
| **IntegrationTests.http** | Main cross-domain smoke tests (non-room heavy) | Rider/IntelliJ HTTP Client |
| **Room.HappyPath.http** | Room happy-path flows (REST + WebSocket) | Rider/IntelliJ HTTP Client |
| **Room.EdgeCases.http** | Room negative/edge-case flows (REST + WebSocket) | Rider/IntelliJ HTTP Client |
| **Room.Reconnect.http** | Room reconnect/grace-timeout scenarios (manual + REST resync) | Rider/IntelliJ HTTP Client |
| **run-integration-tests.sh** | Automated bash runner (curl-based) | Bash + curl + jq |
| **ws-test.sh** | WebSocket automated tests | Bash + websocat |
| **ws-test-manual.md** | WebSocket manual testing guide | wscat or websocat |
| **seed-match-data.sql** | Optional: Create mock match data | PostgreSQL |
| **http-client.env.json** | Environment configuration (base URLs) | HTTP Client config |
| **output/** | Generated test reports and logs | Auto-created |

---

## 🚀 Quick Start

### Option 1: Run in Rider (Recommended)

1. Open `IntegrationTests.http` in Rider
2. Click "Run All Requests in File" (or run individual sections)
3. View inline results with auto variable extraction
4. Check response validation in test scripts

### Option 2: Automated Bash Script

```bash
# Install dependencies (if not already installed)
sudo apt install curl jq

# Make executable
chmod +x run-integration-tests.sh

# Run tests
./run-integration-tests.sh

# View report
cat output/test-report.txt
```

### Option 3: WebSocket Tests

**Automated:**
```bash
# Install websocat
cargo install websocat
# or: sudo snap install websocat

# Get tokens from REST tests first
./run-integration-tests.sh

# Run WebSocket tests (replace with actual tokens)
./ws-test.sh "alice_token_here" "bob_token_here"
```

**Manual (detailed testing):**
```bash
# Install wscat
npm install -g wscat

# Follow guide
less ws-test-manual.md

# Example: Connect as Alice
wscat -c "ws://localhost:5217/ws?access_token=TOKEN"
```

---

## 📋 Test Coverage

### REST API Endpoints (26/26) ✅

| Category | Endpoints | Status |
|----------|-----------|--------|
| **Auth** | register, login | ✅ 2/2 |
| **Users** | me/profile, me/stats, :id, :id/stats, search, leaderboard | ✅ 6/6 |
| **Friendships** | send, respond, list | ✅ 3/3 |
| **CardSets** | list, detail | ✅ 2/2 |
| **Rooms** | create, browse, peek | ✅ 3/3 |
| **Notifications** | list, count, mark, delete, mark-all, clear-all | ✅ 6/6 |
| **Matches** | history, detail, timeline | ✅ 3/3 |
| **Matchmaking** | quick-play | ✅ 1/1 |

### WebSocket Actions (5/5) ✅

1. `heartbeat` - Refresh presence TTL
2. `invite_to_room` - Invite friend to room
3. `respond_invitation` - Accept/decline invitation
4. `request_join_room` - Request join private room
5. `respond_join_request` - Accept/decline request

### Error Cases ✅

- Duplicate friend request → 400
- Invalid room code → 404
- Unauthorized access → 401
- Rate limiting → 429
- WebSocket invalid token → Connection refused

---

## 📖 Test Flow

### Phase 1: Registration & Authentication
- Alice, Bob, Charlie register
- Login and get JWT tokens
- Tokens auto-extracted for subsequent requests

### Phase 2: Social Discovery
- View profiles and stats
- Search users
- Check leaderboard (early state)

### Phase 3: Friendship Management
- Alice send friend request to Bob
- Bob view notifications
- Bob accept request
- Both view friendships list
- **Error case:** Duplicate request

### Phase 4: Content Exploration
- Browse all card sets
- View card set details with cards

### Phase 5: Room Management
- Alice create private room
- Bob browse public rooms (empty)
- Alice peek room details
- **Error case:** Invalid room code

### Phase 6: Notification Management
- Bob mark notifications as read
- Check unread count
- Mark all as read
- Delete notifications
- Clear all

### Phase 7: Quick Matchmaking
- Charlie quick-play (instant random join)

### Phase 8: Match History
- View match history
- View match details
- View match timeline (full replay)
- Check updated leaderboard
- **Note:** Requires match data (run `seed-match-data.sql` if empty)

### Phase 9: WebSocket Interaction
- Connect with JWT auth
- Heartbeat mechanism
- Presence broadcasting
- Room invitations
- Join requests
- **Documented only in .http, executable in ws-test.sh**

---

## 🛠️ Prerequisites

### Server Requirements
- Server running on `http://localhost:5217`
- PostgreSQL database initialized with migrations
- Redis running on default port (6379)

### Client Requirements

**For .http file (Rider):**
- IntelliJ IDEA or Rider (HTTP Client built-in)

**For bash script:**
- `curl` (HTTP client)
- `jq` (JSON processor: `sudo apt install jq`)

**For WebSocket tests:**
- **Option A:** `wscat` (manual): `npm install -g wscat`
- **Option B:** `websocat` (automated): `cargo install websocat` or `sudo snap install websocat`

---

## 📊 Output

### Bash Script Output Example

```
==========================================
  Memesploding Integration Test Suite
==========================================

ℹ️  Server: http://localhost:5217/api/v1
ℹ️  Output: ./output

ℹ️  PHASE 1: Registration & Authentication
ℹ️  [1] Testing: Alice Register...
✅ Alice Register
ℹ️  Alice Token: eyJhbGciOiJIUzI1NiIsInR5cCI6...
ℹ️  [2] Testing: Bob Register...
✅ Bob Register

... (more tests)

==========================================
           TEST SUMMARY
==========================================

Total Tests:  30
Passed:       29
Failed:       1

Pass Rate:    96.7%

Report saved to: ./output/test-report.txt

⚠️  Some tests failed. Check the report for details.
```

### Rider HTTP Client Output

Inline results with:
- ✅ Green checkmarks for passed tests
- ❌ Red X for failed tests
- Auto-extracted variables highlighted
- JSON response formatted

---

## 🐛 Troubleshooting

### Tests fail with "Connection refused"
**Solution:** Start the server first
```bash
cd /server/Memesploding.Api
dotnet run
```

### Tests fail with "Database connection error"
**Solution:** Check PostgreSQL is running and connection string is correct
```bash
sudo service postgresql status
# Edit appsettings.Development.json if needed
```

### Tests fail with "Redis connection error"
**Solution:** Start Redis
```bash
sudo service redis-server start
```

### WebSocket connection fails
**Solution:** 
- Check JWT token is valid (not expired)
- Verify server is running on correct port
- Check firewall allows WebSocket connections

### No match data found (Phase 8)
**Solution:** Run seed script
```bash
# Get user IDs first
psql -d memesploding -c "SELECT id, username FROM users WHERE username LIKE '%auto%';"

# Edit seed-match-data.sql and replace {ALICE_ID}, {BOB_ID}, {MATCH_ID}

# Run seed script
psql -d memesploding -f seed-match-data.sql
```

### Users already exist error
**Solution:** Use existing users or delete test users
```bash
# Delete test users
psql -d memesploding -c "DELETE FROM users WHERE username LIKE '%auto%' OR username LIKE '%integration%';"

# Or use login instead of register (change in .http file)
```

---

## 📝 Notes

### Test Data Cleanup

Tests create users with prefix `_auto` or `_integration`:
- `alice_auto` / `alice_integration`
- `bob_auto` / `bob_integration`
- `charlie_auto` / `charlie_integration`

**Cleanup script:**
```bash
psql -d memesploding << EOF
DELETE FROM match_participants WHERE user_id IN (SELECT id FROM users WHERE username LIKE '%auto%' OR username LIKE '%integration%');
DELETE FROM match_timeline_events WHERE match_id IN (SELECT id FROM matches WHERE room_code LIKE '%TEST%' OR room_code LIKE '%AUTO%');
DELETE FROM matches WHERE room_code LIKE '%TEST%' OR room_code LIKE '%AUTO%';
DELETE FROM notifications WHERE user_id IN (SELECT id FROM users WHERE username LIKE '%auto%' OR username LIKE '%integration%');
DELETE FROM friendships WHERE user_id_1 IN (SELECT id FROM users WHERE username LIKE '%auto%' OR username LIKE '%integration%');
DELETE FROM friendships WHERE user_id_2 IN (SELECT id FROM users WHERE username LIKE '%auto%' OR username LIKE '%integration%');
DELETE FROM users WHERE username LIKE '%auto%' OR username LIKE '%integration%';
EOF
```

### Environment Switching

Edit `http-client.env.json` to switch between dev and production:
```json
{
  "dev": {
    "baseUrl": "http://localhost:5217/api/v1"
  },
  "production": {
    "baseUrl": "https://api.memesploding.com/api/v1"
  }
}
```

In Rider, select environment in dropdown before running.

---

## 🎯 Success Criteria

- ✅ All 26 REST endpoints return success
- ✅ Auto variable extraction works (tokens, IDs, codes)
- ✅ Error cases return expected status codes
- ✅ WebSocket connection established
- ✅ Presence broadcasting works
- ✅ Invitations and join requests functional
- ✅ No server crashes or exceptions

---

## 📚 Further Reading

- **WebSocket API Docs:** `../WebSocketTests.md`
- **API Documentation:** `../../memesploding-docs/design/api/`
- **Database Schema:** `../../Memesploding.Shared/Entities/`

---

## 🤝 Contributing

To add more test scenarios:

1. Add to `IntegrationTests.http` with auto-extraction
2. Update `run-integration-tests.sh` with curl equivalent
3. Update this README with new test count
4. Run tests to verify

---

## ✅ Status

**Last Updated:** 2026-04-06

**Test Suite:** Complete and verified
- REST API: 26/26 endpoints ✅
- WebSocket: 5/5 actions ✅
- Error cases: 5+ scenarios ✅
- Documentation: Complete ✅

**Ready for:** Local development testing, CI/CD integration, QA validation
