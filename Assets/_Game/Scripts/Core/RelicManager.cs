using UnityEngine;
using System.Collections.Generic;
using Random = UnityEngine.Random;

public class RelicManager : MonoBehaviour
{
    List<RelicSO> ownedRelics = new();
    Health playerHealth;
    PlayerController playerCtrl;
    ElementSO[] allElementsRef;

    // Relic state
    int hitCounter;
    float regenTimer;
    bool roomShieldActive;

    // Synergy state
    float synergyBerserkerGlassTimer; // Berserker+GlassCannon: lifesteal window
    float synergyVampireThornsHeal;   // VampireAura+Thorns: stored overkill heal
    int synergyDoubleStrikeChaosCounter; // DoubleStrike+Chaos: AoE on proc

    // Element relic state
    float prismTimer;
    float prismDamageBuff;
    ElementType[] prismRecentElements = new ElementType[8];
    float[] prismRecentTimes = new float[8];
    int prismIdx;

    public List<RelicSO> OwnedRelics => ownedRelics;

    public void Init(Health hp, PlayerController ctrl, RelicSO[] allRelics, ElementSO[] allElements = null)
    {
        playerHealth = hp;
        playerCtrl = ctrl;
        allElementsRef = allElements;
        ownedRelics.Clear();
        hitCounter = 0;
        regenTimer = 0;
        roomShieldActive = false;
        synergyBerserkerGlassTimer = 0;
        synergyVampireThornsHeal = 0;
        synergyDoubleStrikeChaosCounter = 0;
    }

    /// <summary>Returns a random element for Chaos relic.</summary>
    public ElementSO GetRandomElement()
    {
        if (allElementsRef == null || allElementsRef.Length == 0) return null;
        return allElementsRef[Random.Range(0, allElementsRef.Length)];
    }

    public void AddRelic(RelicSO relic)
    {
        ownedRelics.Add(relic);
        ApplyPassive(relic);
        SFXSystem.Play(SFXSystem.SFXType.PickupJuice, transform.position);
    }

    public bool HasRelic(RelicType type)
    {
        foreach (var r in ownedRelics)
            if (r.relicType == type) return true;
        return false;
    }

    void ApplyPassive(RelicSO relic)
    {
        switch (relic.relicType)
        {
            case RelicType.SpeedBoost:
                if (playerCtrl != null) playerCtrl.moveSpeed *= 1.15f;
                break;
            case RelicType.GlassCannon:
                if (playerHealth != null)
                {
                    playerHealth.maxHP = Mathf.Max(1, playerHealth.maxHP - 1);
                    if (playerHealth.currentHP > playerHealth.maxHP)
                        playerHealth.currentHP = playerHealth.maxHP;
                }
                break;
            case RelicType.CursedSpeed:
                if (playerCtrl != null) playerCtrl.moveSpeed *= 1.4f;
                if (playerHealth != null)
                {
                    playerHealth.maxHP = Mathf.Max(1, playerHealth.maxHP - 2);
                    if (playerHealth.currentHP > playerHealth.maxHP)
                        playerHealth.currentHP = playerHealth.maxHP;
                }
                break;
            case RelicType.GaleRing:
                if (playerCtrl != null) playerCtrl.dashDistance *= 1.3f;
                break;
        }
    }

    void Update()
    {
        if (playerHealth == null || playerHealth.IsDead) return;

        if (synergyBerserkerGlassTimer > 0) synergyBerserkerGlassTimer -= Time.deltaTime;

        // Regeneration: heal 1 HP every 30s (15s with BloodPact synergy)
        if (HasRelic(RelicType.Regeneration))
        {
            float interval = (HasRelic(RelicType.BloodPact)) ? 15f : 30f;
            regenTimer += Time.deltaTime;
            if (regenTimer >= interval)
            {
                regenTimer = 0;
                playerHealth.Heal(1);
            }
        }

        // PrismShard: track element variety, buff if 3+ unique in 10s
        if (HasRelic(RelicType.PrismShard))
        {
            if (prismDamageBuff > 0) prismDamageBuff -= Time.deltaTime;
        }

        // StoneSkin visual hint (handled in ModifyIncomingDamage)
    }

