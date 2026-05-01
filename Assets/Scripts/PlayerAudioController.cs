using System.Collections;
using UnityEngine;

public class PlayerAudioController : Singleton<PlayerAudioController>
{
    [SerializeField] private AudioSource logAudioSource;

    private bool _isPlayingLog;

    public bool PlayLog(AudioLogData data)
    {
        if (_isPlayingLog) return false;
        _isPlayingLog = true;
        logAudioSource.PlayOneShot(data.audioClip);
        StartCoroutine(WaitForClip(data.audioClip.length));
        return true;
    }
    
    IEnumerator WaitForClip(float length)
    {
        yield return new WaitForSeconds(length);
        OnAudioLogFinished();
    }

    private void OnAudioLogFinished()
    {
        _isPlayingLog = false;
    }
}
