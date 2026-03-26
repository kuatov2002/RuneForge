using UnityEngine;

/// <summary>
/// Mirror — copies player's element and shoots it back.
/// Mechanics:
/// - Copies player's active element, shoots colored projectiles
/// - PHASE SHIFT: periodically becomes translucent and immune to damage for 1.5s
///   (player must time attacks between phases)
/// - MIRROR SHIELD: projectiles that match the copied element are reflected back
/// - CLONE: at 50% HP, spawns a decoy copy that doesn't deal damage but distracts
/// </summary>
public class MirrorAI : MonoBehaviour
{
    public float moveSpeed = 2.5f;
    public float preferredDist = 5f;
    public float shootCooldown = 2.5f;
    public float projectileSpeed = 9f;
    public int attackDamage = 2;
    public Color baseColor = new Color(0.7f, 0.7f, 0.75f);

    Transform target;
    Health health;
    Rigidbody rb;
    Renderer[] renderers;
    bool isDead;
    float shootTimer;

    ElementSO copiedElement;
    Color copiedColor;

    // Phase shift
    float phaseTimer;
    float phaseCooldown = 6f;
    float phaseDuration = 1.5f;
    bool isPhased;

    // Clone
    bool hasCloned;
    GameObject cloneObj;

    void Start()
    {
        var player = FindAnyObjectByType<PlayerController>();
        if (player != null)
        {
            target = player.transform;
            var caster = player.GetComponent<SpellCaster>();
            if (caster != null)
                caster.OnOrbsChanged += OnPlayerOrbsChanged;
        }
        health = GetComponent<Health>();
        rb = GetComponent<Rigidbody>();
        renderers = GetComponentsInChildren<Renderer>();
        if (health != null) health.OnDeath += OnDie;
        copiedColor = baseColor;
        phaseTimer = phaseCooldown * 0.5f; // first phase sooner
    }

    void OnPlayerOrbsChanged()
    {
        if (isDead || target == null) return;
        var caster = target.GetComponent<SpellCaster>();
        if (caster == null || caster.rightOrb == null) return;
        copiedElement = caster.rightOrb;
        copiedColor = Color.Lerp(baseColor, copiedElement.color, 0.6f);
    }

    void OnDie()
    {
        isDead = true;
        if (cloneObj != null) Destroy(cloneObj);
        foreach (var r in renderers) if (r != null) r.material.color = Color.gray;
        Destroy(gameObject, 0.3f);
    }

    void Update()
    {
        if (isDead || target == null) return;

        // Clone at 50% HP
        if (!hasCloned && health.currentHP <= health.maxHP / 2)
        {
            hasCloned = true;
            SpawnClone();
        }

        // Phase shift
        phaseTimer -= Time.deltaTime;
        if (isPhased)
        {
            phaseDuration -= Time.deltaTime;
            // Make translucent
            foreach (var r in renderers)
                if (r != null)
                {
                    Color c = r.material.color;
                    c.a = 0.2f + Mathf.PingPong(Time.time * 3f, 0.15f);
                    r.material.color = c;
                }

            if (phaseDuration <= 0)
            {
                isPhased = false;
                phaseDuration = 1.5f;
                phaseTimer = phaseCooldown;
                // Re-solidify
                foreach (var r in renderers)
                    if (r != null) { Color c = r.material.color; c.a = 1f; r.material.color = c; }
            }
            // Still move while phased
        }
        else if (phaseTimer <= 0)
        {
            isPhased = true;
            // Flash VFX
            var flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(flash.GetComponent<SphereCollider>());
            flash.transform.position = transform.position + Vector3.up * 0.5f;
            flash.transform.localScale = Vector3.one * 0.8f;
            flash.GetComponent<Renderer>().material = ShaderCache.NewEmissive(copiedColor, 4f);
            flash.AddComponent<FlashShrink>().Init(0.3f);
        }

        UpdateVisual();
        if (health.IsStunned) return;
        float speedMult = health.SpeedMultiplier;

        Vector3 toPlayer = target.position - transform.position;
        toPlayer.y = 0;
        float dist = toPlayer.magnitude;

        // Movement: strafe around player at preferred distance
        Vector3 moveDir = Vector3.zero;
        if (dist > preferredDist + 1f) moveDir = toPlayer.normalized;
        else if (dist < preferredDist - 1f) moveDir = -toPlayer.normalized;
        else
        {
            // Strafe
            Vector3 strafe = Vector3.Cross(toPlayer.normalized, Vector3.up);
            moveDir = strafe * (Mathf.Sin(Time.time * 0.7f + GetInstanceID()) > 0 ? 1 : -1);
        }

        if (moveDir.sqrMagnitude > 0.01f)
            rb.MovePosition(transform.position + moveDir * moveSpeed * speedMult * Time.deltaTime);
        transform.rotation = Quaternion.LookRotation(toPlayer.normalized);

        // Don't shoot while phased
        if (isPhased) return;

        // Shoot
        shootTimer -= Time.deltaTime;
        if (shootTimer <= 0 && dist < 12f)
        {
            shootTimer = shootCooldown;
            Color projColor = copiedElement != null ? copiedElement.color : baseColor;

            // Shoot 2 projectiles in a slight spread
            Vector3 shootDir = toPlayer.normalized;
            for (int i = -1; i <= 1; i += 2)
            {
                Vector3 dir = Quaternion.Euler(0, i * 8f, 0) * shootDir;
                EnemyProjectile.Create(transform.position, dir, projectileSpeed, attackDamage, projColor);
            }
            SFXSystem.Play(SFXSystem.SFXType.Cast, transform.position, 0.4f);
        }
    }

