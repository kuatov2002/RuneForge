using UnityEngine;

public class ShieldBearerAI : MonoBehaviour
{
    public float moveSpeed = 2f;
    public float attackRange = 1.5f;
    public float attackCooldown = 1.2f;
    public int attackDamage = 2;
    public Color baseColor = new Color(0.3f, 0.35f, 0.5f);

    Transform target;
    float attackTimer;
    Health health;
    Rigidbody rb;
    Renderer[] renderers;
    bool isDead;

    // Shield blocks damage from front
    GameObject shieldObj;

    void Start()
    {
        var player = FindAnyObjectByType<PlayerController>();
        if (player != null) target = player.transform;
        health = GetComponent<Health>();
        rb = GetComponent<Rigidbody>();
        renderers = GetComponentsInChildren<Renderer>();
        if (health != null) health.OnDeath += OnDie;

        // Find shield child
        var t = transform.Find("Shield");
        if (t != null) shieldObj = t.gameObject;
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

        Vector3 toPlayer = target.position - transform.position;
        toPlayer.y = 0;
        float dist = toPlayer.magnitude;

        // Always face player
        if (toPlayer.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(toPlayer.normalized);

        if (dist > attackRange)
        {
            rb.MovePosition(transform.position + toPlayer.normalized * moveSpeed * speedMult * Time.deltaTime);
        }
        else
        {
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0)
            {
                attackTimer = attackCooldown;
                var playerHealth = target.GetComponent<Health>();
                var playerCtrl = target.GetComponent<PlayerController>();
                if (playerHealth != null && !playerHealth.IsDead
                    && (playerCtrl == null || !playerCtrl.isInvulnerable))
                    playerHealth.TakeDamage(attackDamage);
            }
        }
    }

    // Called by SpellProjectile/ConeAttack etc. to check if hit is blocked
    public bool IsBlockedFromDirection(Vector3 attackOrigin)
    {
        Vector3 toAttacker = attackOrigin - transform.position;
        toAttacker.y = 0;
        float angle = Vector3.Angle(transform.forward, toAttacker);
        return angle < 45f; // 90 degree frontal arc (45 each side)
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
            if (r != null && r.gameObject.name != "Eye" && r.gameObject.name != "Shield")
                r.material.color = col;
    }

    void OnDestroy() { if (health != null) health.OnDeath -= OnDie; }
}
