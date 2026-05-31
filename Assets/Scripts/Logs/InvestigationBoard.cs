using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class InvestigationBoard : MonoBehaviour
{
    [Header("Board Entries")]
    [SerializeField] private List<BoardInteractable> entries = new List<BoardInteractable>();

    [Header("Topic Pins")]
    [SerializeField] private List<BoardTopicPin> topicPins = new List<BoardTopicPin>();

    [Header("Connections")]
    [SerializeField] private List<BoardConnection> connections = new List<BoardConnection>();

    [Header("Trigger")]
    [SerializeField] private InteractionZone interactionZone;

    [Header("Animation")]
    [SerializeField] private float intervalSeconds = 0.5f;
    [SerializeField] private float scaleDuration = 0.35f;
    [SerializeField] private Ease scaleEase = Ease.OutBack;

    private readonly Dictionary<LogKeys, BoardInteractable> map = new Dictionary<LogKeys, BoardInteractable>();
    private readonly HashSet<LogKeys> revealed = new HashSet<LogKeys>();
    private readonly HashSet<BoardConnection> activatedConnections = new HashSet<BoardConnection>();
    private readonly Queue<LogKeys> pendingQueue = new Queue<LogKeys>();

    private Coroutine playRoutine;
    private bool isPlayerInZone;

    private void Awake()
    {
        if (interactionZone != null)
        {
            interactionZone.OnEnteredInteractionZone += HandleZoneEntered;
            interactionZone.OnExitedInteractionZone += HandleZoneExited;
        }
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
        {
            interactionZone.OnEnteredInteractionZone -= HandleZoneEntered;
            interactionZone.OnExitedInteractionZone -= HandleZoneExited;
        }

        if (CollectibleRegistry.Instance != null)
            CollectibleRegistry.Instance.OnFirstDiscovery -= HandleFirstDiscovery;
    }

    // ---------- Queries ----------

    public BoardTopicPin GetTopicPin(BoardTopic topic)
    {
        foreach (var pin in topicPins)
            if (pin != null && pin.topic == topic)
                return pin;
        return null;
    }

    public IEnumerable<BoardInteractable> GetEntriesForTopic(BoardTopic topic)
    {
        foreach (var entry in entries)
            if (entry != null && entry.Data != null && entry.Data.topic == topic)
                yield return entry;
    }

    // Highlights every revealed node sharing the given topic. Used on hover so
    // the whole topic group lights up together.
    public void SetTopicHighlight(BoardTopic topic, bool on)
    {
        foreach (var entry in entries)
            if (entry != null && entry.Data != null && entry.Data.topic == topic)
                entry.SetTopicHighlight(on);
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
        if (!map.ContainsKey(data.key)) return;

        UnlockNode(data.key);

        if (isPlayerInZone && playRoutine == null && pendingQueue.Count > 0)
            playRoutine = StartCoroutine(PlayQueueRoutine());
    }

    public void UnlockNode(LogKeys key)
    {
        if (key == LogKeys.None) return;
        if (revealed.Contains(key)) return;
        if (!map.ContainsKey(key)) return;
        if (pendingQueue.Contains(key)) return;

        pendingQueue.Enqueue(key);
        Debug.Log($"[InvestigationBoard] Queued '{key}' for reveal.", this);
    }

    // ---------- Trigger / playback ----------

    private void HandleZoneEntered()
    {
        isPlayerInZone = true;

        if (playRoutine != null) return;
        if (pendingQueue.Count == 0) return;

        playRoutine = StartCoroutine(PlayQueueRoutine());
    }

    private void HandleZoneExited()
    {
        isPlayerInZone = false;
    }

    private IEnumerator PlayQueueRoutine()
    {
        while (pendingQueue.Count > 0)
        {
            var id = pendingQueue.Dequeue();
            RevealAnimated(id);

            yield return new WaitForSeconds(intervalSeconds);

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
        boardInteractable?.Reveal(animated: true);
    }

    private void RevealInstant(LogKeys id)
    {
        if (!map.TryGetValue(id, out var boardInteractable)) return;
        revealed.Add(id);
        boardInteractable?.Reveal(animated: false);
        CheckConnectionsFor(id, animated: false);
    }

    // ---------- Connections ----------

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

    // ---------- Debug ----------

    /// <summary>
    /// Reveals every mapped node and all valid connections instantly, and
    /// persists them as discovered. Bypasses the zone/queue animation.
    /// </summary>
    [ContextMenu("DEBUG / Reveal All")]
    public void RevealAll()
    {
        if (map.Count == 0) BuildMap();

        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }
        pendingQueue.Clear();

        foreach (var kvp in map)
            RevealInstant(kvp.Key);

        CollectibleRegistry.Instance?.DiscoverAll();
    }

    [ContextMenu("DEBUG / Set Interactables")]
    private void SetInteractables()
    {
        entries.Clear();
        GetComponentsInChildren<BoardInteractable>(includeInactive: true, entries);

        topicPins.Clear();
        GetComponentsInChildren<BoardTopicPin>(includeInactive: true, topicPins);

        connections.Clear();
        GetComponentsInChildren<BoardConnection>(includeInactive: true, connections);

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
        if (!Application.isPlaying)
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif

        Debug.Log($"[InvestigationBoard] Found {entries.Count} entry/ies, {topicPins.Count} topic pin(s), {connections.Count} connection(s).", this);
    }
}
