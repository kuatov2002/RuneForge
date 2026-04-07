using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.TextCore.Text;
using System.Collections.Generic;

/// <summary>
/// Procedural title screen built from the same visual language as the game:
/// dark stone floor, glowing rune circle, forge anvil, elemental torches,
/// the player character, and floating rune particles — all top-down.
/// </summary>
public class TitleScreen : MonoBehaviour
{
    UIDocument _uiDoc;
    System.Action _onPlay;
    float _time;

    // UI
    Label _titleLabel;
    VisualElement _container;
    bool _fadingIn = true;
    float _fadeTimer;

    // 3D scene
    GameObject _sceneRoot;
    Transform _runeCircle;
    Transform _portalBeam;
    Transform _playerModel;
    Transform _staffTip;
    Transform _anvilGlow;
    Transform _hotMetal; // glowing workpiece on anvil
    Transform _bellows;
    Transform _camera;
    List<Transform> _torchFlames = new();
    List<Transform> _forgeSparks = new();
    List<Vector3> _sparkVelocities = new();
    List<float> _sparkLife = new();
    List<Transform> _runeParticles = new();
    List<float> _particleSpeeds = new();
    List<Transform> _chains = new();
    List<Transform> _crystals = new();
    List<Transform> _smokeWisps = new();
    List<float> _smokeLife = new();
    Light _portalLight;
    Light _forgeLight;
    Light _pitLight;

    static readonly Color ForgeOrange = new(0.9f, 0.4f, 0.1f);
    static readonly Color PortalGreen = new(0.2f, 0.8f, 0.4f);
    static readonly Color RunePurple = new(0.4f, 0.2f, 0.8f);
    static readonly Color WallColor = new(0.25f, 0.2f, 0.3f);
    static readonly Color FloorDark = new(0.12f, 0.1f, 0.15f);
    static readonly Color FloorAlt = new(0.15f, 0.12f, 0.18f);

    static readonly Color[] ElemColors = {
        new(1f, 0.4f, 0.1f),   // Fire
        new(0.3f, 0.7f, 1f),   // Water
        new(0.6f, 0.4f, 0.2f), // Earth
        new(0.8f, 0.9f, 1f),   // Air
        new(1f, 1f, 0.3f),     // Lightning
        new(0.2f, 0.9f, 0.1f), // Poison
        new(0.6f, 0.1f, 0.9f), // Void
    };

    public void Init(System.Action onPlay)
    {
        _onPlay = onPlay;
        _camera = Camera.main != null ? Camera.main.transform : null;
        Build3DScene();
        BuildUI();
    }

