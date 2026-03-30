using UnityEngine;

/// <summary>
/// Executes combo spell effects based on ComboType.
/// All spells fire toward cursor position.
/// </summary>
public static class ComboSpellFactory
{
    /// <summary>The element of the spell currently being cast. Set during Cast(), read by individual spells.</summary>
    public static ElementType? CurrentCastElement;

    public static void Cast(ComboSpellDef def, Vector3 origin, Vector3 targetPos, float damageMult, bool charged)
    {
        CurrentCastElement = ElementalStatusDefs.GetPrimaryElement(def.comboType);
        float dmg = def.baseDamage * damageMult;
        float radius = def.radius;

        if (charged)
        {
            dmg *= SpellMutationSystem.ModifyChargedMultiplier(def.chargedDamageMultiplier);
            radius *= def.chargedRadiusMultiplier;
        }

        // Apply spell mutations
        dmg = SpellMutationSystem.ModifyDamage(dmg);
        radius = SpellMutationSystem.ModifyRadius(radius);
        float duration = SpellMutationSystem.ModifyDuration(def.duration);

        // Soft cap: diminishing returns on total multiplier (covers charged + mutations)
        dmg = SoftCapDamage(dmg, def.baseDamage);

        Vector3 dir = (targetPos - origin);
        dir.y = 0;
        if (dir.sqrMagnitude > 0.01f) dir.Normalize();
        else dir = Vector3.forward;

        switch (def.comboType)
        {
            // ── Base element combos ──
            case ComboType.Inferno:
                InfernoSpell.Cast(targetPos, dmg, radius, charged);
                break;
            case ComboType.DeepFreeze:
                DeepFreezeSpell.Cast(targetPos, dmg, radius, duration, charged);
                break;
            case ComboType.Bulwark:
                BulwarkSpell.Cast(origin, dir, duration, charged);
                break;
            case ComboType.Ascend:
                AscendSpell.Cast(origin, dir, charged);
                break;

            // ── Cross element combos ──
            case ComboType.Steam:
                SteamSpell.Cast(targetPos, dmg, radius, duration, charged);
                break;
            case ComboType.Magma:
                MagmaSpell.Cast(targetPos, dmg, radius, duration, charged);
                break;
            case ComboType.Wildfire:
                WildfireSpell.Cast(targetPos, dmg, radius, charged);
                break;
            case ComboType.Permafrost:
                PermafrostSpell.Cast(targetPos, dmg, radius, duration, charged);
                break;
            case ComboType.Geyser:
                GeyserSpell.Cast(targetPos, dmg, radius, charged);
                break;
            case ComboType.Rubble:
                RubbleSpell.Cast(targetPos, dmg, radius, duration, charged);
                break;

            // ── Lightning combos ──
            case ComboType.LightningStrike:
                LightningStrikeSpell.Cast(targetPos, dmg, radius, charged);
                break;
            case ComboType.Thunderstorm:
                ThunderstormSpell.Cast(targetPos, dmg, radius, duration, charged);
                break;
            case ComboType.FrostShock:
                FrostShockSpell.Cast(targetPos, dmg, radius, duration, charged);
                break;
            case ComboType.Earthquake:
                EarthquakeSpell.Cast(targetPos, dmg, radius, duration, charged);
                break;
            case ComboType.Cyclone:
                CycloneSpell.Cast(targetPos, dmg, radius, duration, charged);
                break;

            // ── Poison combos ──
            case ComboType.Plague:
                PlagueSpell.Cast(targetPos, dmg, radius, duration, charged);
                break;
            case ComboType.Detonate:
                DetonateSpell.Cast(targetPos, dmg, radius, charged);
                break;
            case ComboType.ToxicFrost:
                ToxicFrostSpell.Cast(targetPos, dmg, radius, duration, charged);
                break;
            case ComboType.Quicksand:
                QuicksandSpell.Cast(targetPos, dmg, radius, duration, charged);
                break;
            case ComboType.Miasma:
                MiasmaSpell.Cast(targetPos, dmg, radius, duration, charged);
                break;
            case ComboType.PlagueSpark:
                PlagueSparkSpell.Cast(targetPos, dmg, radius, charged);
                break;

            // ── Void combos ──
            case ComboType.Collapse:
                CollapseSpell.Cast(targetPos, dmg, radius, charged);
                break;
            case ComboType.Implode:
                ImplodeSpell.Cast(targetPos, dmg, radius, charged);
                break;
            case ComboType.Rift:
                RiftSpell.Cast(targetPos, dmg, radius, duration, charged);
                break;
            case ComboType.Gravity:
                GravitySpell.Cast(targetPos, dmg, radius, charged);
                break;
            case ComboType.Vacuum:
                VacuumSpell.Cast(targetPos, dmg, radius, duration, charged);
                break;
            case ComboType.RiftShock:
                RiftShockSpell.Cast(targetPos, dmg, radius, charged);
                break;
            case ComboType.Corruption:
                CorruptionSpell.Cast(targetPos, dmg, radius, duration, charged);
                break;

            // Fallback
            default:
                GenericComboSpell.Cast(def, targetPos, dmg, radius, charged);
                break;
        }

        // ── Behavior upgrade hooks ──

        // Afterburn: leave a burning DoT zone at impact
        if (RunUpgradeSystem.HasAfterburn)
            RunUpgradeSystem.SpawnAfterburnZone(targetPos);

        // Ricochet: bounce damage to 1 nearby enemy
        if (RunUpgradeSystem.HasRicochet)
        {
            // Find the primary hit target to exclude from bounce
            var primary = Physics.OverlapSphere(targetPos, radius > 0 ? radius : 1f);
            GameObject exclude = null;
            float closestDist = float.MaxValue;
            foreach (var h in primary)
            {
                if (h.GetComponent<PlayerController>() != null) continue;
                var hp = h.GetComponent<Health>();
                if (hp == null || hp.IsDead) continue;
                float d = (h.transform.position - targetPos).sqrMagnitude;
                if (d < closestDist) { closestDist = d; exclude = h.gameObject; }
            }
            RunUpgradeSystem.BounceToNearestEnemy(targetPos, dmg * 0.5f, 5f, exclude);
        }
    }

    /// <summary>
    /// Soft cap on total damage multiplier. Computes effective mult (totalDmg / baseDmg),
    /// applies sqrt compression above threshold 4.0, then reconstructs final damage.
    /// </summary>
    static float SoftCapDamage(float totalDmg, float baseDmg)
    {
        if (baseDmg <= 0) return totalDmg;
        float mult = totalDmg / baseDmg;
        if (mult <= 3.5f) return totalDmg;
        float capped = 3.5f + Mathf.Sqrt(mult - 3.5f) * 0.7f;
        return baseDmg * capped;
    }
}
