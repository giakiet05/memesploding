#!/bin/bash

###############################################################################
# Memesploding Gameplay End-to-End Test Suite
# 
# Full automation: login → create room → join → ready → start → play game
# Uses curl for REST API and websocat for WebSocket
###############################################################################

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
HTTP_BASE_URL="${HTTP_BASE_URL:-http://localhost:5217/api/v1}"
WS_BASE_URL="${WS_BASE_URL:-ws://localhost:5217/ws}"
OUTPUT_DIR="./output"
REPORT_FILE="$OUTPUT_DIR/gameplay-test-report.txt"

# Test data
DEVICE_ID_ALICE="gameplay_alice_$(date +%s)"
DEVICE_ID_BOB="gameplay_bob_$(date +%s)"
DEVICE_ID_CHARLIE="gameplay_charlie_$(date +%s)"

# Counters
PASSED=0
FAILED=0
TOTAL=0

# Create output directory
mkdir -p "$OUTPUT_DIR"

###############################################################################
# Helper Functions
###############################################################################

log_info() {
    echo -e "${BLUE}ℹ️  $1${NC}"
    echo "[INFO] $1" >> "$REPORT_FILE"
}

log_success() {
    echo -e "${GREEN}✅ $1${NC}"
    echo "[PASS] $1" >> "$REPORT_FILE"
    ((PASSED++))
}

log_error() {
    echo -e "${RED}❌ $1${NC}"
    echo "[FAIL] $1" >> "$REPORT_FILE"
    ((FAILED++))
}

log_test() {
    echo -e "${YELLOW}🧪 Testing: $1${NC}"
    echo "[TEST] $1" >> "$REPORT_FILE"
    ((TOTAL++))
}

test_curl() {
    local method=$1
    local endpoint=$2
    local token=$3
    local data=$4
    local name=$5
    
    log_test "$name"
    
    local cmd="curl -s -X $method '$HTTP_BASE_URL$endpoint'"
    
    if [ -n "$token" ]; then
        cmd="$cmd -H 'Authorization: Bearer $token'"
    fi
    
    if [ -n "$data" ]; then
        cmd="$cmd -H 'Content-Type: application/json' -d '$data'"
    fi
    
    local response=$(eval "$cmd")
    local status=$(eval "curl -s -o /dev/null -w '%{http_code}' -X $method '$HTTP_BASE_URL$endpoint' $([ -n "$token" ] && echo "-H 'Authorization: Bearer $token'") $([ -n "$data" ] && echo "-H 'Content-Type: application/json' -d '$data'")")
    
    if [ "$status" = "200" ] || [ "$status" = "201" ]; then
        log_success "$name"
        echo "$response"
    else
        log_error "$name (Status: $status)"
        echo "$response" >> "$REPORT_FILE"
        return 1
    fi
}

###############################################################################
# Main Test Flow
###############################################################################

echo ""
echo "========================================="
echo "  Memesploding Gameplay Test Suite"
echo "========================================="
echo ""
log_info "Server: $HTTP_BASE_URL"
log_info "Output: $OUTPUT_DIR"
echo "" >> "$REPORT_FILE"
echo "=========================================" >> "$REPORT_FILE"
echo "  Memesploding Gameplay Test Suite" >> "$REPORT_FILE"
echo "=========================================" >> "$REPORT_FILE"
echo "" >> "$REPORT_FILE"

###############################################################################
# PHASE 1: Authentication
###############################################################################

echo ""
log_info "PHASE 1: Authentication"

# Alice Login
log_test "Alice Login"
ALICE_RESPONSE=$(curl -s -X POST "$HTTP_BASE_URL/auth/guest" \
    -H 'Content-Type: application/json' \
    -d "{\"deviceId\": \"$DEVICE_ID_ALICE\"}")

ALICE_TOKEN=$(echo "$ALICE_RESPONSE" | jq -r '.data.accessToken // empty')
ALICE_ID=$(echo "$ALICE_RESPONSE" | jq -r '.data.user.id // empty')

if [ -n "$ALICE_TOKEN" ] && [ -n "$ALICE_ID" ]; then
    log_success "Alice Login (Token: ${ALICE_TOKEN:0:20}...)"
else
    log_error "Alice Login"
    echo "$ALICE_RESPONSE" >> "$REPORT_FILE"
    exit 1
fi

