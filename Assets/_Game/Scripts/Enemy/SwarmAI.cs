using UnityEngine;

public class SwarmAI : MonoBehaviour
{
    public float moveSpeed = 4f;
    public float attackRange = 0.8f;
    public float attackCooldown = 0.8f;
    public int attackDamage = 1;
    public Color baseColor = new Color(0.6f, 0.5f, 0.1f);

    Transform target;
    float attackTimer;
    Health health;
    Rigidbody rb;
    Renderer[] renderers;
    bool isDead;

    // Wind-up telegraph
    const float WindupDuration = 0.3f;
    float windupTimer;
    bool isWindingUp;
    Vector3 windupLockPos;

    // Slight randomness so swarm doesn't stack perfectly
    Vector3 offset;

    void Start()
    {
        var player = FindAnyObjectByType<PlayerController>();
        if (player != null) target = player.transform;
        health = GetComponent<Health>();
        rb = GetComponent<Rigidbody>();
        renderers = GetComponentsInChildren<Renderer>();
        if (health != null) health.OnDeath += OnDie;
        offset = new Vector3(Random.Range(-0.5f, 0.5f), 0, Random.Range(-0.5f, 0.5f));
    }

    void OnDie()
    {
        isDead = true;
        Destroy(gameObject, 0.1f);
    }

    void Update()
    {
        if (isDead || target == null) return;
        if (health.IsStunned) return;
        float speedMult = health.SpeedMultiplier;

        Vector3 targetPos = target.position + offset;
        Vector3 toPlayer = targetPos - transform.position;
        toPlayer.y = 0;
        float dist = toPlayer.magnitude;

        // Wind-up phase: flash and hold position before striking
        if (isWindingUp)
        {
            windupTimer -= Time.deltaTime;
            // Pulsing red flash during wind-up
            float pulse = Mathf.PingPong(Time.time * 12f, 1f);
            Color flashColor = Color.Lerp(baseColor, Color.red, pulse);
            foreach (var r in renderers) if (r != null) r.material.color = flashColor;

            if (windupTimer <= 0)
            {
                isWindingUp = false;
                foreach (var r in renderers) if (r != null) r.material.color = baseColor;
                // Check range again after wind-up — player may have dodged
                float finalDist = Vector3.Distance(transform.position, target.position);
                if (finalDist <= attackRange * 1.3f)
                {
                    var playerHealth = target.GetComponent<Health>();
                    var playerCtrl = target.GetComponent<PlayerController>();
                    if (playerHealth != null && !playerHealth.IsDead
                        && (playerCtrl == null || !playerCtrl.isInvulnerable))
                        playerHealth.TakeDamage(attackDamage);
                }
            }
            return;
        }

        if (dist > attackRange)
        {
            Vector3 moveDir = toPlayer.normalized;
            rb.MovePosition(transform.position + moveDir * moveSpeed * speedMult * Time.deltaTime);
            if (moveDir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(moveDir);
        }
        else
        {
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0)
            {
                attackTimer = attackCooldown;
                isWindingUp = true;
                windupTimer = WindupDuration;
            }
        }
    }

    void OnDestroy() { if (health != null) health.OnDeath -= OnDie; }
}
