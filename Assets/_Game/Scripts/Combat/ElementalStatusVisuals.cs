using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages visual indicators for elemental statuses on an enemy:
/// - Emission tint on the enemy model
/// - Small orbiting icon spheres above head
/// - Status-specific particle systems
/// </summary>
public class ElementalStatusVisuals : MonoBehaviour
{
    ElementalStatus _status;
    Renderer[] _renderers;
    Color[] _originalEmission;
    bool _hasOriginals;

    // Active visual objects per status
    readonly Dictionary<ElementalStatusType, GameObject> _icons = new();
    readonly Dictionary<ElementalStatusType, GameObject> _particles = new();

    void Awake()
    {
        _status = GetComponent<ElementalStatus>();
    }

    void OnEnable()
    {
        if (_status != null)
            _status.OnStatusChanged += HandleStatusChanged;
    }

    void OnDisable()
    {
        if (_status != null)
            _status.OnStatusChanged -= HandleStatusChanged;
    }

    // ── Event handler ───────────────────────────────────────

    void HandleStatusChanged(ElementalStatusType type, bool added)
    {
        if (added)
        {
            CreateIcon(type);
            CreateParticles(type);
        }
        else
        {
            DestroyIcon(type);
            DestroyParticles(type);
        }

        UpdateEmissionTint();
    }

    // ── Emission tint ───────────────────────────────────────

    void CacheOriginals()
    {
        if (_hasOriginals) return;
        _renderers = GetComponentsInChildren<Renderer>();
        _originalEmission = new Color[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null && _renderers[i].material != null
                && _renderers[i].material.HasProperty("_EmissionColor"))
                _originalEmission[i] = _renderers[i].material.GetColor("_EmissionColor");
            else
                _originalEmission[i] = Color.black;
        }
        _hasOriginals = true;
    }

    void UpdateEmissionTint()
    {
        CacheOriginals();

        // Blend all active status colors
        Color blended = Color.black;
        int count = 0;
        foreach (var kvp in _icons)
        {
            if (ElementalStatusDefs.StatusColors.TryGetValue(kvp.Key, out var c))
            {
                blended += c;
                count++;
            }
        }

        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null || _renderers[i].material == null) continue;
            if (!_renderers[i].material.HasProperty("_EmissionColor")) continue;
            Color target = _originalEmission[i];
            if (count > 0)
                target = Color.Lerp(_originalEmission[i], blended / count, 0.5f) * 1.5f;
            _renderers[i].material.SetColor("_EmissionColor", target);
        }
    }

    // ── Status icons (small spheres above head) ─────────────

    void CreateIcon(ElementalStatusType type)
    {
        if (_icons.ContainsKey(type)) return;

        var icon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(icon.GetComponent<SphereCollider>());
        icon.name = $"Status_{type}";
        icon.transform.SetParent(transform, false);
        icon.transform.localScale = Vector3.one * 0.25f;

        // Position: offset based on number of existing icons, raised for visibility
        float xOffset = (_icons.Count - 1) * 0.35f;
        icon.transform.localPosition = new Vector3(xOffset, 2.1f, 0f);

        var color = ElementalStatusDefs.StatusColors[type];
        icon.GetComponent<Renderer>().material = ShaderCache.NewEmissive(color, 5f);

        // Trail for motion visibility
        var trail = icon.AddComponent<TrailRenderer>();
        trail.startWidth = 0.12f;
        trail.endWidth = 0f;
        trail.time = 0.3f;
        trail.material = ShaderCache.NewEmissive(color, 2f);
        trail.startColor = color;
        Color trailEnd = color; trailEnd.a = 0;
        trail.endColor = trailEnd;

        _icons[type] = icon;

        // Re-layout all icons so they're centered
        LayoutIcons();
    }

    void DestroyIcon(ElementalStatusType type)
    {
        if (_icons.TryGetValue(type, out var icon))
        {
            if (icon != null) Destroy(icon);
            _icons.Remove(type);
            LayoutIcons();
        }
    }

    void LayoutIcons()
    {
        int i = 0;
        int total = _icons.Count;
        foreach (var kvp in _icons)
        {
            if (kvp.Value == null) continue;
            float xOffset = (i - (total - 1) * 0.5f) * 0.35f;
            kvp.Value.transform.localPosition = new Vector3(xOffset, 2.1f, 0f);
            i++;
        }
    }

    // ── Status particles ────────────────────────────────────

    void CreateParticles(ElementalStatusType type)
    {
        if (_particles.ContainsKey(type)) return;

        var go = new GameObject($"Particles_{type}");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.up * 0.5f;

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.loop = true;

        var emission = ps.emission;
        var shape = ps.shape;

        var color = ElementalStatusDefs.StatusColors[type];

        switch (type)
        {
            case ElementalStatusType.Burning:
                main.startLifetime = 0.5f;
                main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
                main.startColor = new Color(1f, 0.5f, 0f, 0.7f);
                main.gravityModifier = -0.5f;
                emission.rateOverTime = 12;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.3f;
                break;

            case ElementalStatusType.Wet:
                main.startLifetime = 0.4f;
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
                main.startColor = new Color(0.3f, 0.6f, 1f, 0.6f);
                main.gravityModifier = 1f;
                emission.rateOverTime = 8;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.25f;
                go.transform.localPosition = Vector3.up * 1.2f;
                break;

            case ElementalStatusType.Weighted:
                main.startLifetime = 0.6f;
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
                main.startColor = new Color(0.6f, 0.4f, 0.2f, 0.5f);
                main.gravityModifier = 0.5f;
                emission.rateOverTime = 6;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.4f;
                go.transform.localPosition = Vector3.up * 0.1f;
                break;

            case ElementalStatusType.Windswept:
                main.startLifetime = 0.5f;
                main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
                main.startColor = new Color(0.85f, 0.92f, 1f, 0.4f);
                main.gravityModifier = -0.2f;
                emission.rateOverTime = 10;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 0.5f;
                break;

            case ElementalStatusType.Charged:
                main.startLifetime = 0.15f;
                main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
                main.startColor = new Color(1f, 1f, 0.3f, 0.8f);
                main.gravityModifier = 0f;
                emission.rateOverTime = 0;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 5, 8, 3, 0.3f) });
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.3f;
                break;

            case ElementalStatusType.Poisoned:
                main.startLifetime = 0.7f;
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
                main.startColor = new Color(0.2f, 0.9f, 0.1f, 0.5f);
                main.gravityModifier = -0.3f;
                emission.rateOverTime = 8;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.3f;
                break;

            case ElementalStatusType.Exposed:
                main.startLifetime = 0.4f;
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
                main.startColor = new Color(0.5f, 0.1f, 0.8f, 0.6f);
                main.gravityModifier = 0f;
                emission.rateOverTime = 10;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.35f;
                break;
        }

        // Material
        var mat = new Material(ShaderCache.ParticleShader);
        mat.SetColor("_Color", color);
        go.GetComponent<ParticleSystemRenderer>().material = mat;

        ps.Play();
        _particles[type] = go;
    }

    void DestroyParticles(ElementalStatusType type)
    {
        if (_particles.TryGetValue(type, out var go))
        {
            if (go != null) Destroy(go);
            _particles.Remove(type);
        }
    }

    void OnDestroy()
    {
        // Cleanup all visual objects
        foreach (var kvp in _icons)
            if (kvp.Value != null) Destroy(kvp.Value);
        foreach (var kvp in _particles)
            if (kvp.Value != null) Destroy(kvp.Value);
        _icons.Clear();
        _particles.Clear();
    }
}
