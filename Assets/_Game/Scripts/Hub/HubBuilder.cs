using UnityEngine;

/// <summary>
/// Builds the hub environment procedurally — a forge workshop sanctum between runs.
/// Themed stations replace generic NPCs to match the title screen aesthetic.
/// Decorations scale with meta-progression (total essence spent).
/// </summary>
public static class HubBuilder
{
    static readonly Color WallCol = new(0.25f, 0.2f, 0.3f);
    static readonly Color FloorCol = new(0.12f, 0.1f, 0.15f);
    static readonly Color FloorAlt = new(0.15f, 0.12f, 0.18f);
    static readonly Color PillarCol = new(0.3f, 0.25f, 0.35f);
    static readonly Color DarkIron = new(0.22f, 0.22f, 0.25f);
    static readonly Color WoodDark = new(0.2f, 0.14f, 0.08f);
    static readonly Color WoodLight = new(0.25f, 0.18f, 0.1f);
    static readonly Color ForgeOrange = new(0.9f, 0.4f, 0.1f);
    static readonly Color RunePurple = new(0.4f, 0.2f, 0.8f);

    static readonly Color[] ElemColors = {
        new(1f, 0.4f, 0.1f),   new(0.3f, 0.7f, 1f),
        new(0.6f, 0.4f, 0.2f), new(0.8f, 0.9f, 1f),
        new(1f, 1f, 0.3f),     new(0.2f, 0.9f, 0.1f),
        new(0.6f, 0.1f, 0.9f),
    };

    public static GameObject Build()
    {
        var hub = new GameObject("Hub");
        int size = 20;
        float half = size / 2f;

        // ── Floor tiles ──
        for (int x = 0; x < size; x++)
        for (int z = 0; z < size; z++)
        {
            float cx = x - half + 0.5f;
            float cz = z - half + 0.5f;
            if (Mathf.Abs(cx) + Mathf.Abs(cz) > size * 0.65f) continue;

            var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.name = "FloorTile";
            tile.transform.parent = hub.transform;
            tile.transform.position = new Vector3(x + 0.5f, -0.25f, z + 0.5f);
            tile.transform.localScale = new Vector3(0.98f, 0.5f, 0.98f);
            tile.GetComponent<Renderer>().material = ShaderCache.NewFloor(FloorCol, FloorAlt);
            tile.isStatic = true;
        }

        // ── Walls ──
        CreateHubWalls(hub.transform, size);

        // ── Central rune circle ──
        var circle = P(hub.transform, PrimitiveType.Cylinder, new Vector3(half, -0.01f, half),
            new Vector3(4f, 0.02f, 4f), ShaderCache.NewMagic(RunePurple, 2f));
        // Inner circle
        P(hub.transform, PrimitiveType.Cylinder, new Vector3(half, 0f, half),
            new Vector3(2.5f, 0.02f, 2.5f), ShaderCache.NewEmissive(RunePurple, 1.2f));
        // Rune light
        AddLight(hub.transform, new Vector3(half, 0.5f, half), RunePurple, 1.5f, 5f);

        // ── Floor rune lines radiating from center ──
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI * 2f / 8f;
            float len = 5f;
            float lx = half + Mathf.Sin(a) * len * 0.5f;
            float lz = half + Mathf.Cos(a) * len * 0.5f;
            var line = P(hub.transform, PrimitiveType.Cube, new Vector3(lx, 0.015f, lz),
                new Vector3(0.06f, 0.01f, len), ShaderCache.NewEmissive(RunePurple * 0.7f, 1f));
            line.transform.rotation = Quaternion.Euler(0, -i * 45f, 0);
        }

        // ── Themed Stations ──

        // 1. Forge Master (upgrades) — north
        CreateForgeStation(hub.transform, new Vector3(half, 0, size - 3));

        // 2. Element Scholar (element unlocks) — east
        CreateElementAltar(hub.transform, new Vector3(size - 3, 0, half));

        // 3. Portal (start run) — south (unchanged)
        CreateRunPortal(hub.transform, new Vector3(half, 0, 3));

