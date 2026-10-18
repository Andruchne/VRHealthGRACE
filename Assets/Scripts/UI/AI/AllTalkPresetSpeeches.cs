using System;
using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// Independent component for generating, caching, and playing three preset speeches.
/// It listens for AllTalkUIController.VoiceSavedAndAvailable instead of putting
/// preset-speech UI and TTS logic inside AllTalkUIController.
/// </summary>
public class AllTalkPresetSpeeches : MonoBehaviour
{
    [Header("Dependencies")]
    public AllTalkUIController voiceController;
    public AllTalkApiClient apiClient;
    public AllTalkTTSPlayer ttsPlayer;

    [Header("Preset Speech Texts")]
    [TextArea(2, 4)] public string speech1Text = "Hello! I'm so glad that you're here.";
    [TextArea(2, 4)] public string speech2Text = "How are you feeling today?";
    [TextArea(2, 4)] public string speech3Text = "You're my best friend!";
    public string speechStorageFolderName = "AllTalkSpeech";

    [Header("Preset Speech Buttons")]
    public Button speech1Button;
    public Button speech2Button;
    public Button speech3Button;
    public AudioSource speechAudioSource;
    public TMP_Text statusText;

    private AudioClip speech1Clip;
    private AudioClip speech2Clip;
    private AudioClip speech3Clip;
    private bool isGenerating;
    private bool isLoadingCache;
    private string pendingVoiceFileName;

    private void Awake()
    {
        if (voiceController == null) voiceController = GetComponent<AllTalkUIController>();
        if (apiClient == null) apiClient = GetComponent<AllTalkApiClient>();
        if (ttsPlayer == null) ttsPlayer = GetComponent<AllTalkTTSPlayer>();

        if (speechAudioSource == null && ttsPlayer != null)
        {
            speechAudioSource = ttsPlayer.audioSource;
        }
    }

    private void OnEnable()
    {
        if (voiceController != null)
        {
            voiceController.VoiceSavedAndAvailable += HandleVoiceSavedAndAvailable;
        }

        if (speech1Button != null) speech1Button.onClick.AddListener(PlaySpeech1);
        if (speech2Button != null) speech2Button.onClick.AddListener(PlaySpeech2);
        if (speech3Button != null) speech3Button.onClick.AddListener(PlaySpeech3);
    }

    private void Start()
    {
        SetButtonsInteractable(false);
        StartCoroutine(LoadCachedSpeechesRoutine());

        if (voiceController == null)
        {
            Debug.LogError("AllTalkPresetSpeeches: Assign an AllTalkUIController component.");
            SetStatus("Preset speeches: voice controller missing");
        }

        if (apiClient == null)
        {
            Debug.LogError("AllTalkPresetSpeeches: Assign an AllTalkApiClient component.");
            SetStatus("Preset speeches: AllTalk client missing");
        }
    }

    private void OnDisable()
    {
        if (voiceController != null)
        {
            voiceController.VoiceSavedAndAvailable -= HandleVoiceSavedAndAvailable;
        }

        if (speech1Button != null) speech1Button.onClick.RemoveListener(PlaySpeech1);
        if (speech2Button != null) speech2Button.onClick.RemoveListener(PlaySpeech2);
        if (speech3Button != null) speech3Button.onClick.RemoveListener(PlaySpeech3);
    }

    private void OnDestroy()
    {
        ClearClips();
    }

    private void HandleVoiceSavedAndAvailable(string voiceFileName)
    {
        if (string.IsNullOrWhiteSpace(voiceFileName))
        {
            Debug.LogWarning("AllTalkPresetSpeeches: The saved voice filename is empty.");
            return;
        }

        // If the cache is still loading, wait so old cached clips cannot overwrite new ones.
        if (isLoadingCache)
        {
            pendingVoiceFileName = voiceFileName;
            return;
        }

        StartPresetGeneration(voiceFileName);
    }

    private void StartPresetGeneration(string voiceFileName)
    {
        if (isGenerating)
        {
            Debug.LogWarning("AllTalkPresetSpeeches: Preset generation is already running.");
            return;
        }

        StartCoroutine(GeneratePresetSpeechesRoutine(voiceFileName));
    }

