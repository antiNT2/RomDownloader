using GScraper;
using GScraper.Google;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class DownloadPanel : MonoBehaviour
{
    public GameObject downloadPanel;
    public Image gameIllustration;

    public Image downloadProgressBar;

    public TextMeshProUGUI downloadProgressText;
    public TextMeshProUGUI downloadSpeedText;

    public TextMeshProUGUI gameName;

    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.F1))
        {
            ShowDownloadPanel();
            UpdateGameIllustration("Final Fantasy VII");
        }
    }

    public void ShowDownloadPanel()
    {
        downloadPanel.SetActive(true);
    }

    public void HideDownloadPanel()
    {
        downloadPanel.SetActive(false);
    }

    public void SetDownloadStatus(string status)
    {
        downloadSpeedText.text = status;
    }

    public void UpdateDownloadProgress(float progress, float speed, string size, ulong downloadedBytes)
    {
        downloadProgressBar.fillAmount = progress;
        downloadProgressText.text = $"{progress * 100:F2}%";


        downloadSpeedText.text = $"{FormatFileSize(speed)}/s - {FormatFileSize(downloadedBytes)} / {size} ";
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

    public async void UpdateGameIllustration(string filename)
    {

        using var scraper = new GoogleScraper();

        gameName.text = filename;

        IEnumerable<IImageResult> images;
        try
        {
            images = await scraper.GetImagesAsync($"cdromance {filename}");

            // Download the first image
            var firstImage = images.FirstOrDefault();
            
            string imageUrl = firstImage.Url;

            // TODO Download the image and apply it to the gameIllustration
            
            StartCoroutine(ApplyImage(imageUrl));

        }
        catch (Exception ex)
        {
            Debug.Log($"Error: {ex}");
        }

        IEnumerator ApplyImage(string imageUrl)
        {
            // Download the image and apply it to the gameIllustration
            var uwrTexture = UnityWebRequestTexture.GetTexture(imageUrl);
            yield return uwrTexture.SendWebRequest();

            if (uwrTexture.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Failed to download image: {uwrTexture.error}");
            }
            else
            {
                //// Apply the downloaded texture to the gameIllustration Image component
                //gameIllustration.sprite = Sprite.Create(
                //    ((DownloadHandlerTexture)uwrTexture.downloadHandler).texture,
                //    new Rect(0, 0, ((DownloadHandlerTexture)uwrTexture.downloadHandler).texture.width, ((DownloadHandlerTexture)uwrTexture.downloadHandler).texture.height),
                //    new Vector2(0.5f, 0.5f)
                //);

                // Get the downloaded texture
                Texture2D texture = ((DownloadHandlerTexture)uwrTexture.downloadHandler).texture;

                // Calculate the aspect ratio of the downloaded image
                float aspectRatio = (float)texture.width / texture.height;

                // Calculate the size of the image while maintaining its aspect ratio
                float imageSizeX = Mathf.Min(gameIllustration.rectTransform.rect.width, gameIllustration.rectTransform.rect.height * aspectRatio);
                float imageSizeY = imageSizeX / aspectRatio;

                // Set the size of the image
                gameIllustration.rectTransform.sizeDelta = new Vector2(imageSizeX, imageSizeY);

                // Create a sprite from the texture
                Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));

                // Apply the sprite to the gameIllustration Image component
                gameIllustration.sprite = sprite;
            }
        }
    }
}
