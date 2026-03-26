using UnityEngine;

/// <summary>
/// Buffer — support enemy that buffs allies with war cry.
/// Mechanics:
/// - WAR CRY: Every 6s, buffs all allies within 6u (+40% damage, +20% speed for 4s)
/// - MELEE: Weak punch (2 dmg) if player is close
/// - ENRAGE: When last enemy alive, +50% speed, attacks aggressively
/// - Stays near other enemies at medium range from player
/// </summary>
public class BufferAI : MonoBehaviour
{
    public float moveSpeed = 2.8f;
    public Color baseColor = new Color(0.9f, 0.55f, 0.1f);

    Transform target;
    Health health;
    Rigidbody rb;
    Renderer[] renderers;
    bool isDead;

    // War Cry
    float warCryCooldown = 3f; // starts sooner
    const float WarCryInterval = 6f;
    const float BuffRadius = 6f;
    const float BuffDuration = 4f;
    const float BuffDamageMult = 1.4f;
    const float BuffSpeedMult = 1.2f;

    // Melee attack
    float attackTimer;
    const float AttackRange = 1.5f;
    const float AttackCooldown = 1.2f;
    const int AttackDamage = 2;
    bool isWindingUp;
    float windupTimer;

    // Enrage (last alive)
    bool isEnraged;

    // Buff aura VFX
    ParticleSystem auraPS;
    float auraFlashTimer;

    // Void pull
    Vector3 pullTarget;
    float pullTimer;

    void Start()
    {
        var player = FindAnyObjectByType<PlayerController>();
        if (player != null) target = player.transform;
        health = GetComponent<Health>();
        rb = GetComponent<Rigidbody>();
        renderers = GetComponentsInChildren<Renderer>();
        if (health != null) health.OnDeath += OnDie;
        CreateAura();
    }

