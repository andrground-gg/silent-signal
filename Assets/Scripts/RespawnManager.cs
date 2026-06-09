using UnityEngine;

public class RespawnManager : MonoBehaviour
{
    [SerializeField] private InteractionZone waterInteractionZone;
    [SerializeField] private Transform respawnPoint;
    [SerializeField] private KeyCode respawnKey = KeyCode.R;

    CharacterController cc;

    private void Start()
    {
        waterInteractionZone.OnEnteredInteractionZone += OnEnteredWaterInteractionZone;
        cc = GetComponent<CharacterController>();
    }

    private void OnDisable()
    {
        waterInteractionZone.OnEnteredInteractionZone -= OnEnteredWaterInteractionZone;
    }

    private void Update()
    {
        if (Input.GetKeyDown(respawnKey))
            Respawn();
    }

    private void OnEnteredWaterInteractionZone() => Respawn();

    private void Respawn()
    {
        cc.enabled = false;
        transform.position = respawnPoint.position;
        cc.enabled = true;
    }
}
