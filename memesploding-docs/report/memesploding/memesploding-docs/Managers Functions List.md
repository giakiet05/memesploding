## Room Manager

**Role:** Lobby / session management (outside the match)
- Manage connected players
- Handle join / leave / reconnect
- Maintain room state (waiting, in-game, finished)
- Create and destroy GameSession
- Bridge network layer to game logic
## Game Session

**Role:** Orchestrator of a single match
- Owns and coordinates all game systems
- Holds the GameState (source of truth)
- Initializes and wires TurnManager
- Processes player actions (entry point from network layer)
- Controls lifecycle: start → running → end
- Evaluates win/lose conditions
## Turn Manager

**Role:** Turn order and flow control
- Track current player turn
- Enforce valid turn order
- Advance turns and phases
- Handle edge cases:
    - player elimination
    - skip / reverse (if applicable)
    - turn timeout

## Game State

**Role:** Central source of truth for all game data
- Store active players and eliminated players
- Track current turn index and round
- Maintain game status (waiting, playing, finished)
- Store board state and game-specific data
- Manage card state (draw pile, discard pile, player hands)
- Track scores, health, or other player attributes
- Provide simple getters (e.g., current player)
- Support basic state updates (e.g., eliminate player, advance turn)