    void Build3DScene()
    {
        _sceneRoot = new GameObject("TitleScene");
        var root = _sceneRoot.transform;

        // ── Floor: checkerboard tile grid (matches game rooms) ──
        for (int x = -6; x <= 6; x++)
        for (int z = -4; z <= 6; z++)
        {
            var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(tile.GetComponent<BoxCollider>());
            tile.name = "Floor";
            tile.transform.parent = root;
            tile.transform.position = new Vector3(x, -0.25f, z);
            tile.transform.localScale = new Vector3(0.98f, 0.5f, 0.98f);
            bool checker = (x + z) % 2 == 0;
            tile.GetComponent<Renderer>().material = ShaderCache.NewLit(checker ? FloorDark : FloorAlt);
        }

        // ── Walls (back and sides, partial — like a room) ──
        BuildWall(root, new Vector3(0, 1.5f, 6.5f), new Vector3(13f, 3f, 0.5f));
        BuildWall(root, new Vector3(-6.5f, 1.5f, 1f), new Vector3(0.5f, 3f, 11f));
        BuildWall(root, new Vector3(6.5f, 1.5f, 1f), new Vector3(0.5f, 3f, 11f));

        // ── Central rune circle (like the hub) ──
        var circle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(circle.GetComponent<CapsuleCollider>());
        circle.name = "RuneCircle";
        circle.transform.parent = root;
        circle.transform.position = new Vector3(0, 0.02f, 2f);
        circle.transform.localScale = new Vector3(5f, 0.02f, 5f);
        circle.GetComponent<Renderer>().material = ShaderCache.NewEmissive(RunePurple, 2.5f);
        _runeCircle = circle.transform;

        // Inner circle (brighter ring)
        var innerCircle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(innerCircle.GetComponent<CapsuleCollider>());
        innerCircle.transform.parent = root;
        innerCircle.transform.position = new Vector3(0, 0.03f, 2f);
        innerCircle.transform.localScale = new Vector3(3f, 0.02f, 3f);
        innerCircle.GetComponent<Renderer>().material = ShaderCache.NewEmissive(RunePurple, 1.2f);

        // Rune circle ambient light (casts purple glow onto floor)
        var runeLightGO = new GameObject("RuneLight");
        runeLightGO.transform.parent = root;
        runeLightGO.transform.position = new Vector3(0, 0.5f, 2f);
        var runeLight = runeLightGO.AddComponent<Light>();
        runeLight.type = LightType.Point;
        runeLight.color = RunePurple;
        runeLight.intensity = 1.5f;
        runeLight.range = 5f;

        // ── Forge anvil (detailed — stone pedestal + iron body + horn + rune face) ──
        var anvilParent = new GameObject("Anvil");
        anvilParent.transform.parent = root;
        anvilParent.transform.position = new Vector3(0, 0, 4.5f);
        var darkIron = new Color(0.22f, 0.22f, 0.25f);
        var stoneBase = new Color(0.18f, 0.15f, 0.2f);

        // Stone pedestal (wider base, narrower top)
        MakePart(anvilParent.transform, PrimitiveType.Cube, Vector3.up * 0.2f,
            new Vector3(0.9f, 0.4f, 0.7f), ShaderCache.NewStone(stoneBase));
        MakePart(anvilParent.transform, PrimitiveType.Cube, Vector3.up * 0.45f,
            new Vector3(0.7f, 0.1f, 0.55f), ShaderCache.NewStone(stoneBase * 1.1f));

        // Anvil body (main block — slightly tapered via two overlapping cubes)
        MakePart(anvilParent.transform, PrimitiveType.Cube, new Vector3(0, 0.65f, 0),
            new Vector3(0.5f, 0.3f, 0.45f), ShaderCache.NewMetal(darkIron));

        // Anvil face (flat working surface on top — wider than body)
        MakePart(anvilParent.transform, PrimitiveType.Cube, new Vector3(0, 0.82f, 0),
            new Vector3(0.9f, 0.05f, 0.55f), ShaderCache.NewMetal(new Color(0.3f, 0.3f, 0.33f)));

        // Anvil horn (tapered point on one side — stretched cube + small tip)
        MakePart(anvilParent.transform, PrimitiveType.Cube, new Vector3(0.55f, 0.75f, 0),
            new Vector3(0.3f, 0.12f, 0.25f), ShaderCache.NewMetal(darkIron));
        MakePart(anvilParent.transform, PrimitiveType.Sphere, new Vector3(0.72f, 0.75f, 0),
            new Vector3(0.12f, 0.1f, 0.15f), ShaderCache.NewMetal(darkIron));

        // Anvil heel (back step — opposite side of horn)
        MakePart(anvilParent.transform, PrimitiveType.Cube, new Vector3(-0.4f, 0.72f, 0),
            new Vector3(0.15f, 0.18f, 0.45f), ShaderCache.NewMetal(darkIron));

        // Rune channel carved into the face (glowing inset)
        var anvilGlowGO = MakePart(anvilParent.transform, PrimitiveType.Cube, new Vector3(0, 0.86f, 0),
            new Vector3(0.5f, 0.015f, 0.3f), ShaderCache.NewEmissive(ForgeOrange, 5f));
        _anvilGlow = anvilGlowGO.transform;

        // Side rune lines (two glowing strips on the body)
        MakePart(anvilParent.transform, PrimitiveType.Cube, new Vector3(0, 0.65f, 0.24f),
            new Vector3(0.35f, 0.12f, 0.01f), ShaderCache.NewEmissive(ForgeOrange, 2f));
        MakePart(anvilParent.transform, PrimitiveType.Cube, new Vector3(0, 0.65f, -0.24f),
            new Vector3(0.35f, 0.12f, 0.01f), ShaderCache.NewEmissive(ForgeOrange, 2f));

        // Forge point light (warm, dramatic)
        var forgeLightGO = new GameObject("ForgeLight");
        forgeLightGO.transform.parent = root;
        forgeLightGO.transform.position = new Vector3(0, 1.8f, 4.5f);
        _forgeLight = forgeLightGO.AddComponent<Light>();
        _forgeLight.type = LightType.Point;
        _forgeLight.color = ForgeOrange;
        _forgeLight.intensity = 3f;
        _forgeLight.range = 7f;

        // ── 7 elemental braziers in a semicircle ──
        for (int i = 0; i < 7; i++)
        {
            float angle = Mathf.Lerp(-80f, 80f, i / 6f) * Mathf.Deg2Rad;
            float radius = 4.5f;
            float tx = Mathf.Sin(angle) * radius;
            float tz = 2f + Mathf.Cos(angle) * radius;
            Color ec = ElemColors[i];

            var brazierParent = new GameObject($"Brazier_{i}");
            brazierParent.transform.parent = root;
            brazierParent.transform.position = new Vector3(tx, 0, tz);

            // Pillar shaft (stone column)
            MakePart(brazierParent.transform, PrimitiveType.Cylinder, Vector3.up * 0.45f,
                new Vector3(0.18f, 0.45f, 0.18f), ShaderCache.NewStone(WallColor));

            // Pillar base (wider disc)
            MakePart(brazierParent.transform, PrimitiveType.Cylinder, Vector3.up * 0.03f,
                new Vector3(0.32f, 0.03f, 0.32f), ShaderCache.NewStone(WallColor * 0.8f));

            // Bowl / brazier cup (wide short cylinder on top)
            MakePart(brazierParent.transform, PrimitiveType.Cylinder, Vector3.up * 0.92f,
                new Vector3(0.28f, 0.06f, 0.28f), ShaderCache.NewMetal(new Color(0.25f, 0.22f, 0.2f)));

            // Inner bowl (dark, slightly smaller — gives depth illusion)
            MakePart(brazierParent.transform, PrimitiveType.Cylinder, Vector3.up * 0.93f,
                new Vector3(0.22f, 0.04f, 0.22f), ShaderCache.NewLit(new Color(0.05f, 0.04f, 0.04f)));

            // Flame core (sphere — main glow)
            var flame = MakePart(brazierParent.transform, PrimitiveType.Sphere, Vector3.up * 1.08f,
                Vector3.one * 0.2f, ShaderCache.NewEmissive(ec, 5f));
            _torchFlames.Add(flame.transform);

            // Flame tip (smaller, brighter, slightly above)
            MakePart(brazierParent.transform, PrimitiveType.Sphere, Vector3.up * 1.2f,
                Vector3.one * 0.1f, ShaderCache.NewEmissive(Color.Lerp(ec, Color.white, 0.3f), 6f));

            // Faint element-colored point light per brazier
            var bLight = new GameObject($"BrazierLight_{i}");
            bLight.transform.parent = brazierParent.transform;
            bLight.transform.localPosition = Vector3.up * 1.1f;
            var bl = bLight.AddComponent<Light>();
            bl.type = LightType.Point;
            bl.color = ec;
            bl.intensity = 1.2f;
            bl.range = 3f;
        }

        // ── Player character (matching game: green wizard with staff) ──
        _playerModel = new GameObject("TitlePlayer").transform;
        _playerModel.parent = root;
        _playerModel.position = new Vector3(0, 0, 1f);

        var bodyColor = new Color(0.15f, 0.55f, 0.3f);
        var robeColor = new Color(0.1f, 0.35f, 0.2f);

        // Body
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        Object.Destroy(body.GetComponent<CapsuleCollider>());
        body.transform.parent = _playerModel;
        body.transform.localPosition = new Vector3(0, 0.5f, 0);
        body.transform.localScale = new Vector3(0.5f, 0.35f, 0.45f);
        body.GetComponent<Renderer>().material = ShaderCache.NewLit(bodyColor);

        // Robe
        var robe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(robe.GetComponent<CapsuleCollider>());
        robe.transform.parent = _playerModel;
        robe.transform.localPosition = new Vector3(0, 0.2f, 0);
        robe.transform.localScale = new Vector3(0.55f, 0.2f, 0.55f);
        robe.GetComponent<Renderer>().material = ShaderCache.NewLit(robeColor);

        // Head
        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(head.GetComponent<SphereCollider>());
        head.transform.parent = _playerModel;
        head.transform.localPosition = new Vector3(0, 1.05f, 0);
        head.transform.localScale = Vector3.one * 0.35f;
        head.GetComponent<Renderer>().material = ShaderCache.NewSkin(new Color(0.85f, 0.7f, 0.55f));

        // Staff
        var staff = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(staff.GetComponent<CapsuleCollider>());
        staff.transform.parent = _playerModel;
        staff.transform.localPosition = new Vector3(0.25f, 0.7f, 0.2f);
        staff.transform.localScale = new Vector3(0.06f, 0.5f, 0.06f);
        staff.transform.localRotation = Quaternion.Euler(15, 0, -10);
        staff.GetComponent<Renderer>().material = ShaderCache.NewLit(new Color(0.45f, 0.3f, 0.15f));

        // Staff tip (glowing)
        var tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(tip.GetComponent<SphereCollider>());
        tip.transform.parent = staff.transform;
        tip.transform.localPosition = new Vector3(0, 1.1f, 0);
        tip.transform.localScale = new Vector3(2.5f, 0.4f, 2.5f);
        tip.GetComponent<Renderer>().material = ShaderCache.NewMagic(ForgeOrange, 4f);
        _staffTip = tip.transform;

        // Face player toward the forge (away from camera)
        _playerModel.rotation = Quaternion.Euler(0, 0, 0);

        // ── Portal beam behind forge (the run entrance) ──
        var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(beam.GetComponent<CapsuleCollider>());
        beam.name = "PortalBeam";
        beam.transform.parent = root;
        beam.transform.position = new Vector3(0, 1.5f, 5.5f);
        beam.transform.localScale = new Vector3(0.3f, 2.5f, 0.3f);
        beam.GetComponent<Renderer>().material = ShaderCache.NewEmissive(PortalGreen, 3f);
        _portalBeam = beam.transform;

        // Portal base ring
        var portalRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(portalRing.GetComponent<CapsuleCollider>());
        portalRing.transform.parent = root;
        portalRing.transform.position = new Vector3(0, 0.02f, 5.5f);
        portalRing.transform.localScale = new Vector3(2f, 0.02f, 2f);
        portalRing.GetComponent<Renderer>().material = ShaderCache.NewEmissive(PortalGreen, 2f);

        // Portal light
        var lightGO = new GameObject("PortalLight");
        lightGO.transform.parent = root;
        lightGO.transform.position = new Vector3(0, 2f, 5f);
        _portalLight = lightGO.AddComponent<Light>();
        _portalLight.type = LightType.Point;
        _portalLight.color = PortalGreen;
        _portalLight.intensity = 2f;
        _portalLight.range = 8f;

        // ── Floor rune lines radiating from the circle to the walls ──
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI * 2f / 8f;
            float len = 4f;
            float cx = Mathf.Sin(a) * len * 0.5f;
            float cz = 2f + Mathf.Cos(a) * len * 0.5f;
            var line = MakePart(root, PrimitiveType.Cube, new Vector3(cx, 0.015f, cz),
                new Vector3(0.06f, 0.01f, len), ShaderCache.NewEmissive(RunePurple * 0.7f, 1.2f));
            line.transform.rotation = Quaternion.Euler(0, -i * 45f, 0);
        }

