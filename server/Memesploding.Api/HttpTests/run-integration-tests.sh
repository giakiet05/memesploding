#!/bin/bash

###
### Memesploding API - Automated Integration Test Runner
### Converts .http requests to curl commands and runs full test suite
###
### Requirements:
### - curl (HTTP client)
### - jq (JSON processor: apt install jq)
### - Server running on localhost:5217
###
### Usage:
### ./run-integration-tests.sh
###

set -e  # Exit on error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
BASE_URL="http://localhost:5217/api/v1"
OUTPUT_DIR="./output"
REPORT_FILE="$OUTPUT_DIR/test-report.txt"

# Counters
TOTAL_TESTS=0
PASSED_TESTS=0
FAILED_TESTS=0

# Create output directory
mkdir -p "$OUTPUT_DIR"

# Clear previous report
> "$REPORT_FILE"

# Helper functions
log_info() {
    echo -e "${BLUE}ℹ️  $1${NC}"
    echo "[INFO] $1" >> "$REPORT_FILE"
}

log_success() {
    echo -e "${GREEN}✅ $1${NC}"
    echo "[PASS] $1" >> "$REPORT_FILE"
    ((PASSED_TESTS++))
}

log_error() {
    echo -e "${RED}❌ $1${NC}"
    echo "[FAIL] $1" >> "$REPORT_FILE"
    ((FAILED_TESTS++))
}

log_warning() {
    echo -e "${YELLOW}⚠️  $1${NC}"
    echo "[WARN] $1" >> "$REPORT_FILE"
}

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
    
    # Check if response has .data field (success) or .errors field (failure)
    if echo "$response" | jq -e '.data' > /dev/null 2>&1; then
        log_success "$test_name" >&2
        echo "$response"  # Only JSON to stdout
        return 0
    elif echo "$response" | jq -e '.errors' > /dev/null 2>&1; then
        log_error "$test_name - API returned validation errors" >&2
        echo "$response" | jq '.' >&2
        echo "$response"  # Still output JSON for potential parsing
        return 1
    else
        log_error "$test_name - Unexpected response format" >&2
        echo "$response" >&2
        echo "{}"  # Empty JSON
        return 1
    fi
}

# Start tests
echo "=========================================="
echo "  Memesploding Integration Test Suite"
echo "=========================================="
echo ""
log_info "Server: $BASE_URL"
log_info "Output: $OUTPUT_DIR"
echo ""

### ============================================
### PHASE 1: REGISTRATION & AUTHENTICATION
### ============================================

log_info "PHASE 1: Registration & Authentication"

# 1.1 Alice register (Guest)
RESPONSE=$(test_endpoint "Alice Guest Register" "POST" "/auth/guest" "" '{
  "DeviceId": "alice_auto_device"
}')

ALICE_TOKEN=$(echo "$RESPONSE" | jq -r '.data.accessToken // empty')
ALICE_USER_ID=$(echo "$RESPONSE" | jq -r '.data.user.id // empty')

if [ -z "$ALICE_TOKEN" ]; then
    log_error "Failed to extract Alice token"
    exit 1
fi

log_info "Alice Token: ${ALICE_TOKEN:0:30}..."
log_info "Alice User ID: $ALICE_USER_ID"

# 1.2 Bob register (Guest)
RESPONSE=$(test_endpoint "Bob Guest Register" "POST" "/auth/guest" "" '{
  "DeviceId": "bob_auto_device"
}')

BOB_TOKEN=$(echo "$RESPONSE" | jq -r '.data.accessToken // empty')
BOB_USER_ID=$(echo "$RESPONSE" | jq -r '.data.user.id // empty')

if [ -z "$BOB_TOKEN" ]; then
    log_error "Failed to extract Bob token"
    exit 1
fi

log_info "Bob Token: ${BOB_TOKEN:0:30}..."
log_info "Bob User ID: $BOB_USER_ID"

# 1.3 Charlie register (Guest)
RESPONSE=$(test_endpoint "Charlie Guest Register" "POST" "/auth/guest" "" '{
  "DeviceId": "charlie_auto_device"
}')

CHARLIE_TOKEN=$(echo "$RESPONSE" | jq -r '.data.accessToken // empty')
CHARLIE_USER_ID=$(echo "$RESPONSE" | jq -r '.data.user.id // empty')

if [ -z "$CHARLIE_TOKEN" ]; then
    log_warning "Failed to extract Charlie token"
fi

### ============================================
### PHASE 2: SOCIAL DISCOVERY & PROFILES
### ============================================

log_info "PHASE 2: Social Discovery & Profiles"

# 2.1 Alice view own profile
test_endpoint "Alice View Own Profile" "GET" "/me/profile" "$ALICE_TOKEN" > /dev/null

# 2.2 Alice view own stats
test_endpoint "Alice View Own Stats" "GET" "/me/stats" "$ALICE_TOKEN" > /dev/null

