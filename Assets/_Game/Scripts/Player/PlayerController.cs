using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 6f;
    public float dashDistance = 3f;
    public float dashCooldown = 1f;

    const float DashDuration = 0.15f;
    const float BaseIFrameDuration = 0.2f;

    /// <summary>Extra i-frame duration from Phase Step meta-upgrade.</summary>
    [HideInInspector] public float phaseStepBonus;

    /// <summary>Damage dealt to enemies when dashing through them (Blink Strike).</summary>
    [HideInInspector] public int blinkStrikeDamage;

    float IFrameDuration => BaseIFrameDuration + phaseStepBonus;

    [HideInInspector] public bool isInvulnerable;

    Rigidbody rb;
    float dashCDTimer;
    bool isDashing;
    float dashTimer;
    float ghostTimer;

    // Multi-dash charges
    int maxDashCharges = 1;
    int currentDashCharges;
    float chargeRechargeTimer;

    /// <summary>Dash cooldown progress 0 (ready) to 1 (full CD).</summary>
    public float DashCooldownNormalized => dashCooldown > 0 ? Mathf.Clamp01(chargeRechargeTimer / dashCooldown) : 0f;
    public int CurrentDashCharges => currentDashCharges;
    public int MaxDashCharges => maxDashCharges;

    public void SetExtraDashCharges(int extra)
    {
        maxDashCharges = 1 + extra;
        currentDashCharges = maxDashCharges;
    }

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        currentDashCharges = maxDashCharges;
    }

    void Update()
    {
        if (isDashing)
        {
            dashTimer -= Time.deltaTime;

            // Spawn afterimage ghosts during dash
            ghostTimer -= Time.deltaTime;
            if (ghostTimer <= 0)
            {
                ghostTimer = 0.03f;
                var renderers = GetComponentsInChildren<Renderer>();
                GameFeel.SpawnDashGhost(renderers, transform.position, transform.rotation);
            }

            if (dashTimer <= 0)
                isDashing = false;
            return;
        }

        var kb = Keyboard.current;
        if (kb == null) return;

        Vector2 input = Vector2.zero;
        if (kb.wKey.isPressed) input.y += 1;
        if (kb.sKey.isPressed) input.y -= 1;
        if (kb.aKey.isPressed) input.x -= 1;
        if (kb.dKey.isPressed) input.x += 1;
        input = input.normalized;

        // Spell Rush buff tick
        if (spellRushTimer > 0) spellRushTimer -= Time.deltaTime;

        // Apply momentum speed bonus + spell rush buff to movement
        float effectiveSpeed = moveSpeed;
        var momentumSys = GetComponent<MomentumSystem>();
        if (momentumSys != null)
            effectiveSpeed *= (1f + momentumSys.SpeedBonus);
        if (spellRushTimer > 0)
            effectiveSpeed *= (1f + spellRushMultiplier);

        Vector3 vel = new Vector3(input.x, 0, input.y) * effectiveSpeed;
        rb.linearVelocity = new Vector3(vel.x, rb.linearVelocity.y, vel.z);

        // Aim toward mouse
        var mouse = Mouse.current;
        if (mouse != null && Camera.main != null)
        {
            Ray ray = Camera.main.ScreenPointToRay(mouse.position.ReadValue());
            Plane plane = new Plane(Vector3.up, transform.position.y);
            if (plane.Raycast(ray, out float dist))
            {
                Vector3 point = ray.GetPoint(dist);
                Vector3 dir = point - transform.position;
                dir.y = 0;
                if (dir.sqrMagnitude > 0.1f)
                    transform.rotation = Quaternion.LookRotation(dir);
            }
        }

        // Potion (F key)
        if (kb.fKey.wasPressedThisFrame && potionsRemaining > 0)
        {
            var hp = GetComponent<Health>();
            if (hp != null && hp.currentHP < hp.maxHP)
            {
                hp.Heal(potionHealAmount);
                potionsRemaining--;
            }
        }

        // Recharge dash charges
        if (currentDashCharges < maxDashCharges)
        {
            chargeRechargeTimer -= Time.deltaTime;
            if (chargeRechargeTimer <= 0)
            {
                currentDashCharges++;
                chargeRechargeTimer = dashCooldown;
            }
        }

        // Dash — dash in movement direction (WASD), or forward if no input
        if (mouse != null && mouse.rightButton.wasPressedThisFrame && currentDashCharges > 0)
        {
            Vector3 dashFrom = transform.position;
            Vector3 dashDir = input.sqrMagnitude > 0.01f
                ? new Vector3(input.x, 0, input.y).normalized
                : transform.forward;
            isDashing = true;
            isInvulnerable = true;
            dashTimer = DashDuration;
            currentDashCharges--;
            chargeRechargeTimer = dashCooldown;
            rb.linearVelocity = dashDir * (dashDistance / DashDuration);
            Invoke(nameof(EndIFrames), IFrameDuration);
            SFXSystem.Play(SFXSystem.SFXType.Dash, transform.position);

            // Apply momentum speed bonus
            var momentum = GetComponent<MomentumSystem>();
            if (momentum != null)
                rb.linearVelocity *= (1f + momentum.SpeedBonus);

            // Blink Strike: damage enemies along dash path
            if (blinkStrikeDamage > 0)
            {
                var hits = Physics.SphereCastAll(dashFrom, 0.8f, dashDir, dashDistance, ~0, QueryTriggerInteraction.Ignore);
                foreach (var hit in hits)
                {
                    var hp = hit.collider.GetComponent<Health>();
                    if (hp != null && hp.gameObject != gameObject && !hp.IsDead)
                        hp.TakeDamage(blinkStrikeDamage);
                }
            }

            // Dash Strike upgrade: AoE damage at dash endpoint
            if (RunUpgradeSystem.HasDashStrike)
            {
                Vector3 dashEnd = dashFrom + dashDir * dashDistance;
                var dashHits = Physics.OverlapSphere(dashEnd, 1.5f);
                foreach (var dh in dashHits)
                {
                    if (dh.GetComponent<PlayerController>() != null) continue;
                    var hp = dh.GetComponent<Health>();
                    if (hp != null && !hp.IsDead)
                    {
                        // Use last spell's base damage * 0.5
                        var casterComp = GetComponent<SpellCaster>();
                        float dmg = 5f; // fallback
                        if (casterComp != null && casterComp.CurrentComboDef != null)
                            dmg = casterComp.CurrentComboDef.baseDamage * 0.5f;
                        hp.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(dmg)));
                    }
                }
                GameFeel.SpawnHitParticles(dashFrom + dashDir * dashDistance, new Color(0.2f, 0.85f, 0.9f));
                if (TopDownCamera.Instance != null)
                    TopDownCamera.Instance.AddTrauma(0.08f);
            }

            // Dash fire relic
            var relicMgr = GetComponent<RelicManager>();
            if (relicMgr != null)
                relicMgr.OnDash(dashFrom, dashFrom + dashDir * dashDistance);
        }
    }

    // Spell Rush speed buff
    float spellRushTimer;
    float spellRushMultiplier;

    /// <summary>Apply temporary speed buff from Spell Rush upgrade.</summary>
    public void ApplySpeedBuff(float mult, float duration)
    {
        spellRushMultiplier = mult;
        spellRushTimer = duration;
    }

    // Potions
    int potionsRemaining;
    int potionHealAmount = 3;

    public int PotionsRemaining => potionsRemaining;

    public void SetPotions(int count) => potionsRemaining = count;

    public void RefillPotions(int perFloor) => potionsRemaining = Mathf.Min(potionsRemaining + perFloor, 9);

    public void AddPotion(int count) => potionsRemaining = Mathf.Min(potionsRemaining + count, 9);

    bool dodgedThroughAttack;

    void EndIFrames()
    {
        isInvulnerable = false;

        // Check if enemies were close during dash (successful dodge feedback)
        Collider[] nearby = Physics.OverlapSphere(transform.position, 2.5f);
        foreach (var col in nearby)
        {
            if (col.gameObject == gameObject) continue;
            var hp = col.GetComponent<Health>();
            if (hp != null && !hp.IsDead && col.GetComponent<PlayerController>() == null)
            {
                // Successful dodge! Show feedback
                SFXSystem.Play(SFXSystem.SFXType.DualCast, transform.position, 0.3f);
                GameFeel.SpawnDodgeVFX(transform.position);
                break;
            }
        }
    }
}
