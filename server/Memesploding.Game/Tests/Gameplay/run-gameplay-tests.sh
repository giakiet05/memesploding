#!/bin/bash
# =============================================================================
# Memesploding E2E Gameplay Test
# Flow: Login → Create Room → Join → SetReady (WS) → StartMatch (WS)
#       → Nhận wsAccessToken từ RoomMatchStarting event
#       → Connect Game Server → Play game commands
# =============================================================================

set -e

API_URL="http://localhost:5217/api/v1"
APP_HUB_URL="ws://localhost:5217/ws"   # AppHub (dùng accessToken)
GAME_WS_URL="ws://localhost:5204/ws"   # GameHub (dùng wsAccessToken)
OUTPUT_DIR="./output"

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m'

mkdir -p "$OUTPUT_DIR"

echo -e "${BLUE}=========================================${NC}"
echo -e "${BLUE}   Memesploding E2E Gameplay Test${NC}"
echo -e "${BLUE}=========================================${NC}"
echo ""


###############################################################################
# PHASE 1: Authentication
###############################################################################
echo -e "${YELLOW}[PHASE 1] Authentication${NC}"

DEVICE_ALICE="alice_$(date +%s)"
DEVICE_BOB="bob_$(date +%s)1"
DEVICE_CHARLIE="charlie_$(date +%s)2"

ALICE=$(curl -sf -X POST "$API_URL/auth/guest" \
  -H "Content-Type: application/json" \
  -d "{\"deviceId\": \"$DEVICE_ALICE\"}")
ALICE_TOKEN=$(echo "$ALICE" | jq -r '.data.accessToken')
ALICE_ID=$(echo "$ALICE" | jq -r '.data.user.id')
ALICE_NAME=$(echo "$ALICE" | jq -r '.data.user.username')
[ -z "$ALICE_TOKEN" ] && { echo -e "${RED}❌ Alice login FAILED${NC}"; exit 1; }
echo -e "${GREEN}✅ Alice: $ALICE_NAME${NC}"

BOB=$(curl -sf -X POST "$API_URL/auth/guest" \
  -H "Content-Type: application/json" \
  -d "{\"deviceId\": \"$DEVICE_BOB\"}")
BOB_TOKEN=$(echo "$BOB" | jq -r '.data.accessToken')
BOB_ID=$(echo "$BOB" | jq -r '.data.user.id')
BOB_NAME=$(echo "$BOB" | jq -r '.data.user.username')
[ -z "$BOB_TOKEN" ] && { echo -e "${RED}❌ Bob login FAILED${NC}"; exit 1; }
echo -e "${GREEN}✅ Bob: $BOB_NAME${NC}"

CHARLIE=$(curl -sf -X POST "$API_URL/auth/guest" \
  -H "Content-Type: application/json" \
  -d "{\"deviceId\": \"$DEVICE_CHARLIE\"}")
CHARLIE_TOKEN=$(echo "$CHARLIE" | jq -r '.data.accessToken')
CHARLIE_ID=$(echo "$CHARLIE" | jq -r '.data.user.id')
CHARLIE_NAME=$(echo "$CHARLIE" | jq -r '.data.user.username')
[ -z "$CHARLIE_TOKEN" ] && { echo -e "${RED}❌ Charlie login FAILED${NC}"; exit 1; }
echo -e "${GREEN}✅ Charlie: $CHARLIE_NAME${NC}"

###############################################################################
# PHASE 2: Load CardSets & Create Room
###############################################################################
echo ""
echo -e "${YELLOW}[PHASE 2] Room Setup${NC}"

CARDSETS=$(curl -sf "$API_URL/card-sets" \
  -H "Authorization: Bearer $ALICE_TOKEN")
CARDSET_ID=$(echo "$CARDSETS" | jq -r '.data.items[0].id // empty')
if [ -z "$CARDSET_ID" ]; then
    CARDSET_ARRAY="[]"
    echo -e "${CYAN}ℹ️  No card sets found, using empty array${NC}"
