using UnityEngine;

public class TrapForm : MonoBehaviour
{
    float damage;
    ElementSO element;
    float radius;
    float armTimer;
    bool armed;
    bool triggered;
    float leechPercent;
    Health playerHealth;
    bool isVolatile;
    float volatileMiss;

    public void Init(float dmg, ElementSO elem, float rad, float armTime,
        float leech = 0f, Health playerHp = null, bool volatile_ = false, float missCh = 0f)
    {
        damage = dmg;
        element = elem;
        radius = rad;
        armTimer = armTime;
        leechPercent = leech;
        playerHealth = playerHp;
        isVolatile = volatile_;
        volatileMiss = missCh;
    }

    void Update()
    {
        if (triggered) return;
        if (!armed)
        {
            armTimer -= Time.deltaTime;
            if (armTimer <= 0) armed = true;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!armed || triggered) return;
        if (other.GetComponent<PlayerController>() != null) return;
        if (other.GetComponent<SpellProjectile>() != null) return;

        var health = other.GetComponent<Health>();
        if (health == null || health.IsDead) return;

        triggered = true;
        Explode();
    }

    void Explode()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, radius);
        foreach (var hit in hits)
        {
            if (hit.GetComponent<PlayerController>() != null) continue;
            var health = hit.GetComponent<Health>();
            if (health == null || health.IsDead) continue;
            if (isVolatile && Random.value < volatileMiss) continue;
            health.TakeDamage(damage);
            health.ApplyStatusEffect(element);
            if (leechPercent > 0 && playerHealth != null)
                playerHealth.Heal(Mathf.Max(1, Mathf.CeilToInt(damage * leechPercent)));
        }

        var explosion = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(explosion.GetComponent<SphereCollider>());
        explosion.transform.position = transform.position + Vector3.up * 0.2f;
        explosion.transform.localScale = Vector3.one * radius * 2f;
        var mat = ShaderCache.NewEmissive(element.color, 5f);
        explosion.GetComponent<Renderer>().material = mat;
        explosion.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Destroy(explosion, 0.15f);

        Destroy(gameObject, 0.05f);
    }

    public static GameObject Create(Vector3 position, float damage, ElementSO element, float radius, float armTime,
        float leech = 0f, Health playerHp = null, bool volatile_ = false, float missCh = 0f)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "Trap";
        go.transform.position = position + Vector3.up * 0.05f;
        go.transform.localScale = new Vector3(0.6f, 0.05f, 0.6f);

        var col = go.GetComponent<CapsuleCollider>();
        Destroy(col);
        var sphere = go.AddComponent<SphereCollider>();
        sphere.isTrigger = true;
        sphere.radius = radius / 0.6f;

        var rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;

        var mat = ShaderCache.NewEmissive(element.color * 0.7f, 2f);
        mat.SetColor("_EmissionColor", element.color * 2f);
        go.GetComponent<Renderer>().material = mat;

        var trap = go.AddComponent<TrapForm>();
        trap.Init(damage, element, radius, armTime, leech, playerHp, volatile_, missCh);

        Destroy(go, 15f);
        return go;
    }
}
