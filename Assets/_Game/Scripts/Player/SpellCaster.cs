using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using System;

public class SpellCaster : MonoBehaviour
{
    // ── Element slots (keys 1-4) ──
    public ElementSO[] equippedElements = new ElementSO[4];

    // ── Active orbs ──
    public ElementSO leftOrb;
    public ElementSO rightOrb;

    // ── Overheat system ──
    int[] charges;        // current charges per element slot
    float[] overheatTimers; // >0 means overheated, counting down
    const float OverheatRechargeDefault = 5f;

    // ── Charge shot ──
    float chargeHoldTime;
    bool isCharging;
    const float ChargeThreshold = 0.4f;  // hold time to count as charged
    const float MaxChargeTime = 1.5f;

    // ── Combo bonus (variety tracking) ──
    ElementType[] recentElements = new ElementType[6];
    int recentIndex;
    float comboMultiplier = 1f;

    // ── Cooldown ──
    float cooldownTimer;

    // ── Events ──
    public event Action OnOrbsChanged;
    public event Action<string> OnComboNameChanged; // fires combo name for HUD display

    // ── Run stat upgrades ──
    public float damageBonusMult = 1f;
    public float cooldownBonusMult = 1f;
    public float durationBonusMult = 1f;
    public float radiusBonusMult = 1f;

    OrbDisplay orbDisplay;

    void Awake()
    {
        charges = new int[4];
        overheatTimers = new float[4];
    }

    /// <summary>Initialize with starting elements.</summary>
    public void Init(ElementSO[] startingElements)
    {
        for (int i = 0; i < 4 && i < startingElements.Length; i++)
            equippedElements[i] = startingElements[i];

        for (int i = 0; i < 4; i++)
            charges[i] = equippedElements[i] != null ? equippedElements[i].maxCharges : 4;

        // Start with first two elements as orbs
        if (equippedElements[0] != null) rightOrb = equippedElements[0];
        if (equippedElements[1] != null) leftOrb = equippedElements[1];

        // Create orb display
        orbDisplay = gameObject.AddComponent<OrbDisplay>();
        orbDisplay.Init(this);

        UpdateComboName();
    }

    /// <summary>Replace an equipped element slot with a new element.</summary>
    public void ReplaceElement(int slotIndex, ElementSO newElement)
    {
        if (slotIndex < 0 || slotIndex >= 4) return;
        equippedElements[slotIndex] = newElement;
        charges[slotIndex] = newElement.maxCharges;
        overheatTimers[slotIndex] = 0;

        // If replaced element was an active orb, update it
        if (leftOrb != null && leftOrb == equippedElements[slotIndex])
            leftOrb = newElement;
        if (rightOrb != null && rightOrb == equippedElements[slotIndex])
            rightOrb = newElement;

        OnOrbsChanged?.Invoke();
    }

    /// <summary>Get current charges for element slot.</summary>
    public int GetCharges(int slot) => slot >= 0 && slot < 4 ? charges[slot] : 0;

    /// <summary>Is element slot overheated?</summary>
    public bool IsOverheated(int slot) => slot >= 0 && slot < 4 && overheatTimers[slot] > 0;

    /// <summary>Force-overheat an element slot (e.g. boss mechanic). Sets charges to 0 and starts overheat timer.</summary>
    public void ForceOverheat(int slot)
    {
        if (slot < 0 || slot >= 4 || equippedElements[slot] == null) return;
        charges[slot] = 0;
        overheatTimers[slot] = equippedElements[slot].overheatRechargeTime;
    }

    /// <summary>Get overheat recharge progress (0 = overheated, 1 = ready).</summary>
    public float GetOverheatProgress(int slot)
    {
        if (slot < 0 || slot >= 4 || overheatTimers[slot] <= 0) return 1f;
        float maxTime = equippedElements[slot] != null ? equippedElements[slot].overheatRechargeTime : OverheatRechargeDefault;
        return 1f - (overheatTimers[slot] / maxTime);
    }

