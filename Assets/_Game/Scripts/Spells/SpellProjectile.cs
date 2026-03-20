using UnityEngine;

public class SpellProjectile : MonoBehaviour
{
    Vector3 direction;
    float speed;
    float damage;
    ElementSO element;
    float maxDistance;
    Vector3 startPos;
    bool initialized;

    public void Setup(Vector3 dir, float spd, float dmg, ElementSO elem, float range)
    {
        direction = dir.normalized;
        speed = spd;
        damage = dmg;
        element = elem;
        maxDistance = range;
        startPos = transform.position;
        initialized = true;

        // Trail
        var trail = gameObject.AddComponent<TrailRenderer>();
        trail.startWidth = 0.2f;
        trail.endWidth = 0f;
        trail.time = 0.25f;
        var tMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        tMat.color = elem.color;
        trail.material = tMat;
        trail.startColor = elem.color;
        Color endCol = elem.color;
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
        if (other.GetComponent<OrbitForm>() != null) return;
        if (other.GetComponent<TrapForm>() != null) return;

        var health = other.GetComponent<Health>();
        if (health != null && !health.IsDead)
        {
            health.TakeDamage(damage);
            health.ApplyStatusEffect(element);

            // Chain lightning
            if (element.statusEffect == StatusEffectType.Chain)
                ChainToNearby(other.transform.position, other, element.chainCount, damage * 0.5f);
        }

        Destroy(gameObject);
    }

    void ChainToNearby(Vector3 hitPos, Collider originalTarget, int chainCount, float chainDamage)
    {
        Collider[] nearby = Physics.OverlapSphere(hitPos, element.chainRadius);
        int chained = 0;
        foreach (var col in nearby)
        {
            if (chained >= chainCount) break;
            if (col == originalTarget) continue;
            if (col.GetComponent<PlayerController>() != null) continue;
            var h = col.GetComponent<Health>();
            if (h == null || h.IsDead) continue;

            h.TakeDamage(chainDamage);
            h.ApplyStatusEffect(element);
            CreateChainArc(hitPos, col.transform.position);
            hitPos = col.transform.position;
            chained++;
        }
    }

    static void CreateChainArc(Vector3 from, Vector3 to)
    {
        var go = new GameObject("ChainArc");
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 4;
        lr.startWidth = 0.08f;
        lr.endWidth = 0.08f;
        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.color = new Color(1f, 1f, 0.3f);
        lr.material = mat;
        lr.startColor = new Color(1f, 1f, 0.3f);
        lr.endColor = new Color(0.5f, 0.5f, 1f);

        // Jagged lightning shape
        Vector3 mid = (from + to) * 0.5f;
        Vector3 perp = Vector3.Cross(to - from, Vector3.up).normalized;
        lr.SetPosition(0, from + Vector3.up * 0.5f);
        lr.SetPosition(1, Vector3.Lerp(from, mid, 0.5f) + perp * Random.Range(-0.4f, 0.4f) + Vector3.up * 0.5f);
        lr.SetPosition(2, Vector3.Lerp(mid, to, 0.5f) + perp * Random.Range(-0.4f, 0.4f) + Vector3.up * 0.5f);
        lr.SetPosition(3, to + Vector3.up * 0.5f);

        Destroy(go, 0.15f);
    }
}
