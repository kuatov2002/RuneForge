using UnityEngine;
using System;

public class Health : MonoBehaviour
{
    public int maxHP = 5;
    [HideInInspector] public int currentHP;

    public event Action<int, int> OnHPChanged;
    public event Action OnDeath;

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

    void Awake()
    {
        currentHP = maxHP;
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;
        int dmg = Mathf.Max(1, Mathf.CeilToInt(amount));
        currentHP -= dmg;
        if (currentHP < 0) currentHP = 0;
        OnHPChanged?.Invoke(currentHP, maxHP);
        if (currentHP <= 0)
        {
            isDead = true;
            OnDeath?.Invoke();
        }
    }

    public void ApplyStatusEffect(ElementSO element)
    {
        if (element == null || isDead) return;
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
