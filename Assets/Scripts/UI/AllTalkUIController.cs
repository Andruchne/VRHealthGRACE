using System;
using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// Connects the UI to microphone recording, voice-file saving, and TTS playback.
/// HTTP requests are delegated to AllTalkApiClient and speech playback to AllTalkTTSPlayer.
/// </summary>
public class AllTalkUIController : MonoBehaviour
{
    [Header("Dependencies")]
    public AllTalkApiClient apiClient;
    public AllTalkTTSPlayer ttsPlayer;

    [Header("Voice Settings")]
    public string voiceFileName = "unity.wav";
    public string localVoicesFolderPath = @"C:\alltalk_tts\voices\";
    public string ttsLanguage = "de";

    [Header("UI Components")]
    public Button recordButton;
    public TMP_Text recordButtonLabel;
    public Image recordButtonImage;
    public Slider recordingProgressSlider;
    public Button generateTTSButton;
    public TMP_InputField textInputField;
    public TMP_Text statusText;

    [Header("Recording Settings")]
    [Min(1)] public int maxRecordingSeconds = 10;
    [Min(8000)] public int recordingSampleRate = 22050;
    public Color recordingButtonColor = new Color(0.85f, 0.12f, 0.12f, 1f);
    public string idleButtonLabel = "Record Voice";
    public string recordingButtonLabelText = "Stop & Save";

    private AudioClip recordedAudio;
    private string microphoneID;
    private bool isMicrophoneInitialized;
    private bool isRecording;
    private bool isSavingVoice;
    private float recordingElapsed;
    private Color idleButtonColor = Color.white;

    private void Awake()
    {
        if (apiClient == null)
        {
            apiClient = GetComponent<AllTalkApiClient>();
        }

        if (ttsPlayer == null)
        {
            ttsPlayer = GetComponent<AllTalkTTSPlayer>();
        }
    }

    private void Start()
    {
        InitializeMicrophone();
        ConfigureRecordingUI();

        if (recordButton != null)
        {
            recordButton.onClick.AddListener(ToggleRecording);
        }

        if (generateTTSButton != null)
        {
            generateTTSButton.onClick.AddListener(GenerateAndPlayTTS);
        }

        if (apiClient == null)
        {
            Debug.LogError("AllTalkUIController: Assign an AllTalkApiClient component.");
            SetStatus("AllTalk client missing");
            return;
        }

        StartCoroutine(CheckVoiceAvailabilityRoutine());
    }

    private void Update()
    {
        if (!isRecording)
        {
            return;
        }

        recordingElapsed += Time.unscaledDeltaTime;

        if (recordingProgressSlider != null)
        {
            recordingProgressSlider.value = Mathf.Clamp01(recordingElapsed / maxRecordingSeconds);
        }

        if (recordingElapsed >= maxRecordingSeconds)
        {
            StopRecordingAndSave(true);
        }
    }

    public void ToggleRecording()
    {
        if (isSavingVoice)
        {
            return;
        }

        if (isRecording)
        {
            StopRecordingAndSave(false);
        }
        else
        {
            StartRecording();
        }
    }

    public void StartRecording()
    {
        if (!isMicrophoneInitialized || string.IsNullOrEmpty(microphoneID))
        {
            SetStatus("No microphone available");
            Debug.LogWarning("AllTalkUIController: Cannot record because no microphone is available.");
            return;
        }

        if (isRecording || isSavingVoice)
        {
            return;
        }

        recordedAudio = Microphone.Start(microphoneID, false, maxRecordingSeconds, recordingSampleRate);
        if (recordedAudio == null)
        {
            SetStatus("Could not start microphone");
            Debug.LogError("AllTalkUIController: Microphone.Start returned no AudioClip.");
            return;
        }

        isRecording = true;
        recordingElapsed = 0f;

        if (recordingProgressSlider != null)
        {
            recordingProgressSlider.value = 0f;
            recordingProgressSlider.gameObject.SetActive(true);
        }

        UpdateRecordingButton(true);
        SetStatus("Recording...");
        Debug.Log("AllTalk voice recording started.");
    }

