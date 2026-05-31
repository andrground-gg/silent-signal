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
        _board?.SetTopicHighlight(topic, true);
        base.OnHoverEnter();
    }

    public override void OnHoverExit()
    {
        UIManager.Instance?.HideBoardTopic();
        _board?.SetTopicHighlight(topic, false);
        base.OnHoverExit();
    }
}
