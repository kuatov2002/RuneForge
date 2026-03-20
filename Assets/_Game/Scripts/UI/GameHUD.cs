using UnityEngine;
using UnityEngine.UIElements;
using System;

public class GameHUD : MonoBehaviour
{
    UIDocument uiDoc;
    Label hpLabel;
    Label spell1Label;
    Label spell2Label;
    VisualElement spell1Slot;
    VisualElement spell2Slot;
    Label waveLabel;
    VisualElement runePanel;
    Label deathLabel;

    SpellCaster caster;
    Health playerHealth;

    public void Init(SpellCaster sc, Health hp)
    {
        caster = sc;
        playerHealth = hp;

        uiDoc = gameObject.AddComponent<UIDocument>();
        uiDoc.panelSettings = CreatePanelSettings();

        var root = uiDoc.rootVisualElement;
        root.style.flexGrow = 1;
        root.style.flexDirection = FlexDirection.Column;
        root.style.justifyContent = Justify.SpaceBetween;

        // Top bar
        var topBar = new VisualElement();
        topBar.style.flexDirection = FlexDirection.Row;
        topBar.style.paddingTop = 10;
        topBar.style.paddingLeft = 10;

        hpLabel = new Label($"HP: {hp.currentHP}/{hp.maxHP}");
        hpLabel.style.fontSize = 24;
        hpLabel.style.color = Color.white;
        hpLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        topBar.Add(hpLabel);

        waveLabel = new Label("Wave 1");
        waveLabel.style.fontSize = 20;
        waveLabel.style.color = new Color(1f, 0.9f, 0.3f);
        waveLabel.style.marginLeft = 30;
        topBar.Add(waveLabel);

        root.Add(topBar);

        // Death label (hidden)
        deathLabel = new Label("YOU DIED - Press R to restart");
        deathLabel.style.fontSize = 36;
        deathLabel.style.color = new Color(1f, 0.2f, 0.2f);
        deathLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        deathLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        deathLabel.style.position = Position.Absolute;
        deathLabel.style.top = new Length(40, LengthUnit.Percent);
        deathLabel.style.left = 0;
        deathLabel.style.right = 0;
        deathLabel.style.display = DisplayStyle.None;
        root.Add(deathLabel);

        // Rune selection (hidden)
        runePanel = new VisualElement();
        runePanel.style.flexGrow = 1;
        runePanel.style.alignItems = Align.Center;
        runePanel.style.justifyContent = Justify.Center;
        runePanel.style.display = DisplayStyle.None;
        root.Add(runePanel);

        // Bottom bar
        var bottomBar = new VisualElement();
        bottomBar.style.flexDirection = FlexDirection.Row;
        bottomBar.style.justifyContent = Justify.Center;
        bottomBar.style.paddingBottom = 20;
        bottomBar.style.alignItems = Align.Center;

        spell1Slot = CreateSpellSlot(out spell1Label);
        spell2Slot = CreateSpellSlot(out spell2Label);

        bottomBar.Add(spell1Slot);

        var swapLabel = new Label("  [Q]  ");
        swapLabel.style.color = Color.gray;
        swapLabel.style.fontSize = 16;
        bottomBar.Add(swapLabel);

        bottomBar.Add(spell2Slot);
        root.Add(bottomBar);

        // Controls hint
        var controls = new Label("WASD - Move | LMB - Cast | RMB - Dash | Q - Swap spell");
        controls.style.fontSize = 12;
        controls.style.color = new Color(0.5f, 0.5f, 0.5f);
        controls.style.unityTextAlign = TextAnchor.MiddleCenter;
        controls.style.paddingBottom = 5;
        root.Add(controls);

        playerHealth.OnHPChanged += (cur, max) => hpLabel.text = $"HP: {cur}/{max}";
        caster.OnSpellChanged += UpdateSpellDisplay;

        UpdateSpellDisplay();
    }

