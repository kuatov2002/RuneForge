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
    VisualElement spell1Card, spell2Card;
    VisualElement spell1CDFill, spell2CDFill;
    VisualElement dashCDContainer;
    VisualElement dashCDFill;
    VisualElement runeOverlay;
    VisualElement runePanel;
    VisualElement currentSpellPreview;
    Label slotHintLabel;
    VisualElement deathOverlay;
    Label deathWaveLabel;
    VisualElement victoryOverlay;

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
    static readonly Color FormColor = new(0.4f, 0.55f, 0.75f);
    static readonly Color ModColor = new(0.25f, 0.7f, 0.6f);
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
        ps.themeStyleSheet = UnityEngine.UIElements.ThemeStyleSheet.CreateInstance<ThemeStyleSheet>();
        uiDoc.panelSettings = ps;

        BuildUI();

        // Set a system font on root so all labels render text
        var font = Font.CreateDynamicFontFromOSFont("Arial", 14);
        uiDoc.rootVisualElement.style.unityFontDefinition = FontDefinition.FromFont(font);

        playerHealth.OnHPChanged += (_, _) => RefreshHP();
        playerHealth.OnDamaged += OnPlayerOrEnemyDamaged;
        caster.OnSpellChanged += RefreshSpells;
        RefreshHP();
        RefreshSpells();
    }

    /// <summary>Register an enemy's Health for floating damage numbers.</summary>
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

        topBar.Add(rightCol);
        root.Add(topBar);

        // ── Relic bar (under top bar, left side) ──
        // ── Gold display (under HP) ──
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

        // ── Rune selection overlay ──
        runeOverlay = MakeOverlay(new Color(0, 0, 0, 0.65f));

        var runeContainer = new VisualElement();
        runeContainer.style.alignItems = Align.Center;

        var runeTitle = Lbl("CHOOSE A RUNE", 36, Color.white, FontStyle.Bold);
        runeTitle.style.marginBottom = 12;
        runeContainer.Add(runeTitle);

        currentSpellPreview = new VisualElement();
        currentSpellPreview.style.flexDirection = FlexDirection.Row;
        currentSpellPreview.style.alignItems = Align.Center;
        currentSpellPreview.style.marginBottom = 8;
        runeContainer.Add(currentSpellPreview);

        slotHintLabel = Lbl("Press Q to switch target slot", 16, Dim);
        slotHintLabel.style.marginBottom = 24;
        runeContainer.Add(slotHintLabel);

        runePanel = new VisualElement();
        runePanel.style.flexDirection = FlexDirection.Row;
        runePanel.style.justifyContent = Justify.Center;
        runeContainer.Add(runePanel);

        runeOverlay.Add(runeContainer);
        root.Add(runeOverlay);

        // ── Bottom bar ──
        var bottomArea = new VisualElement();
        bottomArea.pickingMode = PickingMode.Ignore;
        bottomArea.style.position = Position.Absolute;
        bottomArea.style.bottom = 0;
        bottomArea.style.left = 0;
        bottomArea.style.right = 0;
        bottomArea.style.alignItems = Align.Center;
        bottomArea.style.paddingBottom = 16;

        var slotRow = new VisualElement();
        slotRow.pickingMode = PickingMode.Ignore;
        slotRow.style.flexDirection = FlexDirection.Row;
        slotRow.style.alignItems = Align.FlexEnd;

        spell1Card = BuildSpellCard(0);
        spell2Card = BuildSpellCard(1);

        var qHint = Lbl("[Q]", 18, Dim, FontStyle.Bold);
        qHint.style.marginLeft = 16;
        qHint.style.marginRight = 16;
        qHint.style.marginBottom = 18;

        slotRow.Add(spell1Card);
        slotRow.Add(qHint);
        slotRow.Add(spell2Card);
        bottomArea.Add(slotRow);

        var controls = Lbl("WASD Move   LMB Cast   RMB Dash   Q Swap   F Potion", 14, new Color(0.35f, 0.35f, 0.4f));
        controls.style.marginTop = 8;
        bottomArea.Add(controls);

        // ── Dash cooldown indicator ──
        dashCDContainer = new VisualElement();
        dashCDContainer.pickingMode = PickingMode.Ignore;
        dashCDContainer.style.width = 80;
        dashCDContainer.style.height = 8;
        dashCDContainer.style.marginTop = 6;
        dashCDContainer.style.backgroundColor = new Color(0.15f, 0.15f, 0.2f);
        Radius(dashCDContainer, 4);

        dashCDFill = new VisualElement();
        dashCDFill.pickingMode = PickingMode.Ignore;
        dashCDFill.style.height = new StyleLength(Length.Percent(100));
        dashCDFill.style.width = new StyleLength(Length.Percent(100));
        dashCDFill.style.backgroundColor = new Color(0.3f, 0.7f, 1f);
        Radius(dashCDFill, 4);
        dashCDContainer.Add(dashCDFill);

        var dashLabel = Lbl("DASH", 11, Dim, FontStyle.Bold);
        dashLabel.style.alignSelf = Align.Center;
        bottomArea.Add(dashCDContainer);

        // Potion indicator
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

        // ── Damage number layer (fullscreen, ignores input) ──
        damageNumberLayer = new VisualElement();
        damageNumberLayer.pickingMode = PickingMode.Ignore;
        damageNumberLayer.style.position = Position.Absolute;
        damageNumberLayer.style.top = 0;
        damageNumberLayer.style.bottom = 0;
        damageNumberLayer.style.left = 0;
        damageNumberLayer.style.right = 0;
        root.Add(damageNumberLayer);
    }

    // ─── SPELL CARDS (bottom) ─────────────────────────────────────

    VisualElement BuildSpellCard(int index)
    {
        var card = new VisualElement();
        card.name = $"spell-card-{index}";
        card.pickingMode = PickingMode.Ignore;
        card.style.backgroundColor = CardBg;
        Pad(card, 12, 16);
        Radius(card, 12);
        Border(card, InactiveBorder, 2);
        card.style.minWidth = 260;
        card.style.overflow = Overflow.Hidden;

        var header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.alignItems = Align.Center;
        header.style.marginBottom = 8;

        var slotLbl = Lbl($"Slot {index + 1}", 14, Dim, FontStyle.Bold);
        header.Add(slotLbl);

        var activeTag = Lbl("ACTIVE", 11, ActiveBorder, FontStyle.Bold);
        activeTag.name = "active-tag";
        activeTag.style.marginLeft = 10;
        activeTag.style.backgroundColor = new Color(1f, 0.85f, 0.2f, 0.15f);
        Pad(activeTag, 2, 8);
        Radius(activeTag, 4);
        header.Add(activeTag);

        // Cooldown label
        var cdLabel = Lbl("READY", 11, new Color(0.5f, 1f, 0.5f), FontStyle.Bold);
        cdLabel.name = "cd-label";
        cdLabel.style.marginLeft = 10;
        header.Add(cdLabel);

        card.Add(header);

        var pills = new VisualElement();
        pills.name = "pills";
        pills.style.flexDirection = FlexDirection.Row;
        pills.style.flexWrap = Wrap.Wrap;
        card.Add(pills);

        // Cooldown overlay fill (covers card from bottom)
        var cdFill = new VisualElement();
        cdFill.name = "cd-fill";
        cdFill.pickingMode = PickingMode.Ignore;
        cdFill.style.position = Position.Absolute;
        cdFill.style.bottom = 0;
        cdFill.style.left = 0;
        cdFill.style.right = 0;
        cdFill.style.height = new StyleLength(Length.Percent(0));
        cdFill.style.backgroundColor = new Color(0f, 0f, 0f, 0.5f);
        card.Add(cdFill);

        if (index == 0) spell1CDFill = cdFill;
        else spell2CDFill = cdFill;

        return card;
    }

    void RefreshSpellCard(int index, VisualElement card)
    {
        var spell = caster.spellSlots[index];
        bool active = caster.activeSlot == index;

        Border(card, active ? ActiveBorder : InactiveBorder, active ? 3 : 2);
        card.Q("active-tag").style.display = active ? DisplayStyle.Flex : DisplayStyle.None;

        var pills = card.Q("pills");
        pills.Clear();

        if (spell == null || spell.element == null)
        {
            pills.Add(Pill("Empty", Color.gray, Color.gray));
            return;
        }

        pills.Add(Pill(spell.element.elementName, spell.element.color, Color.white));
        pills.Add(Pill(spell.form != null ? spell.form.formName : "???", FormColor, Color.white));
        if (spell.modifier != null && spell.modifier.modifierType != ModifierType.None)
            pills.Add(Pill(spell.modifier.modifierName, ModColor, Color.white));
    }

    // ─── HP ───────────────────────────────────────────────────────

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

    // ─── RUNE SELECTION ───────────────────────────────────────────

    int rerollsRemaining;
    Action rerollCallback;
    Action<int> currentRuneOnSelect;
    ScriptableObject[] currentRuneOptions;

    public void ShowRuneSelection(ScriptableObject[] options, int rerolls, Action<int> onSelect, Action onReroll)
    {
        runeOverlay.style.display = DisplayStyle.Flex;
        rerollsRemaining = rerolls;
        rerollCallback = onReroll;
        currentRuneOnSelect = onSelect;
        currentRuneOptions = options;
        RebuildRuneCards();
    }

    void RebuildRuneCards()
    {
        runePanel.Clear();
        RefreshCurrentSpellPreview();

        for (int i = 0; i < currentRuneOptions.Length; i++)
        {
            int idx = i;
            var card = BuildRuneCard(currentRuneOptions[i], () =>
            {
                runeOverlay.style.display = DisplayStyle.None;
                currentRuneOnSelect?.Invoke(idx);
            });
            runePanel.Add(card);
        }

        // Reroll button
        if (rerollsRemaining > 0 && rerollCallback != null)
        {
            var rerollCard = new VisualElement();
            rerollCard.style.flexDirection = FlexDirection.Column;
            rerollCard.style.width = 120;
            rerollCard.style.marginLeft = 8;
            rerollCard.style.backgroundColor = CardBg;
            Radius(rerollCard, 14);
            Border(rerollCard, new Color(0.6f, 0.4f, 0.9f), 2);
            rerollCard.style.alignItems = Align.Center;
            rerollCard.style.justifyContent = Justify.Center;
            Pad(rerollCard, 20, 16);

            var rerollIcon = Lbl("\u21BB", 28, new Color(0.7f, 0.5f, 1f), FontStyle.Bold);
            rerollCard.Add(rerollIcon);
            var rerollLbl = Lbl("REROLL", 16, new Color(0.7f, 0.5f, 1f), FontStyle.Bold);
            rerollCard.Add(rerollLbl);
            var rerollCount = Lbl($"x{rerollsRemaining}", 14, Dim, FontStyle.Bold);
            rerollCount.style.marginTop = 4;
            rerollCard.Add(rerollCount);

            rerollCard.RegisterCallback<ClickEvent>(_ =>
            {
                rerollsRemaining--;
                rerollCallback?.Invoke();
                RebuildRuneCards();
            });
            rerollCard.RegisterCallback<MouseEnterEvent>(_ =>
            {
                rerollCard.style.backgroundColor = CardBgHover;
                Border(rerollCard, new Color(0.8f, 0.6f, 1f), 3);
            });
            rerollCard.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                rerollCard.style.backgroundColor = CardBg;
                Border(rerollCard, new Color(0.6f, 0.4f, 0.9f), 2);
            });
            runePanel.Add(rerollCard);
        }
    }

    void RefreshCurrentSpellPreview()
    {
        currentSpellPreview.Clear();
        var spell = caster.spellSlots[caster.activeSlot];

        currentSpellPreview.Add(Lbl($"Slot {caster.activeSlot + 1}: ", 18, Dim, FontStyle.Bold));

        if (spell != null && spell.element != null)
        {
            currentSpellPreview.Add(Pill(spell.element.elementName, spell.element.color, Color.white));
            if (spell.form != null) currentSpellPreview.Add(Pill(spell.form.formName, FormColor, Color.white));
            if (spell.modifier != null && spell.modifier.modifierType != ModifierType.None)
                currentSpellPreview.Add(Pill(spell.modifier.modifierName, ModColor, Color.white));
        }
        else
        {
            currentSpellPreview.Add(Pill("Empty", Color.gray, Color.gray));
        }
    }

    VisualElement BuildRuneCard(ScriptableObject rune, Action onClick)
    {
        GetRuneInfo(rune, out string runeName, out string typeName, out Color runeColor, out string description);
        string preview = ComputePreview(rune);

        // Check modifier compatibility
        bool incompatible = false;
        if (rune is ModifierSO modSO && modSO.modifierType != ModifierType.None)
        {
            var spell = caster.spellSlots[caster.activeSlot];
            if (spell?.form != null && !ModifierSO.IsCompatible(modSO.modifierType, spell.form.formType))
                incompatible = true;
        }

        var card = new VisualElement();
        card.style.flexDirection = FlexDirection.Column;
        card.style.width = 250;
        card.style.marginLeft = 10;
        card.style.marginRight = 10;
        card.style.backgroundColor = CardBg;
        Radius(card, 14);
        Border(card, incompatible ? new Color(0.4f, 0.4f, 0.4f) : InactiveBorder, 2);
        card.style.overflow = Overflow.Hidden;
        card.style.cursor = StyleKeyword.Auto;
        if (incompatible) card.style.opacity = 0.45f;

        // Click handler (still allow click — rune replaces the slot even if incompatible)
        card.RegisterCallback<ClickEvent>(_ => onClick?.Invoke());

        // ── Header (colored bar) ──
        var header = new VisualElement();
        header.style.backgroundColor = runeColor;
        Pad(header, 14, 18);

        header.Add(Lbl(runeName, 26, Color.white, FontStyle.Bold));
        var typeLbl = Lbl(typeName, 14, new Color(1, 1, 1, 0.75f), FontStyle.Bold);
        typeLbl.style.marginTop = 2;
        header.Add(typeLbl);
        card.Add(header);

        // ── Body ──
        var body = new VisualElement();
        Pad(body, 14, 18);
        body.style.flexGrow = 1;

        var descLbl = Lbl(description, 16, new Color(0.8f, 0.8f, 0.85f));
        descLbl.style.whiteSpace = WhiteSpace.Normal;
        descLbl.style.marginBottom = 14;
        body.Add(descLbl);

        // Separator
        var sep = new VisualElement();
        sep.style.height = 1;
        sep.style.backgroundColor = new Color(0.3f, 0.3f, 0.35f);
        sep.style.marginBottom = 10;
        body.Add(sep);

        if (incompatible)
        {
            var warn = Lbl("INCOMPATIBLE WITH CURRENT FORM", 12, new Color(1f, 0.4f, 0.3f), FontStyle.Bold);
            warn.style.whiteSpace = WhiteSpace.Normal;
            warn.style.marginBottom = 6;
            body.Add(warn);
        }

        body.Add(Lbl("RESULT", 12, Dim, FontStyle.Bold));
        var previewLbl = Lbl(preview, 18, Color.white, FontStyle.Bold);
        previewLbl.style.whiteSpace = WhiteSpace.Normal;
        previewLbl.style.marginTop = 4;
        body.Add(previewLbl);

        card.Add(body);

        // Hover
        card.RegisterCallback<MouseEnterEvent>(_ =>
        {
            card.style.backgroundColor = CardBgHover;
            Border(card, runeColor, 3);
        });
        card.RegisterCallback<MouseLeaveEvent>(_ =>
        {
            card.style.backgroundColor = CardBg;
            Border(card, InactiveBorder, 2);
        });

        return card;
    }

    // ─── RUNE INFO ────────────────────────────────────────────────

    void GetRuneInfo(ScriptableObject rune, out string name, out string type, out Color color, out string desc)
    {
        name = "?"; type = "?"; color = Color.gray; desc = "";

        if (rune is ElementSO e)
        {
            name = e.elementName; type = "ELEMENT"; color = e.color;
            desc = e.statusEffect switch
            {
                StatusEffectType.Burn   => $"Base damage: {e.baseDamage}\nBurn: {e.statusDPS} DMG/s for {e.statusDuration}s",
                StatusEffectType.Slow   => $"Base damage: {e.baseDamage}\nSlow 40% for {e.statusDuration}s\nRepeat hit = Freeze",
                StatusEffectType.Chain  => $"Base damage: {e.baseDamage}\nChains to {e.chainCount} nearby enemies\nStun {e.statusDuration}s",
                StatusEffectType.Poison => $"Base damage: {e.baseDamage}\nPoison stacks up to 5\n2 DMG/s per stack",
                StatusEffectType.VoidMark => $"Base damage: {e.baseDamage}\nOn kill: pulls nearby enemies",
                _ => $"Base damage: {e.baseDamage}"
            };
        }
        else if (rune is FormSO f)
        {
            name = f.formName; type = "FORM"; color = FormColor;
            desc = f.formType switch
            {
                FormType.Bolt  => $"Aimed projectile\nMedium speed, long range\nCD: {f.cooldown}s",
                FormType.Cone  => $"45 deg frontal arc\nClose range AoE\nCD: {f.cooldown}s",
                FormType.Beam  => $"Instant hitscan line\nHits all enemies in path\nCD: {f.cooldown}s",
                FormType.Aura  => $"360 deg pulse around you\nPassive, auto-casts\nCD: {f.cooldown}s",
                FormType.Orbit => $"Orbiting projectiles\nNo aiming needed\nCD: {f.cooldown}s",
                FormType.Trap  => $"Mine at cursor position\nTriggers on proximity\nCD: {f.cooldown}s",
                _ => ""
            };
        }
        else if (rune is ModifierSO m)
        {
            name = m.modifierName; type = "MODIFIER"; color = ModColor;
            desc = m.modifierType switch
            {
                ModifierType.Split => $"x{m.splitCount} copies\n-{(int)((1 - m.damageMultiplier) * 100)}% damage each\n{m.splitSpreadAngle} deg spread",
                ModifierType.Pierce => $"Passes through {m.pierceCount} enemies\nFull damage to each",
                ModifierType.Bounce => $"Bounces {m.bounceCount} times\nOff walls or between enemies",
                ModifierType.Leech => $"{(int)(m.leechPercent * 100)}% damage heals you\nSustain in combat",
                ModifierType.Oversize => $"x{m.sizeMultiplier} size\n-{(int)((1 - m.speedPenalty) * 100)}% projectile speed",
                ModifierType.Volatile => $"x{m.damageMultiplier} damage\n{(int)(m.volatileMissChance * 100)}% chance to miss",
                ModifierType.Homing => $"Seeks nearest enemy\n-{(int)((1 - m.homingSpeedMult) * 100)}% projectile speed",
                _ => "No modification\nPure base spell"
            };
        }
    }

    string ComputePreview(ScriptableObject rune)
    {
        var spell = caster.spellSlots[caster.activeSlot];
        string elem = spell?.element?.elementName ?? "???";
        string form = spell?.form?.formName ?? "???";
        string mod = "";
        if (spell?.modifier != null && spell.modifier.modifierType != ModifierType.None)
            mod = " + " + spell.modifier.modifierName;

        if (rune is ElementSO e) elem = e.elementName;
        else if (rune is FormSO f) form = f.formName;
        else if (rune is ModifierSO m)
            mod = m.modifierType != ModifierType.None ? " + " + m.modifierName : "";

        return $"{elem}  {form}{mod}";
    }

    // ─── RELIC SELECTION ─────────────────────────────────────────

    public void ShowRelicSelection(RelicSO[] options, Action<int> onSelect)
    {
        runeOverlay.style.display = DisplayStyle.Flex;
        runePanel.Clear();
        currentSpellPreview.Clear();
        currentSpellPreview.Add(Lbl("Choose a Relic", 18, new Color(1f, 0.85f, 0.3f), FontStyle.Bold));
        slotHintLabel.text = "Relics give passive bonuses for the rest of the run";

        for (int i = 0; i < options.Length; i++)
        {
            int idx = i;
            var relic = options[i];
            var card = new VisualElement();
            card.style.flexDirection = FlexDirection.Column;
            card.style.width = 250;
            card.style.marginLeft = 10;
            card.style.marginRight = 10;
            card.style.backgroundColor = CardBg;
            Radius(card, 14);
            Border(card, InactiveBorder, 2);
            card.style.overflow = Overflow.Hidden;

            card.RegisterCallback<ClickEvent>(_ =>
            {
                runeOverlay.style.display = DisplayStyle.None;
                onSelect?.Invoke(idx);
            });

            // Header
            var header = new VisualElement();
            header.style.backgroundColor = relic.color;
            Pad(header, 14, 18);
            header.Add(Lbl(relic.relicName, 26, Color.white, FontStyle.Bold));
            var typeLbl = Lbl("RELIC", 14, new Color(1, 1, 1, 0.75f), FontStyle.Bold);
            typeLbl.style.marginTop = 2;
            header.Add(typeLbl);
            card.Add(header);

            // Body
            var body = new VisualElement();
            Pad(body, 14, 18);
            var descLbl = Lbl(relic.description, 18, new Color(0.85f, 0.85f, 0.9f));
            descLbl.style.whiteSpace = WhiteSpace.Normal;
            body.Add(descLbl);
            card.Add(body);

            // Hover
            card.RegisterCallback<MouseEnterEvent>(_ =>
            {
                card.style.backgroundColor = CardBgHover;
                Border(card, relic.color, 3);
            });
            card.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                card.style.backgroundColor = CardBg;
                Border(card, InactiveBorder, 2);
            });

            runePanel.Add(card);
        }
    }

    public void ShowShopRoom(RelicSO[] allRelics, RelicManager mgr, int price, int playerGold, Action<RelicSO> onDone)
    {
        runeOverlay.style.display = DisplayStyle.Flex;
        runePanel.Clear();
        currentSpellPreview.Clear();
        currentSpellPreview.Add(Lbl("SHOP", 24, new Color(1f, 0.85f, 0.2f), FontStyle.Bold));
        slotHintLabel.text = $"Your gold: {playerGold}";

        // Gather available relics
        var available = new List<RelicSO>();
        foreach (var r in allRelics)
            if (!mgr.HasRelic(r.relicType)) available.Add(r);

        int count = Mathf.Min(3, available.Count);
        var options = new RelicSO[count];
        for (int i = 0; i < count; i++)
        {
            int idx = UnityEngine.Random.Range(0, available.Count);
            options[i] = available[idx];
            available.RemoveAt(idx);
        }

        for (int i = 0; i < count; i++)
        {
            var relic = options[i];
            bool canAfford = playerGold >= price;
            var card = new VisualElement();
            card.style.flexDirection = FlexDirection.Column;
            card.style.width = 220;
            card.style.marginLeft = 8;
            card.style.marginRight = 8;
            card.style.backgroundColor = CardBg;
            Radius(card, 14);
            Border(card, canAfford ? InactiveBorder : new Color(0.4f, 0.2f, 0.2f), 2);
            card.style.overflow = Overflow.Hidden;
            if (!canAfford) card.style.opacity = 0.55f;

            card.RegisterCallback<ClickEvent>(_ =>
            {
                runeOverlay.style.display = DisplayStyle.None;
                onDone?.Invoke(relic);
            });

            var header = new VisualElement();
            header.style.backgroundColor = relic.color;
            Pad(header, 12, 16);
            header.Add(Lbl(relic.relicName, 22, Color.white, FontStyle.Bold));
            card.Add(header);

            var body = new VisualElement();
            Pad(body, 12, 16);
            var descLbl = Lbl(relic.description, 16, new Color(0.85f, 0.85f, 0.9f));
            descLbl.style.whiteSpace = WhiteSpace.Normal;
            body.Add(descLbl);

            // Price tag
            var priceRow = new VisualElement();
            priceRow.style.flexDirection = FlexDirection.Row;
            priceRow.style.alignItems = Align.Center;
            priceRow.style.marginTop = 10;

            var goldDot = new VisualElement();
            goldDot.style.width = 12;
            goldDot.style.height = 12;
            Radius(goldDot, 6);
            goldDot.style.backgroundColor = new Color(1f, 0.85f, 0.2f);
            goldDot.style.marginRight = 6;
            priceRow.Add(goldDot);

            var priceLbl = Lbl($"{price} Gold", 18, canAfford ? new Color(1f, 0.9f, 0.3f) : new Color(0.8f, 0.3f, 0.3f), FontStyle.Bold);
            priceRow.Add(priceLbl);
            body.Add(priceRow);
            card.Add(body);

            card.RegisterCallback<MouseEnterEvent>(_ =>
            {
                card.style.backgroundColor = CardBgHover;
                Border(card, relic.color, 3);
            });
            card.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                card.style.backgroundColor = CardBg;
                Border(card, canAfford ? InactiveBorder : new Color(0.4f, 0.2f, 0.2f), 2);
            });

            runePanel.Add(card);
        }

        // Skip button
        var skipCard = new VisualElement();
        skipCard.style.flexDirection = FlexDirection.Column;
        skipCard.style.width = 120;
        skipCard.style.marginLeft = 8;
        skipCard.style.backgroundColor = CardBg;
        Radius(skipCard, 14);
        Border(skipCard, Dim, 2);
        skipCard.style.alignItems = Align.Center;
        skipCard.style.justifyContent = Justify.Center;
        Pad(skipCard, 20, 16);
        skipCard.Add(Lbl("SKIP", 20, Dim, FontStyle.Bold));
        skipCard.RegisterCallback<ClickEvent>(_ =>
        {
            runeOverlay.style.display = DisplayStyle.None;
            onDone?.Invoke(null);
        });
        skipCard.RegisterCallback<MouseEnterEvent>(_ =>
        {
            skipCard.style.backgroundColor = CardBgHover;
            Border(skipCard, Color.white, 2);
        });
        skipCard.RegisterCallback<MouseLeaveEvent>(_ =>
        {
            skipCard.style.backgroundColor = CardBg;
            Border(skipCard, Dim, 2);
        });
        runePanel.Add(skipCard);
    }

    public void ShowRestRoom(Action onContinue)
    {
        runeOverlay.style.display = DisplayStyle.Flex;
        runePanel.Clear();
        currentSpellPreview.Clear();
        currentSpellPreview.Add(Lbl("REST ROOM", 24, new Color(0.3f, 0.9f, 0.5f), FontStyle.Bold));
        slotHintLabel.text = "You feel refreshed. HP fully restored.";

        var continueCard = new VisualElement();
        continueCard.style.flexDirection = FlexDirection.Column;
        continueCard.style.width = 200;
        continueCard.style.backgroundColor = CardBg;
        Radius(continueCard, 14);
        Border(continueCard, new Color(0.3f, 0.8f, 0.5f), 2);
        continueCard.style.alignItems = Align.Center;
        continueCard.style.justifyContent = Justify.Center;
        Pad(continueCard, 24, 20);
        continueCard.Add(Lbl("CONTINUE", 24, new Color(0.3f, 0.9f, 0.5f), FontStyle.Bold));
        continueCard.RegisterCallback<ClickEvent>(_ =>
        {
            runeOverlay.style.display = DisplayStyle.None;
            onContinue?.Invoke();
        });
        continueCard.RegisterCallback<MouseEnterEvent>(_ =>
        {
            continueCard.style.backgroundColor = CardBgHover;
            Border(continueCard, new Color(0.4f, 1f, 0.6f), 3);
        });
        continueCard.RegisterCallback<MouseLeaveEvent>(_ =>
        {
            continueCard.style.backgroundColor = CardBg;
            Border(continueCard, new Color(0.3f, 0.8f, 0.5f), 2);
        });
        runePanel.Add(continueCard);
    }

    public void RefreshRelics(List<RelicSO> relics)
    {
        relicBar.Clear();
        foreach (var r in relics)
        {
            var icon = new VisualElement();
            icon.style.width = 28;
            icon.style.height = 28;
            icon.style.marginRight = 4;
            Radius(icon, 6);
            icon.style.backgroundColor = r.color;
            Border(icon, new Color(r.color.r * 0.6f, r.color.g * 0.6f, r.color.b * 0.6f), 1);
            icon.tooltip = $"{r.relicName}: {r.description}";

            var initial = Lbl(r.relicName.Substring(0, 1), 14, Color.white, FontStyle.Bold);
            initial.style.alignSelf = Align.Center;
            initial.style.unityTextAlign = UnityEngine.TextAnchor.MiddleCenter;
            icon.Add(initial);

            relicBar.Add(icon);
        }
    }

    // ─── PUBLIC API ───────────────────────────────────────────────

    public void SetGold(int amount)
    {
        if (goldLabel != null) goldLabel.text = amount.ToString();
    }

    public void SetWave(int w) => waveLabel.text = $"Wave {w}";

    public void SetFloorRoom(int floor, int room) =>
        floorRoomLabel.text = $"Floor {floor} — Room {room}/10";

    public void ShowVictory(int waves, int floors)
    {
        victoryOverlay.style.display = DisplayStyle.Flex;
    }

    public void ShowBossHP(Health bossHP, string bossName)
    {
        trackedBossHP = bossHP;
        bossNameLabel.text = bossName;
        bossHPContainer.style.display = DisplayStyle.Flex;
        RefreshBossHP(bossHP.currentHP, bossHP.maxHP);
        bossHP.OnHPChanged += RefreshBossHP;
    }

    public void HideBossHP()
    {
        if (trackedBossHP != null)
            trackedBossHP.OnHPChanged -= RefreshBossHP;
        trackedBossHP = null;
        bossHPContainer.style.display = DisplayStyle.None;
    }

    void RefreshBossHP(int cur, int max)
    {
        float pct = max > 0 ? (float)cur / max : 0;
        bossHPFill.style.width = new StyleLength(Length.Percent(pct * 100f));
    }

    public void ShowDeath(bool show, int metaReward = 0)
    {
        deathOverlay.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        victoryOverlay.style.display = DisplayStyle.None;
        if (show)
        {
            string rewardText = metaReward > 0 ? $"\n+{metaReward} Soul Essence" : "";
            deathWaveLabel.text = $"Reached wave {waveLabel.text.Replace("Wave ", "")}{rewardText}";
        }
    }

    public void Refresh() { RefreshSpells(); RefreshHP(); }

    void Update()
    {
        UpdateCooldownUI();
        UpdateDamageNumbers();
    }

    void UpdateCooldownUI()
    {
        if (caster == null) return;

        // Spell cooldown fill
        float spellCD = caster.CooldownNormalized;
        bool isSlot0Active = caster.activeSlot == 0;

        // Active slot shows cooldown, inactive shows 0
        float cd0 = isSlot0Active ? spellCD : 0f;
        float cd1 = isSlot0Active ? 0f : spellCD;

        if (spell1CDFill != null)
            spell1CDFill.style.height = new StyleLength(Length.Percent(cd0 * 100f));
        if (spell2CDFill != null)
            spell2CDFill.style.height = new StyleLength(Length.Percent(cd1 * 100f));

        // Cooldown labels
        UpdateCDLabel(spell1Card, cd0);
        UpdateCDLabel(spell2Card, cd1);

        // Dash cooldown + charges
        if (playerCtrl != null && dashCDFill != null)
        {
            int charges = playerCtrl.CurrentDashCharges;
            int maxCharges = playerCtrl.MaxDashCharges;
            float dashCD = playerCtrl.DashCooldownNormalized;

            // Show fill based on partial recharge progress
            float fillPct = charges >= maxCharges ? 100f : ((charges + (1f - dashCD)) / maxCharges * 100f);
            dashCDFill.style.width = new StyleLength(Length.Percent(fillPct));
            dashCDFill.style.backgroundColor = charges > 0
                ? new Color(0.3f, 0.7f, 1f)
                : new Color(0.4f, 0.4f, 0.5f);
        }

        // Potion count
        if (playerCtrl != null && potionLabel != null)
        {
            int pots = playerCtrl.PotionsRemaining;
            potionLabel.text = pots > 0 ? $"[F] Potion x{pots}" : "No Potions";
            potionLabel.style.color = pots > 0 ? new Color(0.3f, 0.9f, 0.4f) : Dim;
        }
    }

    void UpdateCDLabel(VisualElement card, float cd)
    {
        var lbl = card?.Q<Label>("cd-label");
        if (lbl == null) return;
        if (cd <= 0)
        {
            lbl.text = "READY";
            lbl.style.color = new Color(0.5f, 1f, 0.5f);
        }
        else
        {
            lbl.text = "CD";
            lbl.style.color = new Color(1f, 0.5f, 0.3f);
        }
    }

    // ─── DAMAGE NUMBERS ────────────────────────────────────────

    void SpawnDamageNumber(int amount, Vector3 worldPos, bool killed)
    {
        if (damageNumberLayer == null || Camera.main == null) return;

        var lbl = Lbl(amount.ToString(), killed ? 32 : 22,
            killed ? new Color(1f, 0.3f, 0.1f) : new Color(1f, 0.95f, 0.4f),
            FontStyle.Bold);
        lbl.pickingMode = PickingMode.Ignore;
        lbl.style.position = Position.Absolute;
        damageNumberLayer.Add(lbl);

        activeDamageNumbers.Add(new DamageNumberState
        {
            label = lbl,
            worldPos = worldPos,
            elapsed = 0,
            duration = 0.8f,
            velocity = new Vector3(UnityEngine.Random.Range(-0.5f, 0.5f), 2f, 0)
        });
    }

    void UpdateDamageNumbers()
    {
        if (Camera.main == null) return;

        for (int i = activeDamageNumbers.Count - 1; i >= 0; i--)
        {
            var state = activeDamageNumbers[i];
            state.elapsed += Time.unscaledDeltaTime;
            state.worldPos += state.velocity * Time.unscaledDeltaTime;
            activeDamageNumbers[i] = state;

            float t = state.elapsed / state.duration;
            if (t >= 1f)
            {
                damageNumberLayer.Remove(state.label);
                activeDamageNumbers.RemoveAt(i);
                continue;
            }

            // Project world position to screen
            Vector3 screenPos = Camera.main.WorldToScreenPoint(state.worldPos);
            if (screenPos.z < 0) { state.label.style.display = DisplayStyle.None; continue; }

            // Convert to UI Toolkit coordinates (Y is flipped)
            float uiX = screenPos.x / Screen.width * 1920f;
            float uiY = (1f - screenPos.y / Screen.height) * 1080f;

            state.label.style.left = uiX - 20;
            state.label.style.top = uiY - 20;
            state.label.style.display = DisplayStyle.Flex;

            // Fade and scale
            float alpha = 1f - t * t;
            float scale = 1f + (1f - t) * 0.3f;
            state.label.style.opacity = alpha;
            state.label.transform.scale = new Vector3(scale, scale, 1f);
        }
    }

    void RefreshSpells()
    {
        RefreshSpellCard(0, spell1Card);
        RefreshSpellCard(1, spell2Card);
    }

    // ─── HELPERS ──────────────────────────────────────────────────

    static Label Lbl(string text, int size, Color color, FontStyle style = FontStyle.Normal)
    {
        var l = new Label(text);
        l.style.fontSize = size;
        l.style.color = color;
        l.style.unityFontStyleAndWeight = style;
        return l;
    }

    static VisualElement Pill(string text, Color bg, Color fg)
    {
        var pill = new VisualElement();
        pill.style.flexDirection = FlexDirection.Row;
        pill.style.alignItems = Align.Center;
        Color bgA = bg; bgA.a = 0.3f;
        pill.style.backgroundColor = bgA;
        Pad(pill, 4, 10);
        pill.style.marginRight = 6;
        pill.style.marginTop = 2;
        Radius(pill, 8);

        var dot = new VisualElement();
        dot.style.width = 10;
        dot.style.height = 10;
        Radius(dot, 5);
        dot.style.backgroundColor = bg;
        dot.style.marginRight = 6;
        pill.Add(dot);

        pill.Add(Lbl(text, 15, fg, FontStyle.Bold));
        return pill;
    }

    static VisualElement MakeOverlay(Color bg)
    {
        var o = new VisualElement();
        o.style.position = Position.Absolute;
        o.style.top = 0; o.style.bottom = 0; o.style.left = 0; o.style.right = 0;
        o.style.backgroundColor = bg;
        o.style.alignItems = Align.Center;
        o.style.justifyContent = Justify.Center;
        o.style.display = DisplayStyle.None;
        return o;
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
