using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Screen-space damage feedback for the player:
/// - Red vignette flash when taking damage
/// - Persistent low-HP vignette when below 30% HP
/// - Screen tint on death
/// Attaches to the GameHUD's UIDocument.
/// </summary>
public class DamageVignette : MonoBehaviour
{
    VisualElement _vignetteOverlay;
    VisualElement _flashOverlay;
    Health _playerHealth;
    float _flashTimer;
    float _flashDuration = 0.3f;
    float _lowHPPulse;

    public void Init(UIDocument uiDoc, Health playerHealth)
    {
        _playerHealth = playerHealth;
        _playerHealth.OnDamaged += OnDamaged;
        _playerHealth.OnHPChanged += OnHPChanged;

        var root = uiDoc.rootVisualElement;

        // Persistent vignette overlay (low HP)
        _vignetteOverlay = new VisualElement();
        _vignetteOverlay.pickingMode = PickingMode.Ignore;
        _vignetteOverlay.style.position = Position.Absolute;
        _vignetteOverlay.style.top = 0;
        _vignetteOverlay.style.bottom = 0;
        _vignetteOverlay.style.left = 0;
        _vignetteOverlay.style.right = 0;
        _vignetteOverlay.style.backgroundColor = Color.clear;
        // Border-based vignette: thick dark-red translucent borders
        SetVignetteBorder(_vignetteOverlay, Color.clear, 0);
        root.Add(_vignetteOverlay);

        // Flash overlay (damage hit)
        _flashOverlay = new VisualElement();
        _flashOverlay.pickingMode = PickingMode.Ignore;
        _flashOverlay.style.position = Position.Absolute;
        _flashOverlay.style.top = 0;
        _flashOverlay.style.bottom = 0;
        _flashOverlay.style.left = 0;
        _flashOverlay.style.right = 0;
        _flashOverlay.style.backgroundColor = Color.clear;
        root.Add(_flashOverlay);
    }

    void OnDamaged(int amount, Vector3 pos, bool killed)
    {
        _flashTimer = _flashDuration;
    }

    void OnHPChanged(int current, int max)
    {
        // Update persistent vignette based on HP ratio
        UpdateLowHPVignette();
    }

    void Update()
    {
        // Damage flash
        if (_flashTimer > 0)
        {
            _flashTimer -= Time.unscaledDeltaTime;
            float t = _flashTimer / _flashDuration;
            Color flashColor = new Color(0.8f, 0.05f, 0.05f, t * 0.4f);
            _flashOverlay.style.backgroundColor = flashColor;
        }
        else
        {
            _flashOverlay.style.backgroundColor = Color.clear;
        }

        // Low HP pulse
        if (_playerHealth != null && !_playerHealth.IsDead)
        {
            float hpRatio = (float)_playerHealth.currentHP / _playerHealth.maxHP;
            if (hpRatio <= 0.3f && hpRatio > 0)
            {
                _lowHPPulse += Time.unscaledDeltaTime * 3f;
                float pulse = (Mathf.Sin(_lowHPPulse) + 1f) * 0.5f;
                float intensity = (1f - hpRatio / 0.3f) * 0.25f;
                Color vigColor = new Color(0.6f, 0f, 0f, pulse * intensity);
                SetVignetteBorder(_vignetteOverlay, vigColor, 60 + pulse * 30);
            }
            else
            {
                SetVignetteBorder(_vignetteOverlay, Color.clear, 0);
            }
        }
    }

    void UpdateLowHPVignette()
    {
        if (_playerHealth == null) return;
        float hpRatio = (float)_playerHealth.currentHP / _playerHealth.maxHP;
        if (hpRatio > 0.3f)
            SetVignetteBorder(_vignetteOverlay, Color.clear, 0);
    }

    static void SetVignetteBorder(VisualElement el, Color color, float width)
    {
        el.style.borderTopColor = color;
        el.style.borderBottomColor = color;
        el.style.borderLeftColor = color;
        el.style.borderRightColor = color;
        el.style.borderTopWidth = width;
        el.style.borderBottomWidth = width;
        el.style.borderLeftWidth = width;
        el.style.borderRightWidth = width;
    }

    void OnDestroy()
    {
        if (_playerHealth != null)
        {
            _playerHealth.OnDamaged -= OnDamaged;
            _playerHealth.OnHPChanged -= OnHPChanged;
        }
    }
}
