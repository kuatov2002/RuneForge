using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.TextCore.Text;
using System;

public class GameHUD : MonoBehaviour
{
    SpellCaster caster;
    Health playerHealth;
    UIDocument uiDoc;

    VisualElement hpBar;
    Label waveLabel;
    Label floorRoomLabel;
    VisualElement spell1Card, spell2Card;
    VisualElement runeOverlay;
    VisualElement runePanel;
    VisualElement currentSpellPreview;
    Label slotHintLabel;
    VisualElement deathOverlay;
    Label deathWaveLabel;
    VisualElement victoryOverlay;

    // Boss HUD
    VisualElement bossHPContainer;
    VisualElement bossHPFill;
    Label bossNameLabel;
    Health trackedBossHP;

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
        caster.OnSpellChanged += RefreshSpells;
        RefreshHP();
        RefreshSpells();
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
        var restartHint = Lbl("Press  R  to restart", 22, new Color(0.7f, 0.7f, 0.7f));
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
        var vicHint = Lbl("Press  R  to start a new run", 22, new Color(0.7f, 0.7f, 0.7f));
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

        var controls = Lbl("WASD Move   LMB Cast   RMB Dash   Q Swap", 14, new Color(0.35f, 0.35f, 0.4f));
        controls.style.marginTop = 8;
        bottomArea.Add(controls);

        root.Add(bottomArea);
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
        card.Add(header);

        var pills = new VisualElement();
        pills.name = "pills";
        pills.style.flexDirection = FlexDirection.Row;
        pills.style.flexWrap = Wrap.Wrap;
        card.Add(pills);

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

    public void ShowRuneSelection(ScriptableObject[] options, Action<int> onSelect)
    {
        runeOverlay.style.display = DisplayStyle.Flex;
        runePanel.Clear();
        RefreshCurrentSpellPreview();

        for (int i = 0; i < options.Length; i++)
        {
            int idx = i;
            var card = BuildRuneCard(options[i], () =>
            {
                runeOverlay.style.display = DisplayStyle.None;
                onSelect?.Invoke(idx);
            });
            runePanel.Add(card);
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

    // ─── PUBLIC API ───────────────────────────────────────────────

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

    public void ShowDeath(bool show)
    {
        deathOverlay.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        victoryOverlay.style.display = DisplayStyle.None;
        if (show) deathWaveLabel.text = $"Reached wave {waveLabel.text.Replace("Wave ", "")}";
    }

    public void Refresh() { RefreshSpells(); RefreshHP(); }

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
