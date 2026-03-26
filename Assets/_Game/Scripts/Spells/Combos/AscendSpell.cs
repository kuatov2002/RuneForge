using UnityEngine;

/// <summary>
/// Air + Air = Wind Slash (was Ascend)
/// Fires a piercing wind blade. Charged = 3 blade fan.
/// Player already has dash on RMB, so Air+Air is now offensive.
/// </summary>
public static class AscendSpell
{
    public static void Cast(Vector3 origin, Vector3 direction, bool charged)
    {
        WindSlashSpell.Cast(origin, direction, charged);
    }
}

/// <summary>
/// AscendEffect kept for backward compatibility — no longer used by Air+Air.
/// </summary>
public class AscendEffect : MonoBehaviour
{
    float _timer;
    float _duration;
    float _ghostTimer;
    Vector3 _direction;
    float _speed;
    bool _active;

    public void StartAscend(Vector3 direction, float distance, float duration)
    {
        _direction = direction.normalized;
        _duration = duration;
        _speed = distance / duration;
        _timer = 0;
        _active = true;
        var pc = GetComponent<PlayerController>();
        if (pc != null) pc.isInvulnerable = true;
    }

    void Update()
    {
        if (!_active) return;
        _timer += Time.deltaTime;
        transform.position += _direction * _speed * Time.deltaTime;
        _ghostTimer -= Time.deltaTime;
        if (_ghostTimer <= 0)
        {
            _ghostTimer = 0.03f;
            var renderers = GetComponentsInChildren<Renderer>();
            GameFeel.SpawnDashGhost(renderers, transform.position, transform.rotation);
        }
        if (_timer >= _duration)
        {
            _active = false;
            var pc = GetComponent<PlayerController>();
            if (pc != null) pc.isInvulnerable = false;
            var rb = GetComponent<Rigidbody>();
            if (rb != null) rb.linearVelocity = Vector3.zero;
        }
    }
}
