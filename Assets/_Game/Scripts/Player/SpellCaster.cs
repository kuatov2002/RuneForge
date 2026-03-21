using UnityEngine;
using UnityEngine.InputSystem;
using System;

public class SpellCaster : MonoBehaviour
{
    public SpellData[] spellSlots = new SpellData[2];
    public int activeSlot;

    public event Action OnSpellChanged;
    public event Action<ElementSO> OnSpellFired; // For Mirror enemy

    float cooldownTimer;
    AuraForm activeAura;
    OrbitForm activeOrbit;
    bool[] slotDisabled = new bool[2];

    public void SetSlotDisabled(int slot, bool disabled)
    {
        if (slot < 0 || slot >= 2) return;
        slotDisabled[slot] = disabled;
        OnSpellChanged?.Invoke();
    }

    public bool IsSlotDisabled(int slot) => slot >= 0 && slot < 2 && slotDisabled[slot];

    public SpellData ActiveSpell => spellSlots[activeSlot];

    /// <summary>Spell cooldown progress: 0 = ready, 1 = full CD.</summary>
    public float CooldownNormalized
    {
        get
        {
            var spell = ActiveSpell;
            if (spell == null || spell.form == null || spell.form.cooldown <= 0) return 0f;
            return Mathf.Clamp01(cooldownTimer / spell.form.cooldown);
        }
    }

    void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        if (kb.qKey.wasPressedThisFrame)
        {
            DeactivatePassiveForms();
            activeSlot = (activeSlot + 1) % 2;
            ActivatePassiveForms();
            OnSpellChanged?.Invoke();
        }

        cooldownTimer -= Time.deltaTime;

        var spell = ActiveSpell;
        if (spell == null || !spell.IsComplete) return;
        if (slotDisabled[activeSlot]) return;

        // Aura is passive
        if (spell.form.formType == FormType.Aura && activeAura == null)
            ActivatePassiveForms();

