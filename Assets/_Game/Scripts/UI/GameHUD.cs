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

    // Combo preview tooltip
    VisualElement comboPreviewContainer;
    VisualElement previewOrbLeft, previewOrbRight;
    Label previewNameLabel;
    Label previewStatsLabel;
    int hoveredSlot = -1;

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

    // Encounter objective
    Label encounterObjectiveLabel;

    // Variety bonus
    Label varietyBonusLabel;

    // Gold gain popup
    Label goldGainLabel;
    float goldGainTimer;

    // Synergy bar
    VisualElement synergyBar;

    // Minimap
    VisualElement minimapContainer;

    // Pause menu
    VisualElement pauseOverlay;
    bool isPaused;
    public Action OnReturnToHub;

    // Tutorial hints
    VisualElement hintBar;
    Label hintLabel;
    float hintTimer;
    float hintDuration;

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

        // Damage vignette feedback
        var vignette = gameObject.AddComponent<DamageVignette>();
        vignette.Init(uiDoc, playerHealth);
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

        // ── Encounter objective ──
        encounterObjectiveLabel = Lbl("", 20, new Color(1f, 0.85f, 0.2f), FontStyle.Bold);
        encounterObjectiveLabel.pickingMode = PickingMode.Ignore;
        encounterObjectiveLabel.style.position = Position.Absolute;
        encounterObjectiveLabel.style.top = 60;
        encounterObjectiveLabel.style.left = 0;
        encounterObjectiveLabel.style.right = 0;
        encounterObjectiveLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        encounterObjectiveLabel.style.display = DisplayStyle.None;
        root.Add(encounterObjectiveLabel);

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

        goldGainLabel = Lbl("", 16, new Color(1f, 1f, 0.5f), FontStyle.Bold);
        goldGainLabel.style.marginLeft = 8;
        goldGainLabel.style.display = DisplayStyle.None;
        goldRow.Add(goldGainLabel);

        root.Add(goldRow);

        // ── Relic bar ──
        relicBar = new VisualElement();
        relicBar.pickingMode = PickingMode.Ignore;
        relicBar.style.flexDirection = FlexDirection.Row;
        relicBar.style.paddingLeft = 20;
        relicBar.style.paddingTop = 4;
        root.Add(relicBar);

        // ── Synergy bar ──
        synergyBar = new VisualElement();
        synergyBar.pickingMode = PickingMode.Ignore;
        synergyBar.style.flexDirection = FlexDirection.Row;
        synergyBar.style.paddingLeft = 20;
        synergyBar.style.paddingTop = 2;
        root.Add(synergyBar);

        // ── Variety bonus indicator ──
        varietyBonusLabel = Lbl("", 14, new Color(0.6f, 0.9f, 1f), FontStyle.Bold);
        varietyBonusLabel.pickingMode = PickingMode.Ignore;
        varietyBonusLabel.style.paddingLeft = 20;
        varietyBonusLabel.style.display = DisplayStyle.None;
        root.Add(varietyBonusLabel);

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

        // Phase-2 marker at 50%
        var phaseMarker = new VisualElement();
        phaseMarker.pickingMode = PickingMode.Ignore;
        phaseMarker.style.position = Position.Absolute;
        phaseMarker.style.left = new StyleLength(Length.Percent(50));
        phaseMarker.style.top = 0;
        phaseMarker.style.bottom = 0;
        phaseMarker.style.width = 2;
        phaseMarker.style.backgroundColor = new Color(1f, 1f, 1f, 0.6f);
        bossBarBg.Add(phaseMarker);

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

        // ── Pause overlay ──
        pauseOverlay = MakeOverlay(new Color(0, 0, 0, 0.8f));
        pauseOverlay.focusable = true;

        var pauseTitle = Lbl("PAUSED", 64, new Color(0.9f, 0.85f, 0.6f), FontStyle.Bold);
        pauseOverlay.Add(pauseTitle);

        var resumeBtn = new Button(() => ResumeGame());
        resumeBtn.text = "Resume";
        resumeBtn.style.fontSize = 24;
        resumeBtn.style.marginTop = 24;
        resumeBtn.style.paddingTop = 10;
        resumeBtn.style.paddingBottom = 10;
        resumeBtn.style.paddingLeft = 40;
        resumeBtn.style.paddingRight = 40;
        resumeBtn.style.color = Color.white;
        resumeBtn.style.backgroundColor = new Color(0.2f, 0.5f, 0.3f);
        Radius(resumeBtn, 8);
        pauseOverlay.Add(resumeBtn);

        // Volume slider
        var volumeRow = new VisualElement();
        volumeRow.style.flexDirection = FlexDirection.Row;
        volumeRow.style.alignItems = Align.Center;
        volumeRow.style.marginTop = 20;

        var volLabel = Lbl("Volume", 20, Color.white);
        volLabel.style.marginRight = 12;
        volumeRow.Add(volLabel);

        var volumeSlider = new Slider(0f, 1f);
        volumeSlider.value = AudioListener.volume;
        volumeSlider.style.width = 200;
        volumeSlider.RegisterValueChangedCallback(evt => AudioListener.volume = evt.newValue);
        volumeRow.Add(volumeSlider);
        pauseOverlay.Add(volumeRow);

        // Hub return with confirmation
        var hubConfirmRow = new VisualElement();
        hubConfirmRow.style.alignItems = Align.Center;
        hubConfirmRow.style.marginTop = 16;

        var hubBtn = new Button();
        hubBtn.text = "Return to Hub";
        hubBtn.style.fontSize = 20;
        hubBtn.style.paddingTop = 8;
        hubBtn.style.paddingBottom = 8;
        hubBtn.style.paddingLeft = 30;
        hubBtn.style.paddingRight = 30;
        hubBtn.style.color = Color.white;
        hubBtn.style.backgroundColor = new Color(0.5f, 0.2f, 0.2f);
        Radius(hubBtn, 8);

        var confirmLabel = Lbl("Are you sure? Your run progress will be lost!", 14, new Color(1f, 0.5f, 0.3f));
        confirmLabel.style.marginTop = 8;
        confirmLabel.style.display = DisplayStyle.None;

        var confirmBtn = new Button(() =>
        {
            ResumeGame();
            OnReturnToHub?.Invoke();
        });
        confirmBtn.text = "Yes, abandon run";
        confirmBtn.style.fontSize = 16;
        confirmBtn.style.marginTop = 6;
        confirmBtn.style.paddingTop = 6;
        confirmBtn.style.paddingBottom = 6;
        confirmBtn.style.paddingLeft = 20;
        confirmBtn.style.paddingRight = 20;
        confirmBtn.style.color = Color.white;
        confirmBtn.style.backgroundColor = new Color(0.6f, 0.15f, 0.15f);
        confirmBtn.style.display = DisplayStyle.None;
        Radius(confirmBtn, 6);

        hubBtn.clicked += () =>
        {
            confirmLabel.style.display = DisplayStyle.Flex;
            confirmBtn.style.display = DisplayStyle.Flex;
        };

        hubConfirmRow.Add(hubBtn);
        hubConfirmRow.Add(confirmLabel);
        hubConfirmRow.Add(confirmBtn);
        pauseOverlay.Add(hubConfirmRow);

        root.Add(pauseOverlay);

        // ── Hint bar (tutorial) ──
        hintBar = new VisualElement();
        hintBar.pickingMode = PickingMode.Ignore;
        hintBar.style.position = Position.Absolute;
        hintBar.style.top = 80;
        hintBar.style.left = new StyleLength(Length.Percent(25));
        hintBar.style.right = new StyleLength(Length.Percent(25));
        hintBar.style.backgroundColor = new Color(0, 0, 0, 0.6f);
        hintBar.style.alignItems = Align.Center;
        hintBar.style.justifyContent = Justify.Center;
        Pad(hintBar, 10, 20);
        Radius(hintBar, 8);
        hintBar.style.display = DisplayStyle.None;

        hintLabel = Lbl("", 22, Color.white, FontStyle.Bold);
        hintBar.Add(hintLabel);
        root.Add(hintBar);

        // ── Bottom area: element hotbar + combo display ──
        var bottomArea = new VisualElement();
        bottomArea.pickingMode = PickingMode.Ignore;
        bottomArea.style.position = Position.Absolute;
        bottomArea.style.bottom = 0;
        bottomArea.style.left = 0;
        bottomArea.style.right = 0;
        bottomArea.style.alignItems = Align.Center;
        bottomArea.style.paddingBottom = 16;

        // Combo preview tooltip (shown on hover/key-hold)
        comboPreviewContainer = new VisualElement();
        comboPreviewContainer.pickingMode = PickingMode.Ignore;
        comboPreviewContainer.style.flexDirection = FlexDirection.Row;
        comboPreviewContainer.style.alignItems = Align.Center;
        comboPreviewContainer.style.backgroundColor = new Color(0.08f, 0.08f, 0.14f, 0.85f);
        Radius(comboPreviewContainer, 8);
        Pad(comboPreviewContainer, 6, 12);
        comboPreviewContainer.style.marginBottom = 6;
        comboPreviewContainer.style.display = DisplayStyle.None;

        previewOrbLeft = new VisualElement();
        previewOrbLeft.style.width = 16;
        previewOrbLeft.style.height = 16;
        Radius(previewOrbLeft, 8);
        comboPreviewContainer.Add(previewOrbLeft);

        var previewPlus = Lbl("+", 14, Dim);
        previewPlus.style.marginLeft = 4;
        previewPlus.style.marginRight = 4;
        comboPreviewContainer.Add(previewPlus);

        previewOrbRight = new VisualElement();
        previewOrbRight.style.width = 16;
        previewOrbRight.style.height = 16;
        Radius(previewOrbRight, 8);
        comboPreviewContainer.Add(previewOrbRight);

        var previewSpacer = new VisualElement();
        previewSpacer.style.width = 10;
        comboPreviewContainer.Add(previewSpacer);

        previewNameLabel = Lbl("", 18, new Color(1f, 1f, 1f, 0.8f), FontStyle.Bold);
        comboPreviewContainer.Add(previewNameLabel);

        var previewSpacer2 = new VisualElement();
        previewSpacer2.style.width = 10;
        comboPreviewContainer.Add(previewSpacer2);

        previewStatsLabel = Lbl("", 14, Dim);
        comboPreviewContainer.Add(previewStatsLabel);

        bottomArea.Add(comboPreviewContainer);

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

            // Hover preview callbacks
            int slotIdx = i;
            slot.pickingMode = PickingMode.Position; // Enable mouse events for hover
            slot.RegisterCallback<MouseEnterEvent>(_ => { hoveredSlot = slotIdx; RefreshComboPreview(); });
            slot.RegisterCallback<MouseLeaveEvent>(_ => { if (hoveredSlot == slotIdx) { hoveredSlot = -1; RefreshComboPreview(); } });

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

        // ── Minimap ──
        BuildMinimap(root);

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

    // ── Minimap ──

    void BuildMinimap(VisualElement root)
    {
        minimapContainer = new VisualElement();
        minimapContainer.style.position = Position.Absolute;
        minimapContainer.style.top = 10;
        minimapContainer.style.right = 10;
        minimapContainer.style.width = 180;
        minimapContainer.style.height = 200;
        minimapContainer.style.backgroundColor = new Color(0.05f, 0.05f, 0.1f, 0.8f);
        Border(minimapContainer, new Color(0.3f, 0.3f, 0.4f), 1);
        Pad(minimapContainer, 5, 5);
        minimapContainer.pickingMode = PickingMode.Ignore;
        root.Add(minimapContainer);
    }

    public void UpdateMinimap(int currentRoom, int totalRooms, string[] roomTypes)
    {
        if (minimapContainer == null) return;
        minimapContainer.Clear();

        // Title
        var title = Lbl("Floor Map", 10, new Color(0.6f, 0.6f, 0.7f));
        title.style.unityTextAlign = TextAnchor.MiddleCenter;
        title.style.marginBottom = 4;
        minimapContainer.Add(title);

        // Node grid
        var grid = new VisualElement();
        grid.style.flexDirection = FlexDirection.Row;
        grid.style.flexWrap = Wrap.Wrap;
        grid.style.justifyContent = Justify.Center;
        grid.pickingMode = PickingMode.Ignore;

        for (int i = 0; i < totalRooms; i++)
        {
            var node = new VisualElement();
            node.style.width = 28;
            node.style.height = 28;
            node.style.marginRight = 4;
            node.style.marginBottom = 4;
            Radius(node, 14);
            node.pickingMode = PickingMode.Ignore;

            Color nodeColor;
            if (i + 1 == currentRoom)
                nodeColor = new Color(1f, 1f, 0.3f);       // Current = yellow
            else if (i + 1 < currentRoom)
                nodeColor = new Color(0.3f, 0.3f, 0.3f);   // Visited = gray
            else if (i + 1 == totalRooms)
                nodeColor = new Color(0.8f, 0.2f, 0.3f);   // Boss = red
            else if (roomTypes != null && i < roomTypes.Length)
            {
                nodeColor = roomTypes[i] switch
                {
                    "Shop"     => new Color(0.3f, 0.8f, 0.3f),
                    "Rest"     => new Color(0.8f, 0.8f, 0.8f),
                    "Event"    => new Color(0.3f, 0.5f, 0.9f),
                    "Elite"    => new Color(1f, 0.5f, 0.2f),
                    "Treasure" => new Color(1f, 0.8f, 0.2f),
                    _          => new Color(0.5f, 0.4f, 0.4f)
                };
            }
            else
                nodeColor = new Color(0.4f, 0.4f, 0.4f);   // Unknown

            node.style.backgroundColor = nodeColor;

            var label = Lbl((i + 1).ToString(), 9, Color.white);
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.width = 28;
            label.style.height = 28;
            node.Add(label);

            grid.Add(node);
        }

        minimapContainer.Add(grid);
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

    // ── Combo Preview ──

    void RefreshComboPreview()
    {
        if (hoveredSlot < 0 || caster == null)
        {
            comboPreviewContainer.style.display = DisplayStyle.None;
            return;
        }

        var def = caster.PreviewPush(hoveredSlot);
        if (def == null)
        {
            comboPreviewContainer.style.display = DisplayStyle.None;
            return;
        }

        comboPreviewContainer.style.display = DisplayStyle.Flex;

        // Preview orbs: left = current rightOrb (will become left), right = hovered element
        Color leftColor = caster.rightOrb != null ? caster.rightOrb.color : Color.gray;
        Color rightColor = caster.GetSlotColor(hoveredSlot);
        previewOrbLeft.style.backgroundColor = leftColor;
        previewOrbRight.style.backgroundColor = rightColor;

        previewNameLabel.text = def.comboName;
        previewStatsLabel.text = $"{def.baseDamage:F0} dmg | {def.radius:F1}m | {def.cooldown:F1}s cd";
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

    public void ShowDeath(bool show, int metaCurrency = 0, int floor = 0, int room = 0, int kills = 0,
        int relicCount = 0)
    {
        deathOverlay.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        if (show)
        {
            deathOverlay.Clear();

            // Ensure dark overlay background
            deathOverlay.style.backgroundColor = new Color(0, 0, 0, 0.7f);

            // Central panel for content
            var panel = new VisualElement();
            panel.style.alignItems = Align.Center;
            panel.style.justifyContent = Justify.Center;
            panel.style.paddingTop = 32;
            panel.style.paddingBottom = 32;
            panel.style.paddingLeft = 48;
            panel.style.paddingRight = 48;
            panel.style.backgroundColor = new Color(0.05f, 0.05f, 0.08f, 0.85f);
            panel.style.borderTopLeftRadius = 12;
            panel.style.borderTopRightRadius = 12;
            panel.style.borderBottomLeftRadius = 12;
            panel.style.borderBottomRightRadius = 12;
            panel.style.borderTopWidth = 1;
            panel.style.borderBottomWidth = 1;
            panel.style.borderLeftWidth = 1;
            panel.style.borderRightWidth = 1;
            panel.style.borderTopColor = new Color(0.9f, 0.1f, 0.1f, 0.4f);
            panel.style.borderBottomColor = new Color(0.9f, 0.1f, 0.1f, 0.4f);
            panel.style.borderLeftColor = new Color(0.9f, 0.1f, 0.1f, 0.4f);
            panel.style.borderRightColor = new Color(0.9f, 0.1f, 0.1f, 0.4f);

            var title = Lbl("YOU DIED", 64, new Color(0.9f, 0.1f, 0.1f), FontStyle.Bold);
            panel.Add(title);

            // Separator
            var sep = new VisualElement();
            sep.style.width = 200;
            sep.style.height = 2;
            sep.style.backgroundColor = new Color(0.9f, 0.1f, 0.1f, 0.3f);
            sep.style.marginTop = 8;
            sep.style.marginBottom = 12;
            panel.Add(sep);

            // Stats
            var statsContainer = new VisualElement();
            statsContainer.style.alignItems = Align.Center;
            statsContainer.style.marginTop = 8;

            if (floor > 0)
                statsContainer.Add(Lbl($"Floor {floor} — Room {room}", 22, new Color(0.7f, 0.7f, 0.75f)));
            if (kills > 0)
                statsContainer.Add(Lbl($"Enemies defeated: {kills}", 20, new Color(0.8f, 0.8f, 0.85f)));

            var momentum = playerCtrl != null ? playerCtrl.GetComponent<MomentumSystem>() : null;
            if (momentum != null && momentum.KillStreak > 0)
                statsContainer.Add(Lbl($"Best streak: {momentum.KillStreak}", 18, new Color(1f, 0.8f, 0.2f)));

            // Combos & Reactions discovered
            int comboDisc = Codex.DiscoveredComboCount;
            int comboTotal = Codex.TotalComboCount;
            int reactDisc = Codex.DiscoveredReactions.Count;
            if (comboTotal > 0)
            {
                var comboLbl = Lbl($"Combos discovered: {comboDisc}/{comboTotal}", 18, new Color(0.3f, 0.7f, 0.9f));
                comboLbl.style.marginTop = 4;
                statsContainer.Add(comboLbl);
            }
            if (reactDisc > 0)
            {
                var reactLbl = Lbl($"Reactions discovered: {reactDisc}", 18, new Color(1f, 0.7f, 0.2f));
                reactLbl.style.marginTop = 2;
                statsContainer.Add(reactLbl);
            }

            // Relics discovered
            if (relicCount > 0)
            {
                var relicLbl = Lbl($"Relics collected: {relicCount}", 18, new Color(0.6f, 0.9f, 0.6f));
                relicLbl.style.marginTop = 2;
                statsContainer.Add(relicLbl);
            }

            if (metaCurrency > 0)
            {
                var reward = Lbl($"+{metaCurrency} Soul Essence", 24, new Color(0.8f, 0.6f, 1f), FontStyle.Bold);
                reward.style.marginTop = 12;
                statsContainer.Add(reward);
            }

            panel.Add(statsContainer);

            // Motivational tip based on floor
            string tip = floor switch
            {
                0 or 1 => "Try switching elements for powerful reactions!",
                2 => "Combine spells to discover new combos!",
                _ => "Elemental reactions deal 2x damage!"
            };
            var tipLbl = Lbl(tip, 16, new Color(0.5f, 0.8f, 0.5f), FontStyle.Italic);
            tipLbl.style.marginTop = 16;
            tipLbl.style.unityTextAlign = UnityEngine.TextAnchor.MiddleCenter;
            panel.Add(tipLbl);

            var hint = Lbl("Press  R  to return to the Sanctum", 20, new Color(0.6f, 0.6f, 0.65f));
            hint.style.marginTop = 20;
            panel.Add(hint);

            deathOverlay.Add(panel);
        }
    }

    public void ShowVictory(int wave, int floor, int kills = 0,
        float runTime = 0f, int relicsCollected = 0, int combosDiscovered = 0,
        int damageDealt = 0, int damageTaken = 0, string favCombo = null, int essenceEarned = 0)
    {
        victoryOverlay.style.display = DisplayStyle.Flex;
        victoryOverlay.Clear();

        // Ensure dark overlay background
        victoryOverlay.style.backgroundColor = new Color(0, 0, 0, 0.8f);

        // Central panel
        var panel = new VisualElement();
        panel.style.alignItems = Align.Center;
        panel.style.justifyContent = Justify.Center;
        Pad(panel, 32, 48);
        panel.style.backgroundColor = new Color(0.05f, 0.05f, 0.08f, 0.85f);
        Radius(panel, 12);
        Border(panel, new Color(1f, 0.85f, 0.2f, 0.4f), 1);

        var title = Lbl("VICTORY!", 64, new Color(1f, 0.85f, 0.2f), FontStyle.Bold);
        panel.Add(title);

        var sub = Lbl("You have conquered the dungeon!", 24, new Color(0.8f, 0.8f, 0.85f));
        sub.style.marginTop = 8;
        panel.Add(sub);

        // Separator
        var sep = new VisualElement();
        sep.style.width = 280;
        sep.style.height = 2;
        sep.style.backgroundColor = new Color(1f, 0.85f, 0.2f, 0.3f);
        sep.style.marginTop = 12;
        sep.style.marginBottom = 16;
        panel.Add(sep);

        // Stats grid
        var stats = new VisualElement();
        stats.style.width = 320;

        AddStatRow(stats, "Time", FormatRunTime(runTime));
        AddStatRow(stats, "Floors Cleared", floor.ToString());
        AddStatRow(stats, "Enemies Slain", kills.ToString());
        if (relicsCollected > 0)
            AddStatRow(stats, "Relics Collected", relicsCollected.ToString());
        if (combosDiscovered > 0)
            AddStatRow(stats, "Combos Discovered", combosDiscovered.ToString());
        if (damageDealt > 0)
            AddStatRow(stats, "Damage Dealt", damageDealt.ToString());
        if (damageTaken > 0)
            AddStatRow(stats, "Damage Taken", damageTaken.ToString());
        if (!string.IsNullOrEmpty(favCombo))
            AddStatRow(stats, "Favourite Combo", favCombo);
        if (essenceEarned > 0)
            AddStatRow(stats, "Soul Essence Earned", $"+{essenceEarned}");

        panel.Add(stats);

        var hint = Lbl("Press  R  to return to the Sanctum", 20, new Color(0.6f, 0.6f, 0.65f));
        hint.style.marginTop = 24;
        panel.Add(hint);

        victoryOverlay.Add(panel);
    }

    void AddStatRow(VisualElement parent, string label, string value)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.justifyContent = Justify.SpaceBetween;
        row.style.marginBottom = 4;
        row.pickingMode = PickingMode.Ignore;

        var lbl = Lbl(label, 14, new Color(0.7f, 0.7f, 0.7f));
        row.Add(lbl);

        var val = Lbl(value, 14, Color.white, FontStyle.Bold);
        row.Add(val);

        parent.Add(row);
    }

    static string FormatRunTime(float seconds)
    {
        int m = (int)(seconds / 60);
        int s = (int)(seconds % 60);
        return $"{m}:{s:D2}";
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

    // ── NEW: Objective label ──
    Label objectiveLabel;

    public void SetObjective(string text)
    {
        if (objectiveLabel == null)
        {
            objectiveLabel = Lbl("", 22, new Color(1f, 0.9f, 0.3f), FontStyle.Bold);
            objectiveLabel.style.position = Position.Absolute;
            objectiveLabel.style.top = 60;
            objectiveLabel.style.left = new StyleLength(Length.Percent(50));
            objectiveLabel.style.translate = new StyleTranslate(new Translate(Length.Percent(-50), 0));
            objectiveLabel.pickingMode = PickingMode.Ignore;
            uiDoc.rootVisualElement.Add(objectiveLabel);
        }
        objectiveLabel.text = text;
        objectiveLabel.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
    }

    // ── NEW: Reaction popup labels ──

    public void SpawnReactionLabel(string name, Vector3 worldPos, Color color)
    {
        if (damageNumberLayer == null || Camera.main == null) return;

        var label = Lbl(name, 26, color, FontStyle.Bold);
        label.style.position = Position.Absolute;
        label.pickingMode = PickingMode.Ignore;
        label.style.textShadow = new TextShadow { offset = new Vector2(1, 1), blurRadius = 2, color = Color.black };

        Vector3 screen = Camera.main.WorldToScreenPoint(worldPos + Vector3.up * 1.5f);
        if (screen.z > 0)
        {
            label.style.left = screen.x - 40;
            label.style.top = Screen.height - screen.y;
        }
        damageNumberLayer.Add(label);

        activeDamageNumbers.Add(new DamageNumberState
        {
            label = label,
            worldPos = worldPos + Vector3.up * 1.5f,
            elapsed = 0,
            duration = 1.2f,
            velocity = new Vector3(0, 1.5f, 0)
        });
    }

    // ── NEW: Shop Room (full inventory) ──

    public void ShowShopRoomNew(ShopSystem.ShopItem[] items, int gold, Func<ShopSystem.ShopItem, int, bool> onBuy, Action onLeave)
    {
        var overlay = CreateOverlay();
        var panel = CreatePanel(overlay, "SHOP", new Color(1f, 0.85f, 0.2f));

        var goldRow = new VisualElement();
        goldRow.style.flexDirection = FlexDirection.Row;
        goldRow.style.justifyContent = Justify.Center;
        goldRow.style.marginBottom = 10;
        var goldLbl = Lbl($"Gold: {gold}", 20, new Color(1f, 0.85f, 0.2f), FontStyle.Bold);
        goldRow.Add(goldLbl);
        panel.Add(goldRow);

        var grid = new VisualElement();
        grid.style.flexDirection = FlexDirection.Row;
        grid.style.flexWrap = Wrap.Wrap;
        grid.style.justifyContent = Justify.Center;

        var relicMgr = FindAnyObjectByType<RelicManager>();

        for (int i = 0; i < items.Length; i++)
        {
            var item = items[i];
            int idx = i;
            var card = CreateCard(item.name, $"{item.description}\n\nPrice: {item.price}g", item.color, () =>
            {
                if (onBuy(item, idx))
                {
                    gold = GoldSystem.Instance != null ? GoldSystem.Instance.Gold : 0;
                    goldLbl.text = $"Gold: {gold}";
                }
            });
            card.style.width = 180;
            card.style.marginLeft = 6;
            card.style.marginRight = 6;

            // Show synergy hint for relic items
            if (relicMgr != null && item.relic != null)
            {
                string synHint = CheckSynergy(item.relic, relicMgr);
                if (!string.IsNullOrEmpty(synHint))
                {
                    var synLabel = Lbl(synHint, 10, new Color(0.3f, 0.9f, 0.3f), FontStyle.Bold);
                    synLabel.style.marginTop = 4;
                    synLabel.style.paddingLeft = 12;
                    synLabel.style.paddingRight = 12;
                    synLabel.style.whiteSpace = WhiteSpace.Normal;
                    card.Add(synLabel);
                }
            }

            grid.Add(card);
        }
        panel.Add(grid);

        var leaveBtn = CreateButton("LEAVE SHOP", new Color(0.5f, 0.5f, 0.5f), () =>
        {
            overlay.RemoveFromHierarchy();
            onLeave();
        });
        leaveBtn.style.marginTop = 16;
        panel.Add(leaveBtn);
    }

    // ── Shop Synergy Check ──

    string CheckSynergy(RelicSO newRelic, RelicManager mgr)
    {
        if (newRelic == null || mgr == null) return null;
        var type = newRelic.relicType;

        if (type == RelicType.GlassCannon && mgr.HasRelic(RelicType.Berserker)) return "SYNERGY: Rage Glass";
        if (type == RelicType.Berserker && mgr.HasRelic(RelicType.GlassCannon)) return "SYNERGY: Rage Glass";
        if (type == RelicType.Thorns && mgr.HasRelic(RelicType.Shield)) return "SYNERGY: Retribution";
        if (type == RelicType.Shield && mgr.HasRelic(RelicType.Thorns)) return "SYNERGY: Retribution";
        if (type == RelicType.DashFire && mgr.HasRelic(RelicType.CursedSpeed)) return "SYNERGY: Inferno Dash";
        if (type == RelicType.CursedSpeed && mgr.HasRelic(RelicType.DashFire)) return "SYNERGY: Inferno Dash";
        if (type == RelicType.BloodPact && mgr.HasRelic(RelicType.Regeneration)) return "SYNERGY: Blood Renewal";
        if (type == RelicType.Regeneration && mgr.HasRelic(RelicType.BloodPact)) return "SYNERGY: Blood Renewal";
        if (type == RelicType.DoubleStrike && mgr.HasRelic(RelicType.Chaos)) return "SYNERGY: Chaotic Surge";
        if (type == RelicType.Chaos && mgr.HasRelic(RelicType.DoubleStrike)) return "SYNERGY: Chaotic Surge";
        if (type == RelicType.VampireAura && mgr.HasRelic(RelicType.Lucky)) return "SYNERGY: Fortune's Vitality";
        if (type == RelicType.Lucky && mgr.HasRelic(RelicType.VampireAura)) return "SYNERGY: Fortune's Vitality";

        return null;
    }

    // ── NEW: Boss Intro ──

    VisualElement bossIntroOverlay;

    public void ShowBossIntro(string bossName, string lore)
    {
        bossIntroOverlay = new VisualElement();
        bossIntroOverlay.style.position = Position.Absolute;
        bossIntroOverlay.style.top = 0; bossIntroOverlay.style.left = 0;
        bossIntroOverlay.style.right = 0; bossIntroOverlay.style.bottom = 0;
        bossIntroOverlay.style.backgroundColor = new Color(0, 0, 0, 0.7f);
        bossIntroOverlay.style.justifyContent = Justify.Center;
        bossIntroOverlay.style.alignItems = Align.Center;
        bossIntroOverlay.pickingMode = PickingMode.Ignore;

        var nameLabel = Lbl(bossName, 48, new Color(0.9f, 0.15f, 0.15f), FontStyle.Bold);
        nameLabel.style.letterSpacing = 6;
        bossIntroOverlay.Add(nameLabel);

        var loreLabel = Lbl(lore, 18, new Color(0.7f, 0.7f, 0.75f), FontStyle.Italic);
        loreLabel.style.marginTop = 12;
        bossIntroOverlay.Add(loreLabel);

        uiDoc.rootVisualElement.Add(bossIntroOverlay);
    }

    public void HideBossIntro()
    {
        if (bossIntroOverlay != null)
        {
            bossIntroOverlay.RemoveFromHierarchy();
            bossIntroOverlay = null;
        }
    }

    // ── NEW: Boss Victory Splash ──

    public void ShowBossVictorySplash(string bossName, int hpRemaining, int maxHP)
    {
        var splash = new VisualElement();
        splash.style.position = Position.Absolute;
        splash.style.top = 0; splash.style.left = 0;
        splash.style.right = 0; splash.style.bottom = 0;
        splash.style.backgroundColor = new Color(0, 0, 0, 0.5f);
        splash.style.justifyContent = Justify.Center;
        splash.style.alignItems = Align.Center;
        splash.pickingMode = PickingMode.Ignore;

        var defeatLabel = Lbl($"{bossName} DEFEATED", 36, new Color(1f, 0.85f, 0.2f), FontStyle.Bold);
        splash.Add(defeatLabel);

        var statsLabel = Lbl($"HP Remaining: {hpRemaining}/{maxHP}", 18, new Color(0.7f, 0.9f, 0.7f));
        statsLabel.style.marginTop = 8;
        splash.Add(statsLabel);

        uiDoc.rootVisualElement.Add(splash);

        // Auto-hide after 2 seconds
        StartCoroutine(HideAfterDelay(splash, 2f));
    }

    System.Collections.IEnumerator HideAfterDelay(VisualElement element, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (element != null) element.RemoveFromHierarchy();
    }

    // ── NEW: Codex overlay ──

    public void ShowCodex()
    {
        var overlay = CreateOverlay();
        var panel = CreatePanel(overlay, "CODEX", new Color(0.3f, 0.7f, 0.9f));
        panel.style.maxHeight = 500;
        panel.style.overflow = Overflow.Hidden;

        var scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.style.flexGrow = 1;

        // Combos section
        var comboHeader = Lbl($"COMBOS ({Codex.DiscoveredComboCount}/{Codex.TotalComboCount})", 18, new Color(0.3f, 0.7f, 0.9f), FontStyle.Bold);
        comboHeader.style.marginBottom = 8;
        scroll.Add(comboHeader);

        if (ComboSpellRegistry.AllCombos != null)
        {
            foreach (var combo in ComboSpellRegistry.AllCombos)
            {
                bool discovered = Codex.IsComboDiscovered(combo.Value.comboName);
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.marginBottom = 4;
                row.style.paddingLeft = 8;

                string name = discovered ? combo.Value.comboName : "???";
                Color col = discovered ? new Color(0.9f, 0.9f, 0.95f) : new Color(0.4f, 0.4f, 0.45f);
                var lbl = Lbl(name, 14, col);
                row.Add(lbl);
                scroll.Add(row);
            }
        }

        // Reactions section
        var reactHeader = Lbl($"REACTIONS ({Codex.DiscoveredReactions.Count})", 18, new Color(1f, 0.7f, 0.2f), FontStyle.Bold);
        reactHeader.style.marginTop = 12;
        reactHeader.style.marginBottom = 8;
        scroll.Add(reactHeader);

        foreach (var r in Codex.DiscoveredReactions)
        {
            var lbl = Lbl(r, 14, new Color(1f, 0.85f, 0.3f));
            lbl.style.paddingLeft = 8;
            lbl.style.marginBottom = 2;
            scroll.Add(lbl);
        }

        panel.Add(scroll);

        var closeBtn = CreateButton("CLOSE", new Color(0.5f, 0.5f, 0.5f), () => overlay.RemoveFromHierarchy());
        closeBtn.style.marginTop = 12;
        panel.Add(closeBtn);
    }

    // Helper: create overlay
    VisualElement CreateOverlay()
    {
        var overlay = new VisualElement();
        overlay.style.position = Position.Absolute;
        overlay.style.top = 0; overlay.style.left = 0;
        overlay.style.right = 0; overlay.style.bottom = 0;
        overlay.style.backgroundColor = new Color(0, 0, 0, 0.75f);
        overlay.style.justifyContent = Justify.Center;
        overlay.style.alignItems = Align.Center;
        uiDoc.rootVisualElement.Add(overlay);
        return overlay;
    }

    // Helper: create panel inside overlay
    VisualElement CreatePanel(VisualElement overlay, string title, Color titleColor)
    {
        var panel = new VisualElement();
        panel.style.backgroundColor = CardBg;
        panel.style.paddingTop = 20; panel.style.paddingBottom = 20;
        panel.style.paddingLeft = 24; panel.style.paddingRight = 24;
        Radius(panel, 12);
        panel.style.alignItems = Align.Center;
        panel.style.minWidth = 400;

        var titleLabel = Lbl(title, 28, titleColor, FontStyle.Bold);
        titleLabel.style.marginBottom = 12;
        panel.Add(titleLabel);

        overlay.Add(panel);
        return panel;
    }

    // Helper: create styled card
    VisualElement CreateCard(string title, string desc, Color color, Action onClick)
    {
        var card = new VisualElement();
        card.style.backgroundColor = CardBg;
        card.style.borderTopColor = card.style.borderBottomColor =
            card.style.borderLeftColor = card.style.borderRightColor = color;
        card.style.borderTopWidth = card.style.borderBottomWidth =
            card.style.borderLeftWidth = card.style.borderRightWidth = 2;
        Radius(card, 8);
        card.style.paddingTop = 10; card.style.paddingBottom = 10;
        card.style.paddingLeft = 12; card.style.paddingRight = 12;
        card.style.marginBottom = 8;

        var titleLbl = Lbl(title, 16, color, FontStyle.Bold);
        card.Add(titleLbl);

        var descLbl = Lbl(desc, 12, Dim);
        descLbl.style.marginTop = 4;
        descLbl.style.whiteSpace = WhiteSpace.Normal;
        card.Add(descLbl);

        card.RegisterCallback<ClickEvent>(_ => onClick());
        card.RegisterCallback<MouseEnterEvent>(_ => card.style.backgroundColor = CardBgHover);
        card.RegisterCallback<MouseLeaveEvent>(_ => card.style.backgroundColor = CardBg);

        return card;
    }

    // Helper: create styled button
    VisualElement CreateButton(string text, Color color, Action onClick)
    {
        var btn = new VisualElement();
        btn.style.backgroundColor = new Color(color.r * 0.3f, color.g * 0.3f, color.b * 0.3f, 0.9f);
        btn.style.borderTopColor = btn.style.borderBottomColor =
            btn.style.borderLeftColor = btn.style.borderRightColor = color;
        btn.style.borderTopWidth = btn.style.borderBottomWidth =
            btn.style.borderLeftWidth = btn.style.borderRightWidth = 2;
        Radius(btn, 6);
        btn.style.paddingTop = 8; btn.style.paddingBottom = 8;
        btn.style.paddingLeft = 20; btn.style.paddingRight = 20;
        btn.style.alignItems = Align.Center;

        var lbl = Lbl(text, 16, color, FontStyle.Bold);
        btn.Add(lbl);

        btn.RegisterCallback<ClickEvent>(_ => onClick());
        btn.RegisterCallback<MouseEnterEvent>(_ => btn.style.backgroundColor = CardBgHover);
        btn.RegisterCallback<MouseLeaveEvent>(_ => btn.style.backgroundColor = new Color(color.r * 0.3f, color.g * 0.3f, color.b * 0.3f, 0.9f));

        return btn;
    }

    // ── Update (damage numbers, charge bar, cooldowns) ──

    // ── Pause Menu ──

    void CheckPauseInput()
    {
        if (UnityEngine.InputSystem.Keyboard.current != null &&
            UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (isPaused)
                ResumeGame();
            else
                PauseGame();
        }
    }

    void PauseGame()
    {
        if (deathOverlay.style.display == DisplayStyle.Flex) return;
        if (victoryOverlay.style.display == DisplayStyle.Flex) return;
        isPaused = true;
        Time.timeScale = 0;
        pauseOverlay.style.display = DisplayStyle.Flex;
    }

    void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1;
        pauseOverlay.style.display = DisplayStyle.None;
    }

    // ── Tutorial Hints ──

    public void ShowHint(string text, float duration)
    {
        hintLabel.text = text;
        hintBar.style.display = DisplayStyle.Flex;
        hintBar.style.opacity = 1f;
        hintTimer = 0f;
        hintDuration = duration;
    }

    void UpdateHint()
    {
        if (hintBar.style.display == DisplayStyle.None) return;
        hintTimer += Time.unscaledDeltaTime;
        if (hintTimer >= hintDuration)
        {
            hintBar.style.display = DisplayStyle.None;
        }
        else if (hintTimer >= hintDuration - 1f)
        {
            // Fade out in the last second
            float fade = hintDuration - hintTimer;
            hintBar.style.opacity = fade;
        }
    }

    void Update()
    {
        CheckPauseInput();
        UpdateHint();

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

        // Keyboard combo preview (digit key held → show preview)
        if (caster != null)
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                int keySlot = -1;
                if (kb.digit1Key.isPressed) keySlot = 0;
                else if (kb.digit2Key.isPressed) keySlot = 1;
                else if (kb.digit3Key.isPressed) keySlot = 2;
                else if (kb.digit4Key.isPressed) keySlot = 3;

                // Only override if no mouse hover is active
                if (keySlot >= 0 && hoveredSlot < 0)
                {
                    hoveredSlot = keySlot;
                    RefreshComboPreview();
                }
                else if (keySlot < 0 && hoveredSlot >= 0)
                {
                    // Key released and no mouse hover — check if mouse is still on a slot
                    // If not, hide preview
                    hoveredSlot = -1;
                    RefreshComboPreview();
                }
            }
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

        // Gold gain popup fade
        if (goldGainTimer > 0)
        {
            goldGainTimer -= Time.deltaTime;
            goldGainLabel.style.opacity = Mathf.Clamp01(goldGainTimer / 0.5f);
            if (goldGainTimer <= 0)
                goldGainLabel.style.display = DisplayStyle.None;
        }

        // Variety bonus display
        if (caster != null && varietyBonusLabel != null)
        {
            float combo = caster.comboMultiplier;
            if (combo > 1.01f)
            {
                varietyBonusLabel.text = combo >= 1.25f
                    ? $"VARIETY x{combo:F2} — Excellent!"
                    : $"VARIETY x{combo:F2} — Good";
                varietyBonusLabel.style.color = combo >= 1.25f
                    ? new Color(0.4f, 1f, 0.6f)
                    : new Color(0.6f, 0.9f, 1f);
                varietyBonusLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                varietyBonusLabel.style.display = DisplayStyle.None;
            }
        }
    }

    // ── Encounter Objective ──

    /// <summary>Update encounter objective display. Call from room controller each frame.</summary>
    public void SetEncounterObjective(string text, Color color)
    {
        if (encounterObjectiveLabel == null) return;
        if (string.IsNullOrEmpty(text))
        {
            encounterObjectiveLabel.style.display = DisplayStyle.None;
            return;
        }
        encounterObjectiveLabel.text = text;
        encounterObjectiveLabel.style.color = color;
        encounterObjectiveLabel.style.display = DisplayStyle.Flex;
    }

    /// <summary>Show gold gain popup (+N).</summary>
    public void ShowGoldGain(int amount)
    {
        if (goldGainLabel == null) return;
        goldGainLabel.text = $"+{amount}";
        goldGainLabel.style.display = DisplayStyle.Flex;
        goldGainLabel.style.opacity = 1f;
        goldGainTimer = 1.5f;
    }

    /// <summary>Update the death overlay with actual progress info.</summary>
    public void ShowDeath(int floor, int room, int metaCurrencyEarned)
    {
        deathOverlay.style.display = DisplayStyle.Flex;
        if (deathWaveLabel != null)
            deathWaveLabel.text = $"Reached Floor {floor}, Room {room}  |  +{metaCurrencyEarned} Rune Essence";
    }

    /// <summary>Refresh synergy bar to show active synergies.</summary>
    public void RefreshSynergies(System.Collections.Generic.List<SynergyType> synergies)
    {
        if (synergyBar == null) return;
        synergyBar.Clear();

        if (synergies == null || synergies.Count == 0) return;

        var lbl = Lbl("Synergies: ", 12, Dim);
        synergyBar.Add(lbl);

        foreach (var syn in synergies)
        {
            foreach (var def in SynergySystem.AllSynergies)
            {
                if (def.type == syn)
                {
                    var pip = Pill(def.name, def.color, Color.white);
                    synergyBar.Add(pip);
                    break;
                }
            }
        }
    }

    /// <summary>Update boss HP bar with phase-2 marker.</summary>
    public void SetBossHP(string name, float hpPercent, bool phase2 = false)
    {
        if (bossHPContainer == null) return;
        bossHPContainer.style.display = DisplayStyle.Flex;
        bossNameLabel.text = phase2 ? $"{name} — PHASE 2" : name;
        bossNameLabel.style.color = phase2 ? new Color(0.8f, 0.2f, 1f) : new Color(1f, 0.3f, 0.3f);
        bossHPFill.style.width = new StyleLength(Length.Percent(Mathf.Clamp01(hpPercent) * 100));
        bossHPFill.style.backgroundColor = phase2
            ? new Color(0.6f, 0.15f, 0.8f)
            : new Color(0.85f, 0.15f, 0.15f);
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
