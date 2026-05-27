using DG.Tweening;
using UnityEngine;

public class BoardInteractable : BaseInteractable
{
    [SerializeField] private CollectibleData data;
    [SerializeField] private Renderer boardRenderer;

    private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
    private static readonly Color GrayColor = new Color(0.45f, 0.45f, 0.45f, 1f);

    private bool isRevealed;
    private InvestigationBoard _board;
    private BoardTopicPin _topicPin;

    public CollectibleData Data => data;

    protected override void Awake()
    {
        base.Awake();
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

    public void Reveal(bool animated)
    {
        isRevealed = true;
        if (animated)
            DOVirtual.Float(0f, 1f, 0.5f, t => ApplyColor(Color.Lerp(GrayColor, Color.white, t)));
        else
            ApplyColor(Color.white);
    }

    private void ApplyColor(Color color)
    {
        if (boardRenderer == null) return;
        var block = new MaterialPropertyBlock();
        boardRenderer.GetPropertyBlock(block);
        block.SetColor(ColorId, color);
        boardRenderer.SetPropertyBlock(block);
    }

    public override void OnHoverEnter()
    {
        if (!isRevealed)
        {
            UIManager.Instance?.ShowNotDiscovered(transform);
            base.OnHoverEnter();
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

        base.OnHoverEnter();
    }

    public override void OnHoverExit()
    {
        UIManager.Instance?.HideBoardCollectible();
        UIManager.Instance?.HideBoardTopic();
        TopicPin?.SetHighlight(false);
        base.OnHoverExit();
    }
}
