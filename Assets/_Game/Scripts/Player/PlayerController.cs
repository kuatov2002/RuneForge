using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 6f;
    public float dashDistance = 3f;
    public float dashCooldown = 1f;

    const float DashDuration = 0.15f;
    const float IFrameDuration = 0.2f;

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

        Vector3 vel = new Vector3(input.x, 0, input.y) * moveSpeed;
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

        // Dash
        if (mouse != null && mouse.rightButton.wasPressedThisFrame && currentDashCharges > 0)
        {
            Vector3 dashFrom = transform.position;
            isDashing = true;
            isInvulnerable = true;
            dashTimer = DashDuration;
            currentDashCharges--;
            if (currentDashCharges < maxDashCharges && chargeRechargeTimer <= 0)
                chargeRechargeTimer = dashCooldown;
            rb.linearVelocity = transform.forward * (dashDistance / DashDuration);
            Invoke(nameof(EndIFrames), IFrameDuration);
            SFXSystem.Play(SFXSystem.SFXType.Dash, transform.position);

            // Reactive dual-cast on dash
            var dualCast = GetComponent<DualCast>();
            if (dualCast != null) dualCast.OnDash(transform.forward);

            // Dash fire relic
            var relicMgr = GetComponent<RelicManager>();
            if (relicMgr != null)
                relicMgr.OnDash(dashFrom, dashFrom + transform.forward * dashDistance);
        }
    }

    // Potions
    int potionsRemaining;
    int potionHealAmount = 3;

    public int PotionsRemaining => potionsRemaining;

    public void SetPotions(int count) => potionsRemaining = count;

    public void RefillPotions(int perFloor) => potionsRemaining = Mathf.Min(potionsRemaining + perFloor, 9);

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