        if (spell.form.formType != FormType.Aura)
        {
            if (mouse.leftButton.wasPressedThisFrame && cooldownTimer <= 0)
            {
                Cast(spell);
                cooldownTimer = spell.form.cooldown;
            }
        }
    }

    void ActivatePassiveForms()
    {
        var spell = ActiveSpell;
        if (spell == null || !spell.IsComplete) return;

        if (spell.form.formType == FormType.Aura && activeAura == null)
        {
            float dmg = CalculateDamage(spell);
            float rad = spell.form.auraRadius;
            var mod = GetActiveModifier(spell);

            if (mod != null && mod.modifierType == ModifierType.Oversize)
                rad *= mod.sizeMultiplier;

            activeAura = gameObject.AddComponent<AuraForm>();
            activeAura.Init(rad, dmg, spell.element, spell.form.cooldown,
                GetLeechPercent(spell), GetComponent<Health>(), IsVolatile(spell), GetVolatileMiss(spell));
        }
    }

    void DeactivatePassiveForms()
    {
        if (activeAura != null) { activeAura.Deactivate(); activeAura = null; }
        if (activeOrbit != null) { activeOrbit.Cleanup(); activeOrbit = null; }
    }

    void Cast(SpellData spell)
    {
        OnSpellFired?.Invoke(spell.element);

        var mod = GetActiveModifier(spell);
        ModifierType modType = mod != null ? mod.modifierType : ModifierType.None;

        int count = 1;
        float spreadAngle = 0;
        float damageMult = spell.modifier != null ? spell.modifier.damageMultiplier : 1f;
        float damage = spell.element.baseDamage * damageMult;

        // Volatile: 1.5x damage
        if (modType == ModifierType.Volatile)
            damage *= 1.5f;

        // Split: multiple projectiles
        if (modType == ModifierType.Split)
        {
            count = mod.splitCount;
            spreadAngle = mod.splitSpreadAngle;
        }

        // Form-specific oversize params
        float sizeScale = (modType == ModifierType.Oversize) ? mod.sizeMultiplier : 1f;

        switch (spell.form.formType)
        {
            case FormType.Bolt:
                FireDirectional(spell, count, spreadAngle, damage, (dir, dmg) =>
                {
                    CreateBolt(dir, spell, dmg, mod);
                });
                break;

            case FormType.Cone:
                FireDirectional(spell, count, spreadAngle, damage, (dir, dmg) =>
                {
                    float coneAngle = spell.form.coneAngle * sizeScale;
                    float coneRange = spell.form.coneRange * sizeScale;
                    ConeAttack.Fire(transform.position, dir, coneAngle, coneRange, dmg, spell.element,
                        GetLeechPercent(spell), GetComponent<Health>(), IsVolatile(spell), GetVolatileMiss(spell));
                });
                break;

            case FormType.Beam:
                FireDirectional(spell, count, spreadAngle, damage, (dir, dmg) =>
                {
                    float beamWidth = spell.form.beamWidth * sizeScale;
                    float beamRange = spell.form.beamRange;
                    int pierce = (modType == ModifierType.Pierce) ? mod.pierceCount : 0;
                    BeamAttack.Fire(transform.position, dir, beamRange, beamWidth, dmg, spell.element,
                        GetLeechPercent(spell), GetComponent<Health>(), IsVolatile(spell), GetVolatileMiss(spell));
                });
                break;

            case FormType.Orbit:
                if (activeOrbit != null) break;
                int orbCount = spell.form.orbitCount;
                if (modType == ModifierType.Split) orbCount += mod.splitCount;
                float orbRadius = spell.form.orbitRadius * sizeScale;
                float orbSize = sizeScale;
                activeOrbit = gameObject.AddComponent<OrbitForm>();
                activeOrbit.Init(orbRadius, spell.form.orbitSpeed, orbCount, damage, spell.element,
                    spell.form.orbitDuration, orbSize, modType == ModifierType.Bounce ? mod.bounceCount : 0,
                    modType == ModifierType.Homing, GetLeechPercent(spell), GetComponent<Health>(),
                    IsVolatile(spell), GetVolatileMiss(spell));
                break;

            case FormType.Trap:
                float trapRadius = spell.form.trapRadius * sizeScale;
                PlaceTraps(spell, count, damage, trapRadius,
                    GetLeechPercent(spell), GetComponent<Health>(), IsVolatile(spell), GetVolatileMiss(spell));
                break;
        }
    }

    ModifierSO GetActiveModifier(SpellData spell)
    {
        if (spell.modifier == null || spell.modifier.modifierType == ModifierType.None) return null;
        if (!ModifierSO.IsCompatible(spell.modifier.modifierType, spell.form.formType)) return null;
        return spell.modifier;
    }

    float GetLeechPercent(SpellData spell)
    {
        var mod = GetActiveModifier(spell);
        return (mod != null && mod.modifierType == ModifierType.Leech) ? mod.leechPercent : 0f;
    }

    bool IsVolatile(SpellData spell)
    {
        var mod = GetActiveModifier(spell);
        return mod != null && mod.modifierType == ModifierType.Volatile;
    }

    float GetVolatileMiss(SpellData spell)
    {
        var mod = GetActiveModifier(spell);
        return (mod != null && mod.modifierType == ModifierType.Volatile) ? mod.volatileMissChance : 0f;
    }

    void FireDirectional(SpellData spell, int count, float spreadAngle, float damage, Action<Vector3, float> fireAction)
    {
        Vector3 baseDir = transform.forward;
        float startAngle = -(count - 1) * spreadAngle * 0.5f;

        for (int i = 0; i < count; i++)
        {
            float angle = startAngle + i * spreadAngle;
            Vector3 dir = Quaternion.Euler(0, angle, 0) * baseDir;
            fireAction(dir, damage);
        }
    }

    void CreateBolt(Vector3 dir, SpellData spell, float damage, ModifierSO mod)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Bolt";
        go.transform.position = transform.position + dir * 0.6f + Vector3.up * 0.5f;

        float scale = 0.25f;
        float spd = spell.form.projectileSpeed;

        if (mod != null && mod.modifierType == ModifierType.Oversize)
        {
            scale *= mod.sizeMultiplier;
            spd *= mod.speedPenalty;
        }
        if (mod != null && mod.modifierType == ModifierType.Homing)
            spd *= mod.homingSpeedMult;

        go.transform.localScale = Vector3.one * scale;

        var col = go.GetComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 1.8f;

        var rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;

        var mat = ShaderCache.NewEmissive(spell.element.color);
        go.GetComponent<Renderer>().material = mat;

        var proj = go.AddComponent<SpellProjectile>();
        proj.Setup(dir, spd, damage, spell.element, spell.form.range);
        proj.ApplyModifier(mod, GetComponent<Health>());
    }

    void PlaceTraps(SpellData spell, int count, float damage, float radius,
        float leech, Health playerHp, bool isVolatile, float missCh)
    {
        var mouse = Mouse.current;
        if (mouse == null || Camera.main == null) return;

        Ray ray = Camera.main.ScreenPointToRay(mouse.position.ReadValue());
        Plane ground = new Plane(Vector3.up, 0);
        if (!ground.Raycast(ray, out float d)) return;

        Vector3 basePos = ray.GetPoint(d);

        for (int i = 0; i < count; i++)
        {
            Vector3 offset = count > 1 ? UnityEngine.Random.insideUnitSphere * 1.5f : Vector3.zero;
            offset.y = 0;
            TrapForm.Create(basePos + offset, damage, spell.element, radius, spell.form.trapArmTime,
                leech, playerHp, isVolatile, missCh);
        }
    }

    float CalculateDamage(SpellData spell)
    {
        float dmg = spell.element.baseDamage;
        if (spell.modifier != null) dmg *= spell.modifier.damageMultiplier;
        var mod = GetActiveModifier(spell);
        if (mod != null && mod.modifierType == ModifierType.Volatile)
            dmg *= 1.5f;
        return dmg;
    }

    void OnDisable()
    {
        DeactivatePassiveForms();
    }
}
