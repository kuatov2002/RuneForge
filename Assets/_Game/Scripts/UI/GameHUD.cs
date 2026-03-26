using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.TextCore.Text;
using System;
using System.Collections.Generic;

public class GameHUD : MonoBehaviour
{
    SpellCaster caster;
    Health playerHealth;
    PlayerController playerCtrl;
    UIDocument uiDoc;

    VisualElement hpBar;
    Label waveLabel;
    Label floorRoomLabel;
    VisualElement deathOverlay;
    Label deathWaveLabel;
    VisualElement victoryOverlay;

    // New combo system UI
    VisualElement elementHotbar;
    VisualElement[] elementSlots = new VisualElement[4];
    VisualElement[] chargeIndicators = new VisualElement[4];
    Label comboNameLabel;
    VisualElement orbLeftIndicator, orbRightIndicator;
    VisualElement chargeBar; // shows charge progress when holding LMB

    // Upgrade selection overlay
    VisualElement upgradeOverlay;
    VisualElement upgradePanel;

    // Element unlock overlay
    VisualElement elementUnlockOverlay;
    VisualElement elementUnlockPanel;

    // Synergy selection overlay
    VisualElement synergyOverlay;
    VisualElement synergyPanel;

    // Momentum
    Label momentumLabel;
    VisualElement momentumBar;
    VisualElement momentumFill;

    // Relics
    VisualElement relicBar;

    // Gold
    Label goldLabel;

    // Potions
    Label potionLabel;

    // Boss HUD
    VisualElement bossHPContainer;
    VisualElement bossHPFill;
    Label bossNameLabel;
    Health trackedBossHP;

    // Dash
    VisualElement dashCDContainer;
    VisualElement dashCDFill;

    // Floating damage numbers
    VisualElement damageNumberLayer;
    readonly List<DamageNumberState> activeDamageNumbers = new();

    struct DamageNumberState
    {
        public Label label;
        public Vector3 worldPos;
        public float elapsed;
        public float duration;
        public Vector3 velocity;
    }

    static readonly Color CardBg = new(0.06f, 0.06f, 0.1f, 0.92f);
    static readonly Color CardBgHover = new(0.12f, 0.12f, 0.18f, 0.95f);
    static readonly Color ActiveBorder = new(1f, 0.85f, 0.2f);
    static readonly Color InactiveBorder = new(0.3f, 0.3f, 0.35f);
    static readonly Color Dim = new(0.5f, 0.5f, 0.55f);

    public void Init(SpellCaster sc, Health hp)
    {
        caster = sc;
        playerHealth = hp;
        playerCtrl = hp.GetComponent<PlayerController>();

        uiDoc = gameObject.AddComponent<UIDocument>();
        var ps = ScriptableObject.CreateInstance<PanelSettings>();
        ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        ps.referenceResolution = new Vector2Int(1920, 1080);
        ps.themeStyleSheet = ThemeStyleSheet.CreateInstance<ThemeStyleSheet>();
        uiDoc.panelSettings = ps;

        BuildUI();

        var font = Font.CreateDynamicFontFromOSFont("Arial", 14);
        uiDoc.rootVisualElement.style.unityFontDefinition = FontDefinition.FromFont(font);

        playerHealth.OnHPChanged += (_, _) => RefreshHP();
        playerHealth.OnDamaged += OnPlayerOrEnemyDamaged;
        caster.OnOrbsChanged += RefreshComboDisplay;
        caster.OnComboNameChanged += OnComboNameChanged;
        RefreshHP();
        RefreshComboDisplay();
    }

    public void TrackEnemyDamage(Health enemyHP)
    {
        enemyHP.OnDamaged += OnPlayerOrEnemyDamaged;
    }

    void OnPlayerOrEnemyDamaged(int amount, Vector3 worldPos, bool killed)
    {
        SpawnDamageNumber(amount, worldPos, killed);
    }

