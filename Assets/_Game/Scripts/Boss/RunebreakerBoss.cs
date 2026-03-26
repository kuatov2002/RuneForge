using UnityEngine;

public class RunebreakerBoss : MonoBehaviour
{
    public float moveSpeed = 3f;
    public float meleeRange = 2f;
    public float meleeCooldown = 1.5f;
    public int meleeDamage = 4;
    public float rangedCooldown = 2.5f;
    public float rangedSpeed = 8f;
    public int rangedDamage = 2;
    public Color baseColor = new Color(0.6f, 0.1f, 0.1f);

    Transform target;
    Health health;
    Rigidbody rb;
    Renderer[] renderers;
    bool isDead;
    float meleeTimer;
    float rangedTimer;
    int phase = 1;
    SpellCaster playerCaster;

    void Start()
    {
        var player = FindAnyObjectByType<PlayerController>();
        if (player != null)
        {
            target = player.transform;
            playerCaster = player.GetComponent<SpellCaster>();
        }
        health = GetComponent<Health>();
        rb = GetComponent<Rigidbody>();
        renderers = GetComponentsInChildren<Renderer>();

        // Boss immunity: Earth (stone creature)
        var elemData = gameObject.GetComponent<EnemyElementData>();
        if (elemData == null) elemData = gameObject.AddComponent<EnemyElementData>();
        elemData.AssignBossImmunity(ElementType.Earth);

        if (health != null)
        {
            health.OnDeath += () =>
            {
                isDead = true;
                Destroy(gameObject, 0.5f);
            };
            health.OnHPChanged += OnHPChanged;
        }
    }

    void OnHPChanged(int cur, int max)
    {
        float ratio = (float)cur / max;
        if (ratio < 0.33f && phase < 3)
        {
            phase = 3;
            ForceOverheatRandomSlot();
            UpdateVisual();
        }
        else if (ratio < 0.66f && phase < 2)
        {
            phase = 2;
            ForceOverheatRandomSlot();
            UpdateVisual();
        }
    }

    void ForceOverheatRandomSlot()
    {
        if (playerCaster == null) return;

        // Pick a random equipped element slot to overheat
        int slotCount = playerCaster.equippedElements.Length;
        if (slotCount == 0) return;

        // Try to find a non-overheated slot
        int attempts = 0;
        int slot;
        do
        {
            slot = Random.Range(0, slotCount);
            attempts++;
        }
        while (playerCaster.IsOverheated(slot) && attempts < slotCount * 2);

        playerCaster.ForceOverheat(slot);

        // VFX: shockwave to indicate element disruption
        CreateDisableVFX();
    }

    void Update()
    {
        if (isDead || target == null) return;
        if (health.IsStunned) return;
        float speedMult = health.SpeedMultiplier;

        Vector3 toPlayer = target.position - transform.position;
        toPlayer.y = 0;
        float dist = toPlayer.magnitude;
        Vector3 dir = toPlayer.normalized;

        // Move toward player
        if (dist > meleeRange)
        {
            rb.MovePosition(transform.position + dir * moveSpeed * speedMult * Time.deltaTime);
        }
        transform.rotation = Quaternion.LookRotation(dir);

        // Melee attack
        meleeTimer -= Time.deltaTime;
        if (dist <= meleeRange && meleeTimer <= 0)
        {
            meleeTimer = meleeCooldown;
            DoMelee();
        }

        // Ranged attack (when far)
        rangedTimer -= Time.deltaTime;
        if (dist > meleeRange + 1f && rangedTimer <= 0)
        {
            rangedTimer = rangedCooldown;
            FireRuneBolt();
        }
    }

    void DoMelee()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, meleeRange);
        foreach (var h in hits)
        {
            var pc = h.GetComponent<PlayerController>();
            if (pc == null || pc.isInvulnerable) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead) hp.TakeDamage(meleeDamage);
        }

        // Slam VFX
        var vfx = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(vfx.GetComponent<CapsuleCollider>());
        vfx.transform.position = transform.position + Vector3.up * 0.05f;
        vfx.transform.localScale = new Vector3(meleeRange * 2, 0.05f, meleeRange * 2);
        var mat = ShaderCache.NewEmissive(new Color(0.8f, 0.1f, 0.1f), 2f);
        vfx.GetComponent<Renderer>().material = mat;
        Destroy(vfx, 0.2f);
    }

    void FireRuneBolt()
    {
        Vector3 dir = (target.position + Vector3.up * 0.5f - transform.position).normalized;
        EnemyProjectile.Create(transform.position + Vector3.up * 1f + transform.forward * 0.5f,
            dir, rangedSpeed, rangedDamage, new Color(0.8f, 0.1f, 0.1f), null);
    }

    void UpdateVisual()
    {
        Color c = phase switch
        {
            2 => new Color(0.8f, 0.2f, 0.0f),
            3 => new Color(1f, 0.1f, 0.1f),
            _ => baseColor
        };
        foreach (var r in renderers)
            if (r != null) r.material.color = c;
    }

    void CreateDisableVFX()
    {
        // Shockwave effect
        var vfx = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(vfx.GetComponent<SphereCollider>());
        vfx.transform.position = transform.position + Vector3.up * 1f;
        vfx.transform.localScale = Vector3.one * 3f;
        var mat = ShaderCache.NewEmissive(new Color(0.9f, 0.1f, 0.1f, 0.5f), 4f);
        mat.SetColor("_EmissionColor", new Color(1f, 0f, 0f) * 4f);
        vfx.GetComponent<Renderer>().material = mat;
        Destroy(vfx, 0.4f);
    }
}
