using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FolderDisplayer : MonoBehaviour
{
    [SerializeField]
    Button fileButtonPrefab;

    [SerializeField]
    RectTransform buttonParent;

    [SerializeField]
    AutoScrollRect autoScrollRect;

    Dictionary<ElementButton, GameObject> spawnedButtons = new Dictionary<ElementButton, GameObject>();

    void DeleteAllSpawnedButtons()
    {
        foreach (var button in spawnedButtons.Values)
        {
            Destroy(button);
        }
        spawnedButtons.Clear();
    }

    public async void DisplayFilesAndFolders(params ElementButton[] elements)
    {
        DeleteAllSpawnedButtons();

        foreach (var element in elements)
        {
            var button = Instantiate(fileButtonPrefab, buttonParent);
            button.GetComponentInChildren<TextMeshProUGUI>().text = element.DisplayName;
            button.GetComponentInChildren<TextMeshProUGUI>().fontStyle = element.type == ElementButton.ElementType.Folder ? FontStyles.Bold : FontStyles.Normal;
            button.onClick.AddListener(() => element.action());
            spawnedButtons.Add(element, button.gameObject);

            button.gameObject.SetActive(true);
        }

        // Select the first button
        if (elements.Length > 0)
        {
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(spawnedButtons[elements[0]]);
        }

        await Task.Delay(10);
        autoScrollRect.enabled = true;
    }

    public class ElementButton
    {
        string displayName;
        public string fullPath;
        public Action action;
        public ElementType type;

        public enum ElementType
        {
            Folder,
            File
        }

        public ElementButton(string name, string fullPath, Action action, ElementType type)
        {
            this.displayName = name;
            this.fullPath = fullPath;
            this.action = action;
            this.type = type;
        }

        public string DisplayName => type == ElementType.Folder ? $"[{displayName}]/" : displayName;
    }
}
