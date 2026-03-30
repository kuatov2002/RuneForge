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
    float[] overheatTimers; // >0 means recharging, counting down
    bool[] overheatPenalty; // true = in initial penalty phase (no charges yet)
    const float OverheatRechargeDefault = 5f;
    const float OverheatPenaltyTime = 2.5f; // dead time before recharge starts

    // ── Charge shot ──
    float chargeHoldTime;
    bool isCharging;
    const float ChargeThreshold = 0.4f;  // hold time to count as charged
    const float MaxChargeTime = 1.5f;

    // ── Combo bonus (variety tracking) ──
    ElementType[] recentElements = new ElementType[8];
    int recentIndex;
    [HideInInspector] public float comboMultiplier = 1f;

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
        overheatPenalty = new bool[4];
    }

    /// <summary>Initialize with starting elements.</summary>
    public void Init(ElementSO[] startingElements)
    {
        for (int i = 0; i < 4 && i < startingElements.Length; i++)
            equippedElements[i] = startingElements[i];

        for (int i = 0; i < 4; i++)
            charges[i] = equippedElements[i] != null
                ? equippedElements[i].maxCharges + RunUpgradeSystem.RunExtraCharges : 4;

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
        charges[slotIndex] = newElement.maxCharges + RunUpgradeSystem.RunExtraCharges;
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

    /// <summary>Is element slot overheated (no charges)?</summary>
    public bool IsOverheated(int slot) => slot >= 0 && slot < 4 && charges[slot] <= 0 && overheatTimers[slot] > 0;

    /// <summary>Force-overheat an element slot (e.g. boss mechanic). Sets charges to 0 and starts penalty + trickle.</summary>
    public void ForceOverheat(int slot)
    {
        if (slot < 0 || slot >= 4 || equippedElements[slot] == null) return;
        charges[slot] = 0;
        overheatPenalty[slot] = true;
        overheatTimers[slot] = OverheatPenaltyTime;
    }

    /// <summary>Get overheat recharge progress (0 = empty, 1 = fully charged).</summary>
    public float GetOverheatProgress(int slot)
    {
        if (slot < 0 || slot >= 4) return 1f;
        int maxTotal = equippedElements[slot] != null
            ? equippedElements[slot].maxCharges + RunUpgradeSystem.RunExtraCharges : 4;
        if (charges[slot] >= maxTotal) return 1f;

        // During penalty: bar stays at 0
        if (overheatPenalty[slot]) return 0f;

        // Trickle phase: charge-based progress with smooth fill for current charge
        float baseProgress = (float)charges[slot] / maxTotal;
        if (overheatTimers[slot] > 0)
        {
            float totalTime = (equippedElements[slot] != null
                ? equippedElements[slot].overheatRechargeTime : OverheatRechargeDefault)
                * RunUpgradeSystem.RunRechargeMultiplier;
            float perCharge = totalTime / maxTotal;
            float fill = 1f - (overheatTimers[slot] / perCharge);
            baseProgress += fill / maxTotal;
        }
        return Mathf.Clamp01(baseProgress);
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

        // ── Overheat: 2.5s penalty → trickle (1 charge per interval) ──
        for (int i = 0; i < 4; i++)
        {
            if (overheatTimers[i] > 0)
            {
                overheatTimers[i] -= Time.deltaTime;
                if (overheatTimers[i] <= 0)
                {
                    int maxTotal = equippedElements[i] != null
                        ? equippedElements[i].maxCharges + RunUpgradeSystem.RunExtraCharges : 4;
                    float totalTime = (equippedElements[i] != null
                        ? equippedElements[i].overheatRechargeTime : OverheatRechargeDefault)
                        * RunUpgradeSystem.RunRechargeMultiplier;
                    float perCharge = totalTime / maxTotal;

                    if (overheatPenalty[i])
                    {
                        // Penalty ended — restore first charge, start trickle
                        overheatPenalty[i] = false;
                        charges[i] = 1;
                        overheatTimers[i] = charges[i] < maxTotal ? perCharge : 0;
                    }
                    else
                    {
                        // Trickle — restore next charge
                        charges[i]++;
                        overheatTimers[i] = charges[i] < maxTotal ? perCharge : 0;
                    }
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

    /// <summary>Preview what combo would result from pushing a given slot, without actually changing state.</summary>
    public ComboSpellDef PreviewPush(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= 4) return null;
        var elem = equippedElements[slotIndex];
        if (elem == null) return null;
        if (IsOverheated(slotIndex)) return null;
        // Simulate push: new right = elem, new left = current right
        ElementType newLeft = rightOrb != null ? rightOrb.elementType : elem.elementType;
        ElementType newRight = elem.elementType;
        return ComboSpellRegistry.GetCombo(newLeft, newRight);
    }

    /// <summary>Get the color of an element in a given slot (for preview UI).</summary>
    public Color GetSlotColor(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= 4 || equippedElements[slotIndex] == null)
            return Color.gray;
        return equippedElements[slotIndex].color;
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

    /// <summary>
    /// Soft cap for damage multiplier. Below the threshold, returns the value unchanged.
    /// Above it, excess is sqrt-compressed to create diminishing returns.
    /// Example: threshold=4, input=9 → 4 + sqrt(9-4) = 4 + 2.24 = 6.24 (instead of 9)
    /// </summary>
    static float SoftCapMultiplier(float value, float threshold = 4f)
    {
        if (value <= threshold) return value;
        return threshold + Mathf.Sqrt(value - threshold);
    }

    void Fire(bool charged)
    {
        var def = CurrentComboDef;
        if (def == null) return;

        // Consume charges (Overcharge mutation: 3 charges when charged)
        // Echo Charge: every 4th cast is free
        bool freeCast = RunUpgradeSystem.ShouldSkipChargeCost();
        if (!freeCast)
        {
            int chargeCost = charged ? SpellMutationSystem.ModifyChargedCost(2) : 1;
            ConsumeCharges(leftOrb, chargeCost);
            ConsumeCharges(rightOrb, chargeCost);
        }

        // Calculate damage with bonuses
        UpdateComboMultiplier();
        float dmgMult = damageBonusMult * comboMultiplier * MetaProgression.DamageMultiplier;

        // Spell Mastery: bonus for same-element combos
        if (leftOrb.elementType == rightOrb.elementType)
            dmgMult *= MetaProgression.SpellMasteryBonus;

        // Momentum bonus
        var momentum = GetComponent<MomentumSystem>();
        if (momentum != null) dmgMult *= momentum.DamageMultiplier;

        // Conductor synergy bonus (+50% after Lightning cast)
        var synergySys = GetComponent<SynergySystem>();
        if (synergySys != null) dmgMult *= synergySys.GetDamageMultiplier();

        // Crit chance
        if (UnityEngine.Random.value < MetaProgression.CritChance)
            dmgMult *= 2f;

        // Soft cap moved to ComboSpellFactory.Cast() to cover charged + mutation multipliers

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

        // Chain Cast mutation: 20% chance to auto-cast at current cursor (not stale position)
        if (SpellMutationSystem.HasMutation(SpellMutationSystem.MutationType.ChainCast)
            && UnityEngine.Random.value < 0.2f)
        {
            Vector3 chainTarget = GetCursorWorldPosition();
            ComboSpellFactory.Cast(def, transform.position, chainTarget, dmgMult * 0.7f, false);
        }

        // Notify synergy system of cast
        if (synergySys != null)
            synergySys.OnSpellCast(leftOrb.elementType);

        // Track element variety
        TrackElementUsage(leftOrb.elementType);
        TrackElementUsage(rightOrb.elementType);

        // Spell Rush: +25% move speed for 2s after casting
        if (RunUpgradeSystem.HasSpellRush)
        {
            var ctrl = GetComponent<PlayerController>();
            if (ctrl != null) ctrl.ApplySpeedBuff(0.25f, 2f);
        }

        SFXSystem.Play(SFXSystem.SFXType.Cast, transform.position);
    }

    bool IsElementOverheated(ElementSO elem)
    {
        if (elem == null) return false;
        int slot = GetElementSlot(elem);
        return slot >= 0 && charges[slot] <= 0 && overheatTimers[slot] > 0;
    }

    void ConsumeCharges(ElementSO elem, int cost)
    {
        if (elem == null) return;
        int slot = GetElementSlot(elem);
        if (slot < 0) return;

        charges[slot] = Mathf.Max(0, charges[slot] - cost);

        if (charges[slot] <= 0)
        {
            // Start overheat: always restart penalty, even if trickle was in progress
            overheatPenalty[slot] = true;
            overheatTimers[slot] = OverheatPenaltyTime;
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
            comboMultiplier = 1.0f;
        else if (uniqueCount == 3)
            comboMultiplier = 1.25f;
        else if (uniqueCount == 4)
            comboMultiplier = 1.5f;
        else
            comboMultiplier = 1.75f;
    }

    void UpdateComboName()
    {
        var def = CurrentComboDef;
        if (def != null)
            OnComboNameChanged?.Invoke(def.comboName);
    }

    // Cached UIDocuments to avoid per-frame FindObjectsByType
    UIDocument[] _cachedUIDocs;
    float _uiDocCacheTimer;

    /// <summary>Check if the mouse pointer is over any UI element.</summary>
    bool IsPointerOverUI()
    {
        // Refresh cache every 2 seconds instead of every frame
        _uiDocCacheTimer -= Time.deltaTime;
        if (_cachedUIDocs == null || _uiDocCacheTimer <= 0f)
        {
            _cachedUIDocs = FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
            _uiDocCacheTimer = 2f;
        }

        // Check UIElements (UIDocument panels)
        foreach (var doc in _cachedUIDocs)
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
