using UnityEngine;

public class OrbitForm : MonoBehaviour
{
    float orbitRadius;
    float orbitSpeed;
    int orbitCount;
    float damage;
    ElementSO element;
    float duration;
    float timer;
    GameObject[] orbs;
    bool active;

    public void Init(float radius, float speed, int count, float dmg, ElementSO elem, float dur,
        float sizeScale = 1f, int bounceCount = 0, bool homing = false,
        float leech = 0f, Health playerHp = null, bool volatile_ = false, float missCh = 0f)
    {
        orbitRadius = radius;
        orbitSpeed = speed;
        orbitCount = count;
        damage = dmg;
        element = elem;
        duration = dur;
        timer = duration;
        active = true;

        orbs = new GameObject[orbitCount];
        for (int i = 0; i < orbitCount; i++)
        {
            var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orb.name = "OrbitOrb";
            orb.transform.localScale = Vector3.one * 0.35f * sizeScale;

            var col = orb.GetComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 1.5f;

            var rb = orb.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = elem.color;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", elem.color * 3f);
            orb.GetComponent<Renderer>().material = mat;

            var dmgComp = orb.AddComponent<OrbitDamager>();
            dmgComp.damage = dmg;
            dmgComp.element = elem;
            dmgComp.leechPercent = leech;
            dmgComp.playerHealth = playerHp;
            dmgComp.isVolatile = volatile_;
            dmgComp.volatileMiss = missCh;

            var trail = orb.AddComponent<TrailRenderer>();
            trail.startWidth = 0.15f * sizeScale;
            trail.endWidth = 0f;
            trail.time = 0.2f;
            var tMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            tMat.color = elem.color;
            trail.material = tMat;
            trail.startColor = elem.color;
            Color endC = elem.color;
            endC.a = 0;
            trail.endColor = endC;

            orbs[i] = orb;
        }
    }

    public void Cleanup()
    {
        if (orbs != null)
            foreach (var orb in orbs)
                if (orb != null) Destroy(orb);
        active = false;
        Destroy(this);
    }

    void Update()
    {
        if (!active) return;
        timer -= Time.deltaTime;
        if (timer <= 0) { Cleanup(); return; }

        for (int i = 0; i < orbs.Length; i++)
        {
            if (orbs[i] == null) continue;
            float angle = Time.time * orbitSpeed + (360f / orbitCount) * i;
            float rad = angle * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(rad), 0.5f, Mathf.Sin(rad)) * orbitRadius;
            orbs[i].transform.position = transform.position + offset;
        }
    }

    void OnDestroy()
    {
        if (orbs != null)
            foreach (var orb in orbs)
                if (orb != null) Destroy(orb);
    }
}

public class OrbitDamager : MonoBehaviour
{
    public float damage;
    public ElementSO element;
    public float leechPercent;
    public Health playerHealth;
    public bool isVolatile;
    public float volatileMiss;
    float hitCooldown;

    void Update()
    {
        if (hitCooldown > 0) hitCooldown -= Time.deltaTime;
    }

    void OnTriggerEnter(Collider other)
    {
        if (hitCooldown > 0) return;
        if (other.GetComponent<PlayerController>() != null) return;
        if (other.GetComponent<OrbitDamager>() != null) return;
        if (other.GetComponent<SpellProjectile>() != null) return;

        var health = other.GetComponent<Health>();
        if (health == null || health.IsDead) return;

        if (isVolatile && Random.value < volatileMiss) return;

        health.TakeDamage(damage);
        health.ApplyStatusEffect(element);
        if (leechPercent > 0 && playerHealth != null)
            playerHealth.Heal(Mathf.Max(1, Mathf.CeilToInt(damage * leechPercent)));
        hitCooldown = 0.5f;
    }
}
