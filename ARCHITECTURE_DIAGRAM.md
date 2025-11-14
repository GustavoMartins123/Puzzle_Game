# System Architecture Diagram

## Component Hierarchy

```
┌─────────────────────────────────────────────────────────────┐
│                        Unity Scene                           │
│                                                              │
│  ┌───────────────────────────────────────────────────────┐  │
│  │                   GameManager                         │  │
│  │                   (Singleton)                         │  │
│  │                                                       │  │
│  │  Coordinates:                                         │  │
│  │  • Piece placement                                    │  │
│  │  • Win condition checking                             │  │
│  │  • Animation triggering                               │  │
│  └───────────┬───────────────────────┬──────────────────┘  │
│              │                       │                      │
│      ┌───────▼──────┐        ┌──────▼───────┐             │
│      │    Piece     │        │ InputManager │             │
│      │   Manager    │        │              │             │
│      └───────┬──────┘        └──────────────┘             │
│              │                                              │
│   ┌──────────▼──────────┐                                  │
│   │ PuzzleConfiguration │ ◄─── ScriptableObject           │
│   │   (Data Asset)      │                                  │
│   └──────────┬──────────┘                                  │
│              │                                              │
│   ┌──────────▼──────────┐                                  │
│   │ ImageSlicingService │ ◄─── Service Class               │
│   │   (Pure Logic)      │                                  │
│   └──────────┬──────────┘                                  │
│              │                                              │
│   ┌──────────▼──────────┐                                  │
│   │  PieceClass x N     │ ◄─── Individual Pieces           │
│   │  (MonoBehaviour)    │                                  │
│   └──────────┬──────────┘                                  │
│              │                                              │
│   ┌──────────▼──────────┐                                  │
│   │PuzzleAnimationCtrl  │ ◄─── Animation System            │
│   │  (MonoBehaviour)    │                                  │
│   └─────────────────────┘                                  │
└─────────────────────────────────────────────────────────────┘
```

## Data Flow

### 1. Initialization Flow

```
Game Start
    │
    ▼
GameManager.Awake()
    │
    ├─► Creates Singleton
    │
    ▼
Piece.Awake()
    │
    ├─► Loads PuzzleConfiguration (ScriptableObject)
    │   │
    │   ├─► gridSize: 3
    │   ├─► cellSize: 200
    │   └─► imageResourcePath: "Sprites/Fish"
    │
    ├─► Creates ImageSlicingService
    │   │
    │   └─► Passes configuration
    │
    ├─► Configures Grid Layout
    │   │
    │   └─► Uses config.gridSize and config.cellSize
    │
    ├─► Instantiates Pieces
    │   │
    │   └─► config.TotalPieces (e.g., 9 for 3x3)
    │
    ▼
LoadAndSliceTexture()
    │
    ├─► service.LoadRandomTexture()
    │   │
    │   └─► Returns Texture2D from Resources
    │
    ├─► service.SliceTexture(texture)
    │   │
    │   ├─► Calculates: pieceWidth = texture.width / gridSize
    │   ├─► Calculates: pieceHeight = texture.height / gridSize
    │   │
    │   └─► Returns List<Sprite> (9 pieces)
    │
    └─► Assigns sprites to piece GameObjects
        │
        └─► Each piece now has its image
```

### 2. Image Slicing Flow (AUTOMATIC!)

```
User provides source image (e.g., 1200x1200 px)
    │
    ▼
ImageSlicingService receives:
    │
    ├─► sourceTexture: 1200x1200 px
    ├─► gridSize: 3 (from config)
    │
    ▼
Automatic Calculation:
    │
    ├─► pieceWidth = 1200 / 3 = 400 px
    ├─► pieceHeight = 1200 / 3 = 400 px
    │
    ▼
Slicing Loop:
    │
    for row in 0..3:
        for col in 0..3:
            ├─► xPos = col * 400
            ├─► yPos = (2 - row) * 400  [inverted Y]
            │
            ├─► Extract pixels at (xPos, yPos, 400, 400)
            ├─► Create new Texture2D
            ├─► Create Sprite
            │
            └─► Add to list
    │
    ▼
Returns: [Sprite₀, Sprite₁, ..., Sprite₈]
```

**Key Point:** ✅ NO manual calculations needed!

### 3. Animation Flow

```
Player drops piece
    │
    ▼
PieceClass.OnDrop()
    │
    ├─► Checks if correct slot
    │
    └─► If correct:
        │
        ▼
    PuzzleAnimationController.AnimatePiecePlacement()
        │
        ├─► Coroutine starts
        │
        ├─► Frame by frame:
        │   │
        │   ├─► t = elapsed / duration
        │   ├─► scale = EaseOutBack(t)  ◄─── Bounce!
        │   ├─► rotation = Slerp(current, identity, t)
        │   └─► position = Lerp(current, zero, t)
        │
        └─► Final state:
            │
            ├─► position = (0, 0, 0)
            ├─► rotation = (0, 0, 0)
            └─► scale = (1, 1, 1)
```