    void CreateAura()
    {
        var auraGO = new GameObject("BuffAura");
        auraGO.transform.parent = transform;
        auraGO.transform.localPosition = Vector3.up * 0.5f;
        auraPS = auraGO.AddComponent<ParticleSystem>();
        auraPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = auraPS.main;
        main.startLifetime = 0.8f;
        main.startSpeed = 0.5f;
        main.startSize = 0.05f;
        main.startColor = new Color(1f, 0.7f, 0.1f, 0.5f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.loop = true;
        var em = auraPS.emission;
        em.rateOverTime = 4;
        var sh = auraPS.shape;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = 0.3f;
        var sol = auraPS.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 1, 1, 0));
        var mat = new Material(ShaderCache.ParticleShader);
        mat.SetColor("_Color", new Color(1f, 0.7f, 0.1f));
        auraGO.GetComponent<ParticleSystemRenderer>().material = mat;
        auraPS.Play();
    }

    void OnDie()
    {
        isDead = true;
        if (auraPS != null) auraPS.Stop();
        GameFeel.SpawnDeathVFX(transform.position, baseColor);
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

        // Void pull
        if (pullTimer > 0)
        {
            pullTimer -= Time.deltaTime;
            Vector3 dir = pullTarget - transform.position; dir.y = 0;
            if (dir.magnitude > 0.3f)
                rb.MovePosition(transform.position + dir.normalized * 6f * Time.deltaTime);
            return;
        }

        if (health.IsStunned) return;

        float speedMult = health.SpeedMultiplier;
        float dist = Vector3.Distance(transform.position, target.position);

        // Check if last enemy alive → enrage
        if (!isEnraged)
        {
            int aliveCount = 0;
            var allHealth = FindObjectsByType<Health>(FindObjectsSortMode.None);
            foreach (var h in allHealth)
            {
                if (h.IsDead || h.GetComponent<PlayerController>() != null) continue;
                aliveCount++;
            }
            if (aliveCount <= 1)
            {
                isEnraged = true;
                baseColor = new Color(1f, 0.3f, 0.05f);
                if (TopDownCamera.Instance != null) TopDownCamera.Instance.AddTrauma(0.05f);
            }
        }

        UpdateStatusVisual();

        // War Cry
        warCryCooldown -= Time.deltaTime;
        if (warCryCooldown <= 0 && !isEnraged)
        {
            warCryCooldown = WarCryInterval;
            PerformWarCry();
        }

        // Wind-up
        if (isWindingUp)
        {
            windupTimer -= Time.deltaTime;
            float flash = Mathf.PingPong(Time.time * 10f, 1f);
            foreach (var r in renderers)
                if (r != null && r.gameObject.name != "Eye" && r.gameObject.name != "BuffAura")
                    r.material.color = Color.Lerp(baseColor, Color.white, flash);

            if (windupTimer <= 0)
            {
                isWindingUp = false;
                var playerHealth = target.GetComponent<Health>();
                var playerCtrl = target.GetComponent<PlayerController>();
                if (playerHealth != null && !playerHealth.IsDead
                    && (playerCtrl == null || !playerCtrl.isInvulnerable)
                    && Vector3.Distance(transform.position, target.position) < AttackRange + 0.5f)
                {
                    playerHealth.TakeDamage(AttackDamage);
                    SFXSystem.Play(SFXSystem.SFXType.Hit, transform.position);
                    GameFeel.ApplyKnockback(target, transform.position, 2f);
                }
            }
            return;
        }

        // Movement
        float moveSpeedFinal = moveSpeed * speedMult * (isEnraged ? 1.5f : 1f);

        if (dist <= AttackRange)
        {
            // Melee attack
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0)
            {
                attackTimer = isEnraged ? AttackCooldown * 0.6f : AttackCooldown;
                isWindingUp = true;
                windupTimer = 0.3f;
            }
        }
        else if (isEnraged)
        {
            // Chase player aggressively
            Vector3 moveDir = (target.position - transform.position).normalized;
            moveDir.y = 0;
            rb.MovePosition(transform.position + moveDir * moveSpeedFinal * Time.deltaTime);
            transform.rotation = Quaternion.LookRotation(moveDir);
        }
        else
        {
            // Stay near allies, maintain moderate distance from player
            Vector3 moveDir = Vector3.zero;

            if (dist < 5f)
                moveDir = (transform.position - target.position).normalized;
            else if (dist > 10f)
                moveDir = (target.position - transform.position).normalized * 0.5f;

            // Gravitate toward nearby allies
            var nearestAlly = FindNearestAlly();
            if (nearestAlly != null)
            {
                float allyDist = Vector3.Distance(transform.position, nearestAlly.position);
                if (allyDist > 3f)
                    moveDir += (nearestAlly.position - transform.position).normalized * 0.6f;
            }

            if (moveDir.sqrMagnitude > 0.01f)
            {
                moveDir.y = 0;
                moveDir.Normalize();
                rb.MovePosition(transform.position + moveDir * moveSpeedFinal * Time.deltaTime);
                transform.rotation = Quaternion.LookRotation(moveDir);
            }
        }
    }

    void PerformWarCry()
    {
        int buffed = 0;
        var allHealth = FindObjectsByType<Health>(FindObjectsSortMode.None);
        foreach (var h in allHealth)
        {
            if (h.gameObject == gameObject) continue;
            if (h.IsDead) continue;
            if (h.GetComponent<PlayerController>() != null) continue;

            float d = Vector3.Distance(transform.position, h.transform.position);
            if (d > BuffRadius) continue;

            // Apply buff via EnemyBuff component
            var buff = h.gameObject.GetComponent<EnemyBuff>();
            if (buff == null) buff = h.gameObject.AddComponent<EnemyBuff>();
            buff.ApplyBuff(BuffDamageMult, BuffSpeedMult, BuffDuration);
            buffed++;
        }

        if (buffed > 0)
        {
            // War Cry VFX: expanding ring
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.Destroy(ring.GetComponent<CapsuleCollider>());
            ring.transform.position = transform.position + Vector3.up * 0.1f;
            ring.transform.localScale = new Vector3(0.5f, 0.02f, 0.5f);
            ring.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(1f, 0.6f, 0.1f, 0.5f), 3f);
            ring.AddComponent<ExpandRingVFX>().Init(BuffRadius * 2, 0.5f);

            SFXSystem.Play(SFXSystem.SFXType.LevelUp, transform.position, 0.5f);
            if (TopDownCamera.Instance != null) TopDownCamera.Instance.AddTrauma(0.03f);
        }
    }

    Transform FindNearestAlly()
    {
        Transform best = null;
        float bestDist = float.MaxValue;
        var allHealth = FindObjectsByType<Health>(FindObjectsSortMode.None);
        foreach (var h in allHealth)
        {
            if (h.gameObject == gameObject) continue;
            if (h.IsDead) continue;
            if (h.GetComponent<PlayerController>() != null) continue;
            float d = Vector3.Distance(transform.position, h.transform.position);
            if (d < bestDist) { bestDist = d; best = h.transform; }
        }
        return best;
    }

    void UpdateStatusVisual()
    {
        if (renderers == null) return;
        Color col = baseColor;
        if (health.IsStunned) col = health.IsFrozen ? new Color(0.6f, 0.9f, 1f) : new Color(1f, 1f, 0.3f);
        foreach (var r in renderers)
            if (r != null && r.gameObject.name != "Eye" && r.gameObject.name != "BuffAura") r.material.color = col;
    }

    void OnDestroy() { if (health != null) health.OnDeath -= OnDie; }
}

