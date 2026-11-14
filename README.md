# 🤖 Desktop Pet Animation Controller & Character Integration

Integrated character sprite sheets into the **Desktop Pet** application with per-frame extraction, transparent background rendering, reusable animation controller, and seamless action transitions.

---

## 📁 Project Structure

```
C:\Jay\Demos\dextop-pets\
├── images/                           # Original source sprite sheets
│   ├── idle-spritesheet.png          # 3072x2304 RGBA (6x6 grid, 35 valid frames)
│   ├── kick-spritesheet.png          # 3072x2304 RGBA (6x6 grid, 31 valid frames)
│   ├── punch-spritesheet.png         # 3072x2304 RGBA (6x6 grid, 36 valid frames)
│   └── run-spritesheet.png           # 3072x1152 RGBA (6x3 grid, 16 valid frames)
├── extracted_frames/                 # Extracted individual 32-bit transparent PNG frames
│   ├── idle/ (frame_000.png .. frame_034.png)
│   ├── kick/ (frame_000.png .. frame_030.png)
│   ├── punch/ (frame_000.png .. frame_035.png)
│   └── run/ (frame_000.png .. frame_015.png)
├── animation-config.json             # Animation configuration system with anchor/boundary metadata
├── src/
│   ├── extractor.js                  # Frame boundary inspector & PNG frame extractor
│   └── animation-controller.js       # Reusable Sprite Animation Controller
├── desktop-pet-win/                  # Native Windows Desktop Pet (WPF, .NET 10)
│   ├── DesktopPet.csproj
│   ├── MainWindow.xaml               # Transparent, borderless, floating desktop window
│   └── MainWindow.xaml.cs            # WPF pet controller, drag/move, click-to-kick
├── web/                              # Interactive Web Desktop Pet Viewer & Controller
│   ├── index.html                    # Pet canvas stage with action buttons & inspector
│   ├── style.css                     # Modern UI styles
│   └── app.js                        # Web pet logic & event handlers
├── server.js                         # Zero-dependency local web server
└── tests/
    └── verify-all.js                 # Verification test suite covering all 10 requirements
```

---

## 🔍 Sprite Sheet Inspection & Alignment Analysis

Each sprite sheet was decoded and analyzed at the pixel level:

| Asset | Sheet Size | Grid Layout | Valid Frames | Empty Padding Cells Skipped | Ground Baseline |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **`idle-spritesheet.png`** | 3072 × 2304 | 6 cols × 6 rows | **35 frames** | Row 5, Col 5 (last cell) | `y = 320 px` |
| **`kick-spritesheet.png`** | 3072 × 2304 | 6 cols × 6 rows | **31 frames** | Row 5, Cols 1..5 (last 5 cells) | `y = 321 px` |
| **`punch-spritesheet.png`** | 3072 × 2304 | 6 cols × 6 rows | **36 frames** | None (all 36 valid) | `y = 320 px` |
| **`run-spritesheet.png`** | 3072 × 1152 | 6 cols × 3 rows | **16 frames** | Row 2, Cols 4..5 (last 2 cells) | `y = 324 px` |

### Key Findings & Verification:
1. **Empty Padding Cells Identified**: Without inspecting pixel boundaries, blindly assuming 36 frames for all sheets would have rendered empty frames at the end of `idle` (1 empty frame), `kick` (5 empty frames), and `run` (2 empty frames), causing visible flashing or black rectangles.
2. **Anchor & Baseline Alignment**: Across all animations, the robot's standing foot is grounded at `y = 320 px` to `y = 321 px` and horizontal center is at `x = 220 px`. By anchoring each extracted frame to this pivot, character size and position remain seamless and jitter-free between idle, kick, punch, and run.
3. **Transparency Preservation**: Extracted frames use 32-bit RGBA PNG encoding. Corner and boundary pixels have `alpha = 0` (zero black rectangles), and interior robot pixels retain their exact colors and alpha values.

---

## 🎮 How to Run

### Option 1: Native Windows Desktop Pet (Floating on Desktop)
Runs as a transparent, borderless, floating desktop character:
```powershell
cd c:\Jay\Demos\dextop-pets\desktop-pet-win
dotnet run
```
**Controls**:
- **Left-Click**: Performs **Kick Action**, then smoothly returns to Idle.
- **Click & Drag**: Move the pet anywhere across your monitors.
- **Right-Click Menu**:
  - Switch Actions: Idle, Kick, Punch, Run
  - Adjust Frame Rate: 12, 24 (Default), 30, 48 FPS
  - Pet Scale: Small (256x192), Medium (384x288), Original (512x384)
  - Toggle Always on Top
  - Reset to Screen Center / Exit
- **Hotkeys**: `Space` or `K` (Kick), `P` (Punch), `R` (Run), `I` (Idle), `Esc` (Exit).

---

### Option 2: Interactive Web Desktop Pet (Browser Viewer)
```powershell
cd c:\Jay\Demos\dextop-pets
node server.js
```
Open `http://localhost:3000/web/index.html` in your browser.
- Interactive controls for FPS, pet scale, loop toggling, and live custom frame sequence editing (`[0, 1, 2, 3, 2, 1]`).
- Debug Bounding Box & Anchor crosshair visualizer toggle.
- Click the character on the canvas directly to trigger the kick action.

---

## ⚙️ Animation Configuration System (`animation-config.json`)

The system allows easily registering and customizing animations:

```json
{
  "version": "1.0.0",
  "character": "RobotPet",
  "globalAnchor": { "x": 220, "y": 320 },
  "frameDimensions": { "width": 512, "height": 384 },
  "defaultAnimation": "idle",
  "animations": {
    "idle": {
      "fps": 24,
      "loop": true,
      "nextAnimation": null,
      "totalFrames": 35,
      "frameSequence": [0, 1, 2, ..., 34]
    },
    "kick": {
      "fps": 24,
      "loop": false,
      "nextAnimation": "idle",
      "totalFrames": 31,
      "frameSequence": [0, 1, 2, ..., 30]
    }
  }
}
```

To add a new animation:
1. Add the spritesheet to `images/` or extract frames to `extracted_frames/<anim_name>/`.
2. Add an entry into `animation-config.json` with `fps`, `loop`, `nextAnimation`, and `frameSequence`.
3. In code, simply call `controller.registerAnimation('animName', config)` or `controller.play('animName')`.

---

## 🧪 Verification Tests

Run the automated test suite:
```powershell
node tests/verify-all.js
```
All 10 project requirements pass automated verification.
