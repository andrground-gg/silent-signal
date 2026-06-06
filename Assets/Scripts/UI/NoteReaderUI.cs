using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NoteReaderUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text bodyLabel;
    [SerializeField] private Image paperImage;

    [Header("Buttons")]
    [SerializeField] private Button openButton;
    [SerializeField] private Button closeButton;

    private void Awake()
    {
        panel?.SetActive(false);
        if (openButton != null)  openButton.onClick.AddListener(OnOpen);
        if (closeButton != null) closeButton.onClick.AddListener(OnClose);
    }

    private void OnDestroy()
    {
        if (openButton != null)  openButton.onClick.RemoveListener(OnOpen);
        if (closeButton != null) closeButton.onClick.RemoveListener(OnClose);
    }

    public void Open()     => panel?.SetActive(true);
    private void OnOpen()  => panel?.SetActive(true);
    private void OnClose() => panel?.SetActive(false);

    public void SetData(NoteData data)
    {
        if (data == null) return;

        if (bodyLabel != null) bodyLabel.text = data.FullText;

        if (paperImage != null)
        {
            paperImage.sprite  = data.paperSprite;
        }
    }
}
