using UnityEngine;

/// <summary>
/// Water + Water = Ice Spike
/// Launches an ice spike projectile that pierces through enemies, freezing them.
/// </summary>
public static class DeepFreezeSpell
{
    public static void Cast(Vector3 center, float damage, float radius, float duration, bool charged)
    {
        if (charged) duration *= 1.5f;

        var player = Object.FindAnyObjectByType<PlayerController>();
        if (player == null) return;

        Vector3 origin = player.transform.position + Vector3.up * 0.5f;
        Vector3 dir = (center - player.transform.position);
        dir.y = 0;
        if (dir.sqrMagnitude < 0.01f) dir = player.transform.forward;
        dir.Normalize();

        float speed = charged ? 14f : 18f;
        float spikeLength = charged ? 0.6f : 0.4f;

        // Create ice spike projectile (elongated cube)
        var go = new GameObject("IceSpike");
        go.transform.position = origin + dir * 0.6f;
        go.transform.rotation = Quaternion.LookRotation(dir);

        // Spike visual: elongated diamond shape
        var spike = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.Destroy(spike.GetComponent<BoxCollider>());
        spike.transform.SetParent(go.transform, false);
        spike.transform.localScale = new Vector3(0.12f, 0.12f, spikeLength);
        spike.transform.localRotation = Quaternion.Euler(0, 0, 45);
        spike.GetComponent<Renderer>().material = ShaderCache.NewIce(new Color(0.4f, 0.75f, 1f), 0.85f);

        // Second rotated spike for cross shape
        var spike2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.Destroy(spike2.GetComponent<BoxCollider>());
        spike2.transform.SetParent(go.transform, false);
        spike2.transform.localScale = new Vector3(0.12f, 0.12f, spikeLength);
        spike2.GetComponent<Renderer>().material = ShaderCache.NewIce(new Color(0.5f, 0.85f, 1f), 0.8f);

        // Trigger collider
        var col = go.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.4f;

        var rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;

        // Frost particle trail
        var trailGO = new GameObject("IceSpikeTrail");
        trailGO.transform.SetParent(go.transform, false);
        var trailPS = trailGO.AddComponent<ParticleSystem>();
        trailPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var tm = trailPS.main;
        tm.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
        tm.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
        tm.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
        tm.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.6f, 0.85f, 1f, 0.8f), new Color(0.8f, 0.95f, 1f, 0.6f));
        tm.simulationSpace = ParticleSystemSimulationSpace.World;
        tm.gravityModifier = -0.05f;
        tm.loop = true;

        var te = trailPS.emission;
        te.rateOverTime = 20;

        var ts = trailPS.shape;
        ts.shapeType = ParticleSystemShapeType.Sphere;
        ts.radius = 0.08f;

        var tMat = new Material(Shader.Find("Particles/Standard Unlit"));
        tMat.SetColor("_Color", new Color(0.6f, 0.9f, 1f, 0.7f));
        trailGO.GetComponent<ParticleSystemRenderer>().material = tMat;
        trailPS.Play();

        // Trail renderer
        var trail = go.AddComponent<TrailRenderer>();
        trail.startWidth = 0.15f;
        trail.endWidth = 0f;
        trail.time = 0.3f;
        trail.material = ShaderCache.NewEmissive(new Color(0.4f, 0.7f, 1f), 2f);
        trail.startColor = new Color(0.5f, 0.8f, 1f);
        trail.endColor = new Color(0.3f, 0.6f, 1f, 0f);

        // Add ice spike behavior
        var iceSpike = go.AddComponent<IceSpikeProjectile>();
        iceSpike.Init(dir, speed, damage, duration, charged ? 5 : 3);

        SFXSystem.Play(SFXSystem.SFXType.Cast, origin);
    }
}

/// <summary>Ice spike that pierces enemies and freezes them.</summary>
public class IceSpikeProjectile : MonoBehaviour
{
    Vector3 _dir;
    float _speed;
    float _damage;
    float _freezeDuration;
    int _piercesLeft;
    float _maxDist = 16f;
    Vector3 _startPos;
    System.Collections.Generic.HashSet<Collider> _hit = new();

    public void Init(Vector3 dir, float speed, float damage, float freezeDuration, int pierces)
    {
        _dir = dir;
        _speed = speed;
        _damage = damage;
        _freezeDuration = freezeDuration;
        _piercesLeft = pierces;
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
        if (other.GetComponent<IceSpikeProjectile>() != null) return;
        if (other.GetComponent<SpellProjectile>() != null) return;
        if (_hit.Contains(other)) return;

        var hp = other.GetComponent<Health>();
        if (hp != null && !hp.IsDead)
        {
            hp.TakeDamage(_damage);
            hp.ApplyFreeze(_freezeDuration);
            _hit.Add(other);

            // Ice crystal on frozen enemy
            SpawnFreezeEffect(other.transform);

            _piercesLeft--;
            if (_piercesLeft <= 0)
            {
                Shatter();
                return;
            }

            SFXSystem.Play(SFXSystem.SFXType.Hit, other.transform.position);
        }
        else if (hp == null)
        {
            // Hit wall
            Shatter();
        }
    }

    void SpawnFreezeEffect(Transform enemy)
    {
        var ice = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.Destroy(ice.GetComponent<BoxCollider>());
        ice.transform.SetParent(enemy, false);
        ice.transform.localPosition = Vector3.up * 0.5f;
        ice.transform.localScale = new Vector3(0.7f, 1.1f, 0.7f);
        ice.GetComponent<Renderer>().material = ShaderCache.NewIce(new Color(0.5f, 0.8f, 1f), 0.5f);
        Object.Destroy(ice, _freezeDuration);
    }

    void Shatter()
    {
        Vector3 pos = transform.position;

        // Ice shard fragments
        for (int i = 0; i < 6; i++)
        {
            var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(shard.GetComponent<BoxCollider>());
            shard.transform.position = pos + Random.insideUnitSphere * 0.3f;
            float s = Random.Range(0.03f, 0.08f);
            shard.transform.localScale = new Vector3(s, s, s);
            shard.transform.rotation = Random.rotation;
            shard.GetComponent<Renderer>().material = ShaderCache.NewIce(new Color(0.5f, 0.85f, 1f), 0.8f);
            var srb = shard.AddComponent<Rigidbody>();
            srb.mass = 0.02f;
            srb.AddForce(Random.insideUnitSphere * 3f + Vector3.up * 2f, ForceMode.Impulse);
            Object.Destroy(shard, 0.6f);
        }

        SFXSystem.Play(SFXSystem.SFXType.Hit, pos);
        Destroy(gameObject);
    }
}
