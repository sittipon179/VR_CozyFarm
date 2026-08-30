using System;
using UnityEngine;

public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance { get; private set; }

    public enum TimeOfDay
    {
        Morning,
        Afternoon,
        Evening,
        Night
    }

    [Header("Time Speed Settings")]
    [Tooltip("In-game seconds that pass for every 1 real second. 72 means 1 in-game day takes about 20 real minutes.")]
    [SerializeField] private float timeMultiplier = 72f;

    [Header("Start Settings")]
    [SerializeField] private int startHour = 6;
    [SerializeField] private int startDay = 1;

    [Header("Time Of Day Boundaries (24 hour format)")]
    [SerializeField] private int morningStartHour = 6;
    [SerializeField] private int afternoonStartHour = 12;
    [SerializeField] private int eveningStartHour = 18;
    [SerializeField] private int nightStartHour = 21;

    private float totalGameSeconds;

    public bool IsTimeRunning { get; private set; } = false;

    public int CurrentDay { get; private set; }
    public int CurrentHour { get; private set; }
    public int CurrentMinute { get; private set; }
    public TimeOfDay CurrentTimeOfDay { get; private set; }

    public event Action<int, int> OnTimeChanged;
    public event Action<int> OnNewDay;
    public event Action<TimeOfDay> OnTimeOfDayChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        CurrentDay = startDay;
        totalGameSeconds = startHour * 3600f;

        RecalculateTime(true);
    }

    private void Update()
    {
        if (!IsTimeRunning)
        {
            return;
        }

        totalGameSeconds += Time.deltaTime * timeMultiplier;
        RecalculateTime(false);
    }

    public void StartTime()
    {
        IsTimeRunning = true;
    }

    public void StopTime()
    {
        IsTimeRunning = false;
    }

    private void RecalculateTime(bool isInitialCall)
    {
        int totalMinutes = Mathf.FloorToInt(totalGameSeconds / 60f);
        int minutesPerDay = 24 * 60;

        int dayOffset = totalMinutes / minutesPerDay;
        int minutesToday = totalMinutes % minutesPerDay;

        int newDay = startDay + dayOffset;
        int newHour = minutesToday / 60;
        int newMinute = minutesToday % 60;

        bool minuteChanged = (newHour != CurrentHour) || (newMinute != CurrentMinute);
        bool dayChanged = (newDay != CurrentDay);

        CurrentHour = newHour;
        CurrentMinute = newMinute;
        CurrentDay = newDay;

        if (isInitialCall)
        {
            UpdateTimeOfDay();
            OnTimeChanged?.Invoke(CurrentHour, CurrentMinute);
            return;
        }

        if (dayChanged)
        {
            OnNewDay?.Invoke(CurrentDay);
        }

        if (minuteChanged)
        {
            OnTimeChanged?.Invoke(CurrentHour, CurrentMinute);
            UpdateTimeOfDay();
        }
    }

    private void UpdateTimeOfDay()
    {
        TimeOfDay newTimeOfDay;

        if (CurrentHour >= nightStartHour || CurrentHour < morningStartHour)
        {
            newTimeOfDay = TimeOfDay.Night;
        }
        else if (CurrentHour >= eveningStartHour)
        {
            newTimeOfDay = TimeOfDay.Evening;
        }
        else if (CurrentHour >= afternoonStartHour)
        {
            newTimeOfDay = TimeOfDay.Afternoon;
        }
        else
        {
            newTimeOfDay = TimeOfDay.Morning;
        }

        if (newTimeOfDay != CurrentTimeOfDay)
        {
            CurrentTimeOfDay = newTimeOfDay;
            OnTimeOfDayChanged?.Invoke(CurrentTimeOfDay);
        }
    }

    public bool IsWithinTimeWindow(int targetHour, int targetMinute, int toleranceMinutes)
    {
        int targetTotal = targetHour * 60 + targetMinute;
        int currentTotal = CurrentHour * 60 + CurrentMinute;

        int diff = Mathf.Abs(currentTotal - targetTotal);
        int diffWrapped = Mathf.Min(diff, (24 * 60) - diff);

        return diffWrapped <= toleranceMinutes;
    }

    public string GetFormattedTime()
    {
        return string.Format("{0:00}:{1:00}", CurrentHour, CurrentMinute);
    }

    public void SkipToTimeNextDay(int targetHour, int targetMinute)
    {
        int minutesPerDay = 24 * 60;
        int currentTotalMinutes = Mathf.FloorToInt(totalGameSeconds / 60f);
        int currentDayIndex = currentTotalMinutes / minutesPerDay;
        int nextDayIndex = currentDayIndex + 1;
        int targetMinutesOfDay = targetHour * 60 + targetMinute;

        int newTotalMinutes = nextDayIndex * minutesPerDay + targetMinutesOfDay;
        totalGameSeconds = newTotalMinutes * 60f;

        RecalculateTime(false);
    }
}