    private void StopRecordingAndSave(bool reachedTimeLimit)
    {
        if (!isRecording)
        {
            return;
        }

        int recordedSamples = 0;
        bool microphoneStillRecording = !string.IsNullOrEmpty(microphoneID) && Microphone.IsRecording(microphoneID);

        if (!string.IsNullOrEmpty(microphoneID))
        {
            recordedSamples = Microphone.GetPosition(microphoneID);

            if (microphoneStillRecording)
            {
                Microphone.End(microphoneID);
            }
        }

        isRecording = false;
        UpdateRecordingButton(false);

        if (recordingProgressSlider != null)
        {
            recordingProgressSlider.value = 0f;
            recordingProgressSlider.gameObject.SetActive(false);
        }

        if (recordedAudio == null)
        {
            SetStatus("No audio recorded");
            Debug.LogWarning("AllTalkUIController: The recording AudioClip is missing.");
            return;
        }

        if (reachedTimeLimit && (recordedSamples <= 0 || !microphoneStillRecording))
        {
            recordedSamples = recordedAudio.samples;
        }

        recordedSamples = Mathf.Clamp(recordedSamples, 0, recordedAudio.samples);
        if (recordedSamples <= 0)
        {
            SetStatus("No audio recorded");
            Debug.LogWarning("AllTalkUIController: No microphone samples were captured.");
            Destroy(recordedAudio);
            recordedAudio = null;
            return;
        }

        AudioClip trimmedClip = null;
        byte[] wavBytes;

        try
        {
            trimmedClip = TrimAudioClip(recordedAudio, recordedSamples);
            wavBytes = SaveWav.GetWavBytes(trimmedClip);
        }
        catch (Exception exception)
        {
            SetStatus("Could not process recording");
            Debug.LogError("AllTalkUIController: Failed to process microphone audio: " + exception.Message);
            if (trimmedClip != null) Destroy(trimmedClip);
            Destroy(recordedAudio);
            recordedAudio = null;
            return;
        }

        if (trimmedClip != null) Destroy(trimmedClip);
        Destroy(recordedAudio);
        recordedAudio = null;

        isSavingVoice = true;
        if (recordButton != null) recordButton.interactable = false;
        SetStatus("Saving voice...");
        StartCoroutine(SaveVoiceAndRefreshRoutine(wavBytes));
    }

    private AudioClip TrimAudioClip(AudioClip sourceClip, int lengthSamples)
    {
        if (sourceClip == null)
        {
            throw new ArgumentNullException(nameof(sourceClip));
        }

        lengthSamples = Mathf.Clamp(lengthSamples, 1, sourceClip.samples);
        int channels = sourceClip.channels;
        float[] data = new float[lengthSamples * channels];

        if (!sourceClip.GetData(data, 0))
        {
            throw new InvalidOperationException("Unity could not read the recorded audio data.");
        }

        AudioClip trimmedClip = AudioClip.Create(
            sourceClip.name + "_trimmed",
            lengthSamples,
            channels,
            sourceClip.frequency,
            false);

        if (!trimmedClip.SetData(data, 0))
        {
            Destroy(trimmedClip);
            throw new InvalidOperationException("Unity could not copy the recorded audio data.");
        }

        return trimmedClip;
    }

