using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Physical investigation board.
/// Unlocks are queued — they don't appear immediately. The queue plays out
/// when the player enters the board's InteractionZone, scaling each piece
/// in via DOTween with a delay between them.
/// </summary>

public class InvestigationBoard : MonoBehaviour
{
    [Header("Board Entries")]
    [SerializeField] private List<BoardInteractable> entries = new List<BoardInteractable>();

    [Header("Trigger")]
    [Tooltip("Zone the player walks into to play the queued unlocks.")]
    [SerializeField] private InteractionZone interactionZone;

    [Header("Animation")]
    [Tooltip("Pause between each piece scaling in.")]
    [SerializeField] private float intervalSeconds = 0.5f;

    [SerializeField] private float scaleDuration = 0.35f;
    [SerializeField] private Ease scaleEase = Ease.OutBack;

    // ID -> objects, built from `entries` on Awake.
    private readonly Dictionary<LogKeys, BoardInteractable> map = new Dictionary<LogKeys, BoardInteractable>();

    // Already finalized (revealed and animated). Persisted.
    private readonly HashSet<LogKeys> revealed = new HashSet<LogKeys>();

    // Pending IDs waiting for the player to enter the zone.
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

        // Re-show everything that was already revealed in a previous session.
        // No animation — they were already seen, just appear.
        RestoreFromRegistry();

        // Subscribe live so future unlocks queue up automatically.
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
            if (entry.Data.key == LogKeys.None) continue;
            if (!map.ContainsKey(entry.Data.key))
                map[entry.Data.key] = entry;
        }
    }

    private void HideAll()
    {
        foreach (var entry in entries)
            if (entry != null)
            {
                entry.gameObject.SetActive(false);
            }
    }

    private void RestoreFromRegistry()
    {
        var registry = CollectibleRegistry.Instance;
        if (registry == null) return;

        foreach (var data in registry.GetAllDiscovered())
        {
            if (data.boardUpdateIDs == null) continue;
            foreach (var id in data.boardUpdateIDs)
                RevealInstant(id);
        }
    }

    // ---------- Unlock pipeline ----------

    private void HandleFirstDiscovery(CollectibleData data)
    {
        if (data == null || data.boardUpdateIDs == null) return;
        foreach (var id in data.boardUpdateIDs)
            UnlockNode(id);
    }

    /// <summary>
    /// Queue an ID for reveal. Does NOT show the object immediately —
    /// it appears when the player enters the board's InteractionZone.
    /// </summary>
    public void UnlockNode(LogKeys boardUpdateID)
    {
        if (boardUpdateID == LogKeys.None) return;
        if (revealed.Contains(boardUpdateID)) return;
        if (!map.ContainsKey(boardUpdateID))
        {
            Debug.LogWarning($"[InvestigationBoard] No entry for ID '{boardUpdateID}'.");
            return;
        }

        // Avoid duplicate queue entries.
        if (pendingQueue.Contains(boardUpdateID)) return;

        pendingQueue.Enqueue(boardUpdateID);
        Debug.Log($"[InvestigationBoard] UnlockNode for ID '{boardUpdateID}'.");
    }

    // ---------- Trigger / playback ----------

    private void HandleZoneEntered()
    {
        if (playRoutine != null) return;       // already playing
        if (pendingQueue.Count == 0) return;   // nothing to play

        playRoutine = StartCoroutine(PlayQueueRoutine());
    }

    private IEnumerator PlayQueueRoutine()
    {
        while (pendingQueue.Count > 0)
        {
            var id = pendingQueue.Dequeue();
            RevealAnimated(id);
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
        if (boardInteractable == null) return;
        boardInteractable.gameObject.SetActive(true);
    }
    
    [ContextMenu("DEBUG / Set Interactables")]
    private void SetInteractables()
    {
        entries.Clear();
        GetComponentsInChildren<BoardInteractable>(includeInactive: true, entries);

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
        if (!Application.isPlaying)
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif

        Debug.Log($"[InvestigationBoard] Found {entries.Count} BoardInteractable(s).", this);
    }
}