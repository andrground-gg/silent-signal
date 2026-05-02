using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class PopupCanvasUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private Image iconImage;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Animation")]
    [SerializeField] private float fadeDuration = 0.25f;
    [SerializeField] private float scaleDuration = 0.25f;

    private Vector3 _targetScale;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        _targetScale = transform.localScale;

        if (transform.parent != null)
        {
            var parentScale = transform.parent.lossyScale;
            _targetScale = new Vector3(
                _targetScale.x / parentScale.x,
                _targetScale.y / parentScale.y,
                _targetScale.z / parentScale.z
            );
            transform.localScale = _targetScale;
        }
    }

    public void Show()
    {
        PlayShowAnimation();
    }

    public void SetData(CollectibleData data)
    {
        if (data == null) return;

        if (titleText != null)
            titleText.text = data.title;

        if (bodyText != null)
        {
            // Dynamic text — base content + appended updates from any unlocked triggers.
            bodyText.text = data.GetCurrentBoardText(IsUnlocked);
        }

        if (iconImage != null)
        {
            if (data.boardSprite != null)
            {
                iconImage.gameObject.SetActive(true);
                iconImage.sprite = data.boardSprite;
            }
            else
            {
                iconImage.gameObject.SetActive(false);
            }
        }
    }

    private static bool IsUnlocked(LogKeys key)
    {
        return CollectibleRegistry.Instance != null && CollectibleRegistry.Instance.IsDiscovered(key);
    }

    private void PlayShowAnimation()
    {
        transform.DOKill();
        canvasGroup.DOKill();

        transform.localScale = Vector3.zero;
        canvasGroup.alpha = 0f;

        Sequence seq = DOTween.Sequence();
        seq.Join(transform.DOScale(_targetScale, scaleDuration).SetEase(Ease.OutBack));
        seq.Join(canvasGroup.DOFade(1f, fadeDuration));
    }

    public void HideAndDestroy()
    {
        transform.DOKill();
        canvasGroup.DOKill();

        Sequence seq = DOTween.Sequence();
        seq.Join(transform.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InBack));
        seq.Join(canvasGroup.DOFade(0f, 0.2f));
        seq.OnComplete(() => Destroy(gameObject));
    }
}