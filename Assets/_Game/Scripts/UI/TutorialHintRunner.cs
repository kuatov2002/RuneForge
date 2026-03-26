using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Event-driven tutorial system. Shows contextual hints when the player
/// performs specific actions for the first time.
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
        ShowOnce("dash", "Right-click to dash — you're invulnerable during the dodge!");

        yield return new WaitForSeconds(12f);
        if (hud == null) yield break;
        ShowOnce("explore", "44 combos to discover — experiment with element pairs!");

        yield return new WaitForSeconds(15f);
        Destroy(this);
    }

    void ShowOnce(string key, string text)
    {
        if (shownHints.Contains(key)) return;
        shownHints.Add(key);
        if (hud != null) hud.ShowHint(text, 5f);
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
