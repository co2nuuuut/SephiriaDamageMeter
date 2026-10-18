using System;
using System.IO;
using UnityEngine;

namespace SephiriaDamageMeter
{
    [Serializable]
    public sealed class DamageMeterConfig
    {
        // General
        public bool enabled = true;
        public string defaultScope = "Location"; // Location | Run
        public bool showAllPlayersInMultiplayer = true;

        // Rows / panel
        public int maxSourceRowsSolo = 8;
        public int maxSourceRowsParty = 8;
        public int partySourcesPerPlayer = 4;
        public int maxPlayerBlocks = 4;
        public int maxPlayerLines = 6;
        public float panelWidth = 280f;
        public int fontSize = 14;
        public string fontName = ""; // empty = the game's current-language font
        public string textMaterialStyle = "Outlined";
        public string totalBarColor = "#3D4F70";
        public float backgroundOpacity = 0.22f;
        public bool showIcons = true;
        public bool showBars = true;
        public bool showPercentages = true;
        public bool animateBars = true;

        // Post-battle summary
        public string hotkeyToggleSummary = "F7";
        public bool autoOpenSummaryOnBattleEnd = false;
        public int summaryMaxSources = 8;
        public int summaryMaxPlayers = 4;

        // Visibility
        public bool hideWhenHudHidden = true;
        public bool hideOutsideBattle = false;
        public bool hideWhenEmpty = false;
        public bool dpsUseBattleTime = true;

        // Placement
        public float mapGap = 8f;
        public float offsetX = 0f;
        public float offsetY = 0f;
        public float screenMargin = 14f;
        public float manualPanelX = -99999f;
        public float manualPanelY = -99999f;

        // Hotkeys (Unity Input System Key names)
        public string hotkeyToggleScope = "F8";
        public string hotkeyToggleVisible = "F9";
        public string hotkeyReloadConfig = "F10";

        // Refresh
        public float refreshInterval = 0.35f;

        public bool UseRunScopeByDefault
        {
            get { return !string.IsNullOrEmpty(defaultScope) && defaultScope.ToLowerInvariant() == "run"; }
        }

        public static DamageMeterConfig Load(string modDirectory)
        {
            string path = Path.Combine(modDirectory, "config.json");
            DamageMeterConfig config = new DamageMeterConfig();
            try
            {
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    // FromJsonOverwrite keeps our field initializers for any key missing
                    // from the user's file, instead of zeroing them out.
                    JsonUtility.FromJsonOverwrite(json, config);
                }
                else
                {
                    WriteDefault(path, config);
                }
            }
            catch (Exception ex)
            {
                DamageMeterLog.Warn("读取 config.json 失败，使用默认配置: " + ex.Message);
            }
            config.Normalize();
            return config;
        }

        public static void WriteDefault(string path, DamageMeterConfig config)
        {
            try
            {
                File.WriteAllText(path, JsonUtility.ToJson(config, true));
            }
            catch (Exception ex)
            {
                DamageMeterLog.Warn("写入默认 config.json 失败: " + ex.Message);
            }
        }

        public static void Save(DamageMeterConfig config, string modDirectory)
        {
            if (config == null || string.IsNullOrEmpty(modDirectory))
            {
                return;
            }
            try
            {
                File.WriteAllText(Path.Combine(modDirectory, "config.json"), JsonUtility.ToJson(config, true));
            }
            catch (Exception ex)
            {
                DamageMeterLog.Warn("保存 config.json 失败: " + ex.Message);
            }
        }

        public void Normalize()
        {
            if (panelWidth < 220f) panelWidth = 280f;
            if (panelWidth > 640f) panelWidth = 640f;
            if (fontSize < 10) fontSize = 14;
            if (fontSize > 22) fontSize = 22;
            if (maxSourceRowsSolo < 1) maxSourceRowsSolo = 8;
            if (maxSourceRowsSolo > 30) maxSourceRowsSolo = 30;
            if (maxSourceRowsParty < 1) maxSourceRowsParty = 8;
            if (maxSourceRowsParty > 30) maxSourceRowsParty = 30;
            if (partySourcesPerPlayer < 1) partySourcesPerPlayer = 4;
            if (partySourcesPerPlayer > 8) partySourcesPerPlayer = 8;
            if (maxPlayerBlocks < 1) maxPlayerBlocks = 4;
            if (maxPlayerBlocks > 16) maxPlayerBlocks = 16;
            if (maxPlayerLines < 1) maxPlayerLines = 6;
            if (maxPlayerLines > 24) maxPlayerLines = 24;
            if (refreshInterval < 0.1f) refreshInterval = 0.35f;
            if (refreshInterval > 2f) refreshInterval = 2f;
            if (mapGap < 0f) mapGap = 0f;
            if (screenMargin < 0f) screenMargin = 0f;
            if (backgroundOpacity < 0f) backgroundOpacity = 0f;
            if (backgroundOpacity > 1f) backgroundOpacity = 1f;
            if (summaryMaxSources < 1) summaryMaxSources = 8;
            if (summaryMaxSources > 20) summaryMaxSources = 20;
            if (summaryMaxPlayers < 1) summaryMaxPlayers = 4;
            if (summaryMaxPlayers > 8) summaryMaxPlayers = 8;
            if (string.IsNullOrEmpty(defaultScope)) defaultScope = "Location";
            if (string.IsNullOrEmpty(textMaterialStyle)) textMaterialStyle = "Outlined";
            if (string.IsNullOrEmpty(totalBarColor)) totalBarColor = "#3D4F70";
            if (string.IsNullOrEmpty(hotkeyToggleScope)) hotkeyToggleScope = "F8";
            if (string.IsNullOrEmpty(hotkeyToggleVisible)) hotkeyToggleVisible = "F9";
            if (string.IsNullOrEmpty(hotkeyReloadConfig)) hotkeyReloadConfig = "F10";
            if (string.IsNullOrEmpty(hotkeyToggleSummary)) hotkeyToggleSummary = "F7";
        }
    }
}
