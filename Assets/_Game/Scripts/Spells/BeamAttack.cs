using UnityEngine;

public class BeamAttack : MonoBehaviour
{
    float lifetime = 0.12f;

    public static void Fire(Vector3 origin, Vector3 direction, float range, float width, float damage, ElementSO element,
        float leechPercent = 0f, Health playerHealth = null, bool isVolatile = false, float volatileMiss = 0f)
    {
        origin += Vector3.up * 0.5f;
        direction.y = 0;
        direction.Normalize();

        float hitRange = range;

        if (Physics.Raycast(origin, direction, out RaycastHit wallHit, range))
        {
            if (wallHit.collider.GetComponent<Health>() == null)
                hitRange = wallHit.distance;
        }

        RaycastHit[] hits = Physics.SphereCastAll(origin, width, direction, hitRange);
        foreach (var hit in hits)
        {
            if (hit.collider.GetComponent<PlayerController>() != null) continue;
            var health = hit.collider.GetComponent<Health>();
            if (health == null || health.IsDead) continue;

            if (isVolatile && Random.value < volatileMiss) continue;

            health.TakeDamage(damage);
            health.ApplyStatusEffect(element);

            if (leechPercent > 0 && playerHealth != null)
                playerHealth.Heal(Mathf.Max(1, Mathf.CeilToInt(damage * leechPercent)));

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

        var mat = ShaderCache.NewEmissive(color, 5f);
        beam.GetComponent<Renderer>().material = mat;
        beam.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var glow = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.Destroy(glow.GetComponent<BoxCollider>());
        glow.transform.parent = go.transform;
        glow.transform.position = beam.transform.position;
        glow.transform.rotation = beam.transform.rotation;
        glow.transform.localScale = new Vector3(0.4f, 0.4f, length);
        Color glowCol = color;
        glowCol.a = 0.3f;
        var glowMat = ShaderCache.NewLit(glowCol);
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
