using UnityEngine;
using System.Collections.Generic;

public class WardenBoss : MonoBehaviour
{
    public float moveSpeed = 2f;
    public float slamRange = 2f;
    public float slamCooldown = 3f;
    public int slamDamage = 3;
    public float wallSpawnCooldown = 5f;
    public Color baseColor = new Color(0.4f, 0.4f, 0.35f);

    Transform target;
    Health health;
    Rigidbody rb;
    Renderer[] renderers;
    bool isDead;
    float slamTimer;
    float wallTimer;
    List<GameObject> walls = new();
    bool phase2;
    bool slamWindup;
    float slamWindupTimer;
    const float SlamWindupDuration = 0.6f;
    GameObject slamTelegraph;

    void Start()
    {
        var player = FindAnyObjectByType<PlayerController>();
        if (player != null) target = player.transform;
        health = GetComponent<Health>();
        rb = GetComponent<Rigidbody>();
        renderers = GetComponentsInChildren<Renderer>();

        // Boss immunity: Earth (stone/earth creature)
        var elemData = gameObject.GetComponent<EnemyElementData>();
        if (elemData == null) elemData = gameObject.AddComponent<EnemyElementData>();
        elemData.AssignBossImmunity(ElementType.Earth);

        if (health != null)
        {
            health.OnDeath += () => { isDead = true; CleanupWalls(); Destroy(gameObject, 0.5f); };
            health.OnHPChanged += (cur, max) => { if (cur < max / 2) phase2 = true; };
        }
    }

    void Update()
    {
        if (isDead || target == null) return;
        if (health.IsStunned) return;
        float speedMult = health.SpeedMultiplier;

        Vector3 toPlayer = target.position - transform.position;
        toPlayer.y = 0;
        float dist = toPlayer.magnitude;

        // Slam wind-up (don't move during)
        if (slamWindup)
        {
            slamWindupTimer -= Time.deltaTime;
            // Flash white
            float flash = Mathf.PingPong(Time.time * 10f, 1f);
            foreach (var r in renderers)
                if (r != null && r.gameObject.name != "Eye")
                    r.material.color = Color.Lerp(baseColor, Color.white, flash);
            if (slamWindupTimer <= 0)
            {
                slamWindup = false;
                if (slamTelegraph != null) Destroy(slamTelegraph);
                DoSlam();
            }
            return;
        }

        // Move toward player
        if (dist > slamRange)
        {
            Vector3 dir = toPlayer.normalized;
            rb.MovePosition(transform.position + dir * moveSpeed * speedMult * Time.deltaTime);
            transform.rotation = Quaternion.LookRotation(dir);
        }

        // Slam attack
        slamTimer -= Time.deltaTime;
        if (dist <= slamRange && slamTimer <= 0)
        {
            slamTimer = slamCooldown;
            slamWindup = true;
            slamWindupTimer = SlamWindupDuration;
            // Telegraph: red circle on ground
            slamTelegraph = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(slamTelegraph.GetComponent<CapsuleCollider>());
            slamTelegraph.transform.position = transform.position + Vector3.up * 0.03f;
            slamTelegraph.transform.localScale = new Vector3(slamRange * 2, 0.03f, slamRange * 2);
            slamTelegraph.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(1f, 0.2f, 0.1f, 0.6f), 2f);
            Destroy(slamTelegraph, SlamWindupDuration + 0.1f);
        }

        // Spawn blocking walls
        wallTimer -= Time.deltaTime;
        float wallCD = phase2 ? wallSpawnCooldown * 0.5f : wallSpawnCooldown;
        if (wallTimer <= 0)
        {
            wallTimer = wallCD;
            SpawnWall();
        }
    }

    void DoSlam()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, slamRange);
        foreach (var h in hits)
        {
            var pc = h.GetComponent<PlayerController>();
            if (pc == null || pc.isInvulnerable) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead) hp.TakeDamage(slamDamage);
        }
        // VFX
        var vfx = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(vfx.GetComponent<CapsuleCollider>());
        vfx.transform.position = transform.position + Vector3.up * 0.05f;
        vfx.transform.localScale = new Vector3(slamRange * 2, 0.05f, slamRange * 2);
        var mat = ShaderCache.NewEmissive(new Color(0.5f, 0.4f, 0.2f), 2f);
        vfx.GetComponent<Renderer>().material = mat;
        Destroy(vfx, 0.2f);
    }

    void SpawnWall()
    {
        // Spawn a wall between boss and player
        Vector3 mid = (transform.position + target.position) * 0.5f;
        Vector3 perp = Vector3.Cross((target.position - transform.position).normalized, Vector3.up);

        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "WardenWall";
        wall.transform.position = mid + Vector3.up * 0.75f;
        wall.transform.localScale = new Vector3(3f, 1.5f, 0.4f);
        wall.transform.rotation = Quaternion.LookRotation(perp);
        wall.isStatic = false;
        var mat = ShaderCache.NewLit(new Color(0.4f, 0.35f, 0.25f));
        wall.GetComponent<Renderer>().material = mat;
        walls.Add(wall);
        Destroy(wall, 5f);
    }

    void CleanupWalls()
    {
        foreach (var w in walls) if (w != null) Destroy(w);
        walls.Clear();
    }

    void OnDestroy() { CleanupWalls(); }
}
