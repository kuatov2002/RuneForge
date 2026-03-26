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
        LevelUp,
        Freeze,
        ShieldBlock,
        MomentumUp
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

        // Impact: short punchy thud
        _clips[(int)SFXType.Hit] = MakeClip("Hit", 0.1f, (t, d) =>
        {
            float env = Mathf.Exp(-t * 30f);
            return (Mathf.Sin(t * 600f) + Noise(t, 2500f) * 0.6f) * env * 0.5f;
        });

        // Crit: deeper impact + ring
        _clips[(int)SFXType.CritHit] = MakeClip("CritHit", 0.2f, (t, d) =>
        {
            float env = Mathf.Exp(-t * 15f);
            float ring = Mathf.Sin(t * 400f) * Mathf.Exp(-t * 8f) * 0.3f;
            return ((Mathf.Sin(t * 500f) + Mathf.Sin(t * 750f)) * 0.5f * env + ring) * 0.45f;
        });

        // Cast: whoosh rising pitch
        _clips[(int)SFXType.Cast] = MakeClip("Cast", 0.15f, (t, d) =>
        {
            float freq = 300f + (t / d) * 1500f;
            float env = Mathf.Sin(t / d * Mathf.PI);
            return Mathf.Sin(t * freq) * env * 0.3f + Noise(t, 6000f) * env * 0.15f;
        });

        // Dash: fast swoosh
        _clips[(int)SFXType.Dash] = MakeClip("Dash", 0.12f, (t, d) =>
        {
            float env = Mathf.Sin(t / d * Mathf.PI);
            return Noise(t, 7000f) * env * 0.35f * Mathf.Sin(t * 150f + t * t * 3000f);
        });

        // Enemy death: low thud + crumble
        _clips[(int)SFXType.EnemyDeath] = MakeClip("EnemyDeath", 0.25f, (t, d) =>
        {
            float thud = Mathf.Sin(t * (400f - t * 1200f)) * Mathf.Exp(-t * 12f);
            float crumble = Noise(t, 1500f) * (1f - t / d) * 0.3f;
            return (thud * 0.5f + crumble) * 0.4f;
        });

        // Player hit: sharp impact + low alarm
        _clips[(int)SFXType.PlayerHit] = MakeClip("PlayerHit", 0.2f, (t, d) =>
        {
            float impact = Noise(t, 4000f) * Mathf.Exp(-t * 20f) * 0.6f;
            float alarm = Mathf.Sin(t * 250f) * (1f - t / d) * 0.3f;
            return (impact + alarm) * 0.5f;
        });

        // Gold: bright ding
        _clips[(int)SFXType.GoldPickup] = MakeClip("GoldPickup", 0.1f, (t, d) =>
        {
            float env = Mathf.Exp(-t * 20f);
            return (Mathf.Sin(t * 1500f) + Mathf.Sin(t * 2250f) * 0.5f) * env * 0.2f;
        });

        // DualCast: harmonic chord
        _clips[(int)SFXType.DualCast] = MakeClip("DualCast", 0.3f, (t, d) =>
        {
            float env = (1f - t / d);
            return (Mathf.Sin(t * 440f) + Mathf.Sin(t * 660f) + Mathf.Sin(t * 880f)) / 3f * env * 0.35f;
        });

        // Explosion: big rumble + crack
        _clips[(int)SFXType.Explosion] = MakeClip("Explosion", 0.35f, (t, d) =>
        {
            float crack = Noise(t, 3000f) * Mathf.Exp(-t * 8f) * 0.6f;
            float rumble = Mathf.Sin(t * 80f) * (1f - t / d) * 0.4f;
            float debris = Noise(t, 1200f) * (1f - t / d) * 0.2f;
            return (crack + rumble + debris) * 0.5f;
        });

        _clips[(int)SFXType.MenuClick] = MakeClip("MenuClick", 0.04f, (t, d) =>
            Mathf.Sin(t * 2000f) * Mathf.Exp(-t * 80f) * 0.2f);

        // Level up: ascending arpeggio
        _clips[(int)SFXType.LevelUp] = MakeClip("LevelUp", 0.4f, (t, d) =>
        {
            float note = t < 0.1f ? 523f : t < 0.2f ? 659f : t < 0.3f ? 784f : 1047f;
            float env = Mathf.Exp(-((t % 0.1f) * 15f)) * (1f - t / d * 0.5f);
            return Mathf.Sin(t * note) * env * 0.25f;
        });

        // Freeze: crystalline shatter
        _clips[(int)SFXType.Freeze] = MakeClip("Freeze", 0.2f, (t, d) =>
        {
            float crystal = Mathf.Sin(t * 2000f) * Mathf.Sin(t * 3100f) * Mathf.Exp(-t * 12f);
            float crack = Noise(t, 8000f) * Mathf.Exp(-t * 25f) * 0.4f;
            return (crystal + crack) * 0.3f;
        });

        // Shield block: metallic clang
        _clips[(int)SFXType.ShieldBlock] = MakeClip("ShieldBlock", 0.15f, (t, d) =>
        {
            float clang = (Mathf.Sin(t * 1800f) + Mathf.Sin(t * 2700f) * 0.5f) * Mathf.Exp(-t * 18f);
            return clang * 0.35f;
        });

        // Momentum up: power chord
        _clips[(int)SFXType.MomentumUp] = MakeClip("MomentumUp", 0.2f, (t, d) =>
        {
            float env = Mathf.Exp(-t * 8f);
            return (Mathf.Sin(t * 330f) + Mathf.Sin(t * 440f) + Mathf.Sin(t * 550f)) / 3f * env * 0.3f;
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
        AudioSource.PlayClipAtPoint(_clips[idx], position, volume);
    }

    // ─── Ambient Music ───

    AudioSource _musicSource;
    bool _musicPlaying;

    /// <summary>Start looping ambient music (procedurally generated drone).</summary>
    public void StartMusic()
    {
        if (_musicPlaying) return;
        _musicPlaying = true;

        if (_musicSource == null)
        {
            _musicSource = gameObject.AddComponent<AudioSource>();
            _musicSource.spatialBlend = 0f;
            _musicSource.loop = true;
            _musicSource.volume = 0.08f;
        }

        // Generate ambient drone: low pads + subtle melody
        float duration = 8f; // 8 second loop
        int sampleRate = 44100;
        int samples = Mathf.CeilToInt(duration * sampleRate);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float phase = t / duration; // 0-1 across the loop

            // Low drone pad (smooth loop)
            float drone = Mathf.Sin(t * 55f * Mathf.PI * 2f) * 0.3f; // A1
            drone += Mathf.Sin(t * 82.5f * Mathf.PI * 2f) * 0.15f; // E2
            drone += Mathf.Sin(t * 110f * Mathf.PI * 2f) * 0.1f; // A2

            // Slow LFO modulation
            float lfo = 0.7f + 0.3f * Mathf.Sin(t * 0.3f * Mathf.PI * 2f);
            drone *= lfo;

            // Subtle high shimmer
            float shimmer = Mathf.Sin(t * 440f * Mathf.PI * 2f) * 0.02f;
            shimmer *= (0.5f + 0.5f * Mathf.Sin(t * 0.5f * Mathf.PI * 2f));

            data[i] = Mathf.Clamp(drone + shimmer, -1f, 1f);
        }

        var clip = AudioClip.Create("AmbientMusic", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        _musicSource.clip = clip;
        _musicSource.Play();
    }

    public void StopMusic()
    {
        _musicPlaying = false;
        if (_musicSource != null) _musicSource.Stop();
    }
}