        // ── Corner pillars (detailed — base + shaft + capital + rune inlay) ──
        float[] px = { -5f, 5f, -5f, 5f };
        float[] pz = { -2f, -2f, 5.5f, 5.5f };
        for (int i = 0; i < 4; i++)
        {
            var pp = new GameObject($"Pillar_{i}");
            pp.transform.parent = root;
            pp.transform.position = new Vector3(px[i], 0, pz[i]);

            // Base
            MakePart(pp.transform, PrimitiveType.Cylinder, Vector3.up * 0.05f,
                new Vector3(0.8f, 0.05f, 0.8f), ShaderCache.NewStone(WallColor * 0.8f));
            // Shaft
            MakePart(pp.transform, PrimitiveType.Cylinder, Vector3.up * 1.5f,
                new Vector3(0.5f, 1.5f, 0.5f), ShaderCache.NewStone(new Color(0.3f, 0.25f, 0.35f)));
            // Capital (top wider piece)
            MakePart(pp.transform, PrimitiveType.Cylinder, Vector3.up * 2.95f,
                new Vector3(0.65f, 0.08f, 0.65f), ShaderCache.NewStone(WallColor));
            // Rune glow ring on pillar
            MakePart(pp.transform, PrimitiveType.Cylinder, Vector3.up * 1.5f,
                new Vector3(0.55f, 0.03f, 0.55f), ShaderCache.NewEmissive(RunePurple, 1.5f));
        }

        // ── Wall sconces (pairs on side walls — fill the dark wall space) ──
        float[] scZ = { 0f, 2.5f, 5f };
        for (int side = -1; side <= 1; side += 2)
        {
            float wx = side * 6.2f;
            foreach (float sz in scZ)
            {
                // Bracket
                MakePart(root, PrimitiveType.Cube, new Vector3(wx, 1.6f, sz),
                    new Vector3(0.15f, 0.08f, 0.15f), ShaderCache.NewMetal(new Color(0.25f, 0.22f, 0.28f)));
                // Sconce flame
                var sf = MakePart(root, PrimitiveType.Sphere, new Vector3(wx, 1.8f, sz),
                    Vector3.one * 0.12f, ShaderCache.NewEmissive(ForgeOrange * 0.8f, 3f));
                _torchFlames.Add(sf.transform);
            }
        }

        // ── Forge tools next to anvil ──
        // Glowing hot metal workpiece on the anvil face (the star of the forge)
        _hotMetal = MakePart(anvilParent.transform, PrimitiveType.Cube, new Vector3(-0.05f, 0.87f, 0),
            new Vector3(0.25f, 0.04f, 0.12f), ShaderCache.NewEmissive(new Color(1f, 0.5f, 0.1f), 6f)).transform;

        // Hammer (lying on anvil face beside the workpiece)
        MakePart(anvilParent.transform, PrimitiveType.Cylinder, new Vector3(0.2f, 0.92f, 0.1f),
            new Vector3(0.04f, 0.22f, 0.04f), ShaderCache.NewLit(new Color(0.35f, 0.25f, 0.12f)))
            .transform.localRotation = Quaternion.Euler(0, 30, 85);
        MakePart(anvilParent.transform, PrimitiveType.Cube, new Vector3(0.4f, 0.94f, 0.1f),
            new Vector3(0.12f, 0.08f, 0.08f), ShaderCache.NewMetal(new Color(0.3f, 0.28f, 0.3f)))
            .transform.localRotation = Quaternion.Euler(0, 30, 0);

        // Tongs (leaning against pedestal)
        MakePart(anvilParent.transform, PrimitiveType.Cylinder, new Vector3(-0.55f, 0.35f, 0.2f),
            new Vector3(0.025f, 0.35f, 0.025f), ShaderCache.NewMetal(darkIron))
            .transform.localRotation = Quaternion.Euler(0, 0, 15);
        MakePart(anvilParent.transform, PrimitiveType.Cylinder, new Vector3(-0.52f, 0.35f, 0.22f),
            new Vector3(0.025f, 0.35f, 0.025f), ShaderCache.NewMetal(darkIron))
            .transform.localRotation = Quaternion.Euler(0, 0, 18);

        // Bellows (beside coal pit — two boards + nozzle)
        var bellowsParent = new GameObject("Bellows");
        bellowsParent.transform.parent = root;
        bellowsParent.transform.position = new Vector3(1.8f, 0.3f, 5.3f);
        bellowsParent.transform.rotation = Quaternion.Euler(0, -30, 0);
        // Top board
        MakePart(bellowsParent.transform, PrimitiveType.Cube, new Vector3(0, 0.12f, 0),
            new Vector3(0.4f, 0.03f, 0.25f), ShaderCache.NewLit(new Color(0.25f, 0.18f, 0.1f)));
        // Bottom board
        MakePart(bellowsParent.transform, PrimitiveType.Cube, Vector3.zero,
            new Vector3(0.4f, 0.03f, 0.25f), ShaderCache.NewLit(new Color(0.22f, 0.15f, 0.08f)));
        // Leather middle (accordeon)
        MakePart(bellowsParent.transform, PrimitiveType.Cube, new Vector3(0, 0.06f, 0),
            new Vector3(0.35f, 0.06f, 0.2f), ShaderCache.NewLit(new Color(0.15f, 0.08f, 0.04f)));
        // Nozzle
        MakePart(bellowsParent.transform, PrimitiveType.Cylinder, new Vector3(-0.25f, 0.06f, 0),
            new Vector3(0.05f, 0.1f, 0.05f), ShaderCache.NewMetal(darkIron))
            .transform.localRotation = Quaternion.Euler(0, 0, 90);
        _bellows = bellowsParent.transform;

        // Coal pit / fire pit (beside anvil — glowing embers)
        var pitParent = new GameObject("CoalPit");
        pitParent.transform.parent = root;
        pitParent.transform.position = new Vector3(1.2f, 0, 4.8f);
        MakePart(pitParent.transform, PrimitiveType.Cylinder, Vector3.up * 0.12f,
            new Vector3(0.7f, 0.12f, 0.7f), ShaderCache.NewStone(stoneBase * 0.7f)); // stone rim
        MakePart(pitParent.transform, PrimitiveType.Cylinder, Vector3.up * 0.08f,
            new Vector3(0.55f, 0.08f, 0.55f), ShaderCache.NewEmissive(new Color(0.8f, 0.2f, 0.05f), 2f)); // coals glow
        // Coal chunks
        for (int c = 0; c < 5; c++)
        {
            float ca = Random.Range(0f, Mathf.PI * 2f);
            float cr = Random.Range(0f, 0.2f);
            MakePart(pitParent.transform, PrimitiveType.Cube,
                new Vector3(Mathf.Sin(ca) * cr, 0.18f, Mathf.Cos(ca) * cr),
                Vector3.one * Random.Range(0.05f, 0.1f),
                ShaderCache.NewEmissive(new Color(1f, 0.35f, 0.05f), Random.Range(1f, 3f)))
                .transform.rotation = Random.rotation;
        }
        // Pit light
        var pitLightGO = new GameObject("PitLight");
        pitLightGO.transform.parent = pitParent.transform;
        pitLightGO.transform.localPosition = Vector3.up * 0.4f;
        _pitLight = pitLightGO.AddComponent<Light>();
        _pitLight.type = LightType.Point;
        _pitLight.color = new Color(1f, 0.3f, 0.05f);
        _pitLight.intensity = 1.5f;
        _pitLight.range = 3f;

