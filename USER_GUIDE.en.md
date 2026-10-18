# SephiriaDamageMeter User Guide (v1.6.1)

[简体中文](使用指南.md) | **English**

A damage meter AddOn for **Sephiria（赛菲莉娅）**. During combat it shows your damage sources (and, in co-op, every player's) below the minimap. Press `F7` for a current-location / whole-run damage summary.

This guide walks through installation, in-game use and configuration. No BepInEx, Harmony or other loaders are required.

---

## Contents

1. [Install](#1-install)
2. [Confirm the install](#2-confirm-the-install)
3. [What the overlay looks like](#3-what-the-overlay-looks-like)
4. [In-game controls](#4-in-game-controls)
5. [Position and adjustment](#5-position-and-adjustment)
6. [Configuration (config.json)](#6-configuration-configjson)
7. [Common configurations](#7-common-configurations)
8. [FAQ](#8-faq)
9. [Uninstall](#9-uninstall)
10. [Multiplayer](#10-multiplayer)

---

## 1. Install

### 1.1 Find the game folder

1. Open Steam and find **Sephiria** in your library.
2. Right-click **Sephiria** → **Manage** → **Browse local files**.
3. The folder should contain `Sephiria.exe`.

### 1.2 Copy into AddOns

1. If the game folder has no `AddOns` folder, create one named exactly `AddOns`.
2. Copy the whole `SephiriaDamageMeter` folder into `AddOns`.

The final path must be:

```text
Steam\steamapps\common\Sephiria\AddOns\SephiriaDamageMeter\SephiriaDamageMeter.dll
```

A common mistake is nesting the folder twice:

```text
AddOns\SephiriaDamageMeter\SephiriaDamageMeter\SephiriaDamageMeter.dll   ← wrong
```

If the archive extracts to `SephiriaDamageMeter-v1.6.1\SephiriaDamageMeter\...`, copy the inner `SephiriaDamageMeter` folder into `AddOns`, not the outer versioned folder.

### 1.3 Launch the game

Start Sephiria. Enter a run and combat; the overlay appears below the minimap.

---

## 2. Confirm the install

### 2.1 In game

The overlay appears below the minimap in combat. In town, before dealing damage, it shows “Waiting for damage data…” (等待伤害数据…) — that is normal.

### 2.2 In the log

Open this file (paste the path into Explorer's address bar):

```text
%USERPROFILE%\AppData\LocalLow\TEAMHORAY\Sephiria\Player.log
```

Search for `SephiriaDamageMeter`. A successful load looks like:

```text
[AddOnLoader] ✓ 'SephiriaDamageMeter' v1.6.1 ...
```

The mod also writes its own log:

```text
AddOns\SephiriaDamageMeter\damagemeter.log
```

It records load, UI creation and config reloads, which helps when troubleshooting.

---

## 3. What the overlay looks like

### 3.1 Solo

One player block below the minimap:

- **Header**: avatar, player name, current-location / whole-run total, and a “本层” (location) or “全程” (run) badge on the right.
- **Rows**: damage sources sorted from highest to lowest. Each row shows an icon, name, total damage and the source's share of your total.
- Sources beyond the row limit fold into “另有 N 项” (N more) with their combined damage.

### 3.2 Multiplayer

With two or more players the overlay switches to one block per player:

- Blocks are sorted by total damage.
- Each header shows avatar, `P1 name` (player slot + name), total damage and team share.
- Each block lists that player's top 4 sources (configurable).
- Excess players fold into “另有 N 名玩家” (N more players).

### 3.3 Post-battle summary

Press `F7`. Each player gets a column with:

- Name, current-location / whole-run total, team share and DPS.
- That player's top sources with values and percentages.
- Press `F8` while the summary is open to switch between current location and whole run; the title changes accordingly.
- After leaving a floor, `F7` shows the cached “previous location” summary.

### 3.4 What “本层 / current location” means

“本层” means the **current map / floor instance**, not a biome such as Library, Grassland or Moleland:

- Moving between rooms, fighting and opening chests on the same map keeps the same location.
- Taking stairs, a portal or the world map into a new map resets the location statistics.
- Returning to town / the MultiZone lobby also starts a new location.

---

## 4. In-game controls

| Key | Action |
| --- | --- |
| `F7` | Open / close the post-battle summary |
| `F8` | Toggle current-location / whole-run (also works in the summary) |
| `F9` | Show / hide the overlay |
| `F10` | Reload `config.json` (no restart needed) |

**Dragging**:

- Hold the top header of the overlay (avatar / name / total) and drag to move it.
- The position is saved to `config.json` and persists across restarts.
- Double-click the header to return to following the minimap.

**Note**: dragging uses raw mouse input and does not add a UI raycast target, so it does not block the game's clicks like a normal UI panel. The initial click may still be treated as one attack by the game.

---

## 5. Position and adjustment

### 5.1 Default position

Below the minimap, right-aligned to it.

### 5.2 Fine-tuning

Instead of dragging, edit `config.json`:

- `mapGap`: distance from the minimap's bottom edge.
- `offsetX` / `offsetY`: overall offset.
- `screenMargin`: minimum distance from the screen edge.

Save and press `F10` to apply.

### 5.3 Reset the position

Double-click the overlay header, or set `manualPanelX` and `manualPanelY` back to `-99999` in `config.json`.

---

## 6. Configuration (config.json)

The file is at:

```text
AddOns\SephiriaDamageMeter\config.json
```

Open it with Notepad, edit, save, then press `F10` in game. No restart is needed.

### 6.1 Display

| Field | Default | Description |
| --- | --- | --- |
| `enabled` | `true` | Master switch |
| `defaultScope` | `Location` | Default scope: `Location` or `Run` |
| `showAllPlayersInMultiplayer` | `true` | Show all players in co-op; `false` = only yourself |
| `maxSourceRowsSolo` | `8` | Max source rows in solo |
| `partySourcesPerPlayer` | `4` | Top sources per player block in co-op |
| `maxPlayerBlocks` | `4` | Max player blocks in co-op |
| `showIcons` | `true` | Show source icons |
| `showBars` | `true` | Show ratio bars |
| `showPercentages` | `true` | Show per-row percentages |
| `animateBars` | `true` | Animate bar growth |

### 6.2 Appearance

| Field | Default | Description |
| --- | --- | --- |
| `panelWidth` | `280` | Overlay width |
| `fontSize` | `14` | Base font scale; 14 = 1.0× |
| `fontName` | `""` | Font asset name; empty = follow game language |
| `textMaterialStyle` | `Outlined` | Font material: `Outlined` / `Default` / `Shadow` |
| `totalBarColor` | `#3D4F70` | Solo total-bar colour (co-op uses player colours) |
| `backgroundOpacity` | `0.22` | Backing opacity; 0 = off |

### 6.3 Position

| Field | Default | Description |
| --- | --- | --- |
| `mapGap` | `8` | Gap below the minimap |
| `offsetX` / `offsetY` | `0` | Offset when not manually positioned |
| `manualPanelX` / `manualPanelY` | `-99999` | Dragged position; `-99999` = follow minimap |
| `screenMargin` | `14` | Minimum distance from screen edges |

### 6.4 Post-battle summary

| Field | Default | Description |
| --- | --- | --- |
| `hotkeyToggleSummary` | `F7` | Summary hotkey |
| `autoOpenSummaryOnBattleEnd` | `false` | Auto-open the summary when combat ends |
| `summaryMaxSources` | `8` | Max sources per player in the summary |
| `summaryMaxPlayers` | `4` | Max player columns |
| `dpsUseBattleTime` | `true` | DPS uses combat time; `false` uses elapsed time |
| `refreshInterval` | `0.35` | Data refresh interval (seconds) |

### 6.5 Visibility

| Field | Default | Description |
| --- | --- | --- |
| `hideWhenHudHidden` | `true` | Hide with the game HUD |
| `hideOutsideBattle` | `false` | Only show during combat |
| `hideWhenEmpty` | `false` | Hide when the current scope has no damage; default keeps “Waiting for damage data…” |

### 6.6 Hotkeys

| Field | Default | Description |
| --- | --- | --- |
| `hotkeyToggleScope` | `F8` | Toggle location / run |
| `hotkeyToggleVisible` | `F9` | Show / hide |
| `hotkeyReloadConfig` | `F10` | Reload config |
| `hotkeyToggleSummary` | `F7` | Summary |

Hotkeys use Unity Input System key names, such as `F7`, `F8`, `Tab`, `Backquote`.

---

## 7. Common configurations

### Make it more readable

```json
"fontSize": 16,
"panelWidth": 320,
"backgroundOpacity": 0.35
```

### Make it smaller

```json
"panelWidth": 220,
"maxSourceRowsSolo": 5,
"partySourcesPerPlayer": 3,
"backgroundOpacity": 0.12
```

### Only show during combat

```json
"hideOutsideBattle": true
```

### Only show your own data in co-op

```json
"showAllPlayersInMultiplayer": false
```

### Auto-open the summary after combat

```json
"autoOpenSummaryOnBattleEnd": true
```

### Change the total-bar colour

```json
"totalBarColor": "#5A3B6E"
```

`#RRGGBB` format. `totalBarColor` affects solo only; co-op uses each player's colour.

---

## 8. FAQ

### The overlay does not appear

1. Check for a double-nested `SephiriaDamageMeter` folder.
2. Press `F9` to make sure it is not hidden.
3. In town with no damage it shows “Waiting for damage data…”; it fills in after combat.
4. Search `Player.log` for `[AddOnLoader] ✓ 'SephiriaDamageMeter'`.

### Chinese text shows as boxes

Leave `fontName` empty (`""`) or set it to `Fusion_Pixel_10_Simple`, save, and press `F10`.

### Teammates' damage is missing

Their data is replicated by the server. Make sure `showAllPlayersInMultiplayer` is `true`; a mod that blocks sync may prevent it.

### DPS looks too high or too low

DPS = damage ÷ combat time. To use time spent in the location instead, set `dpsUseBattleTime` to `false`.

### Does damage data survive quitting the game?

- The game writes `dealsStatistics` and `dealsStatistics_LastLocation` into the run save at save points such as floor transitions, and restores them when loading.
- Data is kept if the game saved before you quit. Damage dealt after the last save but before quitting may be lost — this is the game's save behavior, not the mod.
- The mod's DPS clock is not saved. After loading, DPS shows `— DPS` until a new floor / run accumulates combat time.

### Does teammate reconnect refresh the data?

- There is **no code-level integration** with the reconnect mod; both read the game's own player statistics.
- When a teammate disconnects, the game writes their session data into the current run save; on reconnect `PlayerSpawner` restores `dealsStatistics`, and this mod shows it again on the next refresh (about 0.35 s).
- If the host uses “restore this floor / checkpoint restore”, data returns to the checkpoint snapshot. The reconnect mod tries to refresh the snapshot from the current run first, but damage after the checkpoint that was never saved may be rolled back.
- After a reconnect or checkpoint restore, DPS shows `— DPS` until combat time accumulates again.

### The dragged position is not ideal

Double-click the header to follow the minimap again, or set `manualPanelX` / `manualPanelY` to `-99999`.

---

## 9. Uninstall

Delete this folder:

```text
AddOns\SephiriaDamageMeter
```

---

## 10. Multiplayer

- Each player can install independently; everyone does not need the mod.
- Damage data is replicated by the game server, so any client with the mod can read the whole team's data locally.
- Using the same version on all clients is recommended.
- Compatible with larger-lobby mods such as SephiriaUnlocker.

---

## Data notes

- Data comes from the game's own `dealsStatistics` (whole run) and `dealsStatistics_LastLocation` (current location) — the same values the game's result screen uses.
- Only effective HP damage counts; damage absorbed by shields is excluded.
- The same source with different elements appears as separate rows; companions are tracked under `Companion_*`.
- Names, icons and element colours come from the game's `DamageIdEntity` database and follow the game language.