# Bob Login
log_test "Bob Login"
BOB_RESPONSE=$(curl -s -X POST "$HTTP_BASE_URL/auth/guest" \
    -H 'Content-Type: application/json' \
    -d "{\"deviceId\": \"$DEVICE_ID_BOB\"}")

BOB_TOKEN=$(echo "$BOB_RESPONSE" | jq -r '.data.accessToken // empty')
BOB_ID=$(echo "$BOB_RESPONSE" | jq -r '.data.user.id // empty')

if [ -n "$BOB_TOKEN" ] && [ -n "$BOB_ID" ]; then
    log_success "Bob Login"
else
    log_error "Bob Login"
    echo "$BOB_RESPONSE" >> "$REPORT_FILE"
    exit 1
fi

# Charlie Login
log_test "Charlie Login"
CHARLIE_RESPONSE=$(curl -s -X POST "$HTTP_BASE_URL/auth/guest" \
    -H 'Content-Type: application/json' \
    -d "{\"deviceId\": \"$DEVICE_ID_CHARLIE\"}")

CHARLIE_TOKEN=$(echo "$CHARLIE_RESPONSE" | jq -r '.data.accessToken // empty')
CHARLIE_ID=$(echo "$CHARLIE_RESPONSE" | jq -r '.data.user.id // empty')

if [ -n "$CHARLIE_TOKEN" ] && [ -n "$CHARLIE_ID" ]; then
    log_success "Charlie Login"
else
    log_error "Charlie Login"
    echo "$CHARLIE_RESPONSE" >> "$REPORT_FILE"
    exit 1
fi

###############################################################################
# PHASE 2: Room Setup
###############################################################################

echo ""
log_info "PHASE 2: Room Setup"

# Load CardSets
log_test "Load Card Sets"
CARDSET_RESPONSE=$(curl -s -X GET "$HTTP_BASE_URL/card-sets" \
    -H "Authorization: Bearer $ALICE_TOKEN")

CARDSET_ID=$(echo "$CARDSET_RESPONSE" | jq -r '.data.items[0].id // empty')
CARDSET_NAME=$(echo "$CARDSET_RESPONSE" | jq -r '.data.items[0].name // empty')

if [ -n "$CARDSET_ID" ]; then
    log_success "Load Card Sets (CardSet: $CARDSET_NAME)"
else
    log_error "Load Card Sets"
    echo "$CARDSET_RESPONSE" >> "$REPORT_FILE"
    exit 1
fi

# Create Room
log_test "Create Room"
ROOM_RESPONSE=$(curl -s -X POST "$HTTP_BASE_URL/rooms" \
    -H "Authorization: Bearer $ALICE_TOKEN" \
    -H 'Content-Type: application/json' \
    -d "{\"isPublic\": true, \"maxPlayers\": 4, \"cardSetIds\": [\"$CARDSET_ID\"]}")

ROOM_CODE=$(echo "$ROOM_RESPONSE" | jq -r '.data.code // empty')
ROOM_ID=$(echo "$ROOM_RESPONSE" | jq -r '.data.id // empty')

if [ -n "$ROOM_CODE" ]; then
    log_success "Create Room (Code: $ROOM_CODE)"
    echo "[DEBUG] Room Code: $ROOM_CODE" >> "$REPORT_FILE"
    echo "[DEBUG] Full Response: $ROOM_RESPONSE" >> "$REPORT_FILE"
    sleep 1  # Wait for Redis cache to sync
else
    log_error "Create Room"
    echo "$ROOM_RESPONSE" >> "$REPORT_FILE"
    exit 1
fi

# Bob Join
log_test "Bob Join Room"
BOB_JOIN_RESPONSE=$(curl -s -X POST "$HTTP_BASE_URL/rooms/$ROOM_CODE/join" \
    -H "Authorization: Bearer $BOB_TOKEN")

if echo "$BOB_JOIN_RESPONSE" | jq -e '.data' > /dev/null 2>&1; then
    log_success "Bob Join Room"
else
    log_error "Bob Join Room"
    echo "$BOB_JOIN_RESPONSE" >> "$REPORT_FILE"
    exit 1
fi

# Charlie Join
log_test "Charlie Join Room"
CHARLIE_JOIN_RESPONSE=$(curl -s -X POST "$HTTP_BASE_URL/rooms/$ROOM_CODE/join" \
    -H "Authorization: Bearer $CHARLIE_TOKEN")

if echo "$CHARLIE_JOIN_RESPONSE" | jq -e '.data' > /dev/null 2>&1; then
    log_success "Charlie Join Room"
