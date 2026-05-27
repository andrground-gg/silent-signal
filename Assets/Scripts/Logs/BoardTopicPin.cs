using UnityEngine;

public class BoardTopicPin : BaseInteractable
{
    public BoardTopic topic;
    public Sprite sprite;

    private InvestigationBoard _board;

    private void Start()
    {
        _board = GetComponentInParent<InvestigationBoard>();
    }

    public override void OnHoverEnter()
    {
        UIManager.Instance?.ShowBoardTopic(topic, sprite, transform);

        if (_board != null)
            foreach (var entry in _board.GetEntriesForTopic(topic))
                entry.SetHighlight(true);

        base.OnHoverEnter();
    }

    public override void OnHoverExit()
    {
        UIManager.Instance?.HideBoardTopic();

        if (_board != null)
            foreach (var entry in _board.GetEntriesForTopic(topic))
                entry.SetHighlight(false);

        base.OnHoverExit();
    }
}
