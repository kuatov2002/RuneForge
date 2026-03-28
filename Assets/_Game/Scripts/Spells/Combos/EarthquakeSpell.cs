using UnityEngine;

/// <summary>
/// Lightning + Earth = Earthquake
/// Ground AoE with expanding shockwave ring. Enemies hit by the ring edge
/// take damage + stun. Charged version spawns a second delayed ring.
/// </summary>
public static class EarthquakeSpell
{
    public static void Cast(Vector3 center, float damage, float radius, float duration, bool charged)
    {
        if (charged) { damage *= 1.3f; radius *= 1.2f; }

        center.y = 0;

        // First shockwave ring
        SpawnShockwaveRing(center, damage, radius, 0.8f);

        // Ground crack VFX: scattered small brown cubes
        int crackCount = charged ? 14 : 8;
        for (int i = 0; i < crackCount; i++)
        {
            Vector2 rnd = Random.insideUnitCircle * radius * 0.8f;
            Vector3 crackPos = center + new Vector3(rnd.x, 0.02f, rnd.y);

            var crack = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(crack.GetComponent<BoxCollider>());
            crack.name = "EarthquakeCrack";
            crack.transform.position = crackPos;
            float sx = Random.Range(0.15f, 0.5f);
            float sy = Random.Range(0.01f, 0.03f);
            float sz = Random.Range(0.05f, 0.15f);
            crack.transform.localScale = new Vector3(sx, sy, sz);
            crack.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);

            Color crackCol = new Color(
                Random.Range(0.35f, 0.55f),
                Random.Range(0.25f, 0.4f),
                Random.Range(0.1f, 0.2f));
            crack.GetComponent<Renderer>().material = ShaderCache.NewStone(crackCol);
            Object.Destroy(crack, 1.5f);
        }

        // Dust cloud burst at center
        var dustGO = new GameObject("EarthquakeDust");
        dustGO.transform.position = center + Vector3.up * 0.2f;
        var dustPS = dustGO.AddComponent<ParticleSystem>();
        dustPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var dustMain = dustPS.main;
        dustMain.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        dustMain.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
        dustMain.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.35f);
        dustMain.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.55f, 0.45f, 0.25f, 0.6f),
            new Color(0.45f, 0.35f, 0.2f, 0.4f));
        dustMain.simulationSpace = ParticleSystemSimulationSpace.World;
        dustMain.gravityModifier = 0.3f;
        dustMain.duration = 0.2f;
        dustMain.loop = false;

        var dustEmission = dustPS.emission;
        dustEmission.rateOverTime = 0;
        dustEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, charged ? 30 : 20) });

        var dustShape = dustPS.shape;
        dustShape.shapeType = ParticleSystemShapeType.Circle;
        dustShape.radius = radius * 0.3f;

        var dustSizeOL = dustPS.sizeOverLifetime;
        dustSizeOL.enabled = true;
        dustSizeOL.size = new ParticleSystem.MinMaxCurve(1f,
            AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.3f));

        var dustColorOL = dustPS.colorOverLifetime;
        dustColorOL.enabled = true;
        var dGrad = new Gradient();
        dGrad.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.55f, 0.45f, 0.25f), 0f),
                new GradientColorKey(new Color(0.45f, 0.38f, 0.2f), 1f)
            },
            new[] {
                new GradientAlphaKey(0.6f, 0f),
                new GradientAlphaKey(0.3f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            });
        dustColorOL.color = dGrad;

        var dustMat = new Material(ShaderCache.ParticleShader);
        dustMat.SetColor("_Color", new Color(0.5f, 0.4f, 0.25f));
        dustGO.GetComponent<ParticleSystemRenderer>().material = dustMat;
        dustPS.Play();
        Object.Destroy(dustGO, 1.2f);

        // Impact flash at center
        var flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(flash.GetComponent<SphereCollider>());
        flash.transform.position = center + Vector3.up * 0.2f;
        flash.transform.localScale = Vector3.one * 0.6f;
        flash.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(0.9f, 0.8f, 0.3f), 6f);
        flash.AddComponent<FlashShrink>().Init(0.25f);

        // Charged: second ring delayed 0.3s
        if (charged)
        {
            var delayGO = new GameObject("EarthquakeDelay");
            delayGO.transform.position = center;
            delayGO.AddComponent<EarthquakeDelayedRing>().Init(center, damage * 0.8f, radius * 1.15f, 0.3f);
            Object.Destroy(delayGO, 1.5f);
        }

        // Game feel
        if (TopDownCamera.Instance != null)
            TopDownCamera.Instance.AddTrauma(0.4f);
        if (GameFeel.Instance != null)
            GameFeel.Instance.Hitstop(0.05f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, center);
    }

    static void SpawnShockwaveRing(Vector3 center, float damage, float maxRadius, float stunDuration)
    {
        var ringGO = new GameObject("EarthquakeShockwave");
        ringGO.transform.position = center;
        ringGO.AddComponent<EarthquakeShockwaveRing>().Init(center, damage, maxRadius, stunDuration);
    }
}

