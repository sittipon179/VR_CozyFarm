using System;
using UnityEngine;

public class SleepManager : MonoBehaviour
{
    public static SleepManager Instance { get; private set; }

    public enum SleepState
    {
        Awake,
        FadingOut,
        HoldingBlack,
        FadingIn
    }

    [Header("References")]
    public FirstPersonController firstPersonController;

    [Header("Sleep Time Rules (24 hour format)")]
    [Tooltip("Sleeping before this hour counts as sleeping on time.")]
    public int goodSleepDeadlineHour = 22;
    [Tooltip("If the player has not slept by this hour, they pass out automatically.")]
    public int forcedPassOutHour = 2;

    [Header("Wake Up Times")]
    public int goodWakeHour = 6;
    public int lateWakeHourEarly = 9;
    public int lateWakeHourLate = 10;
    public int forcedWakeHour = 12;

    [Header("Fade Timing")]
    [Tooltip("Real-world seconds the screen stays fully black before fading back in.")]
    public float blackHoldDuration = 1f;

    [Header("Forced Wake Grogginess")]
    [Range(0.1f, 1f)]
    public float groggySpeedMultiplier = 0.5f;
    public float groggyRecoveryDuration = 20f;

    public SleepState CurrentState { get; private set; } = SleepState.Awake;
    public bool CanSleepNow => CurrentState == SleepState.Awake;

    public event Action<bool> OnWakeUp;

    private int pendingWakeHour;
    private int pendingWakeMinute;
    private bool pendingIsForced;
    private float blackHoldTimer;

    private float originalMoveSpeed;
    private float originalMouseSensitivity;
    private float groggyTimer;
    private bool isRecoveringFromGroggy;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        if (firstPersonController != null)
        {
            originalMoveSpeed = firstPersonController.moveSpeed;
            originalMouseSensitivity = firstPersonController.mouseSensitivity;
        }
    }

    void Update()
    {
        if (TimeManager.Instance == null || ScreenFadeController.Instance == null)
        {
            return;
        }

        switch (CurrentState)
        {
            case SleepState.Awake:
                UpdateFatigueWarning();
                CheckForForcedPassOut();
                break;
            case SleepState.FadingOut:
                UpdateFadingOut();
                break;
            case SleepState.HoldingBlack:
                UpdateHoldingBlack();
                break;
            case SleepState.FadingIn:
                UpdateFadingIn();
                break;
        }

        UpdateGroggyRecovery();
    }

    void UpdateFatigueWarning()
    {
        int hour = TimeManager.Instance.CurrentHour;
        int minute = TimeManager.Instance.CurrentMinute;

        if (hour >= forcedPassOutHour)
        {
            ScreenFadeController.Instance.SetFatigueWarning(0f);
            return;
        }

        float currentMinutesOfDay = hour * 60f + minute;
        float warningEndMinutes = forcedPassOutHour * 60f;

        float t = Mathf.InverseLerp(0f, warningEndMinutes, currentMinutesOfDay);
        ScreenFadeController.Instance.SetFatigueWarning(t);
    }

    void CheckForForcedPassOut()
    {
        int hour = TimeManager.Instance.CurrentHour;
        int minute = TimeManager.Instance.CurrentMinute;

        if (hour == forcedPassOutHour && minute == 0)
        {
            BeginForcedPassOut();
        }
    }

    // Called by BedInteractable when the player interacts with the bed
    public bool TryToSleep()
    {
        if (CurrentState != SleepState.Awake)
        {
            return false;
        }

        int hour = TimeManager.Instance.CurrentHour;

        if (hour < forcedPassOutHour)
        {
            pendingWakeHour = lateWakeHourLate;
        }
        else if (hour >= goodSleepDeadlineHour)
        {
            pendingWakeHour = lateWakeHourEarly;
        }
        else
        {
            pendingWakeHour = goodWakeHour;
        }

        pendingWakeMinute = 0;
        pendingIsForced = false;

        BeginFadingOut();
        return true;
    }

    void BeginForcedPassOut()
    {
        pendingWakeHour = forcedWakeHour;
        pendingWakeMinute = 0;
        pendingIsForced = true;

        BeginFadingOut();
    }

    void BeginFadingOut()
    {
        CurrentState = SleepState.FadingOut;

        if (firstPersonController != null)
        {
            firstPersonController.enabled = false;
        }

        ScreenFadeController.Instance.SetFatigueWarning(0f);
        ScreenFadeController.Instance.FadeToBlack();
    }

    void UpdateFadingOut()
    {
        if (ScreenFadeController.Instance.CurrentFadeAlpha >= 0.99f)
        {
            CurrentState = SleepState.HoldingBlack;
            blackHoldTimer = 0f;

            TimeManager.Instance.SkipToTimeNextDay(pendingWakeHour, pendingWakeMinute);
        }
    }

    void UpdateHoldingBlack()
    {
        blackHoldTimer += Time.deltaTime;

        if (blackHoldTimer >= blackHoldDuration)
        {
            CurrentState = SleepState.FadingIn;
            ScreenFadeController.Instance.FadeFromBlack();
        }
    }

    void UpdateFadingIn()
    {
        if (ScreenFadeController.Instance.CurrentFadeAlpha <= 0.01f)
        {
            CurrentState = SleepState.Awake;

            if (firstPersonController != null)
            {
                firstPersonController.enabled = true;
            }

            if (pendingIsForced)
            {
                BeginGroggyRecovery();
            }

            OnWakeUp?.Invoke(pendingIsForced);
        }
    }

    void BeginGroggyRecovery()
    {
        if (firstPersonController == null)
        {
            return;
        }

        isRecoveringFromGroggy = true;
        groggyTimer = 0f;

        firstPersonController.moveSpeed = originalMoveSpeed * groggySpeedMultiplier;
        firstPersonController.mouseSensitivity = originalMouseSensitivity * groggySpeedMultiplier;
    }

    void UpdateGroggyRecovery()
    {
        if (!isRecoveringFromGroggy || firstPersonController == null)
        {
            return;
        }

        groggyTimer += Time.deltaTime;
        float t = Mathf.Clamp01(groggyTimer / groggyRecoveryDuration);

        firstPersonController.moveSpeed = Mathf.Lerp(originalMoveSpeed * groggySpeedMultiplier, originalMoveSpeed, t);
        firstPersonController.mouseSensitivity = Mathf.Lerp(originalMouseSensitivity * groggySpeedMultiplier, originalMouseSensitivity, t);

        if (t >= 1f)
        {
            isRecoveringFromGroggy = false;
        }
    }
}