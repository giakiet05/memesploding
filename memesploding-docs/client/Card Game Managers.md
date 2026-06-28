## Game Manager
**Purpose**
Control the overall game flow and high-level state.

**Responsibilities**
- Start and restart the game
- Manage global game states (menu, playing, game over)
- Coordinate major systems
- Detect win/lose conditions

## Turn Manager
**Purpose**
Handle the turn cycle and turn order.

**Responsibilities**
- Track the current turn owner
- Start and end turns
- Trigger turn-related events
- Reset turn-based resources and effects

## Effect Manager
**Purpose**
Execute and resolve card effects.

**Responsibilities**
- Trigger card effects
- Apply damage, buffs, and debuffs
- Resolve chained or triggered effects
- Coordinate effect execution order

## Input Manager
**Purpose**
Handle player interaction with the game.

**Responsibilities**
- Detect card clicks and dragging
- Handle card targeting
- Process player actions

## UI Manager
**Purpose**
Control and update all user interface elements.

**Responsibilities**
- Update UI elements (hand, health, resources)
- Display game messages and notifications
- Manage menus and overlays

## Audio Manager
**Purpose**
Control music and sound effects.

**Responsibilities**
- Play sound effects
- Play and manage background music
- Control audio settings

## Animation Manager
**Purpose**
Manage gameplay-related animations.

**Responsibilities**
- Play card animations
- Play attack and effect animations
- Coordinate animation timing with gameplay