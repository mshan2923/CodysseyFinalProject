using System;
using System.IO;
using System.Text;
using UnityEngine;

public static class TrainingBundleTextWriter
{
    private const string DirectoryName = "CodysseyTraining";
    private const string FileName = "codyssey_training_bundles.txt";

    public static string Append(string context, string recognition, string rawResponse, int eventCount)
    {
        string directoryPath = Path.Combine(Application.persistentDataPath, DirectoryName);
        string filePath = Path.Combine(directoryPath, FileName);

        try
        {
            Directory.CreateDirectory(directoryPath);

            var bundle = new TrainingBundle
            {
                bundleId = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"),
                eventCount = eventCount,
                createdAt = DateTime.Now.ToString("o"),
                context = context ?? string.Empty,
                recognition = recognition ?? string.Empty,
                rawResponse = rawResponse ?? string.Empty
            };

            // Keep the .txt extension, but write one complete JSON object per line.
            string jsonLine = JsonUtility.ToJson(bundle);

            File.AppendAllText(
                filePath,
                jsonLine + Environment.NewLine,
                new UTF8Encoding(false));
            Debug.Log($"[TrainingData] Bundle saved: {filePath}");
            return filePath;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[TrainingData] Bundle save failed: {exception.Message}");
            return string.Empty;
        }
    }

    [Serializable]
    private sealed class TrainingBundle
    {
        public string bundleId;
        public int eventCount;
        public string createdAt;
        public string context;
        public string recognition;
        public string rawResponse;
    }
}
