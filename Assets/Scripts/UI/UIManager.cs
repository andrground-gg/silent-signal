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
    [SerializeField] private PopupCanvasUI popupCanvasUIPrefab;
    [SerializeField] private DiscoveryText discoveryText;
    // [SerializeField] private KeeperArchiveUI archiveUI;

    private PopupCanvasUI currentPopup;
    
    protected void Start()
    {
        // Make sure everything starts hidden.
        if (audioUI != null) audioUI.gameObject.SetActive(false);
        if (discoveryText != null) discoveryText.gameObject.SetActive(false);
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

    // ---------- Board ----------

    public void ShowBoardCollectible(CollectibleData data, Transform parent)
    {
        HideBoardCollectible();

        currentPopup = Instantiate(popupCanvasUIPrefab, parent, worldPositionStays: false);
        currentPopup.SetData(data);
        currentPopup.Show();
    }

    public void HideBoardCollectible()
    {
        if (currentPopup == null) return;

        currentPopup.HideAndDestroy();
        currentPopup = null;
    }

    public void ShowDiscoveryText()
    {
        discoveryText.gameObject.SetActive(true);
        discoveryText.ShowDiscovery();
    }
}
