# Cozy Island Farm — Tripo MCP + Blender 5.2 + Unity 6 (`6000.6.0f1`)

A 3D diorama island farming game built following the 6-department studio workflow from [`@Mnilax`'s post (`2110773148081606775`)](https://x.com/Mnilax/status/2110773148081606775):

1. **Tripo MCP (`tripo-cli` v0.7.0)**: Generated all 10 hero 3D assets (`farmer`, `barn`, `windmill`, `market_stall`, `wooden_bridge`, `crop_pumpkin`, `crop_tomato`, `apple_tree`, `street_lantern`, `watering_can`).
2. **1-Page Game Design Doc**: [`docs/GameDesignDoc.md`](./docs/GameDesignDoc.md) (`Plant -> Water -> Harvest -> Sell` across `Spring`, `Summer`, `Autumn`, `Winter`).
3. **Blender 5.2 Normalization Pipeline**: [`scripts/normalize_tripo_assets.py`](./scripts/normalize_tripo_assets.py) normalizes ground pivots (`min_z = 0.0`), scales every mesh to real-world meter heights, and exports both Unity-ready `.fbx` (`Assets/Resources/TripoModels/`) and `.glb` (`public/assets/models/`).
4. **Unity 6 (`6000.6.0f1`) Project & Standalone macOS App**:
   - Scene: [`Assets/Scenes/CozyIslandFarm.unity`](./Assets/Scenes/CozyIslandFarm.unity)
   - Standalone macOS Application: `Builds/CozyIslandFarm.app`
   - Automated Autopilot Playtester: [`Assets/Scripts/Testing/FarmAutopilotPlaytest.cs`](./Assets/Scripts/Testing/FarmAutopilotPlaytest.cs) (`Logs/autopilot_report.json`)
5. **Interactive Web Mirror (`v01.html`)**:
   - Loads the exact same normalized Tripo `.glb` models in Three.js with the wood-framed Coin Plaque (`¥ 1,240`), Season & Day Plaque (`Day 12 · Spring ☀️`), 6-Slot Wooden Hotbar, Circular Minimap, and Autopilot toggle.

## Quick Start

### 1. Run in Unity 6 (`6000.6.0f1`) or Launch Native macOS App
```bash
open /Users/derya/Gemini-4/cozy-island-farm/Builds/CozyIslandFarm.app
```
Or rebuild and re-verify via Unity 6 batchmode:
```bash
/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -projectPath /Users/derya/Gemini-4/cozy-island-farm \
  -executeMethod CozyFarm.Editor.FarmSceneBuilder.BuildSceneAndVerify \
  -logFile /Users/derya/Gemini-4/cozy-island-farm/Logs/unity_build.log -quit
```

### 2. Run Interactive Browser Mirror (`v01.html`)
```bash
node /Users/derya/Gemini-4/cozy-island-farm/server.mjs
# Open http://127.0.0.1:8788/v01.html
```
