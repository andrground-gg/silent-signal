using TMPro;
using UnityEngine;

public class DebugTimeView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _text;
    void Update()
    {
        if (TimeSystem.Instance)
        {
            _text.text = "Time: " + TimeSystem.Instance.CurrentTime;
        }
    }
}
