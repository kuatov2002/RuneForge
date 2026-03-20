using UnityEngine;

public static class ShaderCache
{
    public static Shader Lit { get; private set; }
    static Material _litTemplate;
    static Material _emissiveTemplate;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        _litTemplate = Resources.Load<Material>("URPLit");
        _emissiveTemplate = Resources.Load<Material>("URPLitEmissive");

        if (_litTemplate != null)
            Lit = _litTemplate.shader;
        else
            Lit = Shader.Find("Universal Render Pipeline/Lit");
    }

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
}
