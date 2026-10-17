using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class AllTalkTTSPlayer : MonoBehaviour
{
    [Header("Settings")]
    public string serverBaseUrl = "http://127.0.0.1:7851"; // Default AllTalk port
    public AudioSource audioSource;

    // Call this method with your dynamic text to request TTS and play it automatically
    public void RequestAndPlayTTS(string textToSpeak, string voiceName = "Bla.wav", string language = "de")
    {
        StartCoroutine(GetTTSAudioRoutine(textToSpeak, voiceName, language));
    }

    IEnumerator GetTTSAudioRoutine(string text, string voice, string lang)
    {
        WWWForm form = new WWWForm();
        form.AddField("text_input", text);
        form.AddField("voice_select", voice);
        form.AddField("language", lang);
        form.AddField("temperature", "0.45");
        form.AddField("speed", "1.0");

        using (UnityWebRequest www = UnityWebRequest.Post($"{serverBaseUrl}/api/tts-generate", form))
        {
            // Send the request and wait for completion
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("TTS Generation Error: " + www.error);
            }
            else
            {
                // Retrieve the AudioClip directly from the download handler
                DownloadHandlerAudioClip audioHandler = (DownloadHandlerAudioClip)www.downloadHandler;
                AudioClip clip = audioHandler.audioClip;

                if (clip != null && audioSource != null)
                {
                    audioSource.clip = clip;
                    audioSource.Play();
                    Debug.Log("Playing generated TTS audio...");
                }
                else
                {
                    Debug.LogError("Failed to load AudioClip from response.");
                }
            }
        }
    }
}