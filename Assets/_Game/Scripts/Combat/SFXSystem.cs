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
        MomentumUp,
        Reaction,
        BossIntro,
        PlayerDeath,
        DoorOpen,
        ShopBuy,
        PickupJuice,
        Footstep,
        UIHover,
        RelicPickup,
        StatusApply
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

        // Reaction: magical burst with harmonic ring
        _clips[(int)SFXType.Reaction] = MakeClip("Reaction", 0.25f, (t, d) =>
        {
            float env = Mathf.Exp(-t * 10f);
            float ring = Mathf.Sin(t * 1200f) * Mathf.Sin(t * 1800f) * 0.4f;
            float burst = Noise(t, 5000f) * Mathf.Exp(-t * 30f) * 0.5f;
            return (ring + burst) * env * 0.35f;
        });

        // Boss intro: deep ominous drone rising
        _clips[(int)SFXType.BossIntro] = MakeClip("BossIntro", 0.8f, (t, d) =>
        {
            float freq = 80f + (t / d) * 200f;
            float env = Mathf.Sin(t / d * Mathf.PI);
            float drone = Mathf.Sin(t * freq) * 0.4f + Mathf.Sin(t * freq * 1.5f) * 0.2f;
            float rumble = Noise(t, 200f) * 0.15f;
            return (drone + rumble) * env * 0.4f;
        });

        // Player death: descending whomp
        _clips[(int)SFXType.PlayerDeath] = MakeClip("PlayerDeath", 0.5f, (t, d) =>
        {
            float freq = 400f - (t / d) * 350f;
            float env = 1f - t / d;
            return (Mathf.Sin(t * freq) * 0.5f + Noise(t, 1000f) * 0.3f) * env * 0.4f;
        });

        // Door open: stone sliding
        _clips[(int)SFXType.DoorOpen] = MakeClip("DoorOpen", 0.3f, (t, d) =>
        {
            float env = Mathf.Sin(t / d * Mathf.PI);
            return Noise(t, 800f) * env * 0.25f + Mathf.Sin(t * 200f) * env * 0.1f;
        });

        // Shop buy: coin register cha-ching
        _clips[(int)SFXType.ShopBuy] = MakeClip("ShopBuy", 0.15f, (t, d) =>
        {
            float env = Mathf.Exp(-t * 15f);
            return (Mathf.Sin(t * 2000f) + Mathf.Sin(t * 3000f) * 0.5f + Mathf.Sin(t * 4000f) * 0.25f) * env * 0.2f;
        });

        // Pickup juice: bright chime
        _clips[(int)SFXType.PickupJuice] = MakeClip("PickupJuice", 0.12f, (t, d) =>
        {
            float env = Mathf.Exp(-t * 20f);
            return (Mathf.Sin(t * 1800f) + Mathf.Sin(t * 2700f) * 0.6f) * env * 0.2f;
        });

        // Footstep: soft thud with noise burst
        _clips[(int)SFXType.Footstep] = MakeClip("Footstep", 0.03f, (t, d) =>
        {
            float env = 1f - t / d;
            float body = Mathf.Sin(t * 200f * 6.28f) * 0.15f;
            float grit = (Mathf.PerlinNoise(t * 500f, 0) - 0.5f) * 0.1f;
            return (body + grit) * env;
        });

        // UI hover: short click
        _clips[(int)SFXType.UIHover] = MakeClip("UIHover", 0.02f, (t, d) =>
        {
            float env = 1f - t / d;
            return Mathf.Sin(t * 1200f * 6.28f) * 0.3f * env;
        });

        // Relic pickup: ascending chord sweep
        _clips[(int)SFXType.RelicPickup] = MakeClip("RelicPickup", 0.2f, (t, d) =>
        {
            float env = 1f - t / d;
            float freq = 600f + t * 3000f;
            return (Mathf.Sin(t * freq * 6.28f) * 0.15f +
                    Mathf.Sin(t * freq * 1.5f * 6.28f) * 0.1f +
                    Mathf.Sin(t * freq * 2f * 6.28f) * 0.08f) * env;
        });

        // Status apply: modulated zing
        _clips[(int)SFXType.StatusApply] = MakeClip("StatusApply", 0.05f, (t, d) =>
        {
            float env = 1f - t / d;
            return Mathf.Sin(t * 500f * 6.28f) * Mathf.Sin(t * 80f * 6.28f) * 0.2f * env;
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

    // ─── Music System ───

    public enum MusicTrack { None, Title, Hub, Combat, Boss }

    AudioSource _musicSource;
    AudioSource _musicSourceB; // for crossfade
    MusicTrack _currentTrack = MusicTrack.None;
    bool _crossfading;
    float _crossfadeTimer;
    const float CrossfadeDuration = 1.5f;
    static AudioClip[] _musicClips;

    void GenerateMusicClips()
    {
        if (_musicClips != null) return;
        _musicClips = new AudioClip[System.Enum.GetValues(typeof(MusicTrack)).Length];

        const int sr = 44100;
        const float TAU = Mathf.PI * 2f;

        // ─── TITLE: Mysterious, grand, slow build ───
        _musicClips[(int)MusicTrack.Title] = MakeMusicClip("TitleMusic", 12f, sr, (t, d) =>
        {
            float phase = t / d;
            // Deep resonant drone in D minor
            float drone = Mathf.Sin(t * 73.4f * TAU) * 0.25f;  // D2
            drone += Mathf.Sin(t * 110f * TAU) * 0.15f;         // A2
            drone += Mathf.Sin(t * 146.8f * TAU) * 0.08f;       // D3

            // Slow swell envelope
            float swell = 0.5f + 0.5f * Mathf.Sin(phase * TAU * 0.5f);
            drone *= swell;

            // Ethereal high pad — fifths shimmering
            float pad = Mathf.Sin(t * 587f * TAU) * 0.02f;  // D5
            pad += Mathf.Sin(t * 880f * TAU) * 0.015f;       // A5
            float padEnv = 0.3f + 0.7f * Mathf.Sin(phase * TAU);
            pad *= padEnv;

            // Mystical bell tones — every 3 seconds, a note rings
            float bellPhase = (t % 3f) / 3f;
            float bellFreq = (int)(t / 3f) % 4 == 0 ? 587f : (int)(t / 3f) % 4 == 1 ? 698f :
                             (int)(t / 3f) % 4 == 2 ? 880f : 784f;
            float bell = Mathf.Sin(t * bellFreq * TAU) * Mathf.Exp(-bellPhase * 4f) * 0.06f;

            // Subtle noise texture
            float noise = Noise(t, 300f) * 0.02f * swell;

            return Mathf.Clamp(drone + pad + bell + noise, -1f, 1f);
        });

        // ─── HUB: Calm, warm, contemplative ───
        _musicClips[(int)MusicTrack.Hub] = MakeMusicClip("HubMusic", 16f, sr, (t, d) =>
        {
            float phase = t / d;

            // Warm pad in C major — root + third + fifth
            float pad = Mathf.Sin(t * 65.4f * TAU) * 0.2f;   // C2
            pad += Mathf.Sin(t * 82.4f * TAU) * 0.12f;        // E2
            pad += Mathf.Sin(t * 98f * TAU) * 0.1f;           // G2
            pad += Mathf.Sin(t * 130.8f * TAU) * 0.06f;       // C3

            // Breathing LFO
            float lfo = 0.6f + 0.4f * Mathf.Sin(t * 0.2f * TAU);
            pad *= lfo;

            // Gentle melodic notes — pentatonic sequence
            float[] melody = { 523f, 587f, 659f, 784f, 880f, 784f, 659f, 587f };
            float noteLen = d / melody.Length;
            int noteIdx = (int)(t / noteLen) % melody.Length;
            float noteT = (t % noteLen) / noteLen;
            float noteEnv = Mathf.Sin(noteT * Mathf.PI) * 0.6f; // Bell shape
            float mel = Mathf.Sin(t * melody[noteIdx] * TAU) * noteEnv * 0.04f;

            // Soft high harmonics
            float shimmer = Mathf.Sin(t * 1047f * TAU) * 0.01f; // C6
            shimmer *= 0.5f + 0.5f * Mathf.Sin(t * 0.15f * TAU);

            // Warmth: soft filtered noise
            float warmth = Noise(t, 200f) * 0.015f * lfo;

            return Mathf.Clamp(pad + mel + shimmer + warmth, -1f, 1f);
        });

        // ─── COMBAT: Driving, rhythmic, tense ───
        _musicClips[(int)MusicTrack.Combat] = MakeMusicClip("CombatMusic", 8f, sr, (t, d) =>
        {
            float phase = t / d;

            // Kick drum pattern — 4 beats per loop (120 BPM in 8s = 16 beats)
            float beatLen = d / 16f;
            float beatPhase = (t % beatLen) / beatLen;
            float kick = Mathf.Sin(beatPhase * 200f * TAU * (1f - beatPhase)) * Mathf.Exp(-beatPhase * 20f) * 0.3f;

            // Hi-hat on offbeats
            float halfBeat = d / 32f;
            float hihatPhase = (t % halfBeat) / halfBeat;
            float hihat = Noise(t, 12000f) * Mathf.Exp(-hihatPhase * 40f) * 0.08f;

            // Aggressive bass in E minor — pulsing
            float bassFreq = 82.4f; // E2
            float bassPulse = 0.5f + 0.5f * Mathf.Sin(beatPhase * Mathf.PI);
            float bass = Mathf.Sin(t * bassFreq * TAU) * 0.2f * bassPulse;
            bass += Mathf.Sin(t * bassFreq * 2f * TAU) * 0.08f * bassPulse; // Octave harmonic

            // Tension chord stabs — Am on beats 1, 5, 9, 13
            int beatNum = (int)(t / beatLen) % 16;
            float stab = 0f;
            if (beatNum % 4 == 0)
            {
                float stabEnv = Mathf.Exp(-beatPhase * 8f);
                stab = (Mathf.Sin(t * 220f * TAU) + Mathf.Sin(t * 330f * TAU) * 0.6f +
                        Mathf.Sin(t * 440f * TAU) * 0.3f) * stabEnv * 0.08f;
            }

            // Rising tension sweep across the loop
            float sweepFreq = 150f + phase * 400f;
            float sweep = Mathf.Sin(t * sweepFreq * TAU) * 0.03f * phase;

            // Rhythmic noise accent
            float accent = 0f;
            if (beatNum % 4 == 2)
                accent = Noise(t, 3000f) * Mathf.Exp(-beatPhase * 15f) * 0.1f;

            return Mathf.Clamp(kick + hihat + bass + stab + sweep + accent, -1f, 1f);
        });

        // ─── BOSS: Epic, intense, dramatic ───
        _musicClips[(int)MusicTrack.Boss] = MakeMusicClip("BossMusic", 10f, sr, (t, d) =>
        {
            float phase = t / d;

            // Heavy double-time kick (150 BPM feel)
            float beatLen = d / 20f;
            float beatPhase = (t % beatLen) / beatLen;
            float kick = Mathf.Sin(beatPhase * 250f * TAU * (1f - beatPhase * 0.8f)) * Mathf.Exp(-beatPhase * 15f) * 0.35f;

            // Snare on beats 5, 10, 15, 20
            int beatNum = (int)(t / beatLen) % 20;
            float snare = 0f;
            if (beatNum % 5 == 4)
                snare = Noise(t, 5000f) * Mathf.Exp(-beatPhase * 12f) * 0.2f;

            // Deep power bass — D minor, octave pulse
            float bassRoot = 73.4f; // D2
            float bassOct = Mathf.Sin(t * bassRoot * TAU) * 0.22f;
            bassOct += Mathf.Sin(t * bassRoot * 2f * TAU) * 0.1f;
            float bassPulse = 0.6f + 0.4f * Mathf.Sin(beatPhase * Mathf.PI);
            bassOct *= bassPulse;

            // Dissonant power chord stabs — Dm, Bb, C progression
            float[] chordRoots = { 293.7f, 233.1f, 261.6f }; // D4, Bb3, C4
            int chordIdx = (int)(phase * 3f) % 3;
            float chordRoot = chordRoots[chordIdx];
            float stabEnv = 0f;
            if (beatNum % 5 < 2)
                stabEnv = Mathf.Exp(-beatPhase * 6f) * 0.8f;
            float chord = (Mathf.Sin(t * chordRoot * TAU) +
                          Mathf.Sin(t * chordRoot * 1.2f * TAU) * 0.7f + // minor third
                          Mathf.Sin(t * chordRoot * 1.5f * TAU) * 0.5f)  // fifth
                          * stabEnv * 0.08f;

            // Eerie high wail — slides between notes
            float wailFreq = 880f + Mathf.Sin(phase * TAU * 2f) * 200f;
            float wail = Mathf.Sin(t * wailFreq * TAU) * 0.025f;
            wail *= 0.5f + 0.5f * Mathf.Sin(t * 3f * TAU); // Tremolo

            // Tension riser — noise sweep building across the loop
            float riser = Noise(t, 1000f + phase * 5000f) * phase * 0.06f;

            // Impact hit at loop start
            float impact = 0f;
            if (t < 0.15f)
                impact = Mathf.Sin(t * 60f * TAU) * Mathf.Exp(-t * 12f) * 0.3f;

            return Mathf.Clamp(kick + snare + bassOct + chord + wail + riser + impact, -1f, 1f);
        });
    }

    static AudioClip MakeMusicClip(string name, float duration, int sampleRate, WaveFunc wave)
    {
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

    void EnsureMusicSources()
    {
        if (_musicSource == null)
        {
            _musicSource = gameObject.AddComponent<AudioSource>();
            _musicSource.spatialBlend = 0f;
            _musicSource.loop = true;
            _musicSource.volume = 0.08f;
        }
        if (_musicSourceB == null)
        {
            _musicSourceB = gameObject.AddComponent<AudioSource>();
            _musicSourceB.spatialBlend = 0f;
            _musicSourceB.loop = true;
            _musicSourceB.volume = 0f;
        }
    }

    /// <summary>Start looping ambient music (legacy — defaults to Combat track).</summary>
    public void StartMusic()
    {
        SetMusicTrack(MusicTrack.Combat);
    }

    /// <summary>Switch to a specific music track with crossfade.</summary>
    public void SetMusicTrack(MusicTrack track)
    {
        if (track == _currentTrack) return;
        GenerateMusicClips();
        EnsureMusicSources();

        if (track == MusicTrack.None)
        {
            StopMusic();
            return;
        }

        int idx = (int)track;
        if (idx < 0 || idx >= _musicClips.Length || _musicClips[idx] == null) return;

        if (_currentTrack == MusicTrack.None)
        {
            // No music playing — just start directly
            _musicSource.clip = _musicClips[idx];
            _musicSource.volume = 0.08f;
            _musicSource.Play();
            _currentTrack = track;
            return;
        }

        // Crossfade: swap sources — B gets the new track, A fades out
        _musicSourceB.clip = _musicClips[idx];
        _musicSourceB.volume = 0f;
        _musicSourceB.Play();
        _crossfading = true;
        _crossfadeTimer = 0f;
        _currentTrack = track;
    }

    void UpdateMusicCrossfade()
    {
        if (!_crossfading) return;
        _crossfadeTimer += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(_crossfadeTimer / CrossfadeDuration);

        _musicSource.volume = 0.08f * (1f - t);
        _musicSourceB.volume = 0.08f * t;

        if (t >= 1f)
        {
            _crossfading = false;
            _musicSource.Stop();
            // Swap sources so _musicSource is always the active one
            (_musicSource, _musicSourceB) = (_musicSourceB, _musicSource);
        }
    }

    public void StopMusic()
    {
        _currentTrack = MusicTrack.None;
        _crossfading = false;
        if (_musicSource != null) { _musicSource.Stop(); _musicSource.volume = 0f; }
        if (_musicSourceB != null) { _musicSourceB.Stop(); _musicSourceB.volume = 0f; }
    }

    void LateUpdate()
    {
        UpdateMusicCrossfade();
    }
}
