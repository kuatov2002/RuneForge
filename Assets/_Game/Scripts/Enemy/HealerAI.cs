using UnityEngine;

/// <summary>
/// Healer — support enemy that heals wounded allies.
/// Mechanics:
/// - Stays behind other enemies, keeps 8u from player
/// - HEAL PULSE: Every 4s, heals nearest wounded ally for 3 HP (green beam VFX)
/// - FLEE: If player gets within 4u, dashes away
/// - No direct attack — pure support, high priority target
/// </summary>
public class HealerAI : MonoBehaviour
{
    public float moveSpeed = 3f;
    public Color baseColor = new Color(0.2f, 0.8f, 0.3f);

    Transform target;
    Health health;
    Rigidbody rb;
    Renderer[] renderers;
    bool isDead;

    float healCooldown = 2f; // starts sooner for first heal
    const float HealInterval = 4f;
    const int HealAmount = 3;
    const float HealRange = 8f;
    const float FleeDistance = 4f;
    const float PreferredDistance = 8f;

    // Flee dash
    bool isFleeing;
    float fleeTimer;
    Vector3 fleeDir;

    // Heal beam VFX
    LineRenderer healBeam;
    float beamTimer;

    // Void pull
    Vector3 pullTarget;
    float pullTimer;

    // Aura particles
    ParticleSystem auraPS;

    void Start()
    {
        var player = FindAnyObjectByType<PlayerController>();
        if (player != null) target = player.transform;
        health = GetComponent<Health>();
        rb = GetComponent<Rigidbody>();
        renderers = GetComponentsInChildren<Renderer>();
        if (health != null) health.OnDeath += OnDie;
        CreateAura();
    }