    void BuildUI()
    {
        var root = uiDoc.rootVisualElement;
        root.style.flexGrow = 1;
        root.pickingMode = PickingMode.Ignore;

        // ── Top bar ──
        var topBar = new VisualElement();
        topBar.pickingMode = PickingMode.Ignore;
        topBar.style.flexDirection = FlexDirection.Row;
        topBar.style.alignItems = Align.Center;
        topBar.style.paddingTop = 16;
        topBar.style.paddingLeft = 20;
        topBar.style.paddingRight = 20;

        var hpRow = new VisualElement();
        hpRow.pickingMode = PickingMode.Ignore;
        hpRow.style.flexDirection = FlexDirection.Row;
        hpRow.style.alignItems = Align.Center;
        var hpLabel = Lbl("HP", 20, new Color(0.95f, 0.2f, 0.2f), FontStyle.Bold);
        hpLabel.style.marginRight = 10;
        hpRow.Add(hpLabel);
        hpBar = new VisualElement();
        hpBar.pickingMode = PickingMode.Ignore;
        hpBar.style.flexDirection = FlexDirection.Row;
        hpRow.Add(hpBar);
        topBar.Add(hpRow);

        var spacer = new VisualElement();
        spacer.style.flexGrow = 1;
        topBar.Add(spacer);

        var rightCol = new VisualElement();
        rightCol.pickingMode = PickingMode.Ignore;
        rightCol.style.alignItems = Align.FlexEnd;

        waveLabel = Lbl("Wave 1", 28, new Color(1f, 0.9f, 0.3f), FontStyle.Bold);
        rightCol.Add(waveLabel);

        floorRoomLabel = Lbl("Floor 1 — Room 1/10", 16, new Color(0.6f, 0.6f, 0.65f));
        floorRoomLabel.style.marginTop = 2;
        rightCol.Add(floorRoomLabel);

        // Momentum indicator
        var momentumRow = new VisualElement();
        momentumRow.pickingMode = PickingMode.Ignore;
        momentumRow.style.flexDirection = FlexDirection.Row;
        momentumRow.style.alignItems = Align.Center;
        momentumRow.style.marginTop = 6;

        momentumLabel = Lbl("", 14, new Color(1f, 0.8f, 0.2f), FontStyle.Bold);
        momentumRow.Add(momentumLabel);

        var momentumBarBg = new VisualElement();
        momentumBarBg.pickingMode = PickingMode.Ignore;
        momentumBarBg.style.width = 80;
        momentumBarBg.style.height = 6;
        momentumBarBg.style.marginLeft = 6;
        momentumBarBg.style.backgroundColor = new Color(0.15f, 0.12f, 0.08f);
        Radius(momentumBarBg, 3);

        momentumFill = new VisualElement();
        momentumFill.pickingMode = PickingMode.Ignore;
        momentumFill.style.height = new StyleLength(Length.Percent(100));
        momentumFill.style.width = new StyleLength(Length.Percent(0));
        momentumFill.style.backgroundColor = new Color(1f, 0.7f, 0.2f);
        Radius(momentumFill, 3);
        momentumBarBg.Add(momentumFill);
        momentumRow.Add(momentumBarBg);
        rightCol.Add(momentumRow);

        topBar.Add(rightCol);
        root.Add(topBar);

        // ── Gold display ──
        var goldRow = new VisualElement();
        goldRow.pickingMode = PickingMode.Ignore;
        goldRow.style.flexDirection = FlexDirection.Row;
        goldRow.style.alignItems = Align.Center;
        goldRow.style.paddingLeft = 20;
        goldRow.style.paddingTop = 4;

        var goldIcon = new VisualElement();
        goldIcon.style.width = 16;
        goldIcon.style.height = 16;
        Radius(goldIcon, 8);
        goldIcon.style.backgroundColor = new Color(1f, 0.85f, 0.2f);
        goldIcon.style.marginRight = 6;
        goldRow.Add(goldIcon);

        goldLabel = Lbl("0", 20, new Color(1f, 0.9f, 0.3f), FontStyle.Bold);
        goldRow.Add(goldLabel);
        root.Add(goldRow);

        // ── Relic bar ──
        relicBar = new VisualElement();
        relicBar.pickingMode = PickingMode.Ignore;
        relicBar.style.flexDirection = FlexDirection.Row;
        relicBar.style.paddingLeft = 20;
        relicBar.style.paddingTop = 4;
        root.Add(relicBar);

        // ── Boss HP bar ──
        bossHPContainer = new VisualElement();
        bossHPContainer.pickingMode = PickingMode.Ignore;
        bossHPContainer.style.alignItems = Align.Center;
        bossHPContainer.style.marginTop = 8;
        bossHPContainer.style.display = DisplayStyle.None;

        bossNameLabel = Lbl("BOSS", 22, new Color(1f, 0.3f, 0.3f), FontStyle.Bold);
        bossNameLabel.style.marginBottom = 4;
        bossHPContainer.Add(bossNameLabel);

        var bossBarBg = new VisualElement();
        bossBarBg.pickingMode = PickingMode.Ignore;
        bossBarBg.style.width = 500;
        bossBarBg.style.height = 20;
        bossBarBg.style.backgroundColor = new Color(0.15f, 0.05f, 0.05f);
        Radius(bossBarBg, 6);
        Border(bossBarBg, new Color(0.6f, 0.15f, 0.15f), 2);

        bossHPFill = new VisualElement();
        bossHPFill.pickingMode = PickingMode.Ignore;
        bossHPFill.style.height = new StyleLength(Length.Percent(100));
        bossHPFill.style.width = new StyleLength(Length.Percent(100));
        bossHPFill.style.backgroundColor = new Color(0.85f, 0.15f, 0.15f);
        Radius(bossHPFill, 4);
        bossBarBg.Add(bossHPFill);
        bossHPContainer.Add(bossBarBg);
        root.Add(bossHPContainer);

        // ── Death overlay ──
        deathOverlay = MakeOverlay(new Color(0, 0, 0, 0.75f));
        var deathTitle = Lbl("YOU DIED", 64, new Color(0.9f, 0.1f, 0.1f), FontStyle.Bold);
        deathOverlay.Add(deathTitle);
        deathWaveLabel = Lbl("Reached wave 1", 26, Dim);
        deathWaveLabel.style.marginTop = 12;
        deathOverlay.Add(deathWaveLabel);
        var restartHint = Lbl("Press  R  to return to the Sanctum", 22, new Color(0.7f, 0.7f, 0.7f));
        restartHint.style.marginTop = 24;
        deathOverlay.Add(restartHint);
        root.Add(deathOverlay);

        // ── Victory overlay ──
        victoryOverlay = MakeOverlay(new Color(0, 0, 0, 0.8f));
        var vicTitle = Lbl("VICTORY!", 64, new Color(1f, 0.85f, 0.2f), FontStyle.Bold);
        victoryOverlay.Add(vicTitle);
        var vicSub = Lbl("You have conquered the dungeon!", 24, new Color(0.8f, 0.8f, 0.85f));
        vicSub.style.marginTop = 12;
        victoryOverlay.Add(vicSub);
        var vicHint = Lbl("Press  R  to return to the Sanctum", 22, new Color(0.7f, 0.7f, 0.7f));
        vicHint.style.marginTop = 24;
        victoryOverlay.Add(vicHint);
        root.Add(victoryOverlay);

        // ── Upgrade selection overlay ──
        upgradeOverlay = MakeOverlay(new Color(0, 0, 0, 0.65f));
        var upgradeContainer = new VisualElement();
        upgradeContainer.style.alignItems = Align.Center;
        var upgradeTitle = Lbl("CHOOSE AN UPGRADE", 36, Color.white, FontStyle.Bold);
        upgradeTitle.style.marginBottom = 24;
        upgradeContainer.Add(upgradeTitle);
        upgradePanel = new VisualElement();
        upgradePanel.style.flexDirection = FlexDirection.Row;
        upgradePanel.style.justifyContent = Justify.Center;
        upgradeContainer.Add(upgradePanel);
        upgradeOverlay.Add(upgradeContainer);
        root.Add(upgradeOverlay);

        // ── Element unlock overlay ──
        elementUnlockOverlay = MakeOverlay(new Color(0, 0, 0, 0.7f));
        var unlockContainer = new VisualElement();
        unlockContainer.style.alignItems = Align.Center;
        var unlockTitle = Lbl("NEW ELEMENT UNLOCKED!", 36, new Color(1f, 0.85f, 0.2f), FontStyle.Bold);
        unlockTitle.style.marginBottom = 12;
        unlockContainer.Add(unlockTitle);
        var unlockSub = Lbl("Choose which slot to replace", 20, Dim);
        unlockSub.style.marginBottom = 24;
        unlockContainer.Add(unlockSub);
        elementUnlockPanel = new VisualElement();
        elementUnlockPanel.style.flexDirection = FlexDirection.Row;
        elementUnlockPanel.style.justifyContent = Justify.Center;
        unlockContainer.Add(elementUnlockPanel);
        elementUnlockOverlay.Add(unlockContainer);
        root.Add(elementUnlockOverlay);

        // ── Synergy selection overlay ──
        synergyOverlay = MakeOverlay(new Color(0, 0, 0, 0.7f));
        var synContainer = new VisualElement();
        synContainer.style.alignItems = Align.Center;
        var synTitle = Lbl("CHOOSE A SYNERGY", 36, new Color(0.8f, 0.6f, 1f), FontStyle.Bold);
        synTitle.style.marginBottom = 24;
        synContainer.Add(synTitle);
        synergyPanel = new VisualElement();
        synergyPanel.style.flexDirection = FlexDirection.Row;
        synergyPanel.style.justifyContent = Justify.Center;
        synContainer.Add(synergyPanel);
        synergyOverlay.Add(synContainer);
        root.Add(synergyOverlay);

        // ── Bottom area: element hotbar + combo display ──
        var bottomArea = new VisualElement();
        bottomArea.pickingMode = PickingMode.Ignore;
        bottomArea.style.position = Position.Absolute;
        bottomArea.style.bottom = 0;
        bottomArea.style.left = 0;
        bottomArea.style.right = 0;
        bottomArea.style.alignItems = Align.Center;
        bottomArea.style.paddingBottom = 16;

        // Combo name
        comboNameLabel = Lbl("", 24, Color.white, FontStyle.Bold);
        comboNameLabel.style.marginBottom = 8;
        bottomArea.Add(comboNameLabel);

        // Orb indicators
        var orbRow = new VisualElement();
        orbRow.pickingMode = PickingMode.Ignore;
        orbRow.style.flexDirection = FlexDirection.Row;
        orbRow.style.alignItems = Align.Center;
        orbRow.style.marginBottom = 8;

        orbLeftIndicator = new VisualElement();
        orbLeftIndicator.style.width = 32;
        orbLeftIndicator.style.height = 32;
        Radius(orbLeftIndicator, 16);
        Border(orbLeftIndicator, Color.white, 2);
        orbRow.Add(orbLeftIndicator);

        var plusLabel = Lbl("+", 24, Dim, FontStyle.Bold);
        plusLabel.style.marginLeft = 8;
        plusLabel.style.marginRight = 8;
        orbRow.Add(plusLabel);

        orbRightIndicator = new VisualElement();
        orbRightIndicator.style.width = 32;
        orbRightIndicator.style.height = 32;
        Radius(orbRightIndicator, 16);
        Border(orbRightIndicator, Color.white, 2);
        orbRow.Add(orbRightIndicator);

        bottomArea.Add(orbRow);

        // Element hotbar (1-4)
        elementHotbar = new VisualElement();
        elementHotbar.pickingMode = PickingMode.Ignore;
        elementHotbar.style.flexDirection = FlexDirection.Row;
        elementHotbar.style.alignItems = Align.FlexEnd;
        elementHotbar.style.marginBottom = 8;

        for (int i = 0; i < 4; i++)
        {
            var slot = new VisualElement();
            slot.pickingMode = PickingMode.Ignore;
            slot.style.width = 70;
            slot.style.height = 70;
            slot.style.marginLeft = 6;
            slot.style.marginRight = 6;
            slot.style.backgroundColor = CardBg;
            Radius(slot, 10);
            Border(slot, InactiveBorder, 2);
            slot.style.alignItems = Align.Center;
            slot.style.justifyContent = Justify.Center;

            var keyLabel = Lbl($"{i + 1}", 12, Dim, FontStyle.Bold);
            keyLabel.style.position = Position.Absolute;
            keyLabel.style.top = 4;
            keyLabel.style.left = 6;
            slot.Add(keyLabel);

            var elemIcon = new VisualElement();
            elemIcon.name = "elem-icon";
            elemIcon.style.width = 28;
            elemIcon.style.height = 28;
            Radius(elemIcon, 14);
            slot.Add(elemIcon);

            var chargeFill = new VisualElement();
            chargeFill.name = "charge-fill";
            chargeFill.pickingMode = PickingMode.Ignore;
            chargeFill.style.position = Position.Absolute;
            chargeFill.style.bottom = 0;
            chargeFill.style.left = 0;
            chargeFill.style.right = 0;
            chargeFill.style.height = new StyleLength(Length.Percent(0));
            chargeFill.style.backgroundColor = new Color(0.3f, 0.1f, 0.1f, 0.5f);
            Radius(chargeFill, 10);
            slot.Add(chargeFill);

            elementSlots[i] = slot;
            elementHotbar.Add(slot);
        }

        bottomArea.Add(elementHotbar);

        // Charge bar (for charged shot)
        chargeBar = new VisualElement();
        chargeBar.pickingMode = PickingMode.Ignore;
        chargeBar.style.width = 100;
        chargeBar.style.height = 6;
        chargeBar.style.backgroundColor = new Color(1f, 0.8f, 0.2f);
        Radius(chargeBar, 3);
        chargeBar.style.display = DisplayStyle.None;
        bottomArea.Add(chargeBar);

        // Controls hint
        var controls = Lbl("WASD Move   LMB Cast (hold to charge)   RMB Dash   1-4 Elements   F Potion", 14, new Color(0.35f, 0.35f, 0.4f));
        controls.style.marginTop = 6;
        bottomArea.Add(controls);

        // Dash cooldown
        dashCDContainer = new VisualElement();
        dashCDContainer.pickingMode = PickingMode.Ignore;
        dashCDContainer.style.width = 80;
        dashCDContainer.style.height = 8;
        dashCDContainer.style.marginTop = 4;
        dashCDContainer.style.backgroundColor = new Color(0.15f, 0.15f, 0.2f);
        Radius(dashCDContainer, 4);

        dashCDFill = new VisualElement();
        dashCDFill.pickingMode = PickingMode.Ignore;
        dashCDFill.style.height = new StyleLength(Length.Percent(100));
        dashCDFill.style.width = new StyleLength(Length.Percent(100));
        dashCDFill.style.backgroundColor = new Color(0.3f, 0.7f, 1f);
        Radius(dashCDFill, 4);
        dashCDContainer.Add(dashCDFill);
        bottomArea.Add(dashCDContainer);

        // Potion
        var potionRow = new VisualElement();
        potionRow.pickingMode = PickingMode.Ignore;
        potionRow.style.flexDirection = FlexDirection.Row;
        potionRow.style.alignItems = Align.Center;
        potionRow.style.marginTop = 4;

        var potionIcon = new VisualElement();
        potionIcon.style.width = 14;
        potionIcon.style.height = 14;
        Radius(potionIcon, 3);
        potionIcon.style.backgroundColor = new Color(0.3f, 0.9f, 0.4f);
        potionIcon.style.marginRight = 4;
        potionRow.Add(potionIcon);

        potionLabel = Lbl("[F] Potion x0", 13, new Color(0.3f, 0.9f, 0.4f), FontStyle.Bold);
        potionRow.Add(potionLabel);
        bottomArea.Add(potionRow);

        root.Add(bottomArea);

        // ── Damage number layer ──
        damageNumberLayer = new VisualElement();
        damageNumberLayer.pickingMode = PickingMode.Ignore;
        damageNumberLayer.style.position = Position.Absolute;
        damageNumberLayer.style.top = 0;
        damageNumberLayer.style.bottom = 0;
        damageNumberLayer.style.left = 0;
        damageNumberLayer.style.right = 0;
        root.Add(damageNumberLayer);
    }