        // 4. Chronicle (stats) — west
        CreateChronicleDesk(hub.transform, new Vector3(3, 0, half));

        // 5. Codex (discoveries) — northwest
        CreateCodexShelf(hub.transform, new Vector3(5, 0, size - 5));

        // ── Detailed corner pillars ──
        float pOff = 2f;
        Vector3[] pillarPositions = {
            new(pOff, 0, pOff), new(size - pOff, 0, pOff),
            new(pOff, 0, size - pOff), new(size - pOff, 0, size - pOff)
        };
        foreach (var pp in pillarPositions)
        {
            P(hub.transform, PrimitiveType.Cylinder, pp + Vector3.up * 0.05f,
                new Vector3(0.8f, 0.05f, 0.8f), ShaderCache.NewStone(PillarCol * 0.8f));
            P(hub.transform, PrimitiveType.Cylinder, pp + Vector3.up * 1.5f,
                new Vector3(0.5f, 1.5f, 0.5f), ShaderCache.NewStone(PillarCol));
            P(hub.transform, PrimitiveType.Cylinder, pp + Vector3.up * 2.95f,
                new Vector3(0.65f, 0.08f, 0.65f), ShaderCache.NewStone(PillarCol));
            P(hub.transform, PrimitiveType.Cylinder, pp + Vector3.up * 1.5f,
                new Vector3(0.55f, 0.03f, 0.55f), ShaderCache.NewEmissive(RunePurple, 1.5f));
        }

        // ── Brazier-style torches ──
        Vector3[] torchPos = {
            new(4, 0, 4), new(size - 4, 0, 4),
            new(4, 0, size - 4), new(size - 4, 0, size - 4)
        };
        Color[] torchCols = {
            new(0.8f, 0.4f, 1f), new(0.8f, 0.4f, 1f),
            new(0.4f, 0.6f, 1f), new(0.4f, 0.6f, 1f)
        };
        for (int i = 0; i < torchPos.Length; i++)
            CreateBrazier(hub.transform, torchPos[i], torchCols[i]);

        // ── Wall sconces ──
        for (int side = 0; side < 4; side++)
        {
            for (int j = 0; j < 3; j++)
            {
                float t = (j + 1f) / 4f * size;
                Vector3 sp = side switch
                {
                    0 => new Vector3(t, 1.6f, 0.3f),        // south wall
                    1 => new Vector3(t, 1.6f, size - 0.3f),  // north wall
                    2 => new Vector3(0.3f, 1.6f, t),          // west wall
                    _ => new Vector3(size - 0.3f, 1.6f, t),   // east wall
                };
                // Skip if too close to a station
                float distToCenter = Mathf.Abs(sp.x - half) + Mathf.Abs(sp.z - half);
                if (distToCenter > size * 0.65f) continue;

                P(hub.transform, PrimitiveType.Cube, sp,
                    new Vector3(0.12f, 0.08f, 0.12f), ShaderCache.NewMetal(DarkIron));
                P(hub.transform, PrimitiveType.Sphere, sp + Vector3.up * 0.15f,
                    Vector3.one * 0.08f, ShaderCache.NewEmissive(ForgeOrange * 0.7f, 2.5f));
            }
        }

        // ── Scattered props ──
        // Crate stacks
        P(hub.transform, PrimitiveType.Cube, new Vector3(3, 0.25f, 6),
            new Vector3(0.6f, 0.5f, 0.5f), ShaderCache.NewLit(WoodLight));
        P(hub.transform, PrimitiveType.Cube, new Vector3(3.3f, 0.55f, 6.1f),
            new Vector3(0.4f, 0.3f, 0.4f), ShaderCache.NewLit(WoodDark));

        // Barrels near forge
        CreateBarrel(hub.transform, new Vector3(half + 2.5f, 0, size - 2.5f));
        CreateBarrel(hub.transform, new Vector3(half + 2.8f, 0, size - 3f), 0.8f);

        // Weapon rack between forge and codex
        CreateWeaponRack(hub.transform, new Vector3(2, 0, size - 2));

