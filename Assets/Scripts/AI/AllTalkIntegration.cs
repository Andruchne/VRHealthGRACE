using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public class AllTalkIntegration : MonoBehaviour
{
    [Header("Settings")]
    public string serverBaseUrl = "http://127.0.0.1:7851"; // Default AllTalk port

    private AudioClip recordedAudio;
    private string microphoneID;

    void Start()
    {
        // Initialize the microphone
        if (Microphone.devices.Length > 0)
        {
            microphoneID = Microphone.devices[0];
            Debug.Log("Microphone found: " + microphoneID);
        }
        else
        {
            Debug.LogError("No microphone found!");
        }
    }

    // Start recording audio from the microphone
    public void StartRecording()
    {
        if (string.IsNullOrEmpty(microphoneID)) return;

        // Record up to 10 seconds at 22050 Hz
        recordedAudio = Microphone.Start(microphoneID, false, 10, 22050);
        Debug.Log("Recording started...");
    }

    // Stop recording and upload it directly as a WAV byte array to AllTalk
    public void StopAndUploadRecording(string voiceName)
    {
        if (string.IsNullOrEmpty(microphoneID)) return;

        int recordedSamples = Microphone.GetPosition(microphoneID);
        Microphone.End(microphoneID);
        Debug.Log("Recording stopped. Converting and uploading...");

        if (recordedSamples <= 0)
        {
            Debug.LogWarning("No audio recorded.");
            return;
        }

        // Trim the AudioClip to the exact recorded length using a helper method or SaveWav
        AudioClip trimmedClip = TrimAudioClip(recordedAudio, recordedSamples);

        // Convert AudioClip to WAV bytes using the modified SaveWav script
        byte[] wavBytes = SaveWav.GetWavBytes(trimmedClip);

        // Upload the WAV bytes to AllTalk
        StartCoroutine(UploadVoiceRoutine(wavBytes, voiceName + ".wav"));
    }

    // Helper method to trim the AudioClip to the actual recorded duration
    private AudioClip TrimAudioClip(AudioClip originalClip, int lengthSamples)
    {
        float[] data = new float[lengthSamples * originalClip.channels];
        originalClip.GetData(data, 0);

        AudioClip trimmed = AudioClip.Create(originalClip.name, lengthSamples, originalClip.channels, originalClip.frequency, false);
        trimmed.SetData(data, 0);
        return trimmed;
    }

    IEnumerator UploadVoiceRoutine(byte[] wavData, string fileName)
    {
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", wavData, fileName, "audio/wav");

        using (UnityWebRequest www = UnityWebRequest.Post($"{serverBaseUrl}/api/refresh-voices", form))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Error updating voices: " + www.error);
            }
            else
            {
                Debug.Log("Voice successfully uploaded and server settings refreshed!");
            }
        }
    }
}