# 2.3 Alice search for Bob
RESPONSE=$(test_endpoint "Alice Search Bob" "GET" "/users/search?query=bob_auto" "$ALICE_TOKEN")

FOUND_BOB=$(echo "$RESPONSE" | jq -r ".data[] | select(.username==\"bob_auto\") | .id // empty")

if [ "$FOUND_BOB" = "$BOB_USER_ID" ]; then
    log_success "Bob found in search results"
else
    log_error "Bob not found in search"
fi

# 2.4 Alice view Bob's profile
test_endpoint "Alice View Bob's Profile" "GET" "/users/$BOB_USER_ID" "$ALICE_TOKEN" > /dev/null

# 2.5 Alice view Bob's stats
test_endpoint "Alice View Bob's Stats" "GET" "/users/$BOB_USER_ID/stats" "$ALICE_TOKEN" > /dev/null

# 2.6 Check leaderboard
test_endpoint "Check Leaderboard" "GET" "/users/leaderboard?limit=10" "$ALICE_TOKEN" > /dev/null

### ============================================
### PHASE 3: FRIENDSHIP MANAGEMENT
### ============================================

log_info "PHASE 3: Friendship Management"

# 3.1 Alice send friend request to Bob
RESPONSE=$(test_endpoint "Alice Send Friend Request" "POST" "/friendships/send" "$ALICE_TOKEN" "{
  \"target_user_id\": \"$BOB_USER_ID\"
}")

FRIENDSHIP_ID=$(echo "$RESPONSE" | jq -r '.data.friendship_id // empty')

if [ -n "$FRIENDSHIP_ID" ]; then
    log_info "Friendship ID: $FRIENDSHIP_ID"
else
    log_error "Failed to extract friendship ID"
fi

# 3.2 ERROR: Duplicate friend request
RESPONSE=$(curl -s -X POST "$BASE_URL/friendships/send" \
    -H "Authorization: Bearer $ALICE_TOKEN" \
    -H "Content-Type: application/json" \
    -d "{\"target_user_id\": \"$BOB_USER_ID\"}")

if echo "$RESPONSE" | jq -e '.success == false' > /dev/null; then
    log_success "Duplicate friend request correctly rejected (400)"
    ((PASSED_TESTS++))
else
    log_error "Duplicate request should have been rejected"
    ((FAILED_TESTS++))
fi
((TOTAL_TESTS++))

# 3.3 Bob check unread notifications
RESPONSE=$(test_endpoint "Bob Check Unread Count" "GET" "/me/notifications/unread-count" "$BOB_TOKEN")

UNREAD_COUNT=$(echo "$RESPONSE" | jq -r '.data.count // 0')
log_info "Bob's unread notifications: $UNREAD_COUNT"

# 3.4 Bob view notifications
RESPONSE=$(test_endpoint "Bob View Notifications" "GET" "/me/notifications?page=1&page_size=10" "$BOB_TOKEN")

NOTIFICATION_ID=$(echo "$RESPONSE" | jq -r '.data.items[0].id // empty')

if [ -n "$NOTIFICATION_ID" ]; then
    log_info "Notification ID: $NOTIFICATION_ID"
fi

# 3.5 Bob accept friend request
test_endpoint "Bob Accept Friend Request" "PATCH" "/friendships/$FRIENDSHIP_ID/respond" "$BOB_TOKEN" '{
  "action": "accept"
}' > /dev/null

# 3.6 Alice view friendships
test_endpoint "Alice View Friendships" "GET" "/me/friendships" "$ALICE_TOKEN" > /dev/null

# 3.7 Bob view friendships
test_endpoint "Bob View Friendships" "GET" "/me/friendships" "$BOB_TOKEN" > /dev/null

### ============================================
### PHASE 4: CONTENT EXPLORATION
### ============================================

log_info "PHASE 4: Content Exploration"

# 4.1 Browse card sets
RESPONSE=$(test_endpoint "Browse Card Sets" "GET" "/card-sets" "$ALICE_TOKEN")

CARD_SET_ID=$(echo "$RESPONSE" | jq -r '.data[0].id // empty')

if [ -n "$CARD_SET_ID" ]; then
    log_info "Card Set ID: $CARD_SET_ID"
else
    log_error "No card sets found"
fi

# 4.2 View card set detail
test_endpoint "View Card Set Detail" "GET" "/card-sets/$CARD_SET_ID" "$BOB_TOKEN" > /dev/null

### ============================================
### PHASE 5: ROOM MANAGEMENT
### ============================================

log_info "PHASE 5: Room Management"

# 5.1 Alice create private room
RESPONSE=$(test_endpoint "Alice Create Room" "POST" "/rooms" "$ALICE_TOKEN" "{
  \"name\": \"Alice Auto Test Room\",
  \"is_public\": false,
  \"max_players\": 4,
  \"turn_timer_seconds\": 15,
  \"card_set_ids\": [\"$CARD_SET_ID\"]
}")

