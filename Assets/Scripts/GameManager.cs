using UnityEngine;

public class GameManager : Singleton<GameManager>
{
    [SerializeField] private Transform player;

    public Transform Player => player;

    private void OnValidate()
    {
        FindPlayer();
    }

    private void FindPlayer()
    {
        if (player != null) return;

        GameObject found = GameObject.FindGameObjectWithTag("Player");

        if (found != null)
            player = found.transform;
    }
}
