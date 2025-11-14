# Puzzle Game - Architecture Documentation

## Overview
This is a modular Unity puzzle game with improved architecture, featuring configurable difficulty settings, automatic image slicing, and smooth animations.

## Recent Improvements

### 1. Modular Configuration System
- **PuzzleConfiguration**: ScriptableObject-based configuration system
  - No more hardcoded arrays for grid sizes and cell dimensions
  - Easy-to-create difficulty presets
  - Validates configuration values automatically
  
### 2. Image Slicing Service
- **ImageSlicingService**: Dedicated service for image processing
  - Automatically calculates piece dimensions based on source image
  - No more manual proportion adjustments needed!
  - Clean separation of concerns
  - Supports any grid size (2x2 to 10x10)

### 3. Animation System
- **PuzzleAnimationController**: Programmatic animations
  - Smooth piece placement with scale and rotation tweens
  - Puzzle completion celebration with wave effect
  - Piece spawn animations
  - UI fade transitions
  - Configurable animation parameters

### 4. Configuration Presets
- **Easy**: 2x2 grid (4 pieces) - Large 300px pieces
- **Medium**: 3x3 grid (9 pieces) - Medium 200px pieces
- **Hard**: 4x4 grid (16 pieces) - Small 150px pieces
- **Expert**: 5x5 grid (25 pieces) - Tiny 120px pieces

## Architecture

### Core Components

#### GameManager
- Singleton pattern for global access
- Coordinates piece and slot management
- Handles win condition detection
- Triggers completion animations

#### Piece
- Generates puzzle pieces from configuration
- Uses ImageSlicingService for automatic slicing
- Configures grid layout based on settings
- Supports spawn animations

#### PieceClass
- Individual puzzle piece behavior
- Drag and drop functionality
- Animation-aware placement
- Slot validation

#### ImageSlicingService
- Pure service class (no MonoBehaviour)
- Handles texture loading from Resources
- Automatic dimension calculation
- Creates sprite slices with proper pivot points

#### PuzzleAnimationController
- MonoBehaviour for coroutine management
- Easing functions for smooth animations
- Configurable animation durations
- Completion celebration system

## Setup Instructions

### 1. Clone and Open Project
```bash
git clone <repository-url>
```
Open the project in Unity Hub (Unity 2021.3 or later recommended).

### 2. Add Images
1. Navigate to `Assets/Resources/Sprites/Fish/`
2. Add your puzzle images (JPG, PNG)
3. Select all images in Unity
4. Set **Texture Type** to "Sprite (2D and UI)"
5. **Enable "Read/Write Enabled"** in Advanced settings (critical!)
6. Click Apply

### 3. Configure Difficulty
Option A - Use Random Difficulty (Default):
- The game will randomly select from available presets each play

Option B - Create Custom Configuration:
1. Right-click in Project window
2. Create → Puzzle → Configuration
3. Set grid size, cell size, and other parameters
4. Assign to Piece component in the scene

### 4. Setup Scene References
Ensure GameManager has references to:
- PuzzleAnimationController
- Piece component
- UI elements (panels, drag image)

### 5. Play
Click Play button and test!

## How It Works

### Image Slicing (No More Manual Setup!)

**Old Way** (Manual):
```csharp
int[] numImages = {4, 9, 16, 25};
int[] numColumsRows = {2, 3, 4, 5};
int[] numCellSize = {300, 200, 150, 120};
// Had to keep arrays in sync manually!
```

**New Way** (Automatic):
```csharp
// Configuration handles everything
PuzzleConfiguration config;
ImageSlicingService service = new ImageSlicingService(config);
List<Sprite> pieces = service.SliceTexture(sourceTexture);
// Automatic calculation of dimensions!
```

### Animation Flow

1. **Game Start**: Pieces spawn with scale animation
2. **Dragging**: Smooth cursor follow with lerp
3. **Placement**: Scale bounce + rotation snap animation
4. **Completion**: Wave effect across all pieces

## Code Structure

```
Assets/Scripts/
├── GameManager.cs                    # Main game coordinator
├── Piece.cs                          # Piece generation and setup
├── PieceClass.cs                     # Individual piece behavior
├── Slot.cs                           # Slot for piece placement
├── InputManager.cs                   # Input handling
├── UiDragPiece.cs                   # Drag UI element
├── PuzzleConfiguration.cs            # Configuration ScriptableObject
├── PuzzleConfigurationPresets.cs     # Preset factory
├── ImageSlicingService.cs            # Image processing service
└── PuzzleAnimationController.cs      # Animation system
```

## Key Features

✅ **Modular Configuration**: No hardcoded values
✅ **Automatic Slicing**: Smart dimension calculation
✅ **Smooth Animations**: Programmatic tweening
✅ **Multiple Difficulties**: Easy preset system
✅ **Clean Architecture**: Separation of concerns
✅ **Extensible**: Easy to add new features

## Future Enhancements

Potential additions:
- Timer and scoring system
- Multiple image categories
- Save/load progress
- Sound effects
- Particle effects on completion
- Hint system
- Undo/redo functionality

## Troubleshooting

**Problem**: Pieces don't appear
- **Solution**: Check that images have "Read/Write Enabled" in import settings

**Problem**: Grid spacing issues
- **Solution**: Adjust `gridSpacing` in PuzzleConfiguration (try 1-2)

**Problem**: Pieces too large/small
- **Solution**: Modify `cellSize` in PuzzleConfiguration

**Problem**: No animations
- **Solution**: Ensure PuzzleAnimationController is assigned to GameManager

## License

This project is open source. Feel free to use and modify!