else
    log_error "Charlie Join Room"
    echo "$CHARLIE_JOIN_RESPONSE" >> "$REPORT_FILE"
    exit 1
fi

###############################################################################
# PHASE 3: Match Preparation
###############################################################################

echo ""
log_info "PHASE 3: Match Preparation"

# Alice Ready
log_test "Alice Set Ready"
ALICE_READY_RESPONSE=$(curl -s -X PATCH "$HTTP_BASE_URL/rooms/$ROOM_CODE/ready?isReady=true" \
    -H "Authorization: Bearer $ALICE_TOKEN")

if echo "$ALICE_READY_RESPONSE" | jq -e '.data' > /dev/null 2>&1; then
    log_success "Alice Set Ready"
else
    log_error "Alice Set Ready"
    echo "$ALICE_READY_RESPONSE" >> "$REPORT_FILE"
fi

# Bob Ready
log_test "Bob Set Ready"
BOB_READY_RESPONSE=$(curl -s -X PATCH "$HTTP_BASE_URL/rooms/$ROOM_CODE/ready?isReady=true" \
    -H "Authorization: Bearer $BOB_TOKEN")

if echo "$BOB_READY_RESPONSE" | jq -e '.data' > /dev/null 2>&1; then
    log_success "Bob Set Ready"
else
    log_error "Bob Set Ready"
    echo "$BOB_READY_RESPONSE" >> "$REPORT_FILE"
fi

# Charlie Ready
log_test "Charlie Set Ready"
CHARLIE_READY_RESPONSE=$(curl -s -X PATCH "$HTTP_BASE_URL/rooms/$ROOM_CODE/ready?isReady=true" \
    -H "Authorization: Bearer $CHARLIE_TOKEN")

if echo "$CHARLIE_READY_RESPONSE" | jq -e '.data' > /dev/null 2>&1; then
    log_success "Charlie Set Ready"
else
    log_error "Charlie Set Ready"
    echo "$CHARLIE_READY_RESPONSE" >> "$REPORT_FILE"
fi

# Start Match
log_test "Start Match"
START_RESPONSE=$(curl -s -X POST "$HTTP_BASE_URL/rooms/$ROOM_CODE/start" \
    -H "Authorization: Bearer $ALICE_TOKEN")

if echo "$START_RESPONSE" | jq -e '.data' > /dev/null 2>&1; then
    log_success "Start Match"
else
    log_error "Start Match"
    echo "$START_RESPONSE" >> "$REPORT_FILE"
    exit 1
fi

###############################################################################
# PHASE 4: Get Game Tickets
###############################################################################

echo ""
log_info "PHASE 4: Get Game Tickets"

# Wait for match to start and cache to sync
sleep 2

# Alice Ticket
log_test "Get Alice Game Ticket"
echo "[DEBUG] Fetching room: $HTTP_BASE_URL/rooms/$ROOM_CODE" >> "$REPORT_FILE"
ALICE_TICKET_RESPONSE=$(curl -s -X GET "$HTTP_BASE_URL/rooms/$ROOM_CODE" \
    -H "Authorization: Bearer $ALICE_TOKEN")

echo "[DEBUG] Get ticket response: $ALICE_TICKET_RESPONSE" >> "$REPORT_FILE"
ALICE_GAME_TICKET=$(echo "$ALICE_TICKET_RESPONSE" | jq -r '.data.gameTicket // empty')

if [ -n "$ALICE_GAME_TICKET" ]; then
    log_success "Get Alice Game Ticket"
    ALICE_WS_URL="$WS_BASE_URL?access_token=$ALICE_GAME_TICKET"
else
    log_error "Get Alice Game Ticket"
    echo "$ALICE_TICKET_RESPONSE" >> "$REPORT_FILE"
    exit 1
fi

# Bob Ticket
log_test "Get Bob Game Ticket"
BOB_TICKET_RESPONSE=$(curl -s -X GET "$HTTP_BASE_URL/rooms/$ROOM_CODE" \
    -H "Authorization: Bearer $BOB_TOKEN")

BOB_GAME_TICKET=$(echo "$BOB_TICKET_RESPONSE" | jq -r '.data.gameTicket // empty')

if [ -n "$BOB_GAME_TICKET" ]; then
    log_success "Get Bob Game Ticket"
    BOB_WS_URL="$WS_BASE_URL?access_token=$BOB_GAME_TICKET"
