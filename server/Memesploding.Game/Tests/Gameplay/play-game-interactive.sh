#!/bin/bash
set -e

###############################################################################
# Memesploding Interactive WebSocket Game Play Test
# - Setup 3 players via REST
# - Extract WebSocket tokens
# - Show connection commands for manual testing
###############################################################################

HTTP_BASE_URL="http://localhost:5217/api/v1"
WS_BASE_URL="ws://localhost:5204/ws"
OUTPUT_DIR="./output/ws-game"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
MAGENTA='\033[0;35m'
NC='\033[0m'

mkdir -p "$OUTPUT_DIR"

cat << 'EOF'
╔═══════════════════════════════════════════════════════════════════════════╗
║                   🎮 MEMESPLODING WEBSOCKET GAME TEST                    ║
║                                                                           ║
║              3 Players → Join Room → Ready → Play Game                   ║
╚═══════════════════════════════════════════════════════════════════════════╝
EOF

echo ""

###############################################################################
# STEP 1: Setup Players & Room via REST
###############################################################################

echo -e "${YELLOW}[STEP 1] Setting up players & room via REST API...${NC}\n"

DEVICE_ALICE="alice_$(date +%s)"
DEVICE_BOB="bob_$(date +%s)"
DEVICE_CHARLIE="charlie_$(date +%s)"

# Alice login
echo -e "${CYAN}→ Alice logging in...${NC}"
ALICE=$(curl -s -X POST "$HTTP_BASE_URL/auth/guest" \
  -H "Content-Type: application/json" \
  -d "{\"deviceId\": \"$DEVICE_ALICE\"}")

ALICE_TOKEN=$(echo "$ALICE" | jq -r '.data.accessToken')
ALICE_ID=$(echo "$ALICE" | jq -r '.data.user.id')
ALICE_NAME=$(echo "$ALICE" | jq -r '.data.user.username')

echo -e "${GREEN}✅ $ALICE_NAME logged in${NC}"

# Bob login
echo -e "${CYAN}→ Bob logging in...${NC}"
BOB=$(curl -s -X POST "$HTTP_BASE_URL/auth/guest" \
  -H "Content-Type: application/json" \
  -d "{\"deviceId\": \"$DEVICE_BOB\"}")

BOB_TOKEN=$(echo "$BOB" | jq -r '.data.accessToken')
BOB_NAME=$(echo "$BOB" | jq -r '.data.user.username')

echo -e "${GREEN}✅ $BOB_NAME logged in${NC}"

# Charlie login
echo -e "${CYAN}→ Charlie logging in...${NC}"
CHARLIE=$(curl -s -X POST "$HTTP_BASE_URL/auth/guest" \
  -H "Content-Type: application/json" \
  -d "{\"deviceId\": \"$DEVICE_CHARLIE\"}")

CHARLIE_TOKEN=$(echo "$CHARLIE" | jq -r '.data.accessToken')
CHARLIE_NAME=$(echo "$CHARLIE" | jq -r '.data.user.username')

echo -e "${GREEN}✅ $CHARLIE_NAME logged in${NC}"

