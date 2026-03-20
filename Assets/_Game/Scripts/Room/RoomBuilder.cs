using UnityEngine;

public static class RoomBuilder
{
    static Material wallMat;
    static Material floorMat;
    static Material pillarMat;
    static Material floorTileMat;

    public static GameObject Build(int width = 12, int height = 12)
    {
        var room = new GameObject("Room");
        var litShader = Shader.Find("Universal Render Pipeline/Lit");

        floorMat = new Material(litShader) { color = new Color(0.18f, 0.18f, 0.22f) };
        floorTileMat = new Material(litShader) { color = new Color(0.22f, 0.22f, 0.28f) };
        wallMat = new Material(litShader) { color = new Color(0.35f, 0.28f, 0.22f) };
        pillarMat = new Material(litShader) { color = new Color(0.4f, 0.35f, 0.3f) };

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

        // Walls - thicker, taller
        CreateWall(room.transform, new Vector3(width * 0.5f, wh * 0.5f, height + 0.25f),
            new Vector3(width + 1, wh, 0.5f));
        CreateWall(room.transform, new Vector3(width * 0.5f, wh * 0.5f, -0.25f),
            new Vector3(width + 1, wh, 0.5f));
        CreateWall(room.transform, new Vector3(width + 0.25f, wh * 0.5f, height * 0.5f),
            new Vector3(0.5f, wh, height));
        CreateWall(room.transform, new Vector3(-0.25f, wh * 0.5f, height * 0.5f),
            new Vector3(0.5f, wh, height));

        // Corner pillars (decorative, taller)
        CreateDecoPillar(room.transform, new Vector3(-0.25f, 0, -0.25f), 3f);
        CreateDecoPillar(room.transform, new Vector3(width + 0.25f, 0, -0.25f), 3f);
        CreateDecoPillar(room.transform, new Vector3(-0.25f, 0, height + 0.25f), 3f);
        CreateDecoPillar(room.transform, new Vector3(width + 0.25f, 0, height + 0.25f), 3f);

        // Gameplay pillars (cover)
        CreatePillar(room.transform, new Vector3(3, 0, 3));
        CreatePillar(room.transform, new Vector3(9, 0, 3));
        CreatePillar(room.transform, new Vector3(3, 0, 9));
        CreatePillar(room.transform, new Vector3(9, 0, 9));
        CreatePillar(room.transform, new Vector3(6, 0, 6));

        // Ambient point lights (torch-like)
        CreateTorch(room.transform, new Vector3(0.3f, 1.5f, 0.3f), new Color(1f, 0.6f, 0.2f));
        CreateTorch(room.transform, new Vector3(width - 0.3f, 1.5f, 0.3f), new Color(1f, 0.6f, 0.2f));
        CreateTorch(room.transform, new Vector3(0.3f, 1.5f, height - 0.3f), new Color(0.2f, 0.5f, 1f));
        CreateTorch(room.transform, new Vector3(width - 0.3f, 1.5f, height - 0.3f), new Color(0.2f, 0.5f, 1f));

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

        // Pillar top cap
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

        // Small emissive sphere as visual
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(sphere.GetComponent<SphereCollider>());
        sphere.transform.parent = torchGO.transform;
        sphere.transform.localPosition = Vector3.zero;
        sphere.transform.localScale = Vector3.one * 0.15f;
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", color * 5f);
        sphere.GetComponent<Renderer>().material = mat;
    }
}