    /// <summary>Is the player currently charging a shot?</summary>
    public bool IsCharging => isCharging;
    public float ChargeProgress => Mathf.Clamp01(chargeHoldTime / MaxChargeTime);
    public float CooldownNormalized
    {
        get
        {
            var def = CurrentComboDef;
            if (def == null) return 0f;
            float cd = def.cooldown * cooldownBonusMult;
            return cd > 0 ? Mathf.Clamp01(cooldownTimer / cd) : 0f;
        }
    }

    /// <summary>Current combo spell definition.</summary>
    public ComboSpellDef CurrentComboDef
    {
        get
        {
            if (leftOrb == null || rightOrb == null) return null;
            return ComboSpellRegistry.GetCombo(leftOrb.elementType, rightOrb.elementType);
        }
    }

    void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        // ── Element switching (keys 1-4) ──
        if (kb.digit1Key.wasPressedThisFrame) PushElement(0);
        if (kb.digit2Key.wasPressedThisFrame) PushElement(1);
        if (kb.digit3Key.wasPressedThisFrame) PushElement(2);
        if (kb.digit4Key.wasPressedThisFrame) PushElement(3);

        // ── Overheat recharge ──
        for (int i = 0; i < 4; i++)
        {
            if (overheatTimers[i] > 0)
            {
                overheatTimers[i] -= Time.deltaTime;
                if (overheatTimers[i] <= 0)
                {
                    // Fully recharge
                    charges[i] = equippedElements[i] != null ? equippedElements[i].maxCharges : 4;
                    overheatTimers[i] = 0;
                    OnOrbsChanged?.Invoke();
                }
            }
        }

        // ── Cooldown ──
        if (cooldownTimer > 0) cooldownTimer -= Time.deltaTime;

        // ── Casting (LMB) ──
        if (leftOrb == null || rightOrb == null) return;

        // Don't cast if mouse is over UI
        if (IsPointerOverUI()) return;

        // Don't cast if either orb's element is overheated
        if (IsElementOverheated(leftOrb) || IsElementOverheated(rightOrb))
        {
            isCharging = false;
            return;
        }

        if (mouse.leftButton.wasPressedThisFrame && cooldownTimer <= 0)
        {
            isCharging = true;
            chargeHoldTime = 0;
        }

