using UnityEngine;

public class AuraForm : MonoBehaviour
{
    float radius;
    float damage;
    ElementSO element;
    float pulseInterval;
    float pulseTimer;
    bool active;
    float leechPercent;
    Health playerHealth;
    bool isVolatile;
    float volatileMiss;

    public void Init(float rad, float dmg, ElementSO elem, float interval,
        float leech = 0f, Health playerHp = null, bool volatile_ = false, float missCh = 0f)
    {
        radius = rad;
        damage = dmg;
        element = elem;
        pulseInterval = interval;
        pulseTimer = 0;
        active = true;
        leechPercent = leech;
        playerHealth = playerHp;
        isVolatile = volatile_;
        volatileMiss = missCh;
    }

    public void Deactivate()
    {
        active = false;
        Destroy(this);
    }

    void Update()
    {
        if (!active) return;
        pulseTimer -= Time.deltaTime;
        if (pulseTimer <= 0)
        {
            pulseTimer = pulseInterval;
            Pulse();
        }
    }

    void Pulse()
    {
        Vector3 origin = transform.position;
        Collider[] hits = Physics.OverlapSphere(origin, radius);
        foreach (var hit in hits)
        {
            if (hit.GetComponent<PlayerController>() != null) continue;
            var health = hit.GetComponent<Health>();
            if (health == null || health.IsDead) continue;
            if (isVolatile && Random.value < volatileMiss) continue;
            health.TakeDamage(damage);
            health.ApplyStatusEffect(element);
            if (leechPercent > 0 && playerHealth != null)
                playerHealth.Heal(Mathf.Max(1, Mathf.CeilToInt(damage * leechPercent)));
        }
        CreatePulseVisual(origin, radius, element.color);
    }

    static void CreatePulseVisual(Vector3 origin, float radius, Color color)
    {
        var go = new GameObject("AuraPulse");
        go.transform.position = origin + Vector3.up * 0.1f;
        var mf = go.AddComponent<MeshFilter>();
        mf.mesh = GenerateRingMesh(radius, 0.15f);
        var mr = go.AddComponent<MeshRenderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", color * 4f);
        mr.material = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Destroy(go, 0.3f);
    }

    static Mesh GenerateRingMesh(float radius, float thickness, int segments = 32)
    {
        var mesh = new Mesh();
        int vertCount = (segments + 1) * 2;
        var verts = new Vector3[vertCount];
        var tris = new int[segments * 6];
        float inner = radius - thickness;
        float outer = radius + thickness;
        for (int i = 0; i <= segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            verts[i * 2] = new Vector3(cos * inner, 0, sin * inner);
            verts[i * 2 + 1] = new Vector3(cos * outer, 0, sin * outer);
        }
        for (int i = 0; i < segments; i++)
        {
            int bi = i * 6;
            int vi = i * 2;
            tris[bi] = vi;
            tris[bi + 1] = vi + 2;
            tris[bi + 2] = vi + 1;
            tris[bi + 3] = vi + 1;
            tris[bi + 4] = vi + 2;
            tris[bi + 5] = vi + 3;
        }
        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        return mesh;
    }
}
