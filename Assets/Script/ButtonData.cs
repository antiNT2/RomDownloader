using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ButtonData : MonoBehaviour
{
    [SerializeField]
    public RectTransform rectTransform;

    [SerializeField]
    public Image image;

    [SerializeField]
    public TextMeshProUGUI text;

    public void ToggleDisplay(bool enabled)
    {
        image.enabled = enabled;
        text.enabled = enabled;
    }
}
