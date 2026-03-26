using UnityEngine;

/// <summary>Expand sphere VFX then destroy.</summary>
public class ComboExpandVFX : MonoBehaviour
{
    float _targetScale;
    float _duration;
    float _timer;
    Color _color;
    Renderer _renderer;

    public void Init(float targetScale, float duration, Color color)
    {
        _targetScale = targetScale;
        _duration = duration;
        _color = color;
        _renderer = GetComponent<Renderer>();
    }

    void Update()
    {
        _timer += Time.deltaTime;
        float t = _timer / _duration;
        if (t >= 1f) { Destroy(gameObject); return; }

        float scale = Mathf.Lerp(0.3f, _targetScale, t);
        transform.localScale = Vector3.one * scale;

        if (_renderer != null)
        {
            Color c = _color * (1f - t);
            _renderer.material.SetColor("_EmissionColor", c * 3f);
        }
    }
}

/// <summary>Shrink sphere VFX (implosion) then destroy.</summary>
public class ComboShrinkVFX : MonoBehaviour
{
    float _duration;
    float _timer;
    Vector3 _startScale;

    public void Init(float duration)
    {
        _duration = duration;
        _startScale = transform.localScale;
    }

    void Update()
    {
        _timer += Time.deltaTime;
        float t = _timer / _duration;
        if (t >= 1f) { Destroy(gameObject); return; }

        transform.localScale = _startScale * (1f - t);
    }
}