    // ── Combo Display ──

    void RefreshComboDisplay()
    {
        if (caster == null) return;

        // Update orb indicators
        if (caster.leftOrb != null)
        {
            orbLeftIndicator.style.backgroundColor = caster.leftOrb.color;
            orbLeftIndicator.style.display = DisplayStyle.Flex;
        }
        else
            orbLeftIndicator.style.display = DisplayStyle.None;

        if (caster.rightOrb != null)
        {
            orbRightIndicator.style.backgroundColor = caster.rightOrb.color;
            orbRightIndicator.style.display = DisplayStyle.Flex;
        }
        else
            orbRightIndicator.style.display = DisplayStyle.None;

        // Update element hotbar
        for (int i = 0; i < 4; i++)
        {
            var elem = caster.equippedElements[i];
            var icon = elementSlots[i].Q("elem-icon");

            if (elem != null)
            {
                icon.style.backgroundColor = elem.color;

                // Overheat overlay
                var fill = elementSlots[i].Q("charge-fill");
                if (caster.IsOverheated(i))
                {
                    float progress = caster.GetOverheatProgress(i);
                    fill.style.height = new StyleLength(Length.Percent((1f - progress) * 100f));
                    fill.style.backgroundColor = new Color(0.5f, 0.1f, 0.1f, 0.6f);
                }
                else
                {
                    int ch = caster.GetCharges(i);
                    int max = elem.maxCharges;
                    float pct = 1f - (float)ch / max;
                    fill.style.height = new StyleLength(Length.Percent(pct * 100f));
                    fill.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.3f);
                }

                // Highlight if this element is active as an orb
                bool isActive = (caster.leftOrb == elem || caster.rightOrb == elem);
                Border(elementSlots[i], isActive ? ActiveBorder : InactiveBorder, isActive ? 3 : 2);
            }
            else
            {
                icon.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
            }
        }

