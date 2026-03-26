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

            // Advanced combos — use generic implementations for now
            default:
                GenericComboSpell.Cast(def, targetPos, dmg, radius, charged);
                break;
        }
    }
}
