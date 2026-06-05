using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles keyboard shortcuts (1-4) for game speed and keeps a UI slider in sync
/// with VillageState.GameSpeed, regardless of where the speed was changed.
/// </summary>
public class GameSpeedController : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("Optional — assign the speed slider so it stays in sync.")]
    [SerializeField] private Slider speedSlider;

    [Tooltip("Optional — displays the current speed value (e.g. '3.4x').")]
    [SerializeField] private TMP_Text speedLabel;

    [Tooltip("Optional — button that pauses/resumes game time (sets timeScale to 0).")]
    [SerializeField] private Button pauseButton;

    [Tooltip("Optional — label on the pause button (shows 'Pause' or 'Resume').")]
    [SerializeField] private TMP_Text pauseButtonLabel;

    // 1 = 0%, 2 = 33%, 3 = 66%, 4 = 100% of the range
    private static readonly float[] SpeedSteps = { 0f, 0.33f, 0.66f, 1f };

    private bool _updatingSlider;
    private bool _isPaused;
    private float _preManualPauseTimeScale;

    void Start()
    {
        if (speedSlider != null)
        {
            speedSlider.minValue = VillageState.MinGameSpeed;
            speedSlider.maxValue = VillageState.MaxGameSpeed;
            speedSlider.value = VillageState.Instance != null ? VillageState.Instance.GameSpeed : 1f;
            speedSlider.onValueChanged.AddListener(OnSliderChanged);
        }

        if (pauseButton != null)
            pauseButton.onClick.AddListener(TogglePause);

        if (VillageState.Instance != null)
        {
            VillageState.Instance.OnGameSpeedChanged += OnSpeedChanged;
            UpdateLabel(VillageState.Instance.GameSpeed);
        }

        UpdatePauseButton();
    }

    void OnDestroy()
    {
        if (speedSlider != null)
            speedSlider.onValueChanged.RemoveListener(OnSliderChanged);

        if (pauseButton != null)
            pauseButton.onClick.RemoveListener(TogglePause);

        if (VillageState.Instance != null)
            VillageState.Instance.OnGameSpeedChanged -= OnSpeedChanged;
    }

    void Update()
    {
        if (VillageState.Instance == null) return;

        if (Input.GetKeyDown(KeyCode.Alpha1))
            SetSpeedByPercent(SpeedSteps[0]);
        else if (Input.GetKeyDown(KeyCode.Alpha2))
            SetSpeedByPercent(SpeedSteps[1]);
        else if (Input.GetKeyDown(KeyCode.Alpha3))
            SetSpeedByPercent(SpeedSteps[2]);
        else if (Input.GetKeyDown(KeyCode.Alpha4))
            SetSpeedByPercent(SpeedSteps[3]);
    }

    private void SetSpeedByPercent(float percent)
    {
        float speed = Mathf.Lerp(VillageState.MinGameSpeed, VillageState.MaxGameSpeed, percent);
        VillageState.Instance.SetGameSpeed(speed);
    }

    public void TogglePause()
    {
        if (_isPaused)
        {
            Time.timeScale = _preManualPauseTimeScale;
            _isPaused = false;
        }
        else
        {
            _preManualPauseTimeScale = VillageState.Instance != null
                ? VillageState.Instance.GameSpeed
                : Time.timeScale;
            Time.timeScale = 0f;
            _isPaused = true;
        }

        UpdatePauseButton();
    }

    private void UpdatePauseButton()
    {
        if (pauseButtonLabel != null)
            pauseButtonLabel.text = _isPaused ? "Resume" : "Pause";
    }

    private void OnSliderChanged(float value)
    {
        if (_updatingSlider || VillageState.Instance == null) return;
        // Resuming via slider should also clear manual pause
        if (_isPaused)
        {
            _isPaused = false;
            UpdatePauseButton();
        }
        VillageState.Instance.SetGameSpeed(value);
    }

    private void OnSpeedChanged(float newSpeed)
    {
        if (speedSlider != null)
        {
            _updatingSlider = true;
            speedSlider.value = newSpeed;
            _updatingSlider = false;
        }

        UpdateLabel(newSpeed);
    }

    private void UpdateLabel(float speed)
    {
        if (speedLabel != null)
            speedLabel.text = $"{speed:F1}x";
    }
}
