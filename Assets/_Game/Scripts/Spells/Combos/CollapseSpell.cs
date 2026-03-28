using UnityEngine;

/// <summary>
/// Void + Void = Collapse
/// Delayed implosion at cursor position. Shows a telegraph, pulls enemies inward, then bursts.
/// </summary>
public static class CollapseSpell
{
    public static void Cast(Vector3 center, float damage, float radius, bool charged)
    {
        float finalRadius = charged ? radius * 1.3f : radius;
        float finalDamage = charged ? damage * 1.2f : damage;
        float pullRadius = charged ? finalRadius * 1.5f : finalRadius * 1.5f;

        center.y = 0.05f;

        var go = new GameObject("CollapseEffect");
        go.transform.position = center;
        go.AddComponent<CollapseEffect>().Init(center, finalDamage, finalRadius, pullRadius, charged);

        SFXSystem.Play(SFXSystem.SFXType.DualCast, center);
    }
}

/// <summary>Manages the delayed collapse implosion sequence.</summary>
public class CollapseEffect : MonoBehaviour
{
    Vector3 _center;
    float _damage;
    float _radius;
    float _pullRadius;
    bool _charged;
    float _timer;
    float _delay = 0.5f;
    bool _imploded;

    // VFX references
    GameObject _telegraphDisc;
    GameObject _telegraphSphere;
    ParticleSystem _telegraphPS;
    float _pullTimer;
    bool _pulling;
    float _pullDuration = 0.3f;
    ElementType _spellElement = ElementType.Fire;

