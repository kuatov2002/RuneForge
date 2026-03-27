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

    // Particle aura
    ParticleSystem auraPS;
    static readonly float[] AuraEmissionRates = { 0f, 5f, 10f, 15f, 25f };
    static readonly float[] AuraStartSizes = { 0f, 0.15f, 0.2f, 0.3f, 0.4f };

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

        CreateAuraParticleSystem();
    }

    void CreateAuraParticleSystem()
    {
        var go = new GameObject("MomentumAura");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0.5f, 0f);

        auraPS = go.AddComponent<ParticleSystem>();
        auraPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = auraPS.main;
        main.loop = true;
        main.startLifetime = 0.8f;
        main.startSpeed = 0.3f;
        main.startSize = 0.15f;
        main.startColor = TierColors[1];
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 40;
        main.gravityModifier = -0.2f;

        var emission = auraPS.emission;
        emission.rateOverTime = 0f;

        var shape = auraPS.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.4f;

        var colorOverLifetime = auraPS.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        colorOverLifetime.color = gradient;

        var sizeOverLifetime = auraPS.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, 0.3f);

        var mat = new Material(ShaderCache.ParticleShader);
        go.GetComponent<ParticleSystemRenderer>().material = mat;
    }

    void UpdateAura()
    {
        if (auraPS == null) return;

        if (tier <= 0)
        {
            if (auraPS.isPlaying)
                auraPS.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            return;
        }

        if (!auraPS.isPlaying)
            auraPS.Play();

        var main = auraPS.main;
        main.startColor = TierColors[tier];
        main.startSize = AuraStartSizes[tier];

        var emission = auraPS.emission;
        emission.rateOverTime = AuraEmissionRates[tier];

        // Brighter material at higher tiers
        var renderer = auraPS.GetComponent<ParticleSystemRenderer>();
        if (renderer != null && renderer.material != null)
        {
            Color c = TierColors[tier];
            renderer.material.color = c;
            float emissionIntensity = 0.5f + tier * 0.5f;
            renderer.material.SetColor("_EmissionColor", c * emissionIntensity);
        }
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
            UpdateAura();

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
        UpdateAura();

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
                UpdateAura();

                if (oldTier > newTier)
                    OnMomentumLost?.Invoke(tier);
            }
        }
    }

    void OnDestroy()
    {
        var hp = GetComponent<Health>();
        if (hp != null) hp.OnDamaged -= OnPlayerDamaged;

        if (auraPS != null)
            Object.Destroy(auraPS.gameObject);
    }
}
