using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using TMPro;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance
    {
        get
        {
            if (instance == null)
                GenerateInstance();
            return instance;
        }

        private set
        {
            instance = value;
        }
    }

    static InputManager instance = null;

    static List<GameObject> spawnedInstances = new List<GameObject>();

    public bool IsUsingMouse => isUsingMouse;

    bool isUsingMouse = true;

    public Vector2 MousePosition => Mouse.current.position.ReadValue();

    public GameObject CurrentSelectedUIElement => EventSystem.current.currentSelectedGameObject;

    LinkedList<GameObject> lastSelectedUIElements = new LinkedList<GameObject>();

    public InputActionAsset InputActionAsset => inputActionAsset;

    InputActionAsset inputActionAsset;
    Canvas parentCanvas;

    HashSet<GameObject> hoveredUiElementsAsGameObjects = new HashSet<GameObject>();

    public static InputAction CancelAction => FindAction("UI/Cancel");
    public static InputAction SubmitAction => FindAction("UI/Submit");
    public static InputAction MoveAction => FindAction("UI/Navigate");

    Dictionary<string, InputAction> inputActions = new Dictionary<string, InputAction>();

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            if (spawnedInstances.Contains(this.gameObject))
            {
                Destroy(this.gameObject);
                spawnedInstances.Remove(this.gameObject);
                return;
            }

            Destroy(this);
            return;
        }

        instance = this;

        inputActionAsset = EventSystem.current.GetComponent<InputSystemUIInputModule>().actionsAsset;
    }

    static void GenerateInstance()
    {
        if (instance != null || !Application.isPlaying)
            return;

        GameObject inputManager = new GameObject("Input Manager");
        instance = inputManager.AddComponent<InputManager>();

        spawnedInstances.Add(inputManager);
    }

    private void Update()
    {
        isUsingMouse = CheckIfUsingMouse();
        Cursor.visible = isUsingMouse;

        if (!isUsingMouse && !IsUiElementValid(CurrentSelectedUIElement))
        {
            GameObject suitableButton = GetSuitableButtonToSelect();
            if (suitableButton != null)
            {
                if (!IsTypingInInputField())
                    EventSystem.current.SetSelectedGameObject(suitableButton);
                // Debug.Log($"AUTO Selected {suitableButton}");
            }
        }
        else if (isUsingMouse && CurrentSelectedUIElement != null)
        {
            if (!IsTypingInInputField())
            {
                // If the selected UI element is an input field, we don't want to deselect it
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        if (CurrentSelectedUIElement != null)
        {
            if (lastSelectedUIElements.First?.Value != CurrentSelectedUIElement)
            {
                lastSelectedUIElements.AddFirst(CurrentSelectedUIElement);
                if (lastSelectedUIElements.Count > 10)
                {
                    lastSelectedUIElements.RemoveLast();
                }
            }
        }

        RefreshUIElementsUnderMouse();

        // DebugEventRaycast();
    }

    bool IsTypingInInputField()
    {
        if (CurrentSelectedUIElement == null)
            return false;

        TMP_InputField inputField = CurrentSelectedUIElement.GetComponent<TMP_InputField>();

        if (inputField == null)
            return false;

        return inputField.isFocused;
    }

    void RefreshUIElementsUnderMouse()
    {
        PointerEventData pointerData = new PointerEventData(EventSystem.current);

        pointerData.position = Instance.MousePosition;

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        // Put every gameobject in the hashset
        hoveredUiElementsAsGameObjects.Clear();

        for (int i = 0; i < results.Count; i++)
        {

            Selectable selectable = results[i].gameObject.GetComponent<Selectable>();
            if (selectable != null)
            {
                bool isObstructed = false;

                foreach (GameObject potentialObstructer in hoveredUiElementsAsGameObjects)
                {
                    if (IsValidObstructer(selectable, potentialObstructer, true))
                    {
                        // Debug.Log($"{results[i].gameObject.name} is obstructed by {potentialObstructer.name}");
                        isObstructed = true;
                        break;
                    }
                }

                if (isObstructed)
                {
                    continue;
                }
            }

            hoveredUiElementsAsGameObjects.Add(results[i].gameObject);
        }

        //foreach (var result in results)
        //{
        //    hoveredUiElementsAsGameObjects.Add(result.gameObject);
        //}

        // Debug.Log($"Hovered UI elements: {string.Join(", ", hoveredUiElementsAsGameObjects.Select(x => x.name))}");
    }

    public static bool IsUiElementsUnderMouse(GameObject uiElement)
    {
        return Instance.hoveredUiElementsAsGameObjects.Contains(uiElement);
    }

    public static bool IsSelectableObstructed(Selectable selectable, bool dontAllowSiblingsObstructingEachOther = false)
    {
        if (selectable == null || EventSystem.current == null)
            return false;

        PointerEventData pointerData = new PointerEventData(EventSystem.current);
        pointerData.position = GetRectScreenPosition(selectable.transform);

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        if (results.Count == 0)
            return false;

        List<GameObject> potentialObstructers = new List<GameObject>();

        //string output = "Raycast results: ";
        //foreach (var result in results)
        //{
        //    output += result.gameObject.name + ", ";
        //}
        //Debug.Log(output);

        foreach (var result in results)
        {
            if (result.gameObject == selectable.gameObject)
            {
                // We've reached the selectable, so we can stop the search
                break;
            }

            potentialObstructers.Add(result.gameObject);
        }

        foreach (var obstructer in potentialObstructers)
        {
            if (IsValidObstructer(selectable, obstructer, dontAllowSiblingsObstructingEachOther))
            {

                Debug.Log($"{selectable.gameObject.name} is obstructed by {obstructer.name}");
                return true;
            }
        }

        // Debug.Log($"{selectable.gameObject.name} is not obstructed - ({output})");

        return false;
    }

    static bool IsValidObstructer(Selectable selectable, GameObject potentialObstructer, bool dontAllowSiblingsObstructingEachOther)
    {
        if (potentialObstructer == null)
            return false;

        if (potentialObstructer.GetComponent<ScrollRect>() != null)
        {
            return false;
        }

        // if the obstructer is the child of the selectable, it's not obstructing
        if (potentialObstructer.transform.IsChildOf(selectable.transform))
            return false;

        // if the obstructer is the parent of the selectable, it's not obstructing
        if (selectable.transform.IsChildOf(potentialObstructer.transform))
            return false;

        if (dontAllowSiblingsObstructingEachOther)
        {
            if (potentialObstructer.transform.parent == selectable.transform.parent)
                return false;
        }

        int obstructerLayer = potentialObstructer.GetComponentInParent<Canvas>().sortingOrder;
        int selectableLayer = selectable.GetComponentInParent<Canvas>().sortingOrder;

        if (selectableLayer >= obstructerLayer)
        {
            return false;
        }

        Button button = potentialObstructer.GetComponentInChildren<Button>();

        if (button != null)
        {
            if (!button.IsInteractable())
            {
                return false;
            }
        }

        return true;
    }

    void DebugEventRaycast()
    {
        if (EventSystem.current == null)
            return;

        PointerEventData pointerData = new PointerEventData(EventSystem.current);
        pointerData.position = MousePosition;

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        string output = "Raycast results: ";
        foreach (var result in results)
        {
            output += result.gameObject.name + ", ";
        }

        Debug.Log(output);
    }

    private void LateUpdate()
    {
        if (!isUsingMouse)
            MoveMousePositionToSelectedGameObjectScreenPosition();
    }

    void MoveMousePositionToSelectedGameObjectScreenPosition()
    {
        if (CurrentSelectedUIElement == null)
            return;

        RectTransform rectTransform = CurrentSelectedUIElement.GetComponent<RectTransform>();
        if (rectTransform == null)
            return;

        Vector2 screenPosition = GetRectScreenPosition(rectTransform);
        Mouse.current.WarpCursorPosition(screenPosition);
    }

    static Vector2 GetRectScreenPosition(Transform transform)
    {
        return RectTransformUtility.WorldToScreenPoint(Camera.main, transform.position);
    }

    static bool IsUiElementValid(GameObject element)
    {
        if (element == null || element.gameObject == null)
            return false;

        if (!element.activeInHierarchy)
            return false;

        Button button = element.GetComponentInChildren<Button>();

        if (button == null)
        {
            return false;
        }

        if (!button.interactable)
            return false;

        if (button.navigation.mode == Navigation.Mode.None)
            return false;

        return true;
    }

    bool CheckIfUsingMouse()
    {
        if (Gamepad.current != null)
        {
            if (Gamepad.current.dpad.value != Vector2.zero || Gamepad.current.aButton.wasPressedThisFrame || Gamepad.current.leftStick.value != Vector2.zero)
            {

                Debug.Log($"Gamepad detected: {Gamepad.current.name}");

                return false;
            }
        }

        if (inputActionAsset?.FindAction("UI/Navigate")?.ReadValue<Vector2>() != Vector2.zero)
        {
            return false;
        }

        if (Mouse.current != null)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.delta.ReadValue() != Vector2.zero)
            {
                return true;
            }
        }

        return isUsingMouse;
    }

    GameObject GetSuitableButtonToSelect()
    {

        List<GameObject> candidates = new List<GameObject>();
        List<GameObject> nonObstructedCandidates = new List<GameObject>();

        // Iterate through all the buttons in the scene and return the first those that are interactable
        foreach (var button in FindObjectsByType<Button>(FindObjectsSortMode.None))
        {
            if (IsUiElementValid(button.gameObject))
            {
                candidates.Add(button.gameObject);

                if (!IsSelectableObstructed(button))
                {
                    nonObstructedCandidates.Add(button.gameObject);
                }
            }
        }

        if (nonObstructedCandidates.Count > 0)
        {
            candidates = new List<GameObject>(nonObstructedCandidates);
        }

        GameObject latestSelectedButton = lastSelectedUIElements.First?.Value;

        if (latestSelectedButton != null)
        {
            GameObject closestButton = null;
            float closestButtonDistance = float.MaxValue;

            // Iterate through all the buttons in the scene and return the closest one to the last selected button
            foreach (var candidate in candidates)
            {
                float distance = Vector3.Distance(latestSelectedButton.transform.position, candidate.transform.position);
                if (distance < closestButtonDistance)
                {
                    closestButtonDistance = distance;
                    closestButton = candidate;
                }
            }

            return closestButton;
        }

        return candidates.Count > 0 ? candidates[0] : null;
    }

    public static void SelectGameObject(GameObject gameObject)
    {
        EventSystem.current.SetSelectedGameObject(gameObject);
    }

    public static void DeselectCurrentGameObject()
    {
        EventSystem.current.SetSelectedGameObject(null);
    }

    public static InputAction FindAction(string actionName)
    {
        if (!Instance.inputActions.ContainsKey(actionName))
        {
            if (instance.InputActionAsset == null)
                instance.inputActionAsset = EventSystem.current.GetComponent<InputSystemUIInputModule>().actionsAsset;

            Instance.inputActions.Add(actionName, Instance.InputActionAsset.FindAction(actionName, true));
        }
        Instance.inputActions[actionName].Enable();
        return Instance.inputActions[actionName];
    }

    public static Vector3 GetVirtualCursorPosition()
    {
        if (!Instance.isUsingMouse)
        {
            if (Instance.CurrentSelectedUIElement == null)
            {
                return Vector3.zero;
            }

            return Instance.CurrentSelectedUIElement.transform.position;
        }
        else
        {
            Vector3 output;

            if (Instance.parentCanvas == null)
                Instance.parentCanvas = GameObject.FindGameObjectWithTag("Parent Canvas").GetComponent<Canvas>();

            Vector2 mousePosition = Instance.MousePosition;
            RectTransformUtility.ScreenPointToWorldPointInRectangle(Instance.parentCanvas.GetComponent<RectTransform>(), mousePosition, Camera.main, out output);
            output.z = 0;

            return output;
        }
    }

    public static bool WasLeftDirectionPressedThisFrame()
    {
        return MoveAction.WasPressedThisFrame() && MoveAction.ReadValue<Vector2>().x < 0;
    }

    public static bool WasRightDirectionPressedThisFrame()
    {
        return MoveAction.WasPressedThisFrame() && MoveAction.ReadValue<Vector2>().x > 0;
    }
}