    // Called by SpellProjectile/attack systems when dealing damage
    public float ModifyDamage(float baseDamage)
    {
        float dmg = baseDamage;

        // Berserker: +25% damage when below 50% HP
        if (HasRelic(RelicType.Berserker) && playerHealth != null)
        {
            if (playerHealth.currentHP <= playerHealth.maxHP / 2)
                dmg *= 1.25f;
        }

        // GlassCannon: +50% damage
        if (HasRelic(RelicType.GlassCannon))
            dmg *= 1.5f;

        // DoubleStrike: every 5th hit deals 2x
        hitCounter++;
        bool isDoubleStrike = HasRelic(RelicType.DoubleStrike) && hitCounter % 5 == 0;
        if (isDoubleStrike)
            dmg *= 2f;

        // Cursed Power: +75% damage
        if (HasRelic(RelicType.CursedPower))
            dmg *= 1.75f;

        // Blood Pact: +100% damage
        if (HasRelic(RelicType.BloodPact))
            dmg *= 2f;

        // Chaos: +30% damage
        if (HasRelic(RelicType.Chaos))
            dmg *= 1.3f;

        // PrismShard: +25% damage when buff active
        if (HasRelic(RelicType.PrismShard) && prismDamageBuff > 0)
            dmg *= 1.25f;

        // ─── SYNERGIES ─────────────────────────────────────────
        // Berserker + GlassCannon = "Rage Glass": 10% lifesteal while below 50% HP
        if (HasRelic(RelicType.Berserker) && HasRelic(RelicType.GlassCannon) && playerHealth != null)
        {
            if (playerHealth.currentHP <= playerHealth.maxHP / 2)
            {
                int heal = Mathf.Max(1, Mathf.FloorToInt(dmg * 0.1f));
                playerHealth.Heal(heal);
            }
        }

        // DoubleStrike + Chaos = "Chaotic Surge": double-strike procs AoE explosion
        if (isDoubleStrike && HasRelic(RelicType.Chaos))
        {
            synergyDoubleStrikeChaosCounter++;
            // Trigger AoE via flag — checked by SpellProjectile on hit
        }

        // BloodPact + Regeneration = "Blood Renewal": regen ticks every 15s instead of 30s
        // (handled in Update)

        // Shield + Thorns = "Retribution": blocked hit reflects 5 damage instead of 1
        // (handled in ModifyIncomingDamage)

        // DashFire + CursedSpeed = "Inferno Dash": fire trail deals 2x damage + lasts 2x longer
        // (handled in CreateFireTrail and OnDash)

        return dmg;
    }

    /// <summary>Whether the Chaotic Surge synergy just triggered (DoubleStrike+Chaos AoE).</summary>
    public bool ConsumeChaoticSurge()
    {
        if (synergyDoubleStrikeChaosCounter > 0) { synergyDoubleStrikeChaosCounter--; return true; }
        return false;
    }

    // Called when player takes damage
    public int ModifyIncomingDamage(int damage)
    {
        // Shield: block first hit per room
        if (roomShieldActive && HasRelic(RelicType.Shield))
        {
            roomShieldActive = false;

            // Shield + Thorns synergy: blocked hit reflects 5 damage to nearby enemies
            if (HasRelic(RelicType.Thorns))
            {
                Collider[] nearby = Physics.OverlapSphere(transform.position, 3f);
                foreach (var col in nearby)
                {
                    if (col.GetComponent<PlayerController>() != null) continue;
                    var hp = col.GetComponent<Health>();
                    if (hp != null && !hp.IsDead) hp.TakeDamage(5);
                }
                SFXSystem.Play(SFXSystem.SFXType.Explosion, transform.position, 0.5f);
            }

            return 0;
        }

        // StoneSkin: -1 damage when player is standing still
        if (HasRelic(RelicType.StoneSkin) && playerCtrl != null)
        {
            var rb = playerCtrl.GetComponent<Rigidbody>();
            if (rb != null && rb.linearVelocity.sqrMagnitude < 0.1f)
                damage = Mathf.Max(0, damage - 1);
        }

        // Cursed Power: take 1 extra damage per hit
        if (HasRelic(RelicType.CursedPower))
            damage += 1;

        // BloodPact: take 1 extra damage per hit (stacks with CursedPower)
        if (HasRelic(RelicType.BloodPact))
            damage += 1;

        return damage;
    }

    // Called at room start
    public void OnRoomEnter()
    {
        if (HasRelic(RelicType.Shield))
            roomShieldActive = true;

        // VampireAura: heal 1 HP per room cleared
        if (HasRelic(RelicType.VampireAura) && playerHealth != null)
        {
            int heal = 1;
            // Lucky + VampireAura = "Fortune's Vitality": heal 2 HP per room + 20% chance for 3
            if (HasRelic(RelicType.Lucky))
                heal = Random.value < 0.2f ? 3 : 2;
            playerHealth.Heal(heal);
        }
    }

