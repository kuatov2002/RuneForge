using UnityEngine;
using System.Collections.Generic;

public class FloorGenerator
{
    public struct RoomData
    {
        public int id;
        public RectInt bounds;
        public RoomType type;
        public List<int> connections; // connected room IDs
        public bool isOnCritPath;
    }

    public enum RoomType { Combat, MiniBoss, Boss, Start }

    List<RoomData> rooms = new();
    int[,] grid; // room id at each cell, -1 = corridor, 0 = empty

    public List<RoomData> Rooms => rooms;

    // Generate a floor with ~roomCount rooms using BSP
    public void Generate(int roomCount, int floorIndex)
    {
        rooms.Clear();

        // Place rooms in a grid layout for simplicity
        // Use a snake pattern: rooms placed left-to-right, then right-to-left
        int cols = 3;
        int rows = Mathf.CeilToInt(roomCount / (float)cols);

        for (int i = 0; i < roomCount; i++)
        {
            int row = i / cols;
            int col = i % cols;
            // Snake: even rows left-to-right, odd rows right-to-left
            if (row % 2 == 1) col = cols - 1 - col;

            // Variable room sizes
            int w, h;
            if (i == 0)
            {
                w = 10; h = 10; // Start room - small
            }
            else if (i == roomCount - 1)
            {
                w = 16; h = 16; // Boss room - large
            }
            else if (i % 3 == 0 && i > 0)
            {
                w = 14; h = 14; // Mini-boss rooms - medium-large
            }
            else
            {
                w = Random.Range(10, 14);
                h = Random.Range(10, 14);
            }

            RoomType type;
            if (i == 0) type = RoomType.Start;
            else if (i == roomCount - 1) type = RoomType.Boss;
            else if (i % 3 == 0) type = RoomType.MiniBoss;
            else type = RoomType.Combat;

            var room = new RoomData
            {
                id = i,
                bounds = new RectInt(col * 20, row * 20, w, h),
                type = type,
                connections = new List<int>(),
                isOnCritPath = true // All rooms on critical path in linear layout
            };
            rooms.Add(room);
        }

        // Connect sequential rooms
        for (int i = 0; i < rooms.Count - 1; i++)
        {
            rooms[i].connections.Add(i + 1);
            rooms[i + 1].connections.Add(i);
        }
    }

    // Get door directions for a room based on its connections
    public (bool north, bool south, bool east, bool west) GetDoors(int roomId)
    {
        bool n = false, s = false, e = false, w = false;
        var room = rooms[roomId];

        foreach (int connId in room.connections)
        {
            var other = rooms[connId];
            int dx = other.bounds.x - room.bounds.x;
            int dz = other.bounds.y - room.bounds.y;

            if (dz > 0) n = true;
            else if (dz < 0) s = true;
            else if (dx > 0) e = true;
            else if (dx < 0) w = true;
        }
        return (n, s, e, w);
    }

    // Get the floor theme colors based on floor index
    public static (Color wall, Color floor, Color floorAlt, Color pillar) GetFloorTheme(int floorIndex)
    {
        return floorIndex switch
        {
            0 => (new Color(0.35f, 0.28f, 0.22f), new Color(0.18f, 0.18f, 0.22f),
                  new Color(0.22f, 0.22f, 0.28f), new Color(0.4f, 0.35f, 0.3f)),
            1 => (new Color(0.22f, 0.30f, 0.35f), new Color(0.14f, 0.18f, 0.22f),
                  new Color(0.18f, 0.22f, 0.28f), new Color(0.28f, 0.35f, 0.4f)),
            2 => (new Color(0.35f, 0.22f, 0.30f), new Color(0.20f, 0.14f, 0.18f),
                  new Color(0.25f, 0.18f, 0.22f), new Color(0.4f, 0.28f, 0.35f)),
            3 => (new Color(0.18f, 0.22f, 0.18f), new Color(0.12f, 0.16f, 0.12f),
                  new Color(0.16f, 0.20f, 0.16f), new Color(0.25f, 0.30f, 0.25f)),
            _ => (new Color(0.28f, 0.15f, 0.15f), new Color(0.16f, 0.10f, 0.10f),
                  new Color(0.20f, 0.14f, 0.14f), new Color(0.35f, 0.20f, 0.20f))
        };
    }
}