        // Book piles
        for (int bp = 0; bp < 3; bp++)
            P(hub.transform, PrimitiveType.Cube, new Vector3(2.5f + bp * 0.15f, 0.05f + bp * 0.07f, half + 2),
                new Vector3(0.22f, 0.05f, 0.16f),
                ShaderCache.NewLit(new Color(0.15f + bp * 0.04f, 0.08f, 0.12f + bp * 0.05f)));

        // Rug near center
        P(hub.transform, PrimitiveType.Cube, new Vector3(half, 0.012f, half),
            new Vector3(3.5f, 0.015f, 3.5f), ShaderCache.NewLit(new Color(0.18f, 0.08f, 0.06f)));

        // Rune stones on floor
        for (int rs = 0; rs < 8; rs++)
        {
            float ra = Random.Range(0f, Mathf.PI * 2f);
            float rd = Random.Range(3f, 5f);
            P(hub.transform, PrimitiveType.Cube,
                new Vector3(half + Mathf.Sin(ra) * rd, 0.06f, half + Mathf.Cos(ra) * rd),
                Vector3.one * 0.1f * Random.Range(0.6f, 1.3f),
                ShaderCache.NewEmissive(RunePurple * 0.5f, Random.Range(0.5f, 1.5f)))
                .transform.rotation = Random.rotation;
        }

        // Center light
        AddLight(hub.transform, new Vector3(half, 4f, half), new Color(0.9f, 0.8f, 1f), 3f, 16f);

        // Progression decorations
        AddProgressDecoration(hub.transform, half);
        BuildStatsBoard(hub.transform);