/// <summary>
/// Expanding shockwave ring that damages enemies at its edge as it passes.
/// Uses a cylinder primitive that scales outward over 0.6s.
/// </summary>
public class EarthquakeShockwaveRing : MonoBehaviour
{
    Vector3 _center;
    float _damage;
    float _maxRadius;
    float _stunDuration;
    float _expandDuration = 0.6f;
    float _timer;
    float _currentRadius;
    GameObject _ringVisual;
    System.Collections.Generic.HashSet<int> _hitIds = new();
    ElementType _spellElement = ElementType.Fire;

    public void Init(Vector3 center, float damage, float maxRadius, float stunDuration)
    {
        _spellElement = ComboSpellFactory.CurrentCastElement ?? ElementType.Fire;
        _center = center;
        _damage = damage;
        _maxRadius = maxRadius;
        _stunDuration = stunDuration;

        // Visual: brown/yellow expanding ring cylinder
        _ringVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(_ringVisual.GetComponent<CapsuleCollider>());
        _ringVisual.transform.SetParent(transform, false);
        _ringVisual.transform.localPosition = Vector3.up * 0.05f;
        _ringVisual.transform.localScale = new Vector3(0.1f, 0.04f, 0.1f);

        _ringVisual.GetComponent<Renderer>().material = ShaderCache.NewPlayerZone(new Color(0.7f, 0.55f, 0.2f), 0.5f, 0.2f, 2f, 0.35f);
    }

    void Update()
    {
        _timer += Time.deltaTime;
        float t = _timer / _expandDuration;

        if (t >= 1f)
        {
            Object.Destroy(gameObject);
            return;
        }

        _currentRadius = Mathf.Lerp(0f, _maxRadius, t);

        // Update ring visual scale
        if (_ringVisual != null)
        {
            _ringVisual.transform.localScale = new Vector3(_currentRadius * 2f, 0.04f, _currentRadius * 2f);

            // Fade out
            var r = _ringVisual.GetComponent<Renderer>();
            if (r != null)
            {
                Color c = r.material.GetColor("_EmissionColor");
                float alpha = Mathf.Lerp(1f, 0f, t);
                r.material.SetColor("_EmissionColor", c * alpha);
            }
        }

        // Check for enemies at the ring edge (within a thin band)
        float bandWidth = 1.2f;
        Collider[] hits = Physics.OverlapSphere(_center, _currentRadius + bandWidth * 0.5f);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp == null || hp.IsDead) continue;

            int id = h.transform.GetInstanceID();
            if (_hitIds.Contains(id)) continue;

            // Only hit enemies near the ring edge
            float dist = Vector3.Distance(_center, h.transform.position);
            if (dist >= _currentRadius - bandWidth * 0.5f && dist <= _currentRadius + bandWidth * 0.5f)
            {
                hp.TakeDamage(_damage, _spellElement);
                hp.ApplyStun(_stunDuration);
                GameFeel.ApplyKnockback(h.transform, _center, _damage * 1.0f);
                _hitIds.Add(id);
            }
        }
    }
}

/// <summary>Spawns a second shockwave ring after a delay.</summary>
public class EarthquakeDelayedRing : MonoBehaviour
{
    Vector3 _center;
    float _damage;
    float _radius;
    float _delay;
    float _timer;
    bool _spawned;

    public void Init(Vector3 center, float damage, float radius, float delay)
    {
        _center = center;
        _damage = damage;
        _radius = radius;
        _delay = delay;
    }

    void Update()
    {
        if (_spawned) return;
        _timer += Time.deltaTime;
        if (_timer < _delay) return;
        _spawned = true;

        // Spawn second ring
        var ringGO = new GameObject("EarthquakeShockwave2");
        ringGO.transform.position = _center;
        ringGO.AddComponent<EarthquakeShockwaveRing>().Init(_center, _damage, _radius, 0.8f);

        // Extra dust for second ring
        var dustGO = new GameObject("EarthquakeDust2");
        dustGO.transform.position = _center + Vector3.up * 0.15f;
        var ps = dustGO.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.25f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.5f, 0.4f, 0.2f, 0.5f),
            new Color(0.4f, 0.35f, 0.2f, 0.3f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.2f;
        main.duration = 0.15f;
        main.loop = false;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 15) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.3f;

        var mat = new Material(ShaderCache.ParticleShader);
        mat.SetColor("_Color", new Color(0.5f, 0.4f, 0.25f));
        dustGO.GetComponent<ParticleSystemRenderer>().material = mat;
        ps.Play();
        Object.Destroy(dustGO, 0.8f);

        if (TopDownCamera.Instance != null)
            TopDownCamera.Instance.AddTrauma(0.3f);
        SFXSystem.Play(SFXSystem.SFXType.Hit, _center);
    }
}
