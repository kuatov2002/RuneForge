using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Tracks elemental statuses on an enemy and triggers reactions
/// when a new element hits an enemy that already has a status.
/// Attach alongside Health on every enemy.
/// </summary>
public class ElementalStatus : MonoBehaviour
{
    // ── Events ──────────────────────────────────────────────
    /// <summary>Fired when a reaction occurs. Same signature as SpellInteractionSystem.OnReaction.</summary>
    public static event Action<string, Vector3, Color> OnReaction;

    /// <summary>Fired when a status is added or removed on this enemy. (type, added)</summary>
    public event Action<ElementalStatusType, bool> OnStatusChanged;

    // ── State ───────────────────────────────────────────────
    readonly Dictionary<ElementalStatusType, float> _timers = new();
    Health _health;
    float _dotAccumulator; // accumulate DoT to deal integer damage

    void Awake()
    {
        _health = GetComponent<Health>();
    }

    // ── Public API ──────────────────────────────────────────

    /// <summary>
    /// Apply an element to this enemy. Checks for reactions with existing statuses.
    /// Returns a damage multiplier (>1 if reaction occurred).
    /// </summary>
    public float ApplyElement(ElementType element, float triggerDamage)
    {
        if (_health == null || _health.IsDead) return 1f;

        var incoming = ElementalStatusDefs.GetStatus(element);
        float reactionMult = 1f;

        // Check for reactions with existing statuses
        ElementalStatusType? consumed = null;
        foreach (var kvp in _timers)
        {
            var existing = kvp.Key;

            var reaction = ElementalStatusDefs.GetReaction(existing, incoming);
            if (reaction.HasValue)
            {
                reactionMult = reaction.Value.damageMultiplier;
                ExecuteReaction(reaction.Value, triggerDamage);
                consumed = existing;
                break; // one reaction per hit
            }
        }

        // Remove consumed status
        if (consumed.HasValue)
            RemoveStatus(consumed.Value);

        // Apply / refresh the incoming status
        ApplyStatus(incoming);

        // Elemental Mastery: bonus damage on reactions
        if (reactionMult > 1f)
            reactionMult *= MetaProgression.ReactionDamageBonus;

        return reactionMult;
    }

    public bool HasStatus(ElementalStatusType type) => _timers.ContainsKey(type);

    public float GetSpeedMultiplier()
    {
        float lowest = 1f;
        foreach (var kvp in _timers)
        {
            var p = ElementalStatusDefs.GetParams(kvp.Key);
            if (p.speedMultiplier < lowest)
                lowest = p.speedMultiplier;
        }
        return lowest;
    }

    public float GetDamageAmplifier()
    {
        float highest = 1f;
        foreach (var kvp in _timers)
        {
            var p = ElementalStatusDefs.GetParams(kvp.Key);
            if (p.damageAmplifier > highest)
                highest = p.damageAmplifier;
        }
        return highest;
    }

    public void ClearAll()
    {
        var keys = new List<ElementalStatusType>(_timers.Keys);
        foreach (var k in keys)
            RemoveStatus(k);
        _dotAccumulator = 0f;
    }

    // ── Internals ───────────────────────────────────────────

    void ApplyStatus(ElementalStatusType type)
    {
        var p = ElementalStatusDefs.GetParams(type);
        bool isNew = !_timers.ContainsKey(type);
        _timers[type] = p.duration; // refresh timer

        if (isNew)
            OnStatusChanged?.Invoke(type, true);
    }

    void RemoveStatus(ElementalStatusType type)
    {
        if (!_timers.ContainsKey(type)) return;
        _timers.Remove(type);
        OnStatusChanged?.Invoke(type, false);
    }

    void Update()
    {
        if (_health == null || _health.IsDead) return;

        var expired = new List<ElementalStatusType>();
        var snapshot = new List<KeyValuePair<ElementalStatusType, float>>(_timers);
        float totalDot = 0f;

        foreach (var kvp in snapshot)
        {
            float remaining = kvp.Value - Time.deltaTime;
            if (remaining <= 0f)
            {
                expired.Add(kvp.Key);
            }
            else
            {
                _timers[kvp.Key] = remaining;

                // Accumulate DoT
                var p = ElementalStatusDefs.GetParams(kvp.Key);
                if (p.dotPerSecond > 0f)
                    totalDot += p.dotPerSecond;
            }
        }

        // Remove expired
        foreach (var e in expired)
            RemoveStatus(e);

        // Apply accumulated DoT as integer ticks
        if (totalDot > 0f)
        {
            _dotAccumulator += totalDot * Time.deltaTime;
            if (_dotAccumulator >= 1f)
            {
                int ticks = Mathf.FloorToInt(_dotAccumulator);
                _dotAccumulator -= ticks;
                _health.TakeDamage(ticks);
            }
        }
    }

    // ── Reaction execution ──────────────────────────────────

