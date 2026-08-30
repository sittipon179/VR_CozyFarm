using UnityEngine;
using TMPro;

public class TimeUIDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI timeText;

    private void Start()
    {
        if (TimeManager.Instance == null)
        {
            Debug.LogError("TimeUIDisplay: TimeManager.Instance was not found in the scene.");
            return;
        }

        TimeManager.Instance.OnTimeChanged += HandleTimeChanged;
        TimeManager.Instance.OnNewDay += HandleNewDay;

        UpdateDisplay(TimeManager.Instance.CurrentDay, TimeManager.Instance.CurrentHour, TimeManager.Instance.CurrentMinute);
    }

    private void OnDestroy()
    {
        if (TimeManager.Instance == null)
        {
            return;
        }

        TimeManager.Instance.OnTimeChanged -= HandleTimeChanged;
        TimeManager.Instance.OnNewDay -= HandleNewDay;
    }

    private void HandleTimeChanged(int hour, int minute)
    {
        UpdateDisplay(TimeManager.Instance.CurrentDay, hour, minute);
    }

    private void HandleNewDay(int day)
    {
        UpdateDisplay(day, TimeManager.Instance.CurrentHour, TimeManager.Instance.CurrentMinute);
    }

    private void UpdateDisplay(int day, int hour, int minute)
    {
        if (timeText == null)
        {
            return;
        }

        timeText.text = string.Format("Day {0}\n{1:00}:{2:00}", day, hour, minute);
    }
}