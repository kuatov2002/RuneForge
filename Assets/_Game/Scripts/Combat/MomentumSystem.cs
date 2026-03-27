using UnityEngine;
using System;
using Object = UnityEngine.Object;

/// <summary>
/// Kill streak momentum system.
/// Killing enemies without taking damage builds a multiplier.
/// Taking damage drops one tier.
/// Multiplier: x1.0 → x1.15 → x1.3 → x1.5 → x2.0
/// </summary>
public class MomentumSystem : MonoBehaviour
{
    public event Action<int, float> OnMomentumChanged; // (tier, multiplier)
    public event Action<int> OnMomentumLost; // (new tier) — fired specifically on tier drop for UI feedback

    int killStreak;
    int tier; // 0-4
    float multiplier = 1f;
    float decayTimer;

    static readonly int[] TierThresholds = { 0, 3, 6, 10, 15 };
    static readonly float[] TierMultipliers = { 1f, 1.15f, 1.3f, 1.5f, 2f };
    static readonly float[] TierSpeedBonus = { 0f, 0.05f, 0.08f, 0.12f, 0.15f };

    public static readonly Color[] TierColors = {
        new Color(0.3f, 0.3f, 0.3f),
        new Color(1f, 1f, 0.5f),
        new Color(1f, 0.8f, 0.2f),
        new Color(1f, 0.5f, 0.1f),
        new Color(1f, 0.2f, 0.1f)
    };
    public static readonly string[] TierNames = { "", "WARM", "HOT", "BLAZING", "INFERNO" };

    public float DamageMultiplier => multiplier;
    public float SpeedBonus => TierSpeedBonus[tier];
    public int Tier => tier;
    public int KillStreak => killStreak;

    void Start()
    {
        var hp = GetComponent<Health>();
        if (hp != null)
            hp.OnDamaged += OnPlayerDamaged;
    }

    void OnPlayerDamaged(int amount, Vector3 pos, bool killed)
    {
        if (killed) return;
        DropMomentumTier();
    }

    public void OnEnemyKilled()
    {
        killStreak++;
        decayTimer = 7f;

        int newTier = 0;
        for (int i = TierThresholds.Length - 1; i >= 0; i--)
            if (killStreak >= TierThresholds[i]) { newTier = i; break; }

        if (newTier != tier)
        {
            tier = newTier;
            multiplier = TierMultipliers[tier];
            OnMomentumChanged?.Invoke(tier, multiplier);

            if (tier >= 2)
            {
                SFXSystem.Play(SFXSystem.SFXType.LevelUp, transform.position, 0.3f);
                if (TopDownCamera.Instance != null)
                    TopDownCamera.Instance.AddTrauma(0.05f);
            }
        }
    }

    void DropMomentumTier()
    {
        if (tier == 0 && killStreak == 0) return;

        int oldTier = tier;

        // Halve kill streak instead of zeroing — keeps momentum partially
        killStreak = killStreak / 2;

        int newTier = 0;
        for (int i = TierThresholds.Length - 1; i >= 0; i--)
            if (killStreak >= TierThresholds[i]) { newTier = i; break; }

        tier = newTier;
        multiplier = TierMultipliers[tier];
        OnMomentumChanged?.Invoke(tier, multiplier);

        // Feedback for losing momentum
        if (oldTier > newTier)
        {
            OnMomentumLost?.Invoke(tier);
            SFXSystem.Play(SFXSystem.SFXType.PlayerHit, transform.position, 0.2f);

            // Brief red flash on momentum loss
            GameFeel.ScreenFlash(new Color(1f, 0.2f, 0.1f, 0.3f), 0.1f);
        }
    }

    void Update()
    {
        if (tier <= 0) return;

        decayTimer -= Time.deltaTime;
        if (decayTimer <= 0)
        {
            killStreak = Mathf.Max(0, killStreak - 1);
            decayTimer = 3f;

            int newTier = 0;
            for (int i = TierThresholds.Length - 1; i >= 0; i--)
                if (killStreak >= TierThresholds[i]) { newTier = i; break; }

            if (newTier != tier)
            {
                int oldTier = tier;
                tier = newTier;
                multiplier = TierMultipliers[tier];
                OnMomentumChanged?.Invoke(tier, multiplier);

                if (oldTier > newTier)
                    OnMomentumLost?.Invoke(tier);
            }
        }
    }

    void OnDestroy()
    {
        var hp = GetComponent<Health>();
        if (hp != null) hp.OnDamaged -= OnPlayerDamaged;
    }
}