        // ── Weapon rack on left wall ──
        var rackX = -5.8f;
        // Back board
        MakePart(root, PrimitiveType.Cube, new Vector3(rackX, 1.5f, 3f),
            new Vector3(0.08f, 1.2f, 1.2f), ShaderCache.NewLit(new Color(0.2f, 0.14f, 0.08f)));
        // Pegs
        for (int p = 0; p < 3; p++)
        {
            float pegZ = 2.6f + p * 0.3f;
            MakePart(root, PrimitiveType.Cylinder, new Vector3(rackX + 0.12f, 1.7f, pegZ),
                new Vector3(0.04f, 0.1f, 0.04f), ShaderCache.NewLit(new Color(0.18f, 0.12f, 0.06f)))
                .transform.rotation = Quaternion.Euler(0, 0, 90);
        }
        // Staffs leaning on rack
        for (int s = 0; s < 3; s++)
        {
            float sz = 2.55f + s * 0.3f;
            Color tipCol = ElemColors[s];
            MakePart(root, PrimitiveType.Cylinder, new Vector3(rackX + 0.15f, 1.2f, sz),
                new Vector3(0.04f, 0.8f, 0.04f), ShaderCache.NewLit(new Color(0.35f, 0.25f, 0.12f)))
                .transform.rotation = Quaternion.Euler(0, 0, 5 + s * 3);
            // Staff tip orb
            MakePart(root, PrimitiveType.Sphere, new Vector3(rackX + 0.1f + s * 0.04f, 2.0f, sz),
                Vector3.one * 0.06f, ShaderCache.NewEmissive(tipCol, 3f));
        }

        // ── Potion shelf on right wall ──
        var shelfX = 5.8f;
        // Shelf boards (two levels)
        MakePart(root, PrimitiveType.Cube, new Vector3(shelfX, 1.0f, 1.5f),
            new Vector3(0.12f, 0.04f, 1.0f), ShaderCache.NewLit(new Color(0.2f, 0.14f, 0.08f)));
        MakePart(root, PrimitiveType.Cube, new Vector3(shelfX, 1.6f, 1.5f),
            new Vector3(0.12f, 0.04f, 1.0f), ShaderCache.NewLit(new Color(0.2f, 0.14f, 0.08f)));
        // Shelf brackets
        MakePart(root, PrimitiveType.Cube, new Vector3(shelfX, 1.3f, 1.1f),
            new Vector3(0.06f, 0.65f, 0.06f), ShaderCache.NewLit(new Color(0.18f, 0.12f, 0.06f)));
        MakePart(root, PrimitiveType.Cube, new Vector3(shelfX, 1.3f, 1.9f),
            new Vector3(0.06f, 0.65f, 0.06f), ShaderCache.NewLit(new Color(0.18f, 0.12f, 0.06f)));
        // Potion bottles (cylinders + sphere caps, various element colors)
        Color[] potionCols = { ElemColors[0], ElemColors[1], ElemColors[5], ElemColors[4], ElemColors[6] };
        for (int b = 0; b < 5; b++)
        {
            float bz = 1.15f + b * 0.18f;
            float by = b < 3 ? 1.06f : 1.66f;
            if (b >= 3) bz = 1.3f + (b - 3) * 0.25f;
            // Bottle body
            MakePart(root, PrimitiveType.Cylinder, new Vector3(shelfX - 0.02f, by, bz),
                new Vector3(0.05f, 0.05f, 0.05f), ShaderCache.NewEmissive(potionCols[b], 1.5f));
            // Bottle neck
            MakePart(root, PrimitiveType.Cylinder, new Vector3(shelfX - 0.02f, by + 0.06f, bz),
                new Vector3(0.025f, 0.02f, 0.025f), ShaderCache.NewLit(new Color(0.2f, 0.2f, 0.22f)));
        }

        // ── Crate stacks (left side near entrance) ──
        MakePart(root, PrimitiveType.Cube, new Vector3(-4.5f, 0.25f, -1f),
            new Vector3(0.6f, 0.5f, 0.5f), ShaderCache.NewLit(new Color(0.25f, 0.18f, 0.1f)));
        MakePart(root, PrimitiveType.Cube, new Vector3(-4.2f, 0.6f, -0.9f),
            new Vector3(0.4f, 0.3f, 0.4f), ShaderCache.NewLit(new Color(0.22f, 0.16f, 0.09f)));
        // More crates right side
        MakePart(root, PrimitiveType.Cube, new Vector3(4.2f, 0.2f, 5f),
            new Vector3(0.5f, 0.4f, 0.45f), ShaderCache.NewLit(new Color(0.2f, 0.15f, 0.09f)));

        // ── Scroll table (left of rune circle) ──
        var tableParent = new GameObject("ScrollTable");
        tableParent.transform.parent = root;
        tableParent.transform.position = new Vector3(-2.8f, 0, 2f);
        // Table legs
        for (int lx = -1; lx <= 1; lx += 2)
        for (int lz = -1; lz <= 1; lz += 2)
            MakePart(tableParent.transform, PrimitiveType.Cylinder,
                new Vector3(lx * 0.3f, 0.3f, lz * 0.2f),
                new Vector3(0.05f, 0.3f, 0.05f), ShaderCache.NewLit(new Color(0.2f, 0.14f, 0.08f)));
        // Table top
        MakePart(tableParent.transform, PrimitiveType.Cube, Vector3.up * 0.62f,
            new Vector3(0.8f, 0.04f, 0.5f), ShaderCache.NewLit(new Color(0.22f, 0.16f, 0.1f)));
        // Scrolls on table (rolled cylinders lying flat)
        MakePart(tableParent.transform, PrimitiveType.Cylinder, new Vector3(-0.15f, 0.68f, 0),
            new Vector3(0.04f, 0.15f, 0.04f), ShaderCache.NewLit(new Color(0.7f, 0.65f, 0.5f)))
            .transform.localRotation = Quaternion.Euler(0, 0, 90);
        MakePart(tableParent.transform, PrimitiveType.Cylinder, new Vector3(0.1f, 0.68f, 0.08f),
            new Vector3(0.04f, 0.12f, 0.04f), ShaderCache.NewLit(new Color(0.6f, 0.55f, 0.4f)))
            .transform.localRotation = Quaternion.Euler(0, 25, 90);
        // Open book on table
        MakePart(tableParent.transform, PrimitiveType.Cube, new Vector3(0.2f, 0.66f, -0.05f),
            new Vector3(0.2f, 0.02f, 0.15f), ShaderCache.NewLit(new Color(0.5f, 0.15f, 0.1f)));
        // Glowing rune on the open page
        MakePart(tableParent.transform, PrimitiveType.Cube, new Vector3(0.2f, 0.675f, -0.05f),
            new Vector3(0.08f, 0.005f, 0.06f), ShaderCache.NewEmissive(RunePurple, 2f));

        // ── Crystal cluster (right of rune circle — raw magic material) ──
        var crystalPos = new Vector3(3f, 0, 1.5f);
        for (int c = 0; c < 5; c++)
        {
            float ch = Random.Range(0.2f, 0.5f);
            float cw = Random.Range(0.06f, 0.12f);
            Color cc = ElemColors[Random.Range(0, ElemColors.Length)];
            var crystal = MakePart(root, PrimitiveType.Cube,
                crystalPos + new Vector3(Random.Range(-0.2f, 0.2f), ch * 0.5f, Random.Range(-0.2f, 0.2f)),
                new Vector3(cw, ch, cw), ShaderCache.NewEmissive(cc, 3f));
            crystal.transform.rotation = Quaternion.Euler(Random.Range(-15f, 15f), Random.Range(0f, 360f), Random.Range(-15f, 15f));
            _crystals.Add(crystal.transform);
        }

        // ── Barrel pair (left of forge) ──
        MakePart(root, PrimitiveType.Cylinder, new Vector3(-1.5f, 0.3f, 4.8f),
            new Vector3(0.35f, 0.3f, 0.35f), ShaderCache.NewLit(new Color(0.2f, 0.15f, 0.08f)));
        MakePart(root, PrimitiveType.Cylinder, new Vector3(-1.5f, 0.25f, 4.8f),
            new Vector3(0.37f, 0.02f, 0.37f), ShaderCache.NewMetal(new Color(0.2f, 0.18f, 0.15f)));
        MakePart(root, PrimitiveType.Cylinder, new Vector3(-1.8f, 0.25f, 5.1f),
            new Vector3(0.3f, 0.25f, 0.3f), ShaderCache.NewLit(new Color(0.18f, 0.13f, 0.07f)));
        MakePart(root, PrimitiveType.Cylinder, new Vector3(-1.8f, 0.2f, 5.1f),
            new Vector3(0.32f, 0.02f, 0.32f), ShaderCache.NewMetal(new Color(0.2f, 0.18f, 0.15f)));

