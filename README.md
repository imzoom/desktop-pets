# 🤖 Desktop Pet Animation Controller & Character Studio

Integrated multi-character sprite system featuring **Hooded Blade (Sword Master)** with **Sword Power** and **Robot Pet** with per-frame extraction, 32-bit alpha transparency, autonomous screen roaming, and seamless action transitions.

---

## 🎭 Characters & Abilities

### 1. 🥷 Hooded Blade (Sword Master) — *Default*
- **`idle`** (32 frames): Ready stance holding glowing emerald blade.
- **`run`** (24 frames): Agile ninja sprint across the desktop with drawn blade.
- **`attack`** (32 frames): Rapid double-slash sword combo.
- **`sword-power`** (32 frames): **Special Power Move!** Charges blade with glowing emerald aura, then unleashes a blazing crescent energy shockwave that sweeps across the screen before returning to idle.
- **`jump`** (7 frames): Acrobatic leap jump.

### 2. 🤖 Robot Pet
- **`idle`** (35 frames): Combat robot stance.
- **`kick`** (31 frames): Martial arts kick action, smoothly returning to idle.
- **`punch`** (36 frames): Forward strike punch.
- **`run`** (16 frames): Bipedal sprint cycle.

---

## 📁 Project Structure

```
C:\Jay\Demos\dextop-pets\
├── images/                           # Source sprite sheets & JSON metadata
│   ├── hooded-blade-idle.png/.json
│   ├── hooded-blade-run.png/.json
│   ├── hooded-blade-attack.png/.json
│   ├── hooded-blade-jump.png/.json
│   ├── idle-spritesheet.png
│   ├── kick-spritesheet.png
│   ├── punch-spritesheet.png
│   └── run-spritesheet.png
├── extracted_frames/                 # 245 extracted 32-bit transparent PNG frames
│   ├── hooded-blade/
│   │   ├── idle/ (32 frames)
│   │   ├── run/ (24 frames)
│   │   ├── attack/ (32 frames)
│   │   ├── sword-power/ (32 frames with energy slash FX)
│   │   └── jump/ (7 frames)
│   └── robot/
│       ├── idle/ (35 frames)
│       ├── kick/ (31 frames)
│       ├── punch/ (36 frames)
│       └── run/ (16 frames)
├── animation-config.json             # Multi-character configuration system
├── src/
│   ├── extractor.js                  # Frame boundary inspector & PNG frame extractor
│   ├── sword-power-fx.js             # Procedural sword aura & crescent shockwave generator
│   └── animation-controller.js       # Reusable Sprite Animation Controller
├── desktop-pet-win/                  # Native Windows Desktop Pet (WPF, .NET 10)
│   ├── DesktopPet.csproj
│   ├── MainWindow.xaml               # Transparent floating pet with context menu
│   └── MainWindow.xaml.cs            # Autonomous roaming, character switching, hotkeys
├── web/                              # Interactive Web Studio & Simulator
│   ├── index.html                    # Character dropdown, action buttons, canvas stage
│   ├── style.css                     # Modern dark theme styles
│   └── app.js                        # Multi-character controller & autonomous logic
└── tests/
    └── verify-all.js                 # Verification test suite
```

---

## 📦 Zero-Dependency Standalone Release (Runs on ANY Windows PC)

No .NET runtime or SDK required on target machines! The standalone executable bundles the complete runtime and embedded assets:

### Option 1: Direct Single-File Executable
You can copy and run `DesktopPet.exe` directly on any Windows 10/11 x64 PC:
- Path: `release\DesktopPet-Standalone-win-x64\DesktopPet.exe`
- **Zero installation**: Double-click to run. No .NET runtime needed!

### Option 2: Ready-to-Share ZIP Archive
- Path: `release\DesktopPet-Standalone-Win64.zip`
- Extract anywhere and double-click `DesktopPet.exe`.

### How to Rebuild the Standalone Release:
```powershell
cd C:\Jay\Demos\dextop-pets\desktop-pet-win
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "..\release\DesktopPet-Standalone-win-x64"
```

---

## 🎮 How to Run (Development Mode)

### Native Windows Desktop Pet (Floating on Desktop)
```powershell
cd C:\Jay\Demos\dextop-pets\desktop-pet-win
dotnet run --no-restore
```

**Controls**:
- **Double-Click**: **Toggle Character** between Hooded Blade and Robot!
- **Left-Click**: Unleashes **Sword Power** (or Kick for Robot).
- **Click & Drag**: Move pet anywhere across all monitors.
- **Hotkeys**:
  - `C`: **Toggle Character** (Hooded Blade ↔ Robot Pet)
  - `M`: **Send to Next Display** (Teleport between Laptop and Extended Screen)
  - `Space` or `S`: **Sword Power** (or Kick)
  - `A`: **Sword Attack** (or Punch)
  - `J`: **Leap Jump**
  - `R`: **Run Sprint**
  - `I`: **Idle Stance**
  - `Esc`: Exit
- **Right-Click Menu**:
  - **👁️ Eye Health Care (20-20-20 Rule)**:
    - ✅ Reminder Active (Every 20 Mins)
    - 🔔 Remind Now (Test Alert Bubble)
    - ⏱️ Every 20 / 30 / 45 Minutes
  - Switch Character (Hooded Blade / Robot)
  - Action Triggers (Sword Power, Sword Slash, Leap Jump, Run, Idle)
  - Autonomous Roaming & Pacing (Chill & Relaxed, Active, Sit Still)
  - Frame Rate (12, 24, 30, 48 FPS)
  - Pet Scale (Small, Medium, Original)
  - Always on Top toggle
  - 📺 Send to Next Display (M)
  - Reset to Screen Center

---

### Interactive Web Simulator
```powershell
cd C:\Jay\Demos\dextop-pets
node server.js
```
Open `http://localhost:3000/web/index.html` in your browser.
- Switch characters live with the dropdown in the header.
- Trigger Sword Power, Sword Slash, Leap Jump, Run, or Idle.
- Test autonomous roaming with direction flipping and speed controls.
