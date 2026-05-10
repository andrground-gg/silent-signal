using UnityEngine;

public class RespawnManager : MonoBehaviour
{
    [SerializeField] private InteractionZone waterInteractionZone;
    [SerializeField] private Transform respawnPoint;
    
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

    private void OnEnteredWaterInteractionZone()
    {
        cc.enabled = false;
        transform.position = respawnPoint.position;
        cc.enabled = true;
    }
}
