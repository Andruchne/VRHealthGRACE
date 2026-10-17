using System.Collections;
using UnityEngine;

/// <summary>
/// Requests generated speech from AllTalk and plays it through an AudioSource.
/// This component does not create or manage UI controls.
/// </summary>
public class AllTalkTTSPlayer : MonoBehaviour
{
    [Header("Dependencies")]
    public AllTalkApiClient apiClient;
    public AudioSource audioSource;

    [Header("Default TTS Settings")]
    public string defaultVoiceFileName = "Bla.wav";
    public string defaultLanguage = "de";
    [Range(0.1f, 1.0f)] public float temperature = 0.45f;
    [Min(0.1f)] public float speed = 1.0f;

    private bool isGenerating;
    private AudioClip generatedClip;

    public bool IsGenerating => isGenerating;

    public void RequestAndPlayTTS(string textToSpeak, string voiceName = null, string language = null)
    {
        if (isGenerating)
        {
            Debug.LogWarning("AllTalk is already generating speech.");
            return;
        }

        if (apiClient == null)
        {
            Debug.LogError("AllTalkTTSPlayer: Assign an AllTalkApiClient component.");
            return;
        }

        if (audioSource == null)
        {
            Debug.LogError("AllTalkTTSPlayer: Assign an AudioSource.");
            return;
        }

        if (string.IsNullOrWhiteSpace(textToSpeak))
        {
            Debug.LogWarning("AllTalkTTSPlayer: Text to speak is empty.");
            return;
        }

        string voice = string.IsNullOrWhiteSpace(voiceName) ? defaultVoiceFileName : voiceName;
        string lang = string.IsNullOrWhiteSpace(language) ? defaultLanguage : language;
        StartCoroutine(GenerateAndPlayRoutine(textToSpeak, voice, lang));
    }

    private IEnumerator GenerateAndPlayRoutine(string text, string voice, string language)
    {
        isGenerating = true;
        AudioClip newClip = null;
        string errorMessage = null;

        yield return apiClient.GenerateAndDownloadTTS(
            text,
            voice,
            language,
            temperature,
            speed,
            clip => newClip = clip,
            error => errorMessage = error);

        isGenerating = false;

        if (newClip == null)
        {
            Debug.LogError("AllTalk TTS failed: " + (errorMessage ?? "No audio clip was returned."));
            yield break;
        }

        audioSource.Stop();
        audioSource.clip = null;

        if (generatedClip != null)
        {
            Destroy(generatedClip);
        }

        generatedClip = newClip;
        audioSource.clip = generatedClip;
        audioSource.Play();

        Debug.Log("AllTalk TTS playing. Duration: " + generatedClip.length + " seconds.");
    }

    private void OnDestroy()
    {
        if (audioSource != null && audioSource.clip == generatedClip)
        {
            audioSource.Stop();
            audioSource.clip = null;
        }

        if (generatedClip != null)
        {
            Destroy(generatedClip);
        }
    }
}