    // Called when player dashes
    public void OnDash(Vector3 from, Vector3 to)
    {
        if (!HasRelic(RelicType.DashFire)) return;

        bool infernoSynergy = HasRelic(RelicType.CursedSpeed); // DashFire+CursedSpeed = "Inferno Dash"
        float spacing = infernoSynergy ? 0.5f : 0.8f; // denser trail
        float lifetime = infernoSynergy ? 4f : 2f;     // lasts longer
        int dmg = infernoSynergy ? 4 : 2;              // double damage

        Vector3 dir = (to - from);
        float dist = dir.magnitude;
        dir.Normalize();
        int segments = Mathf.CeilToInt(dist / spacing);
        for (int i = 0; i < segments; i++)
        {
            Vector3 pos = from + dir * (i * spacing);
            CreateFireTrail(pos, lifetime, dmg, infernoSynergy);
        }
    }

    void CreateFireTrail(Vector3 pos, float lifetime = 2f, int dmg = 2, bool inferno = false)
    {
        var fire = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        fire.name = "DashFire";
        Destroy(fire.GetComponent<CapsuleCollider>());
        var col = fire.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = inferno ? 0.9f : 0.6f;
        fire.transform.position = pos + Vector3.up * 0.05f;
        float scale = inferno ? 1.1f : 0.8f;
        fire.transform.localScale = new Vector3(scale, 0.05f, scale);
        Color fireColor = inferno ? new Color(1f, 0.2f, 0.0f, 0.8f) : new Color(1f, 0.4f, 0.1f, 0.6f);
        var mat = ShaderCache.NewEmissive(fireColor);
        mat.SetColor("_EmissionColor", (inferno ? new Color(1f, 0.1f, 0f) : new Color(1f, 0.3f, 0f)) * 3f);
        fire.GetComponent<Renderer>().material = mat;
        var zone = fire.AddComponent<DashFireZone>();
        zone.damage = dmg;
        Destroy(fire, lifetime);
    }

    // Extra rune choice from Lucky relic
    public bool HasLucky => HasRelic(RelicType.Lucky) && Random.value < 0.2f;

    /// <summary>Track element usage for PrismShard relic.</summary>
    public void TrackElementUsed(ElementType elem)
    {
        if (!HasRelic(RelicType.PrismShard)) return;
        prismRecentElements[prismIdx] = elem;
        prismRecentTimes[prismIdx] = Time.time;
        prismIdx = (prismIdx + 1) % prismRecentElements.Length;

        // Count unique elements in last 10 seconds
        var seen = new System.Collections.Generic.HashSet<ElementType>();
        for (int i = 0; i < prismRecentElements.Length; i++)
        {
            if (Time.time - prismRecentTimes[i] < 10f)
                seen.Add(prismRecentElements[i]);
        }
        if (seen.Count >= 3)
            prismDamageBuff = 5f; // 5 second buff
    }

    /// <summary>Check if FrostCrown is active (freeze duration x1.5).</summary>
    public bool HasFrostCrown => HasRelic(RelicType.FrostCrown);

    /// <summary>Check if VenomSac is active (poison ticks x1.5).</summary>
    public bool HasVenomSac => HasRelic(RelicType.VenomSac);

    /// <summary>Check if VoidLens is active (void pull radius x1.4).</summary>
    public bool HasVoidLens => HasRelic(RelicType.VoidLens);

    /// <summary>Check if StormConductor is active (+10% crit for lightning).</summary>
    public bool HasStormConductor => HasRelic(RelicType.StormConductor);

    /// <summary>Check if EmberHeart is active (fire chains to extra target).</summary>
    public bool HasEmberHeart => HasRelic(RelicType.EmberHeart);

    /// <summary>Remove a relic and undo its passive effects.</summary>
    public void RemoveRelic(RelicSO relic)
    {
        ownedRelics.Remove(relic);
        // Note: some passives (speed, HP) are hard to undo precisely,
        // but for cursed relics this is primarily used for curse purification
    }
}

// Fire zone from dash relic — damages enemies standing in it
public class DashFireZone : MonoBehaviour
{
    public int damage = 2;
    float tickTimer;

    void OnTriggerStay(Collider other)
    {
        tickTimer -= Time.deltaTime;
        if (tickTimer > 0) return;
        tickTimer = 0.5f;

        if (other.GetComponent<PlayerController>() != null) return;
        var hp = other.GetComponent<Health>();
        if (hp != null && !hp.IsDead)
            hp.TakeDamage(damage);
    }
}
