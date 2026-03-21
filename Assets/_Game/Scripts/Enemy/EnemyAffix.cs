using UnityEngine;

/// <summary>
/// Elite enemy affix system. Applied to enemies on floor 3+.
/// Each affix modifies behavior visually and mechanically.
/// </summary>
public enum AffixType
{
    None,
    Berserker,    // Speeds up below 50% HP, +25% damage
    Regenerating, // Heals 2 HP/s
    Teleporting,  // Blinks to random position every 5s
    Shielded,     // Takes 50% less damage, shield visual
    ExplosiveOnDeath // AoE damage + fire on death
}

public class EnemyAffix : MonoBehaviour
{
    public AffixType affixType;

    Health _health;
    Rigidbody _rb;
    float _timer;
    bool _berserkActive;
    GameObject _shieldVisual;
    GameObject _affixIndicator;
    float _originalSpeed;
    bool _dead;

    public void Init(AffixType type)
    {
        affixType = type;
        _health = GetComponent<Health>();
        _rb = GetComponent<Rigidbody>();

        if (_health != null)
            _health.OnDeath += OnDeath;

        // Apply passive effects
        switch (affixType)
        {
            case AffixType.Shielded:
                // Increase max HP by 50% to simulate damage reduction
                if (_health != null)
                {
                    int bonus = _health.maxHP / 2;
                    _health.maxHP += bonus;
                    _health.currentHP += bonus;
                }
                CreateShieldVisual();
                break;
        }

        CreateAffixIndicator();
    }

    void Update()
    {
        if (_dead || _health == null || _health.IsDead) return;

        switch (affixType)
        {
            case AffixType.Berserker:
                UpdateBerserker();
                break;
            case AffixType.Regenerating:
                UpdateRegenerating();
                break;
            case AffixType.Teleporting:
                UpdateTeleporting();
                break;
        }

        // Spin affix indicator
        if (_affixIndicator != null)
            _affixIndicator.transform.Rotate(0, 120f * Time.deltaTime, 0);
    }

    void UpdateBerserker()
    {
        bool lowHP = _health.currentHP <= _health.maxHP / 2;
        if (lowHP && !_berserkActive)
        {
            _berserkActive = true;
            // Speed up: modify the AI's moveSpeed via reflection-free approach
            var shambler = GetComponent<ShamblerAI>();
            if (shambler != null) shambler.moveSpeed *= 1.6f;
            var swarm = GetComponent<SwarmAI>();
            if (swarm != null) swarm.moveSpeed *= 1.6f;
            var archer = GetComponent<ArcherAI>();
            if (archer != null) { archer.moveSpeed *= 1.4f; archer.shootCooldown *= 0.6f; }
            var shieldBearer = GetComponent<ShieldBearerAI>();
            if (shieldBearer != null) shieldBearer.moveSpeed *= 1.5f;

            // Visual: red pulsing aura
            var aura = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(aura.GetComponent<SphereCollider>());
            aura.name = "BerserkAura";
            aura.transform.parent = transform;
            aura.transform.localPosition = Vector3.up * 0.5f;
            aura.transform.localScale = Vector3.one * 1.2f;
            var mat = ShaderCache.NewEmissive(new Color(1f, 0.2f, 0.1f), 2f);
            mat.SetFloat("_Surface", 1); // transparent
            aura.GetComponent<Renderer>().material = mat;
        }
    }

    void UpdateRegenerating()
    {
        _timer += Time.deltaTime;
        if (_timer >= 0.5f)
        {
            _timer = 0;
            _health.Heal(1);
        }
    }

    void UpdateTeleporting()
    {
        _timer += Time.deltaTime;
        if (_timer >= 5f)
        {
            _timer = 0;
            // Blink to random position near player
            var player = FindAnyObjectByType<PlayerController>();
            if (player == null) return;

            // VFX at old position
            SpawnTeleportVFX(transform.position);

            Vector3 offset = new Vector3(Random.Range(-4f, 4f), 0, Random.Range(-4f, 4f));
            Vector3 newPos = player.transform.position + offset;
            newPos.y = 0;
            transform.position = newPos;

            // VFX at new position
            SpawnTeleportVFX(transform.position);
        }
    }

