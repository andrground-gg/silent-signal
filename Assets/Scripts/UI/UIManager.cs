using UnityEngine;

/// <summary>
/// Central UI manager for collectibles.
/// Owns references to Note/AudioLog UI prefabs (instantiated in scene),
/// turns them on/off and pushes data into them.
/// Interactables call UIManager — they don't know about specific UI classes.
/// </summary>
public class UIManager : Singleton<UIManager>
{
    [Header("UI References (drop scene instances here)")]
    [SerializeField] private NoteReaderUI noteUI;
    [SerializeField] private AudioLogUI audioUI;
    // [SerializeField] private KeeperArchiveUI archiveUI;

    protected void Start()
    {
        // Make sure everything starts hidden.
        if (noteUI != null) noteUI.gameObject.SetActive(false);
        if (audioUI != null) audioUI.gameObject.SetActive(false);
        // if (archiveUI != null) archiveUI.gameObject.SetActive(false);
    }

    // ---------- Note ----------

    public void ShowNote(NoteData data)
    {
        if (noteUI == null || data == null) return;
        noteUI.gameObject.SetActive(true);
        noteUI.SetData(data);
    }

    public void HideNote()
    {
        if (noteUI == null) return;
        noteUI.gameObject.SetActive(false);
    }

    // ---------- Audio Log ----------

    public void ShowAudioLog(AudioLogData data, bool withTranscript = false)
    {
        if (audioUI == null || data == null) return;
        audioUI.gameObject.SetActive(true);
        // audioUI.SetData(data, withTranscript);
    }

    public void StopAudioLog()
    {
        if (audioUI == null) return;
        // audioUI.StopPlayback();
        audioUI.gameObject.SetActive(false);
    }

    // ---------- Archive ----------

    // public void ShowArchive()
    // {
    //     if (archiveUI == null) return;
    //     archiveUI.gameObject.SetActive(true);
    //     archiveUI.Refresh();
    // }
    //
    // public void HideArchive()
    // {
    //     if (archiveUI == null) return;
    //     archiveUI.gameObject.SetActive(false);
    // }
}
