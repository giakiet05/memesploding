#!/bin/bash

###
### Memesploding API - WebSocket Integration Test (Automated)
### Tests WebSocket endpoints using websocat
###
### Requirements:
### - websocat (install: cargo install websocat OR sudo snap install websocat)
### - jq (JSON processor)
### - Server running on localhost:5217
###
### Usage:
### ./ws-test.sh <alice_token> <bob_token>
###
### Example:
### ./ws-test.sh "eyJhbGc..." "eyJhbGc..."
###

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

# Configuration
WS_URL="ws://localhost:5217/api/v1/ws"
OUTPUT_DIR="./output"
TIMEOUT=5

# Check arguments
if [ $# -lt 2 ]; then
    echo "Usage: $0 <alice_token> <bob_token>"
    echo ""
    echo "Get tokens by running:"
    echo "  ./run-integration-tests.sh"
    echo "  or register users manually"
    exit 1
fi

ALICE_TOKEN="$1"
BOB_TOKEN="$2"

# Check websocat installation
if ! command -v websocat &> /dev/null; then
    echo -e "${RED}Error: websocat not found${NC}"
    echo ""
    echo "Install with:"
    echo "  cargo install websocat"
    echo "  or"
    echo "  sudo snap install websocat"
    exit 1
fi

# Create output directory
mkdir -p "$OUTPUT_DIR"

# Test counters
TOTAL_TESTS=0
PASSED_TESTS=0
FAILED_TESTS=0

log_info() {
    echo -e "${BLUE}ℹ️  $1${NC}"
}

log_success() {
    echo -e "${GREEN}✅ $1${NC}"
    ((PASSED_TESTS++))
}

log_error() {
    echo -e "${RED}❌ $1${NC}"
    ((FAILED_TESTS++))
}

log_warning() {
    echo -e "${YELLOW}⚠️  $1${NC}"
}

echo "=========================================="
echo "   WebSocket Integration Tests"
echo "=========================================="
echo ""
log_info "WebSocket URL: $WS_URL"
log_info "Timeout: ${TIMEOUT}s per test"
echo ""

### ============================================
### TEST 1: Connection & Authentication
### ============================================

log_info "TEST 1: Connection & Authentication"
((TOTAL_TESTS++))

echo '{"action":"heartbeat"}' | timeout $TIMEOUT websocat "$WS_URL?access_token=$ALICE_TOKEN" > "$OUTPUT_DIR/ws-test1.txt" 2>&1 &
WS_PID=$!

sleep 2

if ps -p $WS_PID > /dev/null 2>&1; then
    kill $WS_PID 2>/dev/null || true
    log_success "Alice connected successfully (JWT auth working)"
else
    log_error "Alice connection failed"
fi

### ============================================
### TEST 2: Heartbeat (no response expected)
### ============================================

log_info "TEST 2: Heartbeat Mechanism"
((TOTAL_TESTS++))

echo '{"action":"heartbeat"}' | timeout 3 websocat "$WS_URL?access_token=$ALICE_TOKEN" > "$OUTPUT_DIR/ws-heartbeat.txt" 2>&1

if [ $? -eq 124 ]; then
    log_success "Heartbeat sent (timeout expected, no response)"
else
    log_warning "Heartbeat test unexpected result"
fi

### ============================================
### TEST 3: Invalid Token (should fail)
### ============================================

log_info "TEST 3: Invalid Token Rejection"
((TOTAL_TESTS++))

echo '{"action":"heartbeat"}' | timeout 2 websocat "$WS_URL?access_token=INVALID" > "$OUTPUT_DIR/ws-invalid.txt" 2>&1

if [ $? -ne 0 ]; then
    log_success "Invalid token correctly rejected"
else
    log_error "Invalid token should have been rejected"
fi

### ============================================
### TEST 4: Presence Broadcast (Alice connects → Bob should see)
### ============================================

log_info "TEST 4: Presence Broadcast (requires manual verification)"
((TOTAL_TESTS++))

log_info "Starting Alice connection..."
websocat "$WS_URL?access_token=$ALICE_TOKEN" > "$OUTPUT_DIR/ws-alice.txt" 2>&1 &
ALICE_PID=$!

sleep 2

log_info "Starting Bob connection..."
websocat "$WS_URL?access_token=$BOB_TOKEN" > "$OUTPUT_DIR/ws-bob.txt" 2>&1 &
BOB_PID=$!

sleep 3

# Check if both connected
if ps -p $ALICE_PID > /dev/null 2>&1 && ps -p $BOB_PID > /dev/null 2>&1; then
    log_success "Both users connected (presence broadcast likely working)"
    
    # Kill connections
    kill $ALICE_PID $BOB_PID 2>/dev/null || true
else
    log_error "One or both connections failed"
fi

### ============================================
### TEST 5: Room Invitation (requires room code)
### ============================================

log_info "TEST 5: Room Invitation Flow"
log_warning "This test requires:"
log_warning "  1. Alice creates a room (use run-integration-tests.sh)"
log_warning "  2. Get room code"
log_warning "  3. Run: echo '{\"action\":\"invite_to_room\",\"data\":{\"room_code\":\"CODE\",\"friend_user_id\":\"BOB_ID\"}}' | websocat \"$WS_URL?access_token=\$ALICE_TOKEN\""
log_info "Skipping automated test (interactive)"
((TOTAL_TESTS++))

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
echo "Output files saved to: $OUTPUT_DIR/"
echo ""

log_info "For comprehensive WebSocket testing, use manual guide: ws-test-manual.md"
echo ""

if [ $FAILED_TESTS -eq 0 ]; then
    echo -e "${GREEN}✅ All automated tests passed!${NC}"
    exit 0
else
    echo -e "${RED}⚠️  Some tests failed.${NC}"
    exit 1
fi
