using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SephiriaDamageMeter
{
    /// <summary>
    /// Lives for the whole process (DontDestroyOnLoad), polls the game's synced
    /// per-player damage dictionaries and feeds the overlay UI.
    /// </summary>
    public sealed class DamageMeterController : MonoBehaviour
    {
        public static DamageMeterController Instance { get; private set; }

        private DamageMeterConfig _config;
        private DamageMeterUI _ui;
        private DamageStatsScope _scope;
        private bool _userVisible = true;
        private float _nextRefresh;
        private float _nextMapSearch;
        private UI_HUDMapViewer _mapViewer;
        private int _lastSignature = int.MinValue;
        private bool _built;
        private bool _modDirectoryWarned;
        private bool _fontWaitLogged;
        private float _nextUiRetry;

        private readonly DamageSnapshot _snapshot = new DamageSnapshot();
        private readonly SummarySnapshot _summary = new SummarySnapshot();
        private readonly SummarySnapshot _cachedSummary = new SummarySnapshot();
        private readonly List<PlayerAvatar> _players = new List<PlayerAvatar>();
        private readonly Dictionary<PlayerAvatar, int> _slots = new Dictionary<PlayerAvatar, int>();

        private bool _summaryVisible;
        private bool _autoSummaryOpened;
        private bool _autoSummaryShown;
        private bool _wasInBattle;
        private float _locationStartTime;
        private float _lastLocalLocationTotal;
        private float _runStartTime;
        private float _lastLocalRunTotal;
        private PlayerAvatar _lastLocalPlayer;
        private float _battleStartTime;
        private float _battleAccumulated;
        private float _runBattleAccumulated;
        private bool _clockInitializedForLocal;
        private bool _locationStatsCarriedOver;
        private bool _runStatsCarriedOver;

        private void Awake()
        {
            Instance = this;
            LoadConfig();
            _scope = _config.UseRunScopeByDefault ? DamageStatsScope.Run : DamageStatsScope.Location;
        }

        private void Start()
        {
            BuildUi();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            DestroyUi();
        }

        private void Update()
        {
            try
            {
                HandleHotkeys();
                if (!_built && Time.unscaledTime >= _nextUiRetry)
                {
                    _nextUiRetry = Time.unscaledTime + 1f;
                    BuildUi();
                }
                if (_ui != null)
                {
                    _ui.HandleDrag(_config);
                    _ui.Tick(Time.unscaledDeltaTime);
                }
                if (Time.unscaledTime >= _nextRefresh)
                {
                    _nextRefresh = Time.unscaledTime + Mathf.Max(0.1f, _config.refreshInterval);
                    Refresh();
                }
            }
            catch (Exception ex)
            {
                DamageMeterLog.ErrorThrottled("update", "Update failed: " + ex, 10f);
            }
        }

        private void LoadConfig()
        {
            string directory = DamageMeterLog.ModDirectory;
            if (string.IsNullOrEmpty(directory))
            {
                if (!_modDirectoryWarned)
                {
                    _modDirectoryWarned = true;
                    DamageMeterLog.Warn("Mod directory unknown; using defaults.");
                }
                directory = Environment.CurrentDirectory;
            }
            _config = DamageMeterConfig.Load(directory);
        }

        private void BuildUi()
        {
            if (!DamageMeterUI.HasGameFont())
            {
                if (!_fontWaitLogged)
                {
                    _fontWaitLogged = true;
                    DamageMeterLog.Info("Waiting for the game's TMP font assets to load ...");
                }
                return;
            }

            DestroyUi();
            try
            {
                _ui = new DamageMeterUI(_config);
                _built = true;
                _lastSignature = int.MinValue;
                DamageMeterLog.Info("UI created with game font (" + Screen.width + "x" + Screen.height + ").");
            }
            catch (Exception ex)
            {
                _built = false;
                DamageMeterLog.Error("Failed to create UI: " + ex);
            }
        }

        private void DestroyUi()
        {
            if (_ui != null)
            {
                _ui.Destroy();
                _ui = null;
            }
            _built = false;
        }

        private void HandleHotkeys()
        {
            if (WasKeyPressed(_config.hotkeyToggleSummary))
            {
                _summaryVisible = !_summaryVisible;
                _autoSummaryOpened = false;
                if (_summaryVisible && _built)
                {
                    PlayerAvatar local = CollectPlayersAndSnapshot();
                    UpdateLocationClock(local);
                    BuildSummary(true);
                    _ui.SetSummaryVisible(true, _summary, _config, _scope);
                }
                else if (_ui != null)
                {
                    _ui.SetSummaryVisible(false, null, _config, _scope);
                }
            }
            if (WasKeyPressed(_config.hotkeyToggleScope))
            {
                _scope = _scope == DamageStatsScope.Location ? DamageStatsScope.Run : DamageStatsScope.Location;
                _lastSignature = int.MinValue;
                _nextRefresh = 0f;
            }
            if (WasKeyPressed(_config.hotkeyToggleVisible))
            {
                _userVisible = !_userVisible;
                _nextRefresh = 0f;
            }
            if (WasKeyPressed(_config.hotkeyReloadConfig))
            {
                DamageMeterLog.Info("Reloading config.json ...");
                LoadConfig();
                _scope = _config.UseRunScopeByDefault ? DamageStatsScope.Run : DamageStatsScope.Location;
                BuildUi();
                _nextRefresh = 0f;
            }
        }

        private static bool WasKeyPressed(string keyName)
        {
            if (string.IsNullOrEmpty(keyName))
            {
                return false;
            }
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }
            Key key;
            if (!Enum.TryParse<Key>(keyName, true, out key) || key == Key.None)
            {
                return false;
            }
            UnityEngine.InputSystem.Controls.KeyControl control = keyboard[key];
            return control != null && control.wasPressedThisFrame;
        }

        private void Refresh()
        {
            if (!_built)
            {
                return;
            }

            PlayerAvatar local = CollectPlayersAndSnapshot();

            bool hudHidden = UIManager.Instance != null && UIManager.Instance.IsHidden;

            UpdateLocationClock(local);

            bool inBattle = _snapshot.LocalInBattle;
            if (inBattle)
            {
                _autoSummaryShown = false;
                if (_summaryVisible && _autoSummaryOpened)
                {
                    _summaryVisible = false;
                    _autoSummaryOpened = false;
                }
            }
            else if (_config.autoOpenSummaryOnBattleEnd
                     && _wasInBattle
                     && !_autoSummaryShown
                     && _lastLocalLocationTotal > 0.01f)
            {
                _summaryVisible = true;
                _autoSummaryOpened = true;
                _autoSummaryShown = true;
            }
            _wasInBattle = inBattle;

            bool summaryAllowed = _summaryVisible && local != null
                                  && !(_config.hideWhenHudHidden && hudHidden);
            if (summaryAllowed)
            {
                BuildSummary(true);
                _ui.SetSummaryVisible(true, _summary, _config, _scope);
            }
            else
            {
                _ui.SetSummaryVisible(false, null, _config, _scope);
            }

            bool show = _config.enabled && _userVisible && local != null;
            if (show && _config.hideWhenHudHidden && hudHidden)
            {
                show = false;
            }
            if (show && _config.hideOutsideBattle && !_snapshot.LocalInBattle)
            {
                show = false;
            }
            // In town, before any fight, the dictionaries are empty. Keep the
            // panel out of the way until there is something to show.
            if (show && _config.hideWhenEmpty && _snapshot.Total <= 0.01f && !_snapshot.LocalInBattle)
            {
                show = false;
            }

            if (!show)
            {
                _ui.SetVisible(false);
                return;
            }

            int signature = ComputeSignature();
            if (signature != _lastSignature)
            {
                _lastSignature = signature;
                _ui.ApplyData(_snapshot, _scope, _config);
            }

            _ui.SetVisible(true);
            _ui.Position(_config, FindMapViewer());
        }

        /// <summary>
        /// Fills _snapshot from every PlayerAvatar's Mirror SyncDictionary.
        /// The dictionaries are written by the server (PlayerAvatar.AddDealStat)
        /// and replicated to every client, so this works in solo and in co-op
        /// without any custom network messages.
        /// </summary>
        private PlayerAvatar CollectPlayersAndSnapshot()
        {
            _snapshot.Clear();
            _players.Clear();

            List<PlayerSpawner> list = PlayerSpawner.MultiplayerList;
            if (list != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    PlayerSpawner spawner = list[i];
                    if (spawner == null)
                    {
                        continue;
                    }
                    PlayerAvatar avatar = spawner.PlayerAvatar;
                    if (avatar == null || _players.Contains(avatar))
                    {
                        continue;
                    }
                    _players.Add(avatar);
                }
            }

            if (_players.Count == 0 && CombatManager.Instance != null && CombatManager.Instance.CurrentPlayer != null)
            {
                _players.Add(CombatManager.Instance.CurrentPlayer);
            }

            PlayerAvatar local = FindLocalPlayer();
            bool party = _config.showAllPlayersInMultiplayer && _players.Count > 1;

            AssignSlots();
            SortPlayersBySlot();

            int maxSources = party ? _config.partySourcesPerPlayer : _config.maxSourceRowsSolo;
            float teamTotal = 0f;
            List<PlayerBlockData> built = new List<PlayerBlockData>();

            for (int i = 0; i < _players.Count; i++)
            {
                PlayerAvatar player = _players[i];
                if (player == null)
                {
                    continue;
                }
                if (!party && player != local)
                {
                    continue;
                }

                int slot = _slots.ContainsKey(player) ? _slots[player] : i;
                float playerTotal = 0f;
                List<DamageRowData> allSources = new List<DamageRowData>();

                IReadOnlyDictionary<DamageKey, float> stats =
                    _scope == DamageStatsScope.Run
                        ? (IReadOnlyDictionary<DamageKey, float>)player.dealsStatistics
                        : (IReadOnlyDictionary<DamageKey, float>)player.dealsStatistics_LastLocation;

                if (stats != null)
                {
                    foreach (KeyValuePair<DamageKey, float> pair in stats)
                    {
                        float value = pair.Value;
                        if (value <= 0.01f)
                        {
                            continue;
                        }
                        playerTotal += value;
                        allSources.Add(new DamageRowData
                        {
                            Id = pair.Key.Id,
                            Element = pair.Key.ElementalType,
                            Value = value,
                            Percent = 0f
                        });
                    }
                }

                if (playerTotal <= 0.01f)
                {
                    if (party)
                    {
                        built.Add(new PlayerBlockData
                        {
                            Name = GetPlayerName(player, slot),
                            Slot = slot,
                            Icon = player.MiniFaceChipSprite,
                            Total = 0f,
                            IsLocal = player == local
                        });
                    }
                    continue;
                }

                allSources.Sort(delegate(DamageRowData a, DamageRowData b)
                {
                    int byValue = b.Value.CompareTo(a.Value);
                    if (byValue != 0)
                    {
                        return byValue;
                    }
                    return string.CompareOrdinal(a.Id, b.Id);
                });

                PlayerBlockData block = new PlayerBlockData
                {
                    Name = GetPlayerName(player, slot),
                    Slot = slot,
                    Icon = player.MiniFaceChipSprite,
                    Total = playerTotal,
                    IsLocal = player == local
                };

                int shown = Mathf.Min(allSources.Count, maxSources);
                for (int s = 0; s < shown; s++)
                {
                    DamageRowData row = allSources[s];
                    row.Percent = playerTotal > 0.01f ? row.Value / playerTotal : 0f;
                    block.Sources.Add(row);
                }
                if (allSources.Count > shown)
                {
                    block.HiddenSourceCount = allSources.Count - shown;
                    for (int s = shown; s < allSources.Count; s++)
                    {
                        block.HiddenTotal += allSources[s].Value;
                    }
                }

                built.Add(block);
                teamTotal += playerTotal;
            }

            if (party)
            {
                built.Sort(delegate(PlayerBlockData a, PlayerBlockData b)
                {
                    int byTotal = b.Total.CompareTo(a.Total);
                    if (byTotal != 0)
                    {
                        return byTotal;
                    }
                    return a.Slot.CompareTo(b.Slot);
                });
            }

            int maxBlocks = party ? _config.maxPlayerBlocks : 1;
            if (built.Count > maxBlocks)
            {
                for (int i = maxBlocks; i < built.Count; i++)
                {
                    _snapshot.HiddenPlayerCount++;
                    _snapshot.HiddenPlayerTotal += built[i].Total;
                }
                built.RemoveRange(maxBlocks, built.Count - maxBlocks);
            }

            for (int i = 0; i < built.Count; i++)
            {
                PlayerBlockData block = built[i];
                block.Share = teamTotal > 0.01f ? block.Total / teamTotal : 0f;
                _snapshot.Blocks.Add(block);
            }

            _snapshot.Total = teamTotal;
            _snapshot.Party = party;
            _snapshot.HasLocal = local != null;
            _snapshot.LocalInBattle = local != null && local.IsInBattle;
            _snapshot.LocalPlayerIcon = local != null ? local.MiniFaceChipSprite : null;
            _snapshot.LocalPlayerName = local != null
                ? GetPlayerName(local, _slots.ContainsKey(local) ? _slots[local] : 0)
                : "";
            _snapshot.LocalSlot = local != null && _slots.ContainsKey(local) ? _slots[local] : 0;

            return local;
        }

        private PlayerAvatar FindLocalPlayer()
        {
            for (int i = 0; i < _players.Count; i++)
            {
                PlayerAvatar player = _players[i];
                if (player != null && player.isOwned)
                {
                    return player;
                }
            }

            try
            {
                NetworkIdentity localPlayer = NetworkClient.localPlayer;
                if (localPlayer != null)
                {
                    PlayerSpawner spawner = localPlayer.GetComponent<PlayerSpawner>();
                    if (spawner != null && spawner.PlayerAvatar != null)
                    {
                        return spawner.PlayerAvatar;
                    }
                }
            }
            catch
            {
                // Mirror may not be initialised yet during boot.
            }

            if (CombatManager.Instance != null && CombatManager.Instance.CurrentPlayer != null)
            {
                return CombatManager.Instance.CurrentPlayer;
            }

            return _players.Count > 0 ? _players[0] : null;
        }

        private void AssignSlots()
        {
            _slots.Clear();
            HashSet<int> used = new HashSet<int>();
            for (int i = 0; i < _players.Count; i++)
            {
                PlayerAvatar player = _players[i];
                int raw = GetRawSlot(player);
                if (raw >= 0)
                {
                    used.Add(raw);
                    _slots[player] = raw;
                }
            }
            int next = 0;
            for (int i = 0; i < _players.Count; i++)
            {
                PlayerAvatar player = _players[i];
                if (_slots.ContainsKey(player))
                {
                    continue;
                }
                while (used.Contains(next))
                {
                    next++;
                }
                used.Add(next);
                _slots[player] = next;
            }
        }

        private static int GetRawSlot(PlayerAvatar player)
        {
            if (player == null)
            {
                return -1;
            }
            try
            {
                PlayerSpawner spawner = player.spawner != null ? player.spawner : player.GetComponent<PlayerSpawner>();
                if (spawner != null && spawner.currentPlayerIdx >= 0)
                {
                    return spawner.currentPlayerIdx;
                }
            }
            catch
            {
            }
            return -1;
        }

        private void SortPlayersBySlot()
        {
            _players.Sort(delegate(PlayerAvatar a, PlayerAvatar b)
            {
                int slotA = _slots.ContainsKey(a) ? _slots[a] : int.MaxValue;
                int slotB = _slots.ContainsKey(b) ? _slots[b] : int.MaxValue;
                if (slotA != slotB)
                {
                    return slotA.CompareTo(slotB);
                }
                return a.GetInstanceID().CompareTo(b.GetInstanceID());
            });
        }

        private static string GetPlayerName(PlayerAvatar player, int slot)
        {
            string name = player != null ? player.Name : null;
            if (string.IsNullOrEmpty(name))
            {
                name = "玩家 " + (slot + 1);
            }
            return name;
        }

        private void UpdateLocationClock(PlayerAvatar local)
        {
            float now = Time.unscaledTime;
            bool inBattle = local != null && local.IsInBattle;

            if (local != _lastLocalPlayer)
            {
                _lastLocalPlayer = local;
                _locationStartTime = now;
                _runStartTime = now;
                _battleStartTime = inBattle ? now : 0f;
                _battleAccumulated = 0f;
                _runBattleAccumulated = 0f;
                _clockInitializedForLocal = false;
                _autoSummaryShown = false;
                _cachedSummary.Clear();
            }

            float total = 0f;
            float runTotal = 0f;
            if (local != null)
            {
                try
                {
                    foreach (KeyValuePair<DamageKey, float> pair in local.dealsStatistics_LastLocation)
                    {
                        if (pair.Value > 0.01f)
                        {
                            total += pair.Value;
                        }
                    }
                    foreach (KeyValuePair<DamageKey, float> pair in local.dealsStatistics)
                    {
                        if (pair.Value > 0.01f)
                        {
                            runTotal += pair.Value;
                        }
                    }
                }
                catch
                {
                }
            }

            // A floor change clears the location dictionary; restart the clocks.
            if (_locationStartTime <= 0f
                || (_lastLocalLocationTotal > 1f && total < _lastLocalLocationTotal * 0.5f))
            {
                _locationStartTime = now;
                if (_battleStartTime > 0f)
                {
                    // The current combat segment belongs to the whole run even
                    // though the location clock is restarting.
                    _runBattleAccumulated += now - _battleStartTime;
                }
                _battleAccumulated = 0f;
                _battleStartTime = inBattle ? now : 0f;
                _locationStatsCarriedOver = false;
                _autoSummaryShown = false;
            }
            _lastLocalLocationTotal = total;

            if (_runStartTime <= 0f
                || (_lastLocalRunTotal > 1f && runTotal < _lastLocalRunTotal * 0.5f))
            {
                _runStartTime = now;
                _runBattleAccumulated = 0f;
                _runStatsCarriedOver = false;
                _locationStatsCarriedOver = false;
                _cachedSummary.Clear();
            }
            _lastLocalRunTotal = runTotal;

            if (!_clockInitializedForLocal && local != null)
            {
                _clockInitializedForLocal = true;
                // Stats loaded from a save have no matching combat clock; DPS
                // is shown as "—" until the relevant scope resets.
                _locationStatsCarriedOver = total > 0.01f;
                _runStatsCarriedOver = runTotal > 0.01f;
            }

            if (inBattle)
            {
                if (_battleStartTime <= 0f)
                {
                    _battleStartTime = now;
                }
            }
            else if (_battleStartTime > 0f)
            {
                float elapsed = now - _battleStartTime;
                _battleAccumulated += elapsed;
                _runBattleAccumulated += elapsed;
                _battleStartTime = 0f;
            }
        }

        /// <summary>
        /// Builds the per-player post-battle breakdown from the synced
        /// dealsStatistics_LastLocation dictionaries. The last non-empty
        /// snapshot is cached so F7 still works right after leaving a floor.
        /// </summary>
        private void BuildSummary(bool allowCache)
        {
            _summary.Clear();

            float now = Time.unscaledTime;
            float currentBattle = _battleStartTime > 0f ? now - _battleStartTime : 0f;
            float battleDuration = (_scope == DamageStatsScope.Run ? _runBattleAccumulated : _battleAccumulated)
                                   + currentBattle;
            float duration;
            if (_config.dpsUseBattleTime && battleDuration > 1f)
            {
                duration = battleDuration;
            }
            else if (_scope == DamageStatsScope.Run)
            {
                duration = Mathf.Max(3f, _runStartTime > 0f ? now - _runStartTime : 3f);
            }
            else
            {
                duration = Mathf.Max(3f, _locationStartTime > 0f ? now - _locationStartTime : 3f);
            }
            float teamTotal = 0f;

            for (int i = 0; i < _players.Count; i++)
            {
                PlayerAvatar player = _players[i];
                if (player == null)
                {
                    continue;
                }

                float total = 0f;
                List<SummarySourceData> sources = new List<SummarySourceData>();
                IReadOnlyDictionary<DamageKey, float> stats =
                    _scope == DamageStatsScope.Run
                        ? (IReadOnlyDictionary<DamageKey, float>)player.dealsStatistics
                        : (IReadOnlyDictionary<DamageKey, float>)player.dealsStatistics_LastLocation;
                try
                {
                    foreach (KeyValuePair<DamageKey, float> pair in stats)
                    {
                        float value = pair.Value;
                        if (value <= 0.01f)
                        {
                            continue;
                        }
                        total += value;
                        sources.Add(new SummarySourceData
                        {
                            Id = pair.Key.Id,
                            Element = pair.Key.ElementalType,
                            Value = value
                        });
                    }
                }
                catch
                {
                }

                if (total <= 0.01f)
                {
                    continue;
                }

                int slot = _slots.ContainsKey(player) ? _slots[player] : i;
                bool dpsKnown = _scope == DamageStatsScope.Run
                    ? !_runStatsCarriedOver
                    : !_locationStatsCarriedOver;
                SummaryPlayerData data = new SummaryPlayerData
                {
                    Name = GetPlayerName(player, slot),
                    Slot = slot,
                    Total = total,
                    Dps = dpsKnown ? total / duration : -1f
                };

                sources.Sort(delegate(SummarySourceData a, SummarySourceData b)
                {
                    int byValue = b.Value.CompareTo(a.Value);
                    if (byValue != 0)
                    {
                        return byValue;
                    }
                    return string.CompareOrdinal(a.Id, b.Id);
                });
                for (int s = 0; s < sources.Count; s++)
                {
                    SummarySourceData source = sources[s];
                    source.Percent = source.Value / total;
                    data.Sources.Add(source);
                }

                _summary.Players.Add(data);
                teamTotal += total;
            }

            _summary.Players.Sort(delegate(SummaryPlayerData a, SummaryPlayerData b)
            {
                int byTotal = b.Total.CompareTo(a.Total);
                if (byTotal != 0)
                {
                    return byTotal;
                }
                return a.Slot.CompareTo(b.Slot);
            });

            _summary.TeamTotal = teamTotal;
            for (int i = 0; i < _summary.Players.Count; i++)
            {
                SummaryPlayerData player = _summary.Players[i];
                player.Share = teamTotal > 0.01f ? player.Total / teamTotal : 0f;
            }
            _summary.FromCache = false;
            _summary.CapturedAt = now;

            if (_scope == DamageStatsScope.Location)
            {
                if (teamTotal > 0.01f)
                {
                    _cachedSummary.CopyFrom(_summary);
                }
                else if (allowCache && _cachedSummary.Players.Count > 0)
                {
                    _summary.CopyFrom(_cachedSummary);
                    _summary.FromCache = true;
                }
            }
        }

        private int ComputeSignature()
        {
            int hash = 17;
            hash = hash * 31 + (_snapshot.Party ? 1 : 0);
            hash = hash * 31 + (int)_scope;
            hash = hash * 31 + Mathf.RoundToInt(_snapshot.Total);
            hash = hash * 31 + _snapshot.HiddenPlayerCount;
            hash = hash * 31 + Mathf.RoundToInt(_snapshot.HiddenPlayerTotal);
            for (int i = 0; i < _snapshot.Blocks.Count; i++)
            {
                PlayerBlockData block = _snapshot.Blocks[i];
                hash = hash * 31 + (block.Name != null ? block.Name.GetHashCode() : 0);
                hash = hash * 31 + Mathf.RoundToInt(block.Total);
                hash = hash * 31 + block.Slot;
                hash = hash * 31 + block.HiddenSourceCount;
                hash = hash * 31 + Mathf.RoundToInt(block.HiddenTotal);
                for (int r = 0; r < block.Sources.Count; r++)
                {
                    DamageRowData row = block.Sources[r];
                    hash = hash * 31 + (row.Id != null ? row.Id.GetHashCode() : 0);
                    hash = hash * 31 + (int)row.Element;
                    hash = hash * 31 + Mathf.RoundToInt(row.Value);
                }
            }
            return hash;
        }

        private UI_HUDMapViewer FindMapViewer()
        {
            if (_mapViewer != null)
            {
                return _mapViewer;
            }
            if (Time.unscaledTime < _nextMapSearch)
            {
                return null;
            }
            _nextMapSearch = Time.unscaledTime + 3f;
            try
            {
                UI_HUDMapViewer[] viewers = UnityEngine.Object.FindObjectsByType<UI_HUDMapViewer>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                float bestArea = float.MaxValue;
                UI_HUDMapViewer best = null;
                for (int i = 0; i < viewers.Length; i++)
                {
                    UI_HUDMapViewer viewer = viewers[i];
                    if (viewer == null)
                    {
                        continue;
                    }
                    RectTransform rect = viewer.transform as RectTransform;
                    if (rect == null)
                    {
                        continue;
                    }
                    float area = rect.rect.width * rect.rect.height;
                    if (area > 0f && area < bestArea)
                    {
                        bestArea = area;
                        best = viewer;
                    }
                }
                _mapViewer = best;
            }
            catch (Exception ex)
            {
                DamageMeterLog.ErrorThrottled("map", "HUD map lookup failed: " + ex.Message, 30f);
            }
            return _mapViewer;
        }
    }
}
