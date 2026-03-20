using UnityEngine;

public class EnemyHealthBar : MonoBehaviour
{
    Health health;
    Transform cam;
    GameObject barRoot;
    Transform fillBar;
    Renderer fillRenderer;
    Renderer bgRenderer;
    float barWidth = 0.6f;
    float barHeight = 0.06f;
    float yOffset = 1.5f;

    void Start()
    {
        health = GetComponent<Health>();
        cam = Camera.main?.transform;
        if (health == null) return;

        var litShader = Shader.Find("Universal Render Pipeline/Lit");

        barRoot = new GameObject("HealthBar");
        barRoot.transform.parent = transform;
        barRoot.transform.localPosition = new Vector3(0, yOffset, 0);

        // Background (dark)
        var bg = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bg.name = "HPBarBg";
        Object.Destroy(bg.GetComponent<BoxCollider>());
        bg.transform.parent = barRoot.transform;
        bg.transform.localPosition = Vector3.zero;
        bg.transform.localScale = new Vector3(barWidth + 0.04f, barHeight + 0.02f, 0.02f);
        bgRenderer = bg.GetComponent<Renderer>();
        bgRenderer.material = new Material(litShader) { color = new Color(0.1f, 0.1f, 0.1f) };

        // Fill (red)
        var fill = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fill.name = "HPBarFill";
        Object.Destroy(fill.GetComponent<BoxCollider>());
        fill.transform.parent = barRoot.transform;
        fill.transform.localPosition = new Vector3(0, 0, -0.01f);
        fill.transform.localScale = new Vector3(barWidth, barHeight, 0.02f);
        fillBar = fill.transform;
        fillRenderer = fill.GetComponent<Renderer>();
        var fillMat = new Material(litShader);
        fillMat.color = new Color(0.9f, 0.15f, 0.1f);
        fillMat.EnableKeyword("_EMISSION");
        fillMat.SetColor("_EmissionColor", new Color(0.9f, 0.15f, 0.1f) * 1.5f);
        fillRenderer.material = fillMat;

        health.OnHPChanged += OnHPChanged;
    }

    void OnHPChanged(int current, int max)
    {
        if (fillBar == null) return;
        float ratio = max > 0 ? (float)current / max : 0;
        float w = barWidth * ratio;
        fillBar.localScale = new Vector3(w, barHeight, 0.02f);
        float offset = (barWidth - w) * 0.5f;
        fillBar.localPosition = new Vector3(-offset, 0, -0.01f);

        // Color: green > yellow > red
        Color col;
        if (ratio > 0.5f)
            col = Color.Lerp(new Color(1f, 0.9f, 0.1f), new Color(0.2f, 0.9f, 0.1f), (ratio - 0.5f) * 2f);
        else
            col = Color.Lerp(new Color(0.9f, 0.1f, 0.1f), new Color(1f, 0.9f, 0.1f), ratio * 2f);

        fillRenderer.material.color = col;
        fillRenderer.material.SetColor("_EmissionColor", col * 1.5f);
    }

    void LateUpdate()
    {
        if (barRoot == null || cam == null) return;
        barRoot.transform.position = transform.position + Vector3.up * yOffset;
        barRoot.transform.rotation = Quaternion.LookRotation(barRoot.transform.position - cam.position);
    }

    void OnDestroy()
    {
        if (health != null) health.OnHPChanged -= OnHPChanged;
    }
}
