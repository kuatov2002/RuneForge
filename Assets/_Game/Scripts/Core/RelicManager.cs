using UnityEngine;
using System.Collections.Generic;

public class RelicManager : MonoBehaviour
{
    List<RelicSO> ownedRelics = new();
    Health playerHealth;
    PlayerController playerCtrl;

    // Relic state
    int hitCounter;
    float regenTimer;
    bool roomShieldActive;

    public List<RelicSO> OwnedRelics => ownedRelics;

    RelicSO[] allRelicsRef; // reference to all relics for loading

    public void Init(Health hp, PlayerController ctrl, RelicSO[] allRelics)
    {
        playerHealth = hp;
        playerCtrl = ctrl;
        allRelicsRef = allRelics;
        LoadRelics();
    }

    public void ClearRelics()
    {
        ownedRelics.Clear();
        hitCounter = 0;
        regenTimer = 0;
        roomShieldActive = false;
        PlayerPrefs.DeleteKey("owned_relics");
        PlayerPrefs.Save();
    }

    public void AddRelic(RelicSO relic)
    {
        ownedRelics.Add(relic);
        ApplyPassive(relic);
        SaveRelics();
    }

    void SaveRelics()
    {
        var types = new List<string>();
        foreach (var r in ownedRelics)
            types.Add(r.relicType.ToString());
        PlayerPrefs.SetString("owned_relics", string.Join(",", types));
        PlayerPrefs.Save();
    }

    void LoadRelics()
    {
        string saved = PlayerPrefs.GetString("owned_relics", "");
        if (string.IsNullOrEmpty(saved) || allRelicsRef == null) return;

        string[] types = saved.Split(',');
        foreach (string t in types)
        {
            if (string.IsNullOrEmpty(t)) continue;
            if (!System.Enum.TryParse<RelicType>(t, out var relicType)) continue;
            // Find matching relic SO
            foreach (var r in allRelicsRef)
            {
                if (r.relicType == relicType && !HasRelic(relicType))
                {
                    ownedRelics.Add(r);
                    ApplyPassive(r);
                    break;
                }
            }
        }
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
        }
    }

    void Update()
    {
        if (playerHealth == null || playerHealth.IsDead) return;

        // Regeneration: heal 1 HP every 30s
        if (HasRelic(RelicType.Regeneration))
        {
            regenTimer += Time.deltaTime;
            if (regenTimer >= 30f)
            {
                regenTimer = 0;
                playerHealth.Heal(1);
            }
        }
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
        if (HasRelic(RelicType.DoubleStrike) && hitCounter % 5 == 0)
            dmg *= 2f;

        return dmg;
    }

    // Called when player takes damage
    public int ModifyIncomingDamage(int damage)
    {
        // Shield: block first hit per room
        if (roomShieldActive && HasRelic(RelicType.Shield))
        {
            roomShieldActive = false;
            return 0;
        }

        // Thorns: reflect 1 damage back (handled elsewhere via event)
        return damage;
    }

    // Called at room start
    public void OnRoomEnter()
    {
        if (HasRelic(RelicType.Shield))
            roomShieldActive = true;

        // VampireAura: heal 1 HP per room cleared
        if (HasRelic(RelicType.VampireAura) && playerHealth != null)
            playerHealth.Heal(1);
    }

    // Called when player dashes
    public void OnDash(Vector3 from, Vector3 to)
    {
        if (!HasRelic(RelicType.DashFire)) return;

        // Create fire trail
        Vector3 dir = (to - from);
        float dist = dir.magnitude;
        dir.Normalize();
        int segments = Mathf.CeilToInt(dist / 0.8f);
        for (int i = 0; i < segments; i++)
        {
            Vector3 pos = from + dir * (i * 0.8f);
            CreateFireTrail(pos);
        }
    }

    void CreateFireTrail(Vector3 pos)
    {
        var fire = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        fire.name = "DashFire";
        Destroy(fire.GetComponent<CapsuleCollider>());
        var col = fire.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.6f;
        fire.transform.position = pos + Vector3.up * 0.05f;
        fire.transform.localScale = new Vector3(0.8f, 0.05f, 0.8f);
        var mat = ShaderCache.NewEmissive(new Color(1f, 0.4f, 0.1f, 0.6f));
        mat.SetColor("_EmissionColor", new Color(1f, 0.3f, 0f) * 3f);
        fire.GetComponent<Renderer>().material = mat;
        fire.AddComponent<DashFireZone>();
        Destroy(fire, 2f);
    }

    // Extra rune choice from Lucky relic
    public bool HasLucky => HasRelic(RelicType.Lucky) && Random.value < 0.2f;
}

// Fire zone from dash relic — damages enemies standing in it
public class DashFireZone : MonoBehaviour
{
    float tickTimer;

    void OnTriggerStay(Collider other)
    {
        tickTimer -= Time.deltaTime;
        if (tickTimer > 0) return;
        tickTimer = 0.5f;

        if (other.GetComponent<PlayerController>() != null) return;
        var hp = other.GetComponent<Health>();
        if (hp != null && !hp.IsDead)
            hp.TakeDamage(2);
    }
}