/// <summary>
/// Temporary buff applied by BufferAI's War Cry.
/// Modifies speed via Health.SpeedMultiplier and tracks damage bonus.
/// </summary>
public class EnemyBuff : MonoBehaviour
{
    public float damageMult = 1f;
    float _speedMult = 1f;
    float _timer;
    Renderer[] _renderers;
    Color[] _originalColors;
    bool _active;

    public void ApplyBuff(float dmgMult, float spdMult, float duration)
    {
        damageMult = dmgMult;
        _speedMult = spdMult;
        _timer = duration;

        if (!_active)
        {
            _renderers = GetComponentsInChildren<Renderer>();
            _originalColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
                _originalColors[i] = _renderers[i].material.color;
        }
        _active = true;
    }

    void Update()
    {
        if (!_active) return;
        _timer -= Time.deltaTime;

        // Orange tint while buffed
        if (_renderers != null)
        {
            float pulse = 0.3f + Mathf.PingPong(Time.time * 3f, 0.3f);
            foreach (var r in _renderers)
                if (r != null && r.gameObject.name != "Eye")
                    r.material.color = Color.Lerp(r.material.color, new Color(1f, 0.6f, 0.1f), pulse * Time.deltaTime * 5f);
        }

        if (_timer <= 0)
        {
            _active = false;
            damageMult = 1f;
            _speedMult = 1f;
            // Restore colors
            if (_renderers != null && _originalColors != null)
            {
                for (int i = 0; i < _renderers.Length; i++)
                    if (_renderers[i] != null && i < _originalColors.Length)
                        _renderers[i].material.color = _originalColors[i];
            }
        }
    }
}

/// <summary>
/// Simple expanding ring VFX used by Buffer's War Cry.
/// </summary>
public class ExpandRingVFX : MonoBehaviour
{
    float _targetScale;
    float _duration;
    float _timer;
    Vector3 _startScale;

    public void Init(float targetScale, float duration)
    {
        _targetScale = targetScale;
        _duration = duration;
        _startScale = transform.localScale;
    }

    void Update()
    {
        _timer += Time.deltaTime;
        float t = _timer / _duration;
        float s = Mathf.Lerp(_startScale.x, _targetScale, t);
        transform.localScale = new Vector3(s, 0.02f, s);

        var r = GetComponent<Renderer>();
        if (r != null)
        {
            var c = r.material.color;
            c.a = 1f - t;
            r.material.color = c;
        }

        if (t >= 1f) Destroy(gameObject);
    }
}