    public void Init(Vector3 center, float damage, float radius, float pullRadius, bool charged)
    {
        _spellElement = ComboSpellFactory.CurrentCastElement ?? ElementType.Fire;
        _center = center;
        _damage = damage;
        _radius = radius;
        _pullRadius = pullRadius;
        _charged = charged;
        _timer = 0f;

        // Telegraph: expanding purple circle on the ground
        _telegraphDisc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(_telegraphDisc.GetComponent<CapsuleCollider>());
        _telegraphDisc.transform.SetParent(transform, false);
        _telegraphDisc.transform.localPosition = Vector3.zero;
        _telegraphDisc.transform.localScale = new Vector3(0.1f, 0.02f, 0.1f);
        _telegraphDisc.GetComponent<Renderer>().material = ShaderCache.NewEmissive(
            new Color(0.4f, 0f, 0.6f), 3f);

        // Dark purple sphere that will implode
        _telegraphSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(_telegraphSphere.GetComponent<SphereCollider>());
        _telegraphSphere.transform.SetParent(transform, false);
        _telegraphSphere.transform.localPosition = Vector3.up * 0.5f;
        _telegraphSphere.transform.localScale = Vector3.one * _radius * 2f;
        _telegraphSphere.GetComponent<Renderer>().material = ShaderCache.NewMagic(
            new Color(0.3f, 0f, 0.5f), 2f);

        // Swirling inward particles during telegraph
        var telegraphParticleGO = new GameObject("CollapseTelegraphParticles");
        telegraphParticleGO.transform.SetParent(transform, false);
        telegraphParticleGO.transform.localPosition = Vector3.up * 0.5f;
        _telegraphPS = telegraphParticleGO.AddComponent<ParticleSystem>();
        _telegraphPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var tm = _telegraphPS.main;
        tm.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
        tm.startSpeed = new ParticleSystem.MinMaxCurve(-3f, -6f); // Inward
        tm.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
        tm.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.5f, 0f, 0.8f, 0.8f),
            new Color(0.3f, 0f, 0.6f, 0.6f));
        tm.simulationSpace = ParticleSystemSimulationSpace.World;
        tm.gravityModifier = 0f;
        tm.loop = true;
        tm.maxParticles = 50;

        var te = _telegraphPS.emission;
        te.rateOverTime = 40;

        var ts = _telegraphPS.shape;
        ts.shapeType = ParticleSystemShapeType.Sphere;
        ts.radius = _radius * 1.5f;

        var tSizeOL = _telegraphPS.sizeOverLifetime;
        tSizeOL.enabled = true;
        tSizeOL.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var tColorOL = _telegraphPS.colorOverLifetime;
        tColorOL.enabled = true;
        var tGrad = new Gradient();
        tGrad.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.5f, 0f, 0.8f), 0f),
                new GradientColorKey(new Color(0.8f, 0.2f, 1f), 1f)
            },
            new[] {
                new GradientAlphaKey(0.8f, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        tColorOL.color = tGrad;

        var tMat = new Material(ShaderCache.ParticleShader);
        tMat.SetColor("_Color", new Color(0.5f, 0f, 0.8f));
        telegraphParticleGO.GetComponent<ParticleSystemRenderer>().material = tMat;
        _telegraphPS.Play();
    }

    void Update()
    {
        if (_imploded)
        {
            // Pull phase
            if (_pulling)
            {
                _pullTimer += Time.deltaTime;
                if (_pullTimer >= _pullDuration)
                {
                    _pulling = false;
                    DoBurst();
                    return;
                }

                // Pull enemies toward center
                Collider[] pullHits = Physics.OverlapSphere(_center, _pullRadius);
                foreach (var h in pullHits)
                {
                    if (h.GetComponent<PlayerController>() != null) continue;
                    var hp = h.GetComponent<Health>();
                    if (hp == null || hp.IsDead) continue;

                    Vector3 toCenter = (_center - h.transform.position);
                    toCenter.y = 0;
                    if (toCenter.sqrMagnitude > 0.1f)
                    {
                        h.transform.position += toCenter.normalized * 10f * Time.deltaTime;
                    }
                }
            }
            return;
        }

        _timer += Time.deltaTime;

        // Animate telegraph: disc expands then contracts
        float t = _timer / _delay;
        if (t < 0.6f)
        {
            // Expand
            float expandT = t / 0.6f;
            float discScale = Mathf.Lerp(0.1f, _pullRadius * 2f, expandT);
            _telegraphDisc.transform.localScale = new Vector3(discScale, 0.02f, discScale);
        }
        else
        {
            // Contract
            float contractT = (t - 0.6f) / 0.4f;
            float discScale = Mathf.Lerp(_pullRadius * 2f, _radius * 0.5f, contractT);
            _telegraphDisc.transform.localScale = new Vector3(discScale, 0.02f, discScale);
        }

        // Sphere shrinks over time (imploding)
        float sphereScale = Mathf.Lerp(_radius * 2f, 0.1f, t);
        _telegraphSphere.transform.localScale = Vector3.one * sphereScale;

        if (_timer >= _delay)
        {
            _imploded = true;
            _pulling = true;
            _pullTimer = 0f;

            // Destroy telegraph visuals
            if (_telegraphPS != null) _telegraphPS.Stop();
            Object.Destroy(_telegraphDisc, 0.1f);
            Object.Destroy(_telegraphSphere, 0.1f);

            SFXSystem.Play(SFXSystem.SFXType.Cast, _center);
        }
    }

    void DoBurst()
    {
        // Burst damage to all in radius
        Collider[] hits = Physics.OverlapSphere(_center, _radius);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
            {
                hp.TakeDamage(_damage, _spellElement);
                GameFeel.ApplyKnockback(h.transform, _center, _damage * 1.0f);
            }
        }

        // VFX: bright purple flash expanding
        var flashGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(flashGO.GetComponent<SphereCollider>());
        flashGO.transform.position = _center + Vector3.up * 0.5f;
        flashGO.transform.localScale = Vector3.one * 0.3f;
        flashGO.GetComponent<Renderer>().material = ShaderCache.NewEmissive(
            new Color(0.7f, 0.1f, 1f), 10f);
        flashGO.AddComponent<FlashShrink>().Init(0.3f);

        // Burst particles expanding outward
        var burstGO = new GameObject("CollapseBurst");
        burstGO.transform.position = _center + Vector3.up * 0.5f;
        var burstPS = burstGO.AddComponent<ParticleSystem>();
        burstPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var bm = burstPS.main;
        bm.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
        bm.startSpeed = new ParticleSystem.MinMaxCurve(5f, 10f);
        bm.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.25f);
        bm.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.6f, 0f, 1f),
            new Color(0.9f, 0.3f, 1f));
        bm.simulationSpace = ParticleSystemSimulationSpace.World;
        bm.gravityModifier = 0.1f;
        bm.duration = 0.15f;
        bm.loop = false;

        var be = burstPS.emission;
        be.rateOverTime = 0;
        be.SetBursts(new[] { new ParticleSystem.Burst(0f, _charged ? 45 : 30) });

        var bs = burstPS.shape;
        bs.shapeType = ParticleSystemShapeType.Sphere;
        bs.radius = 0.2f;

        var bSizeOL = burstPS.sizeOverLifetime;
        bSizeOL.enabled = true;
        bSizeOL.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var bColorOL = burstPS.colorOverLifetime;
        bColorOL.enabled = true;
        var bGrad = new Gradient();
        bGrad.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.9f, 0.3f, 1f), 0f),
                new GradientColorKey(new Color(0.4f, 0f, 0.6f), 1f)
            },
            new[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        bColorOL.color = bGrad;

        var bMat = new Material(ShaderCache.ParticleShader);
        bMat.SetColor("_Color", new Color(0.6f, 0f, 1f));
        burstGO.GetComponent<ParticleSystemRenderer>().material = bMat;
        burstPS.Play();
        Object.Destroy(burstGO, 0.8f);

        // Shockwave disc on ground
        var shockDisc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(shockDisc.GetComponent<CapsuleCollider>());
        shockDisc.transform.position = _center;
        shockDisc.transform.localScale = new Vector3(0.5f, 0.02f, 0.5f);
        shockDisc.GetComponent<Renderer>().material = ShaderCache.NewEmissive(
            new Color(0.6f, 0f, 1f), 6f);
        shockDisc.AddComponent<ExpandAndFade>().Init(_radius * 3f, 0.3f);

        // Screen shake and hitstop
        if (TopDownCamera.Instance != null)
            TopDownCamera.Instance.AddTrauma(0.5f);
        if (GameFeel.Instance != null)
            GameFeel.Instance.Hitstop(0.06f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, _center);

        Object.Destroy(gameObject, 0.1f);
    }
}

/// <summary>Expands a disc and fades it out, then destroys it.</summary>
public class ExpandAndFade : MonoBehaviour
{
    float _targetRadius;
    float _duration;
    float _timer;
    Renderer _renderer;
    Vector3 _startScale;

    public void Init(float targetRadius, float duration)
    {
        _targetRadius = targetRadius;
        _duration = duration;
        _renderer = GetComponent<Renderer>();
        _startScale = transform.localScale;
    }

    void Update()
    {
        _timer += Time.deltaTime;
        float t = _timer / _duration;

        float scale = Mathf.Lerp(_startScale.x, _targetRadius * 2f, t);
        transform.localScale = new Vector3(scale, _startScale.y, scale);

        if (_renderer != null)
        {
            Color c = _renderer.material.GetColor("_EmissionColor");
            c.a = Mathf.Lerp(1f, 0f, t);
            _renderer.material.SetColor("_EmissionColor", c * Mathf.Lerp(6f, 0f, t));
        }

        if (t >= 1f) Object.Destroy(gameObject);
    }
}