    void SpawnTeleportVFX(Vector3 pos)
    {
        var vfx = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(vfx.GetComponent<SphereCollider>());
        vfx.transform.position = pos + Vector3.up * 0.5f;
        vfx.transform.localScale = Vector3.one * 0.8f;
        vfx.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(0.5f, 0.1f, 0.8f), 4f);
        Object.Destroy(vfx, 0.3f);
    }

    void OnDeath()
    {
        _dead = true;

        if (affixType == AffixType.ExplosiveOnDeath)
        {
            // AoE explosion
            float radius = 3.5f;
            int damage = 8;

            Collider[] hits = Physics.OverlapSphere(transform.position, radius);
            foreach (var h in hits)
            {
                if (h.gameObject == gameObject) continue;
                var hp = h.GetComponent<Health>();
                if (hp != null && !hp.IsDead)
                    hp.TakeDamage(damage);
            }

            // VFX
            GameFeel.SpawnDeathVFX(transform.position, new Color(1f, 0.4f, 0.1f));

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.Destroy(ring.GetComponent<CapsuleCollider>());
            ring.transform.position = transform.position + Vector3.up * 0.1f;
            ring.transform.localScale = new Vector3(1f, 0.03f, 1f);
            ring.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(1f, 0.4f, 0.1f), 4f);
            ring.AddComponent<DeathRingEffect>().Init(radius * 2, 0.5f);

            if (TopDownCamera.Instance != null)
                TopDownCamera.Instance.AddTrauma(0.3f);
        }
    }

    void CreateShieldVisual()
    {
        _shieldVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(_shieldVisual.GetComponent<SphereCollider>());
        _shieldVisual.name = "AffixShield";
        _shieldVisual.transform.parent = transform;
        _shieldVisual.transform.localPosition = Vector3.up * 0.6f;
        _shieldVisual.transform.localScale = Vector3.one * 1.4f;
        _shieldVisual.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(0.3f, 0.6f, 1f), 1f);
    }

    void CreateAffixIndicator()
    {
        Color indicatorColor = affixType switch
        {
            AffixType.Berserker => new Color(1f, 0.2f, 0.1f),
            AffixType.Regenerating => new Color(0.2f, 1f, 0.3f),
            AffixType.Teleporting => new Color(0.5f, 0.1f, 0.8f),
            AffixType.Shielded => new Color(0.3f, 0.6f, 1f),
            AffixType.ExplosiveOnDeath => new Color(1f, 0.5f, 0.1f),
            _ => Color.white
        };

        // Small diamond above the enemy's head
        _affixIndicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.Destroy(_affixIndicator.GetComponent<BoxCollider>());
        _affixIndicator.name = "AffixIcon";
        _affixIndicator.transform.parent = transform;
        _affixIndicator.transform.localPosition = Vector3.up * 1.6f;
        _affixIndicator.transform.localScale = Vector3.one * 0.15f;
        _affixIndicator.transform.localRotation = Quaternion.Euler(45, 0, 45);
        _affixIndicator.GetComponent<Renderer>().material = ShaderCache.NewEmissive(indicatorColor, 3f);
    }

    /// <summary>Roll an affix for an enemy based on floor. Returns None if not elite.</summary>
    public static AffixType RollAffix(int floor)
    {
        if (floor < 3) return AffixType.None;

        // Chance increases with floor: 20% at floor 3, 35% at 4, 50% at 5
        float chance = 0.1f + floor * 0.08f;
        if (Random.value > chance) return AffixType.None;

        // Pick random affix
        AffixType[] pool = { AffixType.Berserker, AffixType.Regenerating, AffixType.Teleporting,
                            AffixType.Shielded, AffixType.ExplosiveOnDeath };
        return pool[Random.Range(0, pool.Length)];
    }

    void OnDestroy()
    {
        if (_health != null) _health.OnDeath -= OnDeath;
    }
}