### 4. Win Condition Flow

```
Piece placed successfully
    │
    ▼
GameManager.PieceManager_OnPieceChanged()
    │
    ├─► Check all pieces
    │   │
    │   └─► All in correct slots?
    │
    └─► If YES:
        │
        ▼
    PuzzleAnimationController.AnimatePuzzleCompletion()
        │
        ├─► For each piece (with delay):
        │   │
        │   ├─► Start bounce animation
        │   │   │
        │   │   └─► scale = 1 + sin(t * π) * 0.3
        │   │
        │   └─► Wait 0.05s
        │
        └─► Wave effect across all pieces!
```

## Class Relationships

```
┌─────────────────────────────────────────────────────────────┐
│                    PuzzleConfiguration                       │
│                    (ScriptableObject)                        │
│                                                              │
│  Properties:                                                 │
│  • gridSize: int [2-10]                                     │
│  • cellSize: int [50-500]                                   │
│  • gridSpacing: int [0-20]                                  │
│  • imageResourcePath: string                                │
│  • difficultyName: string                                   │
│                                                              │
│  Computed:                                                   │
│  • TotalPieces => gridSize * gridSize                       │
└───────────────────────┬─────────────────────────────────────┘
                        │
                        │ Uses
                        │
┌───────────────────────▼─────────────────────────────────────┐
│                  ImageSlicingService                         │
│                    (Service Class)                           │
│                                                              │
│  Constructor:                                                │
│  • ImageSlicingService(PuzzleConfiguration config)          │
│                                                              │
│  Methods:                                                    │
│  • LoadRandomTexture() → Texture2D                          │
│  • CreateSpriteFromTexture(Texture2D) → Sprite              │
│  • SliceTexture(Texture2D) → List<Sprite>                   │
│  • GetCellSize() → Vector2                                  │
│  • GetGridSize() → int                                      │
│  • GetTotalPieces() → int                                   │
└─────────────────────────────────────────────────────────────┘
                        │
                        │ Used by
                        │
┌───────────────────────▼─────────────────────────────────────┐
│                        Piece                                 │
│                   (MonoBehaviour)                            │
│                                                              │
│  Fields:                                                     │
│  • configuration: PuzzleConfiguration                        │
│  • slicingService: ImageSlicingService                      │
│  • sprites: List<Image>                                     │
│                                                              │
│  Methods:                                                    │
│  • Awake() - Initialize                                     │
│  • ConfigureGridLayout()                                    │
│  • InstantiateSprites(int)                                  │
│  • LoadAndSliceTexture()                                    │
└─────────────────────────────────────────────────────────────┘
                        │
                        │ Creates
                        │
┌───────────────────────▼─────────────────────────────────────┐
│                     PieceClass                               │
│                  (MonoBehaviour)                             │
│                                                              │
│  Interfaces:                                                 │
│  • IDragHandler                                             │
│  • IDropHandler                                             │
│  • IBeginDragHandler                                        │
│                                                              │
│  Methods:                                                    │
│  • OnBeginDrag(PointerEventData)                            │
│  • OnDrag(PointerEventData)                                 │
│  • OnDrop(PointerEventData)                                 │
└─────────────────────────────────────────────────────────────┘
                        │
                        │ Uses
                        │
┌───────────────────────▼─────────────────────────────────────┐
│              PuzzleAnimationController                       │
│                  (MonoBehaviour)                             │
│                                                              │
│  Methods:                                                    │
│  • AnimatePiecePlacement(Transform, Transform, Action)      │
│  • AnimatePuzzleCompletion(Transform[])                     │
│  • AnimatePieceSpawn(Transform, float)                      │
│  • AnimatePanelFadeIn(CanvasGroup, float)                   │
│                                                              │
│  Private:                                                    │
│  • EaseOutBack(float) - Custom easing                       │
│  • PiecePlacementAnimation() - Coroutine                    │
│  • CompletionCelebrationAnimation() - Coroutine             │
└─────────────────────────────────────────────────────────────┘
```

## Configuration System

```
Unity Editor
    │
    ├─► Create Menu: "Puzzle → Configuration"
    │   │
    │   └─► Creates PuzzleConfiguration.asset
    │
    └─► Tools Menu: "Puzzle Game → Create Configuration Presets"
        │
        ├─► Creates Assets/Resources/Configurations/
        │
        ├─► Creates Easy_2x2.asset
        ├─► Creates Medium_3x3.asset
        ├─► Creates Hard_4x4.asset
        └─► Creates Expert_5x5.asset
```

### Configuration Presets

