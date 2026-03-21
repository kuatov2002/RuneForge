using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Collections.Generic;

/// <summary>
/// Hub UI: currency display, NPC interaction panels (upgrades, element unlocks, stats, portal).
/// </summary>
public class HubUI : MonoBehaviour
{
    UIDocument uiDoc;
    Label currencyLabel;
    Label interactHint;
    VisualElement panelOverlay;
    VisualElement panelContent;
    Label panelTitle;

    Action onStartRun;
    ElementSO[] allElements;

    static readonly Color CardBg = new(0.06f, 0.06f, 0.1f, 0.92f);
    static readonly Color CardBgHover = new(0.12f, 0.12f, 0.18f, 0.95f);
    static readonly Color Dim = new(0.5f, 0.5f, 0.55f);

    public void Init(Action startRunCallback, ElementSO[] elements)
    {
        onStartRun = startRunCallback;
        allElements = elements;

        uiDoc = gameObject.AddComponent<UIDocument>();
        var ps = ScriptableObject.CreateInstance<PanelSettings>();
        ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        ps.referenceResolution = new Vector2Int(1920, 1080);
        ps.themeStyleSheet = ThemeStyleSheet.CreateInstance<ThemeStyleSheet>();
        uiDoc.panelSettings = ps;

        var font = Font.CreateDynamicFontFromOSFont("Arial", 14);

        BuildUI();

        uiDoc.rootVisualElement.style.unityFontDefinition = FontDefinition.FromFont(font);

        HubInteractable.OnPlayerEnter += OnStationEnter;
        HubInteractable.OnPlayerExit += OnStationExit;

        RefreshCurrency();
    }

    void OnDestroy()
    {
        HubInteractable.OnPlayerEnter -= OnStationEnter;
        HubInteractable.OnPlayerExit -= OnStationExit;
    }

    HubInteractable currentStation;

    void OnStationEnter(HubInteractable station)
    {
        currentStation = station;
        interactHint.text = $"Press E — {station.stationId}";
        interactHint.style.display = DisplayStyle.Flex;
    }

    void OnStationExit(HubInteractable station)
    {
        if (currentStation == station)
        {
            currentStation = null;
            interactHint.style.display = DisplayStyle.None;
        }
    }

