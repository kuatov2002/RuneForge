using UnityEngine;

public enum ElementType { Fire, Water, Earth, Air, Lightning, Poison, Void }

[CreateAssetMenu(menuName = "RuneForge/Element")]
public class ElementSO : ScriptableObject
{
    public string elementName;
    public ElementType elementType;
    public int baseDamage = 10;
    public Color color = Color.red;

    /// <summary>Max charges before overheat.</summary>
    public int maxCharges = 4;

    /// <summary>Time in seconds to fully recharge after overheat.</summary>
    public float overheatRechargeTime = 5f;

    /// <summary>Whether this element is a base element (unlocked from start).</summary>
    public bool IsBase => elementType <= ElementType.Air;
}