        // Update combo name
        var def = caster.CurrentComboDef;
        if (def != null)
            comboNameLabel.text = def.comboName;
    }

    void OnComboNameChanged(string name)
    {
        if (comboNameLabel != null)
            comboNameLabel.text = name;
    }

    // ── HP ──

    void RefreshHP()
    {
        hpBar.Clear();
        for (int i = 0; i < playerHealth.maxHP; i++)
        {
            var block = new VisualElement();
            block.style.width = 36;
            block.style.height = 36;
            block.style.marginRight = 5;
            Radius(block, 6);
            bool full = i < playerHealth.currentHP;
            block.style.backgroundColor = full ? new Color(0.9f, 0.12f, 0.12f) : new Color(0.2f, 0.06f, 0.06f);
            if (full) Border(block, new Color(1f, 0.35f, 0.35f), 2);
            hpBar.Add(block);
        }
        var hpText = Lbl($"{playerHealth.currentHP}/{playerHealth.maxHP}", 22, new Color(0.95f, 0.3f, 0.3f), FontStyle.Bold);
        hpText.style.marginLeft = 10;
        hpBar.Add(hpText);
    }

    // ── Upgrade Selection (Dead Cells style) ──

    public void ShowUpgradeSelection(UpgradeType[] choices, Action<int> onSelect)
    {
        upgradeOverlay.style.display = DisplayStyle.Flex;
        upgradePanel.Clear();

        for (int i = 0; i < choices.Length; i++)
        {
            int idx = i;
            var upgrade = choices[i];
            var card = BuildUpgradeCard(upgrade, () =>
            {
                upgradeOverlay.style.display = DisplayStyle.None;
                onSelect?.Invoke(idx);
            });
            upgradePanel.Add(card);
        }
    }

    VisualElement BuildUpgradeCard(UpgradeType type, Action onClick)
    {
        Color col = RunUpgradeSystem.GetColor(type);
        string name = RunUpgradeSystem.GetName(type);
        string desc = RunUpgradeSystem.GetDescription(type);

        var card = new VisualElement();
        card.style.width = 220;
        card.style.marginLeft = 10;
        card.style.marginRight = 10;
        card.style.backgroundColor = CardBg;
        Radius(card, 14);
        Border(card, InactiveBorder, 2);
        card.style.overflow = Overflow.Hidden;

        card.RegisterCallback<ClickEvent>(_ => onClick?.Invoke());

        var header = new VisualElement();
        header.style.backgroundColor = col;
        Pad(header, 14, 18);
        header.Add(Lbl(name, 22, Color.white, FontStyle.Bold));
        card.Add(header);

        var body = new VisualElement();
        Pad(body, 14, 18);
        var descLbl = Lbl(desc, 16, new Color(0.8f, 0.8f, 0.85f));
        descLbl.style.whiteSpace = WhiteSpace.Normal;
        body.Add(descLbl);
        card.Add(body);

        card.RegisterCallback<MouseEnterEvent>(_ => { card.style.backgroundColor = CardBgHover; Border(card, col, 3); });
        card.RegisterCallback<MouseLeaveEvent>(_ => { card.style.backgroundColor = CardBg; Border(card, InactiveBorder, 2); });

        return card;
    }

    // ── Element Unlock ──

    public void ShowElementUnlock(ElementSO newElement, ElementSO[] currentSlots, Action<int> onSlotChosen)
    {
        elementUnlockOverlay.style.display = DisplayStyle.Flex;
        elementUnlockPanel.Clear();

        // Show new element being unlocked
        var newElemLabel = Lbl($"New: {newElement.elementName}", 24, newElement.color, FontStyle.Bold);
        newElemLabel.style.marginBottom = 16;
        // Insert before panel
        var container = elementUnlockOverlay.Q<VisualElement>();

        for (int i = 0; i < currentSlots.Length; i++)
        {
            if (currentSlots[i] == null) continue;
            int idx = i;
            var slot = currentSlots[i];

            var card = new VisualElement();
            card.style.width = 150;
            card.style.height = 120;
            card.style.marginLeft = 8;
            card.style.marginRight = 8;
            card.style.backgroundColor = CardBg;
            Radius(card, 12);
            Border(card, InactiveBorder, 2);
            card.style.alignItems = Align.Center;
            card.style.justifyContent = Justify.Center;

            var keyLbl = Lbl($"[{i + 1}]", 14, Dim, FontStyle.Bold);
            card.Add(keyLbl);

            var icon = new VisualElement();
            icon.style.width = 36;
            icon.style.height = 36;
            Radius(icon, 18);
            icon.style.backgroundColor = slot.color;
            icon.style.marginTop = 8;
            icon.style.marginBottom = 8;
            card.Add(icon);

            var nameLbl = Lbl(slot.elementName, 16, slot.color, FontStyle.Bold);
            card.Add(nameLbl);

            var replaceLbl = Lbl("REPLACE", 12, new Color(1f, 0.4f, 0.3f), FontStyle.Bold);
            replaceLbl.style.marginTop = 4;
            card.Add(replaceLbl);

            card.RegisterCallback<ClickEvent>(_ =>
            {
                elementUnlockOverlay.style.display = DisplayStyle.None;
                onSlotChosen?.Invoke(idx);
            });
            card.RegisterCallback<MouseEnterEvent>(_ => { Border(card, newElement.color, 3); });
            card.RegisterCallback<MouseLeaveEvent>(_ => { Border(card, InactiveBorder, 2); });

            elementUnlockPanel.Add(card);
        }
    }

    // ── Synergy Selection ──

    public void ShowSynergySelection(SynergyDef[] choices, Action<int> onSelect)
    {
        synergyOverlay.style.display = DisplayStyle.Flex;
        synergyPanel.Clear();

        for (int i = 0; i < choices.Length; i++)
        {
            int idx = i;
            var syn = choices[i];

            var card = new VisualElement();
            card.style.width = 250;
            card.style.marginLeft = 10;
            card.style.marginRight = 10;
            card.style.backgroundColor = CardBg;
            Radius(card, 14);
            Border(card, InactiveBorder, 2);
            card.style.overflow = Overflow.Hidden;

            card.RegisterCallback<ClickEvent>(_ =>
            {
                synergyOverlay.style.display = DisplayStyle.None;
                onSelect?.Invoke(idx);
            });

            var header = new VisualElement();
            header.style.backgroundColor = syn.color;
            Pad(header, 14, 18);
            header.Add(Lbl(syn.name, 24, Color.white, FontStyle.Bold));
            card.Add(header);

            var body = new VisualElement();
            Pad(body, 14, 18);
            var descLbl = Lbl(syn.description, 16, new Color(0.8f, 0.8f, 0.85f));
            descLbl.style.whiteSpace = WhiteSpace.Normal;
            body.Add(descLbl);
            card.Add(body);

            card.RegisterCallback<MouseEnterEvent>(_ => { card.style.backgroundColor = CardBgHover; Border(card, syn.color, 3); });
            card.RegisterCallback<MouseLeaveEvent>(_ => { card.style.backgroundColor = CardBg; Border(card, InactiveBorder, 2); });

            synergyPanel.Add(card);
        }
    }

    // ── Relic Selection (reused from old system) ──

    public void ShowRelicSelection(RelicSO[] options, Action<int> onSelect)
    {
        upgradeOverlay.style.display = DisplayStyle.Flex;
        upgradePanel.Clear();

        for (int i = 0; i < options.Length; i++)
        {
            int idx = i;
            var relic = options[i];
            var card = new VisualElement();
            card.style.width = 220;
            card.style.marginLeft = 10;
            card.style.marginRight = 10;
            card.style.backgroundColor = CardBg;
            Radius(card, 14);
            Border(card, InactiveBorder, 2);
            card.style.overflow = Overflow.Hidden;

            card.RegisterCallback<ClickEvent>(_ =>
            {
                upgradeOverlay.style.display = DisplayStyle.None;
                onSelect?.Invoke(idx);
            });

            var header = new VisualElement();
            header.style.backgroundColor = relic.color;
            Pad(header, 14, 18);
            header.Add(Lbl(relic.relicName, 22, Color.white, FontStyle.Bold));
            if (relic.isCursed)
            {
                var cursedTag = Lbl("CURSED", 12, new Color(1f, 0.3f, 0.3f), FontStyle.Bold);
                cursedTag.style.marginTop = 2;
                header.Add(cursedTag);
            }
            card.Add(header);

            var body = new VisualElement();
            Pad(body, 14, 18);
            var descLbl = Lbl(relic.description, 16, new Color(0.8f, 0.8f, 0.85f));
            descLbl.style.whiteSpace = WhiteSpace.Normal;
            body.Add(descLbl);
            card.Add(body);

            card.RegisterCallback<MouseEnterEvent>(_ => { card.style.backgroundColor = CardBgHover; Border(card, relic.color, 3); });
            card.RegisterCallback<MouseLeaveEvent>(_ => { card.style.backgroundColor = CardBg; Border(card, InactiveBorder, 2); });

            upgradePanel.Add(card);
        }
    }

    // ── Event room (reused) ──

    public void ShowEventRoom(string title, string subtitle, Color titleColor,
        string[] labels, string[] descriptions, Color[] colors, Action<int> onChoice)
    {
        upgradeOverlay.style.display = DisplayStyle.Flex;
        upgradePanel.Clear();

        for (int i = 0; i < labels.Length; i++)
        {
            int idx = i;
            var card = new VisualElement();
            card.style.width = 200;
            card.style.marginLeft = 10;
            card.style.marginRight = 10;
            card.style.backgroundColor = CardBg;
            Radius(card, 14);
            Border(card, InactiveBorder, 2);
            card.style.overflow = Overflow.Hidden;

            card.RegisterCallback<ClickEvent>(_ =>
            {
                upgradeOverlay.style.display = DisplayStyle.None;
                onChoice?.Invoke(idx);
            });

            var header = new VisualElement();
            header.style.backgroundColor = colors[i];
            Pad(header, 12, 14);
            header.Add(Lbl(labels[i], 20, Color.white, FontStyle.Bold));
            card.Add(header);

            var body = new VisualElement();
            Pad(body, 12, 14);
            var descLbl = Lbl(descriptions[i], 14, new Color(0.8f, 0.8f, 0.85f));
            descLbl.style.whiteSpace = WhiteSpace.Normal;
            body.Add(descLbl);
            card.Add(body);

            card.RegisterCallback<MouseEnterEvent>(_ => { Border(card, colors[idx], 3); });
            card.RegisterCallback<MouseLeaveEvent>(_ => { Border(card, InactiveBorder, 2); });

            upgradePanel.Add(card);
        }
    }

    // ── Public API ──

    public void SetWave(int w) { if (waveLabel != null) waveLabel.text = $"Wave {w}"; }
    public void SetWave(int w, int sub, int total) { if (waveLabel != null) waveLabel.text = $"Wave {w} ({sub}/{total})"; }
    public void SetFloorRoom(int floor, int room) { if (floorRoomLabel != null) floorRoomLabel.text = $"Floor {floor} — Room {room}/10"; }
    public void SetGold(int gold) { if (goldLabel != null) goldLabel.text = gold.ToString(); }
    public void Refresh() { RefreshHP(); RefreshComboDisplay(); }

    public void ShowDeath(bool show, int metaCurrency = 0)
    {
        deathOverlay.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        if (show)
            deathWaveLabel.text = metaCurrency > 0 ? $"+{metaCurrency} Soul Essence" : "";
    }

    public void ShowVictory(int wave, int floor)
    {
        victoryOverlay.style.display = DisplayStyle.Flex;
    }

    public void ShowBossHP(Health bossHP, string bossName)
    {
        trackedBossHP = bossHP;
        bossHPContainer.style.display = DisplayStyle.Flex;
        bossNameLabel.text = bossName;
        bossHP.OnHPChanged += (cur, max) =>
        {
            float pct = max > 0 ? (float)cur / max : 0;
            bossHPFill.style.width = new StyleLength(Length.Percent(pct * 100f));
        };
        bossHP.OnDeath += () => bossHPContainer.style.display = DisplayStyle.None;
    }

    public void HideBossHP()
    {
        bossHPContainer.style.display = DisplayStyle.None;
        trackedBossHP = null;
    }

    public void RefreshRelics(List<RelicSO> relics)
    {
        relicBar.Clear();
        foreach (var r in relics)
        {
            var icon = new VisualElement();
            icon.style.width = 24;
            icon.style.height = 24;
            Radius(icon, 6);
            icon.style.backgroundColor = r.color;
            icon.style.marginRight = 4;
            Border(icon, r.isCursed ? new Color(0.8f, 0.2f, 0.2f) : new Color(0.5f, 0.5f, 0.5f), 1);
            relicBar.Add(icon);
        }
    }

    // ── Shop / Devil Deal / Rest ──

    public void ShowShopRoom(RelicSO[] allRelics, RelicManager relicMgr, int price, int gold, Action<RelicSO> onBuy)
    {
        var available = new List<RelicSO>();
        foreach (var r in allRelics)
            if (!relicMgr.HasRelic(r.relicType) && !r.isCursed) available.Add(r);

        if (available.Count == 0) { onBuy(null); return; }

        int count = Mathf.Min(3, available.Count);
        var options = new RelicSO[count];
        for (int i = 0; i < count; i++)
        {
            int idx = UnityEngine.Random.Range(0, available.Count);
            options[i] = available[idx];
            available.RemoveAt(idx);
        }

        upgradeOverlay.style.display = DisplayStyle.Flex;
        upgradePanel.Clear();

        for (int i = 0; i < count; i++)
        {
            var relic = options[i];
            var card = new VisualElement();
            card.style.width = 220;
            card.style.marginLeft = 10;
            card.style.marginRight = 10;
            card.style.backgroundColor = CardBg;
            Radius(card, 14);
            Border(card, InactiveBorder, 2);
            card.style.overflow = Overflow.Hidden;

            card.RegisterCallback<ClickEvent>(_ =>
            {
                upgradeOverlay.style.display = DisplayStyle.None;
                onBuy(gold >= price ? relic : null);
            });

            var header = new VisualElement();
            header.style.backgroundColor = relic.color;
            Pad(header, 14, 18);
            header.Add(Lbl(relic.relicName, 22, Color.white, FontStyle.Bold));
            var priceLbl = Lbl($"{price} gold", 14, new Color(1f, 0.9f, 0.3f), FontStyle.Bold);
            priceLbl.style.marginTop = 2;
            header.Add(priceLbl);
            card.Add(header);

            var body = new VisualElement();
            Pad(body, 14, 18);
            var descLbl = Lbl(relic.description, 16, new Color(0.8f, 0.8f, 0.85f));
            descLbl.style.whiteSpace = WhiteSpace.Normal;
            body.Add(descLbl);
            card.Add(body);

            card.RegisterCallback<MouseEnterEvent>(_ => { Border(card, relic.color, 3); });
            card.RegisterCallback<MouseLeaveEvent>(_ => { Border(card, InactiveBorder, 2); });

            upgradePanel.Add(card);
        }

        // Skip button
        var skip = new VisualElement();
        skip.style.width = 120;
        skip.style.marginLeft = 10;
        skip.style.backgroundColor = CardBg;
        Radius(skip, 14);
        Border(skip, Dim, 2);
        skip.style.alignItems = Align.Center;
        skip.style.justifyContent = Justify.Center;
        Pad(skip, 20, 16);
        skip.Add(Lbl("SKIP", 20, Dim, FontStyle.Bold));
        skip.RegisterCallback<ClickEvent>(_ =>
        {
            upgradeOverlay.style.display = DisplayStyle.None;
            onBuy(null);
        });
        upgradePanel.Add(skip);
    }

    public void ShowDevilDeal(List<RelicSO> cursedRelics, RelicManager relicMgr, Health playerHP, Action<RelicSO> onChoice)
    {
        int count = Mathf.Min(2, cursedRelics.Count);
        var options = new RelicSO[count];
        for (int i = 0; i < count; i++)
        {
            int idx = UnityEngine.Random.Range(0, cursedRelics.Count);
            options[i] = cursedRelics[idx];
            cursedRelics.RemoveAt(idx);
        }

        ShowEventRoom("DEVIL'S DEAL", "Trade your vitality for power...",
            new Color(0.6f, 0.05f, 0.1f),
            count >= 2 ? new[] { options[0].relicName, options[1].relicName, "LEAVE" }
                       : new[] { options[0].relicName, "LEAVE" },
            count >= 2 ? new[] { $"{options[0].description}\nCost: -1 max HP", $"{options[1].description}\nCost: -1 max HP", "Walk away safely" }
                       : new[] { $"{options[0].description}\nCost: -1 max HP", "Walk away safely" },
            count >= 2 ? new[] { options[0].color, options[1].color, new Color(0.5f, 0.5f, 0.5f) }
                       : new[] { options[0].color, new Color(0.5f, 0.5f, 0.5f) },
            choice =>
            {
                if (choice < count)
                    onChoice(options[choice]);
                else
                    onChoice(null);
            });
    }

    public void ShowRestRoom(Action onContinue)
    {
        ShowEventRoom("REST", "A moment of peace. You are fully healed.",
            new Color(0.3f, 0.9f, 0.5f),
            new[] { "CONTINUE" },
            new[] { "Proceed to the next room" },
            new[] { new Color(0.3f, 0.9f, 0.5f) },
            _ => onContinue());
    }

    // ── Update (damage numbers, charge bar, cooldowns) ──

    void Update()
    {
        // Damage numbers
        for (int i = activeDamageNumbers.Count - 1; i >= 0; i--)
        {
            var dn = activeDamageNumbers[i];
            dn.elapsed += Time.deltaTime;
            dn.worldPos += dn.velocity * Time.deltaTime;
            dn.velocity.y += 3f * Time.deltaTime;

            if (Camera.main != null)
            {
                Vector3 screen = Camera.main.WorldToScreenPoint(dn.worldPos);
                if (screen.z > 0)
                {
                    float x = screen.x;
                    float y = Screen.height - screen.y;
                    dn.label.style.left = x - 20;
                    dn.label.style.top = y;
                }
            }

            float alpha = 1f - (dn.elapsed / dn.duration);
            dn.label.style.opacity = alpha;

            if (dn.elapsed >= dn.duration)
            {
                damageNumberLayer.Remove(dn.label);
                activeDamageNumbers.RemoveAt(i);
            }
            else
            {
                activeDamageNumbers[i] = dn;
            }
        }

        // Charge bar
        if (caster != null)
        {
            if (caster.IsCharging)
            {
                chargeBar.style.display = DisplayStyle.Flex;
                chargeBar.style.width = 100 * caster.ChargeProgress;
            }
            else
            {
                chargeBar.style.display = DisplayStyle.None;
            }

            // Cooldown visual
            float cd = caster.CooldownNormalized;
            // Could apply to combo display

            // Refresh element charges each frame
            RefreshComboDisplay();
        }

        // Dash cooldown
        if (playerCtrl != null)
        {
            float dashPct = 1f - playerCtrl.DashCooldownNormalized;
            dashCDFill.style.width = new StyleLength(Length.Percent(dashPct * 100));
        }

        // Potion
        if (playerCtrl != null)
            potionLabel.text = $"[F] Potion x{playerCtrl.PotionsRemaining}";

        // Momentum
        if (playerCtrl != null && momentumLabel != null)
        {
            var momentum = playerCtrl.GetComponent<MomentumSystem>();
            if (momentum != null)
            {
                int mTier = momentum.Tier;
                if (mTier > 0)
                {
                    Color tc = MomentumSystem.TierColors[mTier];
                    momentumLabel.text = $"x{momentum.DamageMultiplier:F2} {MomentumSystem.TierNames[mTier]}";
                    momentumLabel.style.color = tc;

                    // Fill bar: progress toward next tier
                    float[] thresholds = { 0, 3, 6, 10, 15 };
                    float current = momentum.KillStreak;
                    float nextThreshold = mTier < 4 ? thresholds[mTier + 1] : thresholds[4];
                    float currentThreshold = thresholds[mTier];
                    float pct = (current - currentThreshold) / Mathf.Max(1, nextThreshold - currentThreshold);
                    momentumFill.style.width = new StyleLength(Length.Percent(Mathf.Clamp01(pct) * 100));
                    momentumFill.style.backgroundColor = tc;
                }
                else
                {
                    momentumLabel.text = "";
                    momentumFill.style.width = new StyleLength(Length.Percent(0));
                }
            }
        }
    }

    void SpawnDamageNumber(int amount, Vector3 worldPos, bool killed)
    {
        var lbl = new Label(amount.ToString());
        lbl.pickingMode = PickingMode.Ignore;
        lbl.style.position = Position.Absolute;
        lbl.style.fontSize = killed ? 32 : (amount >= 8 ? 28 : 22);
        lbl.style.color = killed ? new Color(1f, 0.3f, 0.2f) :
                          (amount >= 8 ? new Color(1f, 0.85f, 0.2f) : Color.white);
        lbl.style.unityFontStyleAndWeight = FontStyle.Bold;

        damageNumberLayer.Add(lbl);

        activeDamageNumbers.Add(new DamageNumberState
        {
            label = lbl,
            worldPos = worldPos,
            elapsed = 0,
            duration = 0.8f,
            velocity = new Vector3(UnityEngine.Random.Range(-1f, 1f), 2f, 0)
        });
    }

    // ── Helpers ──

    static Label Lbl(string text, int size, Color color, FontStyle style = FontStyle.Normal)
    {
        var l = new Label(text);
        l.pickingMode = PickingMode.Ignore;
        l.style.fontSize = size;
        l.style.color = color;
        l.style.unityFontStyleAndWeight = style;
        return l;
    }

    VisualElement Pill(string text, Color bg, Color fg)
    {
        var pill = new VisualElement();
        pill.style.backgroundColor = bg;
        pill.style.paddingLeft = 10;
        pill.style.paddingRight = 10;
        pill.style.paddingTop = 4;
        pill.style.paddingBottom = 4;
        Radius(pill, 8);
        pill.style.marginRight = 6;
        pill.Add(Lbl(text, 14, fg, FontStyle.Bold));
        return pill;
    }

    static void Radius(VisualElement e, float r)
    {
        e.style.borderTopLeftRadius = r;
        e.style.borderTopRightRadius = r;
        e.style.borderBottomLeftRadius = r;
        e.style.borderBottomRightRadius = r;
    }

    static void Border(VisualElement e, Color c, float w)
    {
        e.style.borderTopColor = c;
        e.style.borderBottomColor = c;
        e.style.borderLeftColor = c;
        e.style.borderRightColor = c;
        e.style.borderTopWidth = w;
        e.style.borderBottomWidth = w;
        e.style.borderLeftWidth = w;
        e.style.borderRightWidth = w;
    }

    static void Pad(VisualElement e, float tb, float lr)
    {
        e.style.paddingTop = tb;
        e.style.paddingBottom = tb;
        e.style.paddingLeft = lr;
        e.style.paddingRight = lr;
    }

    static VisualElement MakeOverlay(Color bg)
    {
        var overlay = new VisualElement();
        overlay.style.position = Position.Absolute;
        overlay.style.top = 0;
        overlay.style.bottom = 0;
        overlay.style.left = 0;
        overlay.style.right = 0;
        overlay.style.backgroundColor = bg;
        overlay.style.alignItems = Align.Center;
        overlay.style.justifyContent = Justify.Center;
        overlay.style.display = DisplayStyle.None;
        return overlay;
    }
}
