using UnityEngine;

/// <summary>
/// Unique mechanics per floor:
/// Floor 1: Standard (spike traps from RoomHazards)
/// Floor 2: Water/Lightning conductivity zones - standing in water amplifies lightning damage
/// Floor 3: Darkness - reduced visibility, spotlight around player
/// Floor 4: Corruption - poison fog that stacks poison on player over time
/// Floor 5: Chaos - random hazard activation, unstable ground
/// Applied after room build via Bootstrap.
/// </summary>
public static class FloorMechanics
{
    public static void Apply(GameObject roomGO, int floorIndex, Transform player)
    {
        switch (floorIndex)
        {
            case 1: ApplyWaterFloor(roomGO); break;
            case 2: ApplyDarknessFloor(player); break;
            case 3: ApplyCorruptionFloor(roomGO); break;
            case 4: ApplyChaosFloor(roomGO); break;
        }
    }

    // ─── FLOOR 2: WATER ZONES ─────────────────────────────────

    static void ApplyWaterFloor(GameObject roomGO)
    {
        // Add 2-3 water puddles that amplify lightning damage
        int count = Random.Range(2, 4);
        for (int i = 0; i < count; i++)
        {
            float x = Random.Range(3f, 9f);
            float z = Random.Range(3f, 9f);

            var pool = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.Destroy(pool.GetComponent<CapsuleCollider>());
            pool.name = "WaterZone";
            pool.transform.parent = roomGO.transform;
            pool.transform.localPosition = new Vector3(x, 0.01f, z);
            float size = Random.Range(2f, 3.5f);
            pool.transform.localScale = new Vector3(size, 0.01f, size);
            pool.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(0.2f, 0.4f, 0.7f), 0.8f);

            var col = pool.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.5f;

            pool.AddComponent<WaterZoneBehavior>();
        }
    }

    // ─── FLOOR 3: DARKNESS ────────────────────────────────────

    static void ApplyDarknessFloor(Transform player)
    {
        // Reduce ambient light, add spotlight on player
        var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (var l in lights)
        {
            if (l.type == LightType.Directional)
                l.intensity *= 0.3f;
        }

        // Spotlight following player
        var spotGO = new GameObject("PlayerSpotlight");
        var spot = spotGO.AddComponent<Light>();
        spot.type = LightType.Spot;
        spot.color = new Color(0.7f, 0.7f, 0.9f);
        spot.intensity = 3f;
        spot.range = 12f;
        spot.spotAngle = 60f;
        spot.shadows = LightShadows.Soft;
        spotGO.AddComponent<FollowTarget>().target = player;
        spotGO.transform.position = player.position + Vector3.up * 8f;
        spotGO.transform.rotation = Quaternion.Euler(90, 0, 0);
    }

    // ─── FLOOR 4: CORRUPTION ──────────────────────────────────

    static void ApplyCorruptionFloor(GameObject roomGO)
    {
        // Green fog planes + periodic poison damage to player
        var fog = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.Destroy(fog.GetComponent<BoxCollider>());
        fog.name = "CorruptionFog";
        fog.transform.parent = roomGO.transform;
        fog.transform.localPosition = new Vector3(6, 0.3f, 6);
        fog.transform.localScale = new Vector3(14, 0.01f, 14);
        fog.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(0.15f, 0.4f, 0.1f), 1f);

        var fogBehavior = fog.AddComponent<CorruptionFogBehavior>();
    }

    // ─── FLOOR 5: CHAOS ──────────────────────────────────────

    static void ApplyChaosFloor(GameObject roomGO)
    {
        // Unstable floor tiles that flip between safe/danger
        int count = Random.Range(3, 6);
        for (int i = 0; i < count; i++)
        {
            float x = Random.Range(2f, 10f);
            float z = Random.Range(2f, 10f);

            var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(tile.GetComponent<BoxCollider>());
            tile.name = "ChaosTile";
            tile.transform.parent = roomGO.transform;
            tile.transform.localPosition = new Vector3(x, 0.02f, z);
            tile.transform.localScale = new Vector3(2f, 0.03f, 2f);
            tile.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(0.5f, 0.1f, 0.5f), 1f);

            var col = tile.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(1, 1, 1);
            col.center = Vector3.up * 0.3f;

            tile.AddComponent<ChaosTileBehavior>();
        }
    }
}

// ─── FLOOR MECHANIC BEHAVIORS ──────────────────────────────

/// <summary>Water zone: enemies take 2x lightning damage while in it.</summary>
public class WaterZoneBehavior : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        var hp = other.GetComponent<Health>();
        if (hp != null) hp.ApplyStun(0.1f); // Brief stagger on entering water
    }
}

/// <summary>Simple follow target for spotlight.</summary>
public class FollowTarget : MonoBehaviour
{
    public Transform target;
    void LateUpdate()
    {
        if (target != null)
            transform.position = target.position + Vector3.up * 8f;
    }
}

/// <summary>Poison fog - applies poison stack to player every 3s.</summary>
public class CorruptionFogBehavior : MonoBehaviour
{
    float _timer;

    void Update()
    {
        _timer += Time.deltaTime;
        if (_timer >= 3f)
        {
            _timer = 0;
            var player = FindAnyObjectByType<PlayerController>();
            if (player == null) return;
            var hp = player.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
                hp.AddPoisonStack();
        }
    }
}

/// <summary>Chaos tile: toggles between safe/danger on a random timer.</summary>
public class ChaosTileBehavior : MonoBehaviour
{
    float _timer;
    float _cycle;
    bool _dangerous;
    float _tickTimer;

    void Start()
    {
        _cycle = Random.Range(2f, 4f);
        _timer = Random.Range(0f, _cycle);
    }

    void Update()
    {
        _timer += Time.deltaTime;
        if (_timer >= _cycle)
        {
            _timer = 0;
            _dangerous = !_dangerous;
            GetComponent<Renderer>().material = _dangerous
                ? ShaderCache.NewEmissive(new Color(1f, 0.15f, 0.1f), 3f)
                : ShaderCache.NewEmissive(new Color(0.5f, 0.1f, 0.5f), 1f);
        }
    }

    void OnTriggerStay(Collider other)
    {
        if (!_dangerous) return;
        _tickTimer -= Time.deltaTime;
        if (_tickTimer > 0) return;
        _tickTimer = 0.5f;

        var hp = other.GetComponent<Health>();
        if (hp != null && !hp.IsDead)
        {
            var pc = other.GetComponent<PlayerController>();
            if (pc != null && pc.isInvulnerable) return;
            hp.TakeDamage(2);
        }
    }
}