# Create room
echo -e "\n${CYAN}→ Creating room...${NC}"
ROOM=$(curl -s -X POST "$HTTP_BASE_URL/rooms" \
  -H "Authorization: Bearer $ALICE_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"maxPlayers": 3, "isPublic": true, "cardSetIds": []}')

ROOM_CODE=$(echo "$ROOM" | jq -r '.data.code')
echo -e "${GREEN}✅ Room created: $ROOM_CODE${NC}"

# Join room
echo -e "\n${CYAN}→ Players joining room...${NC}"
curl -s -X POST "$HTTP_BASE_URL/rooms/$ROOM_CODE/join" \
  -H "Authorization: Bearer $BOB_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{}' > /dev/null
echo -e "${GREEN}✅ $BOB_NAME joined${NC}"

curl -s -X POST "$HTTP_BASE_URL/rooms/$ROOM_CODE/join" \
  -H "Authorization: Bearer $CHARLIE_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{}' > /dev/null
echo -e "${GREEN}✅ $CHARLIE_NAME joined${NC}"

# Mark ready via AppHub (SignalR WebSocket)
echo -e "\n${CYAN}→ Marking players ready via AppHub (WS)...${NC}"
READY_MSG='{"type":1,"target":"SetReadyStatus","arguments":["'"$ROOM_CODE"'",true]}'
# Gửi SetReadyStatus qua SignalR cho từng player
for PLAYER_TOKEN in "$ALICE_TOKEN" "$BOB_TOKEN" "$CHARLIE_TOKEN"; do
    printf '%s\x1e%s\x1e' '{"protocol":"json","version":1}' "$READY_MSG" | \
        timeout 3 websocat -n "ws://localhost:5217/ws?access_token=$PLAYER_TOKEN" 2>/dev/null || true
    sleep 0.3
done
echo -e "${GREEN}✅ Ready messages sent${NC}"

# Start match auto via AppHub
echo -e "\n${CYAN}→ Starting match via AppHub (WS) as Alice...${NC}"
START_MSG='{"type":1,"target":"StartRoomMatch","arguments":["'"$ROOM_CODE"'"]}'
printf '%s\x1e%s\x1e' '{"protocol":"json","version":1}' "$START_MSG" | \
    timeout 5 websocat -n "ws://localhost:5217/ws?access_token=$ALICE_TOKEN" 2>/dev/null || true
echo -e "${GREEN}✅ Match start requested${NC}"

# Get connection info
sleep 2
echo -e "\n${CYAN}→ Fetching Game WebSocket tickets from API...${NC}"

ALICE_ROOM=$(curl -sf -X GET "$HTTP_BASE_URL/rooms/$ROOM_CODE" \
  -H "Authorization: Bearer $ALICE_TOKEN")

BOB_ROOM=$(curl -s -X GET "$HTTP_BASE_URL/rooms/$ROOM_CODE" \
  -H "Authorization: Bearer $BOB_TOKEN")

CHARLIE_ROOM=$(curl -s -X GET "$HTTP_BASE_URL/rooms/$ROOM_CODE" \
  -H "Authorization: Bearer $CHARLIE_TOKEN")

ALICE_WS_TOKEN=$(echo "$ALICE_ROOM" | jq -r '.data.connection.wsAccessToken // ""')
BOB_WS_TOKEN=$(echo "$BOB_ROOM" | jq -r '.data.connection.wsAccessToken // ""')
CHARLIE_WS_TOKEN=$(echo "$CHARLIE_ROOM" | jq -r '.data.connection.wsAccessToken // ""')

WS_URL=$(echo "$ALICE_ROOM" | jq -r '.data.connection.wsUrl')

# Save tokens to files for connection
echo "$ALICE_WS_TOKEN" > "$OUTPUT_DIR/alice_token"
echo "$BOB_WS_TOKEN" > "$OUTPUT_DIR/bob_token"
echo "$CHARLIE_WS_TOKEN" > "$OUTPUT_DIR/charlie_token"
echo "$WS_URL" > "$OUTPUT_DIR/ws_url"
echo "$ROOM_CODE" > "$OUTPUT_DIR/room_code"

echo -e "${GREEN}✅ Tokens obtained${NC}"

###############################################################################
# STEP 2: Show Connection Instructions
###############################################################################

cat << EOF

${BLUE}═══════════════════════════════════════════════════════════════════════════${NC}
${YELLOW}[STEP 2] WebSocket Connection Info - Use in 3 Terminals${NC}
${BLUE}═══════════════════════════════════════════════════════════════════════════${NC}

${CYAN}📍 Room Code:${NC} ${YELLOW}$ROOM_CODE${NC}
${CYAN}📡 WebSocket URL:${NC} ${YELLOW}$WS_URL${NC}

${MAGENTA}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}
${YELLOW}🎮 PLAYER 1: Alice ($ALICE_NAME)${NC}
${MAGENTA}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}

Token: ${ALICE_WS_TOKEN}

Run in terminal 1:
${GREEN}websocat '${WS_URL}?access_token=${ALICE_WS_TOKEN}'${NC}

Handshake (gửi đầu tiên):
${CYAN}{"protocol":"json","version":1}${NC}

Game commands:
${CYAN}{"type":1,"target":"SendCommand","arguments":[{"Event":"drawcard","Data":{}}]}${NC}
${CYAN}{"type":1,"target":"SendCommand","arguments":[{"Event":"playcard","Data":{"cardCode":"Attack"}}]}${NC}

