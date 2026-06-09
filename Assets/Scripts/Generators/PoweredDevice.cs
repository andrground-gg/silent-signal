using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

namespace GeneratorSystem
{
    // Anything powered by a specific generator (e.g. the Research Station door).
    // When that generator activates it slides straight down and turns its
    // emission on; when it deactivates or is auto-switched off it slides back up
    // and turns emission off.
    public class PoweredDevice : MonoBehaviour
    {
        [Header("Identity")]
        [Tooltip("Which generator powers this device.")]
        [SerializeField] private GeneratorID _generatorID = GeneratorID.GENERATOR_RESEARCH_STATION;

        [Header("Movement")]
        [Tooltip("Object that actually moves. Defaults to this transform if unset.")]
        [SerializeField] private Transform _door;
        [Tooltip("How far the object slides down (world units) when powered.")]
        [SerializeField] private float _slideDownDistance = 3f;
        [SerializeField] private float _openDuration = 1f;
        [SerializeField] private Ease _ease = Ease.InOutSine;

        [Header("Emission")]
        [Tooltip("Renderer whose emission toggles with power.")]
        [SerializeField] private Renderer _emissionRenderer;
        [Tooltip("Which material slot to drive (3rd material = index 2).")]
        [SerializeField] private int _emissionMaterialIndex = 2;
        [SerializeField] private Color _emissionColor = new Color(0.2f, 1f, 0.2f);
        [SerializeField] private float _emissionIntensity = 2f;
        
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        private Vector3 _closedPosition;
        private bool _isOpen;
        private Tween _tween;
        private MaterialPropertyBlock _mpb;

        private void Awake()
        {
            if (_door == null) _door = transform;
            _closedPosition = _door.position;
            SetEmission(false);
        }

        private void Start()
        {
            GeneratorManager.Instance.OnGeneratorActivated    += HandleActivated;
            GeneratorManager.Instance.OnGeneratorDeactivated  += HandleDeactivated;
            GeneratorManager.Instance.OnGeneratorAutoSwitched += HandleAutoSwitched;

            // Sync to current power state without animating (e.g. on scene load).
            if (GeneratorManager.Instance.IsActive(_generatorID))
                SetOpen(true, animated: false);
        }

        private void OnDisable()
        {
            if (GeneratorManager.Instance == null) return;
            GeneratorManager.Instance.OnGeneratorActivated    -= HandleActivated;
            GeneratorManager.Instance.OnGeneratorDeactivated  -= HandleDeactivated;
            GeneratorManager.Instance.OnGeneratorAutoSwitched -= HandleAutoSwitched;
        }

        private void HandleActivated(GeneratorID id)
        {
            if (id == _generatorID) SetOpen(true, animated: true);
        }

        private void HandleDeactivated(GeneratorID id)
        {
            if (id == _generatorID) SetOpen(false, animated: true);
        }

        private void HandleAutoSwitched(GeneratorID removed, GeneratorID added)
        {
            if (removed == _generatorID) SetOpen(false, animated: true);
        }

        private void SetOpen(bool open, bool animated)
        {
            if (_isOpen == open) return;
            _isOpen = open;

            SetEmission(open);

            Vector3 targetPos = open
                ? _closedPosition + Vector3.down * _slideDownDistance
                : _closedPosition;

            _tween?.Kill();

            if (animated)
            {
                _tween = _door.DOMove(targetPos, _openDuration)
                    .SetEase(_ease);
            }
            else
            {
                _door.position = targetPos;
            }
        }

        private void SetEmission(bool on)
        {
            if (_emissionRenderer == null) return;

            // Material slots are 0-based; guard against an out-of-range index.
            int slotCount = _emissionRenderer.sharedMaterials.Length;
            if (_emissionMaterialIndex < 0 || _emissionMaterialIndex >= slotCount)
            {
                Debug.LogWarning($"[{name}] Emission material index {_emissionMaterialIndex} " +
                                 $"out of range (renderer has {slotCount} materials).", this);
                return;
            }

            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            _emissionRenderer.GetPropertyBlock(_mpb, _emissionMaterialIndex);
            _mpb.SetColor(EmissionColor, on ? _emissionColor * _emissionIntensity : Color.black);
            _emissionRenderer.SetPropertyBlock(_mpb, _emissionMaterialIndex);
        }

    }
}
