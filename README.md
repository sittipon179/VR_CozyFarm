# VR CozyFarm

A cozy VR farming game built in Unity, using the XR Interaction Toolkit. Plant, water, and harvest crops; sleep to advance the day/night cycle; shop for seeds; and collect a pickable in-world book that unlocks an in-game encyclopedia.

## Requirements

- **Unity 6000.0.70f1** (Unity 6) — install this exact version via Unity Hub to avoid asset/package re-import issues.
- A VR headset + controllers (OpenXR-compatible) for real play, **or** just a keyboard/mouse — the project ships with the **XR Interaction Simulator**, so it can be tested entirely inside the Editor without a headset.

## Getting started

1. Clone this repo.
2. Open the project folder with Unity Hub, using Unity `6000.0.70f1`.
3. Let Unity finish importing packages/assets on first open (this can take a while the first time).
4. Open the main scene: `Assets/Scenes/SampleScene.unity`.
5. Press Play.
   - With a headset connected via OpenXR, it should just work.
   - Without a headset, the **XR Interaction Simulator** (already present in the scene, GameObject `XR Interaction Simulator`) lets you drive the rig with keyboard/mouse — WASD to move, mouse to look, and it simulates controller input for interacting with objects. Check the XRI documentation for the simulator's default key bindings if needed.

Note: the Main Menu (`MainMenuController`) disables the whole `Locomotion` GameObject until you actually click **Start** from the menu — you won't be able to move until you do that first.

## Key packages (all in `Packages/manifest.json`, already committed)

- `com.unity.xr.interaction.toolkit` 3.4.1 (Starter Assets samples imported: Dynamic Move Provider, Snap Turn, Continuous Turn, ray/poke/near-far interactors, XR Interaction Simulator, XRI Default Input Actions)
- `com.unity.xr.hands` 1.7.3
- `com.unity.xr.management`, `com.unity.xr.openxr`, `com.unity.xr.meta-openxr`, `com.unity.xr.androidxr-openxr`

## Progress — desktop → VR conversion

The project started as a desktop mouse/keyboard prototype and has since been fully converted to VR. Verified directly against the live scene (component-level checks, not just "should be done"):

