using UnityEngine;

public class EnemyHealthBar : MonoBehaviour
{
    Health health;
    Transform cam;
    GameObject barRoot;
    Transform fillBar;
    Material fillMat;
    float barWidth = 0.6f;
    float barHeight = 0.06f;
    float yOffset = 1.5f;
    int lastHP = -1;

    void Start()
    {
        health = GetComponent<Health>();
        cam = Camera.main?.transform;
        if (health == null) return;

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
        bg.GetComponent<Renderer>().material = ShaderCache.NewLit(new Color(0.1f, 0.1f, 0.1f));

        // Fill (red)
        var fill = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fill.name = "HPBarFill";
        Object.Destroy(fill.GetComponent<BoxCollider>());
        fill.transform.parent = barRoot.transform;
        fill.transform.localPosition = new Vector3(0, 0, -0.01f);
        fill.transform.localScale = new Vector3(barWidth, barHeight, 0.02f);
        fillBar = fill.transform;
        fillMat = ShaderCache.NewEmissive(new Color(0.9f, 0.15f, 0.1f), 1.5f);
        fill.GetComponent<Renderer>().material = fillMat;

        lastHP = health.currentHP;
    }

    void Update()
    {
        if (health == null || fillBar == null || fillMat == null) return;
        if (health.currentHP == lastHP) return;
        lastHP = health.currentHP;

        float ratio = health.maxHP > 0 ? (float)health.currentHP / health.maxHP : 0;
        float w = Mathf.Max(barWidth * ratio, 0.001f);
        fillBar.localScale = new Vector3(w, barHeight, 0.02f);
        float offset = (barWidth - w) * 0.5f;
        fillBar.localPosition = new Vector3(-offset, 0, -0.01f);

        Color col;
        if (ratio > 0.5f)
            col = Color.Lerp(new Color(1f, 0.9f, 0.1f), new Color(0.2f, 0.9f, 0.1f), (ratio - 0.5f) * 2f);
        else
            col = Color.Lerp(new Color(0.9f, 0.1f, 0.1f), new Color(1f, 0.9f, 0.1f), ratio * 2f);

        fillMat.color = col;
        fillMat.SetColor("_EmissionColor", col * 1.5f);
    }

    void LateUpdate()
    {
        if (barRoot == null) return;
        if (cam == null) cam = Camera.main?.transform;
        if (cam == null) return;
        barRoot.transform.position = transform.position + Vector3.up * yOffset;
        barRoot.transform.rotation = Quaternion.LookRotation(barRoot.transform.position - cam.position);
    }
}
