using UnityEngine;

/// <summary>
/// Earth + Earth = Bulwark (Fortress Wall)
/// Creates a stone wall that damages enemies on spawn + knockback.
/// Charged: spike wall that damages enemies pushed against it.
/// If a wall already exists, casting again shatters it for burst damage.
/// </summary>
public static class BulwarkSpell
{
    internal static GameObject _activeWall;

    public static void Cast(Vector3 origin, Vector3 direction, float duration, bool charged)
    {
        // If wall exists: SHATTER it for burst damage
        if (_activeWall != null)
        {
            ShatterWall(_activeWall);
            _activeWall = null;
            return;
        }

        float wallDist = 3f;
        float wallWidth = charged ? 4.5f : 3f;
        float wallHeight = charged ? 2.7f : 1.8f;
        float wallThickness = 0.6f;
        float wallDuration = charged ? 6f : 4f;

        Vector3 wallPos = origin + direction * wallDist;
        wallPos.y = wallHeight * 0.5f;

        var wallParent = new GameObject("BulwarkWall");
        wallParent.transform.position = wallPos;
        wallParent.transform.rotation = Quaternion.LookRotation(direction);
        _activeWall = wallParent;

        // Main physics collider
        var mainCol = wallParent.AddComponent<BoxCollider>();
        mainCol.size = new Vector3(wallWidth, wallHeight, wallThickness);
        mainCol.isTrigger = false;

        var rb = wallParent.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // Rocky appearance
        int rockCount = 12;
        for (int i = 0; i < rockCount; i++)
        {
            var rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(rock.GetComponent<BoxCollider>());
            rock.transform.SetParent(wallParent.transform, false);

            float xPos = Random.Range(-wallWidth * 0.45f, wallWidth * 0.45f);
            float yPos = Random.Range(-wallHeight * 0.4f, wallHeight * 0.4f);
            float zPos = Random.Range(-wallThickness * 0.3f, wallThickness * 0.3f);
            rock.transform.localPosition = new Vector3(xPos, yPos, zPos);

            float sx = Random.Range(wallWidth * 0.2f, wallWidth * 0.4f);
            float sy = Random.Range(wallHeight * 0.2f, wallHeight * 0.4f);
            float sz = Random.Range(wallThickness * 0.4f, wallThickness * 0.8f);
            rock.transform.localScale = new Vector3(sx, sy, sz);
            rock.transform.localRotation = Quaternion.Euler(
                Random.Range(-8f, 8f), Random.Range(-8f, 8f), Random.Range(-5f, 5f));

            Color earthCol = new Color(
                Random.Range(0.35f, 0.6f),
                Random.Range(0.25f, 0.4f),
                Random.Range(0.12f, 0.25f));
            rock.GetComponent<Renderer>().material = ShaderCache.NewStone(earthCol);
        }

        // Add spikes for charged version
        if (charged)
        {
            for (int i = 0; i < 6; i++)
            {
                var spike = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.Destroy(spike.GetComponent<BoxCollider>());
                spike.transform.SetParent(wallParent.transform, false);

                float xPos = Random.Range(-wallWidth * 0.4f, wallWidth * 0.4f);
                float yPos = Random.Range(-wallHeight * 0.3f, wallHeight * 0.3f);
                spike.transform.localPosition = new Vector3(xPos, yPos, wallThickness * 0.5f);
                spike.transform.localScale = new Vector3(0.15f, 0.15f, 0.6f);
                spike.transform.localRotation = Quaternion.Euler(
                    Random.Range(-15f, 15f), 0, Random.Range(-10f, 10f));
                spike.GetComponent<Renderer>().material = ShaderCache.NewStone(new Color(0.3f, 0.2f, 0.1f));
            }
        }

        // Spawn animation + wall behavior
        wallParent.AddComponent<BulwarkFortress>().Init(wallHeight, wallDuration, direction, wallWidth, charged);

        // Damage enemies on spawn (rising wall hits them)
        Collider[] hits = Physics.OverlapBox(wallPos,
            new Vector3(wallWidth * 0.5f, wallHeight * 0.5f, wallThickness * 0.5f + 0.5f),
            Quaternion.LookRotation(direction));
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
            {
                hp.TakeDamage(5);
                GameFeel.ApplyKnockback(h.transform, wallPos, 5f);
            }
        }

