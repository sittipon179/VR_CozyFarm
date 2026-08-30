using UnityEngine;

public static class MaterialColorHelper
{
    public static void SetColor(Renderer targetRenderer, Color color)
    {
        if (targetRenderer == null) return;

        Material mat = targetRenderer.material;

        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", color);
        }
        else if (mat.HasProperty("_Color"))
        {
            mat.SetColor("_Color", color);
        }
    }

    public static Color GetColor(Renderer targetRenderer)
    {
        if (targetRenderer == null) return Color.white;

        Material mat = targetRenderer.material;

        if (mat.HasProperty("_BaseColor"))
        {
            return mat.GetColor("_BaseColor");
        }
        else if (mat.HasProperty("_Color"))
        {
            return mat.GetColor("_Color");
        }

        return Color.white;
    }
}