        // ── Wall banners (hanging fabric with element colors on back wall) ──
        float[] bannerZ = { 6.3f, 6.3f, 6.3f };
        float[] bannerX = { -3.5f, 0f, 3.5f };
        Color[] bannerCols = { ElemColors[0], RunePurple, ElemColors[1] };
        for (int b = 0; b < 3; b++)
        {
            // Banner pole
            MakePart(root, PrimitiveType.Cylinder, new Vector3(bannerX[b], 2.4f, bannerZ[b]),
                new Vector3(0.04f, 0.04f, 0.04f), ShaderCache.NewMetal(darkIron))
                .transform.rotation = Quaternion.Euler(0, 0, 90);
            // Banner fabric
            MakePart(root, PrimitiveType.Cube, new Vector3(bannerX[b], 1.7f, bannerZ[b]),
                new Vector3(0.6f, 1.0f, 0.03f), ShaderCache.NewLit(new Color(bannerCols[b].r * 0.25f, bannerCols[b].g * 0.25f, bannerCols[b].b * 0.25f)));
            // Banner emblem glow
            MakePart(root, PrimitiveType.Cube, new Vector3(bannerX[b], 1.7f, bannerZ[b] - 0.02f),
                new Vector3(0.2f, 0.2f, 0.01f), ShaderCache.NewEmissive(bannerCols[b], 2f));
        }

        // ── Hanging chains from ceiling (near walls) ──
        float[] chainX = { -3f, 3f, -4.5f, 4.5f };
        float[] chainZ = { -1f, -1f, 3f, 3f };
        for (int c = 0; c < 4; c++)
        {
            float chainLen = Random.Range(0.6f, 1.2f);
            float topY = 3f;
            var chainParent = new GameObject($"Chain_{c}");
            chainParent.transform.parent = root;
            chainParent.transform.position = new Vector3(chainX[c], topY, chainZ[c]);
            for (int link = 0; link < 4; link++)
            {
                MakePart(chainParent.transform, PrimitiveType.Cube,
                    Vector3.down * (link * 0.18f),
                    new Vector3(0.03f, 0.12f, 0.03f),
                    ShaderCache.NewMetal(new Color(0.2f, 0.18f, 0.2f)))
                    .transform.localRotation = Quaternion.Euler(0, link * 90, 0);
            }
            _chains.Add(chainParent.transform);
        }

        // ── Small rune stones scattered on floor near the circle ──
        for (int rs = 0; rs < 10; rs++)
        {
            float ra = Random.Range(0f, Mathf.PI * 2f);
            float rd = Random.Range(2.6f, 4f);
            MakePart(root, PrimitiveType.Cube,
                new Vector3(Mathf.Sin(ra) * rd, 0.06f, 2f + Mathf.Cos(ra) * rd),
                new Vector3(0.1f, 0.1f, 0.1f) * Random.Range(0.6f, 1.4f),
                ShaderCache.NewEmissive(RunePurple * 0.5f, Random.Range(0.5f, 1.5f)))
                .transform.rotation = Random.rotation;
        }

        // ── Armor stand (right side, between potion shelf and braziers) ──
        var armorPos = new Vector3(3.5f, 0, 4f);
        // Stand pole
        MakePart(root, PrimitiveType.Cylinder, armorPos + Vector3.up * 0.7f,
            new Vector3(0.05f, 0.7f, 0.05f), ShaderCache.NewLit(new Color(0.2f, 0.14f, 0.08f)));
        // Stand base
        MakePart(root, PrimitiveType.Cylinder, armorPos + Vector3.up * 0.02f,
            new Vector3(0.3f, 0.02f, 0.3f), ShaderCache.NewLit(new Color(0.18f, 0.12f, 0.06f)));
        // Chest plate
        MakePart(root, PrimitiveType.Capsule, armorPos + new Vector3(0, 1.15f, 0),
            new Vector3(0.35f, 0.25f, 0.2f), ShaderCache.NewMetal(new Color(0.28f, 0.26f, 0.3f)));
        // Shoulder pads
        MakePart(root, PrimitiveType.Sphere, armorPos + new Vector3(-0.22f, 1.3f, 0),
            Vector3.one * 0.12f, ShaderCache.NewMetal(new Color(0.3f, 0.28f, 0.32f)));
        MakePart(root, PrimitiveType.Sphere, armorPos + new Vector3(0.22f, 1.3f, 0),
            Vector3.one * 0.12f, ShaderCache.NewMetal(new Color(0.3f, 0.28f, 0.32f)));

        // ── Workbench (left side, near scroll table) ──
        var benchPos = new Vector3(-3.5f, 0, 4.5f);
        // Bench legs
        for (int lx2 = -1; lx2 <= 1; lx2 += 2)
        for (int lz2 = -1; lz2 <= 1; lz2 += 2)
            MakePart(root, PrimitiveType.Cube, benchPos + new Vector3(lx2 * 0.4f, 0.25f, lz2 * 0.2f),
                new Vector3(0.06f, 0.5f, 0.06f), ShaderCache.NewLit(new Color(0.2f, 0.14f, 0.08f)));
        // Bench top
        MakePart(root, PrimitiveType.Cube, benchPos + Vector3.up * 0.52f,
            new Vector3(1f, 0.04f, 0.5f), ShaderCache.NewLit(new Color(0.22f, 0.16f, 0.1f)));
        // Tools on bench: pliers, file, small hammer
        MakePart(root, PrimitiveType.Cylinder, benchPos + new Vector3(-0.3f, 0.58f, 0.05f),
            new Vector3(0.02f, 0.15f, 0.02f), ShaderCache.NewMetal(darkIron))
            .transform.rotation = Quaternion.Euler(0, 15, 85);
        MakePart(root, PrimitiveType.Cylinder, benchPos + new Vector3(0.1f, 0.58f, -0.1f),
            new Vector3(0.02f, 0.12f, 0.02f), ShaderCache.NewMetal(darkIron))
            .transform.rotation = Quaternion.Euler(0, -20, 80);
        MakePart(root, PrimitiveType.Cube, benchPos + new Vector3(0.3f, 0.58f, 0.1f),
            new Vector3(0.08f, 0.06f, 0.06f), ShaderCache.NewMetal(new Color(0.3f, 0.28f, 0.3f)));
        // Rune stone being worked on
        MakePart(root, PrimitiveType.Cube, benchPos + new Vector3(0f, 0.58f, 0f),
            new Vector3(0.12f, 0.08f, 0.12f), ShaderCache.NewEmissive(ElemColors[4], 2f));

        // ── Bucket with water (near forge for quenching) ──
        MakePart(root, PrimitiveType.Cylinder, new Vector3(-0.8f, 0.2f, 3.8f),
            new Vector3(0.25f, 0.2f, 0.25f), ShaderCache.NewLit(new Color(0.18f, 0.12f, 0.08f)));
        MakePart(root, PrimitiveType.Cylinder, new Vector3(-0.8f, 0.15f, 3.8f),
            new Vector3(0.2f, 0.15f, 0.2f), ShaderCache.NewEmissive(new Color(0.15f, 0.25f, 0.4f), 0.5f));

