# 🎮 WebSocket Game Flow Testing

## Overview

This document covers testing the full WebSocket game flow for Memesploding, including:
- 3 players connecting via WebSocket
- Starting a match
- Playing game commands (draw, play, nope, defuse)
- Game state transitions

## Current Status

### ✅ What Works
- ✅ REST API setup (authentication, room creation, joining, ready state)
- ✅ WebSocket server is running and accepting connections
- ✅ Connection requires valid game access token
- ✅ Room transitions to ready state with multiple players

### ⏳ Pending Investigation
- Token generation (currently empty, only generated when match starts?)
- Exact WebSocket authentication mechanism
- StartMatch command format
- Game command handling (DrawCard, PlayCard, Nope, UseDefuse)
- Game state updates sent back from server

## Testing Setup

### Prerequisites
- Both servers running:
  - API Server: `localhost:5217`
  - Game Server: `localhost:5204`
- `websocat` installed: `which websocat`

### Test Scripts Available

#### 1. `run-gameplay-tests.sh` - REST API Only
Sets up everything via REST, gets to ready state.
```bash
./run-gameplay-tests.sh
```

#### 2. `play-game-interactive.sh` - Setup for Manual Testing
Sets up 3 players and shows WebSocket connection commands.
```bash
./play-game-interactive.sh
```

#### 3. `test-ws-connection.sh` - WebSocket Connection Test
Tests if WebSocket server is responding.
```bash
./test-ws-connection.sh
```

## Manual WebSocket Testing

### Step 1: Setup Players (REST)
```bash
./play-game-interactive.sh
```

This outputs:
- Room code
- WebSocket URL
- Connection commands for each player
- Token information (currently empty until match starts)

### Step 2: Open 3 Terminals

**Terminal 1 - Alice (Host):**
```bash
websocat 'ws://localhost:5204/ws'
```

**Terminal 2 - Bob:**
```bash
websocat 'ws://localhost:5204/ws'
```

**Terminal 3 - Charlie:**
```bash
websocat 'ws://localhost:5204/ws'
```

### Step 3: Send Game Commands

#### From Alice (Terminal 1) - Start Match:
```json
{"type":"StartMatch"}
```

#### From Alice - Draw Card:
```json
{"type":"DrawCard"}
```

#### From Alice - Play Attack Card:
```json
{"type":"PlayCard","data":{"cardCode":"ATTACK"}}
```

#### From Bob (Terminal 2) - Draw Card:
```json
{"type":"DrawCard"}
```

#### From Bob - Play Skip:
```json
{"type":"PlayCard","data":{"cardCode":"SKIP"}}
```

#### From Charlie (Terminal 3) - Send Nope:
```json
{"type":"Nope"}
```

## Game Command Reference

Based on `GameplayCommands.md`, available commands:

### Match Control
```json
{"type":"StartMatch"}
{"type":"EndTurn"}
```

### Card Actions
```json
{"type":"DrawCard"}
{"type":"PlayCard","data":{"cardCode":"ATTACK"}}
{"type":"PlayCard","data":{"cardCode":"SKIP"}}
{"type":"PlayCard","data":{"cardCode":"FAVOR"}}
{"type":"PlayCard","data":{"cardCode":"SEE_THE_FUTURE"}}
{"type":"PlayCard","data":{"cardCode":"SHUFFLE"}}
{"type":"PlayCard","data":{"cardCode":"DEFUSE"}}
```

### Reactions
```json
{"type":"Nope"}
{"type":"UseDefuse"}
```

## Expected Flow

1. **Setup Phase** (REST)
   - 3 players login
   - Room created
   - Players join
   - Players mark ready

2. **WebSocket Connection Phase**
   - Each player connects with WebSocket token (or without initially)
   - Server acknowledges connection

3. **Match Start**
   - Host (Alice) sends `StartMatch`
   - Game state initializes
   - Each player receives initial game state

4. **Play Phase**
   - Players draw cards
   - Players play cards
   - Players react (Nope, Defuse)
   - Game state updates between turns

5. **End Phase**
   - Winner determined
   - Match ends
   - Players disconnect

## Troubleshooting

### WebSocket Connection Fails Immediately
- **Cause**: Invalid or missing token
- **Fix**: Tokens are empty until match starts. Try connecting without token parameter.

### No Response from Server
- **Cause**: WebSocket server may require proper authentication handshake
- **Fix**: Check server logs for error details
- **Alternative**: Try sending a command immediately after connecting

### Commands Not Received
- **Cause**: JSON format might be incorrect
- **Fix**: Ensure JSON is properly formatted (no extra quotes, valid syntax)
- **Debug**: Check `websocat` output for any error messages

### Connection Closes Immediately
- **Cause**: Authentication failed
- **Fix**: Verify token is valid and properly included in URL
- **Debug**: Run `test-ws-connection.sh` to confirm server is responding

## WebSocket Protocol Details

### Connection URL Format
```
ws://localhost:5204/ws?token=YOUR_TOKEN_HERE
```

### Message Format
All messages are JSON objects with required `type` field:
```json
{
  "type": "CommandName",
  "data": { /* optional command data */ }
}
```

### Server Response Format
Expected responses (to be confirmed):
```json
{
  "type": "GameStateUpdate",
  "data": { /* game state */ }
}

{
  "type": "CardDrawn",
  "data": { /* card details */ }
}

{
  "type": "CardPlayed",
  "data": { /* card details */ }
}
```

## Next Steps for Investigation

1. **Capture WebSocket Traffic**
   - Use Fiddler or Wireshark to see actual protocol
   - Understand authentication flow
   - Verify message formats

2. **Check Server Logs**
   - Look for WebSocket connection events
   - Check for errors when commands received
   - Verify game state transitions

3. **Test Token Generation**
   - When exactly are tokens generated?
   - How are they provided to clients?
   - Do they expire?

4. **Implement WebSocket Client**
   - Write C# SignalR client for full automation
   - Handle reconnection
   - Parse game state updates

5. **Automated Testing**
   - Create script that simulates full game play
   - Verify all game actions work
   - Test error scenarios

## Files

- `run-gameplay-tests.sh` - REST API test (working)
- `play-game-interactive.sh` - Setup for manual WS testing
- `test-ws-connection.sh` - WS connection verification
- `GameplayCommands.md` - Full command reference
- `TEST_RESULTS.md` - REST API findings
- `WEBSOCKET_FLOW.md` - This file

## References

- GameHub: `Memesploding.Game/Hubs/GameHub.cs`
- WebSocket DTOs: `Memesploding.Game/DTOs/WsClientMessages.cs`
- Game Commands: `Memesploding.Game/Application/CommandHandlers/`