    VisualElement CreateSpellSlot(out Label label)
    {
        var slot = new VisualElement();
        slot.style.backgroundColor = new Color(0, 0, 0, 0.7f);
        slot.style.paddingTop = 8;
        slot.style.paddingBottom = 8;
        slot.style.paddingLeft = 12;
        slot.style.paddingRight = 12;
        slot.style.borderTopLeftRadius = 8;
        slot.style.borderTopRightRadius = 8;
        slot.style.borderBottomLeftRadius = 8;
        slot.style.borderBottomRightRadius = 8;
        SetBorder(slot, Color.gray, 2);
        slot.style.width = 200;

        label = new Label("Empty");
        label.style.fontSize = 14;
        label.style.color = Color.white;
        label.style.unityTextAlign = TextAnchor.MiddleCenter;
        slot.Add(label);

        return slot;
    }

    static void SetBorder(VisualElement el, Color color, int width)
    {
        el.style.borderTopColor = color;
        el.style.borderBottomColor = color;
        el.style.borderLeftColor = color;
        el.style.borderRightColor = color;
        el.style.borderTopWidth = width;
        el.style.borderBottomWidth = width;
        el.style.borderLeftWidth = width;
        el.style.borderRightWidth = width;
    }

    void UpdateSpellDisplay()
    {
        UpdateSlotVisual(0, spell1Slot, spell1Label);
        UpdateSlotVisual(1, spell2Slot, spell2Label);
    }

    void UpdateSlotVisual(int index, VisualElement slot, Label label)
    {
        var spell = caster.spellSlots[index];
        bool isActive = caster.activeSlot == index;

        var borderColor = isActive ? Color.yellow : Color.gray;
        SetBorder(slot, borderColor, isActive ? 3 : 2);

        if (spell != null && spell.element != null)
        {
            string elem = spell.element.elementName;
            string form = spell.form != null ? spell.form.formName : "???";
            string mod = spell.modifier != null && spell.modifier.modifierType != ModifierType.None
                ? $" + {spell.modifier.modifierName}" : "";
            label.text = $"{elem} {form}{mod}";
            label.style.color = spell.element.color;
        }
        else
        {
            label.text = "Empty";
            label.style.color = Color.gray;
        }
    }

    public void SetWave(int wave)
    {
        waveLabel.text = $"Wave {wave}";
    }

    public void ShowRuneSelection(string[] runeNames, Action<int> onSelect)
    {
        runePanel.style.display = DisplayStyle.Flex;
        runePanel.Clear();

        var title = new Label("Choose a Rune");
        title.style.fontSize = 28;
        title.style.color = Color.white;
        title.style.marginBottom = 20;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        runePanel.Add(title);

        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;

        for (int i = 0; i < runeNames.Length; i++)
        {
            int idx = i;
            var btn = new Button(() =>
            {
                runePanel.style.display = DisplayStyle.None;
                onSelect?.Invoke(idx);
            });
            btn.text = runeNames[i];
            btn.style.fontSize = 16;
            btn.style.paddingTop = 15;
            btn.style.paddingBottom = 15;
            btn.style.paddingLeft = 25;
            btn.style.paddingRight = 25;
            btn.style.marginLeft = 10;
            btn.style.marginRight = 10;
            btn.style.backgroundColor = new Color(0.15f, 0.15f, 0.2f, 0.9f);
            btn.style.color = Color.white;
            btn.style.borderTopLeftRadius = 8;
            btn.style.borderTopRightRadius = 8;
            btn.style.borderBottomLeftRadius = 8;
            btn.style.borderBottomRightRadius = 8;
            SetBorder(btn, new Color(0.4f, 0.4f, 0.5f), 1);
            row.Add(btn);
        }

        runePanel.Add(row);
    }

    public void ShowDeath(bool show)
    {
        deathLabel.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
    }

    public void Refresh()
    {
        UpdateSpellDisplay();
        if (playerHealth != null)
            hpLabel.text = $"HP: {playerHealth.currentHP}/{playerHealth.maxHP}";
    }

    PanelSettings CreatePanelSettings()
    {
        var ps = ScriptableObject.CreateInstance<PanelSettings>();
        ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        ps.referenceResolution = new Vector2Int(1920, 1080);
        return ps;
    }
}
