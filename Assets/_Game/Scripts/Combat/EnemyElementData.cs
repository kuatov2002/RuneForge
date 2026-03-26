using UnityEngine;

/// <summary>
/// Attached to enemies to define elemental weakness and immunity.
/// Regular enemies: random weakness (~50% chance).
/// Bosses/mini-bosses: 1-2 immunities.
/// </summary>
public class EnemyElementData : MonoBehaviour
{
    public ElementType? weakness;
    public ElementType? immunity;
    public ElementType? immunity2; // second immunity for bosses

    public const float WeaknessBonusDamage = 1.5f;

    GameObject weaknessIndicator;

    /// <summary>Assign random weakness to a regular enemy.</summary>
    public void AssignRandomWeakness(ElementSO[] availableElements)
    {
        if (availableElements == null || availableElements.Length == 0) return;

        // 50% chance to have a weakness
        if (Random.value < 0.5f)
        {
            var elem = availableElements[Random.Range(0, availableElements.Length)];
            weakness = elem.elementType;
            CreateWeaknessIndicator(elem.color);
        }
    }

    /// <summary>Assign immunities to a boss.</summary>
    public void AssignBossImmunity(ElementType imm1)
    {
        immunity = imm1;
    }

    public void AssignBossImmunity(ElementType imm1, ElementType imm2)
    {
        immunity = imm1;
        immunity2 = imm2;
    }

    /// <summary>Modify damage based on weakness/immunity. Returns 0 for immune.</summary>
    public float ModifyDamage(float baseDamage, ElementType attackElement)
    {
        if (immunity.HasValue && immunity.Value == attackElement) return 0f;
        if (immunity2.HasValue && immunity2.Value == attackElement) return 0f;

        if (weakness.HasValue && weakness.Value == attackElement)
            return baseDamage * WeaknessBonusDamage;

        return baseDamage;
    }

    /// <summary>Check if this enemy is immune to the given element.</summary>
    public bool IsImmuneToElement(ElementType element)
    {
        if (immunity.HasValue && immunity.Value == element) return true;
        if (immunity2.HasValue && immunity2.Value == element) return true;
        return false;
    }

    void CreateWeaknessIndicator(Color color)
    {
        if (weaknessIndicator != null) return;

        weaknessIndicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(weaknessIndicator.GetComponent<SphereCollider>());
        weaknessIndicator.name = "WeaknessIcon";
        weaknessIndicator.transform.parent = transform;
        weaknessIndicator.transform.localPosition = new Vector3(0.4f, 1.5f, 0);
        weaknessIndicator.transform.localScale = Vector3.one * 0.12f;
        weaknessIndicator.GetComponent<Renderer>().material = ShaderCache.NewEmissive(color, 3f);
    }

    void OnDestroy()
    {
        if (weaknessIndicator != null) Destroy(weaknessIndicator);
    }
}
