using UnityEngine;

/// <summary>
/// Earth + Air = Rubble
/// Rain of rocks dealing multiple waves of damage. Enemies get briefly stunned.
/// </summary>
public static class RubbleSpell
{
    public static void Cast(Vector3 center, float damagePerWave, float radius, float duration, bool charged)
    {
        if (charged) { radius *= 1.3f; damagePerWave *= 1.4f; }

        int waves = charged ? 5 : 3;
        float interval = duration / waves;

        var go = new GameObject("RubbleStorm");
        go.transform.position = center;
        var storm = go.AddComponent<RubbleStorm>();
        storm.Init(center, damagePerWave, radius, waves, interval);
        Object.Destroy(go, duration + 0.5f);

        SFXSystem.Play(SFXSystem.SFXType.Cast, center);
    }
}

public class RubbleStorm : MonoBehaviour
{
    Vector3 _center;
    float _damage;
    float _radius;
    int _wavesRemaining;
    float _interval;
    float _timer;

    public void Init(Vector3 center, float damage, float radius, int waves, float interval)
    {
        _center = center;
        _damage = damage;
        _radius = radius;
        _wavesRemaining = waves;
        _interval = interval;
        _timer = 0; // First wave immediately
    }

    void Update()
    {
        _timer -= Time.deltaTime;
        if (_timer > 0 || _wavesRemaining <= 0) return;

        _timer = _interval;
        _wavesRemaining--;

        // Spawn falling rocks
        int rockCount = Random.Range(3, 6);
        for (int i = 0; i < rockCount; i++)
        {
            Vector3 targetPos = _center + new Vector3(
                Random.Range(-_radius, _radius), 0,
                Random.Range(-_radius, _radius));

            var rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(rock.GetComponent<BoxCollider>());
            rock.transform.position = targetPos + Vector3.up * 8f;
            float s = Random.Range(0.2f, 0.5f);
            rock.transform.localScale = new Vector3(s, s, s);
            rock.transform.rotation = Random.rotation;
            Color rockCol = new Color(
                Random.Range(0.3f, 0.5f),
                Random.Range(0.25f, 0.4f),
                Random.Range(0.15f, 0.3f));
            rock.GetComponent<Renderer>().material = ShaderCache.NewLit(rockCol);

            var rb = rock.AddComponent<Rigidbody>();
            rb.mass = 2f;
            rb.AddForce(Vector3.down * 20f, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * 5f, ForceMode.Impulse);

            Object.Destroy(rock, 1.5f);
        }

        // AoE damage
        Collider[] hits = Physics.OverlapSphere(_center, _radius);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
            {
                hp.TakeDamage(_damage);
                hp.ApplyStun(0.5f);
            }
        }

        // Ground impact VFX
        Color dustCol = new Color(0.5f, 0.4f, 0.3f);
        var impact = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(impact.GetComponent<SphereCollider>());
        impact.transform.position = _center + Vector3.up * 0.2f;
        impact.transform.localScale = Vector3.one * 0.3f;
        impact.GetComponent<Renderer>().material = ShaderCache.NewEmissive(dustCol, 2f);
        impact.AddComponent<ComboExpandVFX>().Init(_radius, 0.3f, dustCol);

        if (TopDownCamera.Instance != null)
            TopDownCamera.Instance.AddTrauma(0.15f);
        SFXSystem.Play(SFXSystem.SFXType.Hit, _center);
    }
}