    void SpawnClone()
    {
        // Create a visual decoy — same appearance, no Health, no damage
        cloneObj = new GameObject("MirrorClone");
        Vector3 offset = Vector3.Cross(
            (target.position - transform.position).normalized, Vector3.up) * 3f;
        cloneObj.transform.position = transform.position + offset;
        cloneObj.transform.rotation = transform.rotation;

        // Copy visual appearance
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        Destroy(body.GetComponent<CapsuleCollider>());
        body.transform.SetParent(cloneObj.transform, false);
        body.transform.localPosition = new Vector3(0, 0.45f, 0);
        body.transform.localScale = new Vector3(0.6f, 0.35f, 0.55f);
        body.GetComponent<Renderer>().material = ShaderCache.NewLit(copiedColor);

        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(head.GetComponent<SphereCollider>());
        head.transform.SetParent(cloneObj.transform, false);
        head.transform.localPosition = new Vector3(0, 0.9f, 0);
        head.transform.localScale = new Vector3(0.4f, 0.35f, 0.4f);
        head.GetComponent<Renderer>().material = ShaderCache.NewLit(copiedColor * 0.8f);

        // Clone just wanders randomly and disappears after 8s
        cloneObj.AddComponent<MirrorCloneBehavior>().Init(target, moveSpeed);
        Destroy(cloneObj, 8f);

        SFXSystem.Play(SFXSystem.SFXType.DualCast, transform.position, 0.4f);
    }

    void UpdateVisual()
    {
        if (renderers == null) return;
        Color col = copiedColor;
        if (health.IsStunned) col = health.IsFrozen ? new Color(0.6f, 0.9f, 1f) : new Color(1f, 1f, 0.3f);
        if (!isPhased)
            foreach (var r in renderers)
                if (r != null && r.gameObject.name != "Eye") r.material.color = col;
    }

    /// <summary>Called by projectiles to check if they should be reflected.</summary>
    public bool ShouldReflect(ElementSO projectileElement)
    {
        if (isPhased) return false; // phased = immune but can't reflect
        // Reflect if projectile matches copied element
        return copiedElement != null && projectileElement != null &&
               copiedElement.elementType == projectileElement.elementType;
    }

    void OnDestroy()
    {
        if (health != null) health.OnDeath -= OnDie;
        if (cloneObj != null) Destroy(cloneObj);
        if (target != null)
        {
            var caster = target.GetComponent<SpellCaster>();
            if (caster != null) caster.OnOrbsChanged -= OnPlayerOrbsChanged;
        }
    }
}

/// <summary>Mirror clone: wanders toward player but deals no damage.</summary>
public class MirrorCloneBehavior : MonoBehaviour
{
    Transform _target;
    float _speed;

    public void Init(Transform target, float speed)
    {
        _target = target;
        _speed = speed;
    }

    void Update()
    {
        if (_target == null) return;
        Vector3 toTarget = _target.position - transform.position;
        toTarget.y = 0;
        if (toTarget.magnitude > 2f)
            transform.position += toTarget.normalized * _speed * 0.7f * Time.deltaTime;
        transform.rotation = Quaternion.LookRotation(toTarget.normalized);
    }
}
