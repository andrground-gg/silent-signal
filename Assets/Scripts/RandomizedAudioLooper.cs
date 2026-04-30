using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class RandomizedAudioLooper : MonoBehaviour
{
    [Header("Play / Break Durations (seconds)")]
    public float playDurationMin = 5f;
    public float playDurationMax = 15f;
    public float breakDurationMin = 3f;
    public float breakDurationMax = 8f;

    [Header("Fading")]
    public float fadeDuration = 1f;
    private float targetVolume;

    private AudioSource audioSource;
    private Coroutine cycleCoroutine;
    private bool running;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();

        targetVolume = audioSource.volume;
    }

    void OnEnable()
    {
        StartCycle();
    }

    void OnDisable()
    {
        StopCycle();
    }

    void OnValidate()
    {
        if (playDurationMin < 0f) playDurationMin = 0f;
        if (breakDurationMin < 0f) breakDurationMin = 0f;
        if (playDurationMax < playDurationMin) playDurationMax = playDurationMin;
        if (breakDurationMax < breakDurationMin) breakDurationMax = breakDurationMin;
        if (fadeDuration < 0f) fadeDuration = 0f;
        targetVolume = Mathf.Clamp01(targetVolume);
    }

    public void StartCycle()
    {
        if (running) return;
        running = true;
        if (cycleCoroutine != null) StopCoroutine(cycleCoroutine);
        cycleCoroutine = StartCoroutine(CycleRoutine());
    }

    public void StopCycle()
    {
        if (!running) return;
        running = false;
        if (cycleCoroutine != null) StopCoroutine(cycleCoroutine);
        cycleCoroutine = null;
        StartCoroutine(FadeOutAndPause());
    }

    IEnumerator CycleRoutine()
    {
        audioSource.loop = true;

        // ensure we start muted and play, then fade in
        audioSource.volume = 0f;
        audioSource.Play();
        yield return FadeToVolume(targetVolume);

        while (running)
        {
            float playTime = Random.Range(playDurationMin, playDurationMax);
            yield return WaitSeconds(playTime);

            // fade out then pause
            yield return FadeToVolume(0f);
            audioSource.Pause();

            float breakTime = Random.Range(breakDurationMin, breakDurationMax);
            yield return WaitSeconds(breakTime);

            // resume and fade in
            audioSource.UnPause();
            yield return FadeToVolume(targetVolume);
        }
    }

    IEnumerator FadeToVolume(float to)
    {
        float from = audioSource.volume;
        if (Mathf.Approximately(from, to) || fadeDuration <= 0f)
        {
            audioSource.volume = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / fadeDuration));
            yield return null;
        }

        audioSource.volume = to;
    }

    IEnumerator FadeOutAndPause()
    {
        yield return FadeToVolume(0f);
        audioSource.Pause();
    }

    IEnumerator WaitSeconds(float seconds)
    {
        yield return new WaitForSeconds(seconds);
    }
}
