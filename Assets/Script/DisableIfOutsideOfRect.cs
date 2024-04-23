using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DisableIfOutsideOfRect : MonoBehaviour
{
    [SerializeField]
    private ScrollRect scrollRect;

    [SerializeField]
    private RectTransform _viewportRectangle;

    public float DistanceToRecalcVisibility = 400.0f;
    public float DistanceMarginForLoad = 600.0f;
    private float lastPos = Mathf.Infinity;

    Dictionary<Transform, ButtonData> scrollRectButtons = new Dictionary<Transform, ButtonData>();

    int lastNumberOfChildren = 0;

    private void Start()
    {
        this.scrollRect.onValueChanged.AddListener((newValue) =>
        {
            if (Mathf.Abs(this.lastPos - this.scrollRect.content.transform.localPosition.y) >= DistanceToRecalcVisibility)
            {

                if (lastNumberOfChildren != this.scrollRect.content.childCount)
                {
                    scrollRectButtons.Clear();
                }

                foreach (Transform child in this.scrollRect.content)
                {
                    ButtonData buttonData;
                    if (!scrollRectButtons.ContainsKey(child))
                    {
                        buttonData = child.GetComponent<ButtonData>();
                        if (buttonData != null)
                        {
                            scrollRectButtons.Add(child, buttonData);
                        }
                    }
                    else
                    {
                        buttonData = scrollRectButtons[child];
                    }

                    RectTransform childRectTransform = buttonData.rectTransform;
                    bool needsToBeVisible = childRectTransform.rectTransfOverlaps_inScreenSpace(_viewportRectangle);

                    buttonData.ToggleDisplay(needsToBeVisible);
                }

                lastNumberOfChildren = this.scrollRect.content.childCount;

                this.lastPos = this.scrollRect.content.transform.localPosition.y;
            }
        });
    }

}
