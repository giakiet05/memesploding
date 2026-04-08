#!/bin/bash
# Auto-generated integration test suite based on actual controller code
# Generated: 2026-04-06
# Total endpoints: 31

# Don't exit on errors - we want to run all tests
set +e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

# Configuration
BASE_URL=${BASE_URL:-http://localhost:5217/api/v1}
OUTPUT_DIR=./output
REPORT_FILE=$OUTPUT_DIR/test-report.txt

# Test counters
TOTAL_TESTS=0
PASSED_TESTS=0
FAILED_TESTS=0

# Create output directory
mkdir -p $OUTPUT_DIR
: > $REPORT_FILE  # Clear report file

# Logging functions
log_info() {
    echo -e "${BLUE}ℹ️  $1${NC}" >&2
    echo "[INFO] $1" >> "$REPORT_FILE"
}

log_success() {
    echo -e "${GREEN}✅ $1${NC}" >&2
    echo "[PASS] $1" >> "$REPORT_FILE"
    ((PASSED_TESTS++))
}

log_error() {
    echo -e "${RED}❌ $1${NC}" >&2
    echo "[FAIL] $1" >> "$REPORT_FILE"
    ((FAILED_TESTS++))
}

log_warning() {
    echo -e "${YELLOW}⚠️  $1${NC}" >&2
    echo "[WARN] $1" >> "$REPORT_FILE"
}

# Test endpoint function
test_endpoint() {
    local test_name="$1"
    local method="$2"
    local endpoint="$3"
    local token="$4"
    local data="$5"
    
    ((TOTAL_TESTS++))
    log_info "[$TOTAL_TESTS] Testing: $test_name" >&2
    
    local curl_cmd="curl -s -X $method \"$BASE_URL$endpoint\""
    
    if [ -n "$token" ]; then
        curl_cmd="$curl_cmd -H \"Authorization: Bearer $token\""
    fi
    
    if [ -n "$data" ]; then
        curl_cmd="$curl_cmd -H \"Content-Type: application/json\" -d '$data'"
    fi
    
    local response
    response=$(eval "$curl_cmd")
    
    # Check response type:
    # 1. Success: has .data field
    # 2. Validation error: has .errors field (ASP.NET validation)
    # 3. API error: has .errorCode field (custom API errors)
    if echo "$response" | jq -e '.data' > /dev/null 2>&1; then
        log_success "$test_name" >&2
        echo "$response"  # Only JSON to stdout
        return 0
    elif echo "$response" | jq -e '.errorCode' > /dev/null 2>&1; then
        # API error response (e.g., ALREADY_FRIENDS, NOT_FOUND)
        log_error "$test_name - API Error: $(echo "$response" | jq -r '.errorCode')" >&2
        echo "$response" | jq '.' >&2
        echo "$response"
        return 1
    elif echo "$response" | jq -e '.errors' > /dev/null 2>&1; then
        # Validation errors (ASP.NET model validation)
        log_error "$test_name - Validation errors" >&2
        echo "$response" | jq '.' >&2
        echo "$response"
        return 1
    else
        log_error "$test_name - Unexpected response" >&2
        echo "$response" >&2
        echo "{}"
        return 1
    fi
}

# Start tests
echo "=========================================="
echo "  Memesploding Integration Test Suite v2"
echo "  (Auto-generated from controller code)"
echo "=========================================="
echo ""
log_info "Server: $BASE_URL"
log_info "Output: $OUTPUT_DIR"
echo ""

### ============================================
### PHASE 1: AUTHENTICATION
### ============================================

log_info "PHASE 1: Authentication"

# Generate unique device IDs based on timestamp
TIMESTAMP=$(date +%s%N)

# 1.1 Alice - Guest Register
RESPONSE=$(test_endpoint "Alice Guest Register" "POST" "/auth/guest" "" "{
  \"DeviceId\": \"alice_${TIMESTAMP}\"
}")

ALICE_TOKEN=$(echo "$RESPONSE" | jq -r '.data.accessToken // empty')
ALICE_USER_ID=$(echo "$RESPONSE" | jq -r '.data.user.id // empty')

if [ -z "$ALICE_TOKEN" ]; then
    log_error "Failed to extract Alice token"
    exit 1
fi

log_info "Alice Token: ${ALICE_TOKEN:0:30}..."
log_info "Alice User ID: $ALICE_USER_ID"

# 1.2 Bob - Guest Register
RESPONSE=$(test_endpoint "Bob Guest Register" "POST" "/auth/guest" "" "{
  \"DeviceId\": \"bob_${TIMESTAMP}\"
}")

BOB_TOKEN=$(echo "$RESPONSE" | jq -r '.data.accessToken // empty')
BOB_USER_ID=$(echo "$RESPONSE" | jq -r '.data.user.id // empty')

if [ -z "$BOB_TOKEN" ]; then
    log_error "Failed to extract Bob token"
    exit 1
fi

log_info "Bob Token: ${BOB_TOKEN:0:30}..."
log_info "Bob User ID: $BOB_USER_ID"

# 1.3 Charlie - Guest Register
RESPONSE=$(test_endpoint "Charlie Guest Register" "POST" "/auth/guest" "" "{
  \"DeviceId\": \"charlie_${TIMESTAMP}\"
}")

CHARLIE_TOKEN=$(echo "$RESPONSE" | jq -r '.data.accessToken // empty')
CHARLIE_USER_ID=$(echo "$RESPONSE" | jq -r '.data.user.id // empty')

if [ -z "$CHARLIE_TOKEN" ]; then
    log_warning "Failed to extract Charlie token"
fi

### ============================================
### PHASE 2: USER PROFILES
### ============================================

log_info "PHASE 2: User Profiles"

# 2.1 Alice - View own profile
test_endpoint "Alice View Own Profile" "GET" "/users/me/profile" "$ALICE_TOKEN" > /dev/null

# 2.2 Alice - Update profile
test_endpoint "Alice Update Profile" "PATCH" "/users/me/profile" "$ALICE_TOKEN" '{
  "Username": "alice_updated",
  "Bio": "Integration test user"
}' > /dev/null

