using UnityEngine;
using Sydewa;

// Bridges the game's own day/night clock (TimeManager) into the "Day/Night System with Events
// and Custom Lighting" asset's LightingManager component. LightingManager can run its own
// internal clock (IsDayCycleOn), but this project already has a clock -- TimeManager, which
// sleeping/story events advance and which SleepManager/HouseDoorInteractable pause during
// transitions -- so LightingManager's own clock is left OFF and this script feeds it
// TimeManager's current hour/minute every time TimeManager reports a change instead. That way
// there's exactly one source of truth for "what time it is" and the sky/lighting always agree
// with the in-game clock the rest of the game already uses.
[RequireComponent(typeof(LightingManager))]
public class TimeManagerLightingSync : MonoBehaviour
{

private bool subscribed;

    private void TrySubscribe()
    {
        if (subscribed || TimeManager.Instance == null)
        {
            return;
        }

        TimeManager.Instance.OnTimeChanged += HandleTimeChanged;
        subscribed = true;
    }

    private LightingManager lightingManager;

    void Awake()
    {
        lightingManager = GetComponent<LightingManager>();
    }

void OnEnable()
    {
        // TimeManager.Instance may not be assigned yet if this component's OnEnable happens to
        // run before TimeManager's own Awake() (Unity does not guarantee Awake() order between
        // different components) -- Start() below is the reliable place to subscribe, since Unity
        // guarantees every object's Awake() has already run by the time any Start() runs. This
        // OnEnable is kept only to resubscribe on being re-enabled after Start already ran once.
        if (subscribed)
        {
            return;
        }

        TrySubscribe();
    }

void OnDisable()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnTimeChanged -= HandleTimeChanged;
        }
        subscribed = false;
    }

void Start()
    {
        // By Start(), every object's Awake() in the scene (including TimeManager's, which
        // assigns TimeManager.Instance) is guaranteed to have already run, so this is the
        // reliable place to subscribe -- unlike OnEnable(), whose order relative to
        // TimeManager.Awake() is not guaranteed.
        TrySubscribe();
        ApplyCurrentTime();
    }

    private void HandleTimeChanged(int hour, int minute)
    {
        SetTimeOfDay(hour, minute);
    }

    private void ApplyCurrentTime()
    {
        if (TimeManager.Instance == null)
        {
            return;
        }

        SetTimeOfDay(TimeManager.Instance.CurrentHour, TimeManager.Instance.CurrentMinute);
    }

    private void SetTimeOfDay(int hour, int minute)
    {
        if (lightingManager == null)
        {
            return;
        }

        lightingManager.TimeOfDay = hour + (minute / 60f);
    }
}
