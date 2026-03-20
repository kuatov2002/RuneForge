using UnityEngine;

public enum ModifierType { None, Split }

[CreateAssetMenu(menuName = "RuneForge/Modifier")]
public class ModifierSO : ScriptableObject
{
    public string modifierName;
    public ModifierType modifierType;
    public float damageMultiplier = 1f;
    public int splitCount = 3;
    public float splitSpreadAngle = 15f;
}
