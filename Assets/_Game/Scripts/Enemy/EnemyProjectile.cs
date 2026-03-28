using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    Vector3 direction;
    float speed;
    float damage;
    ElementSO element;
    float maxDistance;
    Vector3 startPos;
    bool initialized;

    public void Setup(Vector3 dir, float spd, float dmg, ElementSO elem = null, float range = 15f)
    {
        direction = dir.normalized;
        speed = spd;
        damage = dmg;
        element = elem;
        maxDistance = range;
        startPos = transform.position;
        initialized = true;
    }

    void Update()
    {
        if (!initialized) return;
        transform.position += direction * speed * Time.deltaTime;
        if (Vector3.Distance(startPos, transform.position) > maxDistance)
            Destroy(gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!initialized) return;
        if (other.GetComponent<EnemyProjectile>() != null) return;

        // Only hit player
        var playerCtrl = other.GetComponent<PlayerController>();
        if (playerCtrl == null) return;
        if (playerCtrl.isInvulnerable) { Destroy(gameObject); return; }

        var health = other.GetComponent<Health>();
        if (health != null && !health.IsDead)
        {
            health.TakeDamage(damage);
        }
        Destroy(gameObject);
    }

    public static GameObject Create(Vector3 pos, Vector3 dir, float speed, float damage, Color color, ElementSO elem = null)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "EnemyProjectile";
        go.transform.position = pos + Vector3.up * 0.5f;
        go.transform.localScale = new Vector3(0.1f, 0.1f, 0.3f);
        go.transform.rotation = Quaternion.LookRotation(dir);
        go.layer = 0;

        var boxCol = go.GetComponent<BoxCollider>();
        Object.Destroy(boxCol);
        var sphereCol = go.AddComponent<SphereCollider>();
        sphereCol.isTrigger = true;
        sphereCol.radius = 2f;

        var rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;

        // Enemy projectiles: bright red emissive for danger readability
        Color emissiveColor = Color.Lerp(color, Color.red, 0.5f);
        var mat = ShaderCache.NewEmissive(emissiveColor, 4f);
        go.GetComponent<Renderer>().material = mat;

        var trail = go.AddComponent<TrailRenderer>();
        trail.startWidth = 0.25f;
        trail.endWidth = 0f;
        trail.time = 0.3f;
        var tMat = ShaderCache.NewEmissive(emissiveColor, 2f);
        trail.material = tMat;
        trail.startColor = emissiveColor;
        Color endC = emissiveColor; endC.a = 0;
        trail.endColor = endC;

        SFXSystem.Play(SFXSystem.SFXType.Hit, pos, 0.15f);

        var proj = go.AddComponent<EnemyProjectile>();
        proj.Setup(dir, speed, damage, elem);
        return go;
    }
}
