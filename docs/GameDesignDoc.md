# Cozy Island Farm — 1-Page Game Design Document (`Step 2`)

> **Reference Inspiration**: [`@Mnilax` (`2110773148081606775`)](https://x.com/Mnilax/status/2110773148081606775) — *"write a 1-page doc: plant > water > harvest > sell, 4 seasons"*

## 1. High-Concept & Visual Direction
A tactile 3D diorama island farming game rendered from an elevated 3/4 cozy isometric camera. A straw-hatted farmer tends rich furrowed soil plots surrounded by a Red Gambrel Barn, a spinning Stone & Timber Windmill, a Cobblestone Market Plaza with three red-and-white striped stalls, and an arched wooden footbridge over a coastal inlet.

## 2. Core Gameplay Loop (`Plant -> Water -> Harvest -> Sell`)
1. **Plant**: Select `Pumpkin Seeds` (`[3]`) or `Tomato Seeds` (`[4]`) on the 6-slot wooden hotbar and interact with an empty tilled soil mound in the 7-furrow central plot.
2. **Water**: Select the `Watering Can` (`[1]`) and water planted mounds. Watering emits a sparkly droplet spray, darkens the soil furrow to moist loam, and starts the active growth timer.
3. **Grow (`3 Visual Stages`)**:
   - `Stage 1 (Seedling / 4-Leaf Green Sprout)`: Emerges immediately upon watering.
   - `Stage 2 (Leafy Bush / Flowering Vine)`: Mid-growth foliage stage.
   - `Stage 3 (Mature Harvestable Crop)`: Plump ribbed **Orange Pumpkin** or fruit-laden **Tomato Bush** with gentle ready-to-harvest bounce.
4. **Harvest**: Interact with a mature crop (or sweep with the `Hoe/Basket` `[2]`) to pop the produce into hotbar slots `[5]` (Pumpkins) and `[6]` (Tomatoes).
5. **Sell at Market**: Walk onto the Cobblestone Market Plaza in front of the 3 striped Market Stalls to sell harvested produce. Golden coins fly into the top-left `¥` coin plaque (`+45G` per Pumpkin, `+30G` per Tomato).

## 3. Four-Season Calendar & Day/Dusk Lighting Cycle
- **Time-of-Day Cycle**: Each in-game day progresses smoothly from **Crisp Mid-Day Sunshine (`☀️`)** through **Warm Golden Sunset** to **Lantern-Lit Dusk (`🌙`)**, automatically illuminating the shepherd's-crook street lanterns, market stalls, and barn windows.
- **4 Seasons (`Spring -> Summer -> Autumn -> Winter`)**:
  - **Spring**: Balanced `1.0x` growth speed; emerald grass and white wildflowers.
  - **Summer**: Sun-drenched `1.25x` tomato growth boost; golden-green grass and bright turquoise ocean.
  - **Autumn**: Harvest-gold `1.35x` pumpkin sell bonus; warm amber tree foliage and sunset sky.
  - **Winter**: Cozy frost-rimmed `0.85x` growth speed with `+25%` winter market premium; snow-dusted grass tint.

## 4. World Landmarks & Tripo 3D Asset Roster
| Asset ID | Role in Island Diorama | Target Height |
| :--- | :--- | :--- |
| `farmer` | Player character in blue overalls & straw hat (Mixamo/Tripo rigged: Idle, Walk) | `1.65 m` |
| `barn` | Northwest landmark with white X-braced double doors & cupola | `5.60 m` |
| `windmill` | North-center landmark with continuously rotating 4-blade timber rotor | `6.80 m` |
| `market_stall` | 3 red-and-white striped booths along the eastern cobblestone plaza | `3.20 m` |
| `wooden_bridge` | Arched timber footbridge spanning the eastern coastal water channel | `4.50 m` |
| `crop_pumpkin` | Mature ribbed orange pumpkin crop (North plot & interactive plot Stage 3) | `0.58 m` |
| `crop_tomato` | Mature fruiting tomato bush (North plot & interactive plot Stage 3) | `0.85 m` |
| `apple_tree` | Fruit-bearing broadleaf trees framing the north & south island edges | `4.20 m` |
| `street_lantern` | Shepherd's-crook iron lanterns lighting the fence perimeter & market plaza | `2.40 m` |
| `watering_can` | Blue metal watering can held by the farmer during watering actions | `0.45 m` |