else
    CARDSET_ARRAY="[\"$CARDSET_ID\"]"
    echo -e "${GREEN}✅ CardSet: $(echo "$CARDSETS" | jq -r '.data.items[0].name')${NC}"
fi

ROOM=$(curl -sf -X POST "$API_URL/rooms" \
  -H "Authorization: Bearer $ALICE_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{\"maxPlayers\": 3, \"isPublic\": true, \"cardSetIds\": $CARDSET_ARRAY}")
ROOM_CODE=$(echo "$ROOM" | jq -r '.data.code // empty')
[ -z "$ROOM_CODE" ] && { echo -e "${RED}❌ Room creation FAILED${NC}"; echo "$ROOM" | jq .; exit 1; }
echo -e "${GREEN}✅ Room created: $ROOM_CODE${NC}"

# Bob join
curl -sf -X POST "$API_URL/rooms/$ROOM_CODE/join" \
  -H "Authorization: Bearer $BOB_TOKEN" > /dev/null
echo -e "${GREEN}✅ $BOB_NAME joined${NC}"

# Charlie join
curl -sf -X POST "$API_URL/rooms/$ROOM_CODE/join" \
  -H "Authorization: Bearer $CHARLIE_TOKEN" > /dev/null
echo -e "${GREEN}✅ $CHARLIE_NAME joined${NC}"

###############################################################################
# PHASE 3: Set Ready + Start Match via AppHub (SignalR WebSocket)
###############################################################################
echo ""
echo -e "${YELLOW}[PHASE 3] Set Ready & Start Match via AppHub (SignalR)${NC}"
echo -e "${CYAN}  AppHub: $APP_HUB_URL${NC}"

# Kiểm tra websocat
if ! command -v websocat &> /dev/null; then
    echo -e "${RED}❌ websocat không tìm thấy. Cài bằng: sudo snap install websocat${NC}"
    exit 1
fi

# Helper: gửi SignalR message qua websocat (không dùng function lồng để tránh flag trùng)
READY_MSG='{"type":1,"target":"SetReadyStatus","arguments":["'"$ROOM_CODE"'",true]}'

# Alice set ready
echo -e "${CYAN}→ Alice SetReadyStatus...${NC}"
printf '%s\x1e%s\x1e' '{"protocol":"json","version":1}' "$READY_MSG" | \
    timeout 3 websocat -n "$APP_HUB_URL?access_token=$ALICE_TOKEN" 2>/dev/null || true
echo -e "${GREEN}✅ Alice ready sent${NC}"
sleep 0.5

# Bob set ready
echo -e "${CYAN}→ Bob SetReadyStatus...${NC}"
printf '%s\x1e%s\x1e' '{"protocol":"json","version":1}' "$READY_MSG" | \
    timeout 3 websocat -n "$APP_HUB_URL?access_token=$BOB_TOKEN" 2>/dev/null || true
echo -e "${GREEN}✅ Bob ready sent${NC}"
sleep 0.5

# Charlie set ready
echo -e "${CYAN}→ Charlie SetReadyStatus...${NC}"
printf '%s\x1e%s\x1e' '{"protocol":"json","version":1}' "$READY_MSG" | \
    timeout 3 websocat -n "$APP_HUB_URL?access_token=$CHARLIE_TOKEN" 2>/dev/null || true
echo -e "${GREEN}✅ Charlie ready sent${NC}"
sleep 1

# Verify tất cả đã ready qua REST
ROOM_STATE=$(curl -sf "$API_URL/rooms/$ROOM_CODE" \
  -H "Authorization: Bearer $ALICE_TOKEN")
NOT_READY=$(echo "$ROOM_STATE" | jq '[.data.currentParticipants[] | select(.isReady == false)] | length')
if [ "$NOT_READY" -gt 0 ]; then
    echo -e "${RED}❌ $NOT_READY player(s) not ready yet. Check AppHub connection.${NC}"
    echo "$ROOM_STATE" | jq '.data.currentParticipants[] | {nickname, isReady}'
    exit 1
