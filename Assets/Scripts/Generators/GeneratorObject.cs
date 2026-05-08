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
        [SerializeField] private AudioSource startUpSound;
        [SerializeField] private AudioSource shutdownSound;
        [SerializeField] private AudioSource humSound;
        [SerializeField] private AudioSource leverClunkSound;

        [Header("Timing")]
        [SerializeField] private float _effectDelay = 0.7f;

        private bool        _isActive;
        private Coroutine   _flickerCoroutine;
        private float       _humInitialVolume;
        private MaterialPropertyBlock _mpb;
        
        protected void Awake()
        {
            _lever?.SnapToReleased();

            _humInitialVolume = humSound.volume;
        }

        private void Start()
        {
            _lever.OnPulled += Interact;
            _lever.OnRelease += Interact;
            SetIndicatorLight(on: false);
            
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
            leverClunkSound.Play();
            GeneratorManager.Instance.ToggleGenerator(_generatorID);
        }

        private void HandleActivated(GeneratorID id)
        {
            if (id != _generatorID) return;
            Debug.Log($"[{gameObject.name}] HandleActivated received id={id}, my id={_generatorID}, match={id == _generatorID}", this);
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
            startUpSound.Play();
            SetIndicatorLight(on: true);

            yield return new WaitForSeconds(_effectDelay);

            if (humSound != null)
            {
                humSound.volume = _humInitialVolume;
                humSound.Play();
            }

            GeneratorManager.Instance.NotifyActivated(_generatorID);
        }

        private IEnumerator DeactivateSequence(bool forced)
        {
            if (forced)
            {
                shutdownSound.Play();
                if (_flickerCoroutine != null) StopCoroutine(_flickerCoroutine);
                _flickerCoroutine = StartCoroutine(FlickerIndicator(times: 4));
            }
            else
            {
                shutdownSound.Play();
            }
            
            StartCoroutine(FadeHum(to: 0f, duration: 1.2f));

            yield return new WaitForSeconds(0.15f);

            SetIndicatorLight(on: false);
            GeneratorManager.Instance.NotifyDeactivated(_generatorID);
        }

        private void SetIndicatorLight(bool on)
        {
            if (_indicatorRenderer == null) return;

            Debug.Log($"[{gameObject.name}] SetIndicatorLight({on}) → renderer: {_indicatorRenderer.gameObject.name} (id: {_indicatorRenderer.GetInstanceID()})", this);

            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            _indicatorRenderer.GetPropertyBlock(_mpb);

            Color emission = on
                ? _lightOnColor  * _lightEmissionIntensity
                : _lightOffColor * _lightEmissionIntensity;

            _mpb.SetColor(EmissionColor, emission);
            _indicatorRenderer.SetPropertyBlock(_mpb);
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

        private IEnumerator FadeHum(float to, float duration)
        {
            float start   = humSound.volume;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                humSound.volume = Mathf.Lerp(start, to, elapsed / duration);
                yield return null;
            }
            humSound.volume = to;
            if (to <= 0f) humSound.Stop();
        }
    }
}
