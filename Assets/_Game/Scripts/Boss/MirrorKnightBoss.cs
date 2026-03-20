using UnityEngine;

public class MirrorKnightBoss : MonoBehaviour
{
    public float moveSpeed = 3f;
    public float chargeSpeed = 10f;
    public float chargeCooldown = 4f;
    public int attackDamage = 3;
    public float shieldUpTime = 3f;
    public float attackWindowTime = 2f;
    public Color baseColor = new Color(0.6f, 0.6f, 0.7f);

    Transform target;
    Health health;
    Rigidbody rb;
    Renderer[] renderers;
    bool isDead;

    enum State { ShieldUp, Charging, Attacking, Cooldown }
    State state = State.ShieldUp;
    float stateTimer;
    Vector3 chargeDir;
    bool isVulnerable;

    // Shield reflects projectiles by being immune when shieldUp
    public bool IsShielded => state == State.ShieldUp || state == State.Cooldown;

    void Start()
    {
        var player = FindAnyObjectByType<PlayerController>();
        if (player != null) target = player.transform;
        health = GetComponent<Health>();
        rb = GetComponent<Rigidbody>();
        renderers = GetComponentsInChildren<Renderer>();
        stateTimer = shieldUpTime;
        if (health != null)
            health.OnDeath += () => { isDead = true; Destroy(gameObject, 0.5f); };
    }

    void Update()
    {
        if (isDead || target == null) return;
        if (health.IsStunned) return;
        float speedMult = health.SpeedMultiplier;

        stateTimer -= Time.deltaTime;

        switch (state)
        {
            case State.ShieldUp:
                // Circle player
                Vector3 toP = target.position - transform.position;
                toP.y = 0;
                Vector3 strafe = Vector3.Cross(toP.normalized, Vector3.up);
                rb.MovePosition(transform.position + strafe * moveSpeed * speedMult * Time.deltaTime);
                transform.rotation = Quaternion.LookRotation(toP.normalized);

                if (stateTimer <= 0)
                {
                    state = State.Charging;
                    chargeDir = toP.normalized;
                    stateTimer = 1f; // charge for 1 sec
                }
                break;

            case State.Charging:
                rb.MovePosition(transform.position + chargeDir * chargeSpeed * speedMult * Time.deltaTime);
                if (stateTimer <= 0)
                {
                    // Sword sweep — damage nearby player
                    Collider[] hits = Physics.OverlapSphere(transform.position, 2f);
                    foreach (var h in hits)
                    {
                        var pc = h.GetComponent<PlayerController>();
                        if (pc == null || pc.isInvulnerable) continue;
                        var hp = h.GetComponent<Health>();
                        if (hp != null && !hp.IsDead) hp.TakeDamage(attackDamage);
                    }
                    state = State.Attacking;
                    stateTimer = attackWindowTime;
                    isVulnerable = true;
                    // Visual cue: turn reddish
                    foreach (var r in renderers)
                        if (r != null) r.material.color = new Color(0.9f, 0.3f, 0.3f);
                }
                break;

            case State.Attacking:
                // Vulnerable window - stands still
                if (stateTimer <= 0)
                {
                    isVulnerable = false;
                    state = State.Cooldown;
                    stateTimer = 1f;
                    foreach (var r in renderers)
                        if (r != null) r.material.color = baseColor;
                }
                break;

            case State.Cooldown:
                if (stateTimer <= 0)
                {
                    state = State.ShieldUp;
                    stateTimer = shieldUpTime;
                }
                break;
        }
    }

    // Called from SpellProjectile to check if damage should be reflected
    public bool ShouldBlockDamage()
    {
        return !isVulnerable;
    }
}
