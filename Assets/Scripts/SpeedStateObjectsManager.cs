using System.Collections.Generic;
using UnityEngine;

public class SpeedStateObjectsManager : MonoBehaviour
{
    [System.Serializable]
    public class SpeedStateGroup
    {
        public SpeedState state;
        public List<GameObject> objects = new List<GameObject>();
    }

    [SerializeField] private List<SpeedStateGroup> groups = new List<SpeedStateGroup>();

    private Dictionary<SpeedState, List<GameObject>> _map;

    private void Awake()
    {
        BuildMap();
    }
    private void Start()
    {
        if (LighthouseManager.Instance != null)
        {
            LighthouseManager.Instance.OnSpeedChanged += HandleSpeedChanged;
        }
        ApplyState(SpeedState.Normal);
    }

    private void OnDisable()
    {
        if (LighthouseManager.Instance != null)
        {
            LighthouseManager.Instance.OnSpeedChanged -= HandleSpeedChanged;
        }
    }

    private void BuildMap()
    {
        _map = new Dictionary<SpeedState, List<GameObject>>();
        foreach (var group in groups)
        {
            if (!_map.ContainsKey(group.state))
                _map[group.state] = new List<GameObject>();

            _map[group.state].AddRange(group.objects);
        }
    }

    private void HandleSpeedChanged(SpeedState newState)
    {
        ApplyState(newState);
    }

    private void ApplyState(SpeedState activeState)
    {
        foreach (var pair in _map)
        {
            bool isActive = pair.Key == activeState;
            foreach (var obj in pair.Value)
            {
                if (obj != null && obj.activeSelf != isActive)
                    obj.SetActive(isActive);
            }
        }
    }
}