    void ExecuteReaction(ReactionResult reaction, float triggerDamage)
    {
        Vector3 pos = transform.position;
        float reactionDmg = triggerDamage * reaction.damageMultiplier;

        switch (reaction.type)
        {
            case ReactionType.Vaporize:
                // Big damage to this target, spawn small steam zone at enemy position
                SteamSpell.SpawnZone(pos, 2f, 1.5f, 2f, false);
                break;

            case ReactionType.Electrocute:
                // Chain damage to all Wet enemies in radius
                ChainToStatusEnemies(pos, reaction.aoeRadius, ElementalStatusType.Wet, reactionDmg * 0.5f);
                break;

            case ReactionType.Shatter:
                // Instant freeze
                _health.ApplyFreeze(2f);
                break;

            case ReactionType.Extinguish:
                // Steam burst AoE
                AoEDamage(pos, reaction.aoeRadius, reactionDmg * 0.4f);
                SteamSpell.SpawnZone(pos, 1.5f, 1f, 1.5f, false);
                break;

            case ReactionType.ToxicExplosion:
                // Big AoE burst
                AoEDamage(pos, reaction.aoeRadius, reactionDmg * 0.5f);
                break;

            case ReactionType.Firestorm:
                // Spread Burning to nearby enemies
                SpreadStatus(pos, reaction.aoeRadius, ElementalStatusType.Burning);
                break;

            case ReactionType.Detonate:
                // Explosion AoE
                AoEDamage(pos, reaction.aoeRadius, reactionDmg * 0.5f);
                break;

            case ReactionType.PlagueSpark:
                // Chain Poisoned status to nearby enemies
                SpreadStatus(pos, reaction.aoeRadius, ElementalStatusType.Poisoned);
                break;

            case ReactionType.ShortCircuit:
                // AoE stun
                AoEStun(pos, reaction.aoeRadius, 2f);
                break;

            case ReactionType.Overload:
                // Big knockback
                GameFeel.ApplyKnockback(transform, pos - transform.forward, 8f);
                break;

            case ReactionType.Shockwave:
                // AoE ground tremor
                AoEDamage(pos, reaction.aoeRadius, reactionDmg * 0.4f);
                AoEStun(pos, reaction.aoeRadius, 1f);
                break;

            case ReactionType.Launch:
                // Upward launch + stun on landing
                var kbUp = gameObject.AddComponent<KnockbackEffect>();
                kbUp.Init(Vector3.up * 3f, 0.4f);
                _health.ApplyStun(0.6f);
                break;

            case ReactionType.Exploit:
                // Damage multiplier already applied via reactionMult
                break;
        }

        // VFX: explosion sphere
        var vfx = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        UnityEngine.Object.Destroy(vfx.GetComponent<SphereCollider>());
        vfx.transform.position = pos + Vector3.up * 0.5f;
        float vfxRadius = reaction.aoeRadius > 0 ? reaction.aoeRadius : 1.5f;
        vfx.transform.localScale = Vector3.one * vfxRadius * 0.8f;
        vfx.GetComponent<Renderer>().material = ShaderCache.NewEmissive(reaction.color, 4f);
        vfx.AddComponent<FlashShrink>().Init(0.2f);

        // Game feel
        if (TopDownCamera.Instance != null) TopDownCamera.Instance.AddTrauma(0.25f);
        if (GameFeel.Instance != null) GameFeel.Instance.Hitstop(0.04f);
        SFXSystem.Play(SFXSystem.SFXType.Reaction, pos);

        // Fire event for HUD / Codex
        OnReaction?.Invoke(reaction.displayName, pos, reaction.color);
    }

    // ── Helpers ──────────────────────────────────────────────

    void AoEDamage(Vector3 center, float radius, float damage)
    {
        var hits = Physics.OverlapSphere(center, radius);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            if (h.gameObject == gameObject) continue; // skip self
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
                hp.TakeDamage(damage);
        }
    }

    void AoEStun(Vector3 center, float radius, float duration)
    {
        var hits = Physics.OverlapSphere(center, radius);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
                hp.ApplyStun(duration);
        }
    }

    void ChainToStatusEnemies(Vector3 center, float radius, ElementalStatusType requiredStatus, float damage)
    {
        var hits = Physics.OverlapSphere(center, radius);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            if (h.gameObject == gameObject) continue;
            var es = h.GetComponent<ElementalStatus>();
            if (es != null && es.HasStatus(requiredStatus))
            {
                var hp = h.GetComponent<Health>();
                if (hp != null && !hp.IsDead)
                    hp.TakeDamage(damage);
            }
        }
    }

    void SpreadStatus(Vector3 center, float radius, ElementalStatusType status)
    {
        var hits = Physics.OverlapSphere(center, radius);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            if (h.gameObject == gameObject) continue;
            var es = h.GetComponent<ElementalStatus>();
            if (es != null)
                es.ApplyStatus(status);
        }
    }
}
