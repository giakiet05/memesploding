# ✅ Verification Checklist

Run this to verify all test files are properly set up.

## File Structure
- [x] `GameplaySetup.http` - REST API setup flow (179 lines)
- [x] `GameplayCommands.md` - WebSocket commands (147 lines)
- [x] `http-client.env.json` - Environment config (at parent: ../http-client.env.json)
- [x] `run-gameplay-tests.sh` - Bash automation script (executable)
- [x] `README.md` - Full documentation
- [x] `QUICKSTART.md` - Quick start guide
- [x] `VERIFY.md` - This file

## Environment
- [x] `baseUrl` = `http://localhost:5217/api/v1`
- [x] `gameWsUrl` = `ws://localhost:5217/ws` (NOT 5204)
- [x] Device ID format with {{$timestamp}} (no duplicates)
- [x] Dev/Prod environments configured

## GameplaySetup.http
- [x] Uses {{baseUrl}} for API calls
- [x] Uses {{gameWsUrl}} for WebSocket
- [x] Phase 1: Login (3 users) ✅
- [x] Phase 2: Room (create, join) ✅
- [x] Phase 3: Ready & Start ✅
- [x] Phase 4: Get tickets ✅
- [x] Auto-extraction of tokens & IDs ✅

## GameplayCommands.md
- [x] Correct WS URL (5217)
- [x] Handshake message documented
- [x] All game commands (draw, play, nope, defuse, bomb position)
- [x] Request state snapshot
- [x] Heartbeat
- [x] websocat installation guide
- [x] SignalR protocol notes

## run-gameplay-tests.sh
- [x] Executable permission set
- [x] Phase 1: Auth (alice, bob, charlie)
- [x] Phase 2: Room setup (create, join)
- [x] Phase 3: Ready & start
- [x] Phase 4: Get tickets
- [x] Phase 5: WebSocket test (optional)
- [x] Colorized output
- [x] Error handling
- [x] Test reports (text + URLs)
- [x] Uses curl, jq, websocat

## Documentation
- [x] README.md - Full guide (8121 bytes)
- [x] QUICKSTART.md - Quick ref (4717 bytes)
- [x] Tests/README.md - Overview
- [x] Troubleshooting sections
- [x] Prerequisites listed
- [x] 3 testing options documented

## Ready to Test?
```bash
cd Memesploding.Game/Tests/Gameplay

# Option 1: Manual (Rider)
# Open GameplaySetup.http → Select "dev" → Run phases

# Option 2: Automated (Bash)
./run-gameplay-tests.sh

# Option 3: Manual WebSocket
wscat -c "ws://localhost:5217/ws?access_token=TOKEN"
```

---

**Status:** ✅ All files created and verified
**Next:** Try running tests to verify everything works!