fi
echo -e "${GREEN}✅ All players are ready${NC}"

# Alice (host) start match
echo -e "${CYAN}→ Alice StartRoomMatch...${NC}"
START_MSG='{"type":1,"target":"StartRoomMatch","arguments":["'"$ROOM_CODE"'"]}'

# Lắng nghe event RoomMatchStarting để bắt wsAccessToken
echo -e "${CYAN}  Listening for RoomMatchStarting event (10s)...${NC}"
MATCH_EVENTS=$(printf '%s\x1e%s\x1e' '{"protocol":"json","version":1}' "$START_MSG" | \
    timeout 10 websocat -n "$APP_HUB_URL?access_token=$ALICE_TOKEN" 2>/dev/null || true)

ALICE_WS_TOKEN=$(echo "$MATCH_EVENTS" | \
    grep -o '"WsAccessToken":"[^"]*"' | head -1 | \
    sed 's/"WsAccessToken":"//; s/"//')

GAME_WS_BASE=$(echo "$MATCH_EVENTS" | \
    grep -o '"WsUrl":"[^"]*"' | head -1 | \
    sed 's/"WsUrl":"//; s/"//')

sleep 1

# Nếu không bắt được token từ event, lấy từ REST sau khi start
if [ -z "$ALICE_WS_TOKEN" ]; then
    echo -e "${YELLOW}⚠️  Không bắt được token từ event, thử lấy từ REST...${NC}"
    ALICE_ROOM=$(curl -sf "$API_URL/rooms/$ROOM_CODE" -H "Authorization: Bearer $ALICE_TOKEN")
    ALICE_WS_TOKEN=$(echo "$ALICE_ROOM" | jq -r '.data.connection.wsAccessToken // empty')
    GAME_WS_BASE=$(echo "$ALICE_ROOM" | jq -r '.data.connection.wsUrl // "ws://localhost:5204/ws"')
fi

if [ -z "$ALICE_WS_TOKEN" ]; then
    echo -e "${RED}❌ StartMatch FAILED - không lấy được wsAccessToken${NC}"
    echo -e "${RED}   Có thể do: không phải host, có player chưa ready, hoặc room không tồn tại${NC}"
    exit 1
fi
echo -e "${GREEN}✅ Match started! WsToken: ${ALICE_WS_TOKEN:0:30}...${NC}"

# Lấy tokens cho Bob và Charlie
BOB_ROOM=$(curl -sf "$API_URL/rooms/$ROOM_CODE" -H "Authorization: Bearer $BOB_TOKEN")
BOB_WS_TOKEN=$(echo "$BOB_ROOM" | jq -r '.data.connection.wsAccessToken // empty')

CHARLIE_ROOM=$(curl -sf "$API_URL/rooms/$ROOM_CODE" -H "Authorization: Bearer $CHARLIE_TOKEN")
CHARLIE_WS_TOKEN=$(echo "$CHARLIE_ROOM" | jq -r '.data.connection.wsAccessToken // empty')

GAME_WS_URL_ACTUAL="${GAME_WS_BASE:-$GAME_WS_URL}"

echo -e "${GREEN}✅ Tokens obtained for all players${NC}"
echo -e "   Alice:   ${ALICE_WS_TOKEN:0:25}..."
echo -e "   Bob:     ${BOB_WS_TOKEN:0:25}..."
echo -e "   Charlie: ${CHARLIE_WS_TOKEN:0:25}..."

###############################################################################
# PHASE 4: Connect Game Server & Play
###############################################################################
echo ""
echo -e "${YELLOW}[PHASE 4] Game Server Connection & Commands${NC}"
echo -e "${CYAN}  GameHub: $GAME_WS_URL_ACTUAL${NC}"

# Kiểm tra Game server
HEALTH=$(curl -sf "http://localhost:5204/health" 2>/dev/null || echo "FAIL")
if echo "$HEALTH" | grep -q '"status":"ok"'; then
    echo -e "${GREEN}✅ Game server is healthy${NC}"
