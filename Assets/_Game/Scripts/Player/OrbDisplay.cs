using UnityEngine;

/// <summary>
/// Visual display of the two element orbs floating above the player.
/// Left orb and right orb show the current combo.
/// </summary>
public class OrbDisplay : MonoBehaviour
{
    SpellCaster _caster;
    GameObject _leftOrb;
    GameObject _rightOrb;
    Renderer _leftRenderer;
    Renderer _rightRenderer;

    // Animation
    Vector3 _leftTargetPos;
    Vector3 _rightTargetPos;
    float _bobTimer;

    // Transition
    float _transitionTimer;
    const float TransitionDuration = 0.2f;

    const float OrbHeight = 1.8f;
    const float OrbSpacing = 0.4f;
    const float OrbSize = 0.18f;
    const float BobSpeed = 2f;
    const float BobAmount = 0.08f;

    public void Init(SpellCaster caster)
    {
        _caster = caster;
        _caster.OnOrbsChanged += OnOrbsChanged;

        CreateOrbs();
        UpdateOrbColors();
    }

    void CreateOrbs()
    {
        _leftOrb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(_leftOrb.GetComponent<SphereCollider>());
        _leftOrb.name = "LeftOrb";
        _leftOrb.transform.parent = transform;
        _leftOrb.transform.localScale = Vector3.one * OrbSize;
        _leftRenderer = _leftOrb.GetComponent<Renderer>();

        _rightOrb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(_rightOrb.GetComponent<SphereCollider>());
        _rightOrb.name = "RightOrb";
        _rightOrb.transform.parent = transform;
        _rightOrb.transform.localScale = Vector3.one * OrbSize;
        _rightRenderer = _rightOrb.GetComponent<Renderer>();
    }

    void OnOrbsChanged()
    {
        _transitionTimer = TransitionDuration;
        UpdateOrbColors();
    }

    void UpdateOrbColors()
    {
        if (_caster.leftOrb != null)
        {
            _leftRenderer.material = ShaderCache.NewMagic(_caster.leftOrb.color, 4f);
            _leftOrb.SetActive(true);
        }
        else
            _leftOrb.SetActive(false);

        if (_caster.rightOrb != null)
        {
            _rightRenderer.material = ShaderCache.NewMagic(_caster.rightOrb.color, 4f);
            _rightOrb.SetActive(true);
        }
        else
            _rightOrb.SetActive(false);
    }

    void Update()
    {
        if (_caster == null) return;

        _bobTimer += Time.deltaTime * BobSpeed;
        float bob = Mathf.Sin(_bobTimer) * BobAmount;

        // Target positions (local space)
        _leftTargetPos = new Vector3(-OrbSpacing, OrbHeight + bob, 0);
        _rightTargetPos = new Vector3(OrbSpacing, OrbHeight + bob + Mathf.Sin(_bobTimer * 1.3f) * BobAmount * 0.5f, 0);

        // Smooth movement
        float speed = _transitionTimer > 0 ? 20f : 5f;
        _leftOrb.transform.localPosition = Vector3.Lerp(_leftOrb.transform.localPosition, _leftTargetPos, Time.deltaTime * speed);
        _rightOrb.transform.localPosition = Vector3.Lerp(_rightOrb.transform.localPosition, _rightTargetPos, Time.deltaTime * speed);

        if (_transitionTimer > 0) _transitionTimer -= Time.deltaTime;

        // Overheat visual: dim overheated orbs
        UpdateOverheatVisual(_leftOrb, _leftRenderer, _caster.leftOrb);
        UpdateOverheatVisual(_rightOrb, _rightRenderer, _caster.rightOrb);

        // Charge visual: scale pulse when charging
        if (_caster.IsCharging)
        {
            float pulse = 1f + _caster.ChargeProgress * 0.5f;
            _leftOrb.transform.localScale = Vector3.one * OrbSize * pulse;
            _rightOrb.transform.localScale = Vector3.one * OrbSize * pulse;
        }
        else
        {
            _leftOrb.transform.localScale = Vector3.Lerp(_leftOrb.transform.localScale, Vector3.one * OrbSize, Time.deltaTime * 8f);
            _rightOrb.transform.localScale = Vector3.Lerp(_rightOrb.transform.localScale, Vector3.one * OrbSize, Time.deltaTime * 8f);
        }
    }

    void UpdateOverheatVisual(GameObject orb, Renderer rend, ElementSO elem)
    {
        if (elem == null) return;
        int slot = -1;
        for (int i = 0; i < 4; i++)
            if (_caster.equippedElements[i] == elem) { slot = i; break; }
        if (slot < 0) return;

        if (_caster.IsOverheated(slot))
        {
            // Dim and red tint
            float progress = _caster.GetOverheatProgress(slot);
            Color dimCol = Color.Lerp(new Color(0.3f, 0.1f, 0.1f), elem.color, progress);
            rend.material.SetColor("_EmissionColor", dimCol * 2f);
        }
        else
        {
            int ch = _caster.GetCharges(slot);
            int max = elem.maxCharges;
            if (ch <= 1 && max > 1)
            {
                // Low charge warning: flicker
                float flicker = Mathf.PingPong(Time.time * 4f, 1f) > 0.5f ? 3f : 1f;
                rend.material.SetColor("_EmissionColor", elem.color * flicker);
            }
        }
    }

    void OnDestroy()
    {
        if (_leftOrb != null) Destroy(_leftOrb);
        if (_rightOrb != null) Destroy(_rightOrb);
        if (_caster != null) _caster.OnOrbsChanged -= OnOrbsChanged;
    }
}
