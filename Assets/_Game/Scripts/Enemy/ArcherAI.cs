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
    public int floorLevel = 1;

    Transform target;
    float shootTimer;
    Health health;
    Rigidbody rb;
    Renderer[] renderers;
    bool isDead;

    // Telegraph
    bool isAiming;
    float aimTimer;
    const float AimDuration = 0.5f;
    Vector3 aimDir;
    GameObject aimLine;

    // Floor 3+: burst fire
    int burstShotsRemaining;
    float burstTimer;

    // Floor 4+: scatter shot
    float scatterCooldown;

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

        // Aiming telegraph
        if (isAiming)
        {
            aimTimer -= Time.deltaTime;
            // Flash bow
            float flash = Mathf.PingPong(Time.time * 10f, 1f);
            var bow = transform.Find("Bow");
            if (bow != null)
                bow.GetComponent<Renderer>().material.color = Color.Lerp(new Color(0.4f, 0.25f, 0.1f), Color.white, flash);

            if (aimTimer <= 0)
            {
                isAiming = false;
                if (aimLine != null) Destroy(aimLine);
                EnemyProjectile.Create(transform.position, aimDir, projectileSpeed, attackDamage,
                    new Color(0.3f, 0.8f, 0.3f));

                // Floor 3+: burst fire (2 extra shots)
                if (floorLevel >= 3)
                    burstShotsRemaining = 2;
            }
            return;
        }

        // Burst fire (rapid follow-up shots)
        if (burstShotsRemaining > 0)
        {
            burstTimer -= Time.deltaTime;
            if (burstTimer <= 0)
            {
                burstTimer = 0.2f;
                burstShotsRemaining--;
                Vector3 burstDir = (target.position - transform.position).normalized;
                burstDir = Quaternion.Euler(0, Random.Range(-8f, 8f), 0) * burstDir; // slight spread
                EnemyProjectile.Create(transform.position, burstDir, projectileSpeed * 1.1f, attackDamage,
                    new Color(0.3f, 0.8f, 0.3f));
            }
            return;
        }

        // Floor 4+: scatter shot (3 arrows in a fan)
        if (floorLevel >= 4)
        {
            scatterCooldown -= Time.deltaTime;
            if (scatterCooldown <= 0 && dist < 8f)
            {
                scatterCooldown = 6f;
                Vector3 scatterDir = toPlayer.normalized;
                for (int i = -1; i <= 1; i++)
                {
                    Vector3 dir = Quaternion.Euler(0, i * 20f, 0) * scatterDir;
                    EnemyProjectile.Create(transform.position, dir, projectileSpeed * 0.8f, attackDamage,
                        new Color(0.8f, 0.3f, 0.3f)); // red = scatter
                }
                SFXSystem.Play(SFXSystem.SFXType.Cast, transform.position, 0.5f);
            }
        }

        // Shoot
        shootTimer -= Time.deltaTime;
        if (shootTimer <= 0 && dist < 12f)
        {
            shootTimer = shootCooldown;
            aimDir = toPlayer.normalized;
            isAiming = true;
            aimTimer = AimDuration;

            // Red aim line
            aimLine = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(aimLine.GetComponent<BoxCollider>());
            aimLine.name = "AimLine";
            Vector3 mid = transform.position + aimDir * 5f;
            aimLine.transform.position = mid + Vector3.up * 0.05f;
            aimLine.transform.localScale = new Vector3(0.08f, 0.02f, 10f);
            aimLine.transform.rotation = Quaternion.LookRotation(aimDir);
            aimLine.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(1f, 0.2f, 0.1f), 2f);
            Destroy(aimLine, AimDuration + 0.1f);
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
