using UnityEngine;

/// <summary>
/// Executes combo spell effects based on ComboType.
/// All spells fire toward cursor position.
/// </summary>
public static class ComboSpellFactory
{
    public static void Cast(ComboSpellDef def, Vector3 origin, Vector3 targetPos, float damageMult, bool charged)
    {
        float dmg = def.baseDamage * damageMult;
        float radius = def.radius;

        if (charged)
        {
            dmg *= def.chargedDamageMultiplier;
            radius *= def.chargedRadiusMultiplier;
        }

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
                DeepFreezeSpell.Cast(targetPos, dmg, radius, def.duration, charged);
                break;
            case ComboType.Bulwark:
                BulwarkSpell.Cast(origin, dir, def.duration, charged);
                break;
            case ComboType.Ascend:
                AscendSpell.Cast(origin, dir, charged);
                break;

            // ── Cross element combos ──
            case ComboType.Steam:
                SteamSpell.Cast(targetPos, dmg, radius, def.duration, charged);
                break;
            case ComboType.Magma:
                MagmaSpell.Cast(targetPos, dmg, radius, def.duration, charged);
                break;
            case ComboType.Wildfire:
                WildfireSpell.Cast(targetPos, dmg, radius, charged);
                break;
            case ComboType.Permafrost:
                PermafrostSpell.Cast(targetPos, dmg, radius, def.duration, charged);
                break;
            case ComboType.Geyser:
                GeyserSpell.Cast(targetPos, dmg, radius, charged);
                break;
            case ComboType.Rubble:
                RubbleSpell.Cast(targetPos, dmg, radius, def.duration, charged);
                break;

            // ── Lightning combos ──
            case ComboType.LightningStrike:
                LightningStrikeSpell.Cast(targetPos, dmg, radius, charged);
                break;
            case ComboType.Thunderstorm:
                ThunderstormSpell.Cast(targetPos, dmg, radius, def.duration, charged);
                break;
            case ComboType.FrostShock:
                FrostShockSpell.Cast(targetPos, dmg, radius, def.duration, charged);
                break;
            case ComboType.Earthquake:
                EarthquakeSpell.Cast(targetPos, dmg, radius, def.duration, charged);
                break;
            case ComboType.Cyclone:
                CycloneSpell.Cast(targetPos, dmg, radius, def.duration, charged);
                break;

            // ── Poison combos ──
            case ComboType.Plague:
                PlagueSpell.Cast(targetPos, dmg, radius, def.duration, charged);
                break;
            case ComboType.Detonate:
                DetonateSpell.Cast(targetPos, dmg, radius, charged);
                break;
            case ComboType.ToxicFrost:
                ToxicFrostSpell.Cast(targetPos, dmg, radius, def.duration, charged);
                break;
            case ComboType.Quicksand:
                QuicksandSpell.Cast(targetPos, dmg, radius, def.duration, charged);
                break;
            case ComboType.Miasma:
                MiasmaSpell.Cast(targetPos, dmg, radius, def.duration, charged);
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
                RiftSpell.Cast(targetPos, dmg, radius, def.duration, charged);
                break;
            case ComboType.Gravity:
                GravitySpell.Cast(targetPos, dmg, radius, charged);
                break;
            case ComboType.Vacuum:
                VacuumSpell.Cast(targetPos, dmg, radius, def.duration, charged);
                break;
            case ComboType.RiftShock:
                RiftShockSpell.Cast(targetPos, dmg, radius, charged);
                break;
            case ComboType.Corruption:
                CorruptionSpell.Cast(targetPos, dmg, radius, def.duration, charged);
                break;

            // Fallback
            default:
                GenericComboSpell.Cast(def, targetPos, dmg, radius, charged);
                break;
        }
    }
}
