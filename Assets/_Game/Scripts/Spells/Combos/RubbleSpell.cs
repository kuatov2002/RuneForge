using UnityEngine;

/// <summary>
/// Earth + Air = Rubble
/// Rain of rocks dealing multiple waves of damage. Enemies get briefly stunned.
/// Balance: radius 2.5 (was 4), damage per wave 5 (was 8)
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
        _timer = 0;
    }

    void Update()
    {
        _timer -= Time.deltaTime;
        if (_timer > 0 || _wavesRemaining <= 0) return;

        _timer = _interval;
        _wavesRemaining--;

        // Spawn falling rock cubes with physics
        int rockCount = Random.Range(3, 6);
        for (int i = 0; i < rockCount; i++)
        {
            Vector3 targetPos = _center + new Vector3(
                Random.Range(-_radius, _radius), 0,
                Random.Range(-_radius, _radius));

            var rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            // Keep BoxCollider for physics collision with ground
            var rockCol = rock.GetComponent<BoxCollider>();
            rockCol.isTrigger = false;

            rock.transform.position = targetPos + Vector3.up * 8f;
            float s = Random.Range(0.2f, 0.5f);
            rock.transform.localScale = new Vector3(s, s * 0.7f, s);
            rock.transform.rotation = Random.rotation;
            Color rockColColor = new Color(
                Random.Range(0.3f, 0.5f),
                Random.Range(0.25f, 0.4f),
                Random.Range(0.15f, 0.3f));
            rock.GetComponent<Renderer>().material = ShaderCache.NewStone(rockColColor);

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

        // Dust cloud particle burst at ground level on each wave
        var dustGO = new GameObject("RubbleDust");
        dustGO.transform.position = _center + Vector3.up * 0.2f;
        var dustPS = dustGO.AddComponent<ParticleSystem>();
        dustPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var dustMain = dustPS.main;
        dustMain.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        dustMain.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
        dustMain.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
        dustMain.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.55f, 0.45f, 0.3f, 0.6f),
            new Color(0.4f, 0.35f, 0.25f, 0.4f));
        dustMain.simulationSpace = ParticleSystemSimulationSpace.World;
        dustMain.gravityModifier = 0.2f;
        dustMain.duration = 0.2f;
        dustMain.loop = false;

        var dustEmission = dustPS.emission;
        dustEmission.rateOverTime = 0;
        dustEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });

        var dustShape = dustPS.shape;
        dustShape.shapeType = ParticleSystemShapeType.Circle;
        dustShape.radius = _radius * 0.6f;

        var dustSizeOL = dustPS.sizeOverLifetime;
        dustSizeOL.enabled = true;
        dustSizeOL.size = new ParticleSystem.MinMaxCurve(1f,
            AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.3f));

        var dustColorOL = dustPS.colorOverLifetime;
        dustColorOL.enabled = true;
        var dGrad = new Gradient();
        dGrad.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.55f, 0.45f, 0.3f), 0f),
                new GradientColorKey(new Color(0.45f, 0.38f, 0.28f), 1f)
            },
            new[] {
                new GradientAlphaKey(0.6f, 0f),
                new GradientAlphaKey(0.3f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            });
        dustColorOL.color = dGrad;

        var dustMat = new Material(Shader.Find("Particles/Standard Unlit"));
        dustMat.SetColor("_Color", new Color(0.5f, 0.4f, 0.3f));
        dustGO.GetComponent<ParticleSystemRenderer>().material = dustMat;
        dustPS.Play();
        Object.Destroy(dustGO, 1f);

        // Small debris particles bursting outward
        var debrisGO = new GameObject("RubbleDebris");
        debrisGO.transform.position = _center + Vector3.up * 0.3f;
        var debrisPS = debrisGO.AddComponent<ParticleSystem>();
        debrisPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var debrisMain = debrisPS.main;
        debrisMain.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
        debrisMain.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
        debrisMain.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
        debrisMain.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.45f, 0.35f, 0.2f),
            new Color(0.55f, 0.4f, 0.25f));
        debrisMain.simulationSpace = ParticleSystemSimulationSpace.World;
        debrisMain.gravityModifier = 1.5f;
        debrisMain.duration = 0.1f;
        debrisMain.loop = false;

        var debrisEmission = debrisPS.emission;
        debrisEmission.rateOverTime = 0;
        debrisEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 10) });

        var debrisShape = debrisPS.shape;
        debrisShape.shapeType = ParticleSystemShapeType.Sphere;
        debrisShape.radius = 0.3f;

        debrisGO.GetComponent<ParticleSystemRenderer>().material = dustMat;
        debrisPS.Play();
        Object.Destroy(debrisGO, 0.8f);

        // Screen shake on each wave
        if (TopDownCamera.Instance != null)
            TopDownCamera.Instance.AddTrauma(0.15f);
        SFXSystem.Play(SFXSystem.SFXType.Hit, _center);
    }
}
