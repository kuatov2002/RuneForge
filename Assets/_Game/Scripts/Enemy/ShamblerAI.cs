using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Shambler — melee zombie-like enemy.
/// Mechanics:
/// - Walks toward player, attacks in melee
/// - ENRAGE below 30% HP: turns red, +50% speed, attacks faster
/// - DODGE ROLL: occasionally sidesteps incoming projectiles (reacts to nearby SpellProjectiles)
/// - DEATH BURST: small AoE damage on death (punishes melee-range players)
/// - Floor 3+: LEAP attack
/// - Floor 5+: SLAM shockwave
/// </summary>
public class ShamblerAI : MonoBehaviour
{
    public float moveSpeed = 2.5f;
    public float attackRange = 1.5f;
    public float attackCooldown = 1f;
    public int attackDamage = 1;
    public Color baseColor = new Color(0.8f, 0.2f, 0.2f);
    public int floorLevel = 1;

    Transform target;
    float attackTimer;
    Health health;
    Rigidbody rb;
    Renderer[] renderers;
    bool isDead;

    // Enrage
    bool isEnraged;
    float enrageSpeedMult = 1.5f;
    float enrageCooldownMult = 0.6f;

    // Dodge
    float dodgeCooldown;
    bool isDodging;
    float dodgeTimer;
    Vector3 dodgeDir;

    // Wind-up
    bool isWindingUp;
    float windupTimer;
    const float WindupDuration = 0.35f;

    // Floor 3+: leap
    float leapCooldown;
    bool isLeaping;
    float leapTimer;
    Vector3 leapTarget;

    // Floor 5+: slam
    float slamCooldown;

    // Void pull
    Vector3 pullTarget;
    float pullTimer;

    // Leap prediction
    const float LeapPredictionTime = 0.4f;
    Vector3 lastPlayerPos;
    Vector3 playerVelocity;

    // Leap poison pool
    const float PoisonPoolRadius = 2f;
    const float PoisonPoolDuration = 4f;
    const float PoisonPoolDamagePerSec = 1f;

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

        // Death burst: small AoE on death
        Collider[] hits = Physics.OverlapSphere(transform.position, 1.5f);
        foreach (var h in hits)
        {
            var pc = h.GetComponent<PlayerController>();
            if (pc != null && !pc.isInvulnerable)
            {
                var php = h.GetComponent<Health>();
                if (php != null && !php.IsDead) php.TakeDamage(1);
            }
        }

