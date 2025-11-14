# Puzzle Game - Usage Guide

## Quick Start for Developers

### Understanding the New Architecture

The game has been refactored with a **modular, service-oriented architecture** that eliminates hardcoded values and makes the system much more flexible.

## Key Concepts

### 1. PuzzleConfiguration (ScriptableObject)
This is your **single source of truth** for puzzle settings.

**Properties:**
- `gridSize` - Number of rows/columns (2-10)
- `cellSize` - Size of each piece in pixels (50-500)
- `gridSpacing` - Gap between pieces (0-20)
- `imageResourcePath` - Path to images in Resources folder
- `difficultyName` - Display name for this preset

**Creating Configurations:**
1. In Unity Editor: Right-click → Create → Puzzle → Configuration
2. Or use menu: Tools → Puzzle Game → Create Configuration Presets
3. Or in code: `PuzzleConfiguration.CreatePreset("Name", size, cellSize)`

### 2. ImageSlicingService (Service Class)
Handles all image slicing logic automatically.

**No more manual calculations!** The service:
- Loads images from Resources
- Calculates piece dimensions automatically
- Slices textures into sprites
- Handles any grid size dynamically

**Usage:**
```csharp
ImageSlicingService service = new ImageSlicingService(configuration);
Texture2D texture = service.LoadRandomTexture();
List<Sprite> pieces = service.SliceTexture(texture);
```

### 3. PuzzleAnimationController (MonoBehaviour)
Provides smooth, programmatic animations.

**Features:**
- Piece placement with bounce effect
- Completion celebration wave
- Spawn animations
- UI transitions

**Usage:**
```csharp
animationController.AnimatePiecePlacement(piece, slot, onComplete);
animationController.AnimatePuzzleCompletion(allPieces);
```

## Setting Up a New Puzzle Game

### Step 1: Create Configurations

**Option A - Use Editor Tool (Recommended):**
1. Go to Tools → Puzzle Game → Create Configuration Presets
2. This creates 4 presets automatically in `Assets/Resources/Configurations/`

**Option B - Manual Creation:**
1. Right-click in Project → Create → Puzzle → Configuration
2. Set your desired values
3. Save the asset

### Step 2: Assign to Scene

1. Select the `Piece` GameObject in your scene
2. In the Inspector, find the Piece component
3. **Random Difficulty Mode:**
   - Enable "Use Random Difficulty"
   - Assign multiple presets to "Difficulty Presets" array
   - Game will randomly select one each play
4. **Fixed Difficulty Mode:**
   - Disable "Use Random Difficulty"
   - Assign one configuration to "Configuration"

### Step 3: Configure Animation

1. Find/Create a GameObject with `PuzzleAnimationController`
2. Adjust animation settings:
   - `Piece Placement Duration` - How long placement animation takes
   - `Piece Scale Bounce` - How much pieces bounce (1.2 = 20% larger)
   - `Rotation Snap Speed` - How fast pieces rotate to align
3. Assign this controller to:
   - GameManager's "Animation" field
   - PieceClass prefab's "Animation Controller" field

### Step 4: Test

Press Play! The game will:
1. Load the configuration
2. Generate pieces with spawn animations
3. Slice a random image automatically
4. Set up the grid layout
5. Ready to play!

## Common Scenarios

### Change Grid Size at Runtime
```csharp
// Currently not supported - configuration is set in Awake()
// Future enhancement: Add ReloadPuzzle() method
```

### Use Custom Image Folder
1. Create your folder in `Assets/Resources/YourFolder/`
2. In PuzzleConfiguration, set `imageResourcePath = "YourFolder"`
3. Ensure images have:
   - Texture Type: Sprite (2D and UI)
   - Read/Write Enabled: ✓

### Adjust Piece Size
- Smaller `cellSize` = More challenge (pieces are smaller)
- Larger `cellSize` = Easier (pieces are larger)
- Recommended ranges:
  - 2x2: 250-350px
  - 3x3: 150-250px
  - 4x4: 120-180px
  - 5x5: 100-140px

### Disable Animations
In Piece component:
- Uncheck "Enable Spawn Animations"

In PuzzleAnimationController:
- Set durations to 0 for instant placement

## Advanced: Creating Custom Presets

### In Code
```csharp
public class MyCustomPresets : MonoBehaviour
{
    void Start()
    {
        // Create a mega-hard 6x6 puzzle
        PuzzleConfiguration megaHard = PuzzleConfiguration.CreatePreset("Mega Hard", 6, 100);
        megaHard.gridSpacing = 0; // No spacing for extra challenge!
        
        // Use it (requires refactoring to support runtime changes)
    }
}
```

### In Editor
Use the custom editor tool at Tools → Puzzle Game → Create Configuration Presets, or create manually via Create menu.

## Architecture Benefits

### Before (Old System)
```csharp
// Had to manually keep these in sync 😰
int[] numImages = {4, 9, 16, 25};
int[] numColumsRows = {2, 3, 4, 5};
int[] numCellSize = {300, 200, 150, 120};
```

### After (New System)
```csharp
// Single configuration object 😊
PuzzleConfiguration config;
// Everything calculated automatically!
ImageSlicingService service = new ImageSlicingService(config);
```

**Benefits:**
✅ No manual synchronization needed
✅ Easy to add new difficulties
✅ Reusable configurations
✅ Type-safe and validated
✅ Testable and maintainable

## Troubleshooting

### Images Don't Appear
**Cause:** Images not readable
**Solution:** Select images → Inspector → Enable "Read/Write Enabled"

### Wrong Number of Pieces
**Cause:** Configuration not assigned
**Solution:** Assign PuzzleConfiguration to Piece component

### No Animations
**Cause:** Animation controller not assigned
**Solution:** Assign PuzzleAnimationController to GameManager and PieceClass prefab

### Pieces Too Small/Large
**Cause:** Cell size not appropriate for grid size
**Solution:** Adjust `cellSize` in configuration
- Larger grid = smaller cellSize
- Use recommended ranges above

## Performance Tips

- Use power-of-two texture sizes (256, 512, 1024) for best performance
- Limit grid size to 6x6 or smaller for mobile devices
- Disable spawn animations for better initial performance
- Use texture compression in Build Settings

## Next Steps

1. ✅ Read ARCHITECTURE.md for detailed design
2. ✅ Create your configurations
3. ✅ Add your images
4. ✅ Test different difficulties
5. 🎮 Start customizing!

## Support

For issues or questions, refer to:
- ARCHITECTURE.md - System design documentation
- README.md - Setup and features overview
- Source code comments - Inline documentation
