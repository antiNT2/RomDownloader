using UnityEngine;
using UnityEngine.UI;
using System.Linq;

//Igor Aherne 10/May/2017
//https://www.facebook.com/igor.aherne
//
//as soon as our rect no longer overlaps the reference rect
//we will disable our graphic (if supplied)  and  an optional GameObject (could be our child)
//
//Works even if recttransforms belong to different parents
[ExecuteInEditMode]
public class OffScreenUI_Cull : MonoBehaviour
{


    [SerializeField] RectTransform _viewportRectangle;
    [SerializeField, Space(15)] RectTransform _ownRectTransform;

    //will be disabled if our GUI goes outside of wanted region
    [SerializeField] public Graphic _localGraphicComponent;
    [SerializeField] public GameObject[] _optionalGO_to_On_Off;


    void Reset()
    {
        _ownRectTransform = transform as RectTransform;
    }


    void Start()
    {
        if (_viewportRectangle == null)
        {
            _viewportRectangle = (GetComponentInParent(typeof(Canvas)) as Canvas).transform as RectTransform;
        }
    }



    void Update()
    {
#if UNITY_EDITOR
        //while in editor, this will discard null "optional game objects", automatically.
        int prevLength = _optionalGO_to_On_Off.Length;

        _optionalGO_to_On_Off = _optionalGO_to_On_Off.Where(go => go != null)
                                                     .ToArray();

        if (_optionalGO_to_On_Off.Length != prevLength)
        {
            UnityEditor.EditorUtility.SetDirty(this);
        }

        if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode == false) { return; }
#endif

        Cull();
    }



    void Cull()
    {
        if (_viewportRectangle == null) { return; }

        bool overlaps = _ownRectTransform.rectTransfOverlaps_inScreenSpace(_viewportRectangle);

        if (overlaps == true)
        {
            toggleElements_ifNeeded(true);
        }
        else
        {
            toggleElements_ifNeeded(false);
        }
    }



    void toggleElements_ifNeeded(bool requiredValue)
    {

        for (int i = 0; i < _optionalGO_to_On_Off.Length; i++)
        {
            GameObject optionalGO = _optionalGO_to_On_Off[i];

            if (optionalGO.activeSelf != requiredValue)
            {
                optionalGO.SetActive(requiredValue);
            }
        }//end for


        if (_localGraphicComponent != null && _localGraphicComponent.enabled != requiredValue)
        {
            _localGraphicComponent.enabled = requiredValue;
        }

    }



}



static class Extensions
{

    public static bool rectTransfOverlaps_inScreenSpace(this RectTransform rectTrans1, RectTransform rectTrans2)
    {
        Rect rect1 = rectTrans1.getScreenSpaceRect();
        Rect rect2 = rectTrans2.getScreenSpaceRect();

        return rect1.Overlaps(rect2);
    }



    //rect transform into coordinates expressed as seen on the screen (in pixels)
    //takes into account RectTrasform pivots
    // based on answer by Tobias-Pott
    // http://answers.unity3d.com/questions/1013011/convert-recttransform-rect-to-screen-space.html
    public static Rect getScreenSpaceRect(this RectTransform transform)
    {
        Vector2 size = Vector2.Scale(transform.rect.size, transform.lossyScale);
        Rect rect = new Rect(transform.position.x, Screen.height - transform.position.y, size.x, size.y);
        rect.x -= (transform.pivot.x * size.x);
        rect.y -= ((1.0f - transform.pivot.y) * size.y);
        return rect;
    }

}