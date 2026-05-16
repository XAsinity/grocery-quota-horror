# Grocery Quota Horror

`Grocery Quota Horror` is a Unity 6 co-op horror prototype for `1-4` players. The team enters a giant supermarket after hours, grabs enough items to hit quota, survives roaming monsters, and extracts before the night collapses.

## Unity Version

- `6000.3.10f1`

## Project Structure

- `Assets/Scenes/Bootstrap.unity`: menu, session setup, host/client flow
- `Assets/Scenes/SupermarketNight.unity`: procedural run scene
- `Assets/Data`: run config, item, monster, and chunk data assets
- `Assets/Prefabs/Chunks`: modular store chunks your map partner can edit
- `Assets/Prefabs/Gameplay`: player, item, monster, and objective prefabs
- `Assets/Scripts`: runtime and editor code

## Opening The Project

1. Open the folder in Unity Hub with version `6000.3.10f1`.
2. Let Package Manager restore dependencies.
3. Open `Bootstrap` and press play.
4. Use `Host Local` for offline testing or `Host Relay` / `Join Relay` after configuring Unity Services.

## Map Workflow

Your friend can contribute without touching gameplay code.

- Edit or duplicate prefabs in `Assets/Prefabs/Chunks`.
- Keep `ChunkMetadata` on the chunk root.
- Keep at least one `ChunkConnection` marked as `Entry` and one marked as `Exit`.
- Use `ItemSpawnMarker` children to define valid item spawn points.
- Use `PatrolPoint` children to shape monster paths inside a room.
- Add the chunk prefab to `RunConfig.chunkPool` if it should generate in runs.

## GitHub Workflow

- Keep `Visible Meta Files` enabled.
- Keep `Asset Serialization` set to `Force Text`.
- Commit prefab and scene changes together when changing layout data.
- Use short-lived branches for features or chunk work, then merge back into `main`.

## Current Vertical Slice

- Procedural supermarket layout from modular chunk prefabs
- Shared quota + extraction loop
- Networked host/client co-op via NGO
- Relay session wrapper with local fallback
- Three simple monster archetypes
- Down, revive, flashlight, hide, sprint, and door interaction

