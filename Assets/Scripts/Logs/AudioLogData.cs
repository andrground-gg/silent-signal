using UnityEngine;

[CreateAssetMenu(fileName = "AudioLog_", menuName = "Collectibles/Audio Log", order = 1)]
public class AudioLogData : CollectibleData
{
    public override CollectibleType Type => CollectibleType.Audio;

    [Header("Audio")]
    public AudioClip audioClip;

    [TextArea(5, 30)]
    [Tooltip("Transcript shown in keeper's archive next to playback.")]
    public string transcriptText;
}