```
┌─────────────┬──────────┬─────────┬──────────┬──────────────┐
│ Preset      │ GridSize │ Pieces  │ CellSize │ Difficulty   │
├─────────────┼──────────┼─────────┼──────────┼──────────────┤
│ Easy        │   2x2    │    4    │  300px   │ ⭐           │
│ Medium      │   3x3    │    9    │  200px   │ ⭐⭐         │
│ Hard        │   4x4    │   16    │  150px   │ ⭐⭐⭐       │
│ Expert      │   5x5    │   25    │  120px   │ ⭐⭐⭐⭐     │
└─────────────┴──────────┴─────────┴──────────┴──────────────┘
```

## Before vs After Architecture

### BEFORE (Old System)

```
Piece.cs
    │
    ├─► int[] numImages = {4, 9, 16, 25}
    ├─► int[] numColumsRows = {2, 3, 4, 5}
    └─► int[] numCellSize = {300, 200, 150, 120}
        │
        ├─► Manual matching in loops
        ├─► Hardcoded calculations
        └─► Error-prone synchronization
```

**Problems:**
- ❌ 3 arrays to maintain
- ❌ Manual synchronization
- ❌ Hardcoded values
- ❌ No validation
- ❌ Not reusable

### AFTER (New System)

```
PuzzleConfiguration (ScriptableObject)
    │
    ├─► gridSize: 3
    ├─► cellSize: 200
    └─► imageResourcePath: "Sprites/Fish"
        │
        ▼
ImageSlicingService
    │
    ├─► Automatic dimension calculation
    ├─► Works with any size
    └─► Clean, testable code
        │
        ▼
Piece.cs
    │
    └─► Uses service for everything
```

**Benefits:**
- ✅ Single source of truth
- ✅ Automatic calculation
- ✅ Validated configuration
- ✅ Reusable assets
- ✅ Extensible system

## Animation System Architecture

```
PuzzleAnimationController
    │
    ├─► Component Animations
    │   │
    │   ├─► Piece Spawn
    │   │   └─► Scale: 0 → 1 (EaseOutBack)
    │   │
    │   ├─► Piece Placement
    │   │   ├─► Position: current → (0,0,0)
    │   │   ├─► Rotation: current → identity
    │   │   └─► Scale: 1 → 1.2 → 1 (bounce)
    │   │
    │   ├─► Puzzle Completion
    │   │   └─► Wave effect with scale pulse
    │   │
    │   └─► UI Transitions
    │       └─► Alpha fade: 0 ↔ 1
    │
    └─► Easing Functions
        │
        └─► EaseOutBack(t)
            └─► Creates bounce effect
```

## File Structure

```
Puzzle_Game/
│
├─── Documentation
│    ├── README.md                      (Quick start)
│    ├── SUMMARY.md                     (Overview)
│    ├── ARCHITECTURE.md                (This file)
│    ├── USAGE_GUIDE.md                 (How-to guide)
│    └── BEFORE_AFTER_COMPARISON.md     (Changes)
│
└─── Assets/
     │
     ├── Scripts/
     │   │
     │   ├── Configuration System
     │   │   ├── PuzzleConfiguration.cs
     │   │   └── PuzzleConfigurationPresets.cs
     │   │
     │   ├── Services
     │   │   └── ImageSlicingService.cs
     │   │
     │   ├── Animation
     │   │   └── PuzzleAnimationController.cs
     │   │
     │   ├── Game Logic
     │   │   ├── GameManager.cs
     │   │   ├── Piece.cs
     │   │   ├── PieceClass.cs
     │   │   └── Slot.cs
     │   │
     │   ├── UI
     │   │   ├── DifficultySelector.cs
     │   │   └── UiDragPiece.cs
     │   │
     │   ├── Input
     │   │   └── InputManager.cs
     │   │
     │   └── Editor/
     │       └── PuzzleConfigurationCreator.cs
     │
     └── Resources/
         └── Configurations/
             ├── Easy_2x2.asset
             ├── Medium_3x3.asset
             ├── Hard_4x4.asset
             └── Expert_5x5.asset
```

## Design Patterns Used

1. **Singleton Pattern**
   - GameManager
   - Global access point

2. **Service Pattern**
   - ImageSlicingService
   - Pure logic, no Unity dependencies

3. **Strategy Pattern**
   - PuzzleConfiguration
   - Interchangeable configurations

4. **Factory Pattern**
   - PuzzleConfigurationPresets
   - Creates preset instances

5. **Observer Pattern**
   - InputManager events
   - Event-driven architecture

6. **Coroutine Pattern**
   - PuzzleAnimationController
   - Async animations

## Summary

This architecture provides:

✅ **Modularity**: Each component has single responsibility
✅ **Flexibility**: Easy to modify and extend
✅ **Testability**: Service classes are pure logic
✅ **Maintainability**: Clear structure and documentation
✅ **Reusability**: Components work independently
✅ **Scalability**: Ready for new features

**The main improvement:** Image slicing is now **fully automatic** through the service architecture!
