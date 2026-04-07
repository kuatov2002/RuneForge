using UnityEngine;

/// <summary>
/// Full-screen fade overlay for scene transitions.
/// Creates an unlit quad parented to the main camera.
/// The ScreenTransition GameObject itself is persistent (DontDestroyOnLoad).
/// Call Init() after each camera recreation to re-attach the overlay quad.
/// </summary>
public class ScreenTransition : MonoBehaviour
{
    public static ScreenTransition Instance { get; private set; }

    GameObject _quad;
    Renderer _renderer;
    Material _material;
    float _timer;
    float _duration;
    float _fromAlpha;
    float _toAlpha;
    float _lastAlpha;
    System.Action _onComplete;
    bool _active;

    void Awake()
    {
        Instance = this;
    }

    /// <summary>Create (or recreate) the overlay quad and parent it to the current main camera.</summary>
    public void Init()
    {
        // Destroy old quad if it still exists
        if (_quad != null)
            Object.Destroy(_quad);

        // Create a full-screen quad
        _quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.Destroy(_quad.GetComponent<MeshCollider>());
        _quad.name = "TransitionOverlay";

        // Parent to camera
        if (Camera.main != null)
        {
            _quad.transform.SetParent(Camera.main.transform, false);
            _quad.transform.localPosition = new Vector3(0, 0, 0.2f);
            _quad.transform.localScale = new Vector3(50f, 50f, 1f);
            _quad.transform.localRotation = Quaternion.identity;
        }

        _renderer = _quad.GetComponent<Renderer>();

        // Unlit transparent black material
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        _material = new Material(shader);
        _material.SetColor("_BaseColor", new Color(0, 0, 0, 0));
        _material.SetFloat("_Surface", 1); // Transparent
        _material.SetFloat("_Blend", 0); // Alpha
        _material.SetFloat("_ZWrite", 0);
        _material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _material.renderQueue = 4000;
        _material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

        _renderer.material = _material;
        _renderer.sortingOrder = 999;

        // Restore last known alpha
        SetAlpha(_lastAlpha);
    }

    void SetAlpha(float a)
    {
        _lastAlpha = a;
        if (_material == null || _renderer == null) return;
        _material.SetColor("_BaseColor", new Color(0, 0, 0, a));
        _material.color = new Color(0, 0, 0, a);
        _renderer.enabled = a > 0.001f;
    }

    void Update()
    {
        if (!_active) return;

        _timer += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(_timer / _duration);
        // Smooth ease in-out
        float smooth = t * t * (3f - 2f * t);
        float alpha = Mathf.Lerp(_fromAlpha, _toAlpha, smooth);
        SetAlpha(alpha);

        if (t >= 1f)
        {
            _active = false;
            SetAlpha(_toAlpha);
            _onComplete?.Invoke();
        }
    }

    /// <summary>Fade to black, invoke callback, then fade back in.</summary>
    public void Transition(float fadeDuration, System.Action onMidpoint)
    {
        FadeTo(1f, fadeDuration, () =>
        {
            onMidpoint?.Invoke();
            FadeTo(0f, fadeDuration, null);
        });
    }

    /// <summary>Fade to target alpha.</summary>
    public void FadeTo(float targetAlpha, float duration, System.Action onComplete)
    {
        _fromAlpha = _lastAlpha;
        _toAlpha = targetAlpha;
        _duration = Mathf.Max(duration, 0.01f);
        _timer = 0f;
        _onComplete = onComplete;
        _active = true;
        if (_renderer != null) _renderer.enabled = true;
    }

    /// <summary>Instantly set to black (for startup).</summary>
    public void SetBlack()
    {
        SetAlpha(1f);
    }
}