${MAGENTA}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}
${YELLOW}🎮 PLAYER 2: Bob ($BOB_NAME)${NC}
${MAGENTA}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}

Token: ${BOB_WS_TOKEN}

Run in terminal 2:
${GREEN}websocat '${WS_URL}?access_token=${BOB_WS_TOKEN}'${NC}

Handshake (gửi đầu tiên):
${CYAN}{"protocol":"json","version":1}${NC}

Game commands:
${CYAN}{"type":1,"target":"SendCommand","arguments":[{"Event":"drawcard","Data":{}}]}${NC}
${CYAN}{"type":1,"target":"SendCommand","arguments":[{"Event":"nope","Data":{}}]}${NC}

${MAGENTA}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}
${YELLOW}🎮 PLAYER 3: Charlie ($CHARLIE_NAME)${NC}
${MAGENTA}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}

Token: ${CHARLIE_WS_TOKEN}

Run in terminal 3:
${GREEN}websocat '${WS_URL}?access_token=${CHARLIE_WS_TOKEN}'${NC}

Handshake (gửi đầu tiên):
${CYAN}{"protocol":"json","version":1}${NC}

Game commands:
${CYAN}{"type":1,"target":"SendCommand","arguments":[{"Event":"drawcard","Data":{}}]}${NC}
${CYAN}{"type":1,"target":"SendCommand","arguments":[{"Event":"nope","Data":{}}]}${NC}

${BLUE}═══════════════════════════════════════════════════════════════════════════${NC}
${YELLOW}[QUICK START - COPY VÀO POSTMAN / WS KING]${NC}

${CYAN}# Alice URL:${NC}
${WS_URL}?access_token=${ALICE_WS_TOKEN}

${CYAN}# Bob URL:${NC}
${WS_URL}?access_token=${BOB_WS_TOKEN}

${CYAN}# Charlie URL:${NC}
${WS_URL}?access_token=${CHARLIE_WS_TOKEN}

${BLUE}═══════════════════════════════════════════════════════════════════════════${NC}
${YELLOW}[HOẶC CHẠY TRÊN TERMINAL BẰNG WEBSOCAT]${NC}

websocat '${WS_URL}?access_token=${ALICE_WS_TOKEN}'
websocat '${WS_URL}?access_token=${BOB_WS_TOKEN}'
websocat '${WS_URL}?access_token=${CHARLIE_WS_TOKEN}'

${BLUE}═══════════════════════════════════════════════════════════════════════════${NC}
${YELLOW}[GAME COMMANDS - SignalR Format]${NC}

${CYAN}Draw card:${NC}
  {"type":1,"target":"SendCommand","arguments":[{"Event":"drawcard","Data":{}}]}

${CYAN}Play card:${NC}
  {"type":1,"target":"SendCommand","arguments":[{"Event":"playcard","Data":{"cardCode":"Skip"}}]}
  {"type":1,"target":"SendCommand","arguments":[{"Event":"playcard","Data":{"cardCode":"Attack"}}]}
  {"type":1,"target":"SendCommand","arguments":[{"Event":"playcard","Data":{"cardCode":"Favor","targetUserId":"UUID"}}]}

${CYAN}React:${NC}
  {"type":1,"target":"SendCommand","arguments":[{"Event":"nope","Data":{}}]}

${CYAN}Use defuse:${NC}
  {"type":1,"target":"SendCommand","arguments":[{"Event":"usedefuse","Data":{}}]}

${CYAN}Bomb position:${NC}
  {"type":1,"target":"SendCommand","arguments":[{"Event":"choosebombinsertposition","Data":{"position":0}}]}

${BLUE}═══════════════════════════════════════════════════════════════════════════${NC}

✅ Setup complete! Waiting for WebSocket connections...

EOF

###############################################################################
# STEP 3: Wait for Interactive Testing
###############################################################################

echo -e "\n${YELLOW}[WAITING] All setup is ready. You can now:${NC}"
echo -e "  1. Open 3 terminals"
echo -e "  2. Copy the websocat commands from above"
echo -e "  3. Connect each player"
echo -e "  4. Send game commands as shown"
echo ""
echo -e "${CYAN}Data saved to: $OUTPUT_DIR${NC}"
echo -e "  - alice_token"
echo -e "  - bob_token"
echo -e "  - charlie_token"
echo -e "  - ws_url"
echo -e "  - room_code"
echo ""

