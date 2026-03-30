using UnityEngine;

public enum RoomType { Normal, Elite, Boss, Shop, Other }

public enum RoomShape { Rectangle, LShape, Arena, Cross, Corridor, Pillared }

public static class RoomBuilder
{
    static Material wallMat;
    static Material floorMat;
    static Material floorTileMat;
    static Material pillarMat;

    public static RoomShape LastBuiltShape { get; private set; }

    public static GameObject Build(int width = 12, int height = 12,
        bool doorN = false, bool doorS = false, bool doorE = false, bool doorW = false,
        int floorIndex = 0, RoomType roomType = RoomType.Normal)
    {
        var room = new GameObject("Room");
        var (wallCol, floorCol, floorAltCol, pillarCol) = FloorGenerator.GetFloorTheme(floorIndex);
        floorMat = ShaderCache.NewFloor(floorCol, floorAltCol);
        floorTileMat = floorMat;
        wallMat = ShaderCache.NewStone(wallCol);
        pillarMat = ShaderCache.NewStone(pillarCol);

        RoomShape shape = PickRoomShape(roomType, width, height);
        LastBuiltShape = shape;

        // Corridor overrides room dimensions to be narrow and elongated
        if (shape == RoomShape.Corridor)
        {
            width = 6;
            height = Mathf.RoundToInt(height * 1.3f);
        }

        float halfW = width * 0.5f;
        float halfH = height * 0.5f;

        // Floor - checkerboard tiles (skip tiles based on shape)
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                if (!ShouldPlaceTile(shape, x, z, width, height, halfW, halfH))
                    continue;

                var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tile.name = "FloorTile";
                tile.transform.parent = room.transform;
                tile.transform.position = new Vector3(x + 0.5f, -0.25f, z + 0.5f);
                tile.transform.localScale = new Vector3(0.98f, 0.5f, 0.98f);
                tile.GetComponent<Renderer>().material = (x + z) % 2 == 0 ? floorMat : floorTileMat;
                tile.isStatic = true;
            }
        }

        float wh = 2f;
        float doorWidth = 2f;

        // -- Outer walls --

        // North wall (z = height)
        if (doorN)
        {
            float leftW = halfW - doorWidth * 0.5f;
            float rightW = halfW - doorWidth * 0.5f;
            CreateWall(room.transform, new Vector3(leftW * 0.5f, wh * 0.5f, height + 0.25f),
                new Vector3(leftW + 0.5f, wh, 0.5f));
            CreateWall(room.transform, new Vector3(width - rightW * 0.5f, wh * 0.5f, height + 0.25f),
                new Vector3(rightW + 0.5f, wh, 0.5f));
        }
        else
        {
            CreateWall(room.transform, new Vector3(halfW, wh * 0.5f, height + 0.25f),
                new Vector3(width + 1, wh, 0.5f));
        }

        // South wall (z = 0)
        if (doorS)
        {
            float leftW = halfW - doorWidth * 0.5f;
            float rightW = halfW - doorWidth * 0.5f;
            CreateWall(room.transform, new Vector3(leftW * 0.5f, wh * 0.5f, -0.25f),
                new Vector3(leftW + 0.5f, wh, 0.5f));
            CreateWall(room.transform, new Vector3(width - rightW * 0.5f, wh * 0.5f, -0.25f),
                new Vector3(rightW + 0.5f, wh, 0.5f));
        }
        else
        {
            CreateWall(room.transform, new Vector3(halfW, wh * 0.5f, -0.25f),
                new Vector3(width + 1, wh, 0.5f));
        }

        // East wall (x = width)
        if (doorE)
        {
            float botH = halfH - doorWidth * 0.5f;
            float topH = halfH - doorWidth * 0.5f;
            CreateWall(room.transform, new Vector3(width + 0.25f, wh * 0.5f, botH * 0.5f),
                new Vector3(0.5f, wh, botH));
            CreateWall(room.transform, new Vector3(width + 0.25f, wh * 0.5f, height - topH * 0.5f),
                new Vector3(0.5f, wh, topH));
        }
        else
        {
            CreateWall(room.transform, new Vector3(width + 0.25f, wh * 0.5f, halfH),
                new Vector3(0.5f, wh, height));
        }

        // West wall (x = 0)
        if (doorW)
        {
            float botH = halfH - doorWidth * 0.5f;
            float topH = halfH - doorWidth * 0.5f;
            CreateWall(room.transform, new Vector3(-0.25f, wh * 0.5f, botH * 0.5f),
                new Vector3(0.5f, wh, botH));
            CreateWall(room.transform, new Vector3(-0.25f, wh * 0.5f, height - topH * 0.5f),
                new Vector3(0.5f, wh, topH));
        }
        else
        {
            CreateWall(room.transform, new Vector3(-0.25f, wh * 0.5f, halfH),
                new Vector3(0.5f, wh, height));
        }

        // -- Shape-specific inner walls and features --

        if (shape == RoomShape.LShape)
        {
            // Inner walls along the L-shape cutout (top-right corner removed)
            float cutX = width * 0.6f;
            float cutZ = height * 0.6f;
            float innerWallLenX = width - cutX;
            float innerWallLenZ = height - cutZ;

            // Vertical inner wall (along x = cutX, from z = cutZ to z = height)
            CreateWall(room.transform, new Vector3(cutX, wh * 0.5f, cutZ + innerWallLenZ * 0.5f),
                new Vector3(0.5f, wh, innerWallLenZ));

            // Horizontal inner wall (along z = cutZ, from x = cutX to x = width)
            CreateWall(room.transform, new Vector3(cutX + innerWallLenX * 0.5f, wh * 0.5f, cutZ),
                new Vector3(innerWallLenX, wh, 0.5f));
        }
        else if (shape == RoomShape.Arena)
        {
            // Central pillar arrangement: ring of pillars around center
            float ringRadius = Mathf.Min(width, height) * 0.3f;
            int pillarCount = 6;
            for (int i = 0; i < pillarCount; i++)
            {
                float angle = i * Mathf.PI * 2f / pillarCount;
                float px = halfW + Mathf.Cos(angle) * ringRadius;
                float pz = halfH + Mathf.Sin(angle) * ringRadius;
                CreatePillar(room.transform, new Vector3(px, 0, pz));
            }
        }
        else if (shape == RoomShape.Pillared)
        {
            // Scattered pillars in center area for cover-based combat
            int count = Random.Range(4, 7);
            float innerW = width * 0.6f;
            float innerH = height * 0.6f;
            for (int i = 0; i < count; i++)
            {
                Vector3 pos = new Vector3(
                    halfW + Random.Range(-innerW / 2f, innerW / 2f),
                    0,
                    halfH + Random.Range(-innerH / 2f, innerH / 2f)
                );
                CreatePillar(room.transform, pos);
            }
        }

        // Corner pillars
        CreateDecoPillar(room.transform, new Vector3(-0.25f, 0, -0.25f), 3f);
        CreateDecoPillar(room.transform, new Vector3(width + 0.25f, 0, -0.25f), 3f);
        CreateDecoPillar(room.transform, new Vector3(-0.25f, 0, height + 0.25f), 3f);
        CreateDecoPillar(room.transform, new Vector3(width + 0.25f, 0, height + 0.25f), 3f);

        // Gameplay pillars - vary based on room size (skip for shapes that have their own pillars)
        bool hasOwnPillars = shape == RoomShape.Arena || shape == RoomShape.Pillared;
        if (!hasOwnPillars && width >= 10 && height >= 10)
        {
            float px1 = width * 0.25f;
            float px2 = width * 0.75f;
            float pz1 = height * 0.25f;
            float pz2 = height * 0.75f;

            // For L-shape, only place pillars that are inside the room
            if (shape == RoomShape.LShape)
            {
                if (ShouldPlaceTile(shape, (int)px1, (int)pz1, width, height, halfW, halfH))
                    CreatePillar(room.transform, new Vector3(px1, 0, pz1));
                if (ShouldPlaceTile(shape, (int)px2, (int)pz1, width, height, halfW, halfH))
                    CreatePillar(room.transform, new Vector3(px2, 0, pz1));
                if (ShouldPlaceTile(shape, (int)px1, (int)pz2, width, height, halfW, halfH))
                    CreatePillar(room.transform, new Vector3(px1, 0, pz2));
                if (ShouldPlaceTile(shape, (int)px2, (int)pz2, width, height, halfW, halfH))
                    CreatePillar(room.transform, new Vector3(px2, 0, pz2));
            }
            else
            {
                CreatePillar(room.transform, new Vector3(px1, 0, pz1));
                CreatePillar(room.transform, new Vector3(px2, 0, pz1));
                CreatePillar(room.transform, new Vector3(px1, 0, pz2));
                CreatePillar(room.transform, new Vector3(px2, 0, pz2));
            }

            if (width >= 12 && height >= 12 && shape != RoomShape.LShape)
                CreatePillar(room.transform, new Vector3(halfW, 0, halfH));
        }

        // Torches - place only where floor exists
        if (ShouldPlaceTile(shape, 0, 0, width, height, halfW, halfH))
            CreateTorch(room.transform, new Vector3(0.3f, 1.5f, 0.3f), new Color(1f, 0.6f, 0.2f));
        if (ShouldPlaceTile(shape, width - 1, 0, width, height, halfW, halfH))
            CreateTorch(room.transform, new Vector3(width - 0.3f, 1.5f, 0.3f), new Color(1f, 0.6f, 0.2f));
        if (ShouldPlaceTile(shape, 0, height - 1, width, height, halfW, halfH))
            CreateTorch(room.transform, new Vector3(0.3f, 1.5f, height - 0.3f), new Color(0.2f, 0.5f, 1f));
        if (ShouldPlaceTile(shape, width - 1, height - 1, width, height, halfW, halfH))
            CreateTorch(room.transform, new Vector3(width - 0.3f, 1.5f, height - 0.3f), new Color(0.2f, 0.5f, 1f));

        // Door trigger zones (always placed - doors are at midpoints which are always inside all shapes)
        if (doorN) CreateDoorTrigger(room.transform, new Vector3(halfW, 0.5f, height + 0.5f), "DoorN");
        if (doorS) CreateDoorTrigger(room.transform, new Vector3(halfW, 0.5f, -0.5f), "DoorS");
        if (doorE) CreateDoorTrigger(room.transform, new Vector3(width + 0.5f, 0.5f, halfH), "DoorE");
        if (doorW) CreateDoorTrigger(room.transform, new Vector3(-0.5f, 0.5f, halfH), "DoorW");

        return room;
    }

    /// <summary>
    /// Selects a room shape based on room type and random chance.
    /// Boss and Shop rooms always use Rectangle. Elite rooms have higher Arena chance.
    /// </summary>
    static RoomShape PickRoomShape(RoomType roomType, int width, int height)
    {
        if (roomType == RoomType.Boss || roomType == RoomType.Shop || roomType == RoomType.Other)
            return RoomShape.Rectangle;

        if (roomType == RoomType.Elite)
            return Random.value < 0.4f ? RoomShape.Arena : RoomShape.Rectangle;

        // Normal combat rooms: weighted random selection
        float r = Random.value;
        if (r < 0.35f) return RoomShape.Rectangle;
        if (r < 0.50f) return RoomShape.Arena;
        if (r < 0.65f) return RoomShape.Cross;
        if (r < 0.78f) return RoomShape.Corridor;
        if (r < 0.90f) return RoomShape.Pillared;
        return RoomShape.LShape;
    }

    /// <summary>
    /// Determines whether a floor tile should be placed at (x, z) based on room shape.
    /// </summary>
    static bool ShouldPlaceTile(RoomShape shape, int x, int z, int w, int h,
        float halfW, float halfH)
    {
        switch (shape)
        {
            case RoomShape.LShape:
                // Remove top-right corner quadrant
                if (x > w * 0.6f && z > h * 0.6f) return false;
                return true;

            case RoomShape.Arena:
                // Circular: remove corner tiles where distance from center > 85% of half-size
                float dx = (x + 0.5f) - halfW;
                float dz = (z + 0.5f) - halfH;
                float maxDist = Mathf.Min(halfW, halfH) * 0.85f;
                // Keep tiles near door positions (within 2 tiles of center axis)
                bool nearNSDoor = Mathf.Abs(dx) < 2f;
                bool nearEWDoor = Mathf.Abs(dz) < 2f;
                if (nearNSDoor || nearEWDoor) return true;
                float dist = Mathf.Sqrt(dx * dx + dz * dz);
                if (dist > maxDist) return false;
                return true;

            case RoomShape.Cross:
                // Skip tiles in four corners where both x and z offsets exceed 30% of room size
                float xOff = Mathf.Abs((x + 0.5f) - halfW);
                float zOff = Mathf.Abs((z + 0.5f) - halfH);
                if (xOff > w * 0.3f && zOff > h * 0.3f) return false;
                return true;

            default: // Rectangle, Corridor, Pillared all use full rectangular floor
                return true;
        }
    }

    static void CreateWall(Transform parent, Vector3 pos, Vector3 scale)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "Wall";
        wall.transform.parent = parent;
        wall.transform.position = pos;
        wall.transform.localScale = scale;
        wall.GetComponent<Renderer>().material = wallMat;
        wall.isStatic = true;
    }

    static void CreatePillar(Transform parent, Vector3 pos)
    {
        var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pillar.name = "Pillar";
        pillar.transform.parent = parent;
        pillar.transform.position = pos + Vector3.up * 0.75f;
        pillar.transform.localScale = new Vector3(0.8f, 0.75f, 0.8f);
        pillar.GetComponent<Renderer>().material = pillarMat;
        pillar.isStatic = true;

        var cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cap.name = "PillarCap";
        cap.transform.parent = pillar.transform;
        cap.transform.localPosition = new Vector3(0, 1f, 0);
        cap.transform.localScale = new Vector3(1.3f, 0.1f, 1.3f);
        cap.GetComponent<Renderer>().material = pillarMat;
        cap.isStatic = true;
    }

    static void CreateDecoPillar(Transform parent, Vector3 pos, float h)
    {
        var pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pillar.name = "CornerPillar";
        pillar.transform.parent = parent;
        pillar.transform.position = pos + Vector3.up * h * 0.5f;
        pillar.transform.localScale = new Vector3(0.6f, h, 0.6f);
        pillar.GetComponent<Renderer>().material = pillarMat;
        pillar.isStatic = true;
    }

    static void CreateTorch(Transform parent, Vector3 pos, Color color)
    {
        var torchGO = new GameObject("Torch");
        torchGO.transform.parent = parent;
        torchGO.transform.position = pos;

        var light = torchGO.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = 2f;
        light.range = 6f;
        light.shadows = LightShadows.Soft;

        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(sphere.GetComponent<SphereCollider>());
        sphere.transform.parent = torchGO.transform;
        sphere.transform.localPosition = Vector3.zero;
        sphere.transform.localScale = Vector3.one * 0.15f;
        var mat = ShaderCache.NewFire(color);
        sphere.GetComponent<Renderer>().material = mat;
    }

    static void CreateDoorTrigger(Transform parent, Vector3 pos, string name)
    {
        var trigger = new GameObject(name);
        trigger.transform.parent = parent;
        trigger.transform.position = pos;
        var col = trigger.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(2f, 2f, 1f);
        trigger.AddComponent<DoorTrigger>().doorName = name;
    }
}
