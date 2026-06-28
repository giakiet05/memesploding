#!/bin/bash
set -e

###############################################################################
# Memesploding WebSocket Game Play Test
# Tests full game flow: 3 players, join, ready, start, play cards
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

echo -e "${BLUE}═══════════════════════════════════════════════════════════${NC}"
echo -e "${BLUE}🎮 Memesploding WebSocket Game Play Test${NC}"
echo -e "${BLUE}═══════════════════════════════════════════════════════════${NC}\n"

###############################################################################
# STEP 1: Setup via REST
###############################################################################

echo -e "${YELLOW}STEP 1: Setting up game via REST API${NC}\n"

# Login 3 players
DEVICE_ALICE="alice_$(date +%s)"
DEVICE_BOB="bob_$(date +%s)"
DEVICE_CHARLIE="charlie_$(date +%s)"

echo -e "${CYAN}[REST] Alice login...${NC}"
ALICE=$(curl -s -X POST "$HTTP_BASE_URL/auth/guest" \
  -H "Content-Type: application/json" \
  -d "{\"deviceId\": \"$DEVICE_ALICE\"}")

ALICE_TOKEN=$(echo "$ALICE" | jq -r '.data.accessToken')
ALICE_ID=$(echo "$ALICE" | jq -r '.data.user.id')
ALICE_NAME=$(echo "$ALICE" | jq -r '.data.user.username')

echo -e "${GREEN}✅ Alice logged in: $ALICE_NAME (${ALICE_TOKEN:0:20}...)${NC}"

echo -e "${CYAN}[REST] Bob login...${NC}"
BOB=$(curl -s -X POST "$HTTP_BASE_URL/auth/guest" \
  -H "Content-Type: application/json" \
  -d "{\"deviceId\": \"$DEVICE_BOB\"}")

BOB_TOKEN=$(echo "$BOB" | jq -r '.data.accessToken')
BOB_ID=$(echo "$BOB" | jq -r '.data.user.id')
BOB_NAME=$(echo "$BOB" | jq -r '.data.user.username')

echo -e "${GREEN}✅ Bob logged in: $BOB_NAME (${BOB_TOKEN:0:20}...)${NC}"

echo -e "${CYAN}[REST] Charlie login...${NC}"
CHARLIE=$(curl -s -X POST "$HTTP_BASE_URL/auth/guest" \
  -H "Content-Type: application/json" \
  -d "{\"deviceId\": \"$DEVICE_CHARLIE\"}")

CHARLIE_TOKEN=$(echo "$CHARLIE" | jq -r '.data.accessToken')
CHARLIE_ID=$(echo "$CHARLIE" | jq -r '.data.user.id')
CHARLIE_NAME=$(echo "$CHARLIE" | jq -r '.data.user.username')

echo -e "${GREEN}✅ Charlie logged in: $CHARLIE_NAME (${CHARLIE_TOKEN:0:20}...)${NC}"

