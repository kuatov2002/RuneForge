using UnityEngine;

public class LichBoss : MonoBehaviour
{
    public float teleportCooldown = 3.5f;
    public float teleportImmuneDuration = 0.5f;
    public float orbFireRate = 1.5f;
    public float orbSpeed = 5f;
    public int orbDamage = 2;
    public float orbHomingTurn = 90f;
    public float poisonZoneRadius = 2f;
    public float poisonZoneDuration = 4f;
    public float poisonDPS = 1f;
    public Color baseColor = new Color(0.3f, 0.1f, 0.5f);

    Transform target;
    Health health;
    Rigidbody rb;
    Renderer[] renderers;
    bool isDead;
    float teleportTimer;
    float orbTimer;
    bool isTeleporting;
    float teleportImmuneTimer;
    bool phase2;

    void Start()
    {
        var player = FindAnyObjectByType<PlayerController>();
        if (player != null) target = player.transform;
        health = GetComponent<Health>();
        rb = GetComponent<Rigidbody>();
        renderers = GetComponentsInChildren<Renderer>();
        teleportTimer = teleportCooldown;
        orbTimer = 1f;
        if (health != null)
        {
            health.OnDeath += () => { isDead = true; Destroy(gameObject, 0.5f); };
            health.OnHPChanged += (cur, max) => { if (cur < max / 2) phase2 = true; };
        }
    }

    void Update()
    {
        if (isDead || target == null) return;
        if (health.IsStunned) return;
        float speedMult = health.SpeedMultiplier;

        // Face player
        Vector3 toPlayer = target.position - transform.position;
        toPlayer.y = 0;
        if (toPlayer.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(toPlayer.normalized);

        // Teleport immune window
        if (isTeleporting)
        {
            teleportImmuneTimer -= Time.deltaTime;
            if (teleportImmuneTimer <= 0)
                isTeleporting = false;
            return; // Can't act while teleporting
        }

        // Teleport
        teleportTimer -= Time.deltaTime;
        float tpCD = phase2 ? teleportCooldown * 0.7f : teleportCooldown;
        if (teleportTimer <= 0)
        {
            teleportTimer = tpCD;
            DoTeleport();
        }

        // Fire homing void orbs
        orbTimer -= Time.deltaTime;
        float fireCD = phase2 ? orbFireRate * 0.6f : orbFireRate;
        if (orbTimer <= 0)
        {
            orbTimer = fireCD;
            FireVoidOrb();
        }
    }

    void DoTeleport()
    {
        Vector3 oldPos = transform.position;

        // Teleport to random position around player
        Vector2 circle = Random.insideUnitCircle.normalized * Random.Range(4f, 8f);
        Vector3 newPos = target.position + new Vector3(circle.x, 0, circle.y);
        newPos.y = 0;
        transform.position = newPos;

        isTeleporting = true;
        teleportImmuneTimer = teleportImmuneDuration;

        // VFX at old position
        CreateTeleportVFX(oldPos);

        // Phase 2: leave poison zone at old position
        if (phase2)
            CreatePoisonZone(oldPos);
    }

    void FireVoidOrb()
    {
        Vector3 dir = (target.position + Vector3.up * 0.5f - transform.position).normalized;

        var orb = new GameObject("VoidOrb");
        var sc = orb.AddComponent<SphereCollider>();
        sc.radius = 0.2f;
        sc.isTrigger = true;

        var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(body.GetComponent<SphereCollider>());
        body.transform.parent = orb.transform;
        body.transform.localPosition = Vector3.zero;
        body.transform.localScale = Vector3.one * 0.35f;
        var litShader = Shader.Find("Universal Render Pipeline/Lit");
        var mat = new Material(litShader);
        mat.color = new Color(0.4f, 0f, 0.6f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(0.6f, 0f, 1f) * 3f);
        body.GetComponent<Renderer>().material = mat;

        orb.transform.position = transform.position + Vector3.up * 1f;
        var vo = orb.AddComponent<VoidOrbProjectile>();
        vo.Setup(dir, orbSpeed, orbDamage, orbHomingTurn, target);

        // Trail
        var trail = orb.AddComponent<TrailRenderer>();
        trail.startWidth = 0.15f;
        trail.endWidth = 0f;
        trail.time = 0.3f;
        var tMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        tMat.color = new Color(0.5f, 0f, 0.8f);
        trail.material = tMat;
        trail.startColor = new Color(0.5f, 0f, 0.8f);
        Color endCol = new Color(0.5f, 0f, 0.8f, 0f);
        trail.endColor = endCol;

        Destroy(orb, 6f);
    }

    void CreateTeleportVFX(Vector3 pos)
    {
        var vfx = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(vfx.GetComponent<SphereCollider>());
        vfx.transform.position = pos + Vector3.up * 0.5f;
        vfx.transform.localScale = Vector3.one * 1.5f;
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(0.3f, 0f, 0.5f, 0.5f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(0.5f, 0f, 1f) * 2f);
        vfx.GetComponent<Renderer>().material = mat;
        Destroy(vfx, 0.3f);
    }

    void CreatePoisonZone(Vector3 pos)
    {
        var zone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(zone.GetComponent<CapsuleCollider>());
        zone.name = "PoisonZone";
        zone.transform.position = pos + Vector3.up * 0.05f;
        zone.transform.localScale = new Vector3(poisonZoneRadius * 2, 0.05f, poisonZoneRadius * 2);
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(0.1f, 0.6f, 0.1f, 0.6f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(0.1f, 0.8f, 0.1f) * 1.5f);
        zone.GetComponent<Renderer>().material = mat;

        var pz = zone.AddComponent<PoisonZone>();
        pz.dps = poisonDPS;
        pz.radius = poisonZoneRadius;

        Destroy(zone, poisonZoneDuration);
    }

    public bool IsImmune => isTeleporting;
}

// Homing void orb projectile
public class VoidOrbProjectile : MonoBehaviour
{
    Vector3 direction;
    float speed;
    float damage;
    float homingTurn;
    Transform target;

    public void Setup(Vector3 dir, float spd, float dmg, float turn, Transform tgt)
    {
        direction = dir.normalized;
        speed = spd;
        damage = dmg;
        homingTurn = turn;
        target = tgt;
    }

    void Update()
    {
        if (target != null)
        {
            Vector3 toTarget = (target.position + Vector3.up * 0.5f - transform.position).normalized;
            direction = Vector3.RotateTowards(direction, toTarget, homingTurn * Mathf.Deg2Rad * Time.deltaTime, 0f).normalized;
        }
        transform.position += direction * speed * Time.deltaTime;
    }

    void OnTriggerEnter(Collider other)
    {
        var pc = other.GetComponent<PlayerController>();
        if (pc == null) return;
        if (pc.isInvulnerable) return;
        var hp = other.GetComponent<Health>();
        if (hp != null && !hp.IsDead) hp.TakeDamage(damage);
        Destroy(gameObject);
    }
}

// Poison zone that damages player standing in it
public class PoisonZone : MonoBehaviour
{
    public float dps;
    public float radius;
    float tickTimer;

    void Update()
    {
        tickTimer -= Time.deltaTime;
        if (tickTimer > 0) return;
        tickTimer = 0.5f; // Tick every 0.5s

        var player = FindAnyObjectByType<PlayerController>();
        if (player == null) return;
        float dist = Vector3.Distance(transform.position, player.transform.position);
        if (dist > radius) return;
        if (player.isInvulnerable) return;
        var hp = player.GetComponent<Health>();
        if (hp != null && !hp.IsDead)
            hp.TakeDamage(dps * 0.5f); // 0.5s tick
    }
}
