using UnityEngine;

/// <summary>
/// Defines encounter types for combat rooms beyond simple "kill all".
/// Rolled per combat room to add variety.
/// </summary>
public class EncounterSystem
{
    public enum EncounterType { KillAll, Survival, PriorityTarget, Gauntlet, TimedChallenge }

    public EncounterType Type { get; private set; }
    public float Timer { get; set; }
    public bool IsComplete { get; set; }
    public int BonusGold { get; private set; }
    public bool BonusAwarded { get; set; }

    public EncounterSystem(EncounterType type)
    {
        Type = type;
        IsComplete = false;
        BonusAwarded = false;

        switch (type)
        {
            case EncounterType.Survival:
                Timer = 30f;
                BonusGold = 30;
                break;
            case EncounterType.TimedChallenge:
                Timer = 25f;
                BonusGold = 50;
                break;
            case EncounterType.Gauntlet:
                BonusGold = 40;
                break;
            case EncounterType.PriorityTarget:
                BonusGold = 25;
                break;
            default:
                BonusGold = 0;
                break;
        }
    }

    /// <summary>Roll a random encounter type based on floor and room.</summary>
    public static EncounterType Roll(int floor, int room)
    {
        // Floor 1: always KillAll
        if (floor <= 1 && room <= 3) return EncounterType.KillAll;

        float r = Random.value;
        // KillAll 45%, Survival 15%, PriorityTarget 15%, Gauntlet 12%, Timed 13%
        if (r < 0.45f) return EncounterType.KillAll;
        if (r < 0.60f) return EncounterType.Survival;
        if (r < 0.75f) return EncounterType.PriorityTarget;
        if (r < 0.87f) return EncounterType.Gauntlet;
        return EncounterType.TimedChallenge;
    }

    public string GetObjectiveText()
    {
        return Type switch
        {
            EncounterType.Survival => $"SURVIVE — {Timer:F0}s remaining",
            EncounterType.TimedChallenge => $"TIMED CHALLENGE — {Timer:F0}s left for bonus!",
            EncounterType.PriorityTarget => "PRIORITY TARGET — Kill the marked enemy!",
            EncounterType.Gauntlet => "GAUNTLET — Survive the onslaught!",
            _ => ""
        };
    }
}