        // Death burst VFX
        var burstGO = new GameObject("ShamblerDeathBurst");
        burstGO.transform.position = transform.position + Vector3.up * 0.3f;
        var ps = burstGO.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.startLifetime = 0.3f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.12f);
        main.startColor = new ParticleSystem.MinMaxGradient(baseColor, new Color(0.5f, 0.1f, 0.1f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.duration = 0.1f; main.loop = false;
        var em = ps.emission; em.rateOverTime = 0;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, 12) });
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.2f;
        var mat = new Material(ShaderCache.ParticleShader);
        mat.SetColor("_Color", baseColor);
        burstGO.GetComponent<ParticleSystemRenderer>().material = mat;
        ps.Play();
        Object.Destroy(burstGO, 0.5f);

        foreach (var r in renderers) if (r != null) r.material.color = Color.gray;
        Destroy(gameObject, 0.3f);
    }

    public void ApplyPull(Vector3 pullTo, float duration)
    {
        pullTarget = pullTo;
        pullTimer = duration;
    }

    void Update()
    {
        if (isDead || target == null) return;

        // Track player velocity for leap prediction
        if (Time.deltaTime > 0)
        {
            playerVelocity = (target.position - lastPlayerPos) / Time.deltaTime;
            playerVelocity.y = 0;
            lastPlayerPos = target.position;
        }

        // Check enrage
        if (!isEnraged && health.currentHP <= Mathf.CeilToInt(health.maxHP * 0.3f))
        {
            isEnraged = true;
            baseColor = new Color(1f, 0.15f, 0.05f); // bright red
            // Enrage VFX: brief red flash
            if (TopDownCamera.Instance != null) TopDownCamera.Instance.AddTrauma(0.05f);
        }

        UpdateStatusVisual();

        // Void pull
        if (pullTimer > 0)
        {
            pullTimer -= Time.deltaTime;
            Vector3 dir = (pullTarget - transform.position); dir.y = 0;
            if (dir.magnitude > 0.3f)
                rb.MovePosition(transform.position + dir.normalized * 6f * Time.deltaTime);
            return;
        }

        if (health.IsStunned) return;
        float speedMult = health.SpeedMultiplier;
        float speedFinal = moveSpeed * speedMult * (isEnraged ? enrageSpeedMult : 1f);

        // Dodge roll
        if (isDodging)
        {
            dodgeTimer -= Time.deltaTime;
            rb.MovePosition(transform.position + dodgeDir * 8f * Time.deltaTime);
            if (dodgeTimer <= 0) isDodging = false;
            return;
        }

        // Check for incoming projectiles to dodge
        if (dodgeCooldown <= 0)
        {
            dodgeCooldown -= Time.deltaTime;
            if (TryDodgeProjectile())
            {
                dodgeCooldown = 3f;
                return;
            }
        }
        else
        {
            dodgeCooldown -= Time.deltaTime;
        }

        Vector3 toPlayer = target.position - transform.position;
        toPlayer.y = 0;
        float dist = toPlayer.magnitude;

        // Wind-up
        if (isWindingUp)
        {
            windupTimer -= Time.deltaTime;
            float flash = Mathf.PingPong(Time.time * 10f, 1f);
            foreach (var r in renderers)
                if (r != null && r.gameObject.name != "Eye")
                    r.material.color = Color.Lerp(baseColor, Color.white, flash);

            if (windupTimer <= 0)
            {
                isWindingUp = false;
                var playerHealth = target.GetComponent<Health>();
                var playerCtrl = target.GetComponent<PlayerController>();
                if (playerHealth != null && !playerHealth.IsDead
                    && (playerCtrl == null || !playerCtrl.isInvulnerable)
                    && Vector3.Distance(transform.position, target.position) < attackRange + 0.5f)
                {
                    playerHealth.TakeDamage(attackDamage + (isEnraged ? 1 : 0));
                    SFXSystem.Play(SFXSystem.SFXType.Hit, transform.position);
                }
            }
            return;
        }

        // Floor 3+: leap
        if (floorLevel >= 3 && !isLeaping)
        {
            leapCooldown -= Time.deltaTime;
            if (leapCooldown <= 0 && dist > 4f && dist < 10f)
            {
                leapCooldown = isEnraged ? 3f : 5f;
                isLeaping = true;
                leapTimer = 0.4f;
                leapTarget = target.position + playerVelocity * LeapPredictionTime;
                var warn = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(warn.GetComponent<CapsuleCollider>());
                warn.transform.position = leapTarget + Vector3.up * 0.02f;
                warn.transform.localScale = new Vector3(2f, 0.01f, 2f);
                warn.GetComponent<Renderer>().material = ShaderCache.NewDanger(new Color(1f, 0.2f, 0.1f), 0.1f, 0.5f, 6f, 3f);
                Destroy(warn, 0.5f);
                return;
            }
        }

        if (isLeaping)
        {
            leapTimer -= Time.deltaTime;
            Vector3 toTarget = leapTarget - transform.position; toTarget.y = 0;
            rb.MovePosition(transform.position + toTarget.normalized * 18f * Time.deltaTime + Vector3.up * 0.02f);
            if (leapTimer <= 0 || toTarget.magnitude < 0.5f)
            {
                isLeaping = false;
                Collider[] hits = Physics.OverlapSphere(transform.position, 1.8f);
                foreach (var h in hits)
                {
                    var pc = h.GetComponent<PlayerController>();
                    if (pc != null && !pc.isInvulnerable)
                    {
                        var php = h.GetComponent<Health>();
                        if (php != null && !php.IsDead) php.TakeDamage(attackDamage + 1);
                    }
                }
                GameFeel.SpawnDeathVFX(transform.position, baseColor);
                if (TopDownCamera.Instance != null) TopDownCamera.Instance.AddTrauma(0.15f);

                // Poison pool at landing site
                SpawnPoisonPool(transform.position);
            }
            return;
        }

        // Floor 5+: slam
        if (floorLevel >= 5 && dist <= attackRange + 1f)
        {
            slamCooldown -= Time.deltaTime;
            if (slamCooldown <= 0)
            {
                slamCooldown = isEnraged ? 2.5f : 4f;
                var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(ring.GetComponent<CapsuleCollider>());
                ring.transform.position = transform.position + Vector3.up * 0.05f;
                ring.transform.localScale = new Vector3(0.5f, 0.02f, 0.5f);
                ring.GetComponent<Renderer>().material = ShaderCache.NewDanger(new Color(1f, 0.3f, 0.1f), 0.2f, 0.5f, 8f, 4f);
                ring.AddComponent<DeathRingEffect>().Init(5f, 0.5f);
                Collider[] slamHits = Physics.OverlapSphere(transform.position, 2.5f);
                foreach (var h in slamHits)
                {
                    var pc = h.GetComponent<PlayerController>();
                    if (pc != null && !pc.isInvulnerable)
                    {
                        var php = h.GetComponent<Health>();
                        if (php != null && !php.IsDead) php.TakeDamage(attackDamage);
                    }
                }
            }
        }

        // Movement
        if (dist > attackRange)
        {
            Vector3 moveDir = toPlayer.normalized;
            rb.MovePosition(transform.position + moveDir * speedFinal * Time.deltaTime);
            if (moveDir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(moveDir);
        }
        else
        {
            float cd = attackCooldown * (isEnraged ? enrageCooldownMult : 1f);
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0)
            {
                attackTimer = cd;
                isWindingUp = true;
                windupTimer = WindupDuration * (isEnraged ? 0.6f : 1f);
            }
        }
    }

    bool TryDodgeProjectile()
    {
        // Check for nearby incoming projectiles
        Collider[] nearby = Physics.OverlapSphere(transform.position, 3f);
        foreach (var c in nearby)
        {
            var proj = c.GetComponent<SpellProjectile>();
            var fb = c.GetComponent<FireballProjectile>();
            var ice = c.GetComponent<IceSpikeProjectile>();
            if (proj == null && fb == null && ice == null) continue;

            // Dodge perpendicular to projectile direction
            Vector3 projDir = (c.transform.position - transform.position).normalized;
            Vector3 perpendicular = Vector3.Cross(projDir, Vector3.up).normalized;
            if (Random.value > 0.5f) perpendicular = -perpendicular;

            isDodging = true;
            dodgeTimer = 0.2f;
            dodgeDir = perpendicular;
            return true;
        }
        return false;
    }

    void UpdateStatusVisual()
    {
        if (renderers == null || renderers.Length == 0) return;
        Color col = baseColor;
        if (health.IsStunned) col = health.IsFrozen ? new Color(0.6f, 0.9f, 1f) : new Color(1f, 1f, 0.3f);
        foreach (var r in renderers)
            if (r != null && r.gameObject.name != "Eye") r.material.color = col;
    }

    void SpawnPoisonPool(Vector3 center)
    {
        var pool = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(pool.GetComponent<CapsuleCollider>());
        pool.name = "PoisonPool";
        pool.transform.position = center + Vector3.up * 0.02f;
        float diameter = PoisonPoolRadius * 2f;
        pool.transform.localScale = new Vector3(diameter, 0.015f, diameter);
        pool.GetComponent<Renderer>().material = ShaderCache.NewPoison(new Color(0.1f, 0.2f, 0.05f), 0.5f);

        // Bubbling particles
        var particleGO = new GameObject("PoisonBubbles");
        particleGO.transform.parent = pool.transform;
        particleGO.transform.localPosition = Vector3.up * 0.5f;
        var ps = particleGO.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.startLifetime = 0.5f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
        main.startColor = new Color(0.3f, 0.9f, 0.1f, 0.6f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.3f;
        var em = ps.emission; em.rateOverTime = 8;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = PoisonPoolRadius * 0.8f;
        var mat = new Material(ShaderCache.ParticleShader);
        mat.SetColor("_Color", new Color(0.3f, 0.9f, 0.1f));
        particleGO.GetComponent<ParticleSystemRenderer>().material = mat;
        ps.Play();

        var zone = pool.AddComponent<PoisonPool>();
        zone.Init(PoisonPoolRadius, PoisonPoolDuration, PoisonPoolDamagePerSec);
    }

    void OnDestroy() { if (health != null) health.OnDeath -= OnDie; }
}

