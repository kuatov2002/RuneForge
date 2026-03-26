using UnityEngine;

/// <summary>
/// Spawns environmental hazards in combat rooms.
/// Called by Bootstrap after room is built.
/// </summary>
public static class RoomHazards
{
    /// <summary>Populate a room with hazards based on floor and room size.</summary>
    public static void Populate(Transform roomRoot, int width, int height, int floorIndex)
    {
        int hazardBudget = 2 + floorIndex; // More hazards on later floors

        int safety = 50;
        while (hazardBudget > 0 && safety-- > 0)
        {
            float x = Random.Range(2f, width - 2f);
            float z = Random.Range(2f, height - 2f);
            Vector3 pos = new Vector3(x, 0, z);

            // Don't place too close to center (spawn/pickup area)
            float cx = width / 2f, cz = height / 2f;
            if (Vector3.Distance(pos, new Vector3(cx, 0, cz)) < 2.5f) continue;

            float roll = Random.value;
            if (roll < 0.3f)
            {
                CreateSpikeTrap(roomRoot, pos);
                hazardBudget--;
            }
            else if (roll < 0.6f)
            {
                CreateExplosiveBarrel(roomRoot, pos);
                hazardBudget--;
            }
            else if (roll < 0.8f)
            {
                CreateLightningPool(roomRoot, pos, floorIndex);
                hazardBudget--;
            }
            else
            {
                CreateFireVent(roomRoot, pos);
                hazardBudget--;
            }
        }
    }

    // ─── SPIKE TRAP ─────────────────────────────────────────────

    /// <summary>Floor spikes that periodically activate, damaging anything on them.</summary>
    static void CreateSpikeTrap(Transform parent, Vector3 pos)
    {
        var trap = new GameObject("SpikeTrap");
        trap.transform.parent = parent;
        trap.transform.position = pos;

        // Base plate
        var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.Destroy(plate.GetComponent<BoxCollider>());
        plate.name = "SpikePlate";
        plate.transform.parent = trap.transform;
        plate.transform.localPosition = new Vector3(0, -0.2f, 0);
        plate.transform.localScale = new Vector3(1.5f, 0.08f, 1.5f);
        plate.GetComponent<Renderer>().material = ShaderCache.NewLit(new Color(0.3f, 0.15f, 0.1f));

        // Warning lines
        var lines = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.Destroy(lines.GetComponent<BoxCollider>());
        lines.name = "Warning";
        lines.transform.parent = trap.transform;
        lines.transform.localPosition = new Vector3(0, -0.14f, 0);
        lines.transform.localScale = new Vector3(1.3f, 0.02f, 1.3f);
        lines.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(0.8f, 0.2f, 0.1f), 1f);

        // Trigger
        var col = trap.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(1.5f, 1f, 1.5f);
        col.center = Vector3.up * 0.3f;

