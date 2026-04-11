using UnityEngine;

public class PrefabSpawner : MonoBehaviour
{
    [SerializeField] private GameObject _cubePrefab;
    [SerializeField] private int _hourToSpawn;
    [SerializeField] private bool _repeatable;
    private bool _spawned;
    
    private void Start()
    {
        TimeSystem.Instance.OnHourChanged += InstanceOnOnHourChanged;
    }

    private void InstanceOnOnHourChanged(int hour)
    {
        if (!_repeatable && _spawned) return;
        
        if (_hourToSpawn == hour)
        {
            _spawned = true;
            Instantiate(_cubePrefab, transform.position, Quaternion.identity);
        }
    }

    private void OnDestroy()
    {
        TimeSystem.Instance.OnHourChanged -= InstanceOnOnHourChanged;
    }
}