    private IEnumerator LoadCachedSpeechesRoutine()
    {
        isLoadingCache = true;

        string directory = GetStorageDirectory();
        string path1 = Path.Combine(directory, "Speech1.wav");
        string path2 = Path.Combine(directory, "Speech2.wav");
        string path3 = Path.Combine(directory, "Speech3.wav");

        // Load a cache only when all three WAV files are present, avoiding a mixed set.
        if (!File.Exists(path1) || !File.Exists(path2) || !File.Exists(path3))
        {
            isLoadingCache = false;
            SetButtonsInteractable(false);
            SetStatus("Record a voice to generate the three speeches");
            StartPendingGenerationIfNeeded();
            yield break;
        }

        AudioClip loaded1 = null;
        AudioClip loaded2 = null;
        AudioClip loaded3 = null;
        yield return LoadWavClipRoutine(path1, "Speech1", clip => loaded1 = clip);
        if (loaded1 != null)
        {
            yield return LoadWavClipRoutine(path2, "Speech2", clip => loaded2 = clip);
        }
        if (loaded1 != null && loaded2 != null)
        {
            yield return LoadWavClipRoutine(path3, "Speech3", clip => loaded3 = clip);
        }

        if (loaded1 != null && loaded2 != null && loaded3 != null)
        {
            speech1Clip = loaded1;
            speech2Clip = loaded2;
            speech3Clip = loaded3;
            SetButtonsInteractable(true);
            SetStatus("Cached speeches loaded");
        }
        else
        {
            if (loaded1 != null) Destroy(loaded1);
            if (loaded2 != null) Destroy(loaded2);
            if (loaded3 != null) Destroy(loaded3);
            SetButtonsInteractable(false);
            SetStatus("Speech cache could not be loaded. Record a voice to regenerate it.");
        }

        isLoadingCache = false;
        StartPendingGenerationIfNeeded();
    }

