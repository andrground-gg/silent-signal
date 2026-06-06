using System;
using DG.Tweening;
using UnityEngine;

public class ResonanceEmitter : MonoBehaviour
{
    public enum EmitterState { Off, Active }

    [Header("Activation")]
    [Tooltip("Button that triggers the emitter. Pressing it starts the sequence.")]
    [SerializeField] private ButtonInteractable activationButton;

    [Header("Player")]
    [Tooltip("Control is removed after the cry. Found automatically if left empty.")]
    [SerializeField] private PlayerController player;

    [Header("Audio — Idle")]
    [SerializeField] private AudioSource ambientHumSound;

    [Header("Audio — Activation")]
    [SerializeField] private AudioSource mechanicalStartupSound;

    [Header("Audio — Failure")]
    [SerializeField] private AudioSource shutdownSound;

    [Header("Audio — Endgame")]
    [SerializeField] private AudioSource successResonanceSound;
    [SerializeField] private AudioSource caveResonanceSound;
    [Tooltip("Distorted, animal-like noise rising from underwater.")]
    [SerializeField] private AudioSource underwaterNoiseSound;
    [Tooltip("A second underwater noise, a couple seconds after the first.")]
    [SerializeField] private AudioSource underwaterNoiseSound2;

    [Header("Endgame — Moving Part")]
    [Tooltip("The part that pumps up and down during the endgame.")]
    [SerializeField] private Transform movingPart;
    [Tooltip("Local axis the part travels along when pumping.")]
    [SerializeField] private Vector3 pumpAxis = Vector3.up;
    [Tooltip("How far the part rises (and falls back) each pump, in local units.")]
    [SerializeField] private float pumpDistance = 5f;
    [Tooltip("Time for one up move (and the same for one down move).")]
    [SerializeField] private float pumpDuration = 0.4f;
    [SerializeField] private Ease pumpUpEase   = Ease.OutQuad;
    [SerializeField] private Ease pumpDownEase = Ease.InQuad;
    [Tooltip("Plays on each pump — the impact thud at the bottom.")]
    [SerializeField] private AudioSource pumpSound;

    [Header("Endgame — Camera Shake")]
    [Tooltip("Camera gets a jolt on each pump, growing stronger every time.")]
    [SerializeField] private CameraShake cameraShake;
    [Tooltip("Jolt strength on the very first pump.")]
    [SerializeField] private float shakeStartAmplitude = 0.05f;
    [Tooltip("Added to the jolt strength on every following pump.")]
    [SerializeField] private float shakePerPump = 0.04f;
    [Tooltip("Upper cap so it doesn't grow forever.")]
    [SerializeField] private float maxShakeAmplitude = 0.5f;
    [SerializeField] private float shakeFrequency = 22f;

    [Header("Endgame — Canvas")]
    [Tooltip("Fullscreen canvas faded in (via alpha) after the cry.")]
    [SerializeField] private CanvasGroup blackScreen;
    [SerializeField] private float canvasFadeDuration = 3f;

    [Header("Endgame — Timing")]
    [SerializeField] private float buildupDuration   = 6f;
    [SerializeField] private float underwaterNoiseDelay = 2.5f;
    [Tooltip("Gap after the first underwater noise before the second one plays.")]
    [SerializeField] private float secondUnderwaterNoiseGap = 2f;
    [SerializeField] private float holdAtPeak        = 1f;
    [SerializeField] private float blackoutDuration  = 2f;
    [Tooltip("How long to stay on the black screen after the game ends before quitting.")]
    [SerializeField] private float quitDelay         = 0f;

    [Header("Failure")]
    [SerializeField] private Transform[] vibratingParts;
    [SerializeField] private float failVibrationStrength = 0.05f;
    [SerializeField] private float failVibrationDuration = 1.5f;
    [SerializeField] private float failShutdownDelay     = 2f;

    public EmitterState State      { get; private set; }
    public bool         IsUnlocked { get; private set; }

    public event Action OnActivationSuccess;
    public event Action OnActivationFailure;
    public event Action OnGameEnded;

    private Sequence _sequence;
    private bool     _isActivating;

    // Endgame runtime state.
    private Vector3 _movingPartRestPos;
    private Tween   _pumpTween;
    private bool    _endgame;
    private int     _endgamePumpIndex;

    private void Awake()
    {
        if (activationButton != null)
        {
            activationButton.OnPressed += OnButtonPressed;
            activationButton.OnFailure += OnButtonFailure;
        }
    }

    private void OnDestroy()
    {
        if (activationButton != null)
        {
            activationButton.OnPressed -= OnButtonPressed;
            activationButton.OnFailure -= OnButtonFailure;
        }
        _sequence?.Kill();
        _pumpTween?.Kill();
    }

    public void Unlock()
    {
        if (IsUnlocked) return;
        IsUnlocked = true;
    }

    // Fired by the button only when its condition passed (emitter unlocked).
    private void OnButtonPressed()
    {
        if (_isActivating) return;

        StartEndgame();
    }

    // Fired by the button when pressed while its condition was not met (locked).
    private void OnButtonFailure() => PlayFailureSequence();

    // ---------- Endgame ----------

