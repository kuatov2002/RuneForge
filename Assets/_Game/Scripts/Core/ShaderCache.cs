using UnityEngine;

public static class ShaderCache
{
    public static Shader Lit { get; private set; }
    static Material _litTemplate;
    static Material _emissiveTemplate;
    static Shader _particleShader;

    // Cached custom shaders
    static Shader _iceShader;
    static Shader _lavaShader;
    static Shader _waterShader;
    static Shader _stoneShader;
    static Shader _magicShader;
    static Shader _skinShader;
    static Shader _metalShader;
    static Shader _floorShader;
    static Shader _goldShader;
    static Shader _poisonShader;
    static Shader _voidShader;
    static Shader _fireShader;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        _litTemplate = Resources.Load<Material>("URPLit");
        _emissiveTemplate = Resources.Load<Material>("URPLitEmissive");

        if (_litTemplate != null)
            Lit = _litTemplate.shader;
        else
            Lit = Shader.Find("Universal Render Pipeline/Lit");

        // Particle shader — try URP first, then legacy
        _particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (_particleShader == null)
            _particleShader = Shader.Find("Particles/Standard Unlit");

        // Load custom shaders via Resources so they are included in builds
        _iceShader = LoadShader("ProceduralIce");
        _lavaShader = LoadShader("ProceduralLava");
        _waterShader = LoadShader("ProceduralWater");
        _stoneShader = LoadShader("ProceduralStone");
        _magicShader = LoadShader("ProceduralMagic");
        _skinShader = LoadShader("ProceduralSkin");
        _metalShader = LoadShader("ProceduralMetal");
        _floorShader = LoadShader("ProceduralFloor");
        _goldShader = LoadShader("ProceduralGold");
        _poisonShader = LoadShader("ProceduralPoison");
        _voidShader = LoadShader("ProceduralVoid");
        _fireShader = LoadShader("ProceduralFire");
    }

    static Shader LoadShader(string name)
    {
        var shader = Resources.Load<Shader>("Shaders/" + name);
        if (shader != null) return shader;
        // Fallback to Shader.Find for editor compatibility
        return Shader.Find("RuneForge/" + name);
    }

    // Helper: create material from custom shader, fallback to Lit if shader not found
    static Material MakeCustom(Shader shader, Color color)
    {
        if (shader != null)
        {
            var mat = new Material(shader);
            mat.SetColor("_Color", color);
            return mat;
        }
        return NewEmissive(color, 2f);
    }

    /// Cached particle shader — use instead of Shader.Find("Particles/Standard Unlit")
    public static Shader ParticleShader => _particleShader != null ? _particleShader : Lit;

    // ─── Original methods ───

    public static Material NewLit(Color color)
    {
        var mat = new Material(_litTemplate != null ? _litTemplate : new Material(Lit));
        mat.SetColor("_BaseColor", color);
        mat.color = color;
        return mat;
    }

    public static Material NewEmissive(Color color, float intensity = 3f)
    {
        var mat = new Material(_emissiveTemplate != null ? _emissiveTemplate : new Material(Lit));
        mat.SetColor("_BaseColor", color);
        mat.color = color;
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", color * intensity);
        return mat;
    }

    // ─── Custom shader materials ───

    public static Material NewIce(Color tint, float opacity = 0.7f)
    {
        var mat = MakeCustom(_iceShader, tint);
        mat.SetColor("_RimColor", new Color(0.7f, 0.9f, 1f));
        mat.SetFloat("_RimPower", 2.5f);
        mat.SetFloat("_Opacity", opacity);
        mat.SetFloat("_NoiseScale", 3f);
        mat.SetFloat("_NoiseSpeed", 0.3f);
        return mat;
    }

    public static Material NewLava(Color baseColor)
    {
        var mat = MakeCustom(_lavaShader, baseColor);
        mat.SetColor("_CrackColor", new Color(1f, 0.6f, 0f));
        mat.SetFloat("_CrackIntensity", 3f);
        mat.SetFloat("_FlowSpeed", 0.4f);
        mat.SetFloat("_NoiseScale", 4f);
        return mat;
    }

    public static Material NewWater(Color tint, float opacity = 0.6f)
    {
        var mat = MakeCustom(_waterShader, tint);
        mat.SetColor("_WaveColor", new Color(0.6f, 0.85f, 1f));
        mat.SetFloat("_WaveSpeed", 1.5f);
        mat.SetFloat("_WaveScale", 5f);
        mat.SetFloat("_Opacity", opacity);
        return mat;
    }

    public static Material NewStone(Color baseColor)
    {
        var mat = MakeCustom(_stoneShader, baseColor);
        Color variation = baseColor * 0.7f;
        mat.SetColor("_Color2", variation);
        mat.SetFloat("_NoiseScale", 5f);
        mat.SetFloat("_Roughness", 0.8f);
        return mat;
    }

    public static Material NewMagic(Color color, float intensity = 4f)
    {
        var mat = MakeCustom(_magicShader, color);
        mat.SetFloat("_Intensity", intensity);
        mat.SetFloat("_PulseSpeed", 2f);
        mat.SetFloat("_SwirlSpeed", 1.5f);
        mat.SetFloat("_SwirlScale", 3f);
        return mat;
    }

    public static Material NewSkin(Color color)
    {
        var mat = MakeCustom(_skinShader, color);
        mat.SetColor("_SubsurfaceColor", new Color(0.8f, 0.3f, 0.2f));
        mat.SetFloat("_SubsurfaceStrength", 0.4f);
        return mat;
    }

    public static Material NewMetal(Color color)
    {
        var mat = MakeCustom(_metalShader, color);
        mat.SetFloat("_Metallic", 0.9f);
        mat.SetFloat("_Smoothness", 0.7f);
        mat.SetColor("_ReflectionColor", new Color(0.6f, 0.6f, 0.7f));
        return mat;
    }

    public static Material NewFloor(Color color1, Color color2)
    {
        var mat = MakeCustom(_floorShader, color1);
        mat.SetColor("_Color2", color2);
        mat.SetColor("_GridColor", color1 * 0.5f);
        mat.SetFloat("_TileSize", 1f);
        mat.SetFloat("_GridWidth", 0.03f);
        return mat;
    }

    public static Material NewGold()
    {
        Color goldCol = new Color(1f, 0.85f, 0.2f);
        var mat = MakeCustom(_goldShader, goldCol);
        mat.SetFloat("_SparkleSpeed", 5f);
        mat.SetFloat("_SparkleIntensity", 3f);
        mat.SetFloat("_Metallic", 0.95f);
        return mat;
    }

    public static Material NewPoison(Color tint, float opacity = 0.7f)
    {
        var mat = MakeCustom(_poisonShader, tint);
        mat.SetColor("_BubbleColor", new Color(0.4f, 1f, 0.2f));
        mat.SetFloat("_BubbleSpeed", 1f);
        mat.SetFloat("_BubbleScale", 4f);
        mat.SetFloat("_Opacity", opacity);
        return mat;
    }

    public static Material NewVoid()
    {
        Color voidCol = new Color(0.15f, 0.05f, 0.25f);
        var mat = MakeCustom(_voidShader, voidCol);
        mat.SetColor("_StarColor", new Color(0.8f, 0.6f, 1f));
        mat.SetFloat("_SwirlSpeed", 0.5f);
        mat.SetFloat("_DistortionStrength", 0.3f);
        return mat;
    }

    public static Material NewFire(Color baseColor)
    {
        var mat = MakeCustom(_fireShader, baseColor);
        mat.SetColor("_TopColor", new Color(1f, 0.9f, 0.3f));
        mat.SetFloat("_ScrollSpeed", 2f);
        mat.SetFloat("_NoiseScale", 4f);
        mat.SetFloat("_Intensity", 3f);
        return mat;
    }
}
