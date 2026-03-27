using UnityEngine;

/// <summary>
/// Lingering DoT zone spawned by the Afterburn run upgrade.
/// Deals periodic damage to enemies standing in the area, then fades.
/// </summary>
public class AfterburnZone : MonoBehaviour
{
    float dps;
    float radius;
    float lifetime;
    float elapsed;
    float tickTimer;
    const float TickInterval = 0.5f;

    Renderer rend;

    public void Init(float damagePerSecond, float zoneRadius, float duration)
    {
        dps = damagePerSecond;
        radius = zoneRadius;
        lifetime = duration;
        rend = GetComponent<Renderer>();
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        if (elapsed >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        // Fade out
        float alpha = 1f - (elapsed / lifetime);
        if (rend != null)
        {
            var c = rend.material.color;
            c.a = alpha * 0.6f;
            rend.material.color = c;
        }

        // Tick damage
        tickTimer += Time.deltaTime;
        if (tickTimer >= TickInterval)
        {
            tickTimer -= TickInterval;
            int dmg = Mathf.Max(1, Mathf.RoundToInt(dps * TickInterval));
            var hits = Physics.OverlapSphere(transform.position, radius);
            foreach (var h in hits)
            {
                if (h.GetComponent<PlayerController>() != null) continue;
                var hp = h.GetComponent<Health>();
                if (hp != null && !hp.IsDead)
                    hp.TakeDamage(dmg);
            }
        }
    }
}
