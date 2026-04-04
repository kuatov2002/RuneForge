using UnityEngine;

/// <summary>
/// Builds the hub environment procedurally — a cozy sanctum between runs.
/// Layout: central hall with NPC stations around it.
/// Decorations scale with meta-progression (total essence spent).
/// </summary>
public static class HubBuilder
{
    public static GameObject Build()
    {
        var hub = new GameObject("Hub");

        // -- Floor (large circular-ish area) --
        int size = 20;
        var (wallCol, floorCol, floorAltCol, pillarCol) = (
            new Color(0.25f, 0.2f, 0.3f),
            new Color(0.12f, 0.1f, 0.15f),
            new Color(0.15f, 0.12f, 0.18f),
            new Color(0.3f, 0.25f, 0.35f)
        );

        // Floor tiles
        for (int x = 0; x < size; x++)
        {
            for (int z = 0; z < size; z++)
            {
                // Octagonal shape — skip far corners
                float cx = x - size / 2f + 0.5f;
                float cz = z - size / 2f + 0.5f;
                if (Mathf.Abs(cx) + Mathf.Abs(cz) > size * 0.65f) continue;

                var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tile.name = "FloorTile";
                tile.transform.parent = hub.transform;
                tile.transform.position = new Vector3(x + 0.5f, -0.25f, z + 0.5f);
                tile.transform.localScale = new Vector3(0.98f, 0.5f, 0.98f);
                tile.GetComponent<Renderer>().material = ShaderCache.NewFloor(floorCol, floorAltCol);
                tile.isStatic = true;
            }
        }

        // Walls (octagonal perimeter)
        float half = size / 2f;
        CreateHubWalls(hub.transform, size, wallCol);

        // Center: Runic circle (decorative floor marker)
        var circle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(circle.GetComponent<CapsuleCollider>());
        circle.name = "RuneCircle";
        circle.transform.parent = hub.transform;
        circle.transform.position = new Vector3(half, -0.01f, half);
        circle.transform.localScale = new Vector3(4f, 0.02f, 4f);
        circle.GetComponent<Renderer>().material = ShaderCache.NewMagic(new Color(0.4f, 0.2f, 0.8f), 2f);

        // -- NPC Stations --

        // 1. Forge Master (upgrades) — north
        CreateNPCStation(hub.transform, new Vector3(half, 0, size - 3),
            "Forge Master", new Color(0.9f, 0.4f, 0.1f), 1.3f);

        // 2. Element Scholar (element unlocks) — east
        CreateNPCStation(hub.transform, new Vector3(size - 3, 0, half),
            "Element Scholar", new Color(0.3f, 0.5f, 0.9f), 1.1f);

        // 3. Portal (start run) — south
        CreateRunPortal(hub.transform, new Vector3(half, 0, 3));

        // 4. Trophy / Stats pillar — west
        CreateNPCStation(hub.transform, new Vector3(3, 0, half),
            "Chronicle", new Color(0.7f, 0.7f, 0.5f), 1.0f);

        // 5. Synergy Codex — northwest
        CreateNPCStation(hub.transform, new Vector3(5, 0, size - 5),
            "Codex", new Color(0.6f, 0.3f, 0.9f), 1.0f);

        // -- Decorations --

        // Corner pillars
        float pOff = 2f;
        CreateHubPillar(hub.transform, new Vector3(pOff, 0, pOff), pillarCol);
        CreateHubPillar(hub.transform, new Vector3(size - pOff, 0, pOff), pillarCol);
        CreateHubPillar(hub.transform, new Vector3(pOff, 0, size - pOff), pillarCol);
        CreateHubPillar(hub.transform, new Vector3(size - pOff, 0, size - pOff), pillarCol);

        // Ambient torches
        CreateHubTorch(hub.transform, new Vector3(4, 1.8f, 4), new Color(0.8f, 0.4f, 1f));
        CreateHubTorch(hub.transform, new Vector3(size - 4, 1.8f, 4), new Color(0.8f, 0.4f, 1f));
        CreateHubTorch(hub.transform, new Vector3(4, 1.8f, size - 4), new Color(0.4f, 0.6f, 1f));
        CreateHubTorch(hub.transform, new Vector3(size - 4, 1.8f, size - 4), new Color(0.4f, 0.6f, 1f));

        // Center chandelier light
        var centerLight = new GameObject("CenterLight");
        centerLight.transform.parent = hub.transform;
        centerLight.transform.position = new Vector3(half, 4f, half);
        var cl = centerLight.AddComponent<Light>();
        cl.type = LightType.Point;
        cl.color = new Color(0.9f, 0.8f, 1f);
        cl.intensity = 3f;
        cl.range = 16f;

        // -- Progression-based decorations --
        AddProgressDecoration(hub.transform, half);
        BuildStatsBoard(hub.transform);

        return hub;
    }

