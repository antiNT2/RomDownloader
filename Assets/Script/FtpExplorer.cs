using FluentFTP;
using FluentFTP.Helpers;
using GScraper;
using GScraper.Google;
using HtmlAgilityPack;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Policy;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using Unity.VisualScripting.Antlr3.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;

public class FtpExplorer : MonoBehaviour
{
    string ftpServer = "ftp.myrient.erista.me";
    //string ftpServer = "cygwin.mirror.rafal.ca";
    int ftpPort = 21;
    string ftpUser = "anonymous";
    string ftpPassword = "anonymous";

    public FolderDisplayer folderDisplayer;
    public FolderExplorer folderExplorer;
    public DownloadPanel downloadPanel;

    public TextMeshProUGUI directoryDisplay;

    CancellationTokenSource TokenSource { get; set; }

    bool isConnected = true;

    string serverUrl = "https://myrient.erista.me/files";

    string lastUrl = "";

    UnityWebRequest currentUnityWebRequest;

    Stack<string> visitedUrls = new Stack<string>();

    private void Start()
    {
        // TestServerResponse();
    }


    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            TestScraper();
        }

        if (InputManager.FindAction("Player/CancelDownload").WasPerformedThisFrame())
        {
            if (currentUnityWebRequest != null)
            {
                CancelCurrentDownload();
            }
            else
            {
                ReturnToPreviousUrl();
            }
        }
    }

    public void CancelCurrentDownload()
    {
        if (currentUnityWebRequest != null)
        {
            currentUnityWebRequest.Abort();
            currentUnityWebRequest = null;
        }
    }

    public async void NavigateToLastUrl()
    {
        if (lastUrl == "")
        {
            lastUrl = serverUrl;
        }

        NavigateToUrl(lastUrl);
    }

    void ReturnToPreviousUrl()
    {
        if (visitedUrls.Count > 1)
        {
            visitedUrls.Pop();
            string previousUrl = visitedUrls.Pop();
            NavigateToUrl(previousUrl);
        }
    }

    public async void NavigateToUrl(string path)
    {
        if (!isConnected)
        {
            directoryDisplay.text = "<color=red>Not connected to FTP server</color>";
            return;
        }

        List<FolderDisplayer.ElementButton> content = await GetDirectoryContent(path);
        //content.Insert(0, new FolderDisplayer.ElementButton("..", GetDirectoryParentPath(path), () => NavigateToPath(GetDirectoryParentPath(path)), FolderDisplayer.ElementButton.ElementType.Folder));
        if (content == null)
        {
            //directoryDisplay.text = $"Error accessing {path}";
            return;
        }

        lastUrl = path;
        visitedUrls.Push(path);
        folderDisplayer.DisplayFilesAndFolders(content.ToArray());
        directoryDisplay.text = $"[{System.DateTime.Now}] {path} ({content.Count} elements)";
    }

    public async void TestServerResponse()
    {
        // Connect to the server and display the returned HTTP code and message

        var client = new HttpClient();
        var response = await client.GetAsync(serverUrl);
        directoryDisplay.text = $"{response.ReasonPhrase} {response}";
    }

    public async Task<List<FolderDisplayer.ElementButton>> GetDirectoryContent(string url)
    {
        try
        {
            List<FolderDisplayer.ElementButton> elements = new List<FolderDisplayer.ElementButton>();

            var handler = new HttpClientHandler()
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 10,

            };

            var client = new HttpClient(handler);

            Debug.Log($"Getting content for {url}");
            directoryDisplay.text = $"Getting content for {url}";

            string clientUrl = url;

            // Make sure the URL ends with a slash
            if (!clientUrl.EndsWith("/"))
            {
                clientUrl += "/";
            }

            var response = await client.GetStringAsync(clientUrl);

            var doc = new HtmlDocument();
            doc.LoadHtml(response);

            var links = doc.DocumentNode.SelectNodes("//td[@class='link']/a");

            if (links != null)
            {
                int downloadCount = 0;
                foreach (var link in links)
                {

                    string downloadLink = link.GetAttributeValue("href", string.Empty);

                    // Get the file size from <td class="size">...</td> (sibling node of the link's parent node)
                    var sizeNode = link.ParentNode.SelectSingleNode("following-sibling::td[@class='size']");

                    string size = sizeNode?.InnerText ?? string.Empty;

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

                        FolderDisplayer.ElementButton element = new FolderDisplayer.ElementButton(folderName, downloadLink, () => NavigateToUrl(downloadLink), FolderDisplayer.ElementButton.ElementType.Folder, "");
                        elements.Add(element);
                    }
                    else
                    {
                        FolderDisplayer.ElementButton element = new FolderDisplayer.ElementButton(fileName, downloadLink, () => { DownloadFileWithProgress(downloadLink, fileName, size); }, FolderDisplayer.ElementButton.ElementType.File, size);
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
            directoryDisplay.text = $"Error accessing {url}: {ex.Message} {ex.InnerException}";
            return null;
        }
    }

    async void DownloadFile(string remotePath, string filename, string size)
    {

        directoryDisplay.text = $"Downloading {filename} ({size})...";

        string localPath = folderExplorer.currentFolder;

        // Download the file through HTTP
        var client = new HttpClient();
        var fileBytes = await client.GetByteArrayAsync(remotePath);

        // Save the file to the local path
        var filePath = System.IO.Path.Combine(localPath, filename);
        await System.IO.File.WriteAllBytesAsync(filePath, fileBytes);

        directoryDisplay.text = $"Downloaded {filename} to {localPath}";

        ExtractZipFile(filePath);

        //using var scraper = new GoogleScraper();

        //IEnumerable<IImageResult> images;
        //try
        //{
        //    images = await scraper.GetImagesAsync($"cdromance {filename}");

        //    // Open the first image in the browser
        //    foreach (var image in images)
        //    {
        //        Debug.Log($"Image: {image.Url}");
        //    }

        //    // Download the first image
        //    var firstImage = images.FirstOrDefault();
        //    Application.OpenURL(firstImage.Url);
        //}
        //catch (Exception ex)
        //{
        //    Debug.Log($"Error: {ex}");
        //}


    }
    async void DownloadFileWithProgress(string remotePath, string filename, string size)
    {

        directoryDisplay.text = $"Downloading {filename} ({size})...";

        string localPath = folderExplorer.currentFolder;

        // Save the file to the local path
        string localFilePath = Path.Combine(localPath, filename);

        currentUnityWebRequest = new UnityWebRequest(remotePath, UnityWebRequest.kHttpVerbGET);

        // Define a Stopwatch to measure time
        System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();

        downloadPanel.ShowDownloadPanel();
        downloadPanel.UpdateGameIllustration(filename);

        StartCoroutine(DownloadFile());
        StartCoroutine(ShowDownloadProgress());

        IEnumerator DownloadFile()
        {
            stopwatch.Start(); // Start the stopwatch when download begins
            currentUnityWebRequest.downloadHandler = new DownloadHandlerFile(localFilePath);
            yield return currentUnityWebRequest.SendWebRequest();

            if (currentUnityWebRequest == null)
            {
                // Aborted

                // Stop the stopwatch when download is aborted
                stopwatch.Stop();

                // Delete the incomplete file
                if (File.Exists(localFilePath))
                {
                    File.Delete(localFilePath);
                }

                directoryDisplay.text = $"Download of {filename} aborted";
                downloadPanel.HideDownloadPanel();
                yield break;
            }

            if (currentUnityWebRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(currentUnityWebRequest.error);
                directoryDisplay.text = $"Error downloading {filename}: {currentUnityWebRequest.error}";
                downloadPanel.HideDownloadPanel();
            }
            else
            {
                stopwatch.Stop(); // Stop the stopwatch when download completes
                directoryDisplay.text = $"Downloaded {filename} to {localPath}";
                ExtractZipFile(localFilePath);
            }

        }

        IEnumerator ShowDownloadProgress()
        {
            while (currentUnityWebRequest != null && !currentUnityWebRequest.isDone)
            {
                // Calculate download speed
                float speed = currentUnityWebRequest.downloadedBytes / (float)stopwatch.Elapsed.TotalSeconds;

                directoryDisplay.text = $"{filename} ({size}) {currentUnityWebRequest.downloadProgress * 100:F2}% | {FormatFileSize(speed)}/s";

                downloadPanel.UpdateDownloadProgress(currentUnityWebRequest.downloadProgress, speed, size, currentUnityWebRequest.downloadedBytes);
                yield return null;
            }
        }

        // Helper function to format file size
        string FormatFileSize(float bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int suffixIndex = 0;
            while (bytes >= 1024 && suffixIndex < suffixes.Length - 1)
            {
                bytes /= 1024;
                suffixIndex++;
            }
            return $"{bytes:F2} {suffixes[suffixIndex]}";
        }
    }


    async void ExtractZipFile(string filePath)
    {
        try
        {
            await ExtractZipFileAsync(filePath);

            // Delete the zip file after extraction
            System.IO.File.Delete(filePath);
        }
        catch (Exception ex)
        {
            directoryDisplay.text = $"Error extracting {filePath}: {ex.Message}";
        }
        finally
        {
            downloadPanel.HideDownloadPanel();
        }

        async Task ExtractZipFileAsync(string filePath)
        {
            string localPath = folderExplorer.currentFolder;

            // Extraction path is the zip file's directory
            string extractPath = System.IO.Path.GetDirectoryName(filePath);

            directoryDisplay.text = $"Extracting {filePath} to {extractPath}...";
            downloadPanel.SetDownloadStatus($"Extracting {filePath} to {extractPath}...");

            // Extract the zip file
            await Task.Run(() => ZipFile.ExtractToDirectory(filePath, extractPath));

            directoryDisplay.text = $"Extracted {filePath} to {localPath}. Download complete";
            downloadPanel.SetDownloadStatus($"Extracted {filePath} to {localPath}. Download complete");
        }
    }

    //async void DownloadFile(string remotePath, string filename)
    //{
    //    // Create progress reporter
    //    var progress = new Progress<(long, long, double)>(progress =>
    //    {
    //        // Progress reporting logic
    //        var (bytesReceived, totalBytes, elapsedTime) = progress;
    //        var progressPercentage = totalBytes <= 0 ? 0 : (double)bytesReceived / totalBytes * 100;
    //        var speed = bytesReceived / elapsedTime / 1024; // Speed in KB/s

    //        double totalSizeInMB = totalBytes / 1024d / 1024d;
    //        // Round to 2 decimal places
    //        totalSizeInMB = Math.Round(totalSizeInMB, 2);

    //        directoryDisplay.text = $"{filename} ({totalSizeInMB} MB) {progressPercentage:F2}% at {speed:F2} KB/s";
    //    });

    //    // Call the DownloadFile method with the progress reporter
    //    await DownloadFileTask(remotePath, filename, progress);
    //}

    async Task DownloadFileTask(string remotePath, string filename, IProgress<(long, long, double)> progressReporter)
    {
        directoryDisplay.text = $"Downloading {filename} from {remotePath}...";

        string localPath = folderExplorer.currentFolder;

        // Download the file through HTTP
        var client = new HttpClient();
        using (var response = await client.GetAsync(remotePath, HttpCompletionOption.ResponseHeadersRead))
        {
            if (!response.IsSuccessStatusCode)
            {
                // Handle error
                return;
            }

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var readBytes = 0L;
            int bufferSize = 8388608; // 8 MB buffer size
            var buffer = new byte[bufferSize]; // 8 MB buffer size

            using (var contentStream = await response.Content.ReadAsStreamAsync())
            using (var fileStream = new FileStream(Path.Combine(localPath, filename), FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, true))
            {
                int bytesRead;
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    readBytes += bytesRead;
                    var progress = (readBytes, totalBytes, stopwatch.Elapsed.TotalSeconds);
                    progressReporter?.Report(progress);
                }
            }
        }

        directoryDisplay.text = $"Downloaded {filename} to {localPath}";
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
}
