using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Physical investigation board.
/// Unlocks are queued — they don't appear immediately. The queue plays out
/// when the player enters the board's InteractionZone, scaling each piece
/// in via DOTween with a delay between them.
///
/// Connections between nodes are activated automatically whenever both
/// endpoint keys are revealed.
/// </summary>
public class InvestigationBoard : MonoBehaviour
{
    [Header("Board Entries")]
    [SerializeField] private List<BoardInteractable> entries = new List<BoardInteractable>();

    [Header("Connections")]
    [Tooltip("Strings/lines between nodes. Each becomes visible when both its endpoint keys are revealed.")]
    [SerializeField] private List<BoardConnection> connections = new List<BoardConnection>();

    [Header("Trigger")]
    [Tooltip("Zone the player walks into to play the queued unlocks.")]
    [SerializeField] private InteractionZone interactionZone;

    [Header("Animation")]
    [Tooltip("Pause between each piece scaling in.")]
    [SerializeField] private float intervalSeconds = 0.5f;

    [SerializeField] private float scaleDuration = 0.35f;
    [SerializeField] private Ease scaleEase = Ease.OutBack;

    private readonly Dictionary<LogKeys, BoardInteractable> map = new Dictionary<LogKeys, BoardInteractable>();
    private readonly HashSet<LogKeys> revealed = new HashSet<LogKeys>();
    private readonly HashSet<BoardConnection> activatedConnections = new HashSet<BoardConnection>();
    private readonly Queue<LogKeys> pendingQueue = new Queue<LogKeys>();

    private Coroutine playRoutine;

    private void Awake()
    {
        if (interactionZone != null)
            interactionZone.OnEnteredInteractionZone += HandleZoneEntered;
    }

    private void Start()
    {
        BuildMap();
        HideAll();
        RestoreFromRegistry();

        if (CollectibleRegistry.Instance != null)
            CollectibleRegistry.Instance.OnFirstDiscovery += HandleFirstDiscovery;
    }

    private void OnDestroy()
    {
        if (interactionZone != null)
            interactionZone.OnEnteredInteractionZone -= HandleZoneEntered;

        if (CollectibleRegistry.Instance != null)
            CollectibleRegistry.Instance.OnFirstDiscovery -= HandleFirstDiscovery;
    }

    // ---------- Setup ----------

    private void BuildMap()
    {
        map.Clear();
        foreach (var entry in entries)
        {
            if (entry == null || entry.Data == null) continue;
            if (entry.Data.key == LogKeys.None) continue;
            if (!map.ContainsKey(entry.Data.key))
                map[entry.Data.key] = entry;
        }
    }

    private void HideAll()
    {
        foreach (var entry in entries)
            if (entry != null) entry.gameObject.SetActive(false);

        foreach (var conn in connections)
            if (conn != null) conn.gameObject.SetActive(false);
    }

    private void RestoreFromRegistry()
    {
        var registry = CollectibleRegistry.Instance;
        if (registry == null) return;

        foreach (var data in registry.GetAllDiscovered())
            RevealInstant(data.key);
    }

    // ---------- Unlock pipeline ----------

    private void HandleFirstDiscovery(CollectibleData data)
    {
        if (data == null || data.key == LogKeys.None) return;
        UnlockNode(data.key);
    }

    public void UnlockNode(LogKeys key)
    {
        if (key == LogKeys.None) return;
        if (revealed.Contains(key)) return;
        if (!map.ContainsKey(key))
        {
            Debug.LogWarning($"[InvestigationBoard] No entry for ID '{key}'.");
            return;
        }

        if (pendingQueue.Contains(key)) return;

        pendingQueue.Enqueue(key);
        Debug.Log($"[InvestigationBoard] UnlockNode for ID '{key}'.");
    }

    // ---------- Trigger / playback ----------

    private void HandleZoneEntered()
    {
        if (playRoutine != null) return;
        if (pendingQueue.Count == 0) return;

        playRoutine = StartCoroutine(PlayQueueRoutine());
    }

    private IEnumerator PlayQueueRoutine()
    {
        while (pendingQueue.Count > 0)
        {
            var id = pendingQueue.Dequeue();
            RevealAnimated(id);

            yield return new WaitForSeconds(intervalSeconds);

            // After the node is shown, see if any connections it touches can now light up.
            int before = activatedConnections.Count;
            CheckConnectionsFor(id, animated: true);
            if (activatedConnections.Count > before)
                yield return new WaitForSeconds(intervalSeconds);
        }
        playRoutine = null;
    }

    // ---------- Reveal ----------

    private void RevealAnimated(LogKeys id)
    {
        if (!map.TryGetValue(id, out var boardInteractable)) return;
        revealed.Add(id);

        if (boardInteractable == null) return;
        Vector3 scale = boardInteractable.transform.localScale;
        boardInteractable.transform.localScale = Vector3.zero;
        boardInteractable.gameObject.SetActive(true);
        boardInteractable.transform.DOScale(scale, scaleDuration).SetEase(scaleEase);
    }

    private void RevealInstant(LogKeys id)
    {
        if (!map.TryGetValue(id, out var boardInteractable)) return;
        revealed.Add(id);
        if (boardInteractable != null)
            boardInteractable.gameObject.SetActive(true);

        // Restore connections too — any whose both endpoints are now revealed.
        CheckConnectionsFor(id, animated: false);
    }

    // ---------- Connections ----------

    /// <summary>
    /// Looks at every connection touching `key` and activates those whose
    /// other endpoint is already revealed.
    /// </summary>
    private void CheckConnectionsFor(LogKeys key, bool animated)
    {
        foreach (var conn in connections)
        {
            if (conn == null) continue;
            if (activatedConnections.Contains(conn)) continue;
            if (!conn.Involves(key)) continue;
            if (!conn.CanActivate(revealed)) continue;

            ActivateConnection(conn, animated);
        }
    }

    private void ActivateConnection(BoardConnection conn, bool animated)
    {
        activatedConnections.Add(conn);

        if (animated)
        {
            Vector3 scale = conn.transform.localScale;
            conn.transform.localScale = Vector3.zero;
            conn.gameObject.SetActive(true);
            conn.transform.DOScale(scale, scaleDuration).SetEase(scaleEase);
        }
        else
        {
            conn.gameObject.SetActive(true);
        }
    }

    [ContextMenu("DEBUG / Set Interactables")]
    private void SetInteractables()
    {
        entries.Clear();
        GetComponentsInChildren<BoardInteractable>(includeInactive: true, entries);

        connections.Clear();
        GetComponentsInChildren<BoardConnection>(includeInactive: true, connections);

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
        if (!Application.isPlaying)
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif

        Debug.Log($"[InvestigationBoard] Found {entries.Count} interactable(s) and {connections.Count} connection(s).", this);
    }
}