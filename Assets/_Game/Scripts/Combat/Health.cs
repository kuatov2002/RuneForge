using UnityEngine;
using System;

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

    bool isDead;

    public bool IsDead => isDead;
    public bool IsFrozen => freezeTimer > 0;
    public bool IsStunned => stunTimer > 0 || freezeTimer > 0;

    public float SpeedMultiplier
    {
        get
        {
            if (stunTimer > 0 || freezeTimer > 0) return 0f;
            if (magmaSlowTimer > 0) return 0.3f;
            return 1f;
        }
    }

    void Awake()
    {
        currentHP = maxHP;
    }

    // Player hit recovery
    float hitRecoveryTimer;
    const float HitRecoveryDuration = 0.5f;
    public bool IsInHitRecovery => hitRecoveryTimer > 0;

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
        OnHPChanged?.Invoke(currentHP, maxHP);

        bool killed = currentHP <= 0;

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
            GameFeel.ApplyKnockback(transform, transform.position - transform.forward, dmg * 0.3f);

            // Hit impact particles on every enemy hit
            Color hitColor = Color.white;
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0 && renderers[0] != null)
                hitColor = renderers[0].material.color;
            GameFeel.SpawnHitParticles(transform.position + Vector3.up * 0.5f, hitColor, dmg >= 5 ? 1.3f : 0.8f);
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

    /// <summary>Take damage with element type — checks weakness/immunity.</summary>
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

    /// <summary>Freeze enemy completely (Deep Freeze spell).</summary>
    public void ApplyFreeze(float duration)
    {
        freezeTimer = Mathf.Max(freezeTimer, duration);

        // Visual: blue tint
        var renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            if (r != null && r.material != null)
                r.material.SetColor("_EmissionColor", new Color(0.3f, 0.6f, 1f) * 2f);
        }
    }

    /// <summary>Stun enemy (Rubble, Geyser).</summary>
    public void ApplyStun(float duration)
    {
        stunTimer = Mathf.Max(stunTimer, duration);
    }

    /// <summary>Magma slow — very heavy, clears when leaving pool.</summary>
    public void ApplyMagmaSlow()
    {
        magmaSlowTimer = 0.3f; // Refreshed each physics tick while in pool
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
                // Remove blue tint
                var renderers = GetComponentsInChildren<Renderer>();
                foreach (var r in renderers)
                {
                    if (r != null && r.material != null)
                        r.material.SetColor("_EmissionColor", Color.black);
                }
            }
        }

        // Stun
        if (stunTimer > 0)
            stunTimer -= Time.deltaTime;

        // Magma slow
        if (magmaSlowTimer > 0)
            magmaSlowTimer -= Time.deltaTime;
    }

    public void ResetHealth()
    {
        isDead = false;
        currentHP = maxHP;
        freezeTimer = 0;
        stunTimer = 0;
        magmaSlowTimer = 0;
        OnHPChanged?.Invoke(currentHP, maxHP);
    }
}
