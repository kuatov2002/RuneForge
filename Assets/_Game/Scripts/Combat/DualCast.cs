using UnityEngine;

/// <summary>
/// Tracks dual-cast windows. When both spell slots are cast within 1s,
/// triggers a bonus effect (damage boost + elemental combo burst).
/// Attached to player by Bootstrap.
/// </summary>
public class DualCast : MonoBehaviour
{
    const float DualCastWindow = 1.0f;
    const float BonusDamageMult = 1.5f;
    const float BonusDuration = 3f;

    SpellCaster _caster;
    int _lastCastSlot = -1;
    float _lastCastTime;
    float _bonusTimer;

    // Reactive cast: slot 2 auto-fires on dash
    float _reactiveCooldown;

    public bool IsBonusActive => _bonusTimer > 0;
    public float BonusMultiplier => _bonusTimer > 0 ? BonusDamageMult : 1f;

    public void Init(SpellCaster caster)
    {
        _caster = caster;
    }

    void Update()
    {
        if (_bonusTimer > 0) _bonusTimer -= Time.deltaTime;
        if (_reactiveCooldown > 0) _reactiveCooldown -= Time.deltaTime;
    }

    /// <summary>Called by SpellCaster after every cast.</summary>
    public void OnCast(int slotIndex)
    {
        float now = Time.time;

        if (_lastCastSlot >= 0 && _lastCastSlot != slotIndex && (now - _lastCastTime) <= DualCastWindow)
        {
            TriggerDualCast();
        }

        _lastCastSlot = slotIndex;
        _lastCastTime = now;
    }

    void TriggerDualCast()
    {
        _bonusTimer = BonusDuration;

        // Visual feedback: ring burst around player
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(ring.GetComponent<CapsuleCollider>());
        ring.transform.position = transform.position + Vector3.up * 0.1f;
        ring.transform.localScale = new Vector3(0.5f, 0.03f, 0.5f);

        // Color: blend of both spell elements
        Color c1 = _caster.spellSlots[0]?.element?.color ?? Color.white;
        Color c2 = _caster.spellSlots[1]?.element?.color ?? Color.white;
        Color blend = (c1 + c2) * 0.5f;
        ring.GetComponent<Renderer>().material = ShaderCache.NewEmissive(blend, 5f);
        ring.AddComponent<DeathRingEffect>().Init(4f, 0.4f);

        // AoE burst damage around player (small)
        Collider[] hits = Physics.OverlapSphere(transform.position, 3f);
        foreach (var h in hits)
        {
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead && h.GetComponent<PlayerController>() == null)
                hp.TakeDamage(5);
        }

        // Camera + hitstop
        if (TopDownCamera.Instance != null)
            TopDownCamera.Instance.AddTrauma(0.2f);
        if (GameFeel.Instance != null)
            GameFeel.Instance.Hitstop(0.04f);

        SFXSystem.Play(SFXSystem.SFXType.DualCast, transform.position);
    }

    /// <summary>Called on dash — reactive-casts the inactive slot toward aim direction.</summary>
    public void OnDash(Vector3 dashDir)
    {
        if (_reactiveCooldown > 0 || _caster == null) return;

        int inactiveSlot = (_caster.activeSlot + 1) % 2;
        var spell = _caster.spellSlots[inactiveSlot];
        if (spell == null || !spell.IsComplete) return;
        if (_caster.IsSlotDisabled(inactiveSlot)) return;

        _reactiveCooldown = 2f;

        // Fire a weaker version of the inactive spell in dash direction
        float dmg = spell.element.baseDamage * 0.5f * MetaProgression.DamageMultiplier;
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "ReactiveShot";
        go.transform.position = transform.position + dashDir * 0.6f + Vector3.up * 0.5f;
        go.transform.localScale = Vector3.one * 0.18f;

        var col = go.GetComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 1.8f;

        var rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;

        go.GetComponent<Renderer>().material = ShaderCache.NewEmissive(spell.element.color);

        var proj = go.AddComponent<SpellProjectile>();
        proj.Setup(dashDir, 12f, dmg, spell.element, 10f);
    }
}
