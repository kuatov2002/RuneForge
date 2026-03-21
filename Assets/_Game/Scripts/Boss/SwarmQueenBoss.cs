using UnityEngine;
using System.Collections.Generic;
using Object = UnityEngine.Object;

public class SwarmQueenBoss : MonoBehaviour
{
    public float moveSpeed = 1.5f;
    public float teleportCooldown = 6f;
    public float spawnCooldown = 3f;
    public int spawnCount = 3;
    public Color baseColor = new Color(0.7f, 0.5f, 0.1f);

    Transform target;
    Health health;
    Rigidbody rb;
    Renderer[] renderers;
    bool isDead;
    float teleportTimer;
    float spawnTimer;
    bool phase2;
    List<GameObject> minions = new();

    void Start()
    {
        var player = FindAnyObjectByType<PlayerController>();
        if (player != null) target = player.transform;
        health = GetComponent<Health>();
        rb = GetComponent<Rigidbody>();
        renderers = GetComponentsInChildren<Renderer>();
        if (health != null)
        {
            health.OnDeath += () => { isDead = true; CleanupMinions(); Destroy(gameObject, 0.5f); };
            health.OnHPChanged += (cur, max) => { if (cur < max / 2) phase2 = true; };
        }
    }

    void Update()
    {
        if (isDead || target == null) return;
        if (health.IsStunned) return;
        float speedMult = health.SpeedMultiplier;

        // Slow movement away from player
        Vector3 toPlayer = target.position - transform.position;
        toPlayer.y = 0;
        if (toPlayer.magnitude < 4f)
        {
            rb.MovePosition(transform.position - toPlayer.normalized * moveSpeed * speedMult * Time.deltaTime);
        }
        transform.rotation = Quaternion.LookRotation(toPlayer.normalized);

        // Teleport
        teleportTimer -= Time.deltaTime;
        if (teleportTimer <= 0)
        {
            teleportTimer = teleportCooldown;
            Vector3 newPos = new Vector3(Random.Range(2f, 10f), 0, Random.Range(2f, 10f));
            transform.position = newPos;
        }

        // Spawn minions
        spawnTimer -= Time.deltaTime;
        float cd = phase2 ? spawnCooldown * 0.6f : spawnCooldown;
        if (spawnTimer <= 0)
        {
            spawnTimer = cd;
            SpawnMinions();
        }

        // Cleanup dead minions
        minions.RemoveAll(m => m == null);
    }

    void SpawnMinions()
    {
        int count = phase2 ? spawnCount + 2 : spawnCount;
        for (int i = 0; i < count; i++)
        {
            var minion = new GameObject("QueenMinion");
            var sc = minion.AddComponent<SphereCollider>();
            sc.radius = 0.2f; sc.center = new Vector3(0, 0.2f, 0);
            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "Body"; Object.Destroy(body.GetComponent<SphereCollider>());
            body.transform.parent = minion.transform;
            body.transform.localPosition = new Vector3(0, 0.2f, 0);
            body.transform.localScale = Vector3.one * 0.2f;
            var mat = ShaderCache.NewLit(baseColor);
            body.GetComponent<Renderer>().material = mat;

            Vector3 offset = Random.insideUnitSphere * 2f; offset.y = 0;
            minion.transform.position = transform.position + offset;

            var mrb = minion.AddComponent<Rigidbody>(); mrb.useGravity = false; mrb.isKinematic = true;
            var mhp = minion.AddComponent<Health>(); mhp.maxHP = 3; mhp.currentHP = 3;
            minion.AddComponent<EnemyHealthBar>();
            var ai = minion.AddComponent<SwarmAI>(); ai.baseColor = baseColor;

            // Register with Bootstrap
            var bootstrap = FindAnyObjectByType<Bootstrap>();
            if (bootstrap != null)
            {
                var enemyRef = minion;
                mhp.OnDeath += () => { if (bootstrap != null) bootstrap.OnMinionDeath(enemyRef); };
            }

            minions.Add(minion);
        }
    }

    void CleanupMinions()
    {
        foreach (var m in minions) if (m != null) Destroy(m);
        minions.Clear();
    }

    void OnDestroy() { CleanupMinions(); }
}
