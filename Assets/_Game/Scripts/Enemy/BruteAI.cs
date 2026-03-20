using UnityEngine;

public class BruteAI : MonoBehaviour
{
    public float moveSpeed = 1.5f;
    public float chargeSpeed = 8f;
    public float chargeRange = 6f;
    public float chargeCooldown = 4f;
    public float slamRadius = 2f;
    public int attackDamage = 2;
    public Color baseColor = new Color(0.5f, 0.2f, 0.15f);

    Transform target;
    Health health;
    Rigidbody rb;
    Renderer[] renderers;
    bool isDead;

    float chargeTimer;
    bool isCharging;
    Vector3 chargeDir;
    float chargeDist;
    float chargeDistLeft;

    void Start()
    {
        var player = FindAnyObjectByType<PlayerController>();
        if (player != null) target = player.transform;
        health = GetComponent<Health>();
        rb = GetComponent<Rigidbody>();
        renderers = GetComponentsInChildren<Renderer>();
        if (health != null) health.OnDeath += OnDie;
    }

    void OnDie()
    {
        isDead = true;
        foreach (var r in renderers) if (r != null) r.material.color = Color.gray;
        Destroy(gameObject, 0.3f);
    }

    void Update()
    {
        if (isDead || target == null) return;
        UpdateStatusVisual();
        if (health.IsStunned) return;
        float speedMult = health.SpeedMultiplier;

        if (isCharging)
        {
            float step = chargeSpeed * speedMult * Time.deltaTime;
            rb.MovePosition(transform.position + chargeDir * step);
            chargeDistLeft -= step;
            if (chargeDistLeft <= 0)
            {
                isCharging = false;
                Slam();
            }
            return;
        }

        Vector3 toPlayer = target.position - transform.position;
        toPlayer.y = 0;
        float dist = toPlayer.magnitude;

        chargeTimer -= Time.deltaTime;

        // Charge if in range
        if (dist <= chargeRange && dist > 1.5f && chargeTimer <= 0)
        {
            chargeTimer = chargeCooldown;
            isCharging = true;
            chargeDir = toPlayer.normalized;
            chargeDistLeft = dist;
            transform.rotation = Quaternion.LookRotation(chargeDir);
            return;
        }

        // Normal walk
        if (dist > 1.5f)
        {
            Vector3 moveDir = toPlayer.normalized;
            rb.MovePosition(transform.position + moveDir * moveSpeed * speedMult * Time.deltaTime);
            transform.rotation = Quaternion.LookRotation(moveDir);
        }
    }

    void Slam()
    {
        // AoE damage around landing point
        Collider[] hits = Physics.OverlapSphere(transform.position, slamRadius);
        foreach (var hit in hits)
        {
            var pc = hit.GetComponent<PlayerController>();
            if (pc == null) continue;
            if (pc.isInvulnerable) continue;
            var hp = hit.GetComponent<Health>();
            if (hp != null && !hp.IsDead) hp.TakeDamage(attackDamage);
        }

        // Visual slam
        var vfx = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(vfx.GetComponent<CapsuleCollider>());
        vfx.transform.position = transform.position + Vector3.up * 0.05f;
        vfx.transform.localScale = new Vector3(slamRadius * 2, 0.05f, slamRadius * 2);
        var mat = ShaderCache.NewEmissive(new Color(0.6f, 0.3f, 0.1f), 2f);
        vfx.GetComponent<Renderer>().material = mat;
        Destroy(vfx, 0.2f);
    }

    void UpdateStatusVisual()
    {
        if (renderers == null) return;
        Color col = baseColor;
        if (health.IsStunned) col = health.IsFrozen ? new Color(0.6f, 0.9f, 1f) : new Color(1f, 1f, 0.3f);
        else if (health.IsSlowed) col = new Color(0.4f, 0.6f, 1f);
        else if (health.PoisonStacks > 0)
            col = Color.Lerp(baseColor, new Color(0.2f, 0.9f, 0.1f), health.PoisonStacks / 5f);
        foreach (var r in renderers)
            if (r != null && r.gameObject.name != "Eye") r.material.color = col;
    }

    void OnDestroy() { if (health != null) health.OnDeath -= OnDie; }
}
