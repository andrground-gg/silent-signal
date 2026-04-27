using System.Collections;
using UnityEngine;

namespace GeneratorSystem
{
    [RequireComponent(typeof(AudioSource))]
    public class GeneratorObject : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private GeneratorID _generatorID;

        [Header("Lever")]
        [SerializeField] private Lever _lever;

        [Header("Indicator Light")]
        [SerializeField] private Renderer _indicatorRenderer;
        [SerializeField] private Color    _lightOnColor           = new Color(0.2f, 1f, 0.2f);
        [SerializeField] private Color    _lightOffColor          = new Color(0.05f, 0.1f, 0.05f);
        [SerializeField] private float    _lightEmissionIntensity = 2f;
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        [Header("Audio")]
        [SerializeField] private AudioClip _clipStartup;
        [SerializeField] private AudioClip _clipShutdown;
        [SerializeField] private AudioClip _clipForcedShutdown;
        [SerializeField] private AudioClip _clipHum;
        [SerializeField] private AudioClip _clipLeverClunk;
        [SerializeField] [Range(0f, 1f)] private float _humVolume = 0.35f;

        [Header("Timing")]
        [SerializeField] private float _effectDelay = 0.7f;

        private AudioSource _audio;
        private AudioSource _humSource;
        private bool        _isActive;
        private Coroutine   _flickerCoroutine;

        protected void Awake()
        {
            _audio              = GetComponent<AudioSource>();
            _audio.spatialBlend = 1f;

            _humSource              = gameObject.AddComponent<AudioSource>();
            _humSource.clip         = _clipHum;
            _humSource.loop         = true;
            _humSource.volume       = 0f;
            _humSource.playOnAwake  = false;
            _humSource.spatialBlend = 1f;

            _lever?.SnapToReleased();
        }

        private void Start()
        {
            _lever.OnPulled += Interact;
            _lever.OnRelease += Interact;
            
            GeneratorManager.Instance.OnGeneratorActivated    += HandleActivated;
            GeneratorManager.Instance.OnGeneratorDeactivated  += HandleDeactivated;
            GeneratorManager.Instance.OnGeneratorAutoSwitched += HandleAutoSwitched;
        }

        private void OnDisable()
        {
            _lever.OnPulled -= Interact;
            _lever.OnRelease -= Interact;

            if (GeneratorManager.Instance == null) return;
            GeneratorManager.Instance.OnGeneratorActivated    -= HandleActivated;
            GeneratorManager.Instance.OnGeneratorDeactivated  -= HandleDeactivated;
            GeneratorManager.Instance.OnGeneratorAutoSwitched -= HandleAutoSwitched;
        }

        public void Interact()
        {
            PlaySound(_clipLeverClunk);
            GeneratorManager.Instance.ToggleGenerator(_generatorID);
        }

        private void HandleActivated(GeneratorID id)
        {
            if (id != _generatorID) return;
            _isActive = true;
            StartCoroutine(ActivateSequence());
        }

        private void HandleDeactivated(GeneratorID id)
        {
            if (id != _generatorID) return;
            _isActive = false;
            StartCoroutine(DeactivateSequence(forced: false));
        }

        private void HandleAutoSwitched(GeneratorID removed, GeneratorID added)
        {
            if (removed != _generatorID) return;
            _isActive = false;
            StartCoroutine(DeactivateSequence(forced: true));
        }

        private IEnumerator ActivateSequence()
        {
            PlaySound(_clipStartup);
            SetIndicatorLight(on: true);

            yield return new WaitForSeconds(_effectDelay);

            if (_clipHum != null)
            {
                _humSource.volume = _humVolume;
                if (!_humSource.isPlaying) _humSource.Play();
            }

            GeneratorManager.Instance.NotifyActivated(_generatorID);
        }

        private IEnumerator DeactivateSequence(bool forced)
        {
            if (forced)
            {
                PlaySound(_clipForcedShutdown);
                if (_flickerCoroutine != null) StopCoroutine(_flickerCoroutine);
                _flickerCoroutine = StartCoroutine(FlickerIndicator(times: 4));
            }
            else
            {
                PlaySound(_clipShutdown);
            }
            
            StartCoroutine(FadeHum(to: 0f, duration: 1.2f));

            yield return new WaitForSeconds(0.15f);

            SetIndicatorLight(on: false);
            GeneratorManager.Instance.NotifyDeactivated(_generatorID);
        }

        private void SetIndicatorLight(bool on)
        {
            if (_indicatorRenderer == null) return;
            Material mat = _indicatorRenderer.material;
            Color col = on ? _lightOnColor : _lightOffColor;
            mat.color = col;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor(EmissionColor, on ? col * _lightEmissionIntensity : Color.black);
        }

        private IEnumerator FlickerIndicator(int times)
        {
            for (int i = 0; i < times; i++)
            {
                SetIndicatorLight(on: false);
                yield return new WaitForSeconds(0.06f);
                SetIndicatorLight(on: true);
                yield return new WaitForSeconds(0.09f);
            }
            SetIndicatorLight(on: false);
        }

        private void PlaySound(AudioClip clip)
        {
            if (clip == null) return;
            _audio.PlayOneShot(clip);
        }

        private IEnumerator FadeHum(float to, float duration)
        {
            float start   = _humSource.volume;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                _humSource.volume = Mathf.Lerp(start, to, elapsed / duration);
                yield return null;
            }
            _humSource.volume = to;
            if (to <= 0f) _humSource.Stop();
        }
    }
}