        // ── Second crystal cluster (left of entrance) ──
        var crystalPos2 = new Vector3(-3f, 0, -1.5f);
        for (int c2 = 0; c2 < 4; c2++)
        {
            float ch2 = Random.Range(0.15f, 0.4f);
            float cw2 = Random.Range(0.05f, 0.1f);
            Color cc2 = ElemColors[3 + Random.Range(0, 4)]; // air, lightning, poison, void
            var cr2 = MakePart(root, PrimitiveType.Cube,
                crystalPos2 + new Vector3(Random.Range(-0.15f, 0.15f), ch2 * 0.5f, Random.Range(-0.15f, 0.15f)),
                new Vector3(cw2, ch2, cw2), ShaderCache.NewEmissive(cc2, 3f));
            cr2.transform.rotation = Quaternion.Euler(Random.Range(-20f, 20f), Random.Range(0f, 360f), Random.Range(-20f, 20f));
            _crystals.Add(cr2.transform);
        }

        // ── Rug / carpet in front of forge (worn fabric on floor) ──
        MakePart(root, PrimitiveType.Cube, new Vector3(0, 0.015f, 2.5f),
            new Vector3(2.5f, 0.02f, 1.5f), ShaderCache.NewLit(new Color(0.18f, 0.08f, 0.06f)));
        // Rug border (slightly brighter)
        MakePart(root, PrimitiveType.Cube, new Vector3(0, 0.012f, 2.5f),
            new Vector3(2.7f, 0.015f, 1.7f), ShaderCache.NewLit(new Color(0.22f, 0.1f, 0.08f)));

        // ── Ingot pile (near forge — raw materials) ──
        for (int ig = 0; ig < 6; ig++)
        {
            int row = ig / 3;
            int col = ig % 3;
            MakePart(root, PrimitiveType.Cube,
                new Vector3(1.8f + col * 0.15f, 0.06f + row * 0.1f, 3.8f),
                new Vector3(0.12f, 0.06f, 0.06f),
                ShaderCache.NewMetal(new Color(0.3f + Random.Range(0f, 0.1f), 0.28f, 0.25f)));
        }

        // ── Rope coil on floor (near barrels) ──
        MakePart(root, PrimitiveType.Cylinder, new Vector3(-2.2f, 0.06f, 5.3f),
            new Vector3(0.25f, 0.06f, 0.25f), ShaderCache.NewLit(new Color(0.3f, 0.25f, 0.15f)));

        // ── Candelabra on scroll table ──
        MakePart(root, PrimitiveType.Cylinder, new Vector3(-2.45f, 0.72f, 2.15f),
            new Vector3(0.03f, 0.1f, 0.03f), ShaderCache.NewMetal(new Color(0.3f, 0.25f, 0.2f)));
        var candle = MakePart(root, PrimitiveType.Cylinder, new Vector3(-2.45f, 0.85f, 2.15f),
            new Vector3(0.025f, 0.04f, 0.025f), ShaderCache.NewLit(new Color(0.8f, 0.75f, 0.6f)));
        var candleFlame = MakePart(root, PrimitiveType.Sphere, new Vector3(-2.45f, 0.92f, 2.15f),
            Vector3.one * 0.04f, ShaderCache.NewEmissive(new Color(1f, 0.7f, 0.2f), 4f));
        _torchFlames.Add(candleFlame.transform);

        // ── Extra book piles scattered ──
        // Near left wall
        for (int bp = 0; bp < 3; bp++)
            MakePart(root, PrimitiveType.Cube, new Vector3(-5.2f, 0.05f + bp * 0.07f, 0.5f + Random.Range(-0.1f, 0.1f)),
                new Vector3(0.22f, 0.05f, 0.16f),
                ShaderCache.NewLit(new Color(0.12f + bp * 0.06f, 0.06f + bp * 0.03f, 0.15f)));
        // Near right entrance
        for (int bp2 = 0; bp2 < 2; bp2++)
            MakePart(root, PrimitiveType.Cube, new Vector3(4.8f, 0.05f + bp2 * 0.07f, -0.5f + Random.Range(-0.15f, 0.15f)),
                new Vector3(0.25f, 0.05f, 0.18f),
                ShaderCache.NewLit(new Color(0.2f, 0.1f + bp2 * 0.05f, 0.08f)));

        // ── Sack / bag near barrels ──
        MakePart(root, PrimitiveType.Sphere, new Vector3(-1.2f, 0.15f, 5.4f),
            new Vector3(0.25f, 0.2f, 0.2f), ShaderCache.NewLit(new Color(0.22f, 0.18f, 0.1f)));

        // ── Floor debris / rubble near walls ──
        for (int d = 0; d < 15; d++)
        {
            float dx = Random.value < 0.5f ? Random.Range(-5.5f, -4f) : Random.Range(4f, 5.5f);
            float dz = Random.Range(-2f, 5.5f);
            float ds = Random.Range(0.06f, 0.15f);
            MakePart(root, PrimitiveType.Cube,
                new Vector3(dx, ds * 0.4f, dz),
                new Vector3(ds, ds * 0.6f, ds * Random.Range(0.7f, 1.3f)),
                ShaderCache.NewStone(WallColor * Random.Range(0.5f, 0.9f)))
                .transform.rotation = Random.rotation;
        }

