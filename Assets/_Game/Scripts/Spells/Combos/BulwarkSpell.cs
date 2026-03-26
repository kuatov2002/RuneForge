using UnityEngine;

/// <summary>
/// Earth + Earth = Bulwark
/// Creates a stone wall in the direction of the cursor.
/// Wall physically blocks projectiles and enemies.
/// Balance: width 3 (was 4), height 1.8 (was 2), duration 4s (was 5s)
/// </summary>
public static class BulwarkSpell
{
    public static void Cast(Vector3 origin, Vector3 direction, float duration, bool charged)
    {
        float wallDist = 3f;
        float wallWidth = charged ? 4.5f : 3f;
        float wallHeight = charged ? 2.7f : 1.8f;
        float wallThickness = 0.6f;
        float wallDuration = charged ? 6f : 4f;

        Vector3 wallPos = origin + direction * wallDist;
        wallPos.y = wallHeight * 0.5f;

        // Build rocky wall from multiple overlapping cubes of varying brown shades
        var wallParent = new GameObject("BulwarkWall");
        wallParent.transform.position = wallPos;
        wallParent.transform.rotation = Quaternion.LookRotation(direction);

        // Main physics collider (invisible)
        var mainCol = wallParent.AddComponent<BoxCollider>();
        mainCol.size = new Vector3(wallWidth, wallHeight, wallThickness);
        mainCol.isTrigger = false;

        var rb = wallParent.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // Rocky appearance: multiple cubes with varying brown shades
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

        // Spawn animation: scale up from ground
        wallParent.AddComponent<BulwarkRise>().Init(wallHeight, wallDuration);

        // Dust cloud particles at base on spawn
        var dustGO = new GameObject("BulwarkDust");
        dustGO.transform.position = new Vector3(wallPos.x, 0.1f, wallPos.z);
        dustGO.transform.rotation = Quaternion.LookRotation(direction);
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
        dustShape.scale = new Vector3(wallWidth, 0.2f, wallThickness);

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

        dustGO.GetComponent<ParticleSystemRenderer>().material =
            CreateDustParticleMat(new Color(0.55f, 0.45f, 0.3f));
        dustPS.Play();
        Object.Destroy(dustGO, 1.2f);

        if (TopDownCamera.Instance != null)
            TopDownCamera.Instance.AddTrauma(0.15f);
        SFXSystem.Play(SFXSystem.SFXType.Hit, wallPos);
    }

    static Material CreateDustParticleMat(Color color)
    {
        var mat = new Material(Shader.Find("Particles/Standard Unlit"));
        mat.SetColor("_Color", color);
        return mat;
    }
}

/// <summary>Animate wall rising from ground, then destroy after duration with crumble VFX.</summary>
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
        _fullScale = Vector3.one; // parent scale is 1,1,1; children handle visual scale
    }

    void Update()
    {
        _timer += Time.deltaTime;

        if (!_risen)
        {
            float t = Mathf.Clamp01(_timer / _riseTime);
            // Scale child rocks up from ground
            float yScale = Mathf.Lerp(0.05f, 1f, t);
            transform.localScale = new Vector3(1f, yScale, 1f);

            Vector3 pos = transform.position;
            pos.y = _targetHeight * 0.5f * yScale;
            transform.position = pos;

            if (t >= 1f) _risen = true;
        }

        if (_timer >= _duration)
        {
            // Crumble: spawn rock fragments with physics
            Vector3 pos = transform.position;
            for (int i = 0; i < 10; i++)
            {
                var frag = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.Destroy(frag.GetComponent<BoxCollider>());
                frag.transform.position = pos + Random.insideUnitSphere * 1.2f;
                float s = Random.Range(0.1f, 0.3f);
                frag.transform.localScale = new Vector3(s, s * 0.7f, s);
                frag.transform.rotation = Random.rotation;
                Color fragCol = new Color(
                    Random.Range(0.35f, 0.55f),
                    Random.Range(0.25f, 0.4f),
                    Random.Range(0.12f, 0.22f));
                frag.GetComponent<Renderer>().material = ShaderCache.NewStone(fragCol);
                var fragRb = frag.AddComponent<Rigidbody>();
                fragRb.mass = 0.5f;
                fragRb.AddForce(Random.insideUnitSphere * 3f + Vector3.up * 2f, ForceMode.Impulse);
                fragRb.AddTorque(Random.insideUnitSphere * 3f, ForceMode.Impulse);
                Object.Destroy(frag, 1.5f);
            }

            // Crumble dust burst
            var dustGO = new GameObject("CrumbleDust");
            dustGO.transform.position = pos;
            var dustPS = dustGO.AddComponent<ParticleSystem>();
            dustPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var m = dustPS.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
            m.startColor = new Color(0.5f, 0.4f, 0.3f, 0.5f);
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.gravityModifier = 0.3f;
            m.duration = 0.2f;
            m.loop = false;

            var e = dustPS.emission;
            e.rateOverTime = 0;
            e.SetBursts(new[] { new ParticleSystem.Burst(0f, 12) });

            var sh = dustPS.shape;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = 1f;

            var col = dustPS.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(new Color(0.5f, 0.4f, 0.3f), 0f), new GradientColorKey(new Color(0.4f, 0.35f, 0.25f), 1f) },
                new[] { new GradientAlphaKey(0.5f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            var mat = new Material(Shader.Find("Particles/Standard Unlit"));
            mat.SetColor("_Color", new Color(0.5f, 0.4f, 0.3f));
            dustGO.GetComponent<ParticleSystemRenderer>().material = mat;
            dustPS.Play();
            Object.Destroy(dustGO, 1f);

            if (TopDownCamera.Instance != null)
                TopDownCamera.Instance.AddTrauma(0.1f);

            Destroy(gameObject);
        }
    }
}
