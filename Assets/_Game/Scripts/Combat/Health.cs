using UnityEngine;
using System;
using Object = UnityEngine.Object;

public class Health : MonoBehaviour
{
    public int maxHP = 5;
    [HideInInspector] public int currentHP;

    public event Action<int, int> OnHPChanged;
    public event Action OnDeath;

    /// <summary>Fired on every hit: (damageAmount, worldPosition, wasKillingBlow)</summary>
    public event Action<int, Vector3, bool> OnDamaged;

    // Freeze (Deep Freeze spell)
    float freezeTimer;

    // Stun
    float stunTimer;

    // Slow (Magma)
    float magmaSlowTimer;

    // Stun VFX: orbiting stars
    GameObject stunVFXRoot;

    // Magma slow visual state
    bool magmaSlowTinted;
    Color[] preMagmaEmission;

    bool isDead;

    /// <summary>Second Wind: survive lethal hits, set to 1 HP instead. Decrements each use.</summary>
    [HideInInspector] public int secondWindCharges;

    public bool IsDead => isDead;
    public bool IsFrozen => freezeTimer > 0;
    public bool IsStunned => stunTimer > 0 || freezeTimer > 0;

    public float SpeedMultiplier
    {
        get
        {
            if (stunTimer > 0 || freezeTimer > 0) return 0f;
            if (magmaSlowTimer > 0) return 0.3f;
            var es = GetComponent<ElementalStatus>();
            if (es != null) return es.GetSpeedMultiplier();
            return 1f;
        }
    }

    void Awake()
    {
        currentHP = maxHP;
    }

    // Player hit recovery
    float hitRecoveryTimer;
    const float HitRecoveryDuration = 0.3f;
    public bool IsInHitRecovery => hitRecoveryTimer > 0;

    /// <summary>Grant temporary damage immunity (used by Siphon Shield upgrade).</summary>
    public void GrantImmunity(float duration)
    {
        hitRecoveryTimer = Mathf.Max(hitRecoveryTimer, duration);
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;

        // Player hit recovery: immune to damage during recovery window
        var pc = GetComponent<PlayerController>();
        bool isPlayer = pc != null;
        if (isPlayer && hitRecoveryTimer > 0) return;
        if (isPlayer && pc.isInvulnerable) return;

        int dmg = Mathf.Max(1, Mathf.CeilToInt(amount));

        // Relic: modify incoming damage (Shield blocks first hit per room)
        var relicMgr = GetComponent<RelicManager>();
        if (relicMgr != null)
            dmg = relicMgr.ModifyIncomingDamage(dmg);
        if (dmg <= 0) return;

        currentHP -= dmg;
        if (currentHP < 0) currentHP = 0;

        // Second Wind: survive lethal hit, set to 1 HP
        bool killed = currentHP <= 0;
        if (killed && isPlayer && secondWindCharges > 0)
        {
            secondWindCharges--;
            currentHP = 1;
            killed = false;
        }

        OnHPChanged?.Invoke(currentHP, maxHP);

        // Player: hit recovery i-frames + knockback
        if (isPlayer)
        {
            if (!killed)
            {
                hitRecoveryTimer = HitRecoveryDuration;
                GameFeel.ApplyKnockback(transform, transform.position + transform.forward, 2f);
            }
            if (TopDownCamera.Instance != null)
                TopDownCamera.Instance.AddTrauma(0.4f);
            if (GameFeel.Instance != null)
                GameFeel.Instance.Hitstop(0.04f);
        }

        // Juice: floating damage number + hitstop + knockback
        Vector3 hitPos = transform.position + Vector3.up * 1.2f;
        OnDamaged?.Invoke(dmg, hitPos, killed);

        if (!isPlayer && GameFeel.Instance != null)
        {
            if (dmg >= 3)
                GameFeel.Instance.Hitstop(dmg >= 8 ? 0.05f : 0.03f);
            GameFeel.ApplyKnockback(transform, transform.position - transform.forward, dmg * 0.7f);

            // Hit impact particles on every enemy hit
            Color hitColor = Color.white;
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0 && renderers[0] != null)
                hitColor = renderers[0].material.color;
            GameFeel.SpawnHitParticles(transform.position + Vector3.up * 0.5f, hitColor, dmg >= 5 ? 1.3f : 0.8f, dmg);
        }