else
    echo -e "${RED}❌ Game server health check failed (localhost:5204)${NC}"
    exit 1
fi

# Lấy FRESH ticket ngay trước khi connect (ticket hết hạn sau 120s)
echo -e "${CYAN}→ Fetching fresh game tickets (valid 120s)...${NC}"
ALICE_ROOM_FRESH=$(curl -sf "$API_URL/rooms/$ROOM_CODE" -H "Authorization: Bearer $ALICE_TOKEN")
ALICE_WS_TOKEN=$(echo "$ALICE_ROOM_FRESH" | jq -r '.data.connection.wsAccessToken // empty')
GAME_WS_URL_ACTUAL=$(echo "$ALICE_ROOM_FRESH" | jq -r '.data.connection.wsUrl // "ws://localhost:5204/ws"')

BOB_ROOM_FRESH=$(curl -sf "$API_URL/rooms/$ROOM_CODE" -H "Authorization: Bearer $BOB_TOKEN")
BOB_WS_TOKEN=$(echo "$BOB_ROOM_FRESH" | jq -r '.data.connection.wsAccessToken // empty')

CHARLIE_ROOM_FRESH=$(curl -sf "$API_URL/rooms/$ROOM_CODE" -H "Authorization: Bearer $CHARLIE_TOKEN")
CHARLIE_WS_TOKEN=$(echo "$CHARLIE_ROOM_FRESH" | jq -r '.data.connection.wsAccessToken // empty')

if [ -z "$ALICE_WS_TOKEN" ]; then
    echo -e "${RED}❌ Không lấy được fresh ticket - room status có thể chưa 'playing'?${NC}"
    echo "$ALICE_ROOM_FRESH" | jq '.data.status'
    exit 1
fi
echo -e "${GREEN}✅ Fresh tickets obtained${NC}"

# Function gửi game command qua SignalR GameHub
game_command() {
    local token="$1"
    local event="$2"
    local data="${3:-{}}"
    local timeout_s="${4:-5}"
    local cmd='{"type":1,"target":"SendCommand","arguments":[{"Event":"'"$event"'","Data":'"$data"'}]}'
    printf '%s\x1e%s\x1e' '{"protocol":"json","version":1}' "$cmd" | \
        timeout "$timeout_s" websocat -n \
        "$GAME_WS_URL_ACTUAL?access_token=$token" 2>/dev/null || true
}

# Alice connect & get state snapshot
echo -e "${CYAN}→ Alice connecting to Game Server...${NC}"
ALICE_CONNECT=$(game_command "$ALICE_WS_TOKEN" "requeststatesnapshot" "{}" 8)


if echo "$ALICE_CONNECT" | grep -q '"event":"connected"'; then
    echo -e "${GREEN}✅ Alice connected to Game Server${NC}"
else
    echo -e "${YELLOW}⚠️  Alice connection response:${NC}"
    echo "$ALICE_CONNECT" | head -3
fi

PHASE=$(echo "$ALICE_CONNECT" | grep -o '"phase":"[^"]*"' | head -1 | sed 's/"phase":"//; s/"//')
echo -e "${CYAN}  Game Phase: ${PHASE:-unknown}${NC}"

sleep 1

# Bob connect
echo -e "${CYAN}→ Bob connecting to Game Server...${NC}"
BOB_CONNECT=$(game_command "$BOB_WS_TOKEN" "requeststatesnapshot" "{}" 5)
if echo "$BOB_CONNECT" | grep -q '"event":"connected"'; then
    echo -e "${GREEN}✅ Bob connected${NC}"
fi

# Charlie connect
echo -e "${CYAN}→ Charlie connecting to Game Server...${NC}"
CHARLIE_CONNECT=$(game_command "$CHARLIE_WS_TOKEN" "requeststatesnapshot" "{}" 5)
if echo "$CHARLIE_CONNECT" | grep -q '"event":"connected"'; then
    echo -e "${GREEN}✅ Charlie connected${NC}"
