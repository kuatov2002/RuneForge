using UnityEngine;

/// <summary>
/// Swarm — tiny fast enemy, spawns in groups.
/// Mechanics:
/// - Fast movement, weak individually
/// - FRENZY: gains +30% speed for each nearby swarm ally (pack hunting)
/// - EXPLODE ON DEATH: small AoE burst that damages player AND other swarm units
///   (chain reaction potential — killing one near others causes cascade)
/// - SCATTER: when an ally dies nearby, briefly runs away from death point
/// </summary>
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

    // Wind-up
    const float WindupDuration = 0.3f;
    float windupTimer;
    bool isWindingUp;

    // Scatter (flee from ally death)
    Vector3 scatterDir;
    float scatterTimer;

    // Pack offset
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

        // Explode on death: small AoE
        float explodeRadius = 1.2f;
        Collider[] hits = Physics.OverlapSphere(transform.position, explodeRadius);
        foreach (var h in hits)
        {
            if (h.gameObject == gameObject) continue;

            // Damage player
            var pc = h.GetComponent<PlayerController>();
            if (pc != null && !pc.isInvulnerable)
            {
                var php = h.GetComponent<Health>();
                if (php != null && !php.IsDead) php.TakeDamage(1);
            }

            // Damage other swarm units (chain reaction!)
            var otherSwarm = h.GetComponent<SwarmAI>();
            if (otherSwarm != null && !otherSwarm.isDead)
            {
                var ohp = h.GetComponent<Health>();
                if (ohp != null && !ohp.IsDead) ohp.TakeDamage(2);
            }

            // Make nearby swarm scatter
            var scatterSwarm = h.GetComponent<SwarmAI>();
            if (scatterSwarm != null && !scatterSwarm.isDead)
                scatterSwarm.TriggerScatter(transform.position);
        }

        // Death explosion VFX
        var burstGO = new GameObject("SwarmExplosion");
        burstGO.transform.position = transform.position + Vector3.up * 0.2f;
        var ps = burstGO.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.startLifetime = 0.2f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.8f, 0.1f), new Color(0.8f, 0.4f, 0.1f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.duration = 0.1f; main.loop = false;
        var em = ps.emission; em.rateOverTime = 0;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, 8) });
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.15f;
        var mat = new Material(ShaderCache.ParticleShader);
        mat.SetColor("_Color", new Color(1f, 0.7f, 0.1f));
        burstGO.GetComponent<ParticleSystemRenderer>().material = mat;
        ps.Play();
        Object.Destroy(burstGO, 0.4f);

        Destroy(gameObject, 0.1f);
    }

    public void TriggerScatter(Vector3 deathPos)
    {
        Vector3 away = (transform.position - deathPos);
        away.y = 0;
        if (away.sqrMagnitude < 0.01f) away = Random.insideUnitSphere;
        away.y = 0;
        scatterDir = away.normalized;
        scatterTimer = 0.5f;
    }

    // Queen command: charge a specific position at high speed
    Vector3 chargeTarget;
    bool isCharging;
    float chargeTimer;

    public void CommandCharge(Vector3 targetPos)
    {
        chargeTarget = targetPos;
        isCharging = true;
        chargeTimer = 1.5f; // Charge for 1.5s then resume normal behavior
    }

    void Update()
    {
        if (isDead || target == null) return;
        if (health.IsStunned) return;
        float speedMult = health.SpeedMultiplier;

        // Scatter (flee from ally death)
        if (scatterTimer > 0)
        {
            scatterTimer -= Time.deltaTime;
            rb.MovePosition(transform.position + scatterDir * moveSpeed * 2f * Time.deltaTime);
            return;
        }

        // Queen command: charge at target
        if (isCharging)
        {
            chargeTimer -= Time.deltaTime;
            Vector3 toCharge = chargeTarget - transform.position; toCharge.y = 0;
            if (toCharge.magnitude > 0.5f)
            {
                rb.MovePosition(transform.position + toCharge.normalized * moveSpeed * 3f * speedMult * Time.deltaTime);
                transform.rotation = Quaternion.LookRotation(toCharge.normalized);
            }
            if (chargeTimer <= 0 || toCharge.magnitude < 0.5f)
                isCharging = false;
            return;
        }

        // Frenzy: count nearby swarm allies for speed boost
        int nearbyAllies = 0;
        Collider[] pack = Physics.OverlapSphere(transform.position, 3f);
        foreach (var p in pack)
        {
            if (p.gameObject == gameObject) continue;
            if (p.GetComponent<SwarmAI>() != null) nearbyAllies++;
        }
        float frenzyMult = 1f + nearbyAllies * 0.15f; // +15% per ally, up to ~+75% with 5
        frenzyMult = Mathf.Min(frenzyMult, 1.75f);

        // Visual: brighter yellow with more allies
        Color currentColor = Color.Lerp(baseColor, new Color(1f, 0.8f, 0.1f), (frenzyMult - 1f) * 1.5f);
        foreach (var r in renderers) if (r != null) r.material.color = currentColor;

        Vector3 targetPos = target.position + offset;
        Vector3 toPlayer = targetPos - transform.position;
        toPlayer.y = 0;
        float dist = toPlayer.magnitude;

        // Wind-up
        if (isWindingUp)
        {
            windupTimer -= Time.deltaTime;
            float pulse = Mathf.PingPong(Time.time * 12f, 1f);
            foreach (var r in renderers) if (r != null) r.material.color = Color.Lerp(currentColor, Color.red, pulse);

            if (windupTimer <= 0)
            {
                isWindingUp = false;
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

        float finalSpeed = moveSpeed * speedMult * frenzyMult;
        if (dist > attackRange)
        {
            Vector3 moveDir = toPlayer.normalized;
            rb.MovePosition(transform.position + moveDir * finalSpeed * Time.deltaTime);
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