        // SFX
        if (isPlayer)
            SFXSystem.Play(SFXSystem.SFXType.PlayerHit, transform.position);
        else
            SFXSystem.Play(dmg >= 8 ? SFXSystem.SFXType.CritHit : SFXSystem.SFXType.Hit, transform.position);

        if (killed)
        {
            isDead = true;
            if (!isPlayer)
            {
                Color deathColor = Color.white;
                var renderers = GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0 && renderers[0] != null)
                    deathColor = renderers[0].material.color;
                GameFeel.SpawnDeathVFX(transform.position, deathColor);
                SFXSystem.Play(SFXSystem.SFXType.EnemyDeath, transform.position);
                if (TopDownCamera.Instance != null)
                    TopDownCamera.Instance.AddTrauma(0.15f);
            }
            OnDeath?.Invoke();
        }
    }

    /// <summary>Take damage with element type — checks weakness/immunity and applies elemental status.</summary>
    public void TakeDamage(float amount, ElementType element)
    {
        var elemData = GetComponent<EnemyElementData>();
        if (elemData != null)
        {
            if (elemData.IsImmuneToElement(element))
            {
                // Show "IMMUNE" feedback
                Vector3 hitPos = transform.position + Vector3.up * 1.2f;
                OnDamaged?.Invoke(0, hitPos, false);
                return;
            }
            amount = elemData.ModifyDamage(amount, element);
        }

        // Elemental status: apply status and check for reactions
        var elemStatus = GetComponent<ElementalStatus>();
        if (elemStatus != null)
        {
            float reactionMult = elemStatus.ApplyElement(element, amount);
            amount *= reactionMult;
            amount *= elemStatus.GetDamageAmplifier();
        }

        TakeDamage(amount);
    }

    /// <summary>Force-fire the HP changed event.</summary>
    public void InvokeHPChanged() => OnHPChanged?.Invoke(currentHP, maxHP);

    public void Heal(int amount)
    {
        if (isDead || amount <= 0) return;
        currentHP = Mathf.Min(currentHP + amount, maxHP);
        OnHPChanged?.Invoke(currentHP, maxHP);
    }

    // Store pre-freeze emission colors so we can restore them
    Color[] preFreezeEmission;

    /// <summary>Freeze enemy completely (Deep Freeze spell).</summary>
    public void ApplyFreeze(float duration)
    {
        bool wasAlreadyFrozen = freezeTimer > 0;
        freezeTimer = Mathf.Max(freezeTimer, duration);

        // Store original emission colors before freeze (only on first freeze)
        var renderers = GetComponentsInChildren<Renderer>();
        if (!wasAlreadyFrozen)
        {
            preFreezeEmission = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].material != null
                    && renderers[i].material.HasProperty("_EmissionColor"))
                    preFreezeEmission[i] = renderers[i].material.GetColor("_EmissionColor");
            }
        }

        // Visual: blue tint
        foreach (var r in renderers)
        {
            if (r != null && r.material != null && r.material.HasProperty("_EmissionColor"))
                r.material.SetColor("_EmissionColor", new Color(0.3f, 0.6f, 1f) * 2f);
        }
    }

    /// <summary>Stun enemy (Rubble, Geyser).</summary>
    public void ApplyStun(float duration)
    {
        stunTimer = Mathf.Max(stunTimer, duration);

        // Spawn orbiting stars VFX if not already present
        if (stunVFXRoot == null && GetComponent<PlayerController>() == null)
        {
            stunVFXRoot = new GameObject("StunStars");
            stunVFXRoot.transform.SetParent(transform, false);
            stunVFXRoot.transform.localPosition = new Vector3(0, 1.2f, 0);
            for (int i = 0; i < 3; i++)
            {
                var star = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.Destroy(star.GetComponent<SphereCollider>());
                star.name = "StunStar";
                star.transform.SetParent(stunVFXRoot.transform, false);
                float angle = i * 120f * Mathf.Deg2Rad;
                star.transform.localPosition = new Vector3(Mathf.Cos(angle) * 0.3f, 0, Mathf.Sin(angle) * 0.3f);
                star.transform.localScale = Vector3.one * 0.08f;
                star.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(1f, 0.9f, 0.2f), 4f);
            }
        }
    }

    /// <summary>Magma slow — very heavy, clears when leaving pool.</summary>
    public void ApplyMagmaSlow()
    {
        magmaSlowTimer = 0.3f; // Refreshed each physics tick while in pool

        // Apply orange tint (like freeze does blue)
        if (!magmaSlowTinted && GetComponent<PlayerController>() == null)
        {
            var renderers = GetComponentsInChildren<Renderer>();
            preMagmaEmission = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].material != null
                    && renderers[i].material.HasProperty("_EmissionColor"))
                    preMagmaEmission[i] = renderers[i].material.GetColor("_EmissionColor");
            }
            foreach (var r in renderers)
            {
                if (r != null && r.material != null && r.material.HasProperty("_EmissionColor"))
                    r.material.SetColor("_EmissionColor", new Color(1f, 0.5f, 0.1f) * 1.5f);
            }
            magmaSlowTinted = true;
        }
    }

    void Update()
    {
        if (isDead) return;

        // Hit recovery timer
        if (hitRecoveryTimer > 0)
        {
            hitRecoveryTimer -= Time.deltaTime;
            var renderers = GetComponentsInChildren<Renderer>();
            bool visible = Mathf.PingPong(Time.time * 20f, 1f) > 0.5f;
            foreach (var r in renderers) if (r != null) r.enabled = visible;
            if (hitRecoveryTimer <= 0)
                foreach (var r in renderers) if (r != null) r.enabled = true;
        }

        // Freeze
        if (freezeTimer > 0)
        {
            freezeTimer -= Time.deltaTime;
            if (freezeTimer <= 0)
            {
                // Restore pre-freeze emission colors (not just black)
                var renderers = GetComponentsInChildren<Renderer>();
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] != null && renderers[i].material != null
                        && renderers[i].material.HasProperty("_EmissionColor"))
                    {
                        Color restore = (preFreezeEmission != null && i < preFreezeEmission.Length)
                            ? preFreezeEmission[i]
                            : Color.black;
                        renderers[i].material.SetColor("_EmissionColor", restore);
                    }
                }
                preFreezeEmission = null;
            }
        }

        // Stun
        if (stunTimer > 0)
        {
            stunTimer -= Time.deltaTime;
            // Rotate stun stars
            if (stunVFXRoot != null)
                stunVFXRoot.transform.Rotate(0, 360f * Time.deltaTime, 0);
            // Cleanup when stun ends
            if (stunTimer <= 0 && stunVFXRoot != null)
            {
                Object.Destroy(stunVFXRoot);
                stunVFXRoot = null;
            }
        }

        // Magma slow
        if (magmaSlowTimer > 0)
        {
            magmaSlowTimer -= Time.deltaTime;
            // Restore colors when slow ends
            if (magmaSlowTimer <= 0 && magmaSlowTinted)
            {
                var renderers = GetComponentsInChildren<Renderer>();
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] != null && renderers[i].material != null
                        && renderers[i].material.HasProperty("_EmissionColor"))
                    {
                        Color restore = (preMagmaEmission != null && i < preMagmaEmission.Length)
                            ? preMagmaEmission[i]
                            : Color.black;
                        renderers[i].material.SetColor("_EmissionColor", restore);
                    }
                }
                preMagmaEmission = null;
                magmaSlowTinted = false;
            }
        }
    }

    public void ResetHealth()
    {
        isDead = false;
        currentHP = maxHP;
        freezeTimer = 0;
        stunTimer = 0;
        magmaSlowTimer = 0;
        hitRecoveryTimer = 0;
        preFreezeEmission = null;
        preMagmaEmission = null;
        magmaSlowTinted = false;
        if (stunVFXRoot != null) { Object.Destroy(stunVFXRoot); stunVFXRoot = null; }
        GetComponent<ElementalStatus>()?.ClearAll();

        // Ensure all renderers are visible
        var renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers) if (r != null) r.enabled = true;

        OnHPChanged?.Invoke(currentHP, maxHP);
    }
}
