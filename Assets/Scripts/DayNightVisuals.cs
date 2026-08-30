using UnityEngine;

public class DayNightVisuals : MonoBehaviour
{
    [Header("References")]
    public Camera mainCamera;
    public Light sunLight;

    [Header("Sky Background Color Over Time (0 = 00:00, 1 = 24:00)")]
    public Gradient skyColorGradient;

    [Header("Sun/Moon Light Color Over Time")]
    public Gradient lightColorGradient;

    [Header("Sun/Moon Light Intensity Over Time")]
    public AnimationCurve lightIntensityCurve;

    [Header("Sun Rotation")]
    [Tooltip("Fixed horizontal rotation of the light, purely aesthetic.")]
    public float sunYRotation = 170f;

    void Reset()
    {
        skyColorGradient = BuildDefaultSkyGradient();
        lightColorGradient = BuildDefaultLightColorGradient();
        lightIntensityCurve = BuildDefaultIntensityCurve();
    }

    void Update()
    {
        if (TimeManager.Instance == null)
        {
            return;
        }

        float normalizedTime = GetNormalizedTimeOfDay();

        UpdateSkyColor(normalizedTime);
        UpdateSunLight(normalizedTime);
    }

    float GetNormalizedTimeOfDay()
    {
        int totalMinutes = TimeManager.Instance.CurrentHour * 60 + TimeManager.Instance.CurrentMinute;
        return totalMinutes / 1440f;
    }

    void UpdateSkyColor(float normalizedTime)
    {
        if (mainCamera == null || skyColorGradient == null)
        {
            return;
        }

        mainCamera.backgroundColor = skyColorGradient.Evaluate(normalizedTime);
    }

    void UpdateSunLight(float normalizedTime)
    {
        if (sunLight == null)
        {
            return;
        }

        float sunAngle = normalizedTime * 360f - 90f;
        sunLight.transform.rotation = Quaternion.Euler(sunAngle, sunYRotation, 0f);

        if (lightColorGradient != null)
        {
            sunLight.color = lightColorGradient.Evaluate(normalizedTime);
        }

        if (lightIntensityCurve != null)
        {
            sunLight.intensity = lightIntensityCurve.Evaluate(normalizedTime);
        }
    }

    static Gradient BuildDefaultSkyGradient()
    {
        Gradient gradient = new Gradient();

        GradientColorKey[] colorKeys = new GradientColorKey[7];
        colorKeys[0] = new GradientColorKey(HexColor("05050F"), 0.00f);
        colorKeys[1] = new GradientColorKey(HexColor("05050F"), 0.20f);
        colorKeys[2] = new GradientColorKey(HexColor("A8D8EA"), 0.26f);
        colorKeys[3] = new GradientColorKey(HexColor("4FA8E0"), 0.50f);
        colorKeys[4] = new GradientColorKey(HexColor("FF9142"), 0.75f);
        colorKeys[5] = new GradientColorKey(HexColor("2E1F54"), 0.88f);
        colorKeys[6] = new GradientColorKey(HexColor("05050F"), 1.00f);

        GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
        alphaKeys[0] = new GradientAlphaKey(1f, 0f);
        alphaKeys[1] = new GradientAlphaKey(1f, 1f);

        gradient.SetKeys(colorKeys, alphaKeys);
        return gradient;
    }

    static Gradient BuildDefaultLightColorGradient()
    {
        Gradient gradient = new Gradient();

        GradientColorKey[] colorKeys = new GradientColorKey[7];
        colorKeys[0] = new GradientColorKey(HexColor("1B1F3B"), 0.00f);
        colorKeys[1] = new GradientColorKey(HexColor("1B1F3B"), 0.20f);
        colorKeys[2] = new GradientColorKey(HexColor("FFD9A0"), 0.26f);
        colorKeys[3] = new GradientColorKey(HexColor("FFF6E5"), 0.50f);
        colorKeys[4] = new GradientColorKey(HexColor("FFB870"), 0.75f);
        colorKeys[5] = new GradientColorKey(HexColor("3A4270"), 0.88f);
        colorKeys[6] = new GradientColorKey(HexColor("1B1F3B"), 1.00f);

        GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
        alphaKeys[0] = new GradientAlphaKey(1f, 0f);
        alphaKeys[1] = new GradientAlphaKey(1f, 1f);

        gradient.SetKeys(colorKeys, alphaKeys);
        return gradient;
    }

    static AnimationCurve BuildDefaultIntensityCurve()
    {
        Keyframe[] keys = new Keyframe[7];
        keys[0] = new Keyframe(0.00f, 0.05f);
        keys[1] = new Keyframe(0.20f, 0.05f);
        keys[2] = new Keyframe(0.26f, 0.6f);
        keys[3] = new Keyframe(0.50f, 1.2f);
        keys[4] = new Keyframe(0.75f, 0.7f);
        keys[5] = new Keyframe(0.88f, 0.15f);
        keys[6] = new Keyframe(1.00f, 0.05f);

        return new AnimationCurve(keys);
    }

    static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color color);
        return color;
    }
}