else
    log_error "Get Bob Game Ticket"
    echo "$BOB_TICKET_RESPONSE" >> "$REPORT_FILE"
    exit 1
fi

# Charlie Ticket
log_test "Get Charlie Game Ticket"
CHARLIE_TICKET_RESPONSE=$(curl -s -X GET "$HTTP_BASE_URL/rooms/$ROOM_CODE" \
    -H "Authorization: Bearer $CHARLIE_TOKEN")

CHARLIE_GAME_TICKET=$(echo "$CHARLIE_TICKET_RESPONSE" | jq -r '.data.gameTicket // empty')

if [ -n "$CHARLIE_GAME_TICKET" ]; then
    log_success "Get Charlie Game Ticket"
    CHARLIE_WS_URL="$WS_BASE_URL?access_token=$CHARLIE_GAME_TICKET"
else
    log_error "Get Charlie Game Ticket"
    echo "$CHARLIE_TICKET_RESPONSE" >> "$REPORT_FILE"
    exit 1
fi

###############################################################################
# PHASE 5: WebSocket Game (Optional - requires websocat)
###############################################################################

echo ""
log_info "PHASE 5: WebSocket Game (Optional)"

# Check if websocat is available
if ! command -v websocat &> /dev/null; then
    log_error "websocat not found - skipping WebSocket tests"
    echo "Install with: sudo snap install websocat"
else
    log_info "websocat found, testing WebSocket connections..."
    
    # Alice Connect & Handshake
    log_test "Alice WebSocket Handshake"
    ALICE_HS=$(echo '{"protocol":"json","version":1}' | timeout 5 websocat "$ALICE_WS_URL" 2>/dev/null || echo "")
    if [ -n "$ALICE_HS" ]; then
        log_success "Alice WebSocket Handshake"
    else
        log_error "Alice WebSocket Handshake (timeout or connection error)"
    fi
    
    # Bob Connect & Handshake
    log_test "Bob WebSocket Handshake"
    BOB_HS=$(echo '{"protocol":"json","version":1}' | timeout 5 websocat "$BOB_WS_URL" 2>/dev/null || echo "")
    if [ -n "$BOB_HS" ]; then
        log_success "Bob WebSocket Handshake"
    else
        log_error "Bob WebSocket Handshake (timeout or connection error)"
    fi
    
    # Charlie Connect & Handshake
    log_test "Charlie WebSocket Handshake"
    CHARLIE_HS=$(echo '{"protocol":"json","version":1}' | timeout 5 websocat "$CHARLIE_WS_URL" 2>/dev/null || echo "")
    if [ -n "$CHARLIE_HS" ]; then
        log_success "Charlie WebSocket Handshake"
    else
        log_error "Charlie WebSocket Handshake (timeout or connection error)"
    fi
fi

###############################################################################
# Cleanup (Optional)
###############################################################################

echo ""
log_info "Cleanup (if needed)"

# Save test URLs for manual testing
cat > "$OUTPUT_DIR/game-urls.txt" << EOF
===========================================
   Generated Game URLs
===========================================

Room Code: $ROOM_CODE
Alice ID: $ALICE_ID
Bob ID: $BOB_ID
Charlie ID: $CHARLIE_ID

Alice WebSocket URL:
$ALICE_WS_URL

Bob WebSocket URL:
$BOB_WS_URL

Charlie WebSocket URL:
$CHARLIE_WS_URL

===========================================

Use these URLs with wscat or WebSocketKing to manually test game commands.
See GameplayCommands.md for available commands.

EOF

log_success "Saved game URLs to $OUTPUT_DIR/game-urls.txt"

###############################################################################
# Summary
###############################################################################

echo ""
echo "========================================="
echo "           TEST SUMMARY"
echo "========================================="
echo ""
log_info "Total Tests:  $TOTAL"
log_info "Passed:       $PASSED"
log_info "Failed:       $FAILED"

if [ $FAILED -eq 0 ]; then
    echo -e "${GREEN}Result: ✅ ALL TESTS PASSED${NC}"
    echo "[SUMMARY] Result: ALL TESTS PASSED" >> "$REPORT_FILE"
else
    echo -e "${RED}Result: ❌ SOME TESTS FAILED${NC}"
    echo "[SUMMARY] Result: SOME TESTS FAILED" >> "$REPORT_FILE"
fi

echo ""
log_info "Report saved to: $REPORT_FILE"
echo ""

exit $FAILED
