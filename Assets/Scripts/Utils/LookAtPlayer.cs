using UnityEngine;

public class LookAtPlayer : MonoBehaviour
{
    [SerializeField] private bool onlyYAxis = true;

    private Transform _player;

    private void Start()
    {
        _player = GameManager.Instance?.Player;
    }

    void Update()
    {
        if (_player == null) return;

        Vector3 targetPos = _player.position;

        if (onlyYAxis)
            targetPos.y = transform.position.y;

        transform.LookAt(targetPos);
        transform.Rotate(0f, 180f, 0f);
    }
}