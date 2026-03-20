using UnityEngine;

public enum StatusEffectType { None, Burn, Slow, Chain, Poison, VoidMark }

[CreateAssetMenu(menuName = "RuneForge/Element")]
public class ElementSO : ScriptableObject
{
    public string elementName;
    public int baseDamage = 10;
    public StatusEffectType statusEffect;
    public float statusDPS;
    public float statusDuration;
    public Color color = Color.red;
    [Tooltip("For Chain (Lightning): max chain targets")]
    public int chainCount = 2;
    [Tooltip("For Chain (Lightning): chain radius")]
    public float chainRadius = 4f;
}
