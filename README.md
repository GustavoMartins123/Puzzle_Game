# Puzzle Game

A modular Unity puzzle game with automatic image slicing, configurable difficulty, and smooth animations.

## ✨ New Features

- **Automatic Image Slicing**: No more manual proportion adjustments! The system automatically calculates dimensions.
- **Modular Configuration**: Use ScriptableObjects to configure difficulty (Easy, Medium, Hard, Expert)
- **Smooth Animations**: Programmatic piece placement, spawn, and completion animations
- **Better Architecture**: Clean separation of concerns with services and configuration

## 🎮 Quick Setup

### 1. Clone and Open
Clone the repository and open the project in Unity Hub.

### 2. Add Images
1. Navigate to `Assets/Resources/Sprites/Fish/` (or update path in configuration)
2. Add your puzzle images (JPG, PNG)
3. Select all images in Unity Inspector
4. Set **Texture Type** to "Sprite (2D and UI)"
5. **Enable "Read/Write Enabled"** in Advanced settings (Required!)
6. Click Apply

### 3. Play
Click the Play button and enjoy!

## 📖 Full Documentation

For detailed architecture documentation, see [ARCHITECTURE.md](ARCHITECTURE.md)

## 🔧 Configuration

The game now uses a modular configuration system. You can:
- Use random difficulty (default behavior)
- Create custom configurations via Create → Puzzle → Configuration
- Adjust grid size (2-10), cell size, spacing, and image paths

## 🎯 Difficulty Presets

- **Easy**: 2x2 grid (4 pieces)
- **Medium**: 3x3 grid (9 pieces)  
- **Hard**: 4x4 grid (16 pieces)
- **Expert**: 5x5 grid (25 pieces)

## 🏗️ Architecture Highlights

### Key Improvements
1. **PuzzleConfiguration**: ScriptableObject for settings (replaces hardcoded arrays)
2. **ImageSlicingService**: Dedicated service for automatic image slicing
3. **PuzzleAnimationController**: Smooth programmatic animations
4. **Better separation of concerns**: Each class has a clear responsibility

### Old vs New

**Before** (Manual setup):
```csharp
int[] numImages = {4, 9, 16, 25};
int[] numCellSize = {300, 200, 150, 120};
// Arrays had to be kept in sync manually
```

**After** (Automatic):
```csharp
PuzzleConfiguration config; // Single source of truth
ImageSlicingService service = new ImageSlicingService(config);
// Everything calculated automatically!
```

## 🎨 Features

- ✅ Automatic image slicing with smart dimension calculation
- ✅ Drag and drop puzzle pieces
- ✅ Smooth placement animations
- ✅ Puzzle completion celebration
- ✅ Random piece positioning and rotation
- ✅ Configurable difficulty levels
- ✅ Pause functionality

## 🛠️ Technologies

- Unity 2021.3+ (recommended)
- C# with modern patterns
- Unity Input System
- ScriptableObjects for configuration

## 📝 License

Open source - feel free to use and modify!
