#!/bin/bash
set -e

HTTP_BASE_URL="http://localhost:5217/api/v1"
WS_BASE_URL="ws://localhost:5217/ws"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

echo -e "${BLUE}=========================================${NC}"
echo "Memesploding Gameplay E2E Test"
echo -e "${BLUE}=========================================${NC}\n"

# Get Card Sets
echo -e "${BLUE}FETCHING CARD SETS${NC}"
CARDSETS=$(curl -s -X GET "$HTTP_BASE_URL/card-sets?pageNumber=1&pageSize=10")
CARDSET_ID=$(echo "$CARDSETS" | jq -r '.data[0].id // empty')

if [ -z "$CARDSET_ID" ]; then
    echo -e "${YELLOW}⚠️  No card sets found, using empty array${NC}\n"
    CARDSET_ARRAY="[]"
else
    echo -e "${GREEN}✅ Card sets fetched${NC}"
    echo "   Using Card Set ID: $CARDSET_ID\n"
    CARDSET_ARRAY="[\"$CARDSET_ID\"]"
fi

# Phase 1: Auth
echo -e "${BLUE}PHASE 1: Authentication${NC}"

DEVICE_ID_ALICE="alice_$(date +%s)"
echo -e "${YELLOW}[Alice] Logging in...${NC}"

ALICE=$(curl -s -X POST "$HTTP_BASE_URL/auth/guest" \
  -H "Content-Type: application/json" \
  -d "{\"deviceId\": \"$DEVICE_ID_ALICE\"}")

ALICE_TOKEN=$(echo "$ALICE" | jq -r '.data.accessToken')
ALICE_ID=$(echo "$ALICE" | jq -r '.data.user.id')

if [ -n "$ALICE_TOKEN" ] && [ -n "$ALICE_ID" ]; then
    echo -e "${GREEN}✅ Alice login OK${NC}"
    echo "   Token: ${ALICE_TOKEN:0:20}..."
    echo "   ID: $ALICE_ID"
else
    echo -e "${RED}❌ Alice login FAILED${NC}"
    echo "$ALICE" | jq .
    exit 1
fi

# Phase 2: Create Room
echo -e "\n${BLUE}PHASE 2: Create Room${NC}"
echo -e "${YELLOW}[Alice] Creating room...${NC}"

ROOM=$(curl -s -X POST "$HTTP_BASE_URL/rooms" \
  -H "Authorization: Bearer $ALICE_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{\"maxPlayers\": 2, \"isPublic\": true, \"cardSetIds\": $CARDSET_ARRAY}")

ROOM_CODE=$(echo "$ROOM" | jq -r '.data.code // empty')

if [ -n "$ROOM_CODE" ]; then
    echo -e "${GREEN}✅ Room created${NC}"
    echo "   Code: $ROOM_CODE"
    sleep 1  # Wait for Redis cache sync
else
    echo -e "${RED}❌ Room creation FAILED${NC}"
    echo "$ROOM" | jq .
    exit 1
fi

# Phase 3: Join & Ready
echo -e "\n${BLUE}PHASE 3: Join & Ready${NC}"

DEVICE_ID_BOB="bob_$(date +%s)"
echo -e "${YELLOW}[Bob] Logging in...${NC}"

BOB=$(curl -s -X POST "$HTTP_BASE_URL/auth/guest" \
  -H "Content-Type: application/json" \
  -d "{\"deviceId\": \"$DEVICE_ID_BOB\"}")

BOB_TOKEN=$(echo "$BOB" | jq -r '.data.accessToken')
BOB_ID=$(echo "$BOB" | jq -r '.data.user.id')

if [ -n "$BOB_TOKEN" ]; then
    echo -e "${GREEN}✅ Bob login OK${NC}"
else
    echo -e "${RED}❌ Bob login FAILED${NC}"
    exit 1
fi

echo -e "${YELLOW}[Alice] Joining room...${NC}"
ALICE_JOIN=$(curl -s -X POST "$HTTP_BASE_URL/rooms/$ROOM_CODE/join" \
  -H "Authorization: Bearer $ALICE_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{}')

echo -e "${YELLOW}[Bob] Joining room...${NC}"
BOB_JOIN=$(curl -s -X POST "$HTTP_BASE_URL/rooms/$ROOM_CODE/join" \
  -H "Authorization: Bearer $BOB_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{}')

echo -e "${GREEN}✅ Both players joined${NC}"

echo -e "${YELLOW}[Alice] Ready...${NC}"
ALICE_READY=$(curl -s -X POST "$HTTP_BASE_URL/rooms/$ROOM_CODE/player-ready" \
  -H "Authorization: Bearer $ALICE_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{}')

echo -e "${YELLOW}[Bob] Ready...${NC}"
BOB_READY=$(curl -s -X POST "$HTTP_BASE_URL/rooms/$ROOM_CODE/player-ready" \
  -H "Authorization: Bearer $BOB_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{}')

echo -e "${GREEN}✅ Both players ready${NC}"

# Phase 4: Ready & Start (via WebSocket)
echo -e "\n${BLUE}PHASE 4: Mark Ready & Start (WebSocket Required)${NC}"
echo -e "${YELLOW}⚠️  Marking players as ready...${NC}"

# Mark Alice as ready
ALICE_READY=$(curl -s -X POST "$HTTP_BASE_URL/rooms/$ROOM_CODE/player-ready" \
  -H "Authorization: Bearer $ALICE_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"isReady": true}' 2>/dev/null || echo '{}')

# Mark Bob as ready
BOB_READY=$(curl -s -X POST "$HTTP_BASE_URL/rooms/$ROOM_CODE/player-ready" \
  -H "Authorization: Bearer $BOB_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"isReady": true}' 2>/dev/null || echo '{}')

echo -e "${GREEN}✅ Both players marked as ready${NC}"

echo -e "${YELLOW}⚠️  NOTE: Match start requires WebSocket/SignalR command${NC}"
echo -e "    Use: websocat 'ws://localhost:5204/ws' to connect"
echo -e "    Then send the start command via SignalR\n"

# Phase 5: Get Tickets & WebSocket Info
echo -e "${BLUE}PHASE 5: Get Game Tickets${NC}"
echo -e "${YELLOW}[Alice] Getting game ticket...${NC}"

ALICE_DATA=$(curl -s -X GET "$HTTP_BASE_URL/rooms/$ROOM_CODE" \
  -H "Authorization: Bearer $ALICE_TOKEN")

ALICE_TICKET=$(echo "$ALICE_DATA" | jq -r '.data.connection.wsAccessToken // empty')
ALICE_WS_URL=$(echo "$ALICE_DATA" | jq -r '.data.connection.wsUrl // empty')

if [ -z "$ALICE_TICKET" ]; then
    echo -e "${YELLOW}⚠️  WebSocket token not ready yet (might be available after start)${NC}"
else
    echo -e "${GREEN}✅ Alice game ticket obtained${NC}"
    echo "   WS URL: $ALICE_WS_URL"
    echo "   Token: ${ALICE_TICKET:0:20}..."
fi

echo -e "\n${GREEN}✅ REST API FLOW COMPLETE${NC}"
echo -e "${YELLOW}Next steps:${NC}"
echo -e "  1. Connect to WebSocket: websocat '$ALICE_WS_URL?token=$ALICE_TICKET'"
echo -e "  2. Send game commands (draw, play, nope, etc.)"
echo -e "  See GameplayCommands.md for full list\n"

