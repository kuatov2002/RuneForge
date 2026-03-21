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

    /// <summary>Dash cooldown progress 0 (ready) to 1 (full CD).</summary>
    public float DashCooldownNormalized => dashCooldown > 0 ? Mathf.Clamp01(dashCDTimer / dashCooldown) : 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
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

        // Dash
        dashCDTimer -= Time.deltaTime;
        if (mouse != null && mouse.rightButton.wasPressedThisFrame && dashCDTimer <= 0)
        {
            Vector3 dashFrom = transform.position;
            isDashing = true;
            isInvulnerable = true;
            dashTimer = DashDuration;
            dashCDTimer = dashCooldown;
            rb.linearVelocity = transform.forward * (dashDistance / DashDuration);
            Invoke(nameof(EndIFrames), IFrameDuration);

            // Dash fire relic
            var relicMgr = GetComponent<RelicManager>();
            if (relicMgr != null)
                relicMgr.OnDash(dashFrom, dashFrom + transform.forward * dashDistance);
        }
    }

    void EndIFrames()
    {
        isInvulnerable = false;
    }
}
