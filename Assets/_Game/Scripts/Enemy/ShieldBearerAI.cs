using UnityEngine;

/// <summary>
/// Shield Bearer — tanky enemy that blocks frontal damage.
/// Mechanics:
/// - SHIELD BLOCK: blocks projectiles from the front (90 degree arc)
/// - SHIELD BASH: charges forward with shield, heavy knockback
/// - RALLY CRY: periodically buffs nearby enemies with +30% speed for 3s
///   (visual: nearby enemies flash gold briefly — creates priority target)
/// - SHIELD THROW: throws shield like a boomerang (can't block during this)
///   Shield returns after 1.5s, during which ShieldBearer is vulnerable from all sides
/// </summary>
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

    // Wind-up
    const float WindupDuration = 0.4f;
    float windupTimer;
    bool isWindingUp;

    // Shield bash
    const float BashCooldown = 5f;
    const float BashWindup = 0.6f;
    const float BashRange = 2f;
    const int BashDamage = 3;
    float bashTimer;
    float bashWindupTimer;
    bool isBashWindup;

    // Shield
    GameObject shieldObj;
    bool shieldActive = true;

    // Rally cry
    float rallyCooldown = 10f;
    float rallyTimer;

    // Shield throw
    float throwCooldown = 8f;
    float throwTimer;
    bool shieldThrown;
    float shieldReturnTimer;
    GameObject thrownShieldObj;

    void Start()
    {
        var player = FindAnyObjectByType<PlayerController>();
        if (player != null) target = player.transform;
        health = GetComponent<Health>();
        rb = GetComponent<Rigidbody>();
        renderers = GetComponentsInChildren<Renderer>();
        if (health != null) health.OnDeath += OnDie;

        var t = transform.Find("Shield");
        if (t != null) shieldObj = t.gameObject;

        rallyTimer = rallyCooldown * 0.3f; // first rally sooner
        throwTimer = throwCooldown * 0.5f;
    }

    void OnDie()
    {
        isDead = true;
        if (thrownShieldObj != null) Destroy(thrownShieldObj);
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

        if (toPlayer.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(toPlayer.normalized);

        // Shield return timer
        if (shieldThrown)
        {
            shieldReturnTimer -= Time.deltaTime;
            if (shieldReturnTimer <= 0)
            {
                shieldThrown = false;
                shieldActive = true;
                if (shieldObj != null) shieldObj.SetActive(true);
                if (thrownShieldObj != null) { Destroy(thrownShieldObj); thrownShieldObj = null; }

                // Catch VFX
                SFXSystem.Play(SFXSystem.SFXType.Hit, transform.position, 0.3f);
            }
        }

        // Rally cry
        rallyTimer -= Time.deltaTime;
        if (rallyTimer <= 0)
        {
            rallyTimer = rallyCooldown;
            RallyCry();
        }

        // Shield throw
        throwTimer -= Time.deltaTime;
        if (throwTimer <= 0 && !shieldThrown && dist < 8f && dist > 3f)
        {
            throwTimer = throwCooldown;
            ThrowShield(toPlayer.normalized);
        }

        // Shield bash
        if (isBashWindup)
        {
            bashWindupTimer -= Time.deltaTime;
            float pulse = Mathf.PingPong(Time.time * 10f, 1f);
            if (shieldObj != null && shieldObj.activeSelf)
            {
                var sr = shieldObj.GetComponent<Renderer>();
                if (sr != null) sr.material.color = Color.Lerp(new Color(0.5f, 0.5f, 0.6f), Color.red, pulse);
            }

            float progress = 1f - (bashWindupTimer / BashWindup);
            if (progress > 0.7f)
                rb.MovePosition(transform.position + transform.forward * 6f * Time.deltaTime);

            if (bashWindupTimer <= 0)
            {
                isBashWindup = false;
                if (shieldObj != null)
                {
                    var sr = shieldObj.GetComponent<Renderer>();
                    if (sr != null) sr.material = ShaderCache.NewMetal(new Color(0.5f, 0.5f, 0.6f));
                }
                float finalDist = Vector3.Distance(transform.position, target.position);
                if (finalDist <= BashRange * 1.2f)
                {
                    var playerHealth = target.GetComponent<Health>();
                    var playerCtrl = target.GetComponent<PlayerController>();
                    if (playerHealth != null && !playerHealth.IsDead
                        && (playerCtrl == null || !playerCtrl.isInvulnerable))
                    {
                        playerHealth.TakeDamage(BashDamage);
                        GameFeel.ApplyKnockback(target, target.position + transform.forward, 6f);
                    }
                }
                if (TopDownCamera.Instance != null) TopDownCamera.Instance.AddTrauma(0.15f);
            }
            return;
        }

        // Normal attack
        if (isWindingUp)
        {
            windupTimer -= Time.deltaTime;
            float pulse = Mathf.PingPong(Time.time * 10f, 1f);
            foreach (var r in renderers)
                if (r != null && r.gameObject.name != "Eye" && r.gameObject.name != "Shield")
                    r.material.color = Color.Lerp(baseColor, new Color(1f, 0.5f, 0.2f), pulse);

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

        bashTimer -= Time.deltaTime;

        if (dist > attackRange)
        {
            rb.MovePosition(transform.position + toPlayer.normalized * moveSpeed * speedMult * Time.deltaTime);
        }
        else
        {
            if (bashTimer <= 0 && shieldActive)
            {
                bashTimer = BashCooldown;
                isBashWindup = true;
                bashWindupTimer = BashWindup;
                return;
            }

            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0)
            {
                attackTimer = attackCooldown;
                isWindingUp = true;
                windupTimer = WindupDuration;
            }
        }
    }

    void RallyCry()
    {
        // Buff nearby enemies with speed boost
        Collider[] nearby = Physics.OverlapSphere(transform.position, 5f);
        int buffed = 0;
        foreach (var c in nearby)
        {
            if (c.gameObject == gameObject) continue;
            var hp = c.GetComponent<Health>();
            if (hp == null || hp.IsDead) continue;
            if (c.GetComponent<PlayerController>() != null) continue;

            // Apply speed buff via a temporary component
            var buff = c.gameObject.GetComponent<RallyBuff>();
            if (buff == null) buff = c.gameObject.AddComponent<RallyBuff>();
            buff.Apply(3f);
            buffed++;
        }

        if (buffed > 0)
        {
            // Rally VFX: expanding golden ring
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(ring.GetComponent<CapsuleCollider>());
            ring.transform.position = transform.position + Vector3.up * 0.1f;
            ring.transform.localScale = new Vector3(0.5f, 0.02f, 0.5f);
            ring.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(1f, 0.85f, 0.2f), 3f);
            ring.AddComponent<DeathRingEffect>().Init(10f, 0.5f);

            SFXSystem.Play(SFXSystem.SFXType.LevelUp, transform.position, 0.4f);
        }
    }

    void ThrowShield(Vector3 dir)
    {
        shieldThrown = true;
        shieldActive = false;
        shieldReturnTimer = 1.8f;
        if (shieldObj != null) shieldObj.SetActive(false);

        // Create thrown shield projectile
        thrownShieldObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        thrownShieldObj.name = "ThrownShield";
        thrownShieldObj.transform.position = transform.position + dir * 0.5f + Vector3.up * 0.5f;
        thrownShieldObj.transform.localScale = new Vector3(0.6f, 0.6f, 0.08f);
        thrownShieldObj.GetComponent<Renderer>().material = ShaderCache.NewMetal(new Color(0.5f, 0.5f, 0.6f));

        var col = thrownShieldObj.GetComponent<BoxCollider>();
        col.isTrigger = true;

        var srb = thrownShieldObj.AddComponent<Rigidbody>();
        srb.useGravity = false;
        srb.isKinematic = true;

        var tb = thrownShieldObj.AddComponent<ThrownShieldBehavior>();
        tb.Init(transform, dir, attackDamage + 1);
    }

    /// <summary>Check if a projectile should be blocked by the shield.</summary>
    public bool IsBlockedFromDirection(Vector3 attackOrigin)
    {
        if (!shieldActive) return false; // Shield is thrown, can't block
        Vector3 toAttacker = attackOrigin - transform.position;
        toAttacker.y = 0;
        float angle = Vector3.Angle(transform.forward, toAttacker);
        return angle < 45f;
    }

    void UpdateStatusVisual()
    {
        if (renderers == null) return;
        Color col = baseColor;
        if (health.IsStunned) col = health.IsFrozen ? new Color(0.6f, 0.9f, 1f) : new Color(1f, 1f, 0.3f);
        foreach (var r in renderers)
            if (r != null && r.gameObject.name != "Eye" && r.gameObject.name != "Shield")
                r.material.color = col;
    }

    void OnDestroy()
    {
        if (health != null) health.OnDeath -= OnDie;
        if (thrownShieldObj != null) Destroy(thrownShieldObj);
    }
}

