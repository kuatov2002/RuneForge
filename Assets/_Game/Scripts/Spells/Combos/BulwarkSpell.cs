using UnityEngine;

/// <summary>
/// Earth + Earth = Bulwark
/// Creates a stone wall in the direction of the cursor.
/// Wall physically blocks projectiles and enemies.
/// </summary>
public static class BulwarkSpell
{
    public static void Cast(Vector3 origin, Vector3 direction, float duration, bool charged)
    {
        float wallDist = 3f;
        float wallWidth = charged ? 6f : 4f;
        float wallHeight = charged ? 3f : 2f;
        float wallThickness = 0.6f;
        if (charged) duration *= 1.5f;

        Vector3 wallPos = origin + direction * wallDist;
        wallPos.y = wallHeight * 0.5f;

        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "BulwarkWall";
        wall.transform.position = wallPos;
        wall.transform.rotation = Quaternion.LookRotation(direction);
        wall.transform.localScale = new Vector3(wallWidth, wallHeight, wallThickness);

        Color earthCol = new Color(0.5f, 0.35f, 0.2f);
        wall.GetComponent<Renderer>().material = ShaderCache.NewLit(earthCol);

        // The box collider from CreatePrimitive blocks movement and projectiles
        var col = wall.GetComponent<BoxCollider>();
        col.isTrigger = false;

        // Add rigidbody to make it a static physics object
        var rb = wall.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // Spawn animation: scale up from ground
        wall.AddComponent<BulwarkRise>().Init(wallHeight, duration);

        // Dust VFX at base
        for (int i = 0; i < 6; i++)
        {
            var dust = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(dust.GetComponent<SphereCollider>());
            float xOff = Random.Range(-wallWidth * 0.5f, wallWidth * 0.5f);
            Vector3 dustPos = wallPos + wall.transform.right * xOff;
            dustPos.y = 0.2f;
            dust.transform.position = dustPos;
            dust.transform.localScale = Vector3.one * 0.2f;
            dust.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(0.6f, 0.5f, 0.3f), 2f);
            dust.AddComponent<ComboExpandVFX>().Init(0.8f, 0.5f, new Color(0.6f, 0.5f, 0.3f));
        }

        if (TopDownCamera.Instance != null)
            TopDownCamera.Instance.AddTrauma(0.15f);
        SFXSystem.Play(SFXSystem.SFXType.Hit, wallPos);
    }
}

/// <summary>Animate wall rising from ground, then destroy after duration.</summary>
public class BulwarkRise : MonoBehaviour
{
    float _targetHeight;
    float _riseTime = 0.3f;
    float _duration;
    float _timer;
    Vector3 _fullScale;
    bool _risen;

    public void Init(float targetHeight, float duration)
    {
        _targetHeight = targetHeight;
        _duration = duration;
        _fullScale = transform.localScale;
        transform.localScale = new Vector3(_fullScale.x, 0.1f, _fullScale.z);

        Vector3 pos = transform.position;
        pos.y = 0.05f;
        transform.position = pos;
    }

    void Update()
    {
        _timer += Time.deltaTime;

        if (!_risen)
        {
            float t = Mathf.Clamp01(_timer / _riseTime);
            float h = Mathf.Lerp(0.1f, _fullScale.y, t);
            transform.localScale = new Vector3(_fullScale.x, h, _fullScale.z);

            Vector3 pos = transform.position;
            pos.y = h * 0.5f;
            transform.position = pos;

            if (t >= 1f) _risen = true;
        }

        if (_timer >= _duration)
        {
            // Crumble: spawn fragments
            Vector3 pos = transform.position;
            for (int i = 0; i < 8; i++)
            {
                var frag = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.Destroy(frag.GetComponent<BoxCollider>());
                frag.transform.position = pos + Random.insideUnitSphere * 1f;
                float s = Random.Range(0.1f, 0.3f);
                frag.transform.localScale = new Vector3(s, s, s);
                frag.GetComponent<Renderer>().material = ShaderCache.NewLit(new Color(0.5f, 0.35f, 0.2f));
                var rb = frag.AddComponent<Rigidbody>();
                rb.mass = 0.5f;
                rb.AddForce(Random.insideUnitSphere * 3f + Vector3.up * 2f, ForceMode.Impulse);
                Object.Destroy(frag, 1.5f);
            }
            Destroy(gameObject);
        }
    }
}
