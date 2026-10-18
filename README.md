# SephiriaDamageMeter

**English** | [简体中文](README.zh-CN.md)

A fan-made real-time damage meter AddOn for **Sephiria（赛菲莉娅）**.

> **Unofficial fan project. Not affiliated with TEAM HORAY.**
> Requires a legitimate copy of Sephiria. This repository contains only original mod code — no game code, assets, fonts, or extracted files.

- Tested on Sephiria `1.0.33` (Unity `6000.3.21f1`)
- No BepInEx / Harmony / other loaders required — uses the game's built-in `AddOns` loader
- License: MIT (mod source only)

![status](https://img.shields.io/badge/status-playable-brightgreen)
![license](https://img.shields.io/badge/license-MIT-blue)

## Features

- Compact Brotato-style damage overlay below the minimap.
- Per-source damage, percentage share and element-coloured bars.
- `F7` post-battle summary: per-player totals, team share, DPS and top sources.
- Solo and co-op: every player gets their own block with avatar, name, total and top 4 sources.
- Reads the game's existing synced `dealsStatistics` data — no custom networking.
- Draggable overlay; position is saved to `config.json`.

## Download

Grab the latest `SephiriaDamageMeter-vX.Y.Z.zip` from **GitHub Releases**, or build from source below.

## Install

1. In Steam: right-click **Sephiria** → **Manage** → **Browse local files**.
2. Create an `AddOns` folder inside the game folder if it does not exist.
3. Copy the `SephiriaDamageMeter` folder into `AddOns`.
4. The final path must be:

   ```text
   Sephiria\AddOns\SephiriaDamageMeter\SephiriaDamageMeter.dll
   ```

5. Launch the game. The overlay appears below the minimap in combat.

See [USER_GUIDE.en.md](USER_GUIDE.en.md) for the full guide (install, hotkeys, configuration, FAQ).

## Hotkeys

| Key | Action |
| --- | --- |
| `F7` | Toggle post-battle summary |
| `F8` | Toggle current-location / whole-run scope |
| `F9` | Show / hide overlay |
| `F10` | Reload `config.json` |

## Configuration

The full option table is in [USER_GUIDE.en.md](USER_GUIDE.en.md). Commonly changed options:

- `panelWidth`, `fontSize`, `backgroundOpacity` — size and opacity.
- `maxSourceRowsSolo`, `partySourcesPerPlayer`, `maxPlayerBlocks` — how many rows / players to show.
- `totalBarColor` — solo total-bar colour (`#RRGGBB`).
- `hideWhenEmpty`, `hideOutsideBattle` — visibility rules.
- `dpsUseBattleTime` — DPS denominator (combat time vs. elapsed time).
- `hotkeyToggle*` — rebind hotkeys.

Edit `config.json` and press `F10` in game; no restart needed.

## Multiplayer

- Works in solo and co-op; each player can install the mod independently.
- The server writes `PlayerAvatar.dealsStatistics`; Mirror replicates it to every client, so no custom RPC is needed.
- All players' data is visible to any client that has the mod installed.
- Larger lobbies (e.g. SephiriaUnlocker) are supported; excess player blocks fold into “N more players”.

## How it works

The mod reads `PlayerAvatar.dealsStatistics` and `PlayerAvatar.dealsStatistics_LastLocation` — the same Mirror `SyncDictionary<DamageKey, float>` values the game uses for its own damage screen. The server writes them in `PlayerAvatar.AddDealStat`, and Mirror replicates them to every client. The overlay is plain uGUI on a dedicated Overlay canvas and has no `GraphicRaycaster`, so it does not block the game's clicks.

## Compatibility

- SephiriaUnlocker / larger lobbies: supported; player blocks fold when the limit is exceeded.
- SephiriaReconnect: no code-level integration; the game restores `dealsStatistics` from the run save on reconnect/checkpoint restore, and the meter refreshes from the restored dictionaries.
- Game updates can break field access; please report with `Player.log`.

## Build from source

Requires Windows, PowerShell, and an installed copy of Sephiria.

```powershell
# Option A: auto-detect the Steam install
.\build.ps1

# Option B: pass the game folder explicitly
.\build.ps1 -SephiriaDir "D:\Steam\steamapps\common\Sephiria"
```

The script uses the game's own `Sephiria_Data\Managed` assemblies, downloads Roslyn `csc` on first run if needed, and writes `dist\SephiriaDamageMeter\`. Copy that folder to `Sephiria\AddOns\` or attach it to a GitHub Release.

## Project layout

```text
SephiriaDamageMeter/
├─ src/SephiriaDamageMeter/   # mod source
├─ build.ps1                  # build script
├─ config.example.json        # default config
├─ metadata.json              # AddOns metadata
├─ README.md                  # English
├─ README.zh-CN.md            # 简体中文
├─ USER_GUIDE.en.md           # English guide
├─ 使用指南.md                # 中文指南
├─ LICENSE
└─ CHANGELOG.md
```

## Disclaimer

This is an unofficial fan mod. It is not affiliated with, endorsed by, or supported by TEAM HORAY. It contains no game code or assets and requires a legitimate copy of Sephiria. Use at your own risk. Do not redistribute game files or decompiled game code with this project.

## License

[MIT](LICENSE) — applies only to the original mod code in this repository. Sephiria and its assets belong to TEAM HORAY.