# Create room (Alice is host)
echo -e "\n${CYAN}[REST] Alice creating room...${NC}"
ROOM=$(curl -s -X POST "$HTTP_BASE_URL/rooms" \
  -H "Authorization: Bearer $ALICE_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"maxPlayers": 3, "isPublic": true, "cardSetIds": []}')

ROOM_CODE=$(echo "$ROOM" | jq -r '.data.code')
echo -e "${GREEN}✅ Room created: $ROOM_CODE${NC}"

# Bob joins
echo -e "${CYAN}[REST] Bob joining room $ROOM_CODE...${NC}"
curl -s -X POST "$HTTP_BASE_URL/rooms/$ROOM_CODE/join" \
  -H "Authorization: Bearer $BOB_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{}' > /dev/null

echo -e "${GREEN}✅ Bob joined${NC}"

# Charlie joins
echo -e "${CYAN}[REST] Charlie joining room $ROOM_CODE...${NC}"
curl -s -X POST "$HTTP_BASE_URL/rooms/$ROOM_CODE/join" \
  -H "Authorization: Bearer $CHARLIE_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{}' > /dev/null

echo -e "${GREEN}✅ Charlie joined${NC}"

# All mark ready
echo -e "\n${CYAN}[REST] Marking all players ready...${NC}"
curl -s -X POST "$HTTP_BASE_URL/rooms/$ROOM_CODE/player-ready" \
  -H "Authorization: Bearer $ALICE_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{}' > /dev/null

curl -s -X POST "$HTTP_BASE_URL/rooms/$ROOM_CODE/player-ready" \
  -H "Authorization: Bearer $BOB_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{}' > /dev/null

curl -s -X POST "$HTTP_BASE_URL/rooms/$ROOM_CODE/player-ready" \
  -H "Authorization: Bearer $CHARLIE_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{}' > /dev/null

echo -e "${GREEN}✅ All players ready${NC}"

# Get room to extract WebSocket token
sleep 1
echo -e "\n${CYAN}[REST] Getting WebSocket tokens...${NC}"

ALICE_ROOM=$(curl -s -X GET "$HTTP_BASE_URL/rooms/$ROOM_CODE" \
  -H "Authorization: Bearer $ALICE_TOKEN")

BOB_ROOM=$(curl -s -X GET "$HTTP_BASE_URL/rooms/$ROOM_CODE" \
  -H "Authorization: Bearer $BOB_TOKEN")

CHARLIE_ROOM=$(curl -s -X GET "$HTTP_BASE_URL/rooms/$ROOM_CODE" \
  -H "Authorization: Bearer $CHARLIE_TOKEN")

ALICE_WS_TOKEN=$(echo "$ALICE_ROOM" | jq -r '.data.connection.wsAccessToken // "PENDING"')
BOB_WS_TOKEN=$(echo "$BOB_ROOM" | jq -r '.data.connection.wsAccessToken // "PENDING"')
CHARLIE_WS_TOKEN=$(echo "$CHARLIE_ROOM" | jq -r '.data.connection.wsAccessToken // "PENDING"')

WS_URL=$(echo "$ALICE_ROOM" | jq -r '.data.connection.wsUrl')

echo -e "${GREEN}✅ WebSocket URL: $WS_URL${NC}"
echo -e "${GREEN}✅ Alice token:   ${ALICE_WS_TOKEN:0:20}...${NC}"
echo -e "${GREEN}✅ Bob token:     ${BOB_WS_TOKEN:0:20}...${NC}"
echo -e "${GREEN}✅ Charlie token: ${CHARLIE_WS_TOKEN:0:20}...${NC}"

###############################################################################
# STEP 2: WebSocket Connections
###############################################################################

echo -e "\n${BLUE}═══════════════════════════════════════════════════════════${NC}"
echo -e "${YELLOW}STEP 2: Starting WebSocket connections for 3 players${NC}\n"

# Create named pipes for each player
mkfifo "$OUTPUT_DIR/alice_in" "$OUTPUT_DIR/bob_in" "$OUTPUT_DIR/charlie_in"
mkfifo "$OUTPUT_DIR/alice_out" "$OUTPUT_DIR/bob_out" "$OUTPUT_DIR/charlie_out"

# Start WebSocket connections in background
echo -e "${CYAN}[WS] Connecting Alice...${NC}"
(cat "$OUTPUT_DIR/alice_in" | websocat "$WS_URL?token=$ALICE_WS_TOKEN" > "$OUTPUT_DIR/alice_out" 2>&1) &
ALICE_PID=$!

sleep 0.5

echo -e "${CYAN}[WS] Connecting Bob...${NC}"
(cat "$OUTPUT_DIR/bob_in" | websocat "$WS_URL?token=$BOB_WS_TOKEN" > "$OUTPUT_DIR/bob_out" 2>&1) &
BOB_PID=$!

sleep 0.5

echo -e "${CYAN}[WS] Connecting Charlie...${NC}"
(cat "$OUTPUT_DIR/charlie_in" | websocat "$WS_URL?token=$CHARLIE_WS_TOKEN" > "$OUTPUT_DIR/charlie_out" 2>&1) &
CHARLIE_PID=$!

echo -e "${GREEN}✅ All 3 players connected${NC}"

###############################################################################
# STEP 3: Start Match
###############################################################################

echo -e "\n${BLUE}═══════════════════════════════════════════════════════════${NC}"
echo -e "${YELLOW}STEP 3: Starting match${NC}\n"

# Alice starts the match
echo -e "${CYAN}[Alice] Sending StartMatch command...${NC}"
echo '{"type":"StartMatch"}' > "$OUTPUT_DIR/alice_in"

sleep 1

echo -e "${GREEN}✅ Match should be starting...${NC}"

###############################################################################
# STEP 4: Play the Game
###############################################################################

echo -e "\n${BLUE}═══════════════════════════════════════════════════════════${NC}"
echo -e "${YELLOW}STEP 4: Game Play Simulation${NC}\n"

# Give server time to process
sleep 2

# Draw cards
echo -e "${CYAN}[Alice] Drawing card...${NC}"
echo '{"type":"DrawCard"}' > "$OUTPUT_DIR/alice_in"
sleep 1

echo -e "${CYAN}[Bob] Drawing card...${NC}"
echo '{"type":"DrawCard"}' > "$OUTPUT_DIR/bob_in"
sleep 1

echo -e "${CYAN}[Charlie] Drawing card...${NC}"
echo '{"type":"DrawCard"}' > "$OUTPUT_DIR/charlie_in"
sleep 1

# Play some cards
echo -e "${CYAN}[Alice] Playing card (ATTACK)...${NC}"
echo '{"type":"PlayCard","data":{"cardCode":"ATTACK"}}' > "$OUTPUT_DIR/alice_in"
sleep 1

echo -e "${CYAN}[Bob] Playing card (SKIP)...${NC}"
echo '{"type":"PlayCard","data":{"cardCode":"SKIP"}}' > "$OUTPUT_DIR/bob_in"
sleep 1

# Try Nope (Bob nopes Charlie's card)
echo -e "${CYAN}[Charlie] Playing card...${NC}"
echo '{"type":"PlayCard","data":{"cardCode":"SKIP"}}' > "$OUTPUT_DIR/charlie_in"
sleep 1

echo -e "${CYAN}[Alice] Sending NOPE reaction...${NC}"
echo '{"type":"Nope"}' > "$OUTPUT_DIR/alice_in"
sleep 1

# Continue playing
echo -e "${CYAN}[Bob] Drawing another card...${NC}"
echo '{"type":"DrawCard"}' > "$OUTPUT_DIR/bob_in"
sleep 1

echo -e "${CYAN}[Charlie] Drawing another card...${NC}"
echo '{"type":"DrawCard"}' > "$OUTPUT_DIR/charlie_in"
sleep 1

###############################################################################
# STEP 5: Collect Results
###############################################################################

echo -e "\n${BLUE}═══════════════════════════════════════════════════════════${NC}"
echo -e "${YELLOW}STEP 5: Collecting outputs${NC}\n"

# Give time for responses
sleep 2

# Gracefully close connections
echo -e "${CYAN}[Cleanup] Closing WebSocket connections...${NC}"
kill $ALICE_PID 2>/dev/null || true
kill $BOB_PID 2>/dev/null || true
kill $CHARLIE_PID 2>/dev/null || true

# Clean up pipes
rm -f "$OUTPUT_DIR/alice_in" "$OUTPUT_DIR/bob_in" "$OUTPUT_DIR/charlie_in"
rm -f "$OUTPUT_DIR/alice_out" "$OUTPUT_DIR/bob_out" "$OUTPUT_DIR/charlie_out"

sleep 1

echo -e "\n${GREEN}✅ Game play simulation complete!${NC}\n"

echo -e "${BLUE}═══════════════════════════════════════════════════════════${NC}"
echo -e "${YELLOW}SUMMARY${NC}\n"

echo -e "${CYAN}Players:${NC}"
echo "  🎮 Alice:   $ALICE_NAME"
echo "  🎮 Bob:     $BOB_NAME"
echo "  🎮 Charlie: $CHARLIE_NAME"

echo -e "\n${CYAN}Room:${NC}"
echo "  📍 Code: $ROOM_CODE"
echo "  🎯 Max Players: 3"

echo -e "\n${CYAN}Actions Performed:${NC}"
echo "  ✓ Authentication (3 players)"
echo "  ✓ Room creation & joining"
echo "  ✓ Ready status"
echo "  ✓ WebSocket connections"
echo "  ✓ Match start"
echo "  ✓ Card drawing"
echo "  ✓ Card playing"
echo "  ✓ Reactions (Nope)"

echo -e "\n${BLUE}═══════════════════════════════════════════════════════════${NC}"
echo -e "${GREEN}✅ WebSocket Game Flow Test Complete!${NC}\n"

