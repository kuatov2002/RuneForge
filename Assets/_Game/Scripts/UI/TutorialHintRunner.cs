using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Event-driven tutorial system. Shows contextual hints when the player
/// performs specific actions for the first time.
/// Persists shown hints across runs via SaveSystem.
/// </summary>
public class TutorialHintRunner : MonoBehaviour
{
    GameHUD hud;
    SpellCaster caster;
    PlayerController ctrl;
    HashSet<string> shownHints = new();

    public void Run(GameHUD targetHud)
    {
        hud = targetHud;

        // Find player components
        var player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            caster = player.GetComponent<SpellCaster>();
            ctrl = player.GetComponent<PlayerController>();
        }

        // Immediately show first hint
        ShowOnce("start", "Press 1-4 to select elements, combine them for powerful combos!");

        // Subscribe to events
        if (caster != null)
        {
            caster.OnOrbsChanged += OnOrbsChanged;
            caster.OnComboNameChanged += OnComboChanged;
        }

        // Start coroutine for timed hints
        StartCoroutine(TimedHints());
        StartCoroutine(ContextualHints());
    }

    void OnOrbsChanged()
    {
        ShowOnce("orbs", "Two orbs above your head form a combo — try different pairs!");
    }

    void OnComboChanged(string comboName)
    {
        ShowOnce("combo", $"You discovered {comboName}! Click to cast, hold LMB for charged shot (2.5x damage)");
    }

    IEnumerator TimedHints()
    {
        yield return new WaitForSeconds(8f);
        if (hud == null) yield break;
        ShowOnce("dash", "Right-click to dash — you're invulnerable during the dodge! Dash goes in WASD direction.");

        yield return new WaitForSeconds(10f);
        if (hud == null) yield break;
        ShowOnce("potion", "Press F to drink a healing potion (+3 HP). Potions refill each floor.");

        yield return new WaitForSeconds(10f);
        if (hud == null) yield break;
        int total = MetaProgression.ComboDefinitions.Length;
        ShowOnce("explore", $"{total} spell synergies to discover — experiment with element pairs on enemies!");

        yield return new WaitForSeconds(12f);
        if (hud == null) yield break;
        ShowOnce("overheat", "Each element has limited charges. Use all charges and it OVERHEATS — wait for recharge!");

        yield return new WaitForSeconds(12f);
        if (hud == null) yield break;
        ShowOnce("variety", "Use 3+ different elements for a VARIETY BONUS: +15% to +30% damage!");

        yield return new WaitForSeconds(12f);
        if (hud == null) yield break;
        ShowOnce("momentum", "Kill enemies without taking damage to build MOMENTUM — up to 2x damage!");
    }

    /// <summary>Context-sensitive hints triggered by game state.</summary>
    IEnumerator ContextualHints()
    {
        // Wait for player to actually take damage before showing heal hint
        while (ctrl != null && hud != null)
        {
            yield return new WaitForSeconds(2f);
            if (ctrl == null || hud == null) yield break;

            // Show shop hint when first entering a shop room
            // (This is triggered externally by the room system)
        }
    }

    /// <summary>Call from room system when entering a shop for the first time.</summary>
    public void ShowShopHint()
    {
        ShowOnce("shop", "SHOP — Spend gold on relics, mutations, and potions. Gold earned from enemies!");
    }

    /// <summary>Call when encounter type is non-KillAll.</summary>
    public void ShowEncounterHint(string encounterType)
    {
        ShowOnce("encounter", $"Special encounter: {encounterType}! Check the objective at the top of the screen.");
    }

    void ShowOnce(string key, string text)
    {
        if (shownHints.Contains(key)) return;
        if (SaveSystem.Data.shownTutorialHints.Contains(key)) return;

        shownHints.Add(key);
        SaveSystem.Data.shownTutorialHints.Add(key);
        SaveSystem.Save();

        if (hud != null) hud.ShowHint(text, 6f);
    }

    void OnDestroy()
    {
        if (caster != null)
        {
            caster.OnOrbsChanged -= OnOrbsChanged;
            caster.OnComboNameChanged -= OnComboChanged;
        }
    }
}