        // Dust cloud particles
        SpawnDust(new Vector3(wallPos.x, 0.1f, wallPos.z), direction, wallWidth);

        if (TopDownCamera.Instance != null)
            TopDownCamera.Instance.AddTrauma(0.2f);
        SFXSystem.Play(SFXSystem.SFXType.Hit, wallPos);
    }

    static void ShatterWall(GameObject wall)
    {
        Vector3 pos = wall.transform.position;
        Vector3 forward = wall.transform.forward;

        // Shrapnel damage in a cone in front of the wall
        float shatterRadius = 4f;
        int shatterDamage = 8;

        Collider[] hits = Physics.OverlapSphere(pos, shatterRadius);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp == null || hp.IsDead) continue;

            // Only hit enemies in front of the wall (cone check)
            Vector3 toEnemy = (h.transform.position - pos).normalized;
            if (Vector3.Dot(toEnemy, forward) > -0.3f) // Wide cone
            {
                hp.TakeDamage(shatterDamage);
                GameFeel.ApplyKnockback(h.transform, pos, 6f);
            }
        }

        // Shrapnel VFX: rock fragments flying forward
        for (int i = 0; i < 15; i++)
        {
            var frag = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(frag.GetComponent<BoxCollider>());
            frag.transform.position = pos + Random.insideUnitSphere * 1.5f;
            float s = Random.Range(0.1f, 0.35f);
            frag.transform.localScale = new Vector3(s, s * 0.7f, s);
            frag.transform.rotation = Random.rotation;
            Color fragCol = new Color(
                Random.Range(0.35f, 0.55f),
                Random.Range(0.25f, 0.4f),
                Random.Range(0.12f, 0.22f));
            frag.GetComponent<Renderer>().material = ShaderCache.NewStone(fragCol);
            var fragRb = frag.AddComponent<Rigidbody>();
            fragRb.mass = 0.5f;
            Vector3 fragDir = forward + Random.insideUnitSphere * 0.5f;
            fragRb.AddForce(fragDir * 8f + Vector3.up * 3f, ForceMode.Impulse);
            fragRb.AddTorque(Random.insideUnitSphere * 5f, ForceMode.Impulse);
            Object.Destroy(frag, 1.5f);
        }

        if (TopDownCamera.Instance != null)
            TopDownCamera.Instance.AddTrauma(0.3f);
        if (GameFeel.Instance != null)
            GameFeel.Instance.Hitstop(0.04f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, pos);

        Object.Destroy(wall);
    }

    static void SpawnDust(Vector3 pos, Vector3 dir, float width)
    {
        var dustGO = new GameObject("BulwarkDust");
        dustGO.transform.position = pos;
        dustGO.transform.rotation = Quaternion.LookRotation(dir);
        var dustPS = dustGO.AddComponent<ParticleSystem>();
        dustPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var dustMain = dustPS.main;
        dustMain.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        dustMain.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
        dustMain.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
        dustMain.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.6f, 0.5f, 0.35f, 0.6f),
            new Color(0.5f, 0.4f, 0.25f, 0.4f));
        dustMain.simulationSpace = ParticleSystemSimulationSpace.World;
        dustMain.gravityModifier = 0.2f;
        dustMain.duration = 0.3f;
        dustMain.loop = false;

        var dustEmission = dustPS.emission;
        dustEmission.rateOverTime = 0;
        dustEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 15) });

        var dustShape = dustPS.shape;
        dustShape.shapeType = ParticleSystemShapeType.Box;
        dustShape.scale = new Vector3(width, 0.2f, 0.6f);

        var dustSize = dustPS.sizeOverLifetime;
        dustSize.enabled = true;
        dustSize.size = new ParticleSystem.MinMaxCurve(1f,
            AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.5f));

        var dustColor = dustPS.colorOverLifetime;
        dustColor.enabled = true;
        var dGrad = new Gradient();
        dGrad.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.6f, 0.5f, 0.3f), 0f),
                new GradientColorKey(new Color(0.5f, 0.4f, 0.3f), 1f)
            },
            new[] {
                new GradientAlphaKey(0.6f, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        dustColor.color = dGrad;

        var mat = new Material(ShaderCache.ParticleShader);
        mat.SetColor("_Color", new Color(0.55f, 0.45f, 0.3f));
        dustGO.GetComponent<ParticleSystemRenderer>().material = mat;
        dustPS.Play();
        Object.Destroy(dustGO, 1.2f);
    }
}