/// <summary>Damaging poison pool left by Shambler leap.</summary>
public class PoisonPool : MonoBehaviour
{
    float _radius;
    float _duration;
    float _damagePerSec;
    float _timer;
    float _dmgAccum;
    Renderer _renderer;

    public void Init(float radius, float duration, float damagePerSec)
    {
        _radius = radius;
        _duration = duration;
        _damagePerSec = damagePerSec;
        _timer = duration;
        _renderer = GetComponent<Renderer>();
    }

    void Update()
    {
        _timer -= Time.deltaTime;
        if (_timer <= 0)
        {
            Destroy(gameObject);
            return;
        }

        // Fade out over last second
        if (_renderer != null && _timer < 1f)
        {
            Color c = _renderer.material.GetColor("_EmissionColor");
            _renderer.material.SetColor("_EmissionColor", c * Mathf.Clamp01(_timer));
        }

        // Damage player in radius
        _dmgAccum += _damagePerSec * Time.deltaTime;
        if (_dmgAccum >= 1f)
        {
            _dmgAccum -= 1f;
            Collider[] hits = Physics.OverlapSphere(transform.position, _radius);
            foreach (var h in hits)
            {
                var pc = h.GetComponent<PlayerController>();
                if (pc == null || pc.isInvulnerable) continue;
                var hp = h.GetComponent<Health>();
                if (hp != null && !hp.IsDead)
                    hp.TakeDamage(1, ElementType.Poison);
            }
        }
    }
}