        trap.AddComponent<SpikeTrapBehavior>();
    }

    // ─── EXPLOSIVE BARREL ───────────────────────────────────────

    /// <summary>Barrel that explodes when hit by a spell, dealing AoE damage.</summary>
    static void CreateExplosiveBarrel(Transform parent, Vector3 pos)
    {
        var barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        barrel.name = "ExplosiveBarrel";
        barrel.transform.parent = parent;
        barrel.transform.position = pos + Vector3.up * 0.4f;
        barrel.transform.localScale = new Vector3(0.5f, 0.4f, 0.5f);

        var col = barrel.GetComponent<CapsuleCollider>();
        col.isTrigger = true;

        barrel.GetComponent<Renderer>().material = ShaderCache.NewLit(new Color(0.5f, 0.25f, 0.1f));

        // Red band
        var band = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(band.GetComponent<CapsuleCollider>());
        band.name = "RedBand";
        band.transform.parent = barrel.transform;
        band.transform.localPosition = Vector3.zero;
        band.transform.localScale = new Vector3(1.1f, 0.3f, 1.1f);
        band.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(0.8f, 0.15f, 0.1f), 1.5f);

        // Health component so spells can hit it
        var hp = barrel.AddComponent<Health>();
        hp.maxHP = 1;
        hp.currentHP = 1;
        hp.OnDeath += () =>
        {
            ExplodeBarrel(barrel.transform.position);
            Object.Destroy(barrel);
        };
    }

    static void ExplodeBarrel(Vector3 pos)
    {
        float radius = 3f;
        int damage = 15;

        Collider[] hits = Physics.OverlapSphere(pos, radius);
        foreach (var h in hits)
        {
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
                hp.TakeDamage(damage);
        }

        // VFX: orange explosion
        GameFeel.SpawnDeathVFX(pos, new Color(1f, 0.5f, 0.1f));

        // Expanding fire ring
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(ring.GetComponent<CapsuleCollider>());
        ring.transform.position = pos + Vector3.up * 0.1f;
        ring.transform.localScale = new Vector3(1f, 0.03f, 1f);
        ring.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(1f, 0.4f, 0.1f), 4f);
        ring.AddComponent<DeathRingEffect>().Init(radius * 2, 0.5f);

        if (TopDownCamera.Instance != null)
            TopDownCamera.Instance.AddTrauma(0.35f);
        if (GameFeel.Instance != null)
            GameFeel.Instance.Hitstop(0.04f);
    }

    // ─── LIGHTNING POOL ─────────────────────────────────────────

    /// <summary>Electrified water pool — conducts Lightning spells, deals periodic damage.</summary>
    static void CreateLightningPool(Transform parent, Vector3 pos, int floor)
    {
        var pool = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(pool.GetComponent<CapsuleCollider>());
        pool.name = "LightningPool";
        pool.transform.parent = parent;
        pool.transform.position = new Vector3(pos.x, 0.02f, pos.z);
        float size = Random.Range(1.5f, 2.5f);
        pool.transform.localScale = new Vector3(size, 0.02f, size);
        pool.GetComponent<Renderer>().material = ShaderCache.NewWater(new Color(0.3f, 0.5f, 0.8f), 0.7f);

        var col = pool.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.5f;

        pool.AddComponent<LightningPoolBehavior>();
    }

    // ─── FIRE VENT ──────────────────────────────────────────────

    /// <summary>Periodic fire geyser that shoots flames upward.</summary>
    static void CreateFireVent(Transform parent, Vector3 pos)
    {
        var vent = new GameObject("FireVent");
        vent.transform.parent = parent;
        vent.transform.position = pos;

        // Grate visual
        var grate = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(grate.GetComponent<CapsuleCollider>());
        grate.name = "Grate";
        grate.transform.parent = vent.transform;
        grate.transform.localPosition = new Vector3(0, -0.18f, 0);
        grate.transform.localScale = new Vector3(0.8f, 0.04f, 0.8f);
        grate.GetComponent<Renderer>().material = ShaderCache.NewLit(new Color(0.3f, 0.2f, 0.15f));

        var col = vent.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(0.8f, 2f, 0.8f);
        col.center = Vector3.up * 0.8f;

        vent.AddComponent<FireVentBehavior>();
    }
}

// ─── HAZARD BEHAVIORS ───────────────────────────────────────────

/// <summary>Periodic spike trap — activates every few seconds.</summary>
public class SpikeTrapBehavior : MonoBehaviour
{
    float _cycleTime = 3f;
    float _activeTime = 1f;
    float _timer;
    bool _active;
    float _tickTimer;
    GameObject[] _spikes;

    void Start()
    {
        _timer = Random.Range(0f, _cycleTime); // Stagger
        // Create spike meshes (hidden initially)
        _spikes = new GameObject[4];
        for (int i = 0; i < 4; i++)
        {
            var spike = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(spike.GetComponent<BoxCollider>());
            spike.name = "Spike";
            spike.transform.parent = transform;
            float ox = (i % 2 == 0 ? -0.3f : 0.3f);
            float oz = (i < 2 ? -0.3f : 0.3f);
            spike.transform.localPosition = new Vector3(ox, -0.3f, oz);
            spike.transform.localScale = new Vector3(0.1f, 0.6f, 0.1f);
            spike.GetComponent<Renderer>().material = ShaderCache.NewMetal(new Color(0.5f, 0.5f, 0.55f));
            spike.SetActive(false);
            _spikes[i] = spike;
        }
    }

    void Update()
    {
        _timer += Time.deltaTime;
        float phase = _timer % (_cycleTime + _activeTime);
        bool shouldBeActive = phase >= _cycleTime;

        if (shouldBeActive != _active)
        {
            _active = shouldBeActive;
            foreach (var s in _spikes)
                if (s != null) s.SetActive(_active);
        }
    }

