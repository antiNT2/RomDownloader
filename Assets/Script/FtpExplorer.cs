using FluentFTP;
using FluentFTP.Helpers;
using HtmlAgilityPack;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Policy;
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

    bool isConnected = true;

    CancellationTokenSource downloadCancelTokenSource;
    CancellationToken downloadCancelToken;

    string serverUrl = "https://myrient.erista.me/files";

    private async void Start()
    {
        client = new AsyncFtpClient(ftpServer, ftpUser, ftpPassword);

        // connect to the server and automatically detect working FTP settings

        //FtpProfile ftpProfile = new FtpProfile();
        //ftpProfile.Host = ftpServer;
        //ftpProfile.Credentials = new System.Net.NetworkCredential(ftpUser, ftpPassword);
        //ftpProfile.Protocols = System.Security.Authentication.SslProtocols.None;
        //ftpProfile.Encryption = FtpEncryptionMode.None;
        //ftpProfile.Encoding = System.Text.Encoding.UTF8;


        //await client.Connect(ftpProfile);
        ////await client.AutoConnect();

        //currentFolder = await client.GetWorkingDirectory();

        //Debug.Log("Connected to " + currentFolder);

        //isConnected = true;

        //NavigateToPath(await client.GetWorkingDirectory());
    }

    private void Update()
    {
        if ((Keyboard.current?.backspaceKey.wasPressedThisFrame ?? false) || (Gamepad.current?.selectButton.wasPressedThisFrame ?? false))
        {
            // Cancel download
            downloadCancelTokenSource?.Cancel();
        }

        if (Input.GetKeyDown(KeyCode.T))
        {
            TestScraper();
        }
    }

    public async void NavigateToLastUrl()
    {
        NavigateToUrl(serverUrl);
    }

    public async void NavigateToUrl(string path)
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

    public async Task<List<FolderDisplayer.ElementButton>> GetDirectoryContent(string url)
    {
        try
        {
            List<FolderDisplayer.ElementButton> elements = new List<FolderDisplayer.ElementButton>();

            var client = new HttpClient();

            Debug.Log($"Getting content for {url}");

            var response = await client.GetStringAsync(url);
            var doc = new HtmlDocument();
            doc.LoadHtml(response);

            var links = doc.DocumentNode.SelectNodes("//td[@class='link']/a");

            if (links != null)
            {
                int downloadCount = 0;
                foreach (var link in links)
                {

                    string downloadLink = link.GetAttributeValue("href", string.Empty);
                    if (downloadLink == "../")
                    {
                        // Go to parent directory by removing last part of the URL
                        downloadLink = url;
                        downloadLink = downloadLink.Substring(0, downloadLink.LastIndexOf('/'));
                    }
                    else if (!Uri.IsWellFormedUriString(downloadLink, UriKind.Absolute))
                    {
                        // Create the uri by combining the base url INCULDING /files/ and the relative path
                        downloadLink = @url + "/" + @downloadLink;
                        // Remove the last / if it's there
                        downloadLink = downloadLink.TrimEnd('/');
                    }

                    // Folders are in this format: <a href=link/>folderName/</a>
                    string folderName = link.InnerText;

                    var fileName = System.IO.Path.GetFileName(downloadLink);
                    fileName = Uri.UnescapeDataString(fileName);

                    // if file name ends with a slash, it's a directory
                    if (folderName.EndsWith("/"))
                    {

                        FolderDisplayer.ElementButton element = new FolderDisplayer.ElementButton(folderName, downloadLink, () => NavigateToUrl(downloadLink), FolderDisplayer.ElementButton.ElementType.Folder);
                        elements.Add(element);
                    }
                    else
                    {
                        FolderDisplayer.ElementButton element = new FolderDisplayer.ElementButton(fileName, downloadLink, () => { DownloadFile(downloadLink); }, FolderDisplayer.ElementButton.ElementType.File);
                        elements.Add(element);
                    }

                    downloadCount++;
                }
            }
            else
            {
                Debug.Log("No links found");
            }



            return elements;
        }
        catch (System.Exception ex)
        {
            directoryDisplay.text = $"Error accessing {url}: {ex.Message}";
            return null;
        }
    }

    async void DownloadFile(string remotePath)
    {

        Application.OpenURL(remotePath);
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
            Progress<FtpProgress> progress = new Progress<FtpProgress>(x =>
            {
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

    async void TestScraper()
    {
        string url = "https://myrient.erista.me/files/No-Intro/Nintendo%20-%20Game%20Boy%20Advance/";
        var client = new HttpClient();
        int downloadLimit = -1;

        try
        {
            var response = await client.GetStringAsync(url);
            var doc = new HtmlDocument();
            doc.LoadHtml(response);

            var links = doc.DocumentNode.SelectNodes("//td[@class='link']/a[contains(@href, '.zip')]");

            if (links != null)
            {
                int downloadCount = 0;
                foreach (var link in links)
                {
                    if (downloadLimit != -1 && downloadCount >= downloadLimit)
                    {
                        break;
                    }

                    var downloadLink = link.GetAttributeValue("href", string.Empty);
                    if (!Uri.IsWellFormedUriString(downloadLink, UriKind.Absolute))
                    {
                        downloadLink = new Uri(new Uri(url), downloadLink).AbsoluteUri;
                    }

                    //var fileBytes = await client.GetByteArrayAsync(downloadLink);
                    var fileName = System.IO.Path.GetFileName(downloadLink);
                    fileName = Uri.UnescapeDataString(fileName);

                    //var directoryPath = fileTextBox.Text;
                    //if (!System.IO.Directory.Exists(directoryPath))
                    //{
                    //    System.IO.Directory.CreateDirectory(directoryPath);
                    //}

                    //var filePath = System.IO.Path.Combine(directoryPath, fileName);
                    //await System.IO.File.WriteAllBytesAsync(filePath, fileBytes);

                    Debug.Log($"-{fileName} from {downloadLink}");
                    downloadCount++;
                }
            }
            else
            {
                Debug.Log("No links found");
            }
        }
        catch (Exception ex)
        {
            Debug.Log($"Error: {ex}");
        }
    }

    private void OnDestroy()
    {
        client.Disconnect();
    }
}