    /// <summary>
    /// Places torches and rune pillars around the hub proportional to total essence spent.
    /// More spending = more decorations, up to 20.
    /// </summary>
    static void AddProgressDecoration(Transform parent, float hubCenter)
    {
        int totalSpent = MetaProgression.TotalEssenceSpent;
        int decorCount = Mathf.Min(totalSpent / 50, 20);

        for (int i = 0; i < decorCount; i++)
        {
            float angle = i * (360f / Mathf.Max(1, decorCount)) * Mathf.Deg2Rad;
            float radius = 6f + (i % 3) * 2f;
            Vector3 pos = new Vector3(
                hubCenter + Mathf.Cos(angle) * radius,
                0,
                hubCenter + Mathf.Sin(angle) * radius
            );

            if (i % 2 == 0)
            {
                // Torch pedestal
                var torch = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                torch.name = "ProgressTorch";
                torch.transform.parent = parent;
                torch.transform.position = pos + Vector3.up * 0.75f;
                torch.transform.localScale = new Vector3(0.15f, 0.75f, 0.15f);
                torch.GetComponent<Renderer>().material = ShaderCache.NewLit(new Color(0.3f, 0.25f, 0.2f));
                torch.isStatic = true;

                // Flame sphere
                var flame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.Destroy(flame.GetComponent<SphereCollider>());
                flame.name = "TorchFlame";
                flame.transform.parent = parent;
                flame.transform.position = pos + Vector3.up * 1.6f;
                flame.transform.localScale = Vector3.one * 0.2f;
                flame.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(1f, 0.5f, 0.1f), 3f);
            }
            else
            {
                // Rune pillar with color based on progression tier
                var pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pillar.name = "ProgressPillar";
                pillar.transform.parent = parent;
                pillar.transform.position = pos + Vector3.up * 0.5f;
                pillar.transform.localScale = new Vector3(0.4f, 1f, 0.4f);
                pillar.isStatic = true;

                Color pillarColor;
                if (totalSpent > 500)
                    pillarColor = new Color(0.4f, 0.2f, 0.6f);
                else if (totalSpent > 200)
                    pillarColor = new Color(0.2f, 0.3f, 0.5f);
                else
                    pillarColor = new Color(0.35f, 0.3f, 0.25f);

                pillar.GetComponent<Renderer>().material = ShaderCache.NewEmissive(pillarColor, 1.5f);
            }
        }
    }

    /// <summary>
    /// Creates a stats board displaying lifetime run statistics.
    /// Placed near the hub center on the east side.
    /// </summary>
    static void BuildStatsBoard(Transform parent)
    {
        Vector3 boardPos = new Vector3(14f, 1.5f, 10f);

        var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
        board.name = "StatsBoard";
        board.transform.parent = parent;
        board.transform.position = boardPos;
        board.transform.localScale = new Vector3(2f, 1.5f, 0.1f);
        board.GetComponent<Renderer>().material = ShaderCache.NewLit(new Color(0.2f, 0.15f, 0.1f));
        board.isStatic = true;

        var textObj = new GameObject("StatsBoardText");
        textObj.transform.parent = parent;
        textObj.transform.position = boardPos + Vector3.forward * -0.06f;
        var tm = textObj.AddComponent<TextMesh>();
        tm.text = $"Runs: {MetaProgression.RunsCompleted}\n" +
                  $"Best Floor: {MetaProgression.BestFloor}\n" +
                  $"Total Kills: {MetaProgression.TotalKills}";
        tm.fontSize = 24;
        tm.characterSize = 0.05f;
        tm.color = new Color(0.8f, 0.7f, 0.5f);
        tm.alignment = TextAlignment.Center;
        tm.anchor = TextAnchor.MiddleCenter;
    }

    static void CreateHubWalls(Transform parent, int size, Color wallCol)
    {
        float half = size / 2f;
        float wh = 3f;
        var mat = ShaderCache.NewStone(wallCol);

        // 4 main walls
        CreateWall(parent, new Vector3(half, wh / 2, size + 0.25f), new Vector3(size + 1, wh, 0.5f), mat);
        CreateWall(parent, new Vector3(half, wh / 2, -0.25f), new Vector3(size + 1, wh, 0.5f), mat);
        CreateWall(parent, new Vector3(size + 0.25f, wh / 2, half), new Vector3(0.5f, wh, size), mat);
        CreateWall(parent, new Vector3(-0.25f, wh / 2, half), new Vector3(0.5f, wh, size), mat);
    }

    static void CreateWall(Transform parent, Vector3 pos, Vector3 scale, Material mat)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "HubWall";
        wall.transform.parent = parent;
        wall.transform.position = pos;
        wall.transform.localScale = scale;
        wall.GetComponent<Renderer>().material = mat;
        wall.isStatic = true;
    }

    static void CreateNPCStation(Transform parent, Vector3 pos, string npcName, Color color, float bodyScale)
    {
        var station = new GameObject(npcName);
        station.transform.parent = parent;
        station.transform.position = pos;

        // NPC body
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        Object.Destroy(body.GetComponent<CapsuleCollider>());
        body.transform.parent = station.transform;
        body.transform.localPosition = new Vector3(0, 0.5f * bodyScale, 0);
        body.transform.localScale = new Vector3(0.5f * bodyScale, 0.35f * bodyScale, 0.45f * bodyScale);
        body.GetComponent<Renderer>().material = ShaderCache.NewLit(color);

        // Head
        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        Object.Destroy(head.GetComponent<SphereCollider>());
        head.transform.parent = station.transform;
        head.transform.localPosition = new Vector3(0, 1f * bodyScale, 0);
        head.transform.localScale = Vector3.one * 0.35f * bodyScale;
        head.GetComponent<Renderer>().material = ShaderCache.NewSkin(new Color(0.85f, 0.7f, 0.55f));

        // Eyes
        CreateEye(station.transform, new Vector3(-0.08f * bodyScale, 1.05f * bodyScale, 0.15f * bodyScale), color);
        CreateEye(station.transform, new Vector3(0.08f * bodyScale, 1.05f * bodyScale, 0.15f * bodyScale), color);

        // Name plate (floating label glow)
        var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.Destroy(plate.GetComponent<BoxCollider>());
        plate.name = "NamePlate";
        plate.transform.parent = station.transform;
        plate.transform.localPosition = new Vector3(0, 1.6f * bodyScale, 0);
        plate.transform.localScale = new Vector3(1.2f, 0.08f, 0.08f);
        plate.GetComponent<Renderer>().material = ShaderCache.NewEmissive(color, 2f);

        // Interaction trigger
        var col = station.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 2f;
        col.center = new Vector3(0, 0.5f, 0);

        var interact = station.AddComponent<HubInteractable>();
        interact.stationId = npcName;
        interact.stationColor = color;
    }

    static void CreateRunPortal(Transform parent, Vector3 pos)
    {
        var portal = new GameObject("RunPortal");
        portal.transform.parent = parent;
        portal.transform.position = pos;

        // Base ring
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(ring.GetComponent<CapsuleCollider>());
        ring.name = "PortalRing";
        ring.transform.parent = portal.transform;
        ring.transform.localPosition = Vector3.up * 0.05f;
        ring.transform.localScale = new Vector3(2.5f, 0.05f, 2.5f);
        ring.GetComponent<Renderer>().material = ShaderCache.NewMagic(new Color(0.2f, 0.8f, 0.4f), 4f);

        // Glowing pillar of light
        var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(beam.GetComponent<CapsuleCollider>());
        beam.name = "PortalBeam";
        beam.transform.parent = portal.transform;
        beam.transform.localPosition = Vector3.up * 2f;
        beam.transform.localScale = new Vector3(0.3f, 2f, 0.3f);
        beam.GetComponent<Renderer>().material = ShaderCache.NewMagic(new Color(0.2f, 1f, 0.5f), 4f);

        // Point light
        var light = new GameObject("PortalLight");
        light.transform.parent = portal.transform;
        light.transform.localPosition = Vector3.up * 1f;
        var pl = light.AddComponent<Light>();
        pl.type = LightType.Point;
        pl.color = new Color(0.3f, 1f, 0.5f);
        pl.intensity = 3f;
        pl.range = 6f;

        // Trigger
        var col = portal.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 1.5f;
        col.center = Vector3.up * 0.5f;

        var interact = portal.AddComponent<HubInteractable>();
        interact.stationId = "RunPortal";
        interact.stationColor = new Color(0.2f, 0.9f, 0.4f);
    }

    static void CreateHubPillar(Transform parent, Vector3 pos, Color col)
    {
        var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pillar.name = "HubPillar";
        pillar.transform.parent = parent;
        pillar.transform.position = pos + Vector3.up * 1.5f;
        pillar.transform.localScale = new Vector3(0.6f, 1.5f, 0.6f);
        pillar.GetComponent<Renderer>().material = ShaderCache.NewStone(col);
        pillar.isStatic = true;
    }

    static void CreateHubTorch(Transform parent, Vector3 pos, Color col)
    {
        var torch = new GameObject("HubTorch");
        torch.transform.parent = parent;
        torch.transform.position = pos;
        var light = torch.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = col;
        light.intensity = 2f;
        light.range = 5f;
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(sphere.GetComponent<SphereCollider>());
        sphere.transform.parent = torch.transform;
        sphere.transform.localScale = Vector3.one * 0.12f;
        sphere.GetComponent<Renderer>().material = ShaderCache.NewFire(col);
    }

    static void CreateEye(Transform parent, Vector3 localPos, Color color)
    {
        var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        eye.name = "Eye";
        Object.Destroy(eye.GetComponent<SphereCollider>());
        eye.transform.parent = parent;
        eye.transform.localPosition = localPos;
        eye.transform.localScale = Vector3.one * 0.06f;
        eye.GetComponent<Renderer>().material = ShaderCache.NewMagic(color, 3f);
    }
}
