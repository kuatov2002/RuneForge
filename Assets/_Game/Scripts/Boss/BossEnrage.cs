using UnityEngine;

/// <summary>
/// Soft enrage timer for all bosses. Added as component to boss GameObject via Bootstrap.
/// After 45s of combat, stacks every 15s — increasing speed, reducing cooldowns, boosting damage.
/// Boss scripts query SpeedMultiplier, CooldownMultiplier, DamageMultiplier each frame.
/// </summary>
public class BossEnrage : MonoBehaviour
{
    const float EnrageStartTime = 45f;
    const float StackInterval = 15f;
    const int MaxStacks = 3;

    float timer;
    int stacks;

    public float SpeedMultiplier { get; private set; } = 1f;
    public float CooldownMultiplier { get; private set; } = 1f;
    public float DamageMultiplier { get; private set; } = 1f;
    public int Stacks => stacks;

    Renderer[] renderers;
    GameObject enrageLabel;
    float pulseTimer;

    void Start()
    {
        renderers = GetComponentsInChildren<Renderer>();
    }

    void Update()
    {
        timer += Time.deltaTime;

        // Check for new stacks
        if (stacks < MaxStacks)
        {
            float nextStackTime = stacks == 0 ? EnrageStartTime : EnrageStartTime + stacks * StackInterval;
            if (timer >= nextStackTime)
                ApplyStack();
        }

        // Red pulsation visual when enraged
        if (stacks > 0)
        {
            pulseTimer += Time.deltaTime;
            float pulse = Mathf.Sin(pulseTimer * (3f + stacks)) * 0.5f + 0.5f;
            float intensity = stacks * 1.5f;
            Color enrageEmission = new Color(1f, 0.1f, 0f) * pulse * intensity;
            foreach (var r in renderers)
                if (r != null && r.material.HasProperty("_EmissionColor"))
                    r.material.SetColor("_EmissionColor",
                        r.material.GetColor("_EmissionColor") + enrageEmission * 0.3f);
        }
    }

    void ApplyStack()
    {
        stacks++;
        switch (stacks)
        {
            case 1:
                SpeedMultiplier = 1.2f;
                CooldownMultiplier = 0.8f;
                DamageMultiplier = 1f;
                ShowEnrageLabel();
                break;
            case 2:
                SpeedMultiplier = 1.4f;
                CooldownMultiplier = 0.6f;
                DamageMultiplier = 1.5f;
                break;
            case 3:
                SpeedMultiplier = 1.6f;
                CooldownMultiplier = 0.4f;
                DamageMultiplier = 2f;
                break;
        }

        if (TopDownCamera.Instance != null) TopDownCamera.Instance.AddTrauma(0.3f);
        SFXSystem.Play(SFXSystem.SFXType.Cast, transform.position);

        // Flash red on stack gain
        GameFeel.ScreenFlash(new Color(1f, 0.1f, 0f, 0.3f), 0.2f);
    }

    void ShowEnrageLabel()
    {
        enrageLabel = new GameObject("EnrageLabel");
        enrageLabel.transform.parent = transform;
        enrageLabel.transform.localPosition = Vector3.up * 3f;
        var tm = enrageLabel.AddComponent<TextMesh>();
        tm.text = "ENRAGED";
        tm.fontSize = 32;
        tm.color = new Color(1f, 0.2f, 0f);
        tm.alignment = TextAlignment.Center;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.characterSize = 0.15f;
        enrageLabel.AddComponent<BillboardText>();
    }

    void OnDestroy()
    {
        if (enrageLabel != null) Destroy(enrageLabel);
    }
}

/// <summary>Keeps a TextMesh facing the camera.</summary>
public class BillboardText : MonoBehaviour
{
    void LateUpdate()
    {
        if (Camera.main != null)
            transform.rotation = Camera.main.transform.rotation;
    }
}
