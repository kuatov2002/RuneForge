using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Simplified spell projectile for the new combo system.
/// Used by Wildfire chains and any combo that fires a projectile.
/// </summary>
public class SpellProjectile : MonoBehaviour
{
    Vector3 direction;
    float speed;
    float damage;
    ElementSO element;
    float maxDistance;
    Vector3 startPos;
    bool initialized;

    // Wildfire chain support
    [HideInInspector] public float wildfireChainRadius;
    [HideInInspector] public bool wildfireCharged;

    HashSet<Collider> hitTargets = new();

    public void Setup(Vector3 dir, float spd, float dmg, ElementSO elem, float range)
    {
        direction = dir.normalized;
        speed = spd;
        damage = dmg;
        element = elem;
        maxDistance = range;
        startPos = transform.position;
        initialized = true;

        Color col = elem != null ? elem.color : Color.white;

        // Make projectile body glow with element color
        var renderer = GetComponent<Renderer>();
        if (renderer != null)
            renderer.material = ShaderCache.NewEmissive(col, 3f);

        // Trail
        var trail = gameObject.AddComponent<TrailRenderer>();
        trail.startWidth = 0.2f;
        trail.endWidth = 0f;
        trail.time = 0.25f;
        var tMat = ShaderCache.NewLit(col);
        trail.material = tMat;
        trail.startColor = col;
        Color endCol = col;
        endCol.a = 0;
        trail.endColor = endCol;
    }

    void Update()
    {
        if (!initialized) return;
        transform.position += direction * speed * Time.deltaTime;
        if (Vector3.Distance(startPos, transform.position) > maxDistance)
            Destroy(gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!initialized) return;
        if (other.GetComponent<PlayerController>() != null) return;
        if (other.GetComponent<SpellProjectile>() != null) return;
        if (hitTargets.Contains(other)) return;

        // Check zone interactions (element-dependent)
        ElementType elemType = element != null ? element.elementType : ElementType.Fire;
        if (SpellInteractionSystem.TryInteraction(elemType, other, transform.position, damage))
        {
            Destroy(gameObject);
            return;
        }

        // Skip non-interactive trigger colliders (ground zones like magma, permafrost)
        if (other.isTrigger && other.GetComponent<Health>() == null) return;

        var health = other.GetComponent<Health>();
        if (health != null && !health.IsDead)
        {
            // Shield Bearer block check
            var shield = other.GetComponent<ShieldBearerAI>();
            if (shield != null && shield.IsBlockedFromDirection(startPos))
                return;

            // Mirror Knight shield check
            var mirrorKnight = other.GetComponent<MirrorKnightBoss>();
            if (mirrorKnight != null && mirrorKnight.ShouldBlockDamage())
                return;

            // Lich teleport immunity
            var lich = other.GetComponent<LichBoss>();
            if (lich != null && lich.IsImmune)
                return;

            health.TakeDamage(damage, element != null ? element.elementType : ElementType.Fire);
            hitTargets.Add(other);

            // Wildfire chain: on hit, trigger chain reaction
            if (wildfireChainRadius > 0)
            {
                WildfireSpell.Cast(other.transform.position, damage, wildfireChainRadius, wildfireCharged);
                Destroy(gameObject);
                return;
            }
        }

        Destroy(gameObject);
    }
}
