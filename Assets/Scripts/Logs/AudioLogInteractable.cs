using System.Collections;
using UnityEngine;

/// <summary>
/// World-placed audio log. Player interacts -> playback starts immediately.
/// Audio continues playing even if player walks away (per design doc),
/// so playback runs on a persistent player, not on this object.
/// </summary>
public class AudioLogInteractable : BaseInteractable
{
    [SerializeField] private AudioLogData data;

    public override void Interact()
    {
        if (!canInteract || data == null) return;

        bool? result = PlayerAudioController.Instance?.PlayLog(data);
        
        if (result == null || result == false) return;
        
        UIManager.Instance?.ShowAudioLog(data);
        CollectibleRegistry.Instance?.MarkDiscovered(data);
        
        StartCoroutine(WaitForClip(data.audioClip.length));

        base.Interact();
    }
    
    IEnumerator WaitForClip(float length)
    {
        yield return new WaitForSeconds(length);
        UIManager.Instance?.StopAudioLog();
    }
}
