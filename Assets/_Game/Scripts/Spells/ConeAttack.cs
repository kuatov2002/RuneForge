using UnityEngine;

public class ConeAttack : MonoBehaviour
{
    float lifetime = 0.15f;

    public static void Fire(Vector3 origin, Vector3 direction, float angle, float range, float damage, ElementSO element,
        float leechPercent = 0f, Health playerHealth = null, bool isVolatile = false, float volatileMiss = 0f)
    {
        Collider[] hits = Physics.OverlapSphere(origin, range);
        foreach (var hit in hits)
        {
            if (hit.GetComponent<PlayerController>() != null) continue;
            if (hit.GetComponent<SpellProjectile>() != null) continue;

            var health = hit.GetComponent<Health>();
            if (health == null || health.IsDead) continue;

            Vector3 toTarget = hit.transform.position - origin;
            toTarget.y = 0;
            if (toTarget.sqrMagnitude < 0.01f) continue;

            if (Vector3.Angle(direction, toTarget) <= angle * 0.5f)
            {
                if (isVolatile && Random.value < volatileMiss) continue;
                health.TakeDamage(damage);
                health.ApplyStatusEffect(element);
                if (leechPercent > 0 && playerHealth != null)
                    playerHealth.Heal(Mathf.Max(1, Mathf.CeilToInt(damage * leechPercent)));
            }
        }

        CreateConeVisual(origin, direction, angle, range, element.color);
    }

    static void CreateConeVisual(Vector3 origin, Vector3 direction, float angle, float range, Color color)
    {
        var go = new GameObject("ConeVFX");
        go.transform.position = origin + Vector3.up * 0.15f;
        go.transform.rotation = Quaternion.LookRotation(direction);

        var mf = go.AddComponent<MeshFilter>();
        mf.mesh = GenerateConeMesh(angle, range);

        var mr = go.AddComponent<MeshRenderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", color * 3f);
        mr.material = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        go.AddComponent<ConeAttack>();
    }

    static Mesh GenerateConeMesh(float angle, float range, int segments = 20)
    {
        var mesh = new Mesh();
        var verts = new Vector3[segments + 2];
        var tris = new int[segments * 3];

        verts[0] = Vector3.zero;
        float halfAngle = angle * 0.5f * Mathf.Deg2Rad;

        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            float a = -halfAngle + t * 2f * halfAngle;
            verts[i + 1] = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * range;
        }

        for (int i = 0; i < segments; i++)
        {
            tris[i * 3] = 0;
            tris[i * 3 + 1] = i + 1;
            tris[i * 3 + 2] = i + 2;
        }

        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        return mesh;
    }

    void Update()
    {
        lifetime -= Time.deltaTime;
        if (lifetime <= 0) Destroy(gameObject);
    }
}
