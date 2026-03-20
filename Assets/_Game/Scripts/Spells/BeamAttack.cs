using UnityEngine;

public class BeamAttack : MonoBehaviour
{
    float lifetime = 0.12f;

    public static void Fire(Vector3 origin, Vector3 direction, float range, float width, float damage, ElementSO element)
    {
        origin += Vector3.up * 0.5f;
        direction.y = 0;
        direction.Normalize();

        float hitRange = range;

        // Raycast for walls to limit beam length
        if (Physics.Raycast(origin, direction, out RaycastHit wallHit, range))
        {
            if (wallHit.collider.GetComponent<Health>() == null)
                hitRange = wallHit.distance;
        }

        // SphereCast to hit enemies along the beam path
        RaycastHit[] hits = Physics.SphereCastAll(origin, width, direction, hitRange);
        foreach (var hit in hits)
        {
            if (hit.collider.GetComponent<PlayerController>() != null) continue;
            var health = hit.collider.GetComponent<Health>();
            if (health == null || health.IsDead) continue;

            health.TakeDamage(damage);
            health.ApplyStatusEffect(element);

            // Chain for lightning
            if (element.statusEffect == StatusEffectType.Chain)
            {
                Collider[] nearby = Physics.OverlapSphere(hit.collider.transform.position, element.chainRadius);
                int chained = 0;
                foreach (var col in nearby)
                {
                    if (chained >= element.chainCount) break;
                    if (col == hit.collider) continue;
                    if (col.GetComponent<PlayerController>() != null) continue;
                    var h = col.GetComponent<Health>();
                    if (h == null || h.IsDead) continue;
                    h.TakeDamage(damage * 0.5f);
                    h.ApplyStatusEffect(element);
                    chained++;
                }
            }
        }

        // Visual beam
        CreateBeamVisual(origin, direction, hitRange, element.color);
    }

    static void CreateBeamVisual(Vector3 origin, Vector3 direction, float length, Color color)
    {
        var go = new GameObject("BeamVFX");
        var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.Destroy(beam.GetComponent<BoxCollider>());
        beam.transform.parent = go.transform;
        beam.transform.position = origin + direction * length * 0.5f;
        beam.transform.rotation = Quaternion.LookRotation(direction);
        beam.transform.localScale = new Vector3(0.15f, 0.15f, length);

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", color * 5f);
        beam.GetComponent<Renderer>().material = mat;
        beam.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // Wider glow beam
        var glow = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.Destroy(glow.GetComponent<BoxCollider>());
        glow.transform.parent = go.transform;
        glow.transform.position = beam.transform.position;
        glow.transform.rotation = beam.transform.rotation;
        glow.transform.localScale = new Vector3(0.4f, 0.4f, length);
        var glowMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        Color glowCol = color;
        glowCol.a = 0.3f;
        glowMat.color = glowCol;
        glow.GetComponent<Renderer>().material = glowMat;
        glow.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        go.AddComponent<BeamAttack>();
    }

    void Update()
    {
        lifetime -= Time.deltaTime;
        if (lifetime <= 0) Destroy(gameObject);
    }
}
