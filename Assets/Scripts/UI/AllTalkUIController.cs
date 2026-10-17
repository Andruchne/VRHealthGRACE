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

    [Header("UI Components")]
    public Button startRecordingButton;
    public Button stopRecordingButton;
    public Button generateTTSButton;
    public TMP_InputField textInputField;
    public TextMeshProUGUI statusText;
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

        // Check if the voice file already exists on the server on startup
        StartCoroutine(CheckVoiceAvailabilityRoutine());
    }

    // Start Microphone Recording
    public void StartRecording()
    {
        if (!isMicrophoneInitialized) return;

        // Record up to 10 seconds at 22050 Hz
        recordedAudio = Microphone.Start(microphoneID, false, 10, 22050);
        Debug.Log("Recording started...");
    }

    // Stop Recording, convert to WAV and upload as "unity.wav"
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

        // Upload to AllTalk and trigger server refresh
        StartCoroutine(UploadVoiceRoutine(wavBytes, voiceFileName));
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

    // Upload routine and server refresh trigger
    IEnumerator UploadVoiceRoutine(byte[] wavData, string fileName)
    {
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", wavData, fileName, "audio/wav");

        using (UnityWebRequest www = UnityWebRequest.Post($"{serverBaseUrl}/api/refresh-voices", form))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Failed to upload voice: " + www.error);
            }
            else
            {
                Debug.Log("Voice successfully uploaded as " + fileName);
                // Update UI status to Available
                UpdateStatusText(true);
            }
        }
    }

    // Check if "unity.wav" is available on the server
    IEnumerator CheckVoiceAvailabilityRoutine()
    {
        // AllTalk provides voice lists via API or we can check via a lightweight request. 
        // Alternatively, we query the voices endpoint.
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

    // Generate TTS from TextField and play it immediately
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
        form.AddField("language", "en"); // Change language if needed (e.g., "en", "de, "ru")
        form.AddField("temperature", "0.7");
        form.AddField("speed", "1.0");

        using (UnityWebRequest www = UnityWebRequest.Post($"{serverBaseUrl}/api/tts-generate", form))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("TTS Generation Error: " + www.error);
            }
            else
            {
                // Load AudioClip directly from download handler response
                DownloadHandlerAudioClip audioHandler = (DownloadHandlerAudioClip)www.downloadHandler;
                AudioClip clip = audioHandler.audioClip;

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