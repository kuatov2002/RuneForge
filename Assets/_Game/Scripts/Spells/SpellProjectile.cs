using UnityEngine;
using System.Collections.Generic;

public class SpellProjectile : MonoBehaviour
{
    Vector3 direction;
    float speed;
    float damage;
    ElementSO element;
    float maxDistance;
    Vector3 startPos;
    bool initialized;

    // Modifier state
    int pierceLeft;
    int bounceLeft;
    float leechPercent;
    bool isHoming;
    float homingTurnSpeed;
    float homingDetectRange;
    bool isVolatile;
    float volatileMissChance;
    Health playerHealth;

    HashSet<Collider> hitTargets = new();

    public void Setup(Vector3 dir, float spd, float dmg, ElementSO elem, float range)
    {
        direction = dir.normalized;
        speed = spd;
        damage = dmg;
        element = elem;
        maxDistance = range;
        startPos = transform.position;
        initialized = true;

        // Trail
        var trail = gameObject.AddComponent<TrailRenderer>();
        trail.startWidth = 0.2f;
        trail.endWidth = 0f;
        trail.time = 0.25f;
        var tMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        tMat.color = elem.color;
        trail.material = tMat;
        trail.startColor = elem.color;
        Color endCol = elem.color;
        endCol.a = 0;
        trail.endColor = endCol;
    }

    public void ApplyModifier(ModifierSO mod, Health playerHp)
    {
        if (mod == null) return;
        playerHealth = playerHp;

        switch (mod.modifierType)
        {
            case ModifierType.Pierce:
                pierceLeft = mod.pierceCount;
                break;
            case ModifierType.Bounce:
                bounceLeft = mod.bounceCount;
                break;
            case ModifierType.Leech:
                leechPercent = mod.leechPercent;
                break;
            case ModifierType.Oversize:
                transform.localScale *= mod.sizeMultiplier;
                speed *= mod.speedPenalty;
                break;
            case ModifierType.Volatile:
                isVolatile = true;
                volatileMissChance = mod.volatileMissChance;
                break;
            case ModifierType.Homing:
                isHoming = true;
                homingTurnSpeed = mod.homingTurnSpeed;
                homingDetectRange = mod.homingDetectRange;
                speed *= mod.homingSpeedMult;
                break;
        }
    }

    void Update()
    {
        if (!initialized) return;

        // Homing: steer toward nearest enemy
        if (isHoming)
        {
            var target = FindClosestEnemy();
            if (target != null)
            {
                Vector3 toTarget = (target.transform.position + Vector3.up * 0.5f - transform.position).normalized;
                direction = Vector3.RotateTowards(direction, toTarget, homingTurnSpeed * Mathf.Deg2Rad * Time.deltaTime, 0f).normalized;
            }
        }

        transform.position += direction * speed * Time.deltaTime;
        if (Vector3.Distance(startPos, transform.position) > maxDistance)
            Destroy(gameObject);
    }

    Transform FindClosestEnemy()
    {
        Collider[] nearby = Physics.OverlapSphere(transform.position, homingDetectRange);
        Transform best = null;
        float bestDist = float.MaxValue;
        foreach (var col in nearby)
        {
            if (col.GetComponent<PlayerController>() != null) continue;
            var h = col.GetComponent<Health>();
            if (h == null || h.IsDead) continue;
            float d = Vector3.Distance(transform.position, col.transform.position);
            if (d < bestDist) { bestDist = d; best = col.transform; }
        }
        return best;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!initialized) return;
        if (other.GetComponent<PlayerController>() != null) return;
        if (other.GetComponent<SpellProjectile>() != null) return;
        if (other.GetComponent<OrbitForm>() != null) return;
        if (other.GetComponent<TrapForm>() != null) return;
        if (other.GetComponent<OrbitDamager>() != null) return;

        // Skip already-pierced targets
        if (hitTargets.Contains(other)) return;

