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

    // Burn
    float burnDPS;
    float burnTimer;
    float burnAccumulator;

    // Slow (Ice)
    float slowTimer;
    float slowAmount = 1f;
    int slowHitCount;

    // Freeze (Ice - repeated slow)
    float freezeTimer;

    // Stun (Lightning)
    float stunTimer;

    // Poison (stacking)
    int poisonStacks;
    const float PoisonDPSPerStack = 2f;
    float poisonAccumulator;

    // Void
    [HideInInspector] public bool voidMarked;

    bool isDead;

    public bool IsDead => isDead;
    public bool IsSlowed => slowTimer > 0;
    public bool IsFrozen => freezeTimer > 0;
    public int PoisonStacks => poisonStacks;

    public float SpeedMultiplier
    {
        get
        {
            if (stunTimer > 0 || freezeTimer > 0) return 0f;
            return slowTimer > 0 ? slowAmount : 1f;
        }
    }

    public bool IsStunned => stunTimer > 0 || freezeTimer > 0;
    public bool IsBurning => burnTimer > 0;

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

        // Relic: modify incoming damage (Shield blocks first hit per room)
        int dmg = Mathf.Max(1, Mathf.CeilToInt(amount));
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
                // Knockback player away from damage source
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
            // Hitstop on significant hits (not DoT ticks)
            if (dmg >= 3)
                GameFeel.Instance.Hitstop(dmg >= 8 ? 0.05f : 0.03f);

            // Knockback enemies
            GameFeel.ApplyKnockback(transform, transform.position - transform.forward, dmg * 0.3f);
        }

        // SFX
        if (isPlayer)
            SFXSystem.Play(SFXSystem.SFXType.PlayerHit, transform.position);
        else
            SFXSystem.Play(dmg >= 8 ? SFXSystem.SFXType.CritHit : SFXSystem.SFXType.Hit, transform.position);

        if (killed)
        {
            isDead = true;
            // Death VFX for enemies
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

    /// <summary>Force-fire the HP changed event (e.g. after direct HP modification).</summary>
    public void InvokeHPChanged() => OnHPChanged?.Invoke(currentHP, maxHP);

    public void Heal(int amount)
    {
        if (isDead || amount <= 0) return;
        currentHP = Mathf.Min(currentHP + amount, maxHP);
        OnHPChanged?.Invoke(currentHP, maxHP);
    }

    public void ApplyStatusEffect(ElementSO element)
    {
        if (element == null || isDead) return;

        // Check combo BEFORE applying (we need pre-existing status)
        ElementCombo.CheckCombo(this, element);

        switch (element.statusEffect)
        {
            case StatusEffectType.Burn:
                ApplyBurn(element.statusDPS, element.statusDuration);
                break;
            case StatusEffectType.Slow:
                ApplySlow(element.statusDuration);
                break;
            case StatusEffectType.Chain:
                ApplyStun(element.statusDuration);
                break;
            case StatusEffectType.Poison:
                AddPoisonStack();
                break;
            case StatusEffectType.VoidMark:
                voidMarked = true;
                break;
        }
    }

    public void ApplyBurn(float dps, float duration)
    {
        burnDPS = dps;
        burnTimer = Mathf.Max(burnTimer, duration);
    }

    public void ApplySlow(float duration)
    {
        slowTimer = duration;
        slowAmount = 0.6f;
        slowHitCount++;
        if (slowHitCount >= 2 && freezeTimer <= 0)
        {
            freezeTimer = 1.5f;
            slowHitCount = 0;
        }
    }

    public void ApplyStun(float duration)
    {
        stunTimer = Mathf.Max(stunTimer, duration);
    }

    public void AddPoisonStack()
    {
        poisonStacks = Mathf.Min(poisonStacks + 1, 5);
    }

    void Update()
    {
        if (isDead) return;

        // Hit recovery timer
        if (hitRecoveryTimer > 0)
        {
            hitRecoveryTimer -= Time.deltaTime;
            // Flash player during recovery
            var renderers = GetComponentsInChildren<Renderer>();
            bool visible = Mathf.PingPong(Time.time * 20f, 1f) > 0.5f;
            foreach (var r in renderers) if (r != null) r.enabled = visible;
            if (hitRecoveryTimer <= 0)
                foreach (var r in renderers) if (r != null) r.enabled = true;
        }

        // Burn
        if (burnTimer > 0)
        {
            burnTimer -= Time.deltaTime;
            burnAccumulator += burnDPS * Time.deltaTime;
            if (burnAccumulator >= 1f)
            {
                int ticks = Mathf.FloorToInt(burnAccumulator);
                burnAccumulator -= ticks;
                TakeDamage(ticks);
            }
        }

        // Slow
        if (slowTimer > 0)
        {
            slowTimer -= Time.deltaTime;
            if (slowTimer <= 0) { slowAmount = 1f; slowHitCount = 0; }
        }

        // Freeze
        if (freezeTimer > 0)
            freezeTimer -= Time.deltaTime;

        // Stun
        if (stunTimer > 0)
            stunTimer -= Time.deltaTime;

        // Poison
        if (poisonStacks > 0)
        {
            poisonAccumulator += poisonStacks * PoisonDPSPerStack * Time.deltaTime;
            if (poisonAccumulator >= 1f)
            {
                int ticks = Mathf.FloorToInt(poisonAccumulator);
                poisonAccumulator -= ticks;
                TakeDamage(ticks);
            }
        }
    }

    public void ResetHealth()
    {
        isDead = false;
        currentHP = maxHP;
        burnTimer = 0; burnAccumulator = 0;
        slowTimer = 0; slowAmount = 1f; slowHitCount = 0;
        freezeTimer = 0; stunTimer = 0;
        poisonStacks = 0; poisonAccumulator = 0;
        voidMarked = false;
        OnHPChanged?.Invoke(currentHP, maxHP);
    }
}