    void Update()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null) return;

        if (kb.eKey.wasPressedThisFrame && currentStation != null && panelOverlay.style.display == DisplayStyle.None)
        {
            OpenStation(currentStation);
        }

        if (kb.escapeKey.wasPressedThisFrame && panelOverlay.style.display == DisplayStyle.Flex)
        {
            ClosePanel();
        }
    }

    void OpenStation(HubInteractable station)
    {
        panelContent.Clear();
        panelOverlay.style.display = DisplayStyle.Flex;

        switch (station.stationId)
        {
            case "Forge Master":
                panelTitle.text = "FORGE MASTER";
                panelTitle.style.color = station.stationColor;
                BuildUpgradePanel();
                break;
            case "Element Scholar":
                panelTitle.text = "ELEMENT SCHOLAR";
                panelTitle.style.color = station.stationColor;
                BuildElementPanel();
                break;
            case "Chronicle":
                panelTitle.text = "CHRONICLE";
                panelTitle.style.color = station.stationColor;
                BuildStatsPanel();
                break;
            case "RunPortal":
                panelTitle.text = "ENTER THE DUNGEON";
                panelTitle.style.color = station.stationColor;
                BuildPortalPanel();
                break;
        }
    }

    void ClosePanel()
    {
        panelOverlay.style.display = DisplayStyle.None;
    }

    // ─── BUILD UI ───────────────────────────────────────────────

    void BuildUI()
    {
        var root = uiDoc.rootVisualElement;
        root.style.flexGrow = 1;
        root.pickingMode = PickingMode.Ignore;

        // Top bar: currency
        var topBar = new VisualElement();
        topBar.pickingMode = PickingMode.Ignore;
        topBar.style.flexDirection = FlexDirection.Row;
        topBar.style.alignItems = Align.Center;
        topBar.style.paddingTop = 16;
        topBar.style.paddingLeft = 20;
        topBar.style.paddingRight = 20;

        var runeIcon = new VisualElement();
        runeIcon.style.width = 28;
        runeIcon.style.height = 28;
        Radius(runeIcon, 14);
        runeIcon.style.backgroundColor = new Color(0.6f, 0.3f, 0.9f);
        runeIcon.style.marginRight = 8;
        topBar.Add(runeIcon);

        currencyLabel = Lbl("0", 28, new Color(0.8f, 0.6f, 1f), FontStyle.Bold);
        topBar.Add(currencyLabel);

        var spacer = new VisualElement();
        spacer.style.flexGrow = 1;
        topBar.Add(spacer);

        var titleLbl = Lbl("THE SANCTUM", 20, new Color(0.6f, 0.5f, 0.8f), FontStyle.Bold);
        topBar.Add(titleLbl);

        root.Add(topBar);

        // Interaction hint (center bottom)
        interactHint = Lbl("Press E", 22, Color.white, FontStyle.Bold);
        interactHint.pickingMode = PickingMode.Ignore;
        interactHint.style.position = Position.Absolute;
        interactHint.style.bottom = 60;
        interactHint.style.left = 0;
        interactHint.style.right = 0;
        interactHint.style.unityTextAlign = TextAnchor.MiddleCenter;
        interactHint.style.display = DisplayStyle.None;

        var hintBg = new VisualElement();
        hintBg.pickingMode = PickingMode.Ignore;
        hintBg.style.position = Position.Absolute;
        hintBg.style.bottom = 50;
        hintBg.style.left = new StyleLength(Length.Percent(35));
        hintBg.style.right = new StyleLength(Length.Percent(35));
        hintBg.style.height = 42;
        hintBg.style.backgroundColor = new Color(0, 0, 0, 0.6f);
        Radius(hintBg, 10);
        root.Add(hintBg);
        root.Add(interactHint);

        // Controls hint
        var controls = Lbl("WASD Move   E Interact   ESC Close", 14, new Color(0.35f, 0.35f, 0.4f));
        controls.style.position = Position.Absolute;
        controls.style.bottom = 16;
        controls.style.left = 0;
        controls.style.right = 0;
        controls.style.unityTextAlign = TextAnchor.MiddleCenter;
        root.Add(controls);

        // Panel overlay
        panelOverlay = new VisualElement();
        panelOverlay.style.position = Position.Absolute;
        panelOverlay.style.top = 0;
        panelOverlay.style.bottom = 0;
        panelOverlay.style.left = 0;
        panelOverlay.style.right = 0;
        panelOverlay.style.backgroundColor = new Color(0, 0, 0, 0.7f);
        panelOverlay.style.alignItems = Align.Center;
        panelOverlay.style.justifyContent = Justify.Center;
        panelOverlay.style.display = DisplayStyle.None;

        var panelBox = new VisualElement();
        panelBox.style.backgroundColor = new Color(0.08f, 0.06f, 0.12f, 0.95f);
        panelBox.style.width = 900;
        panelBox.style.maxHeight = 700;
        Radius(panelBox, 16);
        Border(panelBox, new Color(0.3f, 0.2f, 0.5f), 2);
        Pad(panelBox, 24, 28);

        // Header row
        var headerRow = new VisualElement();
        headerRow.style.flexDirection = FlexDirection.Row;
        headerRow.style.alignItems = Align.Center;
        headerRow.style.marginBottom = 20;

        panelTitle = Lbl("STATION", 32, Color.white, FontStyle.Bold);
        headerRow.Add(panelTitle);

        var headerSpacer = new VisualElement();
        headerSpacer.style.flexGrow = 1;
        headerRow.Add(headerSpacer);

        var closeBtn = Lbl("[ESC] Close", 16, Dim, FontStyle.Bold);
        closeBtn.RegisterCallback<ClickEvent>(_ => ClosePanel());
        headerRow.Add(closeBtn);
        panelBox.Add(headerRow);

        panelContent = new VisualElement();
        panelContent.style.flexGrow = 1;
        panelBox.Add(panelContent);

        panelOverlay.Add(panelBox);

        // Click outside to close
        panelOverlay.RegisterCallback<ClickEvent>(evt =>
        {
            if (evt.target == panelOverlay) ClosePanel();
        });

        root.Add(panelOverlay);
    }

    // ─── UPGRADE PANEL (Forge Master) ───────────────────────────

    void BuildUpgradePanel()
    {
        var desc = Lbl("Spend Rune Essence to permanently strengthen yourself.", 16, Dim);
        desc.style.marginBottom = 16;
        panelContent.Add(desc);

        var grid = new VisualElement();
        grid.style.flexDirection = FlexDirection.Row;
        grid.style.flexWrap = Wrap.Wrap;
        grid.style.justifyContent = Justify.Center;

        foreach (var def in MetaProgression.AllUpgrades)
        {
            var card = BuildUpgradeCard(def);
            grid.Add(card);
        }
        panelContent.Add(grid);
    }

    VisualElement BuildUpgradeCard(MetaProgression.UpgradeDef def)
    {
        int level = def.getLevel();
        bool maxed = level >= def.maxLevel;
        int cost = maxed ? 0 : def.getCost(level);
        bool canAfford = MetaProgression.Currency >= cost;

        var card = new VisualElement();
        card.style.width = 250;
        card.style.marginTop = 8; card.style.marginBottom = 8; card.style.marginLeft = 8; card.style.marginRight = 8;
        card.style.backgroundColor = CardBg;
        Radius(card, 12);
        Border(card, maxed ? new Color(0.4f, 0.8f, 0.3f) : new Color(0.3f, 0.3f, 0.35f), 2);
        card.style.overflow = Overflow.Hidden;

        // Header
        var header = new VisualElement();
        header.style.backgroundColor = def.color;
        Pad(header, 10, 14);
        header.Add(Lbl(def.name, 20, Color.white, FontStyle.Bold));
        card.Add(header);

        // Body
        var body = new VisualElement();
        Pad(body, 12, 14);

        body.Add(Lbl(def.description, 14, new Color(0.8f, 0.8f, 0.85f)));

        // Level pips
        var pipRow = new VisualElement();
        pipRow.style.flexDirection = FlexDirection.Row;
        pipRow.style.marginTop = 8;
        pipRow.style.marginBottom = 8;
        for (int i = 0; i < def.maxLevel; i++)
        {
            var pip = new VisualElement();
            pip.style.width = 16;
            pip.style.height = 16;
            pip.style.marginRight = 4;
            Radius(pip, 3);
            pip.style.backgroundColor = i < level ? def.color : new Color(0.2f, 0.2f, 0.25f);
            if (i < level) Border(pip, Color.Lerp(def.color, Color.white, 0.3f), 1);
            pipRow.Add(pip);
        }
        body.Add(pipRow);

        if (maxed)
        {
            body.Add(Lbl("MAXED", 14, new Color(0.4f, 0.8f, 0.3f), FontStyle.Bold));
        }
        else
        {
            var costLabel = Lbl($"Cost: {cost}", 16, canAfford ? new Color(0.8f, 0.6f, 1f) : new Color(0.6f, 0.3f, 0.3f), FontStyle.Bold);
            body.Add(costLabel);

            if (canAfford)
            {
                var buyBtn = Lbl("[ BUY ]", 16, new Color(0.5f, 1f, 0.5f), FontStyle.Bold);
                buyBtn.style.marginTop = 6;
                buyBtn.style.cursor = StyleKeyword.Auto;
                // Capture def by value for closure
                var capturedDef = def;
                card.RegisterCallback<ClickEvent>(_ =>
                {
                    if (MetaProgression.TryBuyUpgrade(capturedDef))
                    {
                        RefreshCurrency();
                        // Rebuild panel
                        panelContent.Clear();
                        BuildUpgradePanel();
                    }
                });
                body.Add(buyBtn);
            }
        }

        card.Add(body);

        // Hover
        card.RegisterCallback<MouseEnterEvent>(_ =>
        {
            card.style.backgroundColor = CardBgHover;
            Border(card, def.color, 3);
        });
        card.RegisterCallback<MouseLeaveEvent>(_ =>
        {
            card.style.backgroundColor = CardBg;
            Border(card, maxed ? new Color(0.4f, 0.8f, 0.3f) : new Color(0.3f, 0.3f, 0.35f), 2);
        });

        return card;
    }

    // ─── ELEMENT PANEL (Element Scholar) ────────────────────────

    void BuildElementPanel()
    {
        var desc = Lbl("Unlock new elements to expand your spell-crafting options in runs.", 16, Dim);
        desc.style.marginBottom = 16;
        panelContent.Add(desc);

        var grid = new VisualElement();
        grid.style.flexDirection = FlexDirection.Row;
        grid.style.flexWrap = Wrap.Wrap;
        grid.style.justifyContent = Justify.Center;

        foreach (var elem in allElements)
        {
            var card = BuildElementCard(elem);
            grid.Add(card);
        }
        panelContent.Add(grid);
    }

    VisualElement BuildElementCard(ElementSO elem)
    {
        bool unlocked = MetaProgression.IsElementUnlocked(elem.elementName);
        bool isBase = elem.elementName == "Fire" || elem.elementName == "Ice";
        int cost = MetaProgression.GetElementUnlockCost(elem.elementName);
        bool canAfford = MetaProgression.Currency >= cost;

        var card = new VisualElement();
        card.style.width = 160;
        card.style.marginTop = 8; card.style.marginBottom = 8; card.style.marginLeft = 8; card.style.marginRight = 8;
        card.style.backgroundColor = CardBg;
        Radius(card, 12);
        Border(card, unlocked ? elem.color : new Color(0.3f, 0.3f, 0.35f), 2);
        card.style.overflow = Overflow.Hidden;
        if (!unlocked && !isBase) card.style.opacity = 0.7f;

        // Header
        var header = new VisualElement();
        header.style.backgroundColor = unlocked ? elem.color : new Color(0.2f, 0.2f, 0.25f);
        Pad(header, 12, 14);

        var dot = new VisualElement();
        dot.style.width = 14;
        dot.style.height = 14;
        Radius(dot, 7);
        dot.style.backgroundColor = elem.color;
        dot.style.marginBottom = 4;
        header.Add(dot);

        header.Add(Lbl(elem.elementName, 22, Color.white, FontStyle.Bold));
        card.Add(header);

        // Body
        var body = new VisualElement();
        Pad(body, 10, 14);

        body.Add(Lbl($"DMG: {elem.baseDamage}", 14, new Color(0.8f, 0.8f, 0.85f)));

        string effectDesc = elem.statusEffect switch
        {
            StatusEffectType.Burn => "Burns over time",
            StatusEffectType.Slow => "Slows, 2x = Freeze",
            StatusEffectType.Chain => "Chains to nearby",
            StatusEffectType.Poison => "Stacking poison",
            StatusEffectType.VoidMark => "Void pull on kill",
            _ => ""
        };
        body.Add(Lbl(effectDesc, 13, Dim));

        if (unlocked || isBase)
        {
            var status = Lbl("UNLOCKED", 13, new Color(0.4f, 0.8f, 0.3f), FontStyle.Bold);
            status.style.marginTop = 8;
            body.Add(status);
        }
        else
        {
            var costLbl = Lbl($"Cost: {cost}", 14,
                canAfford ? new Color(0.8f, 0.6f, 1f) : new Color(0.6f, 0.3f, 0.3f), FontStyle.Bold);
            costLbl.style.marginTop = 8;
            body.Add(costLbl);

            if (canAfford)
            {
                var elemCapture = elem;
                card.RegisterCallback<ClickEvent>(_ =>
                {
                    if (MetaProgression.TryUnlockElement(elemCapture.elementName))
                    {
                        RefreshCurrency();
                        panelContent.Clear();
                        BuildElementPanel();
                    }
                });
            }
        }

        card.Add(body);
        return card;
    }

    // ─── STATS PANEL (Chronicle) ────────────────────────────────

    void BuildStatsPanel()
    {
        var stats = new (string label, string value)[]
        {
            ("Runs Completed", MetaProgression.RunsCompleted.ToString()),
            ("Best Floor", MetaProgression.BestFloor.ToString()),
            ("Rune Essence", MetaProgression.Currency.ToString()),
            ("Max HP Bonus", $"+{MetaProgression.MaxHPBonus}"),
            ("Damage Bonus", $"+{(MetaProgression.DamageMultiplier - 1f) * 100:F0}%"),
            ("Speed Bonus", $"+{(MetaProgression.SpeedMultiplier - 1f) * 100:F0}%"),
            ("Crit Chance", $"{MetaProgression.CritChance * 100:F0}%"),
            ("Extra Dash Charges", MetaProgression.ExtraDashCharges.ToString()),
            ("Starting Gold", MetaProgression.StartingGold.ToString()),
            ("Potions/Floor", MetaProgression.PotionsPerFloor.ToString()),
            ("Rune Rerolls", MetaProgression.Rerolls.ToString()),
        };

        foreach (var (label, value) in stats)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.paddingTop = 6;
            row.style.paddingBottom = 6;
            row.style.borderBottomColor = new Color(0.2f, 0.2f, 0.25f);
            row.style.borderBottomWidth = 1;

            row.Add(Lbl(label, 18, new Color(0.7f, 0.7f, 0.75f)));
            row.Add(Lbl(value, 18, Color.white, FontStyle.Bold));
            panelContent.Add(row);
        }
    }

    // ─── PORTAL PANEL ───────────────────────────────────────────

    void BuildPortalPanel()
    {
        var desc = Lbl("Step through the portal to begin a new dungeon run.", 18, Dim);
        desc.style.marginBottom = 24;
        desc.style.unityTextAlign = TextAnchor.MiddleCenter;
        panelContent.Add(desc);

        // Summary of current bonuses
        var bonusList = new VisualElement();
        bonusList.style.marginBottom = 24;

        if (MetaProgression.MaxHPBonus > 0)
            bonusList.Add(Lbl($"  +{MetaProgression.MaxHPBonus} Max HP", 16, new Color(0.9f, 0.3f, 0.3f)));
        if (MetaProgression.BaseDamageLevel > 0)
            bonusList.Add(Lbl($"  +{(MetaProgression.DamageMultiplier - 1f) * 100:F0}% Damage", 16, new Color(0.9f, 0.5f, 0.1f)));
        if (MetaProgression.SpeedBonusLevel > 0)
            bonusList.Add(Lbl($"  +{(MetaProgression.SpeedMultiplier - 1f) * 100:F0}% Speed", 16, new Color(0.3f, 0.8f, 1f)));
        if (MetaProgression.StartingGold > 0)
            bonusList.Add(Lbl($"  {MetaProgression.StartingGold} Starting Gold", 16, new Color(1f, 0.85f, 0.2f)));
        if (MetaProgression.HasStartingRelic)
            bonusList.Add(Lbl("  Random Starting Relic", 16, new Color(0.8f, 0.6f, 0.2f)));

        panelContent.Add(bonusList);

        // Start button
        var startBtn = new VisualElement();
        startBtn.style.alignSelf = Align.Center;
        startBtn.style.backgroundColor = new Color(0.15f, 0.4f, 0.2f);
        Pad(startBtn, 16, 40);
        Radius(startBtn, 12);
        Border(startBtn, new Color(0.2f, 0.9f, 0.4f), 3);

        startBtn.Add(Lbl("BEGIN RUN", 28, new Color(0.2f, 1f, 0.5f), FontStyle.Bold));

        startBtn.RegisterCallback<ClickEvent>(_ =>
        {
            ClosePanel();
            onStartRun?.Invoke();
        });
        startBtn.RegisterCallback<MouseEnterEvent>(_ =>
        {
            startBtn.style.backgroundColor = new Color(0.2f, 0.5f, 0.25f);
        });
        startBtn.RegisterCallback<MouseLeaveEvent>(_ =>
        {
            startBtn.style.backgroundColor = new Color(0.15f, 0.4f, 0.2f);
        });

        panelContent.Add(startBtn);
    }

    // ─── HELPERS ────────────────────────────────────────────────

    void RefreshCurrency()
    {
        if (currencyLabel != null)
            currencyLabel.text = MetaProgression.Currency.ToString();
    }

    static Label Lbl(string text, int size, Color color, FontStyle style = FontStyle.Normal)
    {
        var l = new Label(text);
        l.style.fontSize = size;
        l.style.color = color;
        l.style.unityFontStyleAndWeight = style;
        return l;
    }

    static void Border(VisualElement el, Color c, int w)
    {
        el.style.borderTopColor = c; el.style.borderBottomColor = c;
        el.style.borderLeftColor = c; el.style.borderRightColor = c;
        el.style.borderTopWidth = w; el.style.borderBottomWidth = w;
        el.style.borderLeftWidth = w; el.style.borderRightWidth = w;
    }

    static void Radius(VisualElement el, int r)
    {
        el.style.borderTopLeftRadius = r; el.style.borderTopRightRadius = r;
        el.style.borderBottomLeftRadius = r; el.style.borderBottomRightRadius = r;
    }

    static void Pad(VisualElement el, int v, int h)
    {
        el.style.paddingTop = v; el.style.paddingBottom = v;
        el.style.paddingLeft = h; el.style.paddingRight = h;
    }
}
