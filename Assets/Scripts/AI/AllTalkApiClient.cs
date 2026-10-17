using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class AllTalkTTSResponse
{
    public string status;
    public string output_file_path;
    public string output_file_url;
    public string output_cache_url;
}

/// <summary>
/// Handles communication with the AllTalk HTTP API.
/// Keep networking and API response parsing in this component.
/// </summary>
public class AllTalkApiClient : MonoBehaviour
{
    [Header("API Settings")]
    [SerializeField] private string serverBaseUrl = "http://127.0.0.1:7851";

    public string ServerBaseUrl => serverBaseUrl.TrimEnd('/');

    public IEnumerator GenerateAndDownloadTTS(
        string text,
        string voiceFileName,
        string language,
        float temperature,
        float speed,
        Action<AudioClip> onSuccess,
        Action<string> onError)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            onError?.Invoke("The text to synthesize is empty.");
            yield break;
        }

        if (string.IsNullOrWhiteSpace(voiceFileName))
        {
            onError?.Invoke("No voice filename was provided.");
            yield break;
        }

        WWWForm form = new WWWForm();
        form.AddField("text_input", text);
        form.AddField("text_filtering", "standard");
        form.AddField("character_voice_gen", voiceFileName);
        form.AddField("narrator_enabled", "false");
        form.AddField("narrator_voice_gen", voiceFileName);
        form.AddField("text_not_inside", "character");
        form.AddField("language", language);
        form.AddField("temperature", temperature.ToString(CultureInfo.InvariantCulture));
        form.AddField("speed", speed.ToString(CultureInfo.InvariantCulture));
        form.AddField("output_file_name", "unity_tts");
        form.AddField("output_file_timestamp", "true");
        form.AddField("autoplay", "false");
        form.AddField("autoplay_volume", "0.8");

        string json;
        AllTalkTTSResponse response;

        using (UnityWebRequest request = UnityWebRequest.Post(ServerBaseUrl + "/api/tts-generate", form))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                string body = request.downloadHandler != null ? request.downloadHandler.text : "<no response body>";
                onError?.Invoke("AllTalk generation request failed: " + request.error + "\nResponse: " + body);
                yield break;
            }

            json = request.downloadHandler.text;
            try
            {
                response = JsonUtility.FromJson<AllTalkTTSResponse>(json);
            }
            catch (Exception exception)
            {
                onError?.Invoke("Could not parse the AllTalk JSON response: " + exception.Message + "\nResponse: " + json);
                yield break;
            }
        }

        if (response == null ||
            response.status != "generate-success" ||
            string.IsNullOrWhiteSpace(response.output_file_url))
        {
            onError?.Invoke("AllTalk did not return a valid audio URL. Response: " + json);
            yield break;
        }

        string audioUrl = ResolveUrl(response.output_file_url);

        using (UnityWebRequest audioRequest = UnityWebRequestMultimedia.GetAudioClip(audioUrl, AudioType.WAV))
        {
            yield return audioRequest.SendWebRequest();

            if (audioRequest.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke("Failed to download/decode the generated WAV: " + audioRequest.error + "\nURL: " + audioUrl);
                yield break;
            }

            AudioClip clip = DownloadHandlerAudioClip.GetContent(audioRequest);
            if (clip == null)
            {
                onError?.Invoke("Unity could not decode the WAV returned by AllTalk. URL: " + audioUrl);
                yield break;
            }

            clip.name = "AllTalkTTS";
            onSuccess?.Invoke(clip);
        }
    }

    public IEnumerator CheckVoiceAvailability(string voiceFileName, Action<bool> onComplete)
    {
        if (string.IsNullOrWhiteSpace(voiceFileName))
        {
            onComplete?.Invoke(false);
            yield break;
        }

        using (UnityWebRequest request = UnityWebRequest.Get(ServerBaseUrl + "/api/voices"))
        {
            yield return request.SendWebRequest();

            bool available = request.result == UnityWebRequest.Result.Success &&
                request.downloadHandler != null &&
                request.downloadHandler.text.IndexOf(voiceFileName, StringComparison.OrdinalIgnoreCase) >= 0;

            onComplete?.Invoke(available);
        }
    }

    public IEnumerator ReloadConfig(Action<bool, string> onComplete)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(ServerBaseUrl + "/api/reload_config"))
        {
            yield return request.SendWebRequest();

            bool success = request.result == UnityWebRequest.Result.Success;
            string message = success
                ? (request.downloadHandler != null ? request.downloadHandler.text : "AllTalk config reload completed.")
                : request.error;

            onComplete?.Invoke(success, message);
        }
    }

    private string ResolveUrl(string pathOrUrl)
    {
        if (Uri.TryCreate(pathOrUrl, UriKind.Absolute, out Uri absoluteUri) &&
            (absoluteUri.Scheme == Uri.UriSchemeHttp || absoluteUri.Scheme == Uri.UriSchemeHttps))
        {
            return absoluteUri.AbsoluteUri;
        }

        return ServerBaseUrl + "/" + pathOrUrl.TrimStart('/');
    }
}
