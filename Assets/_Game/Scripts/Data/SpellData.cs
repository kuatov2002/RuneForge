[System.Serializable]
public class SpellData
{
    public ElementSO element;
    public FormSO form;
    public ModifierSO modifier;

    public bool IsComplete => element != null && form != null;

    public float CalculateDamage()
    {
        if (element == null) return 0;
        float dmg = element.baseDamage;
        if (modifier != null) dmg *= modifier.damageMultiplier;
        return dmg;
    }
}