        if (isCharging)
        {
            chargeHoldTime += Time.deltaTime;

            if (mouse.leftButton.wasReleasedThisFrame || chargeHoldTime >= MaxChargeTime)
            {
                bool charged = chargeHoldTime >= ChargeThreshold;
                Fire(charged);
                isCharging = false;
                chargeHoldTime = 0;
            }
        }
    }

    void PushElement(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= 4) return;
        var elem = equippedElements[slotIndex];
        if (elem == null) return;
        if (IsOverheated(slotIndex)) return; // Can't select overheated element

        // Push: new element appears on right, old right goes to left, old left disappears
        leftOrb = rightOrb;
        rightOrb = elem;

        UpdateComboName();
        OnOrbsChanged?.Invoke();
        SFXSystem.Play(SFXSystem.SFXType.MenuClick, transform.position, 0.3f);
    }

    void Fire(bool charged)
    {
        var def = CurrentComboDef;
        if (def == null) return;

        // Consume charges (Overcharge mutation: 3 charges when charged)
        int chargeCost = charged ? SpellMutationSystem.ModifyChargedCost(2) : 1;
        ConsumeCharges(leftOrb, chargeCost);
        ConsumeCharges(rightOrb, chargeCost);

        // Calculate damage with bonuses
        UpdateComboMultiplier();
        float dmgMult = damageBonusMult * comboMultiplier * MetaProgression.DamageMultiplier;

        // Momentum bonus
        var momentum = GetComponent<MomentumSystem>();
        if (momentum != null) dmgMult *= momentum.DamageMultiplier;

        // Crit chance
        if (UnityEngine.Random.value < MetaProgression.CritChance)
            dmgMult *= 2f;

        // Relic modifiers
        var relicMgr = GetComponent<RelicManager>();

        // BloodPact cost
        if (relicMgr != null && relicMgr.HasRelic(RelicType.BloodPact))
        {
            var hp = GetComponent<Health>();
            if (hp != null && !hp.IsDead)
            {
                hp.currentHP = Mathf.Max(1, hp.currentHP - 1);
                hp.InvokeHPChanged();
            }
        }

        // Get target position
        Vector3 targetPos = GetCursorWorldPosition();

        // Set cooldown
        cooldownTimer = def.cooldown * cooldownBonusMult;

        // Fire the combo spell
        ComboSpellFactory.Cast(def, transform.position, targetPos, dmgMult, charged);

        // Chain Cast mutation: 20% chance to auto-cast again for free
        if (SpellMutationSystem.HasMutation(SpellMutationSystem.MutationType.ChainCast)
            && UnityEngine.Random.value < 0.2f)
        {
            ComboSpellFactory.Cast(def, transform.position, targetPos, dmgMult * 0.7f, false);
        }

        // Track element variety
        TrackElementUsage(leftOrb.elementType);
        TrackElementUsage(rightOrb.elementType);

        SFXSystem.Play(SFXSystem.SFXType.Cast, transform.position);
    }

    bool IsElementOverheated(ElementSO elem)
    {
        if (elem == null) return false;
        int slot = GetElementSlot(elem);
        return slot >= 0 && overheatTimers[slot] > 0;
    }

    void ConsumeCharges(ElementSO elem, int cost)
    {
        if (elem == null) return;
        int slot = GetElementSlot(elem);
        if (slot < 0) return;

        charges[slot] = Mathf.Max(0, charges[slot] - cost);

        if (charges[slot] <= 0)
        {
            // Overheat!
            float rechargeTime = elem.overheatRechargeTime;
            overheatTimers[slot] = rechargeTime;
            OnOrbsChanged?.Invoke();
        }
    }

    int GetElementSlot(ElementSO elem)
    {
        for (int i = 0; i < 4; i++)
            if (equippedElements[i] == elem) return i;
        return -1;
    }

    Vector3 GetCursorWorldPosition()
    {
        var mouse = Mouse.current;
        if (mouse == null || Camera.main == null) return transform.position + transform.forward * 5f;

        Ray ray = Camera.main.ScreenPointToRay(mouse.position.ReadValue());
        Plane ground = new Plane(Vector3.up, 0);
        if (ground.Raycast(ray, out float d))
            return ray.GetPoint(d);
        return transform.position + transform.forward * 5f;
    }

    void TrackElementUsage(ElementType type)
    {
        recentElements[recentIndex % recentElements.Length] = type;
        recentIndex++;
    }

    void UpdateComboMultiplier()
    {
        // Count unique elements in recent history
        int filled = Mathf.Min(recentIndex, recentElements.Length);
        if (filled < 2)
        {
            comboMultiplier = 1f;
            return;
        }

        var seen = new System.Collections.Generic.HashSet<ElementType>();
        for (int i = 0; i < filled; i++)
            seen.Add(recentElements[i]);

        int uniqueCount = seen.Count;

        if (uniqueCount <= 2)
            comboMultiplier = 1.0f; // No penalty for specialization
        else if (uniqueCount == 3)
            comboMultiplier = 1.15f; // Good variety bonus
        else
            comboMultiplier = 1.3f; // Excellent variety bonus
    }

    void UpdateComboName()
    {
        var def = CurrentComboDef;
        if (def != null)
            OnComboNameChanged?.Invoke(def.comboName);
    }

    /// <summary>Check if the mouse pointer is over any UI element.</summary>
    bool IsPointerOverUI()
    {
        // Check UIElements (UIDocument panels)
        var uiDocs = FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
        foreach (var doc in uiDocs)
        {
            if (doc == null || doc.rootVisualElement == null) continue;
            var panel = doc.rootVisualElement.panel;
            if (panel == null) continue;

            var mouse = Mouse.current;
            if (mouse == null) continue;
            Vector2 mousePos = mouse.position.ReadValue();
            // UIElements uses top-left origin, Mouse uses bottom-left
            mousePos.y = Screen.height - mousePos.y;

            // Pick the element under the mouse
            var picked = panel.Pick(new Vector2(mousePos.x, mousePos.y));
            if (picked != null && picked != doc.rootVisualElement && picked.pickingMode != PickingMode.Ignore)
                return true;
        }

        // Also check legacy EventSystem
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            return true;

        return false;
    }

    void OnDisable()
    {
        if (orbDisplay != null) { Destroy(orbDisplay); orbDisplay = null; }
    }
}