    void CreateAura()
    {
        var auraGO = new GameObject("HealAura");
        auraGO.transform.parent = transform;
        auraGO.transform.localPosition = Vector3.up * 0.5f;
        auraPS = auraGO.AddComponent<ParticleSystem>();
        auraPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = auraPS.main;
        main.startLifetime = 1f;
        main.startSpeed = 0.3f;
        main.startSize = 0.06f;
        main.startColor = new Color(0.3f, 1f, 0.4f, 0.6f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.loop = true;
        var em = auraPS.emission;
        em.rateOverTime = 5;
        var sh = auraPS.shape;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = 0.4f;
        var sol = auraPS.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 1, 1, 0));
        var mat = new Material(ShaderCache.ParticleShader);
        mat.SetColor("_Color", new Color(0.3f, 1f, 0.4f));
        auraGO.GetComponent<ParticleSystemRenderer>().material = mat;
        auraPS.Play();
    }

    void OnDie()
    {
        isDead = true;
        if (auraPS != null) auraPS.Stop();
        GameFeel.SpawnDeathVFX(transform.position, baseColor);
        Destroy(gameObject, 0.3f);
    }

    public void ApplyPull(Vector3 pullTo, float duration)
    {
        pullTarget = pullTo;
        pullTimer = duration;
    }

    void Update()
    {
        if (isDead || target == null) return;

        // Void pull
        if (pullTimer > 0)
        {
            pullTimer -= Time.deltaTime;
            Vector3 dir = pullTarget - transform.position; dir.y = 0;
            if (dir.magnitude > 0.3f)
                rb.MovePosition(transform.position + dir.normalized * 6f * Time.deltaTime);
            return;
        }

        if (health.IsStunned) return;

        UpdateStatusVisual();

        float dist = Vector3.Distance(transform.position, target.position);
        float speedMult = health.SpeedMultiplier;

        // Heal beam fade
        if (beamTimer > 0)
        {
            beamTimer -= Time.deltaTime;
            if (beamTimer <= 0 && healBeam != null)
            {
                Destroy(healBeam.gameObject);
                healBeam = null;
            }
        }

        // Flee if player is too close
        if (dist < FleeDistance && !isFleeing)
        {
            isFleeing = true;
            fleeTimer = 0.3f;
            fleeDir = (transform.position - target.position).normalized;
            if (fleeDir.sqrMagnitude < 0.01f) fleeDir = Random.insideUnitSphere; fleeDir.y = 0; fleeDir.Normalize();
        }

        if (isFleeing)
        {
            fleeTimer -= Time.deltaTime;
            rb.MovePosition(transform.position + fleeDir * moveSpeed * 2.5f * speedMult * Time.deltaTime);
            if (fleeTimer <= 0) isFleeing = false;
            return;
        }

        // Heal logic
        healCooldown -= Time.deltaTime;
        if (healCooldown <= 0)
        {
            healCooldown = HealInterval;
            TryHealAlly();
        }

        // Movement: stay at preferred distance from player, move toward nearest ally
        Vector3 moveDir = Vector3.zero;

        if (dist < PreferredDistance)
        {
            // Move away from player
            moveDir = (transform.position - target.position).normalized;
        }
        else if (dist > PreferredDistance + 3f)
        {
            // Too far, approach slightly
            moveDir = (target.position - transform.position).normalized * 0.5f;
        }

        // Also try to stay near allies
        var nearestAlly = FindNearestAlly();
        if (nearestAlly != null)
        {
            float allyDist = Vector3.Distance(transform.position, nearestAlly.position);
            if (allyDist > 4f)
            {
                moveDir += (nearestAlly.position - transform.position).normalized * 0.7f;
            }
        }

        if (moveDir.sqrMagnitude > 0.01f)
        {
            moveDir.y = 0;
            moveDir.Normalize();
            rb.MovePosition(transform.position + moveDir * moveSpeed * speedMult * Time.deltaTime);
            transform.rotation = Quaternion.LookRotation(moveDir);
        }
    }

    void TryHealAlly()
    {
        Health bestTarget = null;
        float bestHP = float.MaxValue;

        var allHealth = FindObjectsByType<Health>(FindObjectsSortMode.None);
        foreach (var h in allHealth)
        {
            if (h.gameObject == gameObject) continue;
            if (h.IsDead) continue;
            if (h.GetComponent<PlayerController>() != null) continue;
            if (h.currentHP >= h.maxHP) continue; // only heal wounded

            float d = Vector3.Distance(transform.position, h.transform.position);
            if (d > HealRange) continue;

            float hpPercent = (float)h.currentHP / h.maxHP;
            if (hpPercent < bestHP)
            {
                bestHP = hpPercent;
                bestTarget = h;
            }
        }

        if (bestTarget != null)
        {
            bestTarget.Heal(HealAmount);
            ShowHealBeam(bestTarget.transform);
            SFXSystem.Play(SFXSystem.SFXType.LevelUp, transform.position, 0.4f);
        }
    }

    void ShowHealBeam(Transform healTarget)
    {
        if (healBeam != null) Destroy(healBeam.gameObject);

        var beamGO = new GameObject("HealBeam");
        healBeam = beamGO.AddComponent<LineRenderer>();
        healBeam.startWidth = 0.08f;
        healBeam.endWidth = 0.08f;
        healBeam.material = ShaderCache.NewEmissive(new Color(0.2f, 1f, 0.3f), 4f);
        healBeam.startColor = new Color(0.2f, 1f, 0.3f, 0.8f);
        healBeam.endColor = new Color(0.2f, 1f, 0.3f, 0.2f);
        healBeam.positionCount = 2;
        healBeam.SetPosition(0, transform.position + Vector3.up * 0.5f);
        healBeam.SetPosition(1, healTarget.position + Vector3.up * 0.5f);
        beamTimer = 0.4f;
    }

    Transform FindNearestAlly()
    {
        Transform best = null;
        float bestDist = float.MaxValue;
        var allHealth = FindObjectsByType<Health>(FindObjectsSortMode.None);
        foreach (var h in allHealth)
        {
            if (h.gameObject == gameObject) continue;
            if (h.IsDead) continue;
            if (h.GetComponent<PlayerController>() != null) continue;
            float d = Vector3.Distance(transform.position, h.transform.position);
            if (d < bestDist) { bestDist = d; best = h.transform; }
        }
        return best;
    }

    void UpdateStatusVisual()
    {
        if (renderers == null) return;
        Color col = baseColor;
        if (health.IsStunned) col = health.IsFrozen ? new Color(0.6f, 0.9f, 1f) : new Color(1f, 1f, 0.3f);
        foreach (var r in renderers)
            if (r != null && r.gameObject.name != "Eye" && r.gameObject.name != "HealAura") r.material.color = col;
    }

    void OnDestroy() { if (health != null) health.OnDeath -= OnDie; }
}
