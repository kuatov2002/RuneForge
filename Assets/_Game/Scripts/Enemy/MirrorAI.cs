using UnityEngine;

public class MirrorAI : MonoBehaviour
{
    public float moveSpeed = 2.5f;
    public float preferredDist = 5f;
    public float shootCooldown = 2.5f;
    public float projectileSpeed = 9f;
    public int attackDamage = 2;
    public Color baseColor = new Color(0.7f, 0.7f, 0.75f);

    Transform target;
    Health health;
    Rigidbody rb;
    Renderer[] renderers;
    bool isDead;
    float shootTimer;

    ElementSO copiedElement;
    Color copiedColor;

    void Start()
    {
        var player = FindAnyObjectByType<PlayerController>();
        if (player != null)
        {
            target = player.transform;
            var caster = player.GetComponent<SpellCaster>();
            if (caster != null)
                caster.OnOrbsChanged += OnPlayerOrbsChanged;
        }
        health = GetComponent<Health>();
        rb = GetComponent<Rigidbody>();
        renderers = GetComponentsInChildren<Renderer>();
        if (health != null) health.OnDeath += OnDie;
        copiedColor = baseColor;
    }

    void OnPlayerOrbsChanged()
    {
        if (isDead || target == null) return;
        var caster = target.GetComponent<SpellCaster>();
        if (caster == null || caster.rightOrb == null) return;
        copiedElement = caster.rightOrb;
        copiedColor = Color.Lerp(baseColor, copiedElement.color, 0.6f);
    }

    void OnDie()
    {
        isDead = true;
        foreach (var r in renderers) if (r != null) r.material.color = Color.gray;
        Destroy(gameObject, 0.3f);
    }

    void Update()
    {
        if (isDead || target == null) return;
        UpdateVisual();
        if (health.IsStunned) return;
        float speedMult = health.SpeedMultiplier;

        Vector3 toPlayer = target.position - transform.position;
        toPlayer.y = 0;
        float dist = toPlayer.magnitude;

        // Keep at preferred distance
        Vector3 moveDir = Vector3.zero;
        if (dist > preferredDist + 1f) moveDir = toPlayer.normalized;
        else if (dist < preferredDist - 1f) moveDir = -toPlayer.normalized;

        if (moveDir.sqrMagnitude > 0.01f)
            rb.MovePosition(transform.position + moveDir * moveSpeed * speedMult * Time.deltaTime);

        transform.rotation = Quaternion.LookRotation(toPlayer.normalized);

        // Shoot with copied element
        shootTimer -= Time.deltaTime;
        if (shootTimer <= 0 && dist < 12f)
        {
            shootTimer = shootCooldown;
            Color projColor = copiedElement != null ? copiedElement.color : baseColor;
            var proj = EnemyProjectile.Create(transform.position, toPlayer.normalized, projectileSpeed,
                attackDamage, projColor, copiedElement);
        }
    }

    void UpdateVisual()
    {
        if (renderers == null) return;
        Color col = copiedColor;
        if (health.IsStunned) col = health.IsFrozen ? new Color(0.6f, 0.9f, 1f) : new Color(1f, 1f, 0.3f);
        // Removed old status effect visuals (Slow, Poison no longer tracked on Health)
        foreach (var r in renderers)
            if (r != null && r.gameObject.name != "Eye") r.material.color = col;
    }

    void OnDestroy()
    {
        if (health != null) health.OnDeath -= OnDie;
        if (target != null)
        {
            var caster = target.GetComponent<SpellCaster>();
            if (caster != null) caster.OnOrbsChanged -= OnPlayerOrbsChanged;
        }
    }
}