/// <summary>Thrown shield: flies in an arc, damages player on hit.</summary>
public class ThrownShieldBehavior : MonoBehaviour
{
    Transform _owner;
    Vector3 _dir;
    int _damage;
    float _timer;
    float _spinSpeed = 720f;
    float _speed = 10f;
    bool _returning;

    public void Init(Transform owner, Vector3 dir, int damage)
    {
        _owner = owner;
        _dir = dir;
        _damage = damage;
    }

    void Update()
    {
        _timer += Time.deltaTime;

        // Spin
        transform.Rotate(0, _spinSpeed * Time.deltaTime, 0);

        if (_timer < 0.8f)
        {
            // Fly outward
            transform.position += _dir * _speed * Time.deltaTime;
        }
        else
        {
            // Return to owner
            _returning = true;
            if (_owner != null)
            {
                Vector3 toOwner = _owner.position + Vector3.up * 0.5f - transform.position;
                transform.position += toOwner.normalized * _speed * 1.2f * Time.deltaTime;
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        var pc = other.GetComponent<PlayerController>();
        if (pc == null) return;
        if (pc.isInvulnerable) return;
        var hp = other.GetComponent<Health>();
        if (hp != null && !hp.IsDead)
        {
            hp.TakeDamage(_damage);
            GameFeel.ApplyKnockback(other.transform, transform.position, 3f);
        }
    }
}

/// <summary>Temporary speed buff from ShieldBearer rally cry.</summary>
public class RallyBuff : MonoBehaviour
{
    float _timer;
    Renderer[] _renderers;
    bool _active;

    public void Apply(float duration)
    {
        _timer = duration;
        _active = true;
        _renderers = GetComponentsInChildren<Renderer>();
    }

    void Update()
    {
        if (!_active) return;
        _timer -= Time.deltaTime;

        // Gold flash during buff
        float flash = Mathf.PingPong(Time.time * 4f, 1f) * 0.3f;
        if (_renderers != null)
            foreach (var r in _renderers)
                if (r != null && r.gameObject.name != "Eye")
                {
                    Color c = r.material.color;
                    r.material.color = Color.Lerp(c, new Color(1f, 0.85f, 0.2f), flash);
                }

        if (_timer <= 0)
        {
            _active = false;
            Destroy(this);
        }
    }

    /// <summary>Speed multiplier while buff is active.</summary>
    public float SpeedMultiplier => _active ? 1.3f : 1f;
}
