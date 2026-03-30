using UnityEngine;
using System.Collections.Generic;
using Random = UnityEngine.Random;

public class RelicManager : MonoBehaviour
{
    List<RelicSO> ownedRelics = new();
    Health playerHealth;
    PlayerController playerCtrl;
    ElementSO[] allElementsRef;

    // Cached references
    SpellCaster spellCasterRef;

    // ─── Original relic state ────────────────────────────────
    int hitCounter;
    float regenTimer;
    bool roomShieldActive;

    // Synergy state (original)
    float synergyBerserkerGlassTimer;
    float synergyVampireThornsHeal;
    int synergyDoubleStrikeChaosCounter;

    // Element relic state (PrismShard)
    float prismTimer;
    float prismDamageBuff;
    ElementType[] prismRecentElements = new ElementType[8];
    float[] prismRecentTimes = new float[8];
    int prismIdx;

    // ─── Trigger relic state ─────────────────────────────────
    float aftershockCooldown;
    float quickSwapBuff;
    int soulHarvestKills;
    float dashChainFreeCast;
    int dashChainEnemiesHit;
    int reactorStacks;
    float reactorTimer;
    float roomStartTime;

    // ─── Defensive relic state ───────────────────────────────
    bool lastStandUsed;
    float lastStandBuff;
    float escapeArtistCooldown;

    // ─── Build-defining relic state ──────────────────────────
    float monolithTimer;
    float monolithCooldown;
    bool monolithActive;
    float entropyTimer;
    float entropyBuff;

    // ─── Conditional passive state ───────────────────────────
    int floorsCleared;

    // ─── Cursed relic state ──────────────────────────────────
    float velocityStunTimer;
    int absorptionStacks;
    float absorptionDrainTimer;
    float hungerTimer;

    // ─── Utility relic state ─────────────────────────────────
    float timeDilationTimer;

    public List<RelicSO> OwnedRelics => ownedRelics;

    public void Init(Health hp, PlayerController ctrl, RelicSO[] allRelics, ElementSO[] allElements = null)
    {
        playerHealth = hp;
        playerCtrl = ctrl;
        allElementsRef = allElements;
        ownedRelics.Clear();

        // Cache SpellCaster reference
        if (ctrl != null)
            spellCasterRef = ctrl.GetComponent<SpellCaster>();

        // Original state
        hitCounter = 0;
        regenTimer = 0;
        roomShieldActive = false;
        synergyBerserkerGlassTimer = 0;
        synergyVampireThornsHeal = 0;
        synergyDoubleStrikeChaosCounter = 0;

        // Trigger relic state
        aftershockCooldown = 0;
        quickSwapBuff = 0;
        soulHarvestKills = 0;
        dashChainFreeCast = 0;
        dashChainEnemiesHit = 0;
        reactorStacks = 0;
        reactorTimer = 0;
        roomStartTime = 0;

        // Defensive relic state
        lastStandUsed = false;
        lastStandBuff = 0;
        escapeArtistCooldown = 0;

        // Build-defining relic state
        monolithTimer = 0;
        monolithCooldown = 0;
        monolithActive = false;
        entropyTimer = 0;
        entropyBuff = 0;

        // Conditional passive state
        floorsCleared = 0;

        // Cursed relic state
        velocityStunTimer = 0;
        absorptionStacks = 0;
        absorptionDrainTimer = 0;
        hungerTimer = 0;

        // Utility relic state
        timeDilationTimer = 0;
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
    }

    public bool HasRelic(RelicType type)
    {
        foreach (var r in ownedRelics)
            if (r.relicType == type) return true;
        return false;
    }