    private IEnumerator LoadWavClipRoutine(string filePath, string clipName, Action<AudioClip> onComplete)
    {
        AudioClip clip = null;
        string fileUrl;

        try
        {
            fileUrl = new Uri(Path.GetFullPath(filePath)).AbsoluteUri;
        }
        catch (Exception exception)
        {
            Debug.LogError("Could not resolve cached speech path: " + exception.Message);
            onComplete?.Invoke(null);
            yield break;
        }

        using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(fileUrl, AudioType.WAV))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                clip = DownloadHandlerAudioClip.GetContent(request);
            }
            else
            {
                Debug.LogError("Could not load cached " + clipName + ": " + request.error);
            }
        }

        if (clip != null) clip.name = clipName;
        onComplete?.Invoke(clip);
    }

    private IEnumerator GeneratePresetSpeechesRoutine(string voiceFileName)
    {
        isGenerating = true;
        SetButtonsInteractable(false);
        ClearClips();

        string directory = GetStorageDirectory();
        try
        {
            Directory.CreateDirectory(directory);
            // Remove the old set first so a failed generation cannot leave a mixed cache.
            DeleteCachedSpeechFiles(directory);
        }
        catch (Exception exception)
        {
            Debug.LogError("Could not prepare preset speech storage: " + exception.Message);
            SetStatus("Could not prepare speech storage");
            isGenerating = false;
            yield break;
        }

        if (apiClient == null)
        {
            Debug.LogError("AllTalkPresetSpeeches: AllTalkApiClient is missing.");
            SetStatus("Preset speech generation failed: AllTalk client missing");
            isGenerating = false;
            yield break;
        }

        SetStatus("Generating Speech1 of 3...");
        yield return GenerateAndSaveSpeechRoutine(speech1Text, "Speech1", voiceFileName,
            clip => speech1Clip = clip);
        if (speech1Clip == null)
        {
            FailPresetGeneration(directory);
            yield break;
        }

        SetStatus("Generating Speech2 of 3...");
        yield return GenerateAndSaveSpeechRoutine(speech2Text, "Speech2", voiceFileName,
            clip => speech2Clip = clip);
        if (speech2Clip == null)
        {
            FailPresetGeneration(directory);
            yield break;
        }

        SetStatus("Generating Speech3 of 3...");
        yield return GenerateAndSaveSpeechRoutine(speech3Text, "Speech3", voiceFileName,
            clip => speech3Clip = clip);
        if (speech3Clip == null)
        {
            FailPresetGeneration(directory);
            yield break;
        }

        SetButtonsInteractable(true);
        SetStatus("All three speeches are ready");
        isGenerating = false;
    }

    private void FailPresetGeneration(string directory)
    {
        Debug.LogError("AllTalkPresetSpeeches: At least one preset speech failed to generate. See Console for details.");
        ClearClips();
        try
        {
            DeleteCachedSpeechFiles(directory);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Could not clean up incomplete speech cache: " + exception.Message);
        }
        SetButtonsInteractable(false);
        SetStatus("Speech generation failed. Check the Unity Console.");
        isGenerating = false;
    }

    private IEnumerator GenerateAndSaveSpeechRoutine(
        string text,
        string speechName,
        string voiceFileName,
        Action<AudioClip> onComplete)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            Debug.LogError("AllTalkPresetSpeeches: Inspector text for " + speechName + " is empty.");
            onComplete?.Invoke(null);
            yield break;
        }

        string language = voiceController != null
            ? voiceController.ttsLanguage
            : (ttsPlayer != null ? ttsPlayer.defaultLanguage : "de");
        float temperature = ttsPlayer != null ? ttsPlayer.temperature : 0.45f;
        float speed = ttsPlayer != null ? ttsPlayer.speed : 1f;

        AudioClip clip = null;
        string errorMessage = null;
        yield return apiClient.GenerateAndDownloadTTS(
            text,
            voiceFileName,
            language,
            temperature,
            speed,
            generated => clip = generated,
            error => errorMessage = error);

        if (clip == null)
        {
            Debug.LogError("Failed to generate " + speechName + ": " + (errorMessage ?? "No AudioClip returned."));
            onComplete?.Invoke(null);
            yield break;
        }

        try
        {
            clip.name = speechName;
            string path = Path.Combine(GetStorageDirectory(), speechName + ".wav");
            File.WriteAllBytes(path, SaveWav.GetWavBytes(clip));
            Debug.Log(speechName + " saved to: " + path);
            onComplete?.Invoke(clip);
        }
        catch (Exception exception)
        {
            Debug.LogError("Failed to save " + speechName + ": " + exception.Message);
            Destroy(clip);
            onComplete?.Invoke(null);
        }
    }

    private void StartPendingGenerationIfNeeded()
    {
        if (!string.IsNullOrWhiteSpace(pendingVoiceFileName))
        {
            string voiceFileName = pendingVoiceFileName;
            pendingVoiceFileName = null;
            StartPresetGeneration(voiceFileName);
        }
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (speech1Button != null) speech1Button.interactable = interactable && speech1Clip != null;
        if (speech2Button != null) speech2Button.interactable = interactable && speech2Clip != null;
        if (speech3Button != null) speech3Button.interactable = interactable && speech3Clip != null;
    }

    public void PlaySpeech1() => PlayPresetSpeech(speech1Clip);
    public void PlaySpeech2() => PlayPresetSpeech(speech2Clip);
    public void PlaySpeech3() => PlayPresetSpeech(speech3Clip);

    private void PlayPresetSpeech(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogWarning("This preset speech has not been generated or loaded yet.");
            return;
        }

        AudioSource source = speechAudioSource != null
            ? speechAudioSource
            : (ttsPlayer != null ? ttsPlayer.audioSource : null);

        if (source == null)
        {
            Debug.LogError("AllTalkPresetSpeeches: Assign a speech AudioSource or the TTS player's AudioSource.");
            return;
        }

        source.Stop();
        source.clip = clip;
        source.Play();
    }

    private void ClearClips()
    {
        AudioSource source = speechAudioSource != null
            ? speechAudioSource
            : (ttsPlayer != null ? ttsPlayer.audioSource : null);

        if (source != null &&
            (source.clip == speech1Clip || source.clip == speech2Clip || source.clip == speech3Clip))
        {
            source.Stop();
            source.clip = null;
        }

        if (speech1Clip != null) Destroy(speech1Clip);
        if (speech2Clip != null) Destroy(speech2Clip);
        if (speech3Clip != null) Destroy(speech3Clip);
        speech1Clip = null;
        speech2Clip = null;
        speech3Clip = null;
    }

    private string GetStorageDirectory()
    {
        string folderName = string.IsNullOrWhiteSpace(speechStorageFolderName)
            ? "AllTalkSpeech"
            : speechStorageFolderName;
        return Path.Combine(Application.persistentDataPath, folderName);
    }

    private static void DeleteCachedSpeechFiles(string directory)
    {
        string[] names = { "Speech1.wav", "Speech2.wav", "Speech3.wav" };
        foreach (string name in names)
        {
            string path = Path.Combine(directory, name);
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message;
    }
}