- **VR rig activated & locomotion configured — done.** `XR Origin Hands (XR Rig)` is the active player rig; the legacy desktop controller (`DebugPlayer_FPS`) is disabled (kept only as a one-toggle-away fallback, not part of the shipped game). Movement runs through `DynamicMoveProvider` (current speed: 24) on the rig's `Move` child. The old, duplicate `ContinuousMoveProvider` on the `Locomotion` object is correctly disabled so it can't double-drive movement. The **XR Interaction Simulator** is present and active for keyboard/mouse testing in the Editor.
- **World interactables (Bed, Book, Shop) converted to XRI — done.** `Bed`, `ShopCounter`, and `InteriorTableBook` all have `XRSimpleInteractable` + `InteractableHoverFeedback`, reacting to VR select (ray or poke) instead of mouse raycasts.
- **UI panels moved to world space — done.** `BookCanvas`, `InventoryCanvas`, `ShopCanvas`, and `MainMenuCanvas` are all `World Space` canvases with a ray-clickable raycaster, and the `EventSystem` runs the XR UI Input Module. (`HUD_Canvas` intentionally stays Screen Space Overlay — it's just status readouts, not something you click.)
- **Farming interaction (`BuildPlantController`) converted to VR input — done.** Its ray now comes from the rig's Near-Far Interactor (`interactionRayOrigin`) instead of the old desktop camera, so tilling/planting/watering/harvesting work through the VR ray + controller button.

### ⚠️ Found while verifying — worth a look

Both **`SnapTurnProvider`** and **`ContinuousTurnProvider`** are enabled at the same time on the rig's `Turn` object (bound to "Snap Turn" and "Turn" respectively, which normally share the same stick axis in the default XRI input actions). Having both active together can make the stick both snap-turn *and* smoothly rotate at once, which usually isn't the intended feel. Whoever picks this up should decide which turn style the game wants and disable the other component — this wasn't touched in this pass since it's a design choice (snap vs. smooth turn), not an obvious bug to silently "fix".

## What's implemented (gameplay)

- **Farming loop**: till, plant, water, and harvest via `BuildPlantController` + `Ground Plot` / `PlantData`, with a hover/highlight system and drag-till placement.
- **Shop**: `ShopInteractable` / `ShopUIController` — buy seeds/items with in-game currency (`CurrencyManager`).
- **Sleep / day-night cycle**: `BedInteractable` + `SleepManager`, `TimeManager`, `DayNightLightingManager` / `TimeManagerLightingSync`.
- **Inventory & equipment**: `InventoryUIController`, `EquipmentManager`, `EquippedToolVisual`.
- **Book / encyclopedia**: pick up the physical book prop in the house (`BookInteractable` on `InteriorTableBook`) → it disappears from the world and unlocks a special icon slot in the inventory (`BookCollectionManager`, wired into `InventoryUIController`) → click it to open a fullscreen, paper-styled encyclopedia panel (`BookUIController`) with page navigation. While open, rig movement and turning are locked (only limited head-look is allowed); close with the small Close button, the **G / Grip button** (either hand), or Escape.
- **UI state management**: `UIStateManager` centrally tracks which panel (if any) is open, gating world interactions and movement while a panel is up.

## Known caveats / things a new contributor should know

- **Double turn-provider** — see the "Found while verifying" note above; pick Snap Turn or Continuous Turn and disable the other.
- **`DebugPlayer_FPS`** — a legacy desktop-style FPS controller GameObject is still in the scene, but **disabled**. It's kept as a one-toggle-away fallback for testing without VR at all; it is not part of the shipped game.
- **`Assets/CobraGamesAssets`** — only the specific files actually used by the scene (~55MB: the bed, book prop, and a small table) are committed. The full pack is ~1.7GB; if you need more props from it, you'll need to re-import/download the original asset pack separately (it's excluded via `.gitignore`).
- A few other third-party packs are intentionally **not** committed because nothing in the scene currently references them: `AK STUDIO ART`, `Davidweber01`, `PolyRonin`, `RawWoodenFurnitureFree`, `StylizedFurniturePack`, `LowPolyFarmLite`. If you start using assets from one of these, remove its exclusion block from `.gitignore` before committing.
- **Not yet implemented** (flagged as out of scope in an earlier planning pass, not forgotten): physical hip-grab tools and a hand-attached physical seed pouch — currently, tool/seed selection happens through the flat inventory UI rather than a physical grab-and-holster interaction.
- `Assets/Screenshots/` is gitignored — it's debug/test capture output, not project content.

## Project structure (scene hierarchy)

- `XR Origin Hands (XR Rig)` — the VR player rig (camera, hands/controllers, locomotion, `BuildPlantController`).
- `Environment` — ground, trees, rocks, fences, garden decoration, flowers, mushrooms, grass.
- `House` — interior/exterior meshes, door, spawn points, wall colliders, interior light.
- `UI` — all Canvases (HUD, Main Menu, Book, Inventory, Shop, sleep fade) plus `EventSystem`.
- `_Managers` — singleton-style manager scripts (`UIStateManager`, `EquipmentManager`, `CurrencyManager`, `SleepManager`, `EquippedToolVisual`, `BookCollectionManager`).
- `_PlantSystem`, `TimeManager`, `DayNightLightingManager`, `DayNightVisuals` — farming/time/lighting systems.
- `Bed`, `ShopCounter`, `InteriorTable`, `InteriorTableBook` — world interactables.
- `XR Interaction Simulator` — Editor-only VR input simulation via keyboard/mouse.

## Handoff notes

This repo is at the point where a fresh clone + Unity Hub open should get you straight into a working project — no missing assets, no manual setup beyond installing the matching Unity version. The one open item worth triaging first is the double turn-provider note above.
