using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Post-boss synergy system. Passive traits that reward element switching.
/// </summary>
public enum SynergyType
{
    Vacuum,       // Air kill pulls enemies to death point
    Conductor,    // After Lightning spell, nearby enemies take +50% for 2s
    Ignition,     // Fire kills leave burning ground
    Frostbite,    // Water kills freeze nearby enemies briefly
    Tremor,       // Earth spells create mini-quakes on kill
    Venomous,     // Poison damage spreads on kill
    Entropy,      // Void kills weaken nearby enemies
}

[System.Serializable]
public class SynergyDef
{
    public SynergyType type;
    public string name;
    public string description;
    public Color color;
}

public class SynergySystem : MonoBehaviour
{
    List<SynergyType> activeSynergies = new();
    float conductorTimer;

    public bool HasSynergy(SynergyType type) => activeSynergies.Contains(type);
    public List<SynergyType> ActiveSynergies => activeSynergies;

    public void AddSynergy(SynergyType type)
    {
        if (!activeSynergies.Contains(type))
            activeSynergies.Add(type);
    }

    /// <summary>Call when an enemy dies. Applies synergy effects.</summary>
    public void OnEnemyKilled(Vector3 deathPos, ElementType killingElement)
    {
        if (activeSynergies.Count == 0) return;

        // Vacuum: Air kills pull nearby enemies
        if (HasSynergy(SynergyType.Vacuum) && killingElement == ElementType.Air)
        {
            Collider[] nearby = Physics.OverlapSphere(deathPos, 5f);
            foreach (var c in nearby)
            {
                if (c.GetComponent<PlayerController>() != null) continue;
                var hp = c.GetComponent<Health>();
                if (hp == null || hp.IsDead) continue;
                var kb = c.gameObject.GetComponent<KnockbackEffect>();
                if (kb == null) kb = c.gameObject.AddComponent<KnockbackEffect>();
                Vector3 pullDir = (deathPos - c.transform.position).normalized;
                kb.Init(pullDir * 5f, 0.3f);
            }

            // Pull VFX
            var vfx = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(vfx.GetComponent<SphereCollider>());
            vfx.transform.position = deathPos + Vector3.up * 0.5f;
            vfx.transform.localScale = Vector3.one * 5f;
            vfx.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(0.8f, 0.9f, 1f), 2f);
            vfx.AddComponent<ComboShrinkVFX>().Init(0.3f);
        }

        // Ignition: Fire kills leave burning ground
        if (HasSynergy(SynergyType.Ignition) && killingElement == ElementType.Fire)
        {
            MagmaSpell.Cast(deathPos, 3f, 2f, 3f, false);
        }

        // Frostbite: Water kills freeze nearby
        if (HasSynergy(SynergyType.Frostbite) && killingElement == ElementType.Water)
        {
            Collider[] nearby = Physics.OverlapSphere(deathPos, 3f);
            foreach (var c in nearby)
            {
                if (c.GetComponent<PlayerController>() != null) continue;
                var hp = c.GetComponent<Health>();
                if (hp != null && !hp.IsDead)
                    hp.ApplyFreeze(1.5f);
            }
        }

        // Tremor: Earth kills mini-quake
        if (HasSynergy(SynergyType.Tremor) && killingElement == ElementType.Earth)
        {
            Collider[] nearby = Physics.OverlapSphere(deathPos, 3f);
            foreach (var c in nearby)
            {
                if (c.GetComponent<PlayerController>() != null) continue;
                var hp = c.GetComponent<Health>();
                if (hp != null && !hp.IsDead)
                {
                    hp.TakeDamage(5);
                    hp.ApplyStun(0.5f);
                }
            }
            if (TopDownCamera.Instance != null)
                TopDownCamera.Instance.AddTrauma(0.15f);
        }
    }

    /// <summary>Call after a spell is cast. Applies post-cast synergies.</summary>
    public void OnSpellCast(ElementType element)
    {
        if (HasSynergy(SynergyType.Conductor) && element == ElementType.Lightning)
            conductorTimer = 2f;
    }

    /// <summary>Returns damage multiplier from active synergies.</summary>
    public float GetDamageMultiplier()
    {
        float mult = 1f;
        if (conductorTimer > 0) mult *= 1.5f;
        return mult;
    }

    void Update()
    {
        if (conductorTimer > 0)
            conductorTimer -= Time.deltaTime;
    }

    // ── Synergy definitions ──
    public static SynergyDef[] AllSynergies = new[]
    {
        new SynergyDef { type = SynergyType.Vacuum, name = "Vacuum",
            description = "Air kills pull nearby enemies to the death point",
            color = new Color(0.8f, 0.9f, 1f) },
        new SynergyDef { type = SynergyType.Conductor, name = "Conductor",
            description = "After Lightning spell, nearby enemies take +50% damage for 2s",
            color = new Color(1f, 1f, 0.3f) },
        new SynergyDef { type = SynergyType.Ignition, name = "Ignition",
            description = "Fire kills leave burning ground",
            color = new Color(1f, 0.4f, 0.1f) },
        new SynergyDef { type = SynergyType.Frostbite, name = "Frostbite",
            description = "Water kills briefly freeze nearby enemies",
            color = new Color(0.3f, 0.7f, 1f) },
        new SynergyDef { type = SynergyType.Tremor, name = "Tremor",
            description = "Earth kills create a mini-quake damaging and stunning nearby",
            color = new Color(0.6f, 0.4f, 0.2f) },
        new SynergyDef { type = SynergyType.Venomous, name = "Venomous",
            description = "Poison damage spreads to nearby enemies on kill",
            color = new Color(0.2f, 0.9f, 0.1f) },
        new SynergyDef { type = SynergyType.Entropy, name = "Entropy",
            description = "Void kills weaken nearby enemies, reducing their damage",
            color = new Color(0.6f, 0.1f, 0.9f) },
    };
}