ROOM_CODE=$(echo "$RESPONSE" | jq -r '.data.code // empty')

if [ -n "$ROOM_CODE" ]; then
    log_info "Room Code: $ROOM_CODE"
else
    log_error "Failed to create room"
fi

# 5.2 Bob browse public rooms
test_endpoint "Bob Browse Public Rooms" "GET" "/rooms" "$BOB_TOKEN" > /dev/null

# 5.3 Alice peek room
test_endpoint "Alice Peek Room" "GET" "/rooms/$ROOM_CODE" "$ALICE_TOKEN" > /dev/null

# 5.4 ERROR: Invalid room code
RESPONSE=$(curl -s -X GET "$BASE_URL/rooms/INVALID" \
    -H "Authorization: Bearer $BOB_TOKEN")

if echo "$RESPONSE" | jq -e '.success == false' > /dev/null; then
    log_success "Invalid room code correctly rejected (404)"
    ((PASSED_TESTS++))
else
    log_error "Invalid room code should return 404"
    ((FAILED_TESTS++))
fi
((TOTAL_TESTS++))

### ============================================
### PHASE 6: NOTIFICATION MANAGEMENT
### ============================================

log_info "PHASE 6: Notification Management"

if [ -n "$NOTIFICATION_ID" ]; then
    # 6.1 Mark notification as read
    test_endpoint "Mark Notification Read" "PATCH" "/notifications/$NOTIFICATION_ID" "$BOB_TOKEN" > /dev/null
    
    # 6.2 Check unread count
    test_endpoint "Check Unread Count After" "GET" "/me/notifications/unread-count" "$BOB_TOKEN" > /dev/null
fi

# 6.3 Mark all as read
test_endpoint "Mark All Notifications Read" "PATCH" "/notifications/mark-all-read" "$BOB_TOKEN" > /dev/null

# 6.4 Check unread count (should be 0)
test_endpoint "Check Unread Count Zero" "GET" "/me/notifications/unread-count" "$BOB_TOKEN" > /dev/null

if [ -n "$NOTIFICATION_ID" ]; then
    # 6.5 Delete specific notification
    test_endpoint "Delete Notification" "DELETE" "/notifications/$NOTIFICATION_ID" "$BOB_TOKEN" > /dev/null
fi

# 6.6 Clear all notifications
test_endpoint "Clear All Notifications" "DELETE" "/notifications/clear-all" "$BOB_TOKEN" > /dev/null

### ============================================
### PHASE 7: QUICK MATCHMAKING
### ============================================

log_info "PHASE 7: Quick Matchmaking"

if [ -n "$CHARLIE_TOKEN" ]; then
    # 7.1 Charlie quick-play
    test_endpoint "Charlie Quick-Play" "POST" "/matchmaking/quick-play" "$CHARLIE_TOKEN" > /dev/null
fi

### ============================================
### PHASE 8: MATCH HISTORY (if data exists)
### ============================================

log_info "PHASE 8: Match History"

# 8.1 Alice view match history
RESPONSE=$(test_endpoint "Alice View Match History" "GET" "/me/matches?page=1&page_size=10" "$ALICE_TOKEN")

MATCH_ID=$(echo "$RESPONSE" | jq -r '.data.items[0].id // empty')

if [ -n "$MATCH_ID" ]; then
    log_info "Match ID: $MATCH_ID"
    
    # 8.2 View match detail
    test_endpoint "View Match Detail" "GET" "/matches/$MATCH_ID" "$ALICE_TOKEN" > /dev/null
    
    # 8.3 View match timeline
    test_endpoint "View Match Timeline" "GET" "/matches/$MATCH_ID/timeline" "$ALICE_TOKEN" > /dev/null
else
    log_warning "No match history found (run seed-match-data.sql if needed)"
fi

# 8.4 Final leaderboard check
test_endpoint "Final Leaderboard Check" "GET" "/users/leaderboard?limit=20" "$ALICE_TOKEN" > /dev/null

### ============================================
### SUMMARY
### ============================================

echo ""
echo "=========================================="
echo "           TEST SUMMARY"
echo "=========================================="
echo ""
echo -e "${BLUE}Total Tests:${NC}  $TOTAL_TESTS"
echo -e "${GREEN}Passed:${NC}       $PASSED_TESTS"
echo -e "${RED}Failed:${NC}       $FAILED_TESTS"
echo ""

PASS_RATE=$(awk "BEGIN {printf \"%.1f\", ($PASSED_TESTS/$TOTAL_TESTS)*100}")
echo -e "${BLUE}Pass Rate:${NC}    $PASS_RATE%"

echo ""
echo "Report saved to: $REPORT_FILE"
echo ""

if [ $FAILED_TESTS -eq 0 ]; then
    echo -e "${GREEN}🎉 All tests passed!${NC}"
    exit 0
else
    echo -e "${RED}⚠️  Some tests failed. Check the report for details.${NC}"
    exit 1
fi
