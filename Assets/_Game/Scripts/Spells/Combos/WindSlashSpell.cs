using UnityEngine;

/// <summary>
/// Air + Air = Wind Slash
/// Fires a piercing wind blade that knocks enemies sideways.
/// Charged: 3 blades in a fan spread.
/// Replaces the old Ascend (duplicate dash).
/// </summary>
public static class WindSlashSpell
{
    public static void Cast(Vector3 origin, Vector3 direction, bool charged)
    {
        var player = Object.FindAnyObjectByType<PlayerController>();
        if (player == null) return;

        origin = player.transform.position + Vector3.up * 0.5f;

        if (charged)
        {
            // 3 blades in fan spread
            for (int i = -1; i <= 1; i++)
            {
                Vector3 dir = Quaternion.Euler(0, i * 20f, 0) * direction;
                SpawnBlade(origin, dir, 9f, 2.5f);
            }
        }
        else
        {
            SpawnBlade(origin, direction, 6f, 2f);
        }

        SFXSystem.Play(SFXSystem.SFXType.Cast, origin);
        if (TopDownCamera.Instance != null)
            TopDownCamera.Instance.AddTrauma(0.15f);
    }

    static void SpawnBlade(Vector3 origin, Vector3 dir, float damage, float radius)
    {
        // Create crescent-shaped blade (stretched flattened cube)
        var blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blade.name = "WindBlade";
        blade.transform.position = origin + dir * 0.8f;
        blade.transform.localScale = new Vector3(radius, 0.08f, 0.3f);
        blade.transform.rotation = Quaternion.LookRotation(dir);

        var col = blade.GetComponent<BoxCollider>();
        col.isTrigger = true;

        var rb = blade.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;

        Color bladeColor = new Color(0.8f, 0.9f, 1f, 0.7f);
        blade.GetComponent<Renderer>().material = ShaderCache.NewEmissive(bladeColor, 4f);

        // Trail
        var trail = blade.AddComponent<TrailRenderer>();
        trail.startWidth = radius * 0.8f;
        trail.endWidth = 0f;
        trail.time = 0.15f;
        trail.material = ShaderCache.NewEmissive(new Color(0.7f, 0.85f, 1f), 2f);
        trail.startColor = new Color(0.8f, 0.9f, 1f, 0.6f);
        trail.endColor = new Color(0.8f, 0.9f, 1f, 0f);

        blade.AddComponent<WindBladeProjectile>().Init(dir, 18f, damage);

        Object.Destroy(blade, 3f);
    }
}

public class WindBladeProjectile : MonoBehaviour
{
    Vector3 _dir;
    float _speed;
    float _damage;
    float _maxDist = 16f;
    Vector3 _startPos;
    System.Collections.Generic.HashSet<int> _hitIds = new();

    public void Init(Vector3 dir, float speed, float damage)
    {
        _dir = dir.normalized;
        _speed = speed;
        _damage = damage;
        _startPos = transform.position;
    }

    void Update()
    {
        transform.position += _dir * _speed * Time.deltaTime;
        if (Vector3.Distance(_startPos, transform.position) > _maxDist)
            Destroy(gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<PlayerController>() != null) return;
        if (other.GetComponent<WindBladeProjectile>() != null) return;

        var hp = other.GetComponent<Health>();
        if (hp == null || hp.IsDead) return;

        int id = other.gameObject.GetInstanceID();
        if (_hitIds.Contains(id)) return;
        _hitIds.Add(id);

        hp.TakeDamage(_damage);

        // Knockback sideways (perpendicular to blade travel)
        Vector3 perp = Vector3.Cross(_dir, Vector3.up).normalized;
        float side = Vector3.Dot(other.transform.position - transform.position, perp) > 0 ? 1f : -1f;
        GameFeel.ApplyKnockback(other.transform, other.transform.position - perp * side, 4f);

        if (GameFeel.Instance != null)
            GameFeel.Instance.Hitstop(0.02f);
        SFXSystem.Play(SFXSystem.SFXType.Hit, other.transform.position, 0.5f);

        // Slash VFX at hit
        var vfx = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.Destroy(vfx.GetComponent<BoxCollider>());
        vfx.transform.position = other.transform.position + Vector3.up * 0.5f;
        vfx.transform.localScale = new Vector3(1.5f, 0.05f, 0.1f);
        vfx.transform.rotation = Quaternion.LookRotation(_dir);
        vfx.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(0.9f, 0.95f, 1f), 5f);
        Object.Destroy(vfx, 0.12f);
    }
}
