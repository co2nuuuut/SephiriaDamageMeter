using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SephiriaDamageMeter
{
    /// <summary>
    /// Compact Brotato-style overlay: outlined numbers over player/element
    /// coloured bars, no card chrome. No GraphicRaycaster on purpose, so the
    /// panel never eats clicks or attacks.
    /// </summary>
    internal sealed class DamageMeterUI
    {
        private static readonly Color Amber = new Color(0.95f, 0.68f, 0.28f, 0.95f);
        private static readonly Color TextColor = new Color(0.97f, 0.97f, 0.95f, 1f);
        private static readonly Color DimTextColor = new Color(0.86f, 0.88f, 0.90f, 1f);
        private static readonly Color TroughColor = new Color(0f, 0f, 0f, 0.52f);
        private static readonly Color RowTroughColor = new Color(0f, 0f, 0f, 0.40f);

        private static readonly Color[] PlayerColors =
        {
            new Color(1.00f, 0.78f, 0.28f, 1f),
            new Color(0.30f, 0.72f, 0.98f, 1f),
            new Color(0.96f, 0.35f, 0.38f, 1f),
            new Color(0.42f, 0.86f, 0.42f, 1f),
            new Color(0.78f, 0.55f, 0.98f, 1f),
            new Color(0.98f, 0.55f, 0.30f, 1f),
            new Color(0.35f, 0.80f, 0.68f, 1f),
            new Color(0.95f, 0.75f, 0.35f, 1f)
        };

        private readonly DamageMeterConfig _config;
        private readonly float _s;
        private readonly float _totalH;
        private readonly float _playerLineH;
        private readonly float _rowH;
        private readonly float _hiddenRowH;
        private readonly float _pad;
        private readonly float _iconSize;
        private readonly float _iconChipSize;
        private readonly float _panelWidth;

        private GameObject _canvasGo;
        private Canvas _canvas;
        private RectTransform _canvasRect;
        private GameObject _panelGo;
        private RectTransform _panelRect;
        private TMP_FontAsset _font;
        private Material _textMaterial;
        private Material _plainMaterial;
        private float _nativePointSize = 10f;
        private Image _background;

        private TextMeshProUGUI _emptyText;
        private TextMeshProUGUI _partyScopeText;
        private TextMeshProUGUI _hiddenPlayersText;

        private GameObject _summaryGo;
        private RectTransform _summaryRect;
        private TextMeshProUGUI _summaryTitle;
        private TextMeshProUGUI _summarySubtitle;
        private TextMeshProUGUI _summaryEmpty;
        private TextMeshProUGUI _summaryFooter;
        private readonly List<SummaryColumnView> _summaryColumns = new List<SummaryColumnView>();

        private readonly List<PlayerBlockView> _blocks = new List<PlayerBlockView>();
        private readonly Dictionary<string, DamageIdEntity> _entities = new Dictionary<string, DamageIdEntity>();
        private readonly Dictionary<string, string> _names = new Dictionary<string, string>();

        private float _panelHeight = 120f;
        private bool _party;
        private bool _animate;

        private const float ManualSentinel = -99999f;
        private bool _dragging;
        private bool _dragMoved;
        private Vector2 _dragStartScreen;
        private Vector2 _dragStartAnchored;
        private float _lastHeaderPressTime;

        public DamageMeterUI(DamageMeterConfig config)
        {
            _config = config;
            _animate = config.animateBars;
            _s = Mathf.Clamp(config.fontSize / 14f, 0.7f, 1.6f);
            _totalH = 30f * _s;
            _playerLineH = 20f * _s;
            _rowH = 28f * _s;
            _hiddenRowH = 24f * _s;
            _pad = 7f * _s;
            _iconSize = 20f * _s;
            _iconChipSize = 22f * _s;
            _panelWidth = config.panelWidth;

            _font = LoadGameFont(config.fontName);
            if (_font == null)
            {
                DamageMeterLog.Error("No TMP font asset found; text will not render.");
            }
            _nativePointSize = GetNativePointSize(_font);
            _textMaterial = LoadGameMaterial(_font, config.textMaterialStyle);
            _plainMaterial = LoadGameMaterial(_font, "Default");

            CreateCanvas();
            CreatePanel();
        }

        public void Destroy()
        {
            if (_canvasGo != null)
            {
                UnityEngine.Object.Destroy(_canvasGo);
                _canvasGo = null;
            }
            _canvas = null;
            _canvasRect = null;
            _panelGo = null;
            _panelRect = null;
            _font = null;
            _textMaterial = null;
            _plainMaterial = null;
            _background = null;
            _emptyText = null;
            _partyScopeText = null;
            _hiddenPlayersText = null;
            _blocks.Clear();
            _summaryGo = null;
            _summaryRect = null;
            _summaryTitle = null;
            _summarySubtitle = null;
            _summaryEmpty = null;
            _summaryFooter = null;
            _summaryColumns.Clear();
            _entities.Clear();
            _names.Clear();
        }

        public void SetVisible(bool visible)
        {
            if (_panelGo != null && _panelGo.activeSelf != visible)
            {
                _panelGo.SetActive(visible);
            }
        }

        public void Tick(float deltaTime)
        {
            if (_panelGo == null || !_panelGo.activeSelf)
            {
                return;
            }
            float k = _animate ? Mathf.Clamp01(deltaTime * 10f) : 1f;
            for (int b = 0; b < _blocks.Count; b++)
            {
                PlayerBlockView block = _blocks[b];
                if (!block.Root.gameObject.activeSelf)
                {
                    continue;
                }
                block.HeaderCurrent = Mathf.Lerp(block.HeaderCurrent, block.HeaderTarget, k);
                SetFillWidth(block.HeaderFill, block.HeaderCurrent, _panelWidth);
                for (int i = 0; i < block.Rows.Count; i++)
                {
                    RowView row = block.Rows[i];
                    if (!row.Root.gameObject.activeSelf)
                    {
                        continue;
                    }
                    row.Current = Mathf.Lerp(row.Current, row.Target, k);
                    SetFillWidth(row.Fill, row.Current, _panelWidth);
                }
                if (block.HiddenRow.Root.gameObject.activeSelf)
                {
                    RowView row = block.HiddenRow;
                    row.Current = Mathf.Lerp(row.Current, row.Target, k);
                    SetFillWidth(row.Fill, row.Current, _panelWidth);
                }
            }
        }

        public void ApplyData(DamageSnapshot snapshot, DamageStatsScope scope, DamageMeterConfig config)
        {
            if (_panelGo == null)
            {
                return;
            }

            _party = snapshot.Party;
            _animate = config.animateBars;

            int blockCount = snapshot.Blocks.Count;
            EnsureBlocks(blockCount);
            for (int i = blockCount; i < _blocks.Count; i++)
            {
                _blocks[i].Root.gameObject.SetActive(false);
            }

            float y = 0f;
            if (_party)
            {
                _partyScopeText.gameObject.SetActive(true);
                PlaceStretchTop(_partyScopeText.rectTransform, 0f, 0f, y, 14f * _s);
                _partyScopeText.text = scope == DamageStatsScope.Run ? "全程" : "本层";
                _partyScopeText.color = TextColor;
                y += 15f * _s;
            }
            else
            {
                _partyScopeText.gameObject.SetActive(false);
            }

            if (blockCount == 0)
            {
                _emptyText.gameObject.SetActive(true);
                PlaceStretchTop(_emptyText.rectTransform, 0f, 0f, y, _rowH);
                _emptyText.text = "等待伤害数据…";
                y += _rowH;
            }
            else
            {
                _emptyText.gameObject.SetActive(false);
            }

            float maxBlockTotal = 0f;
            for (int i = 0; i < blockCount; i++)
            {
                if (snapshot.Blocks[i].Total > maxBlockTotal)
                {
                    maxBlockTotal = snapshot.Blocks[i].Total;
                }
            }

            for (int i = 0; i < blockCount; i++)
            {
                PlayerBlockData data = snapshot.Blocks[i];
                PlayerBlockView block = _blocks[i];
                block.Root.gameObject.SetActive(true);

                int rows = data.Sources.Count;
                if (!_party && data.HiddenSourceCount > 0)
                {
                    rows++;
                }
                float blockHeight = _totalH + 2f * _s + rows * _rowH + 6f * _s;
                PlaceTopLeft(block.Root, 0f, y, _panelWidth, blockHeight);
                ApplyBlock(block, data, scope, config, maxBlockTotal);
                y += blockHeight;
            }

            if (_party && snapshot.HiddenPlayerCount > 0)
            {
                _hiddenPlayersText.gameObject.SetActive(true);
                PlaceStretchTop(_hiddenPlayersText.rectTransform, 0f, 0f, y, 16f * _s);
                _hiddenPlayersText.text = "另有 " + snapshot.HiddenPlayerCount + " 名玩家 · "
                                          + FormatNumber(snapshot.HiddenPlayerTotal);
                _hiddenPlayersText.color = DimTextColor;
                y += 16f * _s;
            }
            else
            {
                _hiddenPlayersText.gameObject.SetActive(false);
            }

            _panelHeight = y + _pad;
            _panelRect.sizeDelta = new Vector2(_panelWidth, _panelHeight);

            if (!_animate)
            {
                for (int b = 0; b < _blocks.Count; b++)
                {
                    PlayerBlockView block = _blocks[b];
                    block.HeaderCurrent = block.HeaderTarget;
                    SetFillWidth(block.HeaderFill, block.HeaderCurrent, _panelWidth);
                    for (int i = 0; i < block.Rows.Count; i++)
                    {
                        RowView row = block.Rows[i];
                        row.Current = row.Target;
                        SetFillWidth(row.Fill, row.Current, _panelWidth);
                    }
                    if (block.HiddenRow.Root.gameObject.activeSelf)
                    {
                        block.HiddenRow.Current = block.HiddenRow.Target;
                        SetFillWidth(block.HiddenRow.Fill, block.HiddenRow.Current, _panelWidth);
                    }
                }
            }
        }

        public void SetSummaryVisible(bool visible, SummarySnapshot summary, DamageMeterConfig config, DamageStatsScope scope)
        {
            if (!visible)
            {
                if (_summaryGo != null)
                {
                    _summaryGo.SetActive(false);
                }
                return;
            }
            ApplySummary(summary, config, scope);
            if (_summaryGo != null)
            {
                _summaryGo.SetActive(true);
            }
        }

        private void EnsureSummary()
        {
            if (_summaryGo != null)
            {
                return;
            }

            _summaryGo = new GameObject("DamageSummaryPanel",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            _summaryGo.transform.SetParent(_canvasGo.transform, false);
            _summaryRect = _summaryGo.GetComponent<RectTransform>();
            _summaryRect.anchorMin = new Vector2(0.5f, 0.5f);
            _summaryRect.anchorMax = new Vector2(0.5f, 0.5f);
            _summaryRect.pivot = new Vector2(0.5f, 0.5f);
            _summaryRect.anchoredPosition = Vector2.zero;

            Image background = _summaryGo.GetComponent<Image>();
            background.color = new Color(0.02f, 0.025f, 0.035f, 0.95f);
            background.raycastTarget = false;
            Outline outline = _summaryGo.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;

            _summaryTitle = NewText(_summaryGo.transform, "Title", 2.0f,
                TextAlignmentOptions.Center, TextColor, FontStyles.Normal);
            _summarySubtitle = NewText(_summaryGo.transform, "Subtitle", 1.0f,
                TextAlignmentOptions.Center, DimTextColor, FontStyles.Normal, outlined: false);
            _summaryEmpty = NewText(_summaryGo.transform, "Empty", 1.25f,
                TextAlignmentOptions.Center, DimTextColor, FontStyles.Normal, outlined: false);
            _summaryFooter = NewText(_summaryGo.transform, "Footer", 1.0f,
                TextAlignmentOptions.Center, DimTextColor, FontStyles.Normal, outlined: false);

            _summaryGo.SetActive(false);
        }

        private void ApplySummary(SummarySnapshot summary, DamageMeterConfig config, DamageStatsScope scope)
        {
            EnsureSummary();
            if (_summaryGo == null)
            {
                return;
            }

            float pad = 12f * _s;
            float gap = 8f * _s;
            float titleH = 26f * _s;
            float subtitleH = 16f * _s;
            float headerH = 24f * _s;
            float totalH = 30f * _s;
            float statH = 18f * _s;
            float rowH = 24f * _s;
            float footerH = 16f * _s;
            float colW;
            int columns = 0;

            if (summary != null && summary.Players.Count > 0 && summary.TeamTotal > 0.01f)
            {
                columns = Mathf.Min(summary.Players.Count, config.summaryMaxPlayers);
                colW = columns == 1 ? 300f * _s : 220f * _s;
            }
            else
            {
                colW = 300f * _s;
            }

            if (columns == 0)
            {
                float width = 360f * _s;
                float height = 150f * _s;
                _summaryRect.sizeDelta = new Vector2(width, height);
                _summaryRect.anchoredPosition = new Vector2(0f, 8f * _s);
                _summaryTitle.text = scope == DamageStatsScope.Run
                    ? "全程伤害总结"
                    : (summary != null && summary.FromCache ? "上一地点伤害总结" : "本层伤害总结");
                _summarySubtitle.text = scope == DamageStatsScope.Run ? "全程暂无伤害数据" : "本层暂无伤害数据";
                _summarySubtitle.gameObject.SetActive(true);
                _summaryEmpty.gameObject.SetActive(true);
                _summaryEmpty.text = "造成伤害后再按 " + config.hotkeyToggleSummary + " 查看";
                _summaryFooter.text = config.hotkeyToggleSummary + " 关闭 · " + config.hotkeyToggleScope + " 切换本层/全程";
                PlaceStretchTop(_summaryTitle.rectTransform, pad, pad, pad, titleH);
                PlaceStretchTop(_summarySubtitle.rectTransform, pad, pad, pad + titleH, subtitleH);
                PlaceStretchTop(_summaryEmpty.rectTransform, pad, pad, pad + titleH + subtitleH + 12f * _s, 30f * _s);
                PlaceStretchTop(_summaryFooter.rectTransform, pad, pad, height - pad - footerH, footerH);
                for (int i = 0; i < _summaryColumns.Count; i++)
                {
                    _summaryColumns[i].Root.gameObject.SetActive(false);
                }
                return;
            }

            int maxRows = 0;
            for (int i = 0; i < columns; i++)
            {
                int count = summary.Players[i].Sources.Count;
                int shown = Mathf.Min(count, config.summaryMaxSources);
                int rows = shown + (count > shown ? 1 : 0);
                if (rows > maxRows)
                {
                    maxRows = rows;
                }
            }
            float columnHeight = headerH + totalH + statH + 8f * _s + maxRows * rowH;
            float width2 = pad * 2f + columns * colW + (columns - 1) * gap;
            float height2 = pad * 2f + titleH + subtitleH + 6f * _s + columnHeight + footerH;

            _summaryRect.sizeDelta = new Vector2(width2, height2);
            _summaryRect.anchoredPosition = new Vector2(0f, 8f * _s);

            _summaryTitle.text = scope == DamageStatsScope.Run
                ? "全程伤害总结"
                : (summary.FromCache ? "上一地点伤害总结" : "本层伤害总结");
            _summarySubtitle.text = scope == DamageStatsScope.Run ? "按玩家分列 · 全程累计" : "按玩家分列 · 本层有效伤害";
            _summarySubtitle.gameObject.SetActive(true);
            _summaryEmpty.gameObject.SetActive(false);
            _summaryFooter.text = config.hotkeyToggleSummary + " 关闭 · "
                                  + config.hotkeyToggleScope + " 切换本层/全程"
                                  + (summary.Players.Count > columns
                                      ? " · 仅显示伤害前 " + columns + " 名玩家"
                                      : "");
            PlaceStretchTop(_summaryTitle.rectTransform, pad, pad, pad, titleH);
            PlaceStretchTop(_summarySubtitle.rectTransform, pad, pad, pad + titleH, subtitleH);
            PlaceStretchTop(_summaryFooter.rectTransform, pad, pad, height2 - pad - footerH, footerH);

            EnsureSummaryColumns(columns);
            float x = pad;
            float y = pad + titleH + subtitleH + 6f * _s;
            for (int i = 0; i < columns; i++)
            {
                SummaryColumnView column = _summaryColumns[i];
                column.Root.gameObject.SetActive(true);
                PlaceTopLeft(column.Root, x, y, colW, columnHeight);
                ApplySummaryColumn(column, summary.Players[i], config, colW, rowH,
                    headerH, totalH, statH);
                x += colW + gap;
            }
            for (int i = columns; i < _summaryColumns.Count; i++)
            {
                _summaryColumns[i].Root.gameObject.SetActive(false);
            }
        }

        private void EnsureSummaryColumns(int count)
        {
            while (_summaryColumns.Count < count)
            {
                _summaryColumns.Add(CreateSummaryColumn(_summaryColumns.Count));
            }
        }

        private SummaryColumnView CreateSummaryColumn(int index)
        {
            SummaryColumnView view = new SummaryColumnView();
            GameObject root = new GameObject("SummaryColumn" + index, typeof(RectTransform));
            root.transform.SetParent(_summaryGo.transform, false);
            view.Root = root.GetComponent<RectTransform>();

            view.Background = NewImage(root.transform, "Background", new Color(1f, 1f, 1f, 0.045f));
            FillParent(view.Background.rectTransform);

            view.Header = NewText(root.transform, "Header", 1.25f,
                TextAlignmentOptions.Center, TextColor, FontStyles.Normal, outlined: false);
            view.Total = NewText(root.transform, "Total", 2.0f,
                TextAlignmentOptions.Center, TextColor, FontStyles.Normal);
            view.Stats = NewText(root.transform, "Stats", 1.0f,
                TextAlignmentOptions.Center, DimTextColor, FontStyles.Normal, outlined: false);

            view.HiddenRow = CreateSummaryRow(root.transform, 24f * _s);
            view.HiddenRow.Root.gameObject.SetActive(false);
            return view;
        }

        private void ApplySummaryColumn(SummaryColumnView column, SummaryPlayerData player,
            DamageMeterConfig config, float colW, float rowH, float headerH, float totalH, float statH)
        {
            Color playerColor = GetPlayerColor(player.Slot);
            float pad = 12f * _s;
            float titleH = 26f * _s;
            float subtitleH = 16f * _s;

            PlaceStretchTop(column.Header.rectTransform, 0f, 0f, 0f, headerH);
            PlaceStretchTop(column.Total.rectTransform, 0f, 0f, headerH, totalH);
            PlaceStretchTop(column.Stats.rectTransform, 0f, 0f, headerH + totalH, statH);

            column.Header.text = "P" + (player.Slot + 1) + " " + TrimToWidth(player.Name, 12);
            column.Header.color = playerColor;
            column.Total.text = FormatNumber(player.Total);
            column.Total.color = TextColor;
            column.Stats.text = FormatPercent(player.Share) + " · " + FormatDps(player.Dps);
            column.Stats.color = DimTextColor;
            column.Background.color = new Color(playerColor.r, playerColor.g, playerColor.b, 0.06f);

            int shown = Mathf.Min(player.Sources.Count, config.summaryMaxSources);
            int hiddenCount = player.Sources.Count - shown;
            EnsureSummaryRows(column, shown + (hiddenCount > 0 ? 1 : 0));

            float rowY = headerH + totalH + statH + 8f * _s;
            for (int i = 0; i < shown; i++)
            {
                SummaryRowView row = column.Rows[i];
                row.Root.gameObject.SetActive(true);
                PlaceStretchTop(row.Root, 0f, 0f, rowY + i * rowH, rowH);
                ApplySummaryRow(row, player.Sources[i], player.Total, playerColor, colW, rowH);
            }
            for (int i = shown; i < column.Rows.Count; i++)
            {
                column.Rows[i].Root.gameObject.SetActive(false);
            }

            if (hiddenCount > 0)
            {
                float hiddenTotal = 0f;
                for (int i = shown; i < player.Sources.Count; i++)
                {
                    hiddenTotal += player.Sources[i].Value;
                }
                SummaryRowView row = column.HiddenRow;
                row.Root.gameObject.SetActive(true);
                PlaceStretchTop(row.Root, 0f, 0f, rowY + shown * rowH, rowH);
                ApplySummaryHiddenRow(row, hiddenCount, hiddenTotal, player.Total, colW, rowH);
            }
            else
            {
                column.HiddenRow.Root.gameObject.SetActive(false);
            }
        }

        private void EnsureSummaryRows(SummaryColumnView column, int count)
        {
            while (column.Rows.Count < count)
            {
                column.Rows.Add(CreateSummaryRow(column.Root, 24f * _s));
            }
        }

        private SummaryRowView CreateSummaryRow(Transform parent, float rowH)
        {
            SummaryRowView view = new SummaryRowView();
            GameObject root = new GameObject("SummaryRow", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            view.Root = root.GetComponent<RectTransform>();

            view.Trough = NewImage(root.transform, "Trough", RowTroughColor);
            FillParent(view.Trough.rectTransform);

            view.Fill = NewImage(root.transform, "Fill", Color.white);
            RectTransform fillRect = view.Fill.rectTransform;
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = new Vector2(0f, -4f * _s);

            view.IconChip = NewImage(root.transform, "IconChip", Color.white);
            PlaceTopLeft(view.IconChip.rectTransform, 3f * _s, (rowH - 17f * _s) * 0.5f,
                17f * _s, 17f * _s);

            view.Icon = NewImage(root.transform, "Icon", Color.white);
            view.Icon.preserveAspect = true;
            PlaceTopLeft(view.Icon.rectTransform, 4f * _s, (rowH - 15f * _s) * 0.5f,
                15f * _s, 15f * _s);

            view.Strip = NewImage(root.transform, "Strip", Color.white);
            RectTransform stripRect = view.Strip.rectTransform;
            stripRect.anchorMin = new Vector2(0f, 0f);
            stripRect.anchorMax = new Vector2(0f, 1f);
            stripRect.pivot = new Vector2(0f, 0.5f);
            stripRect.anchoredPosition = new Vector2(3f * _s, 0f);
            stripRect.sizeDelta = new Vector2(3f * _s, -8f * _s);

            view.Name = NewText(root.transform, "Name", 1.5f,
                TextAlignmentOptions.MidlineLeft, TextColor, FontStyles.Normal, outlined: false);
            view.Value = NewText(root.transform, "Value", 1.5f,
                TextAlignmentOptions.MidlineRight, TextColor, FontStyles.Normal);
            view.Percent = NewText(root.transform, "Percent", 1.25f,
                TextAlignmentOptions.MidlineRight, DimTextColor, FontStyles.Normal, outlined: false);
            return view;
        }

        private void ApplySummaryRow(SummaryRowView row, SummarySourceData source, float playerTotal,
            Color playerColor, float colW, float rowH)
        {
            Color elementColor = GetElementColor(source.Element);
            Color valueColor = Color.Lerp(elementColor, Color.white, 0.30f);
            row.Fill.color = new Color(playerColor.r, playerColor.g, playerColor.b, 0.30f);

            DamageIdEntity entity = GetEntity(source.Id);
            bool hasIcon = _config.showIcons && entity != null && entity.icon != null;
            row.IconChip.gameObject.SetActive(hasIcon);
            row.Icon.gameObject.SetActive(hasIcon);
            row.Strip.gameObject.SetActive(!hasIcon);

            float nameX;
            if (hasIcon)
            {
                row.IconChip.color = new Color(elementColor.r, elementColor.g, elementColor.b, 0.30f);
                row.Icon.sprite = entity.icon;
                nameX = 23f * _s;
            }
            else
            {
                row.Strip.color = elementColor;
                nameX = 9f * _s;
            }

            float valueWidth = 58f * _s;
            float percentWidth = 32f * _s;
            RectTransform nameRect = row.Name.rectTransform;
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(0f, 1f);
            nameRect.offsetMin = new Vector2(nameX, -rowH);
            nameRect.offsetMax = new Vector2(-(valueWidth + percentWidth + 4f * _s), 0f);
            float available = Mathf.Max(24f, colW - nameX - valueWidth - percentWidth - 6f * _s);
            int maxChars = Mathf.Max(3, Mathf.FloorToInt(available / Mathf.Max(5f, 10f * _s * 0.95f)));
            row.Name.text = TrimToWidth(ResolveName(source.Id), maxChars);
            row.Name.color = TextColor;

            row.Value.text = FormatNumber(source.Value);
            row.Value.color = valueColor;
            PlaceTopRight(row.Value.rectTransform, 3f * _s + percentWidth, 0f, valueWidth, rowH);
            row.Percent.text = FormatPercent(source.Percent);
            row.Percent.color = DimTextColor;
            PlaceTopRight(row.Percent.rectTransform, 3f * _s, 0f, percentWidth, rowH);

            float ratio = playerTotal > 0.01f ? Mathf.Clamp01(source.Value / playerTotal) : 0f;
            SetFillWidth(row.Fill, ratio, colW);
        }

        private void ApplySummaryHiddenRow(SummaryRowView row, int hiddenCount, float hiddenTotal,
            float playerTotal, float colW, float rowH)
        {
            row.Fill.color = new Color(1f, 1f, 1f, 0.14f);
            row.IconChip.gameObject.SetActive(false);
            row.Icon.gameObject.SetActive(false);
            row.Strip.gameObject.SetActive(false);

            float valueWidth = 58f * _s;
            float percentWidth = 32f * _s;
            RectTransform nameRect = row.Name.rectTransform;
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(0f, 1f);
            nameRect.offsetMin = new Vector2(9f * _s, -rowH);
            nameRect.offsetMax = new Vector2(-(valueWidth + percentWidth + 4f * _s), 0f);
            row.Name.text = "另有 " + hiddenCount + " 项";
            row.Name.color = DimTextColor;
            row.Value.text = FormatNumber(hiddenTotal);
            row.Value.color = DimTextColor;
            PlaceTopRight(row.Value.rectTransform, 3f * _s + percentWidth, 0f, valueWidth, rowH);
            float ratio = playerTotal > 0.01f ? Mathf.Clamp01(hiddenTotal / playerTotal) : 0f;
            row.Percent.text = FormatPercent(ratio);
            row.Percent.color = DimTextColor;
            PlaceTopRight(row.Percent.rectTransform, 3f * _s, 0f, percentWidth, rowH);
            SetFillWidth(row.Fill, ratio, colW);
        }

        private static string FormatDps(float dps)
        {
            if (dps < 0f)
            {
                return "— DPS";
            }
            if (dps < 0.5f)
            {
                return "0 DPS";
            }
            if (dps < 1f)
            {
                return "<1 DPS";
            }
            return FormatNumber(dps) + " DPS";
        }

        public void Position(DamageMeterConfig config, UI_HUDMapViewer mapViewer)
        {
            if (_panelGo == null || !_panelGo.activeSelf || _canvasRect == null)
            {
                return;
            }

            if (config.manualPanelX > -50000f && config.manualPanelY > -50000f)
            {
                _panelRect.anchoredPosition = ClampAnchoredPosition(
                    new Vector2(config.manualPanelX, config.manualPanelY), config);
                return;
            }

            float screenW = Screen.width;
            float screenH = Screen.height;
            float scale = _canvas != null && _canvas.scaleFactor > 0.01f ? _canvas.scaleFactor : 1f;
            float panelWpx = _panelWidth * scale;
            float panelHpx = _panelHeight * scale;

            float left;
            float top;

            RectTransform mapRect = GetMapRect(mapViewer);
            if (mapRect != null)
            {
                Vector3[] corners = new Vector3[4];
                mapRect.GetWorldCorners(corners);
                Canvas mapCanvas = mapRect.GetComponentInParent<Canvas>();
                Camera camera = GetCanvasCamera(mapCanvas);
                Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
                Vector2 bottomRight = RectTransformUtility.WorldToScreenPoint(camera, corners[3]);
                // Hug the map's right edge so the overlay stays on the right
                // side of the screen regardless of panel width.
                left = bottomRight.x - panelWpx + config.offsetX;
                top = bottomLeft.y - config.mapGap + config.offsetY;
            }
            else
            {
                left = screenW - panelWpx - 24f + config.offsetX;
                top = screenH * 0.62f + config.offsetY;
            }

            float margin = config.screenMargin;
            left = Mathf.Clamp(left, margin, Mathf.Max(margin, screenW - panelWpx - margin));
            if (top - panelHpx < margin)
            {
                top = margin + panelHpx;
            }
            if (top > screenH - margin)
            {
                top = screenH - margin;
            }

            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvasRect, new Vector2(left, top), null, out local))
            {
                _panelRect.anchoredPosition = local;
            }
        }

        /// <summary>
        /// Drag by the first player-block header. Uses raw Input System
        /// polling instead of a GraphicRaycaster so the game's own
        /// "pointer over UI" checks never treat the overlay as a UI hit.
        /// </summary>
        public void HandleDrag(DamageMeterConfig config)
        {
            if (_panelGo == null || !_panelGo.activeSelf || _canvasRect == null)
            {
                return;
            }
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }
            Vector2 screen = mouse.position.ReadValue();

            if (mouse.leftButton.wasPressedThisFrame && IsInHeader(screen))
            {
                float now = Time.unscaledTime;
                if (now - _lastHeaderPressTime < 0.35f)
                {
                    // Double-click the header to go back to following the minimap.
                    _lastHeaderPressTime = 0f;
                    _dragging = false;
                    config.manualPanelX = ManualSentinel;
                    config.manualPanelY = ManualSentinel;
                    DamageMeterConfig.Save(config, DamageMeterLog.ModDirectory);
                    return;
                }
                _lastHeaderPressTime = now;
                _dragging = true;
                _dragMoved = false;
                _dragStartScreen = screen;
                _dragStartAnchored = _panelRect.anchoredPosition;
            }

            if (!_dragging)
            {
                return;
            }

            if (mouse.leftButton.isPressed)
            {
                Vector2 delta = screen - _dragStartScreen;
                if (!_dragMoved && delta.sqrMagnitude > 9f)
                {
                    _dragMoved = true;
                }
                if (_dragMoved)
                {
                    float scale = _canvas != null && _canvas.scaleFactor > 0.01f ? _canvas.scaleFactor : 1f;
                    Vector2 next = ClampAnchoredPosition(_dragStartAnchored + delta / scale, config);
                    _panelRect.anchoredPosition = next;
                    config.manualPanelX = next.x;
                    config.manualPanelY = next.y;
                }
            }
            else
            {
                _dragging = false;
                if (_dragMoved)
                {
                    _dragMoved = false;
                    DamageMeterConfig.Save(config, DamageMeterLog.ModDirectory);
                }
            }
        }

        private bool IsInHeader(Vector2 screenPoint)
        {
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_panelRect, screenPoint, null, out local))
            {
                return false;
            }
            float top = _party ? 15f * _s : 0f;
            return local.x >= 0f && local.x <= _panelWidth
                   && local.y <= -top && local.y >= -(top + _totalH);
        }

        private Vector2 ClampAnchoredPosition(Vector2 anchored, DamageMeterConfig config)
        {
            if (_canvasRect == null)
            {
                return anchored;
            }
            float scale = _canvas != null && _canvas.scaleFactor > 0.01f ? _canvas.scaleFactor : 1f;
            Vector3 world = _canvasRect.TransformPoint(new Vector3(anchored.x, anchored.y, 0f));
            Vector2 topLeftScreen = RectTransformUtility.WorldToScreenPoint(null, world);
            float panelWpx = _panelWidth * scale;
            float panelHpx = _panelHeight * scale;
            float margin = config.screenMargin;
            float x = Mathf.Clamp(topLeftScreen.x, margin,
                Mathf.Max(margin, Screen.width - panelWpx - margin));
            float y = Mathf.Clamp(topLeftScreen.y, margin + panelHpx,
                Mathf.Max(margin + panelHpx, Screen.height - margin));
            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvasRect, new Vector2(x, y), null, out local))
            {
                return local;
            }
            return anchored;
        }

        private void ApplyBlock(PlayerBlockView block, PlayerBlockData data, DamageStatsScope scope,
            DamageMeterConfig config, float maxBlockTotal)
        {
            Color playerColor = GetPlayerColor(data.Slot);
            Color headerColor = _party
                ? playerColor
                : ParseColor(config.totalBarColor, new Color(0.24f, 0.31f, 0.44f, 0.95f));
            block.HeaderFill.color = headerColor;
            block.HeaderTarget = _party && maxBlockTotal > 0.01f ? data.Total / maxBlockTotal : 1f;
            if (!_animate)
            {
                block.HeaderCurrent = block.HeaderTarget;
            }

            bool hasAvatar = data.Icon != null;
            block.AvatarChip.gameObject.SetActive(hasAvatar);
            block.Avatar.gameObject.SetActive(hasAvatar);
            if (hasAvatar)
            {
                block.Avatar.sprite = data.Icon;
            }
            float nameX = hasAvatar ? 28f * _s : 7f * _s;

            float badgeWidth = 44f * _s;
            float totalWidth = 90f * _s;
            float nameWidth = Mathf.Max(40f, _panelWidth - nameX - badgeWidth - totalWidth - 14f * _s);
            PlaceTopLeft(block.Name.rectTransform, nameX, 0f, nameWidth, _totalH);
            string prefix = _party ? "P" + (data.Slot + 1) + " " : "";
            int maxChars = Mathf.Max(3, Mathf.FloorToInt(nameWidth / Mathf.Max(5f, 12.5f * _s * 0.95f)));
            block.Name.text = TrimToWidth(prefix + data.Name, maxChars);
            block.Name.color = TextColor;

            PlaceTopRight(block.Total.rectTransform, 6f * _s + badgeWidth, 0f, totalWidth, _totalH);
            block.Total.text = FormatNumber(data.Total);
            block.Total.color = TextColor;

            PlaceTopRight(block.Badge.rectTransform, 6f * _s, 0f, badgeWidth, _totalH);
            block.Badge.text = _party
                ? FormatPercent(data.Share)
                : (scope == DamageStatsScope.Run ? "全程" : "本层");
            block.Badge.color = TextColor;

            int rowCount = data.Sources.Count;
            int rows = rowCount + (!_party && data.HiddenSourceCount > 0 ? 1 : 0);
            EnsureRows(block, rows);
            float rowY = _totalH + 2f * _s;
            for (int i = 0; i < rowCount; i++)
            {
                RowView row = block.Rows[i];
                row.Root.gameObject.SetActive(true);
                PlaceStretchTop(row.Root, 0f, 0f, rowY + i * _rowH, _rowH);
                ApplyRow(row, data.Sources[i], playerColor, _party);
            }
            for (int i = rowCount; i < block.Rows.Count; i++)
            {
                block.Rows[i].Root.gameObject.SetActive(false);
            }

            if (!_party && data.HiddenSourceCount > 0)
            {
                RowView row = block.HiddenRow;
                row.Root.gameObject.SetActive(true);
                PlaceStretchTop(row.Root, 0f, 0f, rowY + rowCount * _rowH, _hiddenRowH);
                ApplyHiddenRow(row, data, playerColor);
            }
            else
            {
                block.HiddenRow.Root.gameObject.SetActive(false);
            }
        }

        private void EnsureBlocks(int count)
        {
            while (_blocks.Count < count)
            {
                _blocks.Add(CreatePlayerBlock(_blocks.Count));
            }
        }

        private PlayerBlockView CreatePlayerBlock(int index)
        {
            PlayerBlockView view = new PlayerBlockView();
            GameObject root = new GameObject("PlayerBlock" + index, typeof(RectTransform));
            root.transform.SetParent(_panelGo.transform, false);
            view.Root = root.GetComponent<RectTransform>();

            view.HeaderTrough = NewImage(root.transform, "HeaderTrough", TroughColor);
            FillParent(view.HeaderTrough.rectTransform);

            view.HeaderFill = NewImage(root.transform, "HeaderFill", Color.white);
            RectTransform fillRect = view.HeaderFill.rectTransform;
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = new Vector2(0f, -4f * _s);

            view.AvatarChip = NewImage(root.transform, "AvatarChip", new Color(0f, 0f, 0f, 0.38f));
            PlaceTopLeft(view.AvatarChip.rectTransform, 3f * _s, (_totalH - 21f * _s) * 0.5f,
                21f * _s, 21f * _s);

            view.Avatar = NewImage(root.transform, "Avatar", Color.white);
            view.Avatar.preserveAspect = true;
            PlaceTopLeft(view.Avatar.rectTransform, 4f * _s, (_totalH - 19f * _s) * 0.5f,
                19f * _s, 19f * _s);

            view.Name = NewText(root.transform, "Name", 1.25f,
                TextAlignmentOptions.MidlineLeft, TextColor, FontStyles.Normal, outlined: false);
            view.Total = NewText(root.transform, "Total", 2.0f,
                TextAlignmentOptions.MidlineRight, TextColor, FontStyles.Normal);
            view.Badge = NewText(root.transform, "Badge", 1.25f,
                TextAlignmentOptions.MidlineRight, TextColor, FontStyles.Normal, outlined: false);

            if (index == 0)
            {
                for (int i = 0; i < 3; i++)
                {
                    Image grip = NewImage(root.transform, "Grip" + i, new Color(1f, 1f, 1f, 0.35f));
                    PlaceTopLeft(grip.rectTransform, (5f + i * 4f) * _s, 1.5f * _s, 2f * _s, 2f * _s);
                }
            }

            view.HiddenRow = CreateRow(root.transform, "HiddenRow", _hiddenRowH);
            view.HiddenRow.Root.gameObject.SetActive(false);
            return view;
        }

        private void ApplyRow(RowView row, DamageRowData data, Color playerColor, bool party)
        {
            Color elementColor = GetElementColor(data.Element);
            Color valueColor = Color.Lerp(elementColor, Color.white, 0.30f);
            row.Fill.enabled = _config.showBars;
            Color fillBase = party ? playerColor : Color.Lerp(elementColor, Color.white, 0.12f);
            row.Fill.color = new Color(fillBase.r, fillBase.g, fillBase.b, party ? 0.22f : 0.18f);

            DamageIdEntity entity = GetEntity(data.Id);
            bool hasIcon = _config.showIcons && entity != null && entity.icon != null;
            row.IconChip.gameObject.SetActive(hasIcon);
            row.Icon.gameObject.SetActive(hasIcon);
            row.Strip.gameObject.SetActive(!hasIcon);

            float nameX;
            if (hasIcon)
            {
                row.IconChip.color = new Color(elementColor.r, elementColor.g, elementColor.b, 0.30f);
                row.Icon.sprite = entity.icon;
                nameX = 28f * _s;
            }
            else
            {
                row.Strip.color = elementColor;
                nameX = 12f * _s;
            }

            float valueWidth = 70f * _s;
            float percentWidth = _config.showPercentages ? 42f * _s : 0f;
            RectTransform nameRect = row.Name.rectTransform;
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(0f, 1f);
            nameRect.offsetMin = new Vector2(nameX, -_rowH);
            nameRect.offsetMax = new Vector2(-(valueWidth + percentWidth + 4f * _s), 0f);

            float available = Mathf.Max(30f, _panelWidth - nameX - valueWidth - percentWidth - 8f * _s);
            float charWidth = Mathf.Max(6f, 15f * _s * 0.92f);
            int maxChars = Mathf.Max(3, Mathf.FloorToInt(available / charWidth));
            row.Name.text = TrimToWidth(ResolveName(data.Id), maxChars);
            row.Name.color = TextColor;

            row.Value.text = FormatNumber(data.Value);
            row.Value.color = valueColor;
            PlaceTopRight(row.Value.rectTransform, 4f * _s + percentWidth, 0f, valueWidth, _rowH);

            row.Percent.text = _config.showPercentages ? FormatPercent(data.Percent) : "";
            row.Percent.color = DimTextColor;
            PlaceTopRight(row.Percent.rectTransform, 4f * _s, 0f, Mathf.Max(1f, percentWidth), _rowH);

            row.Target = data.Percent;
            if (!_animate)
            {
                row.Current = row.Target;
            }
        }

        private void ApplyHiddenRow(RowView row, PlayerBlockData data, Color playerColor)
        {
            row.Fill.enabled = _config.showBars;
            row.Fill.color = new Color(1f, 1f, 1f, 0.16f);
            row.IconChip.gameObject.SetActive(false);
            row.Icon.gameObject.SetActive(false);
            row.Strip.gameObject.SetActive(false);

            float valueWidth = 70f * _s;
            float percentWidth = _config.showPercentages ? 42f * _s : 0f;
            RectTransform nameRect = row.Name.rectTransform;
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(0f, 1f);
            nameRect.offsetMin = new Vector2(12f * _s, -_hiddenRowH);
            nameRect.offsetMax = new Vector2(-(valueWidth + percentWidth + 4f * _s), 0f);
            row.Name.text = "另有 " + data.HiddenSourceCount + " 项";
            row.Name.color = DimTextColor;

            row.Value.text = FormatNumber(data.HiddenTotal);
            row.Value.color = DimTextColor;
            PlaceTopRight(row.Value.rectTransform, 4f * _s + percentWidth, 0f, valueWidth, _hiddenRowH);

            float ratio = data.Total > 0.01f ? Mathf.Clamp01(data.HiddenTotal / data.Total) : 0f;
            row.Percent.text = _config.showPercentages ? FormatPercent(ratio) : "";
            row.Percent.color = DimTextColor;
            PlaceTopRight(row.Percent.rectTransform, 4f * _s, 0f, Mathf.Max(1f, percentWidth), _hiddenRowH);

            row.Target = ratio;
            if (!_animate)
            {
                row.Current = row.Target;
            }
        }

        private void EnsureRows(PlayerBlockView block, int count)
        {
            while (block.Rows.Count < count)
            {
                block.Rows.Add(CreateRow(block.Root, "Row" + block.Rows.Count, _rowH));
            }
        }

        private RowView CreateRow(Transform parent, string name, float height)
        {
            RowView view = new RowView();
            GameObject root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            view.Root = root.GetComponent<RectTransform>();

            view.Trough = NewImage(root.transform, "Trough", RowTroughColor);
            FillParent(view.Trough.rectTransform);

            view.Fill = NewImage(root.transform, "Fill", Color.white);
            RectTransform fillRect = view.Fill.rectTransform;
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = new Vector2(0f, -4f * _s);

            view.IconChip = NewImage(root.transform, "IconChip", Color.white);
            PlaceTopLeft(view.IconChip.rectTransform, 4f * _s, (height - _iconChipSize) * 0.5f,
                _iconChipSize, _iconChipSize);

            view.Icon = NewImage(root.transform, "Icon", Color.white);
            view.Icon.preserveAspect = true;
            PlaceTopLeft(view.Icon.rectTransform, 5f * _s, (height - _iconSize) * 0.5f,
                _iconSize, _iconSize);

            view.Strip = NewImage(root.transform, "Strip", Color.white);
            RectTransform stripRect = view.Strip.rectTransform;
            stripRect.anchorMin = new Vector2(0f, 0f);
            stripRect.anchorMax = new Vector2(0f, 1f);
            stripRect.pivot = new Vector2(0f, 0.5f);
            stripRect.anchoredPosition = new Vector2(4f * _s, 0f);
            stripRect.sizeDelta = new Vector2(3f * _s, -8f * _s);

            view.Name = NewText(root.transform, "Name", 1.5f,
                TextAlignmentOptions.MidlineLeft, TextColor, FontStyles.Normal, outlined: false);
            view.Value = NewText(root.transform, "Value", 1.5f,
                TextAlignmentOptions.MidlineRight, TextColor, FontStyles.Normal);
            view.Percent = NewText(root.transform, "Percent", 1.25f,
                TextAlignmentOptions.MidlineRight, DimTextColor, FontStyles.Normal, outlined: false);
            return view;
        }

        private void CreateCanvas()
        {
            _canvasGo = new GameObject("SephiriaDamageMeterCanvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            UnityEngine.Object.DontDestroyOnLoad(_canvasGo);

            _canvas = _canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 80;

            CanvasScaler scaler = _canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            _canvasRect = _canvasGo.GetComponent<RectTransform>();
        }

        private void CreatePanel()
        {
            _panelGo = new GameObject("DamageMeterPanel", typeof(RectTransform));
            _panelGo.transform.SetParent(_canvasGo.transform, false);

            _panelRect = _panelGo.GetComponent<RectTransform>();
            _panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            _panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            _panelRect.pivot = new Vector2(0f, 1f);
            _panelRect.sizeDelta = new Vector2(_panelWidth, _panelHeight);

            _background = NewImage(_panelGo.transform, "Background",
                new Color(0.02f, 0.025f, 0.035f, _config.backgroundOpacity));
            RectTransform backgroundRect = _background.rectTransform;
            FillParent(backgroundRect);
            backgroundRect.offsetMin = new Vector2(-4f * _s, -4f * _s);
            backgroundRect.offsetMax = new Vector2(4f * _s, 4f * _s);
            _background.gameObject.SetActive(_config.backgroundOpacity > 0.001f);

            _partyScopeText = NewText(_panelGo.transform, "PartyScope", 1.25f,
                TextAlignmentOptions.MidlineRight, TextColor, FontStyles.Normal, outlined: false);
            _partyScopeText.gameObject.SetActive(false);

            _emptyText = NewText(_panelGo.transform, "Empty", 1.25f,
                TextAlignmentOptions.Center, DimTextColor, FontStyles.Normal, outlined: false);
            _emptyText.gameObject.SetActive(false);

            _hiddenPlayersText = NewText(_panelGo.transform, "HiddenPlayers", 1.25f,
                TextAlignmentOptions.Center, DimTextColor, FontStyles.Normal, outlined: false);
            _hiddenPlayersText.gameObject.SetActive(false);

            _panelGo.SetActive(false);
        }

        private TextMeshProUGUI NewText(Transform parent, string name, float size, TextAlignmentOptions anchor,
            Color color, FontStyles style, bool outlined = true)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            text.font = _font;
            Material material = outlined ? _textMaterial : _plainMaterial;
            if (material != null)
            {
                text.fontSharedMaterial = material;
            }
            // Pixel fonts look right on their native point-size grid; pass
            // multipliers (1.0, 1.25, 1.5, 2.0 ...) instead of arbitrary sizes.
            text.fontSize = size * _nativePointSize * _s;
            text.alignment = anchor;
            text.color = color;
            text.fontStyle = style;
            text.raycastTarget = false;
            text.richText = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }

        private static Image NewImage(Transform parent, string name, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform NewRect(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static void FillParent(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetFillWidth(Image fill, float ratio, float maxWidth)
        {
            if (fill == null)
            {
                return;
            }
            Vector2 size = fill.rectTransform.sizeDelta;
            size.x = Mathf.Max(0f, maxWidth * Mathf.Clamp01(ratio));
            fill.rectTransform.sizeDelta = size;
        }

        private static void PlaceTopLeft(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void PlaceTopRight(RectTransform rect, float right, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-right, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void PlaceStretchTop(RectTransform rect, float left, float right, float y, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -y - height);
            rect.offsetMax = new Vector2(-right, -y);
        }

        private RectTransform GetMapRect(UI_HUDMapViewer viewer)
        {
            if (viewer == null)
            {
                return null;
            }
            RectTransform rect = viewer.transform as RectTransform;
            if (rect == null)
            {
                return null;
            }
            if (rect.rect.width > Screen.width * 0.55f || rect.rect.height > Screen.height * 0.55f)
            {
                if (viewer.contentsZone != null)
                {
                    rect = viewer.contentsZone;
                }
                else if (viewer.hudMapImage != null)
                {
                    rect = viewer.hudMapImage.rectTransform;
                }
            }
            return rect;
        }

        private static Camera GetCanvasCamera(Canvas canvas)
        {
            if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return null;
            }
            return canvas.worldCamera;
        }

        private DamageIdEntity GetEntity(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }
            DamageIdEntity entity;
            if (_entities.TryGetValue(id, out entity))
            {
                return entity;
            }
            try
            {
                entity = KeywordDatabase.GetDamageIdEntity(id);
            }
            catch
            {
                entity = null;
            }
            if (entity != null)
            {
                _entities[id] = entity;
            }
            return entity;
        }

        private string ResolveName(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return "?";
            }
            string cached;
            if (_names.TryGetValue(id, out cached))
            {
                return cached;
            }
            string resolved = null;
            try
            {
                DamageIdEntity entity = GetEntity(id);
                if (entity != null && entity.aName != null)
                {
                    string localized = entity.aName.ToString();
                    if (!string.IsNullOrEmpty(localized))
                    {
                        resolved = KeywordDatabase.Convert(localized, false, false);
                        if (string.IsNullOrEmpty(resolved))
                        {
                            resolved = localized;
                        }
                    }
                }
            }
            catch
            {
                resolved = null;
            }
            if (!string.IsNullOrEmpty(resolved) && resolved != id)
            {
                _names[id] = resolved;
                return resolved;
            }
            return id;
        }

        private static Color GetElementColor(EDamageElementalType element)
        {
            try
            {
                if (CombatManager.Instance != null)
                {
                    Color color = CombatManager.Instance.GetElementalDamageColor(element);
                    if (color.a > 0.01f)
                    {
                        return color;
                    }
                }
            }
            catch
            {
            }
            switch (element)
            {
                case EDamageElementalType.Fire: return new Color(1f, 0.45f, 0.25f);
                case EDamageElementalType.Ice: return new Color(0.50f, 0.85f, 1f);
                case EDamageElementalType.Lightning: return new Color(1f, 0.90f, 0.35f);
                case EDamageElementalType.Chaos: return new Color(0.80f, 0.40f, 1f);
                case EDamageElementalType.IceAndLightning: return new Color(0.60f, 0.90f, 1f);
                case EDamageElementalType.FireAndIce: return new Color(1f, 0.70f, 0.70f);
                case EDamageElementalType.FireAndLightning: return new Color(1f, 0.75f, 0.40f);
                default: return new Color(0.85f, 0.87f, 0.90f);
            }
        }

        private static Color GetPlayerColor(int slot)
        {
            int index = slot % PlayerColors.Length;
            if (index < 0)
            {
                index += PlayerColors.Length;
            }
            return PlayerColors[index];
        }

        private static string TrimToWidth(string value, int maxChars)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxChars)
            {
                return value ?? "";
            }
            if (maxChars <= 1)
            {
                return "…";
            }
            return value.Substring(0, maxChars - 1) + "…";
        }

        private static string FormatNumber(float value)
        {
            double number = value;
            if (number >= 100000000.0)
            {
                return (number / 100000000.0).ToString("0.##", CultureInfo.InvariantCulture) + "亿";
            }
            if (number >= 10000.0)
            {
                return (number / 10000.0).ToString("0.#", CultureInfo.InvariantCulture) + "万";
            }
            return Mathf.RoundToInt((float)number).ToString("N0", CultureInfo.InvariantCulture);
        }

        private static string FormatPercent(float ratio)
        {
            if (ratio <= 0f)
            {
                return "0%";
            }
            float percent = ratio * 100f;
            if (percent < 1f)
            {
                return "<1%";
            }
            return Mathf.RoundToInt(percent).ToString(CultureInfo.InvariantCulture) + "%";
        }

        private static Color ParseColor(string html, Color fallback)
        {
            if (!string.IsNullOrEmpty(html))
            {
                try
                {
                    Color color;
                    if (ColorUtility.TryParseHtmlString(html, out color))
                    {
                        color.a = 0.95f;
                        return color;
                    }
                }
                catch
                {
                }
            }
            return fallback;
        }

        public static bool HasGameFont()
        {
            try
            {
                LocalizationManager manager = LocalizationManager.Instance;
                return manager != null
                       && manager.fontAssets != null
                       && manager.fontAssets.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        private static TMP_FontAsset LoadGameFont(string preferredName)
        {
            try
            {
                LocalizationManager manager = LocalizationManager.Instance;
                if (manager != null && manager.fontAssets != null && manager.fontAssets.Count > 0)
                {
                    TMP_FontAsset font;
                    if (!string.IsNullOrEmpty(preferredName)
                        && manager.fontAssets.TryGetValue(preferredName, out font)
                        && font != null)
                    {
                        return font;
                    }
                    string name = manager.GetFontName();
                    if (!string.IsNullOrEmpty(name)
                        && manager.fontAssets.TryGetValue(name, out font)
                        && font != null)
                    {
                        return font;
                    }
                    if (manager.fontAssets.TryGetValue("Fusion_Pixel_10_Simple", out font) && font != null)
                    {
                        return font;
                    }
                    foreach (KeyValuePair<string, TMP_FontAsset> pair in manager.fontAssets)
                    {
                        if (pair.Value != null)
                        {
                            return pair.Value;
                        }
                    }
                }
            }
            catch
            {
            }
            return TMP_Settings.defaultFontAsset;
        }

        private static float GetNativePointSize(TMP_FontAsset font)
        {
            if (font == null)
            {
                return 10f;
            }
            try
            {
                LocalizationManager manager = LocalizationManager.Instance;
                FontMaterials presets;
                if (manager != null
                    && manager.fontMaterialPresets != null
                    && manager.fontMaterialPresets.TryGetValue(font.name, out presets)
                    && presets != null
                    && presets.defaultFontPointSize > 0)
                {
                    return presets.defaultFontPointSize;
                }
            }
            catch
            {
            }
            return 10f;
        }

        private static Material LoadGameMaterial(TMP_FontAsset font, string style)
        {
            if (font == null)
            {
                return null;
            }
            try
            {
                LocalizationManager manager = LocalizationManager.Instance;
                FontMaterials presets;
                if (manager != null
                    && manager.fontMaterialPresets != null
                    && manager.fontMaterialPresets.TryGetValue(font.name, out presets)
                    && presets != null
                    && presets.presets != null)
                {
                    Material defaultMaterial = null;
                    for (int i = 0; i < presets.presets.Length; i++)
                    {
                        FontMaterials.Pair pair = presets.presets[i];
                        if (pair == null || pair.material == null)
                        {
                            continue;
                        }
                        if (!string.IsNullOrEmpty(style) && pair.style == style)
                        {
                            return pair.material;
                        }
                        if (pair.style == "Default")
                        {
                            defaultMaterial = pair.material;
                        }
                    }
                    if (defaultMaterial != null)
                    {
                        return defaultMaterial;
                    }
                }
            }
            catch
            {
            }
            return font.material;
        }

        private sealed class SummaryColumnView
        {
            public RectTransform Root;
            public Image Background;
            public TextMeshProUGUI Header;
            public TextMeshProUGUI Total;
            public TextMeshProUGUI Stats;
            public SummaryRowView HiddenRow;
            public readonly List<SummaryRowView> Rows = new List<SummaryRowView>();
        }

        private sealed class SummaryRowView
        {
            public RectTransform Root;
            public Image Trough;
            public Image Fill;
            public Image IconChip;
            public Image Icon;
            public Image Strip;
            public TextMeshProUGUI Name;
            public TextMeshProUGUI Value;
            public TextMeshProUGUI Percent;
        }

        private sealed class RowView
        {
            public RectTransform Root;
            public Image Trough;
            public Image Fill;
            public Image IconChip;
            public Image Icon;
            public Image Strip;
            public TextMeshProUGUI Name;
            public TextMeshProUGUI Value;
            public TextMeshProUGUI Percent;
            public float Current;
            public float Target;
        }

        private sealed class PlayerBlockView
        {
            public RectTransform Root;
            public Image HeaderTrough;
            public Image HeaderFill;
            public Image AvatarChip;
            public Image Avatar;
            public TextMeshProUGUI Name;
            public TextMeshProUGUI Total;
            public TextMeshProUGUI Badge;
            public RowView HiddenRow;
            public readonly List<RowView> Rows = new List<RowView>();
            public float HeaderCurrent;
            public float HeaderTarget;
        }
    }
}
