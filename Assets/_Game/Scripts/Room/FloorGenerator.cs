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

    public enum RoomType { Combat, MiniBoss, Boss, Start, Shop, Rest, Event, Treasure }

    List<RoomData> rooms = new();
    int[,] grid;

    public List<RoomData> Rooms => rooms;

    /// <summary>
    /// Generate a floor with two branching paths.
    /// Layout: Start → 2 combat → Branch1 (Shop OR Combat) → Combat → Rest → MiniBoss
    ///         → Branch2 (Treasure/Event OR Combat) → Combat → Boss
    /// Total ~12 rooms with two meaningful route choices per floor.
    /// </summary>
    public void Generate(int roomCount, int floorIndex)
    {
        rooms.Clear();

        // Row 0: Start
        // Row 1-2: Combat rooms (linear)
        // Row 3: Branch 1 — Shop (left) or Combat/Elite (right)
        // Row 4: Converge — combat
        // Row 5: Rest room (floor 2+) or Treasure (floor 1)
        // Row 6: MiniBoss
        // Row 7: Branch 2 — Treasure/Event (left) or Combat (right)
        // Row 8: Converge — combat
        // Row 9: Boss

        int id = 0;

        // Row 0: Start room
        AddRoom(ref id, 1, 0, 10, 10, RoomType.Start, true);             // id 0

        // Row 1-2: Linear combat
        AddRoom(ref id, 1, 1, Random.Range(10, 14), Random.Range(10, 14), RoomType.Combat, true); // id 1
        AddRoom(ref id, 1, 2, Random.Range(10, 14), Random.Range(10, 14), RoomType.Combat, true); // id 2

        // Row 3: Branch 1 — Shop on left, Combat/Event on right
        int shopIdx = id;
        AddRoom(ref id, 0, 3, 10, 10, RoomType.Shop, false);             // id 3
        int branch1CombatIdx = id;
        AddRoom(ref id, 2, 3, Random.Range(12, 14), Random.Range(12, 14),
            floorIndex >= 2 ? RoomType.Event : RoomType.Combat, false);   // id 4

        // Row 4: Converge back
        int converge1Idx = id;
        AddRoom(ref id, 1, 4, Random.Range(10, 14), Random.Range(10, 14), RoomType.Combat, true); // id 5

        // Row 5: Rest room (floor 2+) or treasure (floor 1)
        if (floorIndex >= 1)
            AddRoom(ref id, 1, 5, 10, 10, RoomType.Rest, true);          // id 6
        else
            AddRoom(ref id, 1, 5, 10, 10, RoomType.Treasure, true);      // id 6

        // Row 6: MiniBoss
        int miniBossIdx = id;
        AddRoom(ref id, 1, 6, 14, 14, RoomType.MiniBoss, true);          // id 7

        // Row 7: Branch 2 — Treasure/Event on left, Combat on right
        int branch2LeftIdx = id;
        RoomType leftType = floorIndex >= 2 ? RoomType.Event : RoomType.Treasure;
        AddRoom(ref id, 0, 7, 10, 10, leftType, false);                  // id 8
        int branch2RightIdx = id;
        AddRoom(ref id, 2, 7, Random.Range(12, 14), Random.Range(12, 14), RoomType.Combat, false); // id 9

        // Row 8: Converge back
        int converge2Idx = id;
        AddRoom(ref id, 1, 8, Random.Range(10, 14), Random.Range(10, 14), RoomType.Combat, true); // id 10

        // Row 9: Boss
        AddRoom(ref id, 1, 9, 16, 16, RoomType.Boss, true);              // id 11

        // ── Connections ──

        // Linear: Start → Combat → Combat
        Connect(0, 1);
        Connect(1, 2);

        // Branch 1: Combat(2) → Shop OR Combat/Event
        Connect(2, shopIdx);
        Connect(2, branch1CombatIdx);

        // Converge 1: both branches → Converge combat
        Connect(shopIdx, converge1Idx);
        Connect(branch1CombatIdx, converge1Idx);

        // Linear: Converge → Rest → MiniBoss
        Connect(converge1Idx, converge1Idx + 1); // to Rest/Treasure
        Connect(converge1Idx + 1, miniBossIdx);

        // Branch 2: MiniBoss → Treasure/Event OR Combat
        Connect(miniBossIdx, branch2LeftIdx);
        Connect(miniBossIdx, branch2RightIdx);

        // Converge 2: both branches → Converge combat
        Connect(branch2LeftIdx, converge2Idx);
        Connect(branch2RightIdx, converge2Idx);

        // Final: Converge → Boss
        Connect(converge2Idx, converge2Idx + 1); // to Boss
    }

    void AddRoom(ref int id, int col, int row, int w, int h, RoomType type, bool critPath)
    {
        var room = new RoomData
        {
            id = id,
            bounds = new RectInt(col * 20, row * 20, w, h),
            type = type,
            connections = new List<int>(),
            isOnCritPath = critPath
        };
        rooms.Add(room);
        id++;
    }

    void Connect(int a, int b)
    {
        if (a < 0 || a >= rooms.Count || b < 0 || b >= rooms.Count) return;
        if (!rooms[a].connections.Contains(b))
            rooms[a].connections.Add(b);
        if (!rooms[b].connections.Contains(a))
            rooms[b].connections.Add(a);
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

            if (dx > 0) e = true;
            else if (dx < 0) w = true;

            // If same row and same col (shouldn't happen), use north/south
            if (dx == 0 && dz == 0)
            {
                if (other.id > room.id) n = true;
                else s = true;
            }
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
