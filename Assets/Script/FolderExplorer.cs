using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class FolderExplorer : MonoBehaviour
{
    public TextMeshProUGUI directoryDisplay;

    public TextMeshProUGUI currentViewDisplay;

    public FolderDisplayer folderDisplayer;
    public FtpExplorer ftpExplorer;

    public string currentFolder = "";

    ViewType currentView = ViewType.Local;

    enum ViewType
    {
        Local,
        Server
    }

    private void Start()
    {
        currentFolder = GetHomePartitionPath();
        SetView(ViewType.Local);
    }

    private void Update()
    {
        if(InputManager.FindAction("Player/Switch").WasPerformedThisFrame())
        {
            ToggleView();

        }

        if (InputManager.FindAction("Player/Quit").WasPerformedThisFrame())
        {
            CloseApp();
        }

    }

    public void CloseApp()
    {
        Application.Quit();
    }

    string GetDirectoryParentPath(string directory)
    {
        return Path.GetDirectoryName(directory);
    }

    void NavigateToPath(string path)
    {
        currentFolder = path;
        List<FolderDisplayer.ElementButton> content = GetDirectoryContent(path);
        content.Insert(0, new FolderDisplayer.ElementButton("..", GetDirectoryParentPath(path), () => NavigateToPath(GetDirectoryParentPath(path)), FolderDisplayer.ElementButton.ElementType.Folder, ""));
        if (content == null)
        {
            directoryDisplay.text = $"Error accessing {path}";
            return;
        }

        folderDisplayer.DisplayFilesAndFolders(content.ToArray());
        directoryDisplay.text = $@"[{System.DateTime.Now}] {path} ({content.Count} elements)";
    }

    string GetHomePartitionPath()
    {
        if (Application.platform == RuntimePlatform.LinuxPlayer)
        {
            if (Directory.Exists("/userdata/roms"))
                return $"/userdata/roms";
        }
        return System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
    }

    void CreateTextFile()
    {
        string homeDirectory = GetHomePartitionPath();
        string filePath = Path.Combine(homeDirectory, $"NewTextFile{Random.Range(0, 999)}.txt");

        try
        {
            File.WriteAllText(filePath, "Hello, World!");
            Debug.Log("Text file created successfully.");
            directoryDisplay.text = $"Text file created successfully at {filePath}";
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Error creating text file: " + ex.Message);
            directoryDisplay.text = "Error creating text file. " + ex.Message;
        }
    }

    public List<FolderDisplayer.ElementButton> GetDirectoryContent(string directory)
    {
        // Check if the home directory exists
        if (!Directory.Exists(directory))
        {
            Debug.LogError("Home directory not found.");
            return null;
        }

        try
        {
            // Get all the folders and files in the home directory
            string[] directories = Directory.GetDirectories(directory);
            string[] files = Directory.GetFiles(directory);

            List<FolderDisplayer.ElementButton> elements = new List<FolderDisplayer.ElementButton>();
            foreach (string d in directories)
            {
                FolderDisplayer.ElementButton element = new FolderDisplayer.ElementButton(Path.GetFileName(d), d, () => NavigateToPath(d), FolderDisplayer.ElementButton.ElementType.Folder, "");
                elements.Add(element);
            }

            foreach (string f in files)
            {
                int fileSizeInBytes = (int)new FileInfo(f).Length;
                float fileSizeInMB = fileSizeInBytes / 1024f / 1024f;
                fileSizeInMB = Mathf.Round(fileSizeInMB * 100f) / 100f;

                string fileSize = $"{fileSizeInMB} MB";

                FolderDisplayer.ElementButton element = new FolderDisplayer.ElementButton(Path.GetFileName(f), f, () => { }, FolderDisplayer.ElementButton.ElementType.File, fileSize);
                elements.Add(element);
            }

            return elements;
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Error accessing home partition content: " + ex.Message);
            return null;
        }
    }

    public void ToggleView()
    {
        SetView(currentView == ViewType.Local ? ViewType.Server : ViewType.Local);
    }

    void SetView(ViewType view)
    {
        currentView = view;
        currentViewDisplay.text = currentView.ToString();

        switch (currentView)
        {
            case ViewType.Local:
                NavigateToPath(currentFolder);
                break;
            case ViewType.Server:
                folderDisplayer.DisplayFilesAndFolders(new FolderDisplayer.ElementButton[] { });
                ftpExplorer.NavigateToLastUrl();
                break;
        }
    }
}