    private void StartEndgame()
    {
        _isActivating = true;
        State         = EmitterState.Active;

        // Ambient hum turns on with activation (not at game start) and keeps droning.
        if (ambientHumSound != null) { ambientHumSound.loop = true; ambientHumSound.Play(); }
        mechanicalStartupSound?.Play();
        successResonanceSound?.Play();
        caveResonanceSound?.Play();

        if (blackScreen != null)
        {
            blackScreen.alpha = 0f;
            blackScreen.gameObject.SetActive(true);
        }

        // Start the pump now — once going it runs forever, driving the growing shake.
        StartPump();
        _endgame = true;
        _endgamePumpIndex = 0;

        float secondNoiseTime = underwaterNoiseDelay + secondUnderwaterNoiseGap;
        float window = Mathf.Max(buildupDuration, secondNoiseTime);

        _sequence?.Kill();
        _sequence = DOTween.Sequence();

        // Buildup window — sized so both underwater noises land inside it.
        _sequence.AppendInterval(window);

        // First distorted underwater noise — the cry that seizes the screen...
        _sequence.InsertCallback(underwaterNoiseDelay, FirstCry);
        // ...then a second one a couple seconds later.
        _sequence.InsertCallback(secondNoiseTime, () => underwaterNoiseSound2?.Play());

        // Hold for a beat, then run the endgame: silence the layers.
        _sequence.AppendInterval(holdAtPeak);
        _sequence.AppendCallback(BeginBlackout);

        OnActivationSuccess?.Invoke();
    }

    // UI logic: the single cry rips control from the player and fades the canvas in.
    private void FirstCry()
    {
        underwaterNoiseSound?.Play();

        if (player == null) player = FindObjectOfType<PlayerController>();
        if (player != null) player.IsInspecting = true;

        if (blackScreen != null)
        {
            blackScreen.gameObject.SetActive(true);
            blackScreen.blocksRaycasts = true;
            blackScreen.DOFade(1f, canvasFadeDuration).SetEase(Ease.InOutSine);
        }
    }

    // Loops the moving part up by pumpDistance and back, thudding each cycle.
    private void StartPump()
    {
        if (movingPart == null) return;

        _movingPartRestPos = movingPart.localPosition;
        // localPosition is in PARENT space — rotate the axis by the part's local
        // rotation so it travels along its own up even when the object is tilted.
        Vector3 axis = pumpAxis == Vector3.zero ? Vector3.up : pumpAxis.normalized;
        Vector3 top  = _movingPartRestPos + movingPart.localRotation * (axis * pumpDistance);

        _pumpTween?.Kill();
        _pumpTween = DOTween.Sequence()
            .Append(movingPart.DOLocalMove(top, pumpDuration).SetEase(pumpUpEase))
            .Append(movingPart.DOLocalMove(_movingPartRestPos, pumpDuration).SetEase(pumpDownEase))
            .AppendCallback(OnPump)
            .SetLoops(-1);
    }

    // Every time the part slams back down: thud always; during the endgame it
    // also jolts the camera, growing stronger with each pump.
    private void OnPump()
    {
        pumpSound?.Play();

        if (_endgame && cameraShake != null)
        {
            float amp = Mathf.Min(maxShakeAmplitude, shakeStartAmplitude + shakePerPump * _endgamePumpIndex);
            cameraShake.Kick(amp, pumpDuration, shakeFrequency);
            _endgamePumpIndex++;
        }
    }

    private void BeginBlackout()
    {
        // Fade out only the endgame layers — the ambient hum and the pump keep
        // droning under the black screen.
        FadeOut(mechanicalStartupSound);
        FadeOut(successResonanceSound);
        FadeOut(caveResonanceSound);
        FadeOut(underwaterNoiseSound);
        FadeOut(underwaterNoiseSound2);

        DOVirtual.DelayedCall(blackoutDuration, EndGame);
    }

    private void FadeOut(AudioSource src)
    {
        if (src == null) return;
        src.DOFade(0f, blackoutDuration).SetEase(Ease.InOutSine).OnComplete(() => src.Stop());
    }

    private void EndGame()
    {
        _isActivating = false;
        _endgame      = false;

        // Screen is black; the endgame layers are gone, but the machine keeps
        // pumping and the ambient keeps droning. The game is over.
        OnGameEnded?.Invoke();

        // Stay on the black screen for a beat, then quit the game.
        DOVirtual.DelayedCall(quitDelay, QuitGame);
    }

    private static void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------- Failure ----------

    private void PlayFailureSequence()
    {
        _isActivating = true;

        _sequence?.Kill();
        _sequence = DOTween.Sequence()
            .AppendCallback(() =>
            {
                shutdownSound?.Play();
                VibrateAll(failVibrationStrength, failVibrationDuration);
            })
            .AppendInterval(failVibrationDuration * 0.5f + failShutdownDelay)
            .AppendInterval(0.5f)
            .AppendCallback(() =>
            {
                _isActivating = false;
                OnActivationFailure?.Invoke();
            });
    }

    private void VibrateAll(float strength, float duration)
    {
        foreach (var part in vibratingParts)
            if (part != null)
                part.DOShakePosition(duration, strength, 30, 90f, false, false)
                    .SetLink(part.gameObject);
    }
}
