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
    /// Generate a floor with branching paths.
    /// Layout: Start → 2-3 combat rooms → Branch (choice: Shop OR Combat) → 2 combat → MiniBoss → 1-2 combat → Boss
    /// Total ~10 rooms with meaningful route choices.
    /// </summary>
    public void Generate(int roomCount, int floorIndex)
    {
        rooms.Clear();

        // Fixed structure with branching:
        // Row 0: Start
        // Row 1-2: Combat rooms (linear)
        // Row 3: Branch — player chooses left (Shop) or right (Combat/Elite)
        // Row 4: Converge — combat
        // Row 5: Rest room (heal before boss on floor 2+)
        // Row 6: MiniBoss (every floor has one mid-point challenge)
        // Row 7-8: Combat rooms
        // Row 9: Boss

        int cols = 3;
        int id = 0;

        // Row 0: Start room
        AddRoom(ref id, 1, 0, 10, 10, RoomType.Start, true);

        // Row 1-2: Linear combat
        AddRoom(ref id, 1, 1, Random.Range(10, 14), Random.Range(10, 14), RoomType.Combat, true);
        AddRoom(ref id, 1, 2, Random.Range(10, 14), Random.Range(10, 14), RoomType.Combat, true);

        // Row 3: Branch — Shop on left, Combat/Event on right
        int shopIdx = id;
        AddRoom(ref id, 0, 3, 10, 10, RoomType.Shop, false);
        int branchCombatIdx = id;
        AddRoom(ref id, 2, 3, Random.Range(12, 14), Random.Range(12, 14),
            floorIndex >= 2 ? RoomType.Event : RoomType.Combat, false);

        // Row 4: Converge back
        AddRoom(ref id, 1, 4, Random.Range(10, 14), Random.Range(10, 14), RoomType.Combat, true);

        // Row 5: Rest room (floor 2+) or treasure (floor 1)
        if (floorIndex >= 1)
            AddRoom(ref id, 1, 5, 10, 10, RoomType.Rest, true);
        else
            AddRoom(ref id, 1, 5, 10, 10, RoomType.Treasure, true);

        // Row 6: MiniBoss
        AddRoom(ref id, 1, 6, 14, 14, RoomType.MiniBoss, true);

        // Row 7-8: Post-miniboss combat
        AddRoom(ref id, 1, 7, Random.Range(10, 14), Random.Range(10, 14), RoomType.Combat, true);
        AddRoom(ref id, 1, 8, Random.Range(10, 14), Random.Range(10, 14), RoomType.Combat, true);

        // Row 9: Boss
        AddRoom(ref id, 1, 9, 16, 16, RoomType.Boss, true);

        // Connect linear path: Start(0) → Combat(1) → Combat(2)
        Connect(0, 1);
        Connect(1, 2);

        // Branch: Combat(2) → Shop(3) AND Combat(2) → BranchCombat(4)
        Connect(2, shopIdx);      // left branch
        Connect(2, branchCombatIdx); // right branch

        // Both branches converge: Shop(3) → Converge(5), BranchCombat(4) → Converge(5)
        Connect(shopIdx, 5);
        Connect(branchCombatIdx, 5);

        // Linear from converge: 5 → Rest(6) → MiniBoss(7) → Combat(8) → Combat(9) → Boss(10)
        for (int i = 5; i < rooms.Count - 1; i++)
            Connect(i, i + 1);
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