    private IEnumerator SaveVoiceAndRefreshRoutine(byte[] wavBytes)
    {
        string fullPath;

        try
        {
            Directory.CreateDirectory(localVoicesFolderPath);
            string safeFileName = Path.GetFileName(voiceFileName);
            if (string.IsNullOrWhiteSpace(safeFileName))
            {
                throw new InvalidOperationException("Voice filename is empty.");
            }

            fullPath = Path.Combine(localVoicesFolderPath, safeFileName);
            File.WriteAllBytes(fullPath, wavBytes);
            Debug.Log("AllTalk voice saved to: " + fullPath);
        }
        catch (Exception exception)
        {
            Debug.LogError("Failed to save the voice file: " + exception.Message);
            SetStatus("Failed to save voice");
            FinishSavingVoice();
            yield break;
        }

        if (apiClient == null)
        {
            SetStatus("Voice saved, but AllTalk client is missing");
            FinishSavingVoice();
            yield break;
        }

        bool reloadSucceeded = false;
        string reloadMessage = null;
        yield return apiClient.ReloadConfig((success, message) =>
        {
            reloadSucceeded = success;
            reloadMessage = message;
        });

        if (!reloadSucceeded)
        {
            Debug.LogError("Voice was saved locally, but AllTalk config reload failed: " + reloadMessage);
            SetStatus("Saved locally; AllTalk reload failed");
            FinishSavingVoice();
            yield break;
        }

        bool available = false;
        yield return apiClient.CheckVoiceAvailability(voiceFileName, result => available = result);
        UpdateVoiceAvailabilityStatus(available);

        if (available)
        {
            Debug.Log("AllTalk voice refreshed and available.");
        }
        else
        {
            Debug.LogWarning("The file was saved and AllTalk was refreshed, but the voice was not found in /api/voices.");
        }

        FinishSavingVoice();
    }

    private IEnumerator CheckVoiceAvailabilityRoutine()
    {
        bool available = false;
        yield return apiClient.CheckVoiceAvailability(voiceFileName, result => available = result);
        UpdateVoiceAvailabilityStatus(available);
    }

    public void GenerateAndPlayTTS()
    {
        if (isRecording || isSavingVoice)
        {
            Debug.LogWarning("Finish recording and saving the voice before generating TTS.");
            return;
        }

        if (textInputField == null || string.IsNullOrWhiteSpace(textInputField.text))
        {
            Debug.LogWarning("AllTalkUIController: Text field is empty.");
            return;
        }

        if (ttsPlayer == null)
        {
            Debug.LogError("AllTalkUIController: Assign an AllTalkTTSPlayer component.");
            return;
        }

        ttsPlayer.RequestAndPlayTTS(textInputField.text, voiceFileName, ttsLanguage);
    }

    private void InitializeMicrophone()
    {
        if (Microphone.devices.Length == 0)
        {
            isMicrophoneInitialized = false;
            Debug.LogError("No microphone device found on this system.");
            return;
        }

        microphoneID = Microphone.devices[0];
        isMicrophoneInitialized = true;
        Debug.Log("Microphone found: " + microphoneID);
    }

    private void ConfigureRecordingUI()
    {
        if (recordButtonImage == null && recordButton != null)
        {
            recordButtonImage = recordButton.targetGraphic as Image;
        }

        if (recordButtonImage != null)
        {
            idleButtonColor = recordButtonImage.color;
        }

        if (recordingProgressSlider != null)
        {
            recordingProgressSlider.minValue = 0f;
            recordingProgressSlider.maxValue = 1f;
            recordingProgressSlider.value = 0f;
            recordingProgressSlider.gameObject.SetActive(false);
        }

        UpdateRecordingButton(false);
    }

    private void UpdateRecordingButton(bool recording)
    {
        if (recordButtonLabel != null)
        {
            recordButtonLabel.text = recording ? recordingButtonLabelText : idleButtonLabel;
        }

        if (recordButtonImage != null)
        {
            recordButtonImage.color = recording ? recordingButtonColor : idleButtonColor;
        }

        if (recordButton != null && !isSavingVoice)
        {
            recordButton.interactable = true;
        }
    }

    private void FinishSavingVoice()
    {
        isSavingVoice = false;
        if (recordButton != null) recordButton.interactable = true;
    }

    private void UpdateVoiceAvailabilityStatus(bool available)
    {
        SetStatus(available ? "Available" : "Unavailable");
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    private void OnDestroy()
    {
        if (recordButton != null)
        {
            recordButton.onClick.RemoveListener(ToggleRecording);
        }

        if (generateTTSButton != null)
        {
            generateTTSButton.onClick.RemoveListener(GenerateAndPlayTTS);
        }

        if (isRecording && !string.IsNullOrEmpty(microphoneID) && Microphone.IsRecording(microphoneID))
        {
            Microphone.End(microphoneID);
        }

        if (recordedAudio != null)
        {
            Destroy(recordedAudio);
        }
    }
}