        return hub;
    }

    // ─── THEMED STATIONS ──────────────────────────────────────────

    static void CreateForgeStation(Transform parent, Vector3 pos)
    {
        var station = new GameObject("Forge Master");
        station.transform.parent = parent;
        station.transform.position = pos;

        var stoneBase = new Color(0.18f, 0.15f, 0.2f);

        // Anvil pedestal
        P(station.transform, PrimitiveType.Cube, new Vector3(0, 0.2f, 0),
            new Vector3(0.9f, 0.4f, 0.7f), ShaderCache.NewStone(stoneBase));
        P(station.transform, PrimitiveType.Cube, new Vector3(0, 0.45f, 0),
            new Vector3(0.7f, 0.1f, 0.55f), ShaderCache.NewStone(stoneBase * 1.1f));
        // Anvil body + face
        P(station.transform, PrimitiveType.Cube, new Vector3(0, 0.65f, 0),
            new Vector3(0.5f, 0.3f, 0.45f), ShaderCache.NewMetal(DarkIron));
        P(station.transform, PrimitiveType.Cube, new Vector3(0, 0.82f, 0),
            new Vector3(0.9f, 0.05f, 0.55f), ShaderCache.NewMetal(new Color(0.3f, 0.3f, 0.33f)));
        // Horn + heel
        P(station.transform, PrimitiveType.Cube, new Vector3(0.55f, 0.75f, 0),
            new Vector3(0.3f, 0.12f, 0.25f), ShaderCache.NewMetal(DarkIron));
        P(station.transform, PrimitiveType.Sphere, new Vector3(0.72f, 0.75f, 0),
            new Vector3(0.12f, 0.1f, 0.15f), ShaderCache.NewMetal(DarkIron));
        P(station.transform, PrimitiveType.Cube, new Vector3(-0.4f, 0.72f, 0),
            new Vector3(0.15f, 0.18f, 0.45f), ShaderCache.NewMetal(DarkIron));
        // Rune glow + side runes
        P(station.transform, PrimitiveType.Cube, new Vector3(0, 0.86f, 0),
            new Vector3(0.5f, 0.015f, 0.3f), ShaderCache.NewEmissive(ForgeOrange, 5f));
        P(station.transform, PrimitiveType.Cube, new Vector3(0, 0.65f, 0.24f),
            new Vector3(0.35f, 0.12f, 0.01f), ShaderCache.NewEmissive(ForgeOrange, 2f));
        P(station.transform, PrimitiveType.Cube, new Vector3(0, 0.65f, -0.24f),
            new Vector3(0.35f, 0.12f, 0.01f), ShaderCache.NewEmissive(ForgeOrange, 2f));
        // Hammer on anvil
        P(station.transform, PrimitiveType.Cylinder, new Vector3(0.2f, 0.92f, 0.1f),
            new Vector3(0.04f, 0.22f, 0.04f), ShaderCache.NewLit(new Color(0.35f, 0.25f, 0.12f)))
            .transform.localRotation = Quaternion.Euler(0, 30, 85);
        P(station.transform, PrimitiveType.Cube, new Vector3(0.4f, 0.94f, 0.1f),
            new Vector3(0.12f, 0.08f, 0.08f), ShaderCache.NewMetal(new Color(0.3f, 0.28f, 0.3f)));

        // Coal pit beside
        P(station.transform, PrimitiveType.Cylinder, new Vector3(1.2f, 0.12f, 0.3f),
            new Vector3(0.6f, 0.12f, 0.6f), ShaderCache.NewStone(stoneBase * 0.7f));
        P(station.transform, PrimitiveType.Cylinder, new Vector3(1.2f, 0.08f, 0.3f),
            new Vector3(0.45f, 0.08f, 0.45f), ShaderCache.NewEmissive(new Color(0.8f, 0.2f, 0.05f), 2f));

        // Forge light
        AddLight(station.transform, new Vector3(0, 1.5f, 0), ForgeOrange, 3f, 6f);

        // Name plate
        P(station.transform, PrimitiveType.Cube, new Vector3(0, 1.4f, 0),
            new Vector3(1.2f, 0.08f, 0.08f), ShaderCache.NewEmissive(ForgeOrange, 2f));

        // Interaction
        var col = station.AddComponent<SphereCollider>();
        col.isTrigger = true; col.radius = 2f; col.center = new Vector3(0, 0.5f, 0);
        var interact = station.AddComponent<HubInteractable>();
        interact.stationId = "Forge Master"; interact.stationColor = ForgeOrange;
    }

    static void CreateElementAltar(Transform parent, Vector3 pos)
    {
        var station = new GameObject("Element Scholar");
        station.transform.parent = parent;
        station.transform.position = pos;

        var altarCol = new Color(0.3f, 0.5f, 0.9f);

        // Central pedestal
        P(station.transform, PrimitiveType.Cylinder, Vector3.up * 0.3f,
            new Vector3(0.8f, 0.3f, 0.8f), ShaderCache.NewStone(new Color(0.2f, 0.18f, 0.25f)));
        P(station.transform, PrimitiveType.Cylinder, Vector3.up * 0.62f,
            new Vector3(0.6f, 0.02f, 0.6f), ShaderCache.NewEmissive(altarCol, 2f));

        // 7 element orbs in a circle around the pedestal
        for (int i = 0; i < 7; i++)
        {
            float a = i * Mathf.PI * 2f / 7f;
            float r = 1.3f;
            Vector3 orbPos = new Vector3(Mathf.Sin(a) * r, 0.8f, Mathf.Cos(a) * r);

            // Small stone base for each orb
            P(station.transform, PrimitiveType.Cylinder, new Vector3(Mathf.Sin(a) * r, 0.15f, Mathf.Cos(a) * r),
                new Vector3(0.2f, 0.15f, 0.2f), ShaderCache.NewStone(PillarCol));
            // Element orb
            P(station.transform, PrimitiveType.Sphere, orbPos,
                Vector3.one * 0.18f, ShaderCache.NewEmissive(ElemColors[i], 4f));
        }

        // Central crystal
        P(station.transform, PrimitiveType.Cube, new Vector3(0, 0.85f, 0),
            new Vector3(0.1f, 0.25f, 0.1f), ShaderCache.NewEmissive(altarCol, 3f))
            .transform.rotation = Quaternion.Euler(0, 45, 10);

        AddLight(station.transform, Vector3.up * 1.2f, altarCol, 2f, 5f);

        P(station.transform, PrimitiveType.Cube, new Vector3(0, 1.4f, 0),
            new Vector3(1.2f, 0.08f, 0.08f), ShaderCache.NewEmissive(altarCol, 2f));

        var col = station.AddComponent<SphereCollider>();
        col.isTrigger = true; col.radius = 2f; col.center = new Vector3(0, 0.5f, 0);
        var interact = station.AddComponent<HubInteractable>();
        interact.stationId = "Element Scholar"; interact.stationColor = altarCol;
    }

    static void CreateChronicleDesk(Transform parent, Vector3 pos)
    {
        var station = new GameObject("Chronicle");
        station.transform.parent = parent;
        station.transform.position = pos;

        var chronicleCol = new Color(0.7f, 0.7f, 0.5f);

        // Desk legs + top
        for (int lx = -1; lx <= 1; lx += 2)
        for (int lz = -1; lz <= 1; lz += 2)
            P(station.transform, PrimitiveType.Cube, new Vector3(lx * 0.4f, 0.25f, lz * 0.25f),
                new Vector3(0.06f, 0.5f, 0.06f), ShaderCache.NewLit(WoodDark));
        P(station.transform, PrimitiveType.Cube, Vector3.up * 0.52f,
            new Vector3(1f, 0.04f, 0.6f), ShaderCache.NewLit(new Color(0.22f, 0.16f, 0.1f)));

        // Open book with glowing page
        P(station.transform, PrimitiveType.Cube, new Vector3(0, 0.56f, 0),
            new Vector3(0.3f, 0.02f, 0.2f), ShaderCache.NewLit(new Color(0.5f, 0.15f, 0.1f)));
        P(station.transform, PrimitiveType.Cube, new Vector3(0, 0.575f, 0),
            new Vector3(0.12f, 0.005f, 0.08f), ShaderCache.NewEmissive(chronicleCol, 2f));

        // Candle
        P(station.transform, PrimitiveType.Cylinder, new Vector3(-0.3f, 0.6f, 0.1f),
            new Vector3(0.03f, 0.06f, 0.03f), ShaderCache.NewLit(new Color(0.8f, 0.75f, 0.6f)));
        P(station.transform, PrimitiveType.Sphere, new Vector3(-0.3f, 0.69f, 0.1f),
            Vector3.one * 0.04f, ShaderCache.NewEmissive(new Color(1f, 0.7f, 0.2f), 4f));

        // Scrolls
        P(station.transform, PrimitiveType.Cylinder, new Vector3(0.25f, 0.58f, -0.1f),
            new Vector3(0.03f, 0.1f, 0.03f), ShaderCache.NewLit(new Color(0.7f, 0.65f, 0.5f)))
            .transform.localRotation = Quaternion.Euler(0, 0, 90);

        // Stats board behind desk
        P(station.transform, PrimitiveType.Cube, new Vector3(0, 1.3f, -0.3f),
            new Vector3(1.4f, 1f, 0.06f), ShaderCache.NewLit(WoodDark));
        P(station.transform, PrimitiveType.Cube, new Vector3(0, 1.3f, -0.34f),
            new Vector3(1.2f, 0.8f, 0.01f), ShaderCache.NewEmissive(chronicleCol * 0.3f, 0.5f));

        AddLight(station.transform, Vector3.up * 1.2f, chronicleCol, 1.5f, 4f);

        P(station.transform, PrimitiveType.Cube, new Vector3(0, 1.9f, 0),
            new Vector3(1.2f, 0.08f, 0.08f), ShaderCache.NewEmissive(chronicleCol, 2f));

        var col = station.AddComponent<SphereCollider>();
        col.isTrigger = true; col.radius = 2f; col.center = new Vector3(0, 0.5f, 0);
        var interact = station.AddComponent<HubInteractable>();
        interact.stationId = "Chronicle"; interact.stationColor = chronicleCol;
    }

    static void CreateCodexShelf(Transform parent, Vector3 pos)
    {
        var station = new GameObject("Codex");
        station.transform.parent = parent;
        station.transform.position = pos;

        var codexCol = new Color(0.6f, 0.3f, 0.9f);

        // Bookshelf frame
        P(station.transform, PrimitiveType.Cube, new Vector3(0, 1f, -0.2f),
            new Vector3(1.2f, 2f, 0.08f), ShaderCache.NewLit(WoodDark)); // back
        P(station.transform, PrimitiveType.Cube, new Vector3(-0.58f, 1f, 0),
            new Vector3(0.06f, 2f, 0.5f), ShaderCache.NewLit(WoodDark)); // left
        P(station.transform, PrimitiveType.Cube, new Vector3(0.58f, 1f, 0),
            new Vector3(0.06f, 2f, 0.5f), ShaderCache.NewLit(WoodDark)); // right
        // Shelves
        for (int s = 0; s < 3; s++)
            P(station.transform, PrimitiveType.Cube, new Vector3(0, 0.4f + s * 0.6f, 0),
                new Vector3(1.1f, 0.04f, 0.45f), ShaderCache.NewLit(new Color(0.22f, 0.16f, 0.1f)));

        // Books on shelves (varied colors)
        Color[] bookCols = { new(0.4f, 0.1f, 0.1f), new(0.1f, 0.15f, 0.4f), new(0.15f, 0.3f, 0.1f),
                             new(0.35f, 0.15f, 0.35f), new(0.3f, 0.25f, 0.1f), new(0.1f, 0.1f, 0.3f) };
        for (int s = 0; s < 3; s++)
        {
            float shelfY = 0.45f + s * 0.6f;
            int bookCount = 4 + Random.Range(0, 3);
            float bookX = -0.4f;
            for (int b = 0; b < bookCount && bookX < 0.45f; b++)
            {
                float bw = Random.Range(0.06f, 0.12f);
                float bh = Random.Range(0.2f, 0.35f);
                P(station.transform, PrimitiveType.Cube,
                    new Vector3(bookX + bw * 0.5f, shelfY + bh * 0.5f, 0),
                    new Vector3(bw, bh, 0.2f),
                    ShaderCache.NewLit(bookCols[Random.Range(0, bookCols.Length)]));
                bookX += bw + 0.02f;
            }
        }

        // Crystal cluster at base
        for (int c = 0; c < 3; c++)
        {
            float ch = Random.Range(0.15f, 0.35f);
            P(station.transform, PrimitiveType.Cube,
                new Vector3(0.3f + Random.Range(-0.1f, 0.1f), ch * 0.5f, 0.3f + Random.Range(-0.1f, 0.1f)),
                new Vector3(0.06f, ch, 0.06f), ShaderCache.NewEmissive(codexCol, 3f))
                .transform.rotation = Quaternion.Euler(Random.Range(-15f, 15f), Random.Range(0, 360), Random.Range(-15f, 15f));
        }

        // Glowing rune tome on middle shelf
        P(station.transform, PrimitiveType.Cube, new Vector3(0, 1.08f, 0.12f),
            new Vector3(0.2f, 0.25f, 0.04f), ShaderCache.NewEmissive(codexCol, 2f));

        AddLight(station.transform, Vector3.up * 1.5f, codexCol, 2f, 5f);

        P(station.transform, PrimitiveType.Cube, new Vector3(0, 2.1f, 0),
            new Vector3(1.2f, 0.08f, 0.08f), ShaderCache.NewEmissive(codexCol, 2f));

        var col = station.AddComponent<SphereCollider>();
        col.isTrigger = true; col.radius = 2f; col.center = new Vector3(0, 0.5f, 0);
        var interact = station.AddComponent<HubInteractable>();
        interact.stationId = "Codex"; interact.stationColor = codexCol;
    }

    static void CreateRunPortal(Transform parent, Vector3 pos)
    {
        var portal = new GameObject("RunPortal");
        portal.transform.parent = parent;
        portal.transform.position = pos;
        var portalGreen = new Color(0.2f, 0.8f, 0.4f);

        // Base ring
        P(portal.transform, PrimitiveType.Cylinder, Vector3.up * 0.05f,
            new Vector3(2.5f, 0.05f, 2.5f), ShaderCache.NewMagic(portalGreen, 4f));

        // Inner ring
        P(portal.transform, PrimitiveType.Cylinder, Vector3.up * 0.06f,
            new Vector3(1.5f, 0.03f, 1.5f), ShaderCache.NewEmissive(portalGreen, 2f));

        // Beam
        P(portal.transform, PrimitiveType.Cylinder, Vector3.up * 2f,
            new Vector3(0.3f, 2f, 0.3f), ShaderCache.NewMagic(new Color(0.2f, 1f, 0.5f), 4f));

        // Rune pillars around portal
        for (int i = 0; i < 4; i++)
        {
            float a = i * Mathf.PI * 2f / 4f + Mathf.PI / 4f;
            float r = 1.6f;
            Vector3 pp = new Vector3(Mathf.Sin(a) * r, 0, Mathf.Cos(a) * r);
            P(portal.transform, PrimitiveType.Cylinder, pp + Vector3.up * 0.5f,
                new Vector3(0.12f, 0.5f, 0.12f), ShaderCache.NewStone(PillarCol));
            P(portal.transform, PrimitiveType.Sphere, pp + Vector3.up * 1.05f,
                Vector3.one * 0.1f, ShaderCache.NewEmissive(portalGreen, 3f));
        }

        AddLight(portal.transform, Vector3.up * 1f, new Color(0.3f, 1f, 0.5f), 3f, 6f);

        var col = portal.AddComponent<SphereCollider>();
        col.isTrigger = true; col.radius = 1.5f; col.center = Vector3.up * 0.5f;
        var interact = portal.AddComponent<HubInteractable>();
        interact.stationId = "RunPortal"; interact.stationColor = new Color(0.2f, 0.9f, 0.4f);
    }

    // ─── DECORATION HELPERS ───────────────────────────────────────

    static void CreateBrazier(Transform parent, Vector3 pos, Color col)
    {
        P(parent, PrimitiveType.Cylinder, pos + Vector3.up * 0.03f,
            new Vector3(0.32f, 0.03f, 0.32f), ShaderCache.NewStone(PillarCol * 0.8f));
        P(parent, PrimitiveType.Cylinder, pos + Vector3.up * 0.5f,
            new Vector3(0.18f, 0.5f, 0.18f), ShaderCache.NewStone(PillarCol));
        P(parent, PrimitiveType.Cylinder, pos + Vector3.up * 1f,
            new Vector3(0.28f, 0.06f, 0.28f), ShaderCache.NewMetal(DarkIron));
        P(parent, PrimitiveType.Cylinder, pos + Vector3.up * 1.01f,
            new Vector3(0.22f, 0.04f, 0.22f), ShaderCache.NewLit(new Color(0.05f, 0.04f, 0.04f)));
        P(parent, PrimitiveType.Sphere, pos + Vector3.up * 1.15f,
            Vector3.one * 0.18f, ShaderCache.NewEmissive(col, 5f));
        P(parent, PrimitiveType.Sphere, pos + Vector3.up * 1.25f,
            Vector3.one * 0.09f, ShaderCache.NewEmissive(Color.Lerp(col, Color.white, 0.3f), 6f));
        AddLight(parent, pos + Vector3.up * 1.2f, col, 1.5f, 4f);
    }

    static void CreateBarrel(Transform parent, Vector3 pos, float scale = 1f)
    {
        P(parent, PrimitiveType.Cylinder, pos + Vector3.up * 0.3f * scale,
            new Vector3(0.35f, 0.3f, 0.35f) * scale, ShaderCache.NewLit(new Color(0.2f, 0.15f, 0.08f)));
        P(parent, PrimitiveType.Cylinder, pos + Vector3.up * 0.25f * scale,
            new Vector3(0.37f, 0.02f, 0.37f) * scale, ShaderCache.NewMetal(new Color(0.2f, 0.18f, 0.15f)));
    }

    static void CreateWeaponRack(Transform parent, Vector3 pos)
    {
        P(parent, PrimitiveType.Cube, pos + new Vector3(0, 1.2f, 0),
            new Vector3(0.08f, 1.2f, 1f), ShaderCache.NewLit(WoodDark));
        for (int s = 0; s < 3; s++)
        {
            float sz = pos.z - 0.3f + s * 0.3f;
            P(parent, PrimitiveType.Cylinder, new Vector3(pos.x + 0.15f, 1.0f, sz),
                new Vector3(0.04f, 0.7f, 0.04f), ShaderCache.NewLit(new Color(0.35f, 0.25f, 0.12f)))
                .transform.rotation = Quaternion.Euler(0, 0, 5 + s * 3);
            P(parent, PrimitiveType.Sphere, new Vector3(pos.x + 0.1f + s * 0.04f, 1.7f, sz),
                Vector3.one * 0.06f, ShaderCache.NewEmissive(ElemColors[s + 3], 3f));
        }
    }

    // ─── PROGRESSION & STATS ──────────────────────────────────────

    static void AddProgressDecoration(Transform parent, float hubCenter)
    {
        int totalSpent = MetaProgression.TotalEssenceSpent;
        int decorCount = Mathf.Min(totalSpent / 50, 20);

        for (int i = 0; i < decorCount; i++)
        {
            float angle = i * (360f / Mathf.Max(1, decorCount)) * Mathf.Deg2Rad;
            float radius = 6f + (i % 3) * 2f;
            Vector3 pos = new Vector3(
                hubCenter + Mathf.Cos(angle) * radius, 0,
                hubCenter + Mathf.Sin(angle) * radius);

            if (i % 2 == 0)
            {
                P(parent, PrimitiveType.Cylinder, pos + Vector3.up * 0.75f,
                    new Vector3(0.15f, 0.75f, 0.15f), ShaderCache.NewLit(new Color(0.3f, 0.25f, 0.2f)));
                P(parent, PrimitiveType.Sphere, pos + Vector3.up * 1.6f,
                    Vector3.one * 0.2f, ShaderCache.NewEmissive(new Color(1f, 0.5f, 0.1f), 3f));
            }
            else
            {
                Color pillarColor = totalSpent > 500 ? new Color(0.4f, 0.2f, 0.6f)
                    : totalSpent > 200 ? new Color(0.2f, 0.3f, 0.5f)
                    : new Color(0.35f, 0.3f, 0.25f);
                P(parent, PrimitiveType.Cube, pos + Vector3.up * 0.5f,
                    new Vector3(0.4f, 1f, 0.4f), ShaderCache.NewEmissive(pillarColor, 1.5f));
            }
        }
    }

    static void BuildStatsBoard(Transform parent)
    {
        Vector3 boardPos = new Vector3(14f, 1.5f, 10f);
        P(parent, PrimitiveType.Cube, boardPos,
            new Vector3(2f, 1.5f, 0.1f), ShaderCache.NewLit(WoodDark));

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

    // ─── CORE HELPERS ─────────────────────────────────────────────

    static void CreateHubWalls(Transform parent, int size)
    {
        float half = size / 2f;
        float wh = 3f;
        var mat = ShaderCache.NewStone(WallCol);
        W(parent, new Vector3(half, wh / 2, size + 0.25f), new Vector3(size + 1, wh, 0.5f), mat);
        W(parent, new Vector3(half, wh / 2, -0.25f), new Vector3(size + 1, wh, 0.5f), mat);
        W(parent, new Vector3(size + 0.25f, wh / 2, half), new Vector3(0.5f, wh, size), mat);
        W(parent, new Vector3(-0.25f, wh / 2, half), new Vector3(0.5f, wh, size), mat);
    }

    static void W(Transform parent, Vector3 pos, Vector3 scale, Material mat)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "HubWall";
        wall.transform.parent = parent;
        wall.transform.position = pos;
        wall.transform.localScale = scale;
        wall.GetComponent<Renderer>().material = mat;
        wall.isStatic = true;
    }

    /// <summary>Create a primitive, strip colliders, parent, position, scale, material.</summary>
    static GameObject P(Transform parent, PrimitiveType type, Vector3 localPos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        foreach (var c in go.GetComponents<Collider>()) Object.Destroy(c);
        go.transform.parent = parent;
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().material = mat;
        return go;
    }

    static void AddLight(Transform parent, Vector3 localPos, Color color, float intensity, float range)
    {
        var go = new GameObject("Light");
        go.transform.parent = parent;
        go.transform.localPosition = localPos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
    }
}