    // ═════════════════════════════════════════════════════════
    //  APPLY PASSIVE
    // ═════════════════════════════════════════════════════════

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
            // MagnetField: increase gold magnet radius
            // TODO: Integrate with GoldPickup magnet range
            case RelicType.MagnetField:
                break;
            // Merchant: shop discount handled via HasMerchant property
            case RelicType.Merchant:
                break;
            // RuneRecycler: overheat penalty skip handled via HasRuneRecycler property
            case RelicType.RuneRecycler:
                break;
            // CursedVelocity: cooldown reduction flag for SpellCaster
            // TODO: SpellCaster reads HasCursedVelocity to apply -30% cooldowns
            case RelicType.CursedVelocity:
                break;
        }
    }

    // ═════════════════════════════════════════════════════════
    //  UPDATE
    // ═════════════════════════════════════════════════════════

    void Update()
    {
        if (playerHealth == null || playerHealth.IsDead) return;

        float dt = Time.deltaTime;

        // ── Original timers ──
        if (synergyBerserkerGlassTimer > 0) synergyBerserkerGlassTimer -= dt;

        // Regeneration: heal 1 HP every 30s (15s with BloodPact synergy)
        if (HasRelic(RelicType.Regeneration))
        {
            float interval = HasRelic(RelicType.BloodPact) ? 15f : 30f;
            regenTimer += dt;
            if (regenTimer >= interval)
            {
                regenTimer = 0;
                playerHealth.Heal(1);
            }
        }

        // PrismShard: buff timer
        if (HasRelic(RelicType.PrismShard))
        {
            if (prismDamageBuff > 0) prismDamageBuff -= dt;
        }

        // ── QuickSwap buff timer ──
        if (quickSwapBuff > 0) quickSwapBuff -= dt;

        // ── Aftershock cooldown ──
        if (aftershockCooldown > 0) aftershockCooldown -= dt;

        // ── DashChain free cast timer ──
        if (dashChainFreeCast > 0) dashChainFreeCast -= dt;

        // ── ReactorCore stack decay ──
        if (reactorStacks > 0)
        {
            reactorTimer -= dt;
            if (reactorTimer <= 0)
                reactorStacks = 0;
        }

        // ── LastStand buff timer ──
        if (lastStandBuff > 0) lastStandBuff -= dt;

        // ── EscapeArtist cooldown ──
        if (escapeArtistCooldown > 0) escapeArtistCooldown -= dt;

        // ── EntropyRelic: force overheat every 20s ──
        if (HasRelic(RelicType.EntropyRelic))
        {
            entropyTimer += dt;
            if (entropyTimer >= 20f)
            {
                entropyTimer = 0;
                entropyBuff = 5f;
                // TODO: Force-overheat a random element via SpellCaster
            }
            if (entropyBuff > 0) entropyBuff -= dt;
        }

        // ── Monolith: stand still for damage buff ──
        if (HasRelic(RelicType.Monolith))
        {
            UpdateMonolith(dt);
        }

        // ── TimeDilation: slow nearby enemies when standing still ──
        if (HasRelic(RelicType.TimeDilation))
        {
            UpdateTimeDilation(dt);
        }

        // ── CursedVelocity: stun player every 12s ──
        if (HasRelic(RelicType.CursedVelocity))
        {
            velocityStunTimer += dt;
            if (velocityStunTimer >= 12f)
            {
                velocityStunTimer = 0;
                playerHealth.ApplyStun(0.5f);
            }
        }

        // ── CursedAbsorption: drain 1 HP every 8s ──
        if (HasRelic(RelicType.CursedAbsorption))
        {
            absorptionDrainTimer += dt;
            if (absorptionDrainTimer >= 8f)
            {
                absorptionDrainTimer = 0;
                playerHealth.TakeDamage(1);
            }
        }

        // ── CursedHunger: lose 1 HP every 6s without a kill ──
        if (HasRelic(RelicType.CursedHunger))
        {
            hungerTimer += dt;
            if (hungerTimer >= 6f)
            {
                hungerTimer = 0;
                playerHealth.TakeDamage(1);
            }
        }
    }

    void UpdateMonolith(float dt)
    {
        bool standingStill = IsPlayerStandingStill();

        if (monolithCooldown > 0)
        {
            monolithCooldown -= dt;
            monolithActive = false;
            monolithTimer = 0;
            return;
        }

        if (standingStill)
        {
            monolithTimer = Mathf.Min(monolithTimer + dt, 6f);
            monolithActive = true;
        }
        else
        {
            if (monolithActive)
                monolithCooldown = 3f;
            monolithActive = false;
            monolithTimer = 0;
        }
    }

    void UpdateTimeDilation(float dt)
    {
        bool standingStill = IsPlayerStandingStill();

        if (standingStill)
        {
            timeDilationTimer += dt;
            if (timeDilationTimer >= 0.5f)
            {
                Collider[] nearby = Physics.OverlapSphere(transform.position, 3f);
                foreach (var col in nearby)
                {
                    if (col.GetComponent<PlayerController>() != null) continue;
                    var hp = col.GetComponent<Health>();
                    if (hp != null && !hp.IsDead)
                        hp.ApplyMagmaSlow();
                }
            }
        }
        else
        {
            timeDilationTimer = 0;
        }
    }

    bool IsPlayerStandingStill()
    {
        if (playerCtrl == null) return false;
        var rb = playerCtrl.GetComponent<Rigidbody>();
        return rb != null && rb.linearVelocity.sqrMagnitude < 0.1f;
    }

    // ═════════════════════════════════════════════════════════
    //  MODIFY DAMAGE
    // ═════════════════════════════════════════════════════════

    /// <summary>Called by SpellProjectile/attack systems when dealing damage.</summary>
    public float ModifyDamage(float baseDamage)
    {
        float dmg = baseDamage;

        // ── Original relics ──

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

        // ── Trigger relics ──

        // QuickSwap: +25% damage for 1.5s after switching element
        if (HasRelic(RelicType.QuickSwap) && quickSwapBuff > 0)
            dmg *= 1.25f;

        // ReactorCore: +6% per stack (max 5 stacks)
        if (HasRelic(RelicType.ReactorCore) && reactorStacks > 0)
        {
            // Reaction Cascade synergy: +10% per stack instead of +6%
            float perStack = (HasRelic(RelicType.ReactionCatalyst) && HasRelic(RelicType.PrismShard))
                ? 0.10f
                : 0.06f;
            dmg *= 1f + reactorStacks * perStack;
        }

        // LastStand: +50% damage for 5s after surviving lethal hit
        if (HasRelic(RelicType.LastStand) && lastStandBuff > 0)
            dmg *= 1.5f;

        // DashChain: tracked for free-cast integration with SpellCaster
        // (free-cast logic handled via dashChainFreeCast timer, SpellCaster reads HasFreeCast)

        // ── Conditional passive relics ──

        // FullPower: +20% damage at full HP
        if (HasRelic(RelicType.FullPower) && playerHealth != null)
        {
            if (playerHealth.currentHP >= playerHealth.maxHP)
                dmg *= 1.2f;
        }

        // FloorVeteran: +5% per floor cleared
        if (HasRelic(RelicType.FloorVeteran) && floorsCleared > 0)
            dmg *= 1f + floorsCleared * 0.05f;

        // Minimalist: +40% damage if 3 or fewer relics
        if (HasRelic(RelicType.Minimalist) && ownedRelics.Count <= 3)
            dmg *= 1.4f;

        // VoidAffinity: +10% damage (placeholder for void-specific bonus)
        if (HasRelic(RelicType.VoidAffinity))
            dmg *= 1.1f;

        // CrowdedRoom: +20% damage when 5+ enemies alive
        if (HasRelic(RelicType.CrowdedRoom))
        {
            int enemyCount = CountAliveEnemies();
            if (enemyCount >= 5)
                dmg *= 1.2f;
        }

        // ChargeHoarder: +15% damage when all charges are full
        // TODO: Integrate with SpellCaster.AllChargesFull when available
        if (HasRelic(RelicType.ChargeHoarder) && spellCasterRef != null)
        {
            // Check if no element is overheated as a proxy for "all charges full"
            if (!AnyOverheated())
                dmg *= 1.15f;
        }

        // OverheatMastery: +30% damage when any element is overheated
        if (HasRelic(RelicType.OverheatMastery) && AnyOverheated())
            dmg *= 1.3f;

        // ── Build-defining relics ──

        // Monolith: +60% damage when standing still (fully charged)
        if (HasRelic(RelicType.Monolith) && monolithActive)
            dmg *= 1.6f;

        // Gambler: 50% chance 3x damage, 50% chance 0 damage
        if (HasRelic(RelicType.Gambler))
        {
            if (Random.value < 0.5f)
                dmg *= 3f;
            else
                dmg *= 0f;
        }

        // Siphon: -50% damage, heal 50% of damage dealt
        if (HasRelic(RelicType.Siphon))
        {
            dmg *= 0.5f;
            int heal = Mathf.Max(1, Mathf.FloorToInt(dmg * 0.5f));
            if (playerHealth != null)
                playerHealth.Heal(heal);
        }

        // ElementalOverload: +25% damage always
        if (HasRelic(RelicType.ElementalOverload))
            dmg *= 1.25f;

        // Convergence: +40% damage (element lock handled in SpellCaster)
        // TODO: SpellCaster reads HasConvergence to lock element selection
        if (HasRelic(RelicType.Convergence))
            dmg *= 1.4f;

        // EntropyRelic: +30% damage during buff window
        if (HasRelic(RelicType.EntropyRelic) && entropyBuff > 0)
            dmg *= 1.3f;

        // ── Cursed relics ──

        // CursedAbsorption: +3% per kill stack (max 10)
        if (HasRelic(RelicType.CursedAbsorption) && absorptionStacks > 0)
            dmg *= 1f + absorptionStacks * 0.03f;

        // CursedEcho: 25% chance 1.5x damage, 10% chance self-hit 3 damage
        if (HasRelic(RelicType.CursedEcho))
        {
            float roll = Random.value;
            if (roll < 0.25f)
                dmg *= 1.5f;
            if (roll < 0.10f && playerHealth != null)
                playerHealth.TakeDamage(3);
        }

        // CursedHunger: +35% damage
        if (HasRelic(RelicType.CursedHunger))
            dmg *= 1.35f;

        // Underdog: damage bonus handled via speed buff in Update
        // (damage reduction is in ModifyIncomingDamage)

        // ── SYNERGIES ────────────────────────────────────────

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
        }

        // Glass Juggernaut synergy: Underdog + Berserker + GlassCannon + LastStand
        // Below 30% HP: additional +15% damage on top of individual bonuses
        if (HasRelic(RelicType.Underdog) && HasRelic(RelicType.Berserker)
            && HasRelic(RelicType.GlassCannon) && HasRelic(RelicType.LastStand)
            && playerHealth != null)
        {
            if (playerHealth.currentHP < playerHealth.maxHP * 0.3f)
                dmg *= 1.15f;
        }

        return dmg;
    }

    /// <summary>Whether the Chaotic Surge synergy just triggered (DoubleStrike+Chaos AoE).</summary>
    public bool ConsumeChaoticSurge()
    {
        if (synergyDoubleStrikeChaosCounter > 0) { synergyDoubleStrikeChaosCounter--; return true; }
        return false;
    }

    // ═════════════════════════════════════════════════════════
    //  MODIFY INCOMING DAMAGE
    // ═════════════════════════════════════════════════════════

    /// <summary>Called when player takes damage.</summary>
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
        if (HasRelic(RelicType.StoneSkin) && IsPlayerStandingStill())
            damage = Mathf.Max(0, damage - 1);

        // Cursed Power: take 1 extra damage per hit
        if (HasRelic(RelicType.CursedPower))
            damage += 1;

        // BloodPact: take 1 extra damage per hit (stacks with CursedPower)
        if (HasRelic(RelicType.BloodPact))
            damage += 1;

        // ── New defensive relics ──

        // LastStand: survive lethal hit once, gain +50% damage buff
        if (HasRelic(RelicType.LastStand) && !lastStandUsed && playerHealth != null)
        {
            if (playerHealth.currentHP <= damage)
            {
                lastStandUsed = true;
                lastStandBuff = 5f;
                playerHealth.currentHP = 1;
                playerHealth.InvokeHPChanged();
                GameFeel.ScreenFlash(new Color(1f, 0.9f, 0.2f), 0.3f);
                return 0;
            }
        }

        // Underdog: -1 damage when below 30% HP
        if (HasRelic(RelicType.Underdog) && playerHealth != null)
        {
            if (playerHealth.currentHP < playerHealth.maxHP * 0.3f)
                damage = Mathf.Max(0, damage - 1);
        }

        // MomentumArmor: -2 damage at momentum tier >= 3
        if (HasRelic(RelicType.MomentumArmor))
        {
            var momentum = GetComponent<MomentumSystem>();
            if (momentum != null && momentum.Tier >= 3)
                damage = Mathf.Max(0, damage - 2);
        }

        // EscapeArtist: survive lethal hit, teleport back + immunity (10s cooldown)
        if (HasRelic(RelicType.EscapeArtist) && escapeArtistCooldown <= 0 && playerHealth != null)
        {
            if (playerHealth.currentHP <= damage)
            {
                escapeArtistCooldown = 10f;
                // Teleport player 3 units backward
                if (playerCtrl != null)
                    playerCtrl.transform.position -= playerCtrl.transform.forward * 3f;
                playerHealth.GrantImmunity(1f);
                GameFeel.ScreenFlash(new Color(0.3f, 0.8f, 1f), 0.2f);
                return 0;
            }
        }

        // GoldShield: spend 10 gold to reduce 1 damage
        if (HasRelic(RelicType.GoldShield) && damage > 0)
        {
            // Golden Immortal synergy: half price (5 gold) if has all 4 gold relics
            int goldCost = (HasRelic(RelicType.CursedGold) && HasRelic(RelicType.CursedFortune)
                && HasRelic(RelicType.MagnetField)) ? 5 : 10;

            if (GoldSystem.Instance != null && GoldSystem.Instance.Gold >= goldCost)
            {
                GoldSystem.Instance.TrySpend(goldCost);
                damage = Mathf.Max(0, damage - 1);
            }
        }

        return damage;
    }

    // ═════════════════════════════════════════════════════════
    //  ON ROOM ENTER
    // ═════════════════════════════════════════════════════════

    /// <summary>Called at room start.</summary>
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

        // GoldRush: track room start time
        if (HasRelic(RelicType.GoldRush))
            roomStartTime = Time.time;

        // Cartographer: show room hints
        // TODO: Access room type data and display floating text hints
    }

    // ═════════════════════════════════════════════════════════
    //  ON DASH
    // ═════════════════════════════════════════════════════════

    /// <summary>Called when player dashes.</summary>
    public void OnDash(Vector3 from, Vector3 to)
    {
        // ── DashFire: fire trail ──
        if (HasRelic(RelicType.DashFire))
        {
            bool infernoSynergy = HasRelic(RelicType.CursedSpeed);
            float spacing = infernoSynergy ? 0.5f : 0.8f;
            float lifetime = infernoSynergy ? 4f : 2f;
            int dmg = infernoSynergy ? 4 : 2;

            Vector3 dir = to - from;
            float dist = dir.magnitude;
            dir.Normalize();
            int segments = Mathf.CeilToInt(dist / spacing);
            for (int i = 0; i < segments; i++)
            {
                Vector3 pos = from + dir * (i * spacing);
                CreateFireTrail(pos, lifetime, dmg, infernoSynergy);
            }
        }

        // ── DashRecharge: restore 1 overheated charge ──
        if (HasRelic(RelicType.DashRecharge) && spellCasterRef != null)
        {
            // Restore first overheated element's charge
            // TODO: Implement SpellCaster.RestoreOverheatedCharge() for full integration
            for (int i = 0; i < 4; i++)
            {
                if (spellCasterRef.IsOverheated(i))
                {
                    // Can't directly restore charges without SpellCaster API
                    // For now, the relic is tracked; SpellCaster will check HasDashRecharge
                    break;
                }
            }
        }

        // ── DashChain: free cast if dash hits 2+ enemies ──
        if (HasRelic(RelicType.DashChain))
        {
            Vector3 midpoint = (from + to) / 2f;
            float radius = (to - from).magnitude / 2f + 0.5f;
            Collider[] hits = Physics.OverlapSphere(midpoint, Mathf.Min(radius, 3f));
            int enemiesHit = 0;
            foreach (var hit in hits)
            {
                if (hit.GetComponent<PlayerController>() != null) continue;
                var hp = hit.GetComponent<Health>();
                if (hp != null && !hp.IsDead)
                    enemiesHit++;
            }
            dashChainEnemiesHit = enemiesHit;
            if (enemiesHit >= 2)
                dashChainFreeCast = 2f;
        }

        // ── PhantomCaster: spawn phantom at dash destination ──
        if (HasRelic(RelicType.PhantomCaster))
        {
            // Phantom Storm synergy: double duration and damage
            bool phantomStorm = HasRelic(RelicType.GaleRing) && HasRelic(RelicType.CursedSpeed);
            float phantomLifetime = phantomStorm ? 6f : 3f;
            int phantomDmg = phantomStorm ? 6 : 3;
            float phantomRadius = 2f;

            SpawnPhantom(to, phantomRadius, phantomDmg, phantomLifetime);
        }
    }

    void SpawnPhantom(Vector3 pos, float radius, int damage, float lifetime)
    {
        var phantom = GameObject.CreatePrimitive(PrimitiveType.Cube);
        phantom.name = "PhantomCaster";
        Object.Destroy(phantom.GetComponent<BoxCollider>());
        phantom.transform.position = pos + Vector3.up * 0.5f;
        phantom.transform.localScale = new Vector3(0.6f, 1.2f, 0.6f);
        var mat = ShaderCache.NewEmissive(new Color(0.4f, 0.2f, 0.8f, 0.5f), 2f);
        phantom.GetComponent<Renderer>().material = mat;

        // Add AoE damage zone
        var col = phantom.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = radius;
        var zone = phantom.AddComponent<DashFireZone>();
        zone.damage = damage;

        Object.Destroy(phantom, lifetime);
    }

    // ═════════════════════════════════════════════════════════
    //  ON ENEMY KILL
    // ═════════════════════════════════════════════════════════

    /// <summary>Called when an enemy is killed. Pass victim object and death position.</summary>
    public void OnEnemyKill(GameObject victim, Vector3 deathPos)
    {
        // ── Aftershock: AoE at death pos if momentum tier >= 2 ──
        if (HasRelic(RelicType.Aftershock) && aftershockCooldown <= 0)
        {
            var momentum = GetComponent<MomentumSystem>();
            if (momentum != null && momentum.Tier >= 2)
            {
                aftershockCooldown = 1.5f;
                AoEDamageAtPosition(deathPos, 2f, 3);
                SFXSystem.Play(SFXSystem.SFXType.Explosion, deathPos, 0.3f);
            }
        }

        // ── SoulHarvest: heal 1 HP every 8 kills ──
        if (HasRelic(RelicType.SoulHarvest))
        {
            soulHarvestKills++;
            if (soulHarvestKills >= 8)
            {
                soulHarvestKills = 0;
                if (playerHealth != null) playerHealth.Heal(1);
            }
        }

        // ── ElementalEcho: spread victim's status to nearby enemies ──
        if (HasRelic(RelicType.ElementalEcho) && victim != null)
        {
            var es = victim.GetComponent<ElementalStatus>();
            if (es != null)
            {
                Collider[] nearby = Physics.OverlapSphere(deathPos, 2f);
                foreach (var c in nearby)
                {
                    if (c.GetComponent<PlayerController>() != null) continue;
                    if (c.gameObject == victim) continue;
                    var hp = c.GetComponent<Health>();
                    var nes = c.GetComponent<ElementalStatus>();
                    if (hp != null && !hp.IsDead && nes != null)
                        nes.ApplyElement(ElementType.Fire, 0); // Simplified: apply fire status
                }
            }
        }

        // ── Cascade: AoE at death position ──
        if (HasRelic(RelicType.Cascade))
        {
            AoEDamageAtPosition(deathPos, 1.5f, 5);
            // VFX: small explosion
            var vfx = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(vfx.GetComponent<SphereCollider>());
            vfx.transform.position = deathPos + Vector3.up * 0.5f;
            vfx.transform.localScale = Vector3.one * 1.5f;
            vfx.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(1f, 0.5f, 0.2f), 3f);
            vfx.AddComponent<FlashShrink>().Init(0.2f);
        }

        // ── Scavenger: 15% chance to heal 1 HP ──
        if (HasRelic(RelicType.Scavenger))
        {
            if (Random.value < 0.15f && playerHealth != null)
                playerHealth.Heal(1);
        }

        // ── CursedAbsorption: gain a damage stack ──
        if (HasRelic(RelicType.CursedAbsorption))
            absorptionStacks = Mathf.Min(10, absorptionStacks + 1);

        // ── CursedHunger: reset kill timer ──
        if (HasRelic(RelicType.CursedHunger))
            hungerTimer = 0;
    }

    // ═════════════════════════════════════════════════════════
    //  PUBLIC HOOKS (called by other systems)
    // ═════════════════════════════════════════════════════════

    /// <summary>Called by SpellCaster when the active element is switched.</summary>
    public void OnElementSwitch()
    {
        if (HasRelic(RelicType.QuickSwap))
            quickSwapBuff = 1.5f;
    }

    /// <summary>Called when an element overheats.</summary>
    public void OnOverheat(ElementType element, Vector3 playerPos)
    {
        if (!HasRelic(RelicType.OverheatSurge)) return;

        float radius = 2.5f;
        int damage = 5;

        // Overheat Engine synergy: larger explosion if all 3 overheat relics owned
        if (HasRelic(RelicType.EntropyRelic) && HasRelic(RelicType.OverheatMastery))
        {
            radius *= 1.5f;
            // Grant 1s invulnerability
            if (playerHealth != null)
                playerHealth.GrantImmunity(1f);
        }

        // Damage nearby enemies
        AoEDamageAtPosition(playerPos, radius, damage);

        // VFX: colored explosion based on element
        Color explosionColor = GetElementColor(element);
        var vfx = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(vfx.GetComponent<SphereCollider>());
        vfx.transform.position = playerPos + Vector3.up * 0.5f;
        vfx.transform.localScale = Vector3.one * radius;
        vfx.GetComponent<Renderer>().material = ShaderCache.NewEmissive(explosionColor, 4f);
        vfx.AddComponent<FlashShrink>().Init(0.25f);

        SFXSystem.Play(SFXSystem.SFXType.Explosion, playerPos, 0.5f);
    }

    /// <summary>Called when a spell reaction occurs (for ReactorCore stacking).</summary>
    public void OnReaction()
    {
        if (HasRelic(RelicType.ReactorCore))
        {
            reactorStacks = Mathf.Min(5, reactorStacks + 1);
            reactorTimer = 8f;
        }
    }

    /// <summary>Called when a room is cleared. Handles GoldRush bonus.</summary>
    public void OnRoomCleared(Vector3 pos)
    {
        if (HasRelic(RelicType.GoldRush) && Time.time - roomStartTime < 15f)
            GoldSystem.SpawnGoldDrop(pos, 20);
    }

    /// <summary>Called when the player picks up gold (for CursedFortune).</summary>
    public void OnGoldPickup()
    {
        if (HasRelic(RelicType.CursedFortune) && playerHealth != null)
            playerHealth.Heal(1);
    }

    /// <summary>Called when a floor is cleared (for FloorVeteran).</summary>
    public void OnFloorCleared()
    {
        floorsCleared++;
    }

    // ═════════════════════════════════════════════════════════
    //  PUBLIC PROPERTIES (checked by other systems)
    // ═════════════════════════════════════════════════════════

    /// <summary>Extra rune choice from Lucky relic.</summary>
    public bool HasLucky => HasRelic(RelicType.Lucky) && Random.value < 0.2f;

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

    /// <summary>Check if DashChain free cast is active.</summary>
    public bool HasFreeCast => dashChainFreeCast > 0;

    /// <summary>Check if Merchant relic is owned (shop discount).</summary>
    public bool HasMerchant => HasRelic(RelicType.Merchant);

    /// <summary>Check if RuneRecycler is owned (skip overheat penalty).</summary>
    public bool HasRuneRecycler => HasRelic(RelicType.RuneRecycler);

    /// <summary>Check if DualElement is owned (apply both statuses on combo cast).</summary>
    public bool HasDualElement => HasRelic(RelicType.DualElement);

    /// <summary>Check if CursedVelocity is owned (SpellCaster reads for -30% cooldowns).</summary>
    public bool HasCursedVelocity => HasRelic(RelicType.CursedVelocity);

    /// <summary>Cooldown multiplier for LowTide relic (-15% when no element is overheated).</summary>
    public float GetCooldownMult()
    {
        if (HasRelic(RelicType.LowTide) && !AnyOverheated())
            return 0.85f;
        return 1f;
    }

    /// <summary>Variety bonus reduction for ComboMaster (reduces thresholds by 1).</summary>
    public int GetVarietyBonusReduction()
    {
        return HasRelic(RelicType.ComboMaster) ? 1 : 0;
    }

    /// <summary>30% chance to preserve reaction status (for ElementalStatus).</summary>
    public bool ShouldPreserveReactionStatus()
    {
        return HasRelic(RelicType.ReactionCatalyst) && Random.value < 0.3f;
    }

    /// <summary>Reaction damage bonus for CursedMirror (+50%).</summary>
    public float GetReactionBonus()
    {
        return HasRelic(RelicType.CursedMirror) ? 1.5f : 1f;
    }

    /// <summary>Whether CursedMirror should stun the player on reaction.</summary>
    public bool ShouldMirrorDebuff()
    {
        return HasRelic(RelicType.CursedMirror);
    }

    /// <summary>Shop price multiplier for CursedFortune (+50% prices).</summary>
    public float GetShopPriceMultiplier()
    {
        return HasRelic(RelicType.CursedFortune) ? 1.5f : 1f;
    }

    // ═════════════════════════════════════════════════════════
    //  ELEMENT TRACKING (PrismShard)
    // ═════════════════════════════════════════════════════════

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
            prismDamageBuff = 5f;
    }

    // ═════════════════════════════════════════════════════════
    //  REMOVE RELIC
    // ═════════════════════════════════════════════════════════

    /// <summary>Remove a relic and undo its passive effects.</summary>
    public void RemoveRelic(RelicSO relic)
    {
        ownedRelics.Remove(relic);
        // Note: some passives (speed, HP) are hard to undo precisely,
        // but for cursed relics this is primarily used for curse purification
    }

    // ═════════════════════════════════════════════════════════
    //  HELPERS
    // ═════════════════════════════════════════════════════════

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

    void AoEDamageAtPosition(Vector3 center, float radius, int damage)
    {
        Collider[] hits = Physics.OverlapSphere(center, radius);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
                hp.TakeDamage(damage);
        }
    }

    bool AnyOverheated()
    {
        if (spellCasterRef == null) return false;
        for (int i = 0; i < 4; i++)
        {
            if (spellCasterRef.IsOverheated(i))
                return true;
        }
        return false;
    }

    int CountAliveEnemies()
    {
        // Use FindObjectsByType to count alive enemies (non-player Health components)
        var allHealth = Object.FindObjectsByType<Health>(FindObjectsSortMode.None);
        int count = 0;
        foreach (var hp in allHealth)
        {
            if (hp.IsDead) continue;
            if (hp.GetComponent<PlayerController>() != null) continue;
            count++;
        }
        return count;
    }

    Color GetElementColor(ElementType element)
    {
        switch (element)
        {
            case ElementType.Fire:      return new Color(1f, 0.3f, 0.1f);
            case ElementType.Water:     return new Color(0.2f, 0.5f, 1f);
            case ElementType.Earth:     return new Color(0.6f, 0.4f, 0.2f);
            case ElementType.Air:       return new Color(0.7f, 0.9f, 1f);
            case ElementType.Lightning: return new Color(1f, 1f, 0.3f);
            case ElementType.Poison:    return new Color(0.3f, 0.8f, 0.2f);
            case ElementType.Void:      return new Color(0.5f, 0.1f, 0.8f);
            default:                    return Color.white;
        }
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
