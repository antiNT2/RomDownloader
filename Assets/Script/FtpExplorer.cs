using FluentFTP;
using FluentFTP.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class FtpExplorer : MonoBehaviour
{
    string ftpServer = "ftp.myrient.erista.me";
    //string ftpServer = "cygwin.mirror.rafal.ca";
    int ftpPort = 21;
    string ftpUser = "anonymous";
    string ftpPassword = "anonymous";

    public FolderDisplayer folderDisplayer;
    public FolderExplorer folderExplorer;

    public TextMeshProUGUI directoryDisplay;

    AsyncFtpClient client;

    string currentFolder;

    bool isConnected = false;

    CancellationTokenSource downloadCancelTokenSource;
    CancellationToken downloadCancelToken;

    private async void Start()
    {
        client = new AsyncFtpClient(ftpServer, ftpUser, ftpPassword);

        // connect to the server and automatically detect working FTP settings

        FtpProfile ftpProfile = new FtpProfile();
        ftpProfile.Host = ftpServer;
        ftpProfile.Credentials = new System.Net.NetworkCredential(ftpUser, ftpPassword);
        ftpProfile.Protocols = System.Security.Authentication.SslProtocols.None;
        ftpProfile.Encryption = FtpEncryptionMode.None;
        ftpProfile.Encoding = System.Text.Encoding.UTF8;


        await client.Connect(ftpProfile);
        //await client.AutoConnect();

        currentFolder = await client.GetWorkingDirectory();

        Debug.Log("Connected to " + currentFolder);

        isConnected = true;

        //NavigateToPath(await client.GetWorkingDirectory());
    }

    private void Update()
    {
        if((Keyboard.current?.backspaceKey.wasPressedThisFrame ?? false) || (Gamepad.current?.selectButton.wasPressedThisFrame ?? false))
        {
            // Cancel download
            downloadCancelTokenSource?.Cancel();
        }
    }

    public async void NavigateToPath(string path)
    {
        if (!isConnected)
        {
            directoryDisplay.text = "<color=red>Not connected to FTP server</color>";
            return;
        }

        currentFolder = path;
        List<FolderDisplayer.ElementButton> content = await GetDirectoryContent(path);
        //content.Insert(0, new FolderDisplayer.ElementButton("..", GetDirectoryParentPath(path), () => NavigateToPath(GetDirectoryParentPath(path)), FolderDisplayer.ElementButton.ElementType.Folder));
        if (content == null)
        {
            directoryDisplay.text = $"Error accessing {path}";
            return;
        }

        folderDisplayer.DisplayFilesAndFolders(content.ToArray());
        directoryDisplay.text = $"[{System.DateTime.Now}][FTP] {path} ({content.Count} elements)";
    }

    //string GetDirectoryParentPath(string directory)
    //{
    //    return GetFt
    //}

    public async Task<List<FolderDisplayer.ElementButton>> GetDirectoryContent(string directory)
    {
        try
        {
            await client.SetWorkingDirectory(directory);

            List<FolderDisplayer.ElementButton> elements = new List<FolderDisplayer.ElementButton>();

            foreach (FtpListItem item in await client.GetListing(await client.GetWorkingDirectory()))
            {
                if (item.Type == FtpObjectType.Directory)
                {
                    FolderDisplayer.ElementButton element = new FolderDisplayer.ElementButton(item.Name, item.FullName, () => NavigateToPath(directory + "/" + item.Name), FolderDisplayer.ElementButton.ElementType.Folder);
                    elements.Add(element);
                }
                else if (item.Type == FtpObjectType.File)
                {
                    FolderDisplayer.ElementButton element = new FolderDisplayer.ElementButton(item.Name, item.FullName, () => { DownloadFile(item.FullName); }, FolderDisplayer.ElementButton.ElementType.File);
                    elements.Add(element);
                }

            }

            return elements;
        }
        catch (System.Exception ex)
        {
            directoryDisplay.text = $"FTP Error accessing {directory}: {ex.Message}";
            return null;
        }
    }

    async void DownloadFile(string remotePath)
    {

        string link = $"https://myrient.erista.me/files{remotePath}";

        // Encode the link
        link = Uri.EscapeUriString(link);

        Application.OpenURL(link);
        //downloadCancelTokenSource = new CancellationTokenSource();
        //downloadCancelToken = downloadCancelTokenSource.Token;

        //string localPath = folderExplorer.currentFolder;
        //directoryDisplay.text = $"Downloading {remotePath} to {localPath}";

        //string fileName = Path.GetFileName(remotePath);


        //Progress<FtpProgress> progress = new Progress<FtpProgress>(x =>
        //{

        //    // When progress in unknown, -1 will be sent
        //    if (x.Progress < 0)
        //    {
        //        directoryDisplay.text = $"Downloading {remotePath} to {localPath}...";
        //    }
        //    else
        //    {
        //        int transferSpeedInMegaBytes = (int)(x.TransferSpeed / 1024 / 1024);
        //        float downloadProgressRounded = (float)Math.Round(x.Progress, 2);
        //        directoryDisplay.text = $"DL {fileName} to {localPath}... ({transferSpeedInMegaBytes} MB/s) ({downloadProgressRounded}%) (ETA: {Math.Round(x.ETA.TotalMinutes, 2)} min)";

        //    }
        //});

        //try
        //{
        //    await client.DownloadFile(Path.Combine(localPath, fileName), remotePath, FtpLocalExists.Resume, FtpVerify.None, progress, downloadCancelToken);
        //    directoryDisplay.text = $"Downloaded {remotePath} to {localPath}";
        //}
        //catch (System.Exception ex)
        //{
        //    directoryDisplay.text = $"Error downloading {remotePath}: {ex.Message} {ex.InnerException}";
        //}
    }

    async Task DownloadFileAsync(string remotePath)
    {
        var token = new CancellationToken();
        string localPath = folderExplorer.currentFolder;
        string fileName = Path.GetFileName(remotePath);

        directoryDisplay.text = $"Downloading {remotePath} to {localPath}";

        using (var ftp = new AsyncFtpClient(ftpServer, ftpUser, ftpPassword, ftpPort))
        {
            await ftp.Connect(token);

            // define the progress tracking callback
            Progress<FtpProgress> progress = new Progress<FtpProgress>(x => {
                if (x.Progress == 1)
                {
                    // all done!
                }
                else
                {
                    // percent done = (p.Progress * 100)
                    directoryDisplay.text = $"DL {fileName} to {localPath}... ({x.TransferSpeed} B/s) ({x.Progress}%) (ETA: {x.ETA})";
                }
            });

            // download a file and ensure the local directory is created
            await ftp.DownloadFile(Path.Combine(localPath, fileName), remotePath, FtpLocalExists.Resume, FtpVerify.None, progress, token);

        }
    }

    private void OnDestroy()
    {
        client.Disconnect();
    }
}
