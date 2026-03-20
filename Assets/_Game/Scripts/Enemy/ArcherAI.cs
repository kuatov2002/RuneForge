using UnityEngine;

public class ArcherAI : MonoBehaviour
{
    public float moveSpeed = 3f;
    public float preferredDist = 7f;
    public float retreatDist = 4f;
    public float shootCooldown = 2f;
    public float projectileSpeed = 8f;
    public int attackDamage = 1;
    public Color baseColor = new Color(0.2f, 0.6f, 0.2f);

    Transform target;
    float shootTimer;
    Health health;
    Rigidbody rb;
    Renderer[] renderers;
    bool isDead;

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
        foreach (var r in renderers)
            if (r != null) r.material.color = Color.gray;
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

        // Movement: keep preferred distance
        Vector3 moveDir = Vector3.zero;
        if (dist < retreatDist)
            moveDir = -toPlayer.normalized; // Retreat
        else if (dist > preferredDist + 1f)
            moveDir = toPlayer.normalized; // Approach

        if (moveDir.sqrMagnitude > 0.01f)
        {
            rb.MovePosition(transform.position + moveDir * moveSpeed * speedMult * Time.deltaTime);
            transform.rotation = Quaternion.LookRotation(toPlayer.normalized);
        }
        else
        {
            transform.rotation = Quaternion.LookRotation(toPlayer.normalized);
        }

        // Shoot
        shootTimer -= Time.deltaTime;
        if (shootTimer <= 0 && dist < 12f)
        {
            shootTimer = shootCooldown;
            Vector3 dir = toPlayer.normalized;
            EnemyProjectile.Create(transform.position, dir, projectileSpeed, attackDamage,
                new Color(0.3f, 0.8f, 0.3f));
        }
    }

    void UpdateStatusVisual()
    {
        if (renderers == null) return;
        Color col = baseColor;
        if (health.IsStunned)
            col = health.IsFrozen ? new Color(0.6f, 0.9f, 1f) : new Color(1f, 1f, 0.3f);
        else if (health.IsSlowed) col = new Color(0.4f, 0.6f, 1f);
        else if (health.PoisonStacks > 0)
            col = Color.Lerp(baseColor, new Color(0.2f, 0.9f, 0.1f), health.PoisonStacks / 5f);
        foreach (var r in renderers)
            if (r != null && r.gameObject.name != "Eye") r.material.color = col;
    }

    void OnDestroy() { if (health != null) health.OnDeath -= OnDie; }
}
