using UnityEngine;
using System;
using Random = UnityEngine.Random;
/// <summary>
/// Defines encounter types for combat rooms beyond simple "kill all".
/// Rolled per combat room to add variety.
/// Timer is decremented externally via UpdateTimer().
/// </summary>
public class EncounterSystem
{
    public enum EncounterType { KillAll, Survival, PriorityTarget, Gauntlet, TimedChallenge }

    public EncounterType Type { get; private set; }
    public float Timer { get; set; }
    public bool IsComplete { get; set; }
    public int BonusGold { get; private set; }
    public bool BonusAwarded { get; set; }

    public event Action OnObjectiveComplete;

    public EncounterSystem(EncounterType type, int floor = 1)
    {
        Type = type;
        IsComplete = false;
        BonusAwarded = false;

        // Scale timers and rewards with floor
        float timerScale = 1f + (floor - 1) * 0.1f; // slightly longer on higher floors

        switch (type)
        {
            case EncounterType.Survival:
                Timer = 30f + (floor - 1) * 5f; // 30s floor 1, 50s floor 5
                BonusGold = 30 + floor * 5;
                break;
            case EncounterType.TimedChallenge:
                Timer = 25f + (floor - 1) * 3f; // more time on harder floors
                BonusGold = 50 + floor * 10;
                break;
            case EncounterType.Gauntlet:
                Timer = 40f + (floor - 1) * 5f; // timed onslaught
                BonusGold = 40 + floor * 8;
                break;
            case EncounterType.PriorityTarget:
                BonusGold = 25 + floor * 5;
                break;
            default:
                BonusGold = 0;
                break;
        }
    }

    /// <summary>Roll a random encounter type based on floor and room.
    /// Higher floors have more varied encounters.</summary>
    public static EncounterType Roll(int floor, int room)
    {
        // Floor 1 early rooms: always KillAll
        if (floor <= 1 && room <= 3) return EncounterType.KillAll;

        // Scale probabilities with floor depth
        float killAllChance = Mathf.Max(0.2f, 0.45f - floor * 0.05f);
        float survivalChance = 0.15f;
        float priorityChance = 0.15f;
        float gauntletChance = 0.12f + floor * 0.02f;
        // TimedChallenge gets the rest

        float r = Random.value;
        float cumulative = 0f;

        cumulative += killAllChance;
        if (r < cumulative) return EncounterType.KillAll;

        cumulative += survivalChance;
        if (r < cumulative) return EncounterType.Survival;

        cumulative += priorityChance;
        if (r < cumulative) return EncounterType.PriorityTarget;

        cumulative += gauntletChance;
        if (r < cumulative) return EncounterType.Gauntlet;

        return EncounterType.TimedChallenge;
    }

    /// <summary>Call every frame to update timer-based encounters.</summary>
    public void UpdateTimer(float deltaTime)
    {
        if (IsComplete) return;

        switch (Type)
        {
            case EncounterType.Survival:
            case EncounterType.Gauntlet:
                Timer -= deltaTime;
                if (Timer <= 0f)
                {
                    Timer = 0f;
                    CompleteObjective();
                }
                break;
            case EncounterType.TimedChallenge:
                Timer -= deltaTime;
                if (Timer <= 0f)
                    Timer = 0f; // Timer expired — bonus lost, but room still clears on kill all
                break;
        }
    }

    public void CompleteObjective()
    {
        if (IsComplete) return;
        IsComplete = true;
        OnObjectiveComplete?.Invoke();
    }

    public string GetObjectiveText()
    {
        if (IsComplete)
        {
            return Type switch
            {
                EncounterType.KillAll => "ALL ENEMIES DEFEATED!",
                EncounterType.Survival => "SURVIVED!",
                EncounterType.Gauntlet => "GAUNTLET COMPLETE!",
                EncounterType.TimedChallenge => BonusAwarded ? "BONUS EARNED!" : "CLEARED!",
                EncounterType.PriorityTarget => "TARGET ELIMINATED!",
                _ => "COMPLETE!"
            };
        }

        return Type switch
        {
            EncounterType.KillAll => "KILL ALL ENEMIES",
            EncounterType.Survival => $"SURVIVE — {Timer:F0}s remaining",
            EncounterType.TimedChallenge => Timer > 0
                ? $"TIMED — Clear in {Timer:F0}s for +{BonusGold}g bonus!"
                : "TIME'S UP — Kill remaining enemies",
            EncounterType.PriorityTarget => "PRIORITY — Kill the marked enemy!",
            EncounterType.Gauntlet => $"GAUNTLET — Survive {Timer:F0}s!",
            _ => ""
        };
    }

    /// <summary>Get a color for the objective text based on urgency.</summary>
    public Color GetObjectiveColor()
    {
        if (IsComplete) return new Color(0.3f, 1f, 0.4f);

        return Type switch
        {
            EncounterType.TimedChallenge when Timer <= 5f => new Color(1f, 0.3f, 0.2f),
            EncounterType.TimedChallenge => new Color(1f, 0.85f, 0.2f),
            EncounterType.Survival when Timer <= 10f => new Color(1f, 0.5f, 0.2f),
            EncounterType.Survival => new Color(0.4f, 0.8f, 1f),
            EncounterType.Gauntlet => new Color(1f, 0.4f, 0.2f),
            EncounterType.PriorityTarget => new Color(1f, 0.6f, 0.8f),
            _ => new Color(0.8f, 0.8f, 0.85f)
        };
    }
}
