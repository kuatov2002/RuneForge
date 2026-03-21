[System.Serializable]
public class SpellData
{
    public ElementSO element;
    public FormSO form;
    public ModifierSO modifier;

    // Upgrade tiers: picking the same rune again upgrades it
    public int elementTier;  // +15% damage per tier
    public int formTier;     // -10% cooldown per tier
    public int modifierTier; // +20% modifier effect per tier

    public bool IsComplete => element != null && form != null;

    public float DamageTierMult => 1f + elementTier * 0.15f;
    public float CooldownTierMult => UnityEngine.Mathf.Max(0.4f, 1f - formTier * 0.1f);
    public float ModifierTierMult => 1f + modifierTier * 0.2f;

    public float CalculateDamage()
    {
        if (element == null) return 0;
        float dmg = element.baseDamage * DamageTierMult;
        if (modifier != null) dmg *= modifier.damageMultiplier;
        return dmg;
    }
}
