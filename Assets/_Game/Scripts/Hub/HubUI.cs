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
        string displayName = station.stationId switch
        {
            "RunPortal" => "Enter the Dungeon",
            "Forge Master" => "Forge Master — Upgrades",
            "Element Scholar" => "Element Scholar — Unlock Elements",
            "Chronicle" => "Chronicle — Stats & Records",
            "Codex" => "Synergy Codex — Discoveries",
            _ => station.stationId
        };
        interactHint.text = $"Press E — {displayName}";
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
            case "Codex":
                panelTitle.text = "SYNERGY CODEX";
                panelTitle.style.color = station.stationColor;
                BuildCodexPanel();
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

    // ─── UPGRADE PANEL (Forge Master) — A/B Branching ──────────

    void BuildUpgradePanel()
    {
        var desc = Lbl("Each slot has two paths with independent levels. Switch freely — both keep their progress.", 16, Dim);
        desc.style.whiteSpace = WhiteSpace.Normal;
        desc.style.marginBottom = 16;
        panelContent.Add(desc);

        var grid = new VisualElement();
        grid.style.flexDirection = FlexDirection.Row;
        grid.style.flexWrap = Wrap.Wrap;
        grid.style.justifyContent = Justify.Center;

        foreach (var slot in MetaProgression.AllUpgradeSlots)
        {
            var card = BuildUpgradeSlotCard(slot);
            grid.Add(card);
        }
        panelContent.Add(grid);
    }

    VisualElement BuildUpgradeSlotCard(MetaProgression.UpgradeSlot slot)
    {
        string activePath = MetaProgression.GetChosenPath(slot.pathA.id); // "A" or "B"
        var activeDef = activePath == "B" ? slot.pathB : slot.pathA;
        var inactiveDef = activePath == "B" ? slot.pathA : slot.pathB;

        int level = activeDef.getLevel();
        bool maxed = level >= activeDef.maxLevel;
        int cost = maxed ? 0 : activeDef.getCost(level);
        bool canAfford = MetaProgression.Currency >= cost;

        int inactiveLevel = inactiveDef.getLevel();

        var container = new VisualElement();
        container.style.width = 280;
        container.style.marginTop = 8; container.style.marginBottom = 8;
        container.style.marginLeft = 6; container.style.marginRight = 6;
        container.style.backgroundColor = CardBg;
        Radius(container, 12);
        Border(container, maxed ? new Color(0.4f, 0.8f, 0.3f) : activeDef.color, 2);
        container.style.overflow = Overflow.Hidden;

        // ── HEADER: active path name + pips ──
        var header = new VisualElement();
        header.style.backgroundColor = activeDef.color;
        Pad(header, 8, 12);
        header.Add(Lbl(activeDef.name, 18, Color.white, FontStyle.Bold));
        container.Add(header);

        // ── BODY ──
        var body = new VisualElement();
        Pad(body, 10, 12);

        body.Add(Lbl(activeDef.description, 13, new Color(0.8f, 0.8f, 0.85f)));

        // Level pips
        var pipRow = new VisualElement();
        pipRow.style.flexDirection = FlexDirection.Row;
        pipRow.style.marginTop = 6;
        pipRow.style.marginBottom = 6;
        for (int i = 0; i < activeDef.maxLevel; i++)
        {
            var pip = new VisualElement();
            pip.style.width = 14; pip.style.height = 14;
            pip.style.marginRight = 3;
            Radius(pip, 3);
            pip.style.backgroundColor = i < level ? activeDef.color : new Color(0.2f, 0.2f, 0.25f);
            if (i < level) Border(pip, Color.Lerp(activeDef.color, Color.white, 0.3f), 1);
            pipRow.Add(pip);
        }
        body.Add(pipRow);

        if (maxed)
        {
            body.Add(Lbl("MAXED", 13, new Color(0.4f, 0.8f, 0.3f), FontStyle.Bold));
        }
        else
        {
            body.Add(Lbl($"Cost: {cost}", 14,
                canAfford ? new Color(0.8f, 0.6f, 1f) : new Color(0.6f, 0.3f, 0.3f), FontStyle.Bold));

            if (canAfford)
            {
                var buyBtn = Lbl("[ BUY ]", 14, new Color(0.5f, 1f, 0.5f), FontStyle.Bold);
                buyBtn.style.marginTop = 4;
                body.Add(buyBtn);

                var capturedDef = activeDef;
                container.RegisterCallback<ClickEvent>(_ =>
                {
                    if (MetaProgression.TryBuyUpgrade(capturedDef))
                    {
                        RefreshCurrency();
                        panelContent.Clear();
                        BuildUpgradePanel();
                    }
                });
            }
        }

        // ── SWITCH ROW: inactive path ──
        var switchRow = new VisualElement();
        switchRow.style.flexDirection = FlexDirection.Row;
        switchRow.style.alignItems = Align.Center;
        switchRow.style.marginTop = 8;
        switchRow.style.paddingTop = 6;
        switchRow.style.borderTopColor = new Color(0.25f, 0.25f, 0.3f);
        switchRow.style.borderTopWidth = 1;

        // Inactive path name + its current level
        string inactiveLevelStr = inactiveLevel > 0 ? $" Lv{inactiveLevel}" : "";
        var inactiveName = Lbl($"{inactiveDef.name}{inactiveLevelStr}", 12, new Color(0.55f, 0.55f, 0.6f));
        inactiveName.style.flexGrow = 1;
        switchRow.Add(inactiveName);

        var switchBtn = Lbl("[ SWITCH ]", 11, new Color(0.6f, 0.6f, 0.7f), FontStyle.Bold);
        switchBtn.style.marginLeft = 6;
        switchRow.Add(switchBtn);

        var capturedSlot = slot;
        switchRow.RegisterCallback<ClickEvent>(evt =>
        {
            evt.StopPropagation();
            MetaProgression.SwitchPath(capturedSlot);
            panelContent.Clear();
            BuildUpgradePanel();
        });
        switchRow.RegisterCallback<MouseEnterEvent>(_ =>
        {
            switchBtn.style.color = Color.white;
            inactiveName.style.color = new Color(0.8f, 0.8f, 0.85f);
        });
        switchRow.RegisterCallback<MouseLeaveEvent>(_ =>
        {
            switchBtn.style.color = new Color(0.6f, 0.6f, 0.7f);
            inactiveName.style.color = new Color(0.55f, 0.55f, 0.6f);
        });

        body.Add(switchRow);
        container.Add(body);

        // Hover on whole card
        container.RegisterCallback<MouseEnterEvent>(_ => container.style.backgroundColor = CardBgHover);
        container.RegisterCallback<MouseLeaveEvent>(_ => container.style.backgroundColor = CardBg);

        return container;
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
        bool isBase = elem.elementName == "Fire" || elem.elementName == "Water"
            || elem.elementName == "Earth" || elem.elementName == "Air";
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

        string effectDesc = elem.elementType switch
        {
            ElementType.Fire => "Burst damage, burning DOT",
            ElementType.Water => "Freeze, slow, control",
            ElementType.Earth => "AoE, defense, stone walls",
            ElementType.Air => "Dash, knockback, mobility",
            ElementType.Lightning => "Chain damage, stun",
            ElementType.Poison => "DoT, spreading stacks",
            ElementType.Void => "Gravity pull, implosion",
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
        var statsList = new List<(string label, string value)>
        {
            ("Runs Completed", MetaProgression.RunsCompleted.ToString()),
            ("Best Floor", MetaProgression.BestFloor.ToString()),
            ("Rune Essence", MetaProgression.Currency.ToString()),
        };

        // Show stats for chosen paths only
        string hp = MetaProgression.GetChosenPath("maxhp");
        if (hp == "B" && MetaProgression.SecondWindCharges > 0)
            statsList.Add(("Second Wind", $"{MetaProgression.SecondWindCharges} floor(s)"));
        else if (MetaProgression.MaxHPBonus > 0)
            statsList.Add(("Max HP Bonus", $"+{MetaProgression.MaxHPBonus}"));

        string dmg = MetaProgression.GetChosenPath("damage");
        if (dmg == "B" && MetaProgression.SpellMasteryLevel > 0)
            statsList.Add(("Spell Mastery", $"+{(MetaProgression.SpellMasteryBonus - 1f) * 100:F0}% same-elem"));
        else if (MetaProgression.BaseDamageLevel > 0)
            statsList.Add(("Damage Bonus", $"+{(MetaProgression.DamageMultiplier - 1f) * 100:F0}%"));

        string spd = MetaProgression.GetChosenPath("speed");
        if (spd == "B" && MetaProgression.PhaseStepLevel > 0)
            statsList.Add(("Phase Step", $"+{MetaProgression.PhaseStepDuration:F1}s i-frames"));
        else if (MetaProgression.SpeedBonusLevel > 0)
            statsList.Add(("Speed Bonus", $"+{(MetaProgression.SpeedMultiplier - 1f) * 100:F0}%"));

        string dash = MetaProgression.GetChosenPath("dash");
        if (dash == "B" && MetaProgression.BlinkStrikeLevel > 0)
            statsList.Add(("Blink Strike", $"{MetaProgression.BlinkStrikeDamage} dmg"));
        else if (MetaProgression.ExtraDashCharges > 0)
            statsList.Add(("Extra Dash Charges", MetaProgression.ExtraDashCharges.ToString()));

        string gold = MetaProgression.GetChosenPath("gold");
        if (gold == "B" && MetaProgression.HagglerLevel > 0)
            statsList.Add(("Haggler", $"-{(1f - MetaProgression.HagglerDiscount) * 100:F0}% prices"));
        else if (MetaProgression.StartingGold > 0)
            statsList.Add(("Starting Gold", MetaProgression.StartingGold.ToString()));

        string crit = MetaProgression.GetChosenPath("crit");
        if (crit == "B" && MetaProgression.ElemMasteryLevel > 0)
            statsList.Add(("Reaction Damage", $"+{(MetaProgression.ReactionDamageBonus - 1f) * 100:F0}%"));
        else if (MetaProgression.CritChanceLevel > 0)
            statsList.Add(("Crit Chance", $"{MetaProgression.CritChance * 100:F0}%"));

        string pot = MetaProgression.GetChosenPath("potion");
        if (pot == "B" && MetaProgression.BloodMageLevel > 0)
            statsList.Add(("Blood Mage", $"{MetaProgression.BloodMageChance * 100:F0}% kill heal"));
        else if (MetaProgression.PotionsPerFloor > 0)
            statsList.Add(("Potions/Floor", MetaProgression.PotionsPerFloor.ToString()));

        string reroll = MetaProgression.GetChosenPath("reroll");
        if (reroll == "B" && MetaProgression.LuckyFindLevel > 0)
            statsList.Add(("Lucky Find", $"{MetaProgression.LuckyFindChance * 100:F0}% relic chance"));
        else if (MetaProgression.Rerolls > 0)
            statsList.Add(("Rune Rerolls", MetaProgression.Rerolls.ToString()));

        string relic = MetaProgression.GetChosenPath("relic");
        if (relic == "B" && MetaProgression.HasCursedHeirloom)
            statsList.Add(("Cursed Heirloom", $"Cursed Relic + {MetaProgression.CursedHeirloomGold}g"));
        else if (MetaProgression.HasStartingRelic)
            statsList.Add(("Starting Relic", "Random relic"));

        var stats = statsList.ToArray();

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

        // Path A bonuses
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
        // Path B bonuses
        if (MetaProgression.SecondWindCharges > 0)
            bonusList.Add(Lbl($"  Second Wind: {MetaProgression.SecondWindCharges} floor(s)", 16, new Color(0.9f, 0.4f, 0.4f)));
        if (MetaProgression.SpellMasteryLevel > 0)
            bonusList.Add(Lbl($"  +{(MetaProgression.SpellMasteryBonus - 1f) * 100:F0}% Same-Element Combo", 16, new Color(1f, 0.7f, 0.2f)));
        if (MetaProgression.PhaseStepLevel > 0)
            bonusList.Add(Lbl($"  +{MetaProgression.PhaseStepDuration:F1}s Dash i-frames", 16, new Color(0.4f, 0.6f, 1f)));
        if (MetaProgression.BlinkStrikeDamage > 0)
            bonusList.Add(Lbl($"  Blink Strike: {MetaProgression.BlinkStrikeDamage} dmg", 16, new Color(0.7f, 0.3f, 0.9f)));
        if (MetaProgression.HagglerLevel > 0)
            bonusList.Add(Lbl($"  -{(1f - MetaProgression.HagglerDiscount) * 100:F0}% Shop Prices", 16, new Color(0.9f, 0.75f, 0.3f)));
        if (MetaProgression.ElemMasteryLevel > 0)
            bonusList.Add(Lbl($"  +{(MetaProgression.ReactionDamageBonus - 1f) * 100:F0}% Reaction Damage", 16, new Color(1f, 0.5f, 0.7f)));
        if (MetaProgression.BloodMageLevel > 0)
            bonusList.Add(Lbl($"  {MetaProgression.BloodMageChance * 100:F0}% Kill Heal", 16, new Color(0.5f, 0.9f, 0.3f)));
        if (MetaProgression.LuckyFindLevel > 0)
            bonusList.Add(Lbl($"  {MetaProgression.LuckyFindChance * 100:F0}% Bonus Relic Chance", 16, new Color(0.7f, 0.5f, 1f)));
        if (MetaProgression.HasCursedHeirloom)
            bonusList.Add(Lbl($"  Cursed Relic + {MetaProgression.CursedHeirloomGold}g", 16, new Color(0.6f, 0.3f, 0.5f)));

        panelContent.Add(bonusList);

        // Heat/Ascension selector
        if (AscensionSystem.MaxHeatUnlocked > 0)
        {
            var heatRow = new VisualElement();
            heatRow.style.flexDirection = FlexDirection.Row;
            heatRow.style.alignItems = Align.Center;
            heatRow.style.justifyContent = Justify.Center;
            heatRow.style.marginBottom = 16;

            var heatLabel = Lbl($"Heat: {AscensionSystem.CurrentHeat}", 20, new Color(1f, 0.4f, 0.2f), FontStyle.Bold);
            heatLabel.style.marginRight = 16;
            heatRow.Add(heatLabel);

            var minusBtn = new VisualElement();
            minusBtn.style.width = 32; minusBtn.style.height = 32;
            minusBtn.style.backgroundColor = new Color(0.2f, 0.2f, 0.25f);
            Radius(minusBtn, 6);
            minusBtn.style.alignItems = Align.Center;
            minusBtn.style.justifyContent = Justify.Center;
            minusBtn.Add(Lbl("-", 20, Color.white, FontStyle.Bold));
            minusBtn.RegisterCallback<ClickEvent>(_ =>
            {
                AscensionSystem.CurrentHeat = Mathf.Max(0, AscensionSystem.CurrentHeat - 1);
                panelContent.Clear();
                BuildPortalPanel();
            });
            heatRow.Add(minusBtn);

            var plusBtn = new VisualElement();
            plusBtn.style.width = 32; plusBtn.style.height = 32;
            plusBtn.style.backgroundColor = new Color(0.2f, 0.2f, 0.25f);
            plusBtn.style.marginLeft = 8;
            Radius(plusBtn, 6);
            plusBtn.style.alignItems = Align.Center;
            plusBtn.style.justifyContent = Justify.Center;
            plusBtn.Add(Lbl("+", 20, Color.white, FontStyle.Bold));
            plusBtn.RegisterCallback<ClickEvent>(_ =>
            {
                AscensionSystem.CurrentHeat = Mathf.Min(AscensionSystem.MaxHeatUnlocked, AscensionSystem.CurrentHeat + 1);
                panelContent.Clear();
                BuildPortalPanel();
            });
            heatRow.Add(plusBtn);

            panelContent.Add(heatRow);

            if (AscensionSystem.CurrentHeat > 0)
            {
                var heatDesc = Lbl(AscensionSystem.GetHeatDescription(), 14, new Color(1f, 0.5f, 0.3f));
                heatDesc.style.whiteSpace = WhiteSpace.Normal;
                heatDesc.style.unityTextAlign = TextAnchor.MiddleCenter;
                heatDesc.style.marginBottom = 16;
                panelContent.Add(heatDesc);
            }
        }

        // ── Loadout (Aspect) selector ──
        var loadoutTitle = Lbl("STARTING LOADOUT", 20, new Color(0.8f, 0.7f, 1f), FontStyle.Bold);
        loadoutTitle.style.unityTextAlign = TextAnchor.MiddleCenter;
        loadoutTitle.style.marginBottom = 12;
        panelContent.Add(loadoutTitle);

        var loadoutGrid = new VisualElement();
        loadoutGrid.style.flexDirection = FlexDirection.Row;
        loadoutGrid.style.flexWrap = Wrap.Wrap;
        loadoutGrid.style.justifyContent = Justify.Center;
        loadoutGrid.style.marginBottom = 20;

        foreach (var loadout in MetaProgression.AllLoadouts)
        {
            var lCard = BuildLoadoutCard(loadout);
            loadoutGrid.Add(lCard);
        }
        panelContent.Add(loadoutGrid);

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

    // ─── LOADOUT CARD ──────────────────────────────────────────

    VisualElement BuildLoadoutCard(MetaProgression.LoadoutDef loadout)
    {
        bool unlocked = MetaProgression.IsLoadoutUnlocked(loadout.id);
        bool selected = MetaProgression.SelectedLoadout == loadout.id;
        bool canAfford = MetaProgression.Currency >= loadout.unlockCost;

        var card = new VisualElement();
        card.style.width = 130;
        card.style.marginTop = 4; card.style.marginBottom = 4;
        card.style.marginLeft = 4; card.style.marginRight = 4;
        card.style.backgroundColor = selected ? new Color(0.12f, 0.1f, 0.18f, 0.95f) : CardBg;
        Radius(card, 10);
        Border(card, selected ? loadout.color : (unlocked ? new Color(0.4f, 0.4f, 0.45f) : new Color(0.25f, 0.25f, 0.3f)), selected ? 3 : 1);
        card.style.overflow = Overflow.Hidden;
        if (!unlocked) card.style.opacity = 0.6f;

        // Color header
        var header = new VisualElement();
        header.style.backgroundColor = unlocked ? loadout.color : new Color(0.2f, 0.2f, 0.25f);
        Pad(header, 6, 8);
        header.Add(Lbl(loadout.name, 14, Color.white, FontStyle.Bold));
        card.Add(header);

        var body = new VisualElement();
        Pad(body, 6, 8);

        if (unlocked)
        {
            body.Add(Lbl(loadout.passiveDesc, 11, new Color(0.7f, 0.7f, 0.75f)));
            if (selected)
            {
                var selLabel = Lbl("SELECTED", 11, new Color(0.4f, 1f, 0.5f), FontStyle.Bold);
                selLabel.style.marginTop = 4;
                body.Add(selLabel);
            }
            else
            {
                var selBtn = Lbl("[ SELECT ]", 11, new Color(0.7f, 0.8f, 1f), FontStyle.Bold);
                selBtn.style.marginTop = 4;
                body.Add(selBtn);
            }

            var capturedId = loadout.id;
            card.RegisterCallback<ClickEvent>(_ =>
            {
                MetaProgression.SelectedLoadout = capturedId;
                panelContent.Clear();
                BuildPortalPanel();
            });
        }
        else
        {
            var costLbl = Lbl($"Cost: {loadout.unlockCost}", 12,
                canAfford ? new Color(0.8f, 0.6f, 1f) : new Color(0.6f, 0.3f, 0.3f), FontStyle.Bold);
            costLbl.style.marginTop = 4;
            body.Add(costLbl);

            if (canAfford)
            {
                var capturedId = loadout.id;
                card.RegisterCallback<ClickEvent>(_ =>
                {
                    if (MetaProgression.TryUnlockLoadout(capturedId))
                    {
                        RefreshCurrency();
                        panelContent.Clear();
                        BuildPortalPanel();
                    }
                });
            }
        }

        card.Add(body);

        // Hover
        var capturedLoadout = loadout;
        var capturedSelected = selected;
        card.RegisterCallback<MouseEnterEvent>(_ =>
        {
            card.style.backgroundColor = CardBgHover;
            Border(card, capturedLoadout.color, 2);
        });
        card.RegisterCallback<MouseLeaveEvent>(_ =>
        {
            bool sel = MetaProgression.SelectedLoadout == capturedLoadout.id;
            bool unl = MetaProgression.IsLoadoutUnlocked(capturedLoadout.id);
            card.style.backgroundColor = sel ? new Color(0.12f, 0.1f, 0.18f, 0.95f) : CardBg;
            Border(card, sel ? capturedLoadout.color : (unl ? new Color(0.4f, 0.4f, 0.45f) : new Color(0.25f, 0.25f, 0.3f)), sel ? 3 : 1);
        });

        return card;
    }

    // ─── CODEX PANEL (Synergy Codex) ─────────────────────────

    void BuildCodexPanel()
    {
        int discovered = MetaProgression.DiscoveredComboCount;
        int total = MetaProgression.ComboDefinitions.Length;

        var summary = Lbl($"Synergies Discovered: {discovered} / {total}", 18,
            new Color(0.8f, 0.7f, 1f), FontStyle.Bold);
        summary.style.marginBottom = 16;
        summary.style.unityTextAlign = TextAnchor.MiddleCenter;
        panelContent.Add(summary);

        var desc = Lbl("Combine two different elements on the same enemy to trigger a synergy reaction.", 14, Dim);
        desc.style.whiteSpace = WhiteSpace.Normal;
        desc.style.marginBottom = 16;
        desc.style.unityTextAlign = TextAnchor.MiddleCenter;
        panelContent.Add(desc);

        var grid = new VisualElement();
        grid.style.flexDirection = FlexDirection.Row;
        grid.style.flexWrap = Wrap.Wrap;
        grid.style.justifyContent = Justify.Center;

        foreach (var combo in MetaProgression.ComboDefinitions)
        {
            bool found = MetaProgression.IsComboDiscovered(combo.id);
            var card = BuildComboCard(combo, found);
            grid.Add(card);
        }
        panelContent.Add(grid);
    }

    VisualElement BuildComboCard((string id, string name, string elem1, string elem2, string desc) combo, bool discovered)
    {
        var card = new VisualElement();
        card.style.width = 200;
        card.style.marginTop = 6; card.style.marginBottom = 6;
        card.style.marginLeft = 6; card.style.marginRight = 6;
        card.style.backgroundColor = discovered ? CardBg : new Color(0.04f, 0.04f, 0.06f, 0.9f);
        Radius(card, 10);
        Border(card, discovered ? new Color(0.6f, 0.5f, 0.8f) : new Color(0.2f, 0.2f, 0.25f), discovered ? 2 : 1);
        card.style.overflow = Overflow.Hidden;

        // Header with element dots
        var header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.alignItems = Align.Center;
        header.style.backgroundColor = discovered ? new Color(0.15f, 0.1f, 0.2f) : new Color(0.1f, 0.1f, 0.12f);
        Pad(header, 8, 10);

        Color elem1Col = GetElementColor(combo.elem1);
        Color elem2Col = GetElementColor(combo.elem2);

        var dot1 = new VisualElement();
        dot1.style.width = 12; dot1.style.height = 12;
        Radius(dot1, 6);
        dot1.style.backgroundColor = discovered ? elem1Col : new Color(0.3f, 0.3f, 0.35f);
        dot1.style.marginRight = 4;
        header.Add(dot1);

        header.Add(Lbl("+", 14, Dim));

        var dot2 = new VisualElement();
        dot2.style.width = 12; dot2.style.height = 12;
        Radius(dot2, 6);
        dot2.style.backgroundColor = discovered ? elem2Col : new Color(0.3f, 0.3f, 0.35f);
        dot2.style.marginLeft = 4;
        dot2.style.marginRight = 8;
        header.Add(dot2);

        var nameLabel = Lbl(discovered ? combo.name : "???", 16,
            discovered ? Color.white : new Color(0.4f, 0.4f, 0.45f), FontStyle.Bold);
        header.Add(nameLabel);
        card.Add(header);

        // Body
        var body = new VisualElement();
        Pad(body, 8, 10);

        if (discovered)
        {
            var elemNames = Lbl($"{combo.elem1} + {combo.elem2}", 12, new Color(0.6f, 0.6f, 0.7f));
            body.Add(elemNames);

            var descLabel = Lbl(combo.desc, 12, new Color(0.8f, 0.8f, 0.85f));
            descLabel.style.whiteSpace = WhiteSpace.Normal;
            descLabel.style.marginTop = 4;
            body.Add(descLabel);
        }
        else
        {
            var hint = Lbl("Not yet discovered", 12, new Color(0.4f, 0.4f, 0.45f));
            body.Add(hint);
            var hintDetail = Lbl("Combine two elements in combat to discover", 11, new Color(0.35f, 0.35f, 0.4f));
            hintDetail.style.whiteSpace = WhiteSpace.Normal;
            hintDetail.style.marginTop = 4;
            body.Add(hintDetail);
        }

        card.Add(body);
        return card;
    }

    static Color GetElementColor(string elemName) => elemName switch
    {
        "Fire" => new Color(1f, 0.4f, 0.1f),
        "Water" => new Color(0.3f, 0.7f, 1f),
        "Earth" => new Color(0.6f, 0.4f, 0.2f),
        "Air" => new Color(0.7f, 0.9f, 1f),
        "Lightning" => new Color(1f, 1f, 0.3f),
        "Poison" => new Color(0.2f, 0.9f, 0.1f),
        "Void" => new Color(0.6f, 0.1f, 0.9f),
        _ => Color.white,
    };

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
