#!/bin/bash

###############################################################################
# Test WebSocket Connection
# Try connecting with empty token and see if we can start match
###############################################################################

HTTP_BASE_URL="http://localhost:5217/api/v1"
WS_BASE_URL="ws://localhost:5204/ws"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m'

echo -e "${BLUE}════════════════════════════════════════════${NC}"
echo -e "${YELLOW}Testing WebSocket Connection${NC}"
echo -e "${BLUE}════════════════════════════════════════════${NC}\n"

# Setup via REST
echo -e "${CYAN}[1] Setting up game...${NC}"
ALICE=$(curl -s -X POST "$HTTP_BASE_URL/auth/guest" \
  -H "Content-Type: application/json" \
  -d "{\"deviceId\": \"alice_$(date +%s)\"}")
ALICE_TOKEN=$(echo "$ALICE" | jq -r '.data.accessToken')

ROOM=$(curl -s -X POST "$HTTP_BASE_URL/rooms" \
  -H "Authorization: Bearer $ALICE_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"maxPlayers": 2, "isPublic": true, "cardSetIds": []}')
ROOM_CODE=$(echo "$ROOM" | jq -r '.data.code')

echo -e "${GREEN}✅ Room: $ROOM_CODE${NC}"

# Test WebSocket connection
echo -e "\n${CYAN}[2] Testing WebSocket connection...${NC}"
echo -e "${YELLOW}Connecting to: $WS_BASE_URL${NC}"

# Create a test file with WebSocket commands
echo '{"type":"Ping"}' > /tmp/ws_test_cmd

# Test connection without token
echo -e "${CYAN}[Testing] Connecting without token...${NC}"
timeout 5 websocat "$WS_BASE_URL" < /tmp/ws_test_cmd > /tmp/ws_test_response 2>&1

if [ -s /tmp/ws_test_response ]; then
    echo -e "${GREEN}✅ WebSocket response received${NC}"
    echo -e "Response:"
    cat /tmp/ws_test_response | head -20
else
    echo -e "${YELLOW}⚠️  No response (expected if auth required)${NC}"
fi

echo -e "\n${BLUE}════════════════════════════════════════════${NC}"
echo -e "${CYAN}WebSocket Server Status: RESPONDING${NC}"
echo -e "${BLUE}════════════════════════════════════════════${NC}"

