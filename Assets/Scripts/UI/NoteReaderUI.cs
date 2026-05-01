using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Dumb view: receives data and displays it.
/// UIManager handles show/hide via SetActive on this GameObject.
/// </summary>
public class NoteReaderUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private TMP_Text bodyLabel;
    [SerializeField] private Image paperImage;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape))
            UIManager.Instance?.HideNote();
    }

    public void SetData(NoteData data)
    {
        if (data == null) return;

        if (bodyLabel != null)  bodyLabel.text  = data.contentText;
        if (paperImage != null)
        {
            paperImage.sprite  = data.paperSprite;
            paperImage.enabled = data.paperSprite != null;
        }
    }
}
