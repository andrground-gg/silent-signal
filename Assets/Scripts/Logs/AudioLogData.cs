using UnityEngine;

[CreateAssetMenu(fileName = "AudioLog_", menuName = "Collectibles/Audio Log", order = 1)]
public class AudioLogData : CollectibleData
{
    public override CollectibleType Type => CollectibleType.Audio;

    [Header("Audio")]
    public AudioClip audioClip;
}
