using UnityEngine;

public static class RoomBuilder
{
    static Material wallMat;
    static Material floorMat;
    static Material floorTileMat;
    static Material pillarMat;

    public static GameObject Build(int width = 12, int height = 12,
        bool doorN = false, bool doorS = false, bool doorE = false, bool doorW = false,
        int floorIndex = 0)
    {
        var room = new GameObject("Room");
        var (wallCol, floorCol, floorAltCol, pillarCol) = FloorGenerator.GetFloorTheme(floorIndex);
        floorMat = ShaderCache.NewFloor(floorCol, floorAltCol);
        floorTileMat = floorMat;
        wallMat = ShaderCache.NewStone(wallCol);
        pillarMat = ShaderCache.NewStone(pillarCol);

        // Floor - checkerboard tiles
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
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
        float halfW = width * 0.5f;
        float halfH = height * 0.5f;

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

        // Corner pillars
        CreateDecoPillar(room.transform, new Vector3(-0.25f, 0, -0.25f), 3f);
        CreateDecoPillar(room.transform, new Vector3(width + 0.25f, 0, -0.25f), 3f);
        CreateDecoPillar(room.transform, new Vector3(-0.25f, 0, height + 0.25f), 3f);
        CreateDecoPillar(room.transform, new Vector3(width + 0.25f, 0, height + 0.25f), 3f);

        // Gameplay pillars - vary based on room size
        if (width >= 10 && height >= 10)
        {
            float px1 = width * 0.25f;
            float px2 = width * 0.75f;
            float pz1 = height * 0.25f;
            float pz2 = height * 0.75f;
            CreatePillar(room.transform, new Vector3(px1, 0, pz1));
            CreatePillar(room.transform, new Vector3(px2, 0, pz1));
            CreatePillar(room.transform, new Vector3(px1, 0, pz2));
            CreatePillar(room.transform, new Vector3(px2, 0, pz2));
            if (width >= 12 && height >= 12)
                CreatePillar(room.transform, new Vector3(halfW, 0, halfH));
        }

        // Torches
        CreateTorch(room.transform, new Vector3(0.3f, 1.5f, 0.3f), new Color(1f, 0.6f, 0.2f));
        CreateTorch(room.transform, new Vector3(width - 0.3f, 1.5f, 0.3f), new Color(1f, 0.6f, 0.2f));
        CreateTorch(room.transform, new Vector3(0.3f, 1.5f, height - 0.3f), new Color(0.2f, 0.5f, 1f));
        CreateTorch(room.transform, new Vector3(width - 0.3f, 1.5f, height - 0.3f), new Color(0.2f, 0.5f, 1f));

        // Door trigger zones
        if (doorN) CreateDoorTrigger(room.transform, new Vector3(halfW, 0.5f, height + 0.5f), "DoorN");
        if (doorS) CreateDoorTrigger(room.transform, new Vector3(halfW, 0.5f, -0.5f), "DoorS");
        if (doorE) CreateDoorTrigger(room.transform, new Vector3(width + 0.5f, 0.5f, halfH), "DoorE");
        if (doorW) CreateDoorTrigger(room.transform, new Vector3(-0.5f, 0.5f, halfH), "DoorW");

        return room;
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
