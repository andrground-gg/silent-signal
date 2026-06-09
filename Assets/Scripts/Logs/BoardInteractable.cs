using DG.Tweening;
using UnityEngine;

public class BoardInteractable : BaseInteractable
{
    [SerializeField] private CollectibleData data;
    [SerializeField] private Renderer boardRenderer;

    [Tooltip("Outline color for nodes that are revealed but never hovered yet (\"new\").")]
    [SerializeField] private Color unseenColor = new Color(1f, 0.82f, 0.25f, 1f);

    private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
    private static readonly Color GrayColor = new Color(0.45f, 0.45f, 0.45f, 1f);

    // The material's own color, captured before we ever override it. This is the
    // "revealed" target so revealing shows the material color instead of white.
    private Color revealedColor = Color.white;

    private bool isRevealed;
    private bool isViewed;
    private bool isHovered;
    private bool isTopicHighlighted;
    private Color defaultOutlineColor;
    private InvestigationBoard _board;
    private BoardTopicPin _topicPin;

    public CollectibleData Data => data;

    protected override void Awake()
    {
        base.Awake();
        defaultOutlineColor = GetOutlineColor();
        if (boardRenderer != null)
            revealedColor = boardRenderer.sharedMaterial.GetColor(ColorId);
        ApplyColor(GrayColor);
    }

    private void Start()
    {
        _board = GetComponentInParent<InvestigationBoard>();
    }

    private BoardTopicPin TopicPin
    {
        get
        {
            if (_topicPin == null && _board != null && data != null)
                _topicPin = _board.GetTopicPin(data.topic);
            return _topicPin;
        }
    }

    public void SetData(CollectibleData newData)
    {
        data = newData;
        _topicPin = null; // reset cache when data changes
    }

    // Highlights this node as part of a topic group (topic-pin hover) — only if
    // already revealed, so hovering never exposes undiscovered nodes.
    public void SetTopicHighlight(bool on)
    {
        if (!isRevealed) return;
        isTopicHighlighted = on;
        RefreshOutline();
    }

    public void Reveal(bool animated)
    {
        isRevealed = true;
        isViewed = data != null && (CollectibleRegistry.Instance?.IsViewed(data.key) ?? false);

        if (animated)
            DOVirtual.Float(0f, 1f, 0.5f, t => ApplyColor(Color.Lerp(GrayColor, revealedColor, t)));
        else
            ApplyColor(revealedColor);

        RefreshOutline();
    }

    private void ApplyColor(Color color)
    {
        if (boardRenderer == null) return;
        var block = new MaterialPropertyBlock();
        boardRenderer.GetPropertyBlock(block);
        block.SetColor(ColorId, color);
        boardRenderer.SetPropertyBlock(block);
    }

    // Drives the outline from all three sources: direct hover, topic-group
    // highlight, and the persistent "new" glow on revealed-but-unseen nodes.
    private void RefreshOutline()
    {
        bool unseen = isRevealed && !isViewed;
        bool active = isHovered || isTopicHighlighted || unseen;

        if (active)
            SetOutlineColor(isHovered || isTopicHighlighted ? defaultOutlineColor : unseenColor);

        SetHighlight(active);
    }

    public override void OnHoverEnter()
    {
        if (!isRevealed)
        {
            UIManager.Instance?.ShowNotDiscovered(transform);
            isHovered = true;
            RefreshOutline();
            return;
        }

        if (data == null)
            Debug.LogError("No CollectibleData assigned.", this);

        UIManager.Instance?.ShowBoardCollectible(data, transform);

        var pin = TopicPin;
        if (pin != null)
        {
            UIManager.Instance?.ShowBoardTopic(data.topic, pin.sprite, pin.transform);
            pin.SetHighlight(true);
        }

        // First direct inspection clears the "new" glow for good.
        if (!isViewed && data != null)
        {
            CollectibleRegistry.Instance?.MarkViewed(data.key);
            isViewed = true;
        }

        isHovered = true;
        RefreshOutline();
    }

    public override void OnHoverExit()
    {
        UIManager.Instance?.HideBoardCollectible();
        UIManager.Instance?.HideBoardTopic();
        TopicPin?.SetHighlight(false);
        isHovered = false;
        RefreshOutline();
    }
}