/// <summary>Fortress wall: rises from ground, damages on contact (charged), auto-shatters on expire.</summary>
public class BulwarkFortress : MonoBehaviour
{
    float _targetHeight;
    float _riseTime = 0.3f;
    float _duration;
    float _timer;
    bool _risen;
    Vector3 _forward;
    float _width;
    bool _charged;
    float _spikeDamageTimer;

    public void Init(float targetHeight, float duration, Vector3 forward, float width, bool charged)
    {
        _targetHeight = targetHeight;
        _duration = duration;
        _forward = forward;
        _width = width;
        _charged = charged;
    }

    void Update()
    {
        _timer += Time.deltaTime;

        if (!_risen)
        {
            float t = Mathf.Clamp01(_timer / _riseTime);
            float yScale = Mathf.Lerp(0.05f, 1f, t);
            transform.localScale = new Vector3(1f, yScale, 1f);
            Vector3 pos = transform.position;
            pos.y = _targetHeight * 0.5f * yScale;
            transform.position = pos;
            if (t >= 1f) _risen = true;
        }

        // Charged: spike damage to enemies touching the wall
        if (_charged && _risen)
        {
            _spikeDamageTimer -= Time.deltaTime;
            if (_spikeDamageTimer <= 0)
            {
                _spikeDamageTimer = 0.5f;
                var col = GetComponent<BoxCollider>();
                if (col != null)
                {
                    Vector3 spikeCenter = transform.position + _forward * 0.5f;
                    Collider[] hits = Physics.OverlapBox(spikeCenter,
                        new Vector3(_width * 0.5f, _targetHeight * 0.5f, 0.8f),
                        transform.rotation);
                    foreach (var h in hits)
                    {
                        if (h.GetComponent<PlayerController>() != null) continue;
                        var hp = h.GetComponent<Health>();
                        if (hp != null && !hp.IsDead)
                            hp.TakeDamage(3);
                    }
                }
            }
        }

        if (_timer >= _duration)
        {
            // Crumble on expire
            Vector3 pos = transform.position;
            for (int i = 0; i < 10; i++)
            {
                var frag = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.Destroy(frag.GetComponent<BoxCollider>());
                frag.transform.position = pos + Random.insideUnitSphere * 1.2f;
                float s = Random.Range(0.1f, 0.3f);
                frag.transform.localScale = new Vector3(s, s * 0.7f, s);
                frag.transform.rotation = Random.rotation;
                frag.GetComponent<Renderer>().material = ShaderCache.NewStone(
                    new Color(Random.Range(0.35f, 0.55f), Random.Range(0.25f, 0.4f), Random.Range(0.12f, 0.22f)));
                var fragRb = frag.AddComponent<Rigidbody>();
                fragRb.mass = 0.5f;
                fragRb.AddForce(Random.insideUnitSphere * 3f + Vector3.up * 2f, ForceMode.Impulse);
                fragRb.AddTorque(Random.insideUnitSphere * 3f, ForceMode.Impulse);
                Object.Destroy(frag, 1.5f);
            }

            if (TopDownCamera.Instance != null)
                TopDownCamera.Instance.AddTrauma(0.1f);
            Destroy(gameObject);
        }
    }

    void OnDestroy()
    {
        // Clear static reference
        if (BulwarkSpell._activeWall == gameObject)
            BulwarkSpell._activeWall = null;
    }
}
