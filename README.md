# GK2 Freecam

A [BepInEx 5](https://github.com/BepInEx/BepInEx) plugin that adds a free-flying camera to Graveyard Keeper 2.

The game is fully 3D with a fixed, tilted orthographic camera that makes it look 2D. This mod detaches that camera so you can pan, fly, rotate, zoom, and switch to a real perspective view.

## Install

1. Download `BepInEx_win_x64_5.4.23.5.zip` from the [BepInEx releases](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5) and extract it into the game folder, next to `GraveyardKeeper2.exe`.
2. Download `GK2Freecam.dll` from the [latest release](https://github.com/mja00/gk2-freecam/releases/latest) and put it in `BepInEx/plugins/`.
3. **Linux / Steam Deck (Proton) only:** set the game's Steam launch options to:
   ```
   WINEDLLOVERRIDES="winhttp=n,b" %command%
   ```
4. Launch the game. `BepInEx/LogOutput.log` should contain `Loaded. Press F7 in game to toggle freecam.`

To uninstall, delete `GK2Freecam.dll` (or `winhttp.dll` to disable BepInEx entirely).

## Controls

| Key | Action |
|---|---|
| F7 | Toggle freecam (turning it off restores the game camera) |
| W / A / S / D | Move along the ground, relative to camera heading |
| E / Space | Move up |
| Q / Left Ctrl | Move down |
| Shift | Move faster |
| Hold right mouse | Look around |
| Mouse wheel | Zoom |
| P | Switch between orthographic and perspective |
| R | Snap back to the game camera's pose |
| H | Hide the on-screen controls hint |

While freecam is on, game input is paused so your character stays put.

## Configuration

After the first launch, edit `BepInEx/config/matta.gk2.freecam.cfg` to change keys, move speed, fast-move multiplier, mouse sensitivity, perspective FOV, and whether the hint is shown.

## Known limitations

- Sprites are drawn for the game's fixed camera angle, so when you rotate or use perspective they appear as flat cards.
- The orthographic view has a limited depth range, so steep angles can clip the scene. Perspective (P) draws much further.
- A cutscene starting while freecam is active may take the camera back.

## Building

Requires the .NET SDK (any recent version) and a game install with BepInEx already extracted into it. The project compiles against the game's own assemblies, so no .NET Framework targeting pack is needed.

```sh
dotnet build -c Release -p:GameDir="/path/to/Graveyard Keeper 2"
```

`GameDir` defaults to the author's Steam library path. The build copies `GK2Freecam.dll` into `BepInEx/plugins/` automatically.

## License

[MIT](LICENSE)