# 2.3 Alice - View stats
test_endpoint "Alice View Stats" "GET" "/users/me/stats" "$ALICE_TOKEN" > /dev/null

# 2.4 Bob - View Alice profile
test_endpoint "Bob View Alice Profile" "GET" "/users/$ALICE_USER_ID" "$BOB_TOKEN" > /dev/null

# 2.5 Search users
test_endpoint "Alice Search Users" "GET" "/users?SearchQuery=alice&Pagination.Page=1&Pagination.PageSize=10" "$ALICE_TOKEN" > /dev/null

# 2.6 View leaderboard (public)
test_endpoint "View Leaderboard" "GET" "/users/leaderboard?Page=1&PageSize=20" "" > /dev/null

### ============================================
### PHASE 3: FRIENDSHIPS
### ============================================

log_info "PHASE 3: Friendships"

# 3.1 Alice sends friend request to Bob
RESPONSE=$(test_endpoint "Alice Send Friend Request" "POST" "/me/friends/invitations" "$ALICE_TOKEN" "{
  \"UserId\": \"$BOB_USER_ID\"
}")

# 3.2 Bob views pending invitations
test_endpoint "Bob View Friend Invitations" "GET" "/me/friends?Status=Pending&Page=1&PageSize=10" "$BOB_TOKEN" > /dev/null

# 3.3 Bob accepts Alice's request
test_endpoint "Bob Accept Friend Request" "PATCH" "/me/friends/invitations/$ALICE_USER_ID" "$BOB_TOKEN" '{
  "Accept": true
}' > /dev/null

# 3.4 Alice views friends list
test_endpoint "Alice View Friends" "GET" "/me/friends?Status=Accepted&Page=1&PageSize=10" "$ALICE_TOKEN" > /dev/null

# 3.5 Error case: Duplicate friend request
test_endpoint "Alice Duplicate Friend Request (Error)" "POST" "/me/friends/invitations" "$ALICE_TOKEN" "{
  \"UserId\": \"$BOB_USER_ID\"
}" > /dev/null || true  # Expected to fail

### ============================================
### PHASE 4: CARD SETS
### ============================================

log_info "PHASE 4: Card Sets"

# 4.1 List card sets
RESPONSE=$(test_endpoint "List Card Sets" "GET" "/card-sets?Page=1&PageSize=10" "$ALICE_TOKEN")

CARDSET_ID=$(echo "$RESPONSE" | jq -r '.data.items[0].id // empty')

if [ -n "$CARDSET_ID" ]; then
    log_info "Found CardSet ID: $CARDSET_ID"
    
    # 4.2 Get cards in set
    test_endpoint "Get Cards in Set" "GET" "/card-sets/$CARDSET_ID/cards?Page=1&PageSize=20" "$ALICE_TOKEN" > /dev/null
else
    log_warning "No card sets found"
fi

### ============================================
### PHASE 5: ROOMS
### ============================================

