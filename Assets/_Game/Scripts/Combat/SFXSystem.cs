using UnityEngine;

/// <summary>
/// Procedural audio system. Generates all sounds from code — no audio assets needed.
/// Uses AudioClip.Create with waveform generation.
/// </summary>
public class SFXSystem : MonoBehaviour
{
    public static SFXSystem Instance { get; private set; }

    public enum SFXType
    {
        Hit,
        CritHit,
        Cast,
        Dash,
        EnemyDeath,
        PlayerHit,
        GoldPickup,
        DualCast,
        Explosion,
        MenuClick,
        LevelUp
    }

    AudioSource _source;
    static AudioClip[] _clips;

    void Awake()
    {
        Instance = this;
        _source = gameObject.AddComponent<AudioSource>();
        _source.spatialBlend = 0f; // 2D
        _source.playOnAwake = false;
        GenerateClips();
    }

    static void GenerateClips()
    {
        if (_clips != null) return;
        _clips = new AudioClip[System.Enum.GetValues(typeof(SFXType)).Length];

        _clips[(int)SFXType.Hit] = MakeClip("Hit", 0.08f, (t, d) =>
            Mathf.Sin(t * 800f) * (1f - t / d) * Noise(t, 3000f) * 0.5f);

        _clips[(int)SFXType.CritHit] = MakeClip("CritHit", 0.15f, (t, d) =>
            (Mathf.Sin(t * 600f) + Mathf.Sin(t * 900f)) * (1f - t / d) * 0.4f);

        _clips[(int)SFXType.Cast] = MakeClip("Cast", 0.12f, (t, d) =>
            Mathf.Sin(t * (400f + t * 2000f)) * (1f - t / d) * 0.35f);

        _clips[(int)SFXType.Dash] = MakeClip("Dash", 0.1f, (t, d) =>
            Noise(t, 8000f) * (1f - t / d) * Mathf.Sin(t * 200f) * 0.3f);

        _clips[(int)SFXType.EnemyDeath] = MakeClip("EnemyDeath", 0.2f, (t, d) =>
            (Mathf.Sin(t * (500f - t * 1500f)) + Noise(t, 2000f) * 0.3f) * (1f - t / d) * 0.4f);

        _clips[(int)SFXType.PlayerHit] = MakeClip("PlayerHit", 0.15f, (t, d) =>
            (Mathf.Sin(t * 300f) * Noise(t, 5000f)) * (1f - t / d) * 0.5f);

        _clips[(int)SFXType.GoldPickup] = MakeClip("GoldPickup", 0.08f, (t, d) =>
            Mathf.Sin(t * (1200f + t * 3000f)) * (1f - t / d) * 0.25f);

        _clips[(int)SFXType.DualCast] = MakeClip("DualCast", 0.25f, (t, d) =>
            (Mathf.Sin(t * 500f) + Mathf.Sin(t * 750f) + Mathf.Sin(t * 1000f)) / 3f * (1f - t / d) * 0.4f);

        _clips[(int)SFXType.Explosion] = MakeClip("Explosion", 0.3f, (t, d) =>
            Noise(t, 4000f) * (1f - t / d) * 0.5f * Mathf.Sin(t * 100f));

        _clips[(int)SFXType.MenuClick] = MakeClip("MenuClick", 0.04f, (t, d) =>
            Mathf.Sin(t * 2000f) * (1f - t / d) * 0.2f);

        _clips[(int)SFXType.LevelUp] = MakeClip("LevelUp", 0.3f, (t, d) =>
        {
            float freq = 600f + (t / d) * 600f; // rising pitch
            return Mathf.Sin(t * freq) * (1f - t / d * 0.5f) * 0.3f;
        });
    }

    delegate float WaveFunc(float time, float duration);

    static AudioClip MakeClip(string name, float duration, WaveFunc wave)
    {
        int sampleRate = 44100;
        int samples = Mathf.CeilToInt(duration * sampleRate);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            data[i] = Mathf.Clamp(wave(t, duration), -1f, 1f);
        }

        var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static float Noise(float t, float freq)
    {
        // Deterministic noise via sin with prime multipliers
        return Mathf.Sin(t * freq * 1.7f) * Mathf.Sin(t * freq * 2.3f);
    }

    /// <summary>Play a sound effect. Call from anywhere.</summary>
    public static void Play(SFXType type, Vector3 position = default, float volumeScale = 1f)
    {
        if (Instance == null || _clips == null) return;
        int idx = (int)type;
        if (idx < 0 || idx >= _clips.Length || _clips[idx] == null) return;

        Instance._source.PlayOneShot(_clips[idx], volumeScale);
    }

    /// <summary>Play at a world position with spatial falloff.</summary>
    public static void PlayAt(SFXType type, Vector3 position, float volume = 1f)
    {
        if (Instance == null || _clips == null) return;
        int idx = (int)type;
        if (idx < 0 || idx >= _clips.Length || _clips[idx] == null) return;

        // Use PlayClipAtPoint for spatial audio
        AudioSource.PlayClipAtPoint(_clips[idx], position, volume);
    }
}
