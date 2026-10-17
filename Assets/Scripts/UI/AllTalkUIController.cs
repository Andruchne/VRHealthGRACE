using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;

public class AllTalkUIController : MonoBehaviour
{
    [Header("API Settings")]
    public string serverBaseUrl = "http://127.0.0.1:7851"; // Default AllTalk port
    public string voiceFileName = "unity.wav";

    [Header("Local Path Settings")]
    // Point this directly to your local AllTalk voices folder
    public string localVoicesFolderPath = @"C:\alltalk_tts\voices\";

    [Header("UI Components")]
    public Button startRecordingButton;
    public Button stopRecordingButton;
    public Button generateTTSButton;
    public TMP_InputField textInputField;
    public TextMeshProUGUI statusText; // TMP Text for "Available" / "Unavailable"
    public AudioSource audioSource;

    [Header("Recording Settings")]
    private AudioClip recordedAudio;
    private string microphoneID;
    private bool isMicrophoneInitialized = false;

    void Start()
    {
        // Initialize Microphone
        if (Microphone.devices.Length > 0)
        {
            microphoneID = Microphone.devices[0];
            isMicrophoneInitialized = true;
        }
        else
        {
            Debug.LogError("No microphone device found on this system!");
        }

        // Setup Button Listeners
        if (startRecordingButton != null) startRecordingButton.onClick.AddListener(StartRecording);
        if (stopRecordingButton != null) stopRecordingButton.onClick.AddListener(StopAndUploadRecording);
        if (generateTTSButton != null) generateTTSButton.onClick.AddListener(GenerateAndPlayTTS);

        // Check if the voice file already exists on startup
        StartCoroutine(CheckVoiceAvailabilityRoutine());
    }

    // 1. Start Microphone Recording
    public void StartRecording()
    {
        if (!isMicrophoneInitialized) return;

        // Record up to 10 seconds at 22050 Hz
        recordedAudio = Microphone.Start(microphoneID, false, 10, 22050);
        Debug.Log("Recording started...");
    }

    // 2. Stop Recording, convert to WAV and save directly to AllTalk voices folder
    public void StopAndUploadRecording()
    {
        if (!isMicrophoneInitialized) return;

        int recordedSamples = Microphone.GetPosition(microphoneID);
        Microphone.End(microphoneID);
        Debug.Log("Recording stopped. Processing audio...");

        if (recordedSamples <= 0)
        {
            Debug.LogWarning("No audio data recorded.");
            return;
        }

        // Trim the AudioClip to the actual recorded length
        AudioClip trimmedClip = TrimAudioClip(recordedAudio, recordedSamples);

        // Convert AudioClip to WAV byte array using SaveWav helper
        byte[] wavBytes = SaveWav.GetWavBytes(trimmedClip);

        // Save directly to the local AllTalk directory
        try
        {
            Directory.CreateDirectory(localVoicesFolderPath);
            string fullPath = Path.Combine(localVoicesFolderPath, voiceFileName);
            File.WriteAllBytes(fullPath, wavBytes);
            Debug.Log("Voice successfully saved to: " + fullPath);

            // Trigger AllTalk to reload its config and voices
            StartCoroutine(ReloadAllTalkConfigRoutine());
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to save voice file locally: " + e.Message);
        }
    }

    // Helper method to trim the AudioClip duration
    private AudioClip TrimAudioClip(AudioClip originalClip, int lengthSamples)
    {
        float[] data = new float[lengthSamples * originalClip.channels];
        originalClip.GetData(data, 0);

        AudioClip trimmed = AudioClip.Create(originalClip.name, lengthSamples, originalClip.channels, originalClip.frequency, false);
        trimmed.SetData(data, 0);
        return trimmed;
    }

    // Tell AllTalk server to reload configuration and refresh voices
    IEnumerator ReloadAllTalkConfigRoutine()
    {
        using (UnityWebRequest www = UnityWebRequest.Get($"{serverBaseUrl}/api/reload_config"))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Failed to reload AllTalk config: " + www.error);
            }
            else
            {
                Debug.Log("AllTalk config reloaded successfully!");
                UpdateStatusText(true);
            }
        }
    }

    // 3. Check if "unity.wav" is available on the server via /api/voices
    IEnumerator CheckVoiceAvailabilityRoutine()
    {
        using (UnityWebRequest www = UnityWebRequest.Get($"{serverBaseUrl}/api/voices"))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("Could not fetch voices from server. Setting status to Unavailable.");
                UpdateStatusText(false);
            }
            else
            {
                string jsonResponse = www.downloadHandler.text;
                // Check if response contains our target voice file
                bool isAvailable = jsonResponse.Contains(voiceFileName);
                UpdateStatusText(isAvailable);
            }
        }
    }

    // Update TMP Text based on availability
    private void UpdateStatusText(bool available)
    {
        if (statusText != null)
        {
            statusText.text = available ? "Available" : "Unavailable";
        }
    }

    // 4. Generate TTS from TextField and play it immediately
    public void GenerateAndPlayTTS()
    {
        if (textInputField == null || string.IsNullOrEmpty(textInputField.text))
        {
            Debug.LogWarning("Text field is empty!");
            return;
        }

        string textToSpeak = textInputField.text;
        StartCoroutine(GenerateAndPlayTTSRoutine(textToSpeak));
    }

    IEnumerator GenerateAndPlayTTSRoutine(string text)
    {
        WWWForm form = new WWWForm();
        form.AddField("text_input", text);
        form.AddField("voice_select", voiceFileName);
        form.AddField("language", "de"); // Change language if needed (e.g., "en", "ru")
        form.AddField("temperature", "0.7");
        form.AddField("speed", "1.0");

        // Create a POST request with form data and attach a dedicated DownloadHandlerAudioClip to parse the incoming WAV stream
        using (UnityWebRequest www = UnityWebRequest.Post($"{serverBaseUrl}/api/tts-generate", form))
        {
            // Set the download handler to parse the incoming binary data directly as a WAV AudioClip
            www.downloadHandler = new DownloadHandlerAudioClip($"{serverBaseUrl}/api/tts-generate", AudioType.WAV);

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("TTS Generation Error: " + www.error);
            }
            else
            {
                // Retrieve the decoded AudioClip from the custom download handler
                AudioClip clip = ((DownloadHandlerAudioClip)www.downloadHandler).audioClip;

                if (clip != null && audioSource != null)
                {
                    audioSource.clip = clip;
                    audioSource.Play();
                    Debug.Log("TTS generated and playing successfully!");
                }
                else
                {
                    Debug.LogError("Failed to load AudioClip from TTS response.");
                }
            }
        }
    }
}