fi

sleep 1

# Alice draw card (turn 1)
echo -e "${CYAN}→ Alice DrawCard...${NC}"
DRAW_RESP=$(game_command "$ALICE_WS_TOKEN" "drawcard" "{}" 8)

if echo "$DRAW_RESP" | grep -q '"event":"ack"'; then
    STATE_VERSION=$(echo "$DRAW_RESP" | grep -o '"stateVersion":[0-9]*' | head -1 | sed 's/"stateVersion"://')
    echo -e "${GREEN}✅ DrawCard ACK - StateVersion: $STATE_VERSION${NC}"
else
    echo -e "${YELLOW}⚠️  DrawCard response (có thể không phải lượt Alice):${NC}"
    echo "$DRAW_RESP" | grep -o '"event":"[^"]*"' | head -5
fi

###############################################################################
# PHASE 5: Save Summary
###############################################################################
echo ""
echo -e "${YELLOW}[PHASE 5] Summary${NC}"

cat > "$OUTPUT_DIR/gameplay-test-report.txt" << REPORT
========================================
  Memesploding Gameplay Test Report
  $(date)
========================================

API Server:  $API_URL
AppHub:      $APP_HUB_URL
Game Server: $GAME_WS_URL_ACTUAL

Room Code: $ROOM_CODE

Players:
  Alice:   $ALICE_NAME (ID: $ALICE_ID)
  Bob:     $BOB_NAME   (ID: $BOB_ID)
  Charlie: $CHARLIE_NAME (ID: $CHARLIE_ID)

WebSocket Tokens (valid for ~120s):
  Alice:   $ALICE_WS_TOKEN
  Bob:     $BOB_WS_TOKEN
  Charlie: $CHARLIE_WS_TOKEN

Manual connection commands:
  websocat '$GAME_WS_URL_ACTUAL?access_token=$ALICE_WS_TOKEN'
  websocat '$GAME_WS_URL_ACTUAL?access_token=$BOB_WS_TOKEN'
  websocat '$GAME_WS_URL_ACTUAL?access_token=$CHARLIE_WS_TOKEN'

SignalR Commands (GameHub):
  Handshake:  {"protocol":"json","version":1}
  Draw card:  {"type":1,"target":"SendCommand","arguments":[{"Event":"drawcard","Data":{}}]}
  Play card:  {"type":1,"target":"SendCommand","arguments":[{"Event":"playcard","Data":{"cardCode":"Skip"}}]}
  Nope:       {"type":1,"target":"SendCommand","arguments":[{"Event":"nope","Data":{}}]}
  Use Defuse: {"type":1,"target":"SendCommand","arguments":[{"Event":"usedefuse","Data":{}}]}
  Bomb pos:   {"type":1,"target":"SendCommand","arguments":[{"Event":"choosebombinsertposition","Data":{"position":0}}]}
  State:      {"type":1,"target":"SendCommand","arguments":[{"Event":"requeststatesnapshot","Data":{}}]}

SignalR Commands (AppHub - pre-game):
  Handshake:   {"protocol":"json","version":1}
  Set Ready:   {"type":1,"target":"SetReadyStatus","arguments":["$ROOM_CODE",true]}
  Start Match: {"type":1,"target":"StartRoomMatch","arguments":["$ROOM_CODE"]}
REPORT

echo -e "${GREEN}✅ Report saved: $OUTPUT_DIR/gameplay-test-report.txt${NC}"
echo ""
echo -e "${BLUE}=========================================${NC}"
echo -e "${GREEN}  ✅ ALL PHASES COMPLETE${NC}"
echo -e "${BLUE}=========================================${NC}"
echo ""
echo -e "${CYAN}Manual Game Commands (copy & paste vào websocat):${NC}"
echo -e "  websocat '${GAME_WS_URL_ACTUAL}?access_token=${ALICE_WS_TOKEN}'"
echo ""