        // ── Floating rune particles (subtle accent, not overwhelming) ──
        for (int i = 0; i < 20; i++)
        {
            var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(p.GetComponent<BoxCollider>());
            p.name = "RuneParticle";
            p.transform.parent = root;
            float sz = Random.Range(0.04f, 0.12f);
            p.transform.localScale = new Vector3(sz, sz, sz);
            p.transform.rotation = Random.rotation;
            p.transform.position = new Vector3(
                Random.Range(-5.5f, 5.5f),
                Random.Range(0.3f, 4.5f),
                Random.Range(-2f, 6.5f)
            );
            Color c = ElemColors[Random.Range(0, ElemColors.Length)];
            p.GetComponent<Renderer>().material = ShaderCache.NewEmissive(c, Random.Range(3f, 7f));
            _runeParticles.Add(p.transform);
            _particleSpeeds.Add(Random.Range(0.12f, 0.45f));
        }
    }

    static GameObject MakePart(Transform parent, PrimitiveType type, Vector3 localPos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        // Remove all default colliders
        foreach (var c in go.GetComponents<Collider>()) Object.Destroy(c);
        go.transform.parent = parent;
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().material = mat;
        return go;
    }

    void BuildWall(Transform parent, Vector3 pos, Vector3 scale)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.Destroy(wall.GetComponent<BoxCollider>());
        wall.name = "Wall";
        wall.transform.parent = parent;
        wall.transform.position = pos;
        wall.transform.localScale = scale;
        wall.GetComponent<Renderer>().material = ShaderCache.NewStone(WallColor);
    }

    // ─── UI OVERLAY ───────────────────────────────────────────────

    void BuildUI()
    {
        _uiDoc = gameObject.AddComponent<UIDocument>();
        var ps = Resources.Load<PanelSettings>("UI/DefaultPanelSettings");
        if (ps == null)
        {
            ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1920, 1080);
            ps.themeStyleSheet = ScriptableObject.CreateInstance<ThemeStyleSheet>();
        }
        _uiDoc.panelSettings = ps;

        var font = Font.CreateDynamicFontFromOSFont("Arial", 14);
        var root = _uiDoc.rootVisualElement;
        root.style.flexGrow = 1;
        root.style.unityFontDefinition = FontDefinition.FromFont(font);
        root.pickingMode = PickingMode.Ignore;

        var bg = new VisualElement();
        bg.style.flexGrow = 1;
        bg.style.justifyContent = Justify.Center;
        bg.style.alignItems = Align.Center;
        bg.pickingMode = PickingMode.Ignore;
        root.Add(bg);

        // Central panel — dark backdrop so UI reads clearly over 3D
        _container = new VisualElement();
        _container.style.alignItems = Align.Center;
        _container.style.opacity = 0f;
        _container.style.backgroundColor = new Color(0.02f, 0.02f, 0.05f, 0.88f);
        _container.style.paddingTop = 50;
        _container.style.paddingBottom = 44;
        _container.style.paddingLeft = 80;
        _container.style.paddingRight = 80;
        _container.style.borderTopLeftRadius = 8;
        _container.style.borderTopRightRadius = 8;
        _container.style.borderBottomLeftRadius = 8;
        _container.style.borderBottomRightRadius = 8;
        _container.style.borderTopWidth = 1;
        _container.style.borderBottomWidth = 1;
        _container.style.borderLeftWidth = 1;
        _container.style.borderRightWidth = 1;
        var panelBorder = new Color(0.4f, 0.2f, 0.8f, 0.3f); // subtle purple like rune circle
        _container.style.borderTopColor = panelBorder;
        _container.style.borderBottomColor = panelBorder;
        _container.style.borderLeftColor = panelBorder;
        _container.style.borderRightColor = panelBorder;
        bg.Add(_container);

        // Title
        _titleLabel = new Label("RUNEFORGE");
        _titleLabel.style.fontSize = 96;
        _titleLabel.style.color = new Color(1f, 0.85f, 0.3f);
        _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _titleLabel.style.letterSpacing = 16;
        _titleLabel.style.marginBottom = 32;
        _titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        _titleLabel.pickingMode = PickingMode.Ignore;
        _container.Add(_titleLabel);

        // Play button — large and prominent
        var playBtn = MakeButton("ENTER THE FORGE", new Color(1f, 0.75f, 0.15f), true, () =>
        {
            if (_onPlay == null) return;
            SFXSystem.Play(SFXSystem.SFXType.DoorOpen);
            var cb = _onPlay;
            _onPlay = null;
            cb.Invoke();
        });
        playBtn.style.marginBottom = 16;
        _container.Add(playBtn);

        var quitBtn = MakeButton("QUIT", new Color(0.45f, 0.45f, 0.5f), false, () =>
        {
            SFXSystem.Play(SFXSystem.SFXType.MenuClick);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        });
        _container.Add(quitBtn);

        var hint = new Label("Press ENTER to start");
        hint.style.fontSize = 15;
        hint.style.color = new Color(0.35f, 0.35f, 0.4f);
        hint.style.marginTop = 28;
        hint.style.unityTextAlign = TextAnchor.MiddleCenter;
        hint.name = "start-hint";
        hint.pickingMode = PickingMode.Ignore;
        _container.Add(hint);
    }

    VisualElement MakeButton(string text, Color color, bool primary, System.Action onClick)
    {
        var btn = new VisualElement();
        int padH = primary ? 52 : 32;
        int padV = primary ? 16 : 10;
        btn.style.paddingLeft = padH;
        btn.style.paddingRight = padH;
        btn.style.paddingTop = padV;
        btn.style.paddingBottom = padV;
        var bgNormal = primary
            ? new Color(color.r * 0.12f, color.g * 0.12f, color.b * 0.12f, 0.95f)
            : new Color(0.05f, 0.05f, 0.08f, 0.8f);
        btn.style.backgroundColor = bgNormal;
        btn.style.borderTopLeftRadius = primary ? 6 : 4;
        btn.style.borderTopRightRadius = primary ? 6 : 4;
        btn.style.borderBottomLeftRadius = primary ? 6 : 4;
        btn.style.borderBottomRightRadius = primary ? 6 : 4;
        int bw = primary ? 2 : 1;
        btn.style.borderTopWidth = bw;
        btn.style.borderBottomWidth = bw;
        btn.style.borderLeftWidth = bw;
        btn.style.borderRightWidth = bw;
        var dim = new Color(color.r * 0.5f, color.g * 0.5f, color.b * 0.5f, primary ? 0.7f : 0.4f);
        btn.style.borderTopColor = dim;
        btn.style.borderBottomColor = dim;
        btn.style.borderLeftColor = dim;
        btn.style.borderRightColor = dim;

        var label = new Label(text);
        label.style.fontSize = primary ? 28 : 18;
        label.style.color = color;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.letterSpacing = primary ? 8 : 4;
        label.style.unityTextAlign = TextAnchor.MiddleCenter;
        label.pickingMode = PickingMode.Ignore;
        btn.Add(label);

        var bgHover = new Color(color.r * 0.2f, color.g * 0.2f, color.b * 0.2f, 0.95f);
        btn.RegisterCallback<MouseEnterEvent>(_ =>
        {
            btn.style.backgroundColor = bgHover;
            btn.style.borderTopColor = color;
            btn.style.borderBottomColor = color;
            btn.style.borderLeftColor = color;
            btn.style.borderRightColor = color;
            SFXSystem.Play(SFXSystem.SFXType.UIHover);
        });
        btn.RegisterCallback<MouseLeaveEvent>(_ =>
        {
            btn.style.backgroundColor = bgNormal;
            btn.style.borderTopColor = dim;
            btn.style.borderBottomColor = dim;
            btn.style.borderLeftColor = dim;
            btn.style.borderRightColor = dim;
        });
        btn.RegisterCallback<ClickEvent>(_ => onClick?.Invoke());

        return btn;
    }

    // ─── ANIMATION ────────────────────────────────────────────────

    void Update()
    {
        _time += Time.unscaledDeltaTime;

        // UI fade in
        if (_fadingIn)
        {
            _fadeTimer += Time.unscaledDeltaTime;
            float ft = Mathf.Clamp01(_fadeTimer / 2f);
            _container.style.opacity = ft * ft * (3f - 2f * ft);
            if (ft >= 1f) _fadingIn = false;
        }

        // Title pulse
        float pulse = 0.85f + Mathf.Sin(_time * 1.8f) * 0.15f;
        _titleLabel.style.color = new Color(pulse, 0.85f * pulse, 0.3f);

        // Hint blink
        var hint = _uiDoc.rootVisualElement.Q<VisualElement>("start-hint");
        if (hint != null)
            hint.style.opacity = 0.25f + Mathf.Sin(_time * 2.5f) * 0.25f;

        // ── 3D Animations ──
        float dt = Time.unscaledDeltaTime;

        // ── Camera: slow orbit tracing forge → player → portal ──
        if (_camera != null)
        {
            float orbitAngle = Mathf.Sin(_time * 0.12f) * 12f;
            float orbitY = 7.5f + Mathf.Sin(_time * 0.18f) * 0.5f;
            float rad = orbitAngle * Mathf.Deg2Rad;
            float dist = 9.5f;
            _camera.position = new Vector3(
                Mathf.Sin(rad) * dist,
                orbitY,
                2f - Mathf.Cos(rad) * dist
            );
            // Look target shifts slightly with orbit — follows forge area
            float lookX = Mathf.Sin(rad) * 0.5f;
            _camera.LookAt(new Vector3(lookX, 1f, 3.5f));
        }

        // ── Rune circle ──
        if (_runeCircle != null)
            _runeCircle.Rotate(Vector3.up, 10f * dt);

        // ── Portal beam ──
        if (_portalBeam != null)
        {
            float beamScale = 0.3f + Mathf.Sin(_time * 2f) * 0.06f;
            float beamH = 2.5f + Mathf.Sin(_time * 1.5f) * 0.3f;
            _portalBeam.localScale = new Vector3(beamScale, beamH, beamScale);
            _portalBeam.Rotate(Vector3.up, 40f * dt);
        }

        // ── Lights — irregular fire flicker ──
        if (_portalLight != null)
            _portalLight.intensity = 2f + Mathf.Sin(_time * 3.5f) * 0.7f + Mathf.Sin(_time * 7f) * 0.3f;
        if (_forgeLight != null)
            _forgeLight.intensity = 3f + Mathf.Sin(_time * 5f) * 0.8f + Mathf.Sin(_time * 13f) * 0.4f;
        if (_pitLight != null)
            _pitLight.intensity = 1.5f + Mathf.Sin(_time * 6f) * 0.4f + Mathf.Sin(_time * 11f) * 0.2f;

        // ── Anvil glow + hot metal pulse (the beating heart of the forge) ──
        if (_anvilGlow != null)
        {
            float glowPulse = 1f + Mathf.Sin(_time * 2f) * 0.15f;
            _anvilGlow.localScale = new Vector3(0.5f * glowPulse, 0.015f, 0.3f * glowPulse);
        }
        if (_hotMetal != null)
        {
            // Pulse between orange-hot and yellow-white
            float heat = 0.5f + Mathf.Sin(_time * 1.5f) * 0.5f;
            Color hotCol = Color.Lerp(new Color(0.8f, 0.25f, 0.05f), new Color(1f, 0.7f, 0.2f), heat);
            _hotMetal.GetComponent<Renderer>().material.SetColor("_EmissionColor", hotCol * (5f + heat * 3f));
        }

        // ── Bellows breathing animation ──
        if (_bellows != null)
        {
            float breathe = Mathf.Sin(_time * 1f) * 0.03f;
            var bp = _bellows.localPosition;
            bp.y = 0.3f + breathe;
            _bellows.localPosition = bp;
        }

        // ── Torch flames — lively bob ──
        for (int i = 0; i < _torchFlames.Count; i++)
        {
            var f = _torchFlames[i];
            if (f == null) continue;
            float bob = Mathf.Sin(_time * 3.5f + i * 1.3f) * 0.06f
                      + Mathf.Sin(_time * 7f + i * 2.1f) * 0.02f;
            var pos = f.localPosition;
            pos.y = f.parent != null && f.parent.name.StartsWith("Brazier") ? 1.08f + bob : 1.8f + bob * 0.5f;
            f.localPosition = pos;
            float s = (f.parent != null && f.parent.name.StartsWith("Brazier") ? 0.2f : 0.12f)
                    + Mathf.Sin(_time * 5f + i * 0.9f) * 0.04f;
            f.localScale = Vector3.one * s;
        }

        // ── Player idle ──
        if (_playerModel != null)
        {
            float breathe = Mathf.Sin(_time * 1.2f) * 0.02f;
            float sway = Mathf.Sin(_time * 0.7f) * 1.5f;
            _playerModel.position = new Vector3(0, breathe, 1f);
            _playerModel.rotation = Quaternion.Euler(0, sway, 0);
        }
        if (_staffTip != null)
        {
            float tipScale = 0.4f + Mathf.Sin(_time * 2.5f) * 0.08f;
            _staffTip.localScale = new Vector3(2.5f, tipScale, 2.5f);
        }

        // ── Chains swaying ──
        for (int i = 0; i < _chains.Count; i++)
        {
            if (_chains[i] == null) continue;
            float swing = Mathf.Sin(_time * 0.8f + i * 1.7f) * 3f;
            _chains[i].localRotation = Quaternion.Euler(swing, 0, swing * 0.5f);
        }

        // ── Crystals gentle hover/bob ──
        for (int i = 0; i < _crystals.Count; i++)
        {
            if (_crystals[i] == null) continue;
            var cp = _crystals[i].localPosition;
            cp.y += Mathf.Sin(_time * 1.5f + i * 1.2f) * 0.002f;
            _crystals[i].localPosition = cp;
            _crystals[i].Rotate(Vector3.up * 15f * dt);
        }

        // ── Forge sparks from hot metal ──
        if (_time > 1f && Random.value < 4f * dt)
        {
            var spark = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(spark.GetComponent<BoxCollider>());
            spark.name = "ForgeSpark";
            spark.transform.parent = _sceneRoot.transform;
            float sz = Random.Range(0.03f, 0.07f);
            spark.transform.localScale = new Vector3(sz, sz, sz);
            spark.transform.position = new Vector3(
                Random.Range(-0.2f, 0.2f), 1.1f, 4.5f + Random.Range(-0.15f, 0.15f));
            spark.transform.rotation = Random.rotation;
            Color sparkCol = Color.Lerp(ForgeOrange, new Color(1f, 0.9f, 0.3f), Random.value);
            spark.GetComponent<Renderer>().material = ShaderCache.NewEmissive(sparkCol, Random.Range(5f, 10f));
            _forgeSparks.Add(spark.transform);
            _sparkVelocities.Add(new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(2f, 5f), Random.Range(-1f, 1f)));
            _sparkLife.Add(0f);
        }

        for (int i = _forgeSparks.Count - 1; i >= 0; i--)
        {
            if (_forgeSparks[i] == null) { RemoveSpark(i); continue; }
            _sparkLife[i] += dt;
            if (_sparkLife[i] > 0.8f) { Object.Destroy(_forgeSparks[i].gameObject); RemoveSpark(i); continue; }
            var vel = _sparkVelocities[i];
            vel.y -= 5f * dt;
            _sparkVelocities[i] = vel;
            _forgeSparks[i].position += vel * dt;
            _forgeSparks[i].Rotate(Vector3.one * 200f * dt);
            float life01 = _sparkLife[i] / 0.8f;
            _forgeSparks[i].localScale = Vector3.one * Mathf.Max((1f - life01) * 0.06f, 0.01f);
        }

        // ── Smoke wisps rising from coal pit ──
        if (_time > 1.5f && Random.value < 1.5f * dt)
        {
            var smoke = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(smoke.GetComponent<SphereCollider>());
            smoke.name = "Smoke";
            smoke.transform.parent = _sceneRoot.transform;
            smoke.transform.position = new Vector3(1.2f + Random.Range(-0.2f, 0.2f), 0.3f, 4.8f + Random.Range(-0.2f, 0.2f));
            smoke.transform.localScale = Vector3.one * 0.15f;
            var smokeMat = ShaderCache.NewEmissive(new Color(0.15f, 0.12f, 0.1f), 0.3f);
            smokeMat.SetColor("_BaseColor", new Color(0.15f, 0.12f, 0.1f, 0.3f));
            smoke.GetComponent<Renderer>().material = smokeMat;
            _smokeWisps.Add(smoke.transform);
            _smokeLife.Add(0f);
        }

        for (int i = _smokeWisps.Count - 1; i >= 0; i--)
        {
            if (_smokeWisps[i] == null) { _smokeWisps.RemoveAt(i); _smokeLife.RemoveAt(i); continue; }
            _smokeLife[i] += dt;
            float sl = _smokeLife[i];
            if (sl > 3f) { Object.Destroy(_smokeWisps[i].gameObject); _smokeWisps.RemoveAt(i); _smokeLife.RemoveAt(i); continue; }
            float t01 = sl / 3f;
            _smokeWisps[i].position += new Vector3(Mathf.Sin(sl * 0.7f + i) * 0.003f, 0.4f * dt, 0);
            float smokeScale = 0.15f + t01 * 0.3f; // expand as it rises
            _smokeWisps[i].localScale = Vector3.one * smokeScale;
        }

        // ── Rune particles ──
        for (int i = 0; i < _runeParticles.Count; i++)
        {
            var p = _runeParticles[i];
            if (p == null) continue;
            float speed = _particleSpeeds[i];
            float swayX = Mathf.Sin(_time * 0.5f + i * 0.4f) * 0.006f;
            float swayZ = Mathf.Cos(_time * 0.3f + i * 0.7f) * 0.004f;
            p.position += new Vector3(swayX, speed * dt, swayZ);
            p.Rotate(Vector3.one * 25f * dt);
            if (p.position.y > 5.5f)
                p.position = new Vector3(Random.Range(-5f, 5f), Random.Range(0.2f, 0.6f), Random.Range(-1f, 6f));
        }

        // Enter key
        if (_onPlay != null && UnityEngine.InputSystem.Keyboard.current != null &&
            UnityEngine.InputSystem.Keyboard.current.enterKey.wasPressedThisFrame)
        {
            SFXSystem.Play(SFXSystem.SFXType.DoorOpen);
            var cb = _onPlay;
            _onPlay = null;
            cb.Invoke();
        }
    }

    void RemoveSpark(int i)
    {
        _forgeSparks.RemoveAt(i);
        _sparkVelocities.RemoveAt(i);
        _sparkLife.RemoveAt(i);
    }

    void OnDestroy()
    {
        if (_sceneRoot != null)
            Object.Destroy(_sceneRoot);
    }
}