log_info "PHASE 5: Rooms"

# 5.1 Alice creates room
if [ -n "$CARDSET_ID" ]; then
    RESPONSE=$(test_endpoint "Alice Create Room" "POST" "/rooms" "$ALICE_TOKEN" "{
      \"MaxPlayers\": 4,
      \"IsPublic\": true,
      \"CardSetIds\": [\"$CARDSET_ID\"]
    }")
    
    ROOM_CODE=$(echo "$RESPONSE" | jq -r '.data.code // empty')
    log_info "Room Code: $ROOM_CODE"
else
    log_warning "Skipping room creation - no card sets"
    ROOM_CODE=""
fi

# 5.2 List public rooms
test_endpoint "List Public Rooms" "GET" "/rooms?IsPublic=true&Page=1&PageSize=10" "$BOB_TOKEN" > /dev/null

# 5.3 Bob gets room details
if [ -n "$ROOM_CODE" ]; then
    test_endpoint "Bob Get Room Details" "GET" "/rooms/$ROOM_CODE" "$BOB_TOKEN" > /dev/null
    
    # 5.4 Error case: Invalid room code
    test_endpoint "Get Invalid Room (Error)" "GET" "/rooms/INVALID" "$BOB_TOKEN" > /dev/null || true
fi

### ============================================
### PHASE 6: NOTIFICATIONS
### ============================================

log_info "PHASE 6: Notifications"

# 6.1 Bob lists notifications
RESPONSE=$(test_endpoint "Bob List Notifications" "GET" "/me/notifications?Page=1&PageSize=10" "$BOB_TOKEN")

NOTIFICATION_ID=$(echo "$RESPONSE" | jq -r '.data.items[0].id // empty')

# 6.2 Bob checks unread count
test_endpoint "Bob Unread Count" "GET" "/me/notifications/unread-count" "$BOB_TOKEN" > /dev/null

if [ -n "$NOTIFICATION_ID" ]; then
    # 6.3 Mark one as read
    test_endpoint "Bob Mark Notification Read" "PATCH" "/notifications/$NOTIFICATION_ID" "$BOB_TOKEN" > /dev/null
    
    # 6.4 Delete notification
    test_endpoint "Bob Delete Notification" "DELETE" "/notifications/$NOTIFICATION_ID" "$BOB_TOKEN" > /dev/null
fi

# 6.5 Mark all as read
test_endpoint "Bob Mark All Read" "PATCH" "/notifications/mark-all-read" "$BOB_TOKEN" > /dev/null

# 6.6 Clear all notifications
test_endpoint "Bob Clear All Notifications" "DELETE" "/notifications/clear-all" "$BOB_TOKEN" > /dev/null

### ============================================
### PHASE 7: MATCHMAKING
### ============================================

log_info "PHASE 7: Matchmaking"

# 7.1 Charlie - Quick play
test_endpoint "Charlie Quick Play" "POST" "/matchmaking/quick-play" "$CHARLIE_TOKEN" > /dev/null

### ============================================
### PHASE 8: MATCH HISTORY (Optional - requires data)
### ============================================

log_info "PHASE 8: Match History"

# 8.1 Alice - View own match history
RESPONSE=$(test_endpoint "Alice Match History" "GET" "/me/match-history?Page=1&PageSize=5" "$ALICE_TOKEN")

MATCH_ID=$(echo "$RESPONSE" | jq -r '.data.items[0].matchId // empty')

# 8.2 Bob - View Alice's match history
test_endpoint "Bob View Alice Match History" "GET" "/users/$ALICE_USER_ID/match-history?Page=1&PageSize=5" "$BOB_TOKEN" > /dev/null

if [ -n "$MATCH_ID" ]; then
    # 8.3 Get match details
    test_endpoint "Alice Get Match Details" "GET" "/matches/$MATCH_ID" "$ALICE_TOKEN" > /dev/null
else
    log_warning "No match history found"
fi

### ============================================
### SUMMARY
### ============================================

echo ""
echo "=========================================="
echo "  Test Summary"
echo "=========================================="
log_info "Total Tests: $TOTAL_TESTS"
log_info "Passed: ${GREEN}$PASSED_TESTS${NC}"
log_info "Failed: ${RED}$FAILED_TESTS${NC}"
echo ""

if [ $FAILED_TESTS -eq 0 ]; then
    log_success "All tests passed!"
    echo "Report: $REPORT_FILE"
    exit 0
else
    log_error "$FAILED_TESTS test(s) failed"
    echo "Report: $REPORT_FILE"
    exit 1
fi