    void OnTriggerStay(Collider other)
    {
        if (!_active) return;
        _tickTimer -= Time.deltaTime;
        if (_tickTimer > 0) return;
        _tickTimer = 0.5f;

        var hp = other.GetComponent<Health>();
        if (hp != null && !hp.IsDead)
        {
            // Don't check invulnerability for enemies
            var pc = other.GetComponent<PlayerController>();
            if (pc != null && pc.isInvulnerable) return;
            hp.TakeDamage(1);
        }
    }
}

/// <summary>Electrified pool — shocks anything standing in it.</summary>
public class LightningPoolBehavior : MonoBehaviour
{
    float _tickTimer;
    float _sparkTimer;

    void Update()
    {
        // Random spark VFX
        _sparkTimer -= Time.deltaTime;
        if (_sparkTimer <= 0)
        {
            _sparkTimer = Random.Range(0.5f, 1.5f);
            float r = transform.localScale.x * 0.4f;
            Vector3 sparkPos = transform.position + new Vector3(Random.Range(-r, r), 0.1f, Random.Range(-r, r));
            var spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(spark.GetComponent<SphereCollider>());
            spark.transform.position = sparkPos;
            spark.transform.localScale = Vector3.one * 0.08f;
            spark.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(0.5f, 0.8f, 1f), 5f);
            Object.Destroy(spark, 0.15f);
        }
    }

    void OnTriggerStay(Collider other)
    {
        _tickTimer -= Time.deltaTime;
        if (_tickTimer > 0) return;
        _tickTimer = 1f;

        var hp = other.GetComponent<Health>();
        if (hp != null && !hp.IsDead)
        {
            var pc = other.GetComponent<PlayerController>();
            if (pc != null && pc.isInvulnerable) return;
            hp.TakeDamage(1);
            hp.ApplyStun(0.3f);
        }
    }
}

/// <summary>Periodic fire vent geyser.</summary>
public class FireVentBehavior : MonoBehaviour
{
    float _cycleTime = 4f;
    float _activeTime = 1.2f;
    float _timer;
    bool _active;
    float _tickTimer;
    GameObject _flame;

    void Start()
    {
        _timer = Random.Range(0f, _cycleTime);
        // Flame pillar (hidden initially)
        _flame = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(_flame.GetComponent<CapsuleCollider>());
        _flame.name = "Flame";
        _flame.transform.parent = transform;
        _flame.transform.localPosition = Vector3.up * 0.6f;
        _flame.transform.localScale = new Vector3(0.5f, 0.6f, 0.5f);
        _flame.GetComponent<Renderer>().material = ShaderCache.NewFire(new Color(1f, 0.4f, 0.1f));
        _flame.SetActive(false);

        // Point light
        var light = _flame.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.5f, 0.2f);
        light.intensity = 2f;
        light.range = 4f;
    }

    void Update()
    {
        _timer += Time.deltaTime;

        // Warning flash 0.5s before activation
        float phase = _timer % (_cycleTime + _activeTime);
        bool warning = phase >= _cycleTime - 0.5f && phase < _cycleTime;
        bool shouldBeActive = phase >= _cycleTime;

        if (warning && !_active)
        {
            // Flicker the grate
            var grate = transform.Find("Grate");
            if (grate != null)
            {
                float flash = Mathf.PingPong(Time.time * 8f, 1f);
                grate.GetComponent<Renderer>().material.SetColor("_EmissionColor",
                    new Color(1f, 0.3f, 0.1f) * flash * 2f);
            }
        }

        if (shouldBeActive != _active)
        {
            _active = shouldBeActive;
            if (_flame != null) _flame.SetActive(_active);
        }
    }

    void OnTriggerStay(Collider other)
    {
        if (!_active) return;
        _tickTimer -= Time.deltaTime;
        if (_tickTimer > 0) return;
        _tickTimer = 0.4f;

        var hp = other.GetComponent<Health>();
        if (hp != null && !hp.IsDead)
        {
            var pc = other.GetComponent<PlayerController>();
            if (pc != null && pc.isInvulnerable) return;
            hp.TakeDamage(2);
        }
    }
}
