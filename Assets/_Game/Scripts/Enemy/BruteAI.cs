using UnityEngine;

/// <summary>
/// Brute — heavy melee enemy.
/// Mechanics:
/// - Slow walk, charge attack with telegraph
/// - GROUND POUND: AoE shockwave ring that expands outward (must jump over/dodge)
/// - GRAB: if charge hits player, throws them backward
/// - UNSTOPPABLE: immune to freeze/stun during charge
/// - ENRAGE: below 25% HP, charges twice as fast
/// </summary>
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
    bool isWindingUp;
    float windupTimer;
    const float WindupDuration = 0.5f;
    Vector3 chargeDir;
    float chargeDistLeft;
    GameObject telegraphVFX;

    // Ground pound
    float groundPoundCooldown;
    bool isGroundPounding;
    float gpWindupTimer;

    // Enrage
    bool isEnraged;

    void Start()
    {
        var player = FindAnyObjectByType<PlayerController>();
        if (player != null) target = player.transform;
        health = GetComponent<Health>();
        rb = GetComponent<Rigidbody>();
        renderers = GetComponentsInChildren<Renderer>();
        if (health != null) health.OnDeath += OnDie;
        groundPoundCooldown = 6f;
    }

    void OnDie()
    {
        isDead = true;

        // Death: screen shake + fragments
        if (TopDownCamera.Instance != null) TopDownCamera.Instance.AddTrauma(0.2f);

        foreach (var r in renderers) if (r != null) r.material.color = Color.gray;
        Destroy(gameObject, 0.3f);
    }

    void Update()
    {
        if (isDead || target == null) return;

        // Enrage check
        if (!isEnraged && health.currentHP <= Mathf.CeilToInt(health.maxHP * 0.25f))
        {
            isEnraged = true;
            baseColor = new Color(0.7f, 0.1f, 0.05f);
            if (TopDownCamera.Instance != null) TopDownCamera.Instance.AddTrauma(0.1f);
            SFXSystem.Play(SFXSystem.SFXType.CritHit, transform.position);
        }

        UpdateStatusVisual();
        if (health.IsStunned && !isCharging) return; // Unstoppable during charge
        float speedMult = health.SpeedMultiplier;

        // Ground pound wind-up
        if (isGroundPounding)
        {
            gpWindupTimer -= Time.deltaTime;
            // Raise up slightly during wind-up
            float pulse = Mathf.PingPong(Time.time * 8f, 1f);
            foreach (var r in renderers)
                if (r != null && r.gameObject.name != "Eye")
                    r.material.color = Color.Lerp(baseColor, new Color(1f, 0.5f, 0.1f), pulse);

            if (gpWindupTimer <= 0)
            {
                isGroundPounding = false;
                GroundPound();
            }
            return;
        }

        // Charge wind-up
        if (isWindingUp)
        {
            windupTimer -= Time.deltaTime;
            float flash = Mathf.PingPong(Time.time * 12f, 1f);
            foreach (var r in renderers)
                if (r != null && r.gameObject.name != "Eye")
                    r.material.color = Color.Lerp(baseColor, Color.white, flash);

            if (windupTimer <= 0)
            {
                isWindingUp = false;
                isCharging = true;
                if (telegraphVFX != null) Destroy(telegraphVFX);
                if (TopDownCamera.Instance != null) TopDownCamera.Instance.AddTrauma(0.15f);
            }
            return;
        }

        // Charging
        if (isCharging)
        {
            float step = chargeSpeed * Time.deltaTime; // Ignore speed mult during charge (unstoppable)
            rb.MovePosition(transform.position + chargeDir * step);
            chargeDistLeft -= step;

            // Check for player hit during charge
            float playerDist = Vector3.Distance(transform.position, target.position);
            if (playerDist < 1.5f)
            {
                var pc = target.GetComponent<PlayerController>();
                if (pc != null && !pc.isInvulnerable)
                {
                    var php = target.GetComponent<Health>();
                    if (php != null && !php.IsDead)
                    {
                        php.TakeDamage(attackDamage + 1);
                        // Throw player backward
                        GameFeel.ApplyKnockback(target, transform.position, 8f);
                        if (TopDownCamera.Instance != null) TopDownCamera.Instance.AddTrauma(0.3f);
                    }
                }
                isCharging = false;
                Slam();
                return;
            }

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
        groundPoundCooldown -= Time.deltaTime;

        // Ground pound: when player is close
        if (groundPoundCooldown <= 0 && dist <= 3f)
        {
            groundPoundCooldown = isEnraged ? 4f : 7f;
            isGroundPounding = true;
            gpWindupTimer = 0.6f;

            // Warning circle
            var warn = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(warn.GetComponent<CapsuleCollider>());
            warn.transform.position = transform.position + Vector3.up * 0.02f;
            warn.transform.localScale = new Vector3(4f, 0.01f, 4f);
            warn.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(1f, 0.3f, 0.1f, 0.3f), 1.5f);
            Destroy(warn, 0.7f);
            return;
        }

        // Charge
        float cdMult = isEnraged ? 0.5f : 1f;
        if (dist <= chargeRange && dist > 1.5f && chargeTimer <= 0)
        {
            chargeTimer = chargeCooldown * cdMult;
            chargeDir = toPlayer.normalized;
            chargeDistLeft = dist + 2f; // overshoot slightly
            transform.rotation = Quaternion.LookRotation(chargeDir);
            isWindingUp = true;
            windupTimer = WindupDuration * (isEnraged ? 0.6f : 1f);
            CreateChargeTelegraph(dist + 2f);
            return;
        }

        // Normal walk
        float finalSpeed = moveSpeed * speedMult * (isEnraged ? 1.5f : 1f);
        if (dist > 1.5f)
        {
            Vector3 moveDir = toPlayer.normalized;
            rb.MovePosition(transform.position + moveDir * finalSpeed * Time.deltaTime);
            transform.rotation = Quaternion.LookRotation(moveDir);
        }
    }

    void Slam()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, slamRadius);
        foreach (var hit in hits)
        {
            var pc = hit.GetComponent<PlayerController>();
            if (pc == null || pc.isInvulnerable) continue;
            var hp = hit.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
            {
                hp.TakeDamage(attackDamage);
                GameFeel.ApplyKnockback(hit.transform, transform.position, 4f);
            }
        }

        // Slam VFX
        var vfx = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(vfx.GetComponent<CapsuleCollider>());
        vfx.transform.position = transform.position + Vector3.up * 0.05f;
        vfx.transform.localScale = new Vector3(slamRadius * 2, 0.05f, slamRadius * 2);
        vfx.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(0.6f, 0.3f, 0.1f), 2f);
        Destroy(vfx, 0.2f);

        if (TopDownCamera.Instance != null) TopDownCamera.Instance.AddTrauma(0.2f);
    }

    void GroundPound()
    {
        // Expanding shockwave ring
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(ring.GetComponent<CapsuleCollider>());
        ring.transform.position = transform.position + Vector3.up * 0.05f;
        ring.transform.localScale = new Vector3(0.5f, 0.03f, 0.5f);
        ring.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(0.8f, 0.3f, 0.1f), 3f);
        ring.AddComponent<DeathRingEffect>().Init(8f, 0.6f);

        // Damage in radius
        Collider[] hits = Physics.OverlapSphere(transform.position, 3.5f);
        foreach (var h in hits)
        {
            var pc = h.GetComponent<PlayerController>();
            if (pc == null || pc.isInvulnerable) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
            {
                hp.TakeDamage(attackDamage + 1);
                GameFeel.ApplyKnockback(h.transform, transform.position, 6f);
            }
        }

        // Dust particles
        var dustGO = new GameObject("BruteGroundPoundDust");
        dustGO.transform.position = transform.position + Vector3.up * 0.2f;
        var ps = dustGO.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.25f);
        main.startColor = new Color(0.5f, 0.4f, 0.3f, 0.5f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.3f;
        main.duration = 0.15f; main.loop = false;
        var em = ps.emission; em.rateOverTime = 0;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, 20) });
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.5f;
        var mat = new Material(ShaderCache.ParticleShader);
        mat.SetColor("_Color", new Color(0.5f, 0.4f, 0.3f));
        dustGO.GetComponent<ParticleSystemRenderer>().material = mat;
        ps.Play();
        Object.Destroy(dustGO, 1f);

        if (TopDownCamera.Instance != null) TopDownCamera.Instance.AddTrauma(0.35f);
        if (GameFeel.Instance != null) GameFeel.Instance.Hitstop(0.06f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, transform.position);
    }

    void UpdateStatusVisual()
    {
        if (renderers == null) return;
        Color col = baseColor;
        if (health.IsStunned && !isCharging)
            col = health.IsFrozen ? new Color(0.6f, 0.9f, 1f) : new Color(1f, 1f, 0.3f);
        foreach (var r in renderers)
            if (r != null && r.gameObject.name != "Eye") r.material.color = col;
    }

    void CreateChargeTelegraph(float distance)
    {
        if (telegraphVFX != null) Destroy(telegraphVFX);
        telegraphVFX = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(telegraphVFX.GetComponent<BoxCollider>());
        telegraphVFX.name = "ChargeTelegraph";
        Vector3 midPoint = transform.position + chargeDir * (distance * 0.5f);
        telegraphVFX.transform.position = midPoint + Vector3.up * 0.05f;
        telegraphVFX.transform.localScale = new Vector3(1.2f, 0.04f, distance);
        telegraphVFX.transform.rotation = Quaternion.LookRotation(chargeDir);
        telegraphVFX.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(1f, 0.15f, 0.1f), 3f);
        Destroy(telegraphVFX, WindupDuration + 0.1f);
    }

    void OnDestroy()
    {
        if (health != null) health.OnDeath -= OnDie;
        if (telegraphVFX != null) Destroy(telegraphVFX);
    }
}