        var health = other.GetComponent<Health>();
        if (health != null && !health.IsDead)
        {
            // Shield Bearer block check
            var shield = other.GetComponent<ShieldBearerAI>();
            if (shield != null && shield.IsBlockedFromDirection(startPos))
                return; // Blocked by shield

            // Mirror Knight shield check
            var mirrorKnight = other.GetComponent<MirrorKnightBoss>();
            if (mirrorKnight != null && mirrorKnight.ShouldBlockDamage())
                return;

            // Lich teleport immunity
            var lich = other.GetComponent<LichBoss>();
            if (lich != null && lich.IsImmune)
                return;

            // Volatile miss check
            if (isVolatile && Random.value < volatileMissChance)
            {
                // Miss — continue without hitting
                return;
            }

            float finalDmg = damage;
            if (playerHealth != null)
            {
                var relicMgr = playerHealth.GetComponent<RelicManager>();
                if (relicMgr != null) finalDmg = relicMgr.ModifyDamage(finalDmg);
            }
            health.TakeDamage(finalDmg);
            health.ApplyStatusEffect(element);

            // Leech
            if (leechPercent > 0 && playerHealth != null)
            {
                int healAmt = Mathf.Max(1, Mathf.CeilToInt(damage * leechPercent));
                playerHealth.Heal(healAmt);
            }

            // Chain lightning
            if (element.statusEffect == StatusEffectType.Chain)
                ChainToNearby(other.transform.position, other, element.chainCount, damage * 0.5f);

            hitTargets.Add(other);

            // Pierce: don't destroy, keep going
            if (pierceLeft > 0)
            {
                pierceLeft--;
                return;
            }

            // Bounce: redirect toward nearest other enemy
            if (bounceLeft > 0)
            {
                bounceLeft--;
                var next = FindBounceTarget(other);
                if (next != null)
                {
                    direction = (next.transform.position - transform.position).normalized;
                    direction.y = 0;
                    direction.Normalize();
                    return;
                }
            }
        }
        else if (health == null)
        {
            // Hit a wall/obstacle
            if (bounceLeft > 0)
            {
                bounceLeft--;
                // Reflect off wall
                if (Physics.Raycast(transform.position - direction * 0.5f, direction, out RaycastHit wallHit, 1.5f))
                {
                    direction = Vector3.Reflect(direction, wallHit.normal);
                    direction.y = 0;
                    direction.Normalize();
                    startPos = transform.position; // reset distance
                }
                return;
            }
        }

        Destroy(gameObject);
    }

    Transform FindBounceTarget(Collider exclude)
    {
        Collider[] nearby = Physics.OverlapSphere(transform.position, 8f);
        Transform best = null;
        float bestDist = float.MaxValue;
        foreach (var col in nearby)
        {
            if (col == exclude) continue;
            if (hitTargets.Contains(col)) continue;
            if (col.GetComponent<PlayerController>() != null) continue;
            var h = col.GetComponent<Health>();
            if (h == null || h.IsDead) continue;
            float d = Vector3.Distance(transform.position, col.transform.position);
            if (d < bestDist) { bestDist = d; best = col.transform; }
        }
        return best;
    }

    void ChainToNearby(Vector3 hitPos, Collider originalTarget, int chainCount, float chainDamage)
    {
        Collider[] nearby = Physics.OverlapSphere(hitPos, element.chainRadius);
        int chained = 0;
        foreach (var col in nearby)
        {
            if (chained >= chainCount) break;
            if (col == originalTarget) continue;
            if (col.GetComponent<PlayerController>() != null) continue;
            var h = col.GetComponent<Health>();
            if (h == null || h.IsDead) continue;

            h.TakeDamage(chainDamage);
            h.ApplyStatusEffect(element);
            CreateChainArc(hitPos, col.transform.position);
            hitPos = col.transform.position;
            chained++;
        }
    }

    static void CreateChainArc(Vector3 from, Vector3 to)
    {
        var go = new GameObject("ChainArc");
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 4;
        lr.startWidth = 0.08f;
        lr.endWidth = 0.08f;
        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.color = new Color(1f, 1f, 0.3f);
        lr.material = mat;
        lr.startColor = new Color(1f, 1f, 0.3f);
        lr.endColor = new Color(0.5f, 0.5f, 1f);

        Vector3 mid = (from + to) * 0.5f;
        Vector3 perp = Vector3.Cross(to - from, Vector3.up).normalized;
        lr.SetPosition(0, from + Vector3.up * 0.5f);
        lr.SetPosition(1, Vector3.Lerp(from, mid, 0.5f) + perp * Random.Range(-0.4f, 0.4f) + Vector3.up * 0.5f);
        lr.SetPosition(2, Vector3.Lerp(mid, to, 0.5f) + perp * Random.Range(-0.4f, 0.4f) + Vector3.up * 0.5f);
        lr.SetPosition(3, to + Vector3.up * 0.5f);

        Destroy(go, 0.15f);
    }
}
