using UnityEngine;
using System.Collections;

public class TutorialHintRunner : MonoBehaviour
{
    GameHUD hud;

    public void Run(GameHUD targetHud)
    {
        hud = targetHud;
        StartCoroutine(ShowHints());
    }

    IEnumerator ShowHints()
    {
        // Immediately
        hud.ShowHint("Press 1-4 to select elements", 5f);

        yield return new WaitForSeconds(3f);
        if (hud == null) yield break;
        hud.ShowHint("Two orbs above your head form a combo", 5f);

        yield return new WaitForSeconds(5f);
        if (hud == null) yield break;
        hud.ShowHint("Click to cast, hold for charged shot", 5f);

        yield return new WaitForSeconds(5f);
        if (hud == null) yield break;
        hud.ShowHint("Right-click to dash", 4f);

        yield return new WaitForSeconds(4f);
        Destroy(this);
    }
}
