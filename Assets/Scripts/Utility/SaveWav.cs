using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Encodes Unity AudioClips as 16-bit PCM WAV data.
/// </summary>
public static class SaveWav
{
    private const int HeaderSize = 44;

    public static byte[] GetWavBytes(AudioClip clip)
    {
        if (clip == null)
        {
            throw new ArgumentNullException(nameof(clip));
        }

        using (MemoryStream stream = new MemoryStream())
        {
            for (int i = 0; i < HeaderSize; i++)
            {
                stream.WriteByte(0);
            }

            WritePcmData(stream, clip);
            long endPosition = stream.Position;
            long dataLength = endPosition - HeaderSize;

            stream.Position = 0;
            WriteHeader(stream, clip, dataLength);
            stream.Position = endPosition;
            return stream.ToArray();
        }
    }

    public static bool Save(string filename, AudioClip clip)
    {
        if (string.IsNullOrWhiteSpace(filename))
        {
            throw new ArgumentException("A filename is required.", nameof(filename));
        }

        if (!filename.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
        {
            filename += ".wav";
        }

        string filePath = Path.Combine(Application.persistentDataPath, filename);
        string directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(filePath, GetWavBytes(clip));
        Debug.Log("WAV saved to: " + filePath);
        return true;
    }

    public static AudioClip TrimSilence(AudioClip clip, float threshold)
    {
        if (clip == null)
        {
            throw new ArgumentNullException(nameof(clip));
        }

        float[] samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);
        return TrimSilence(new List<float>(samples), threshold, clip.channels, clip.frequency);
    }

    public static AudioClip TrimSilence(List<float> samples, float threshold, int channels, int sampleRate)
    {
        return TrimSilence(samples, threshold, channels, sampleRate, false, false);
    }

    public static AudioClip TrimSilence(
        List<float> samples,
        float threshold,
        int channels,
        int sampleRate,
        bool is3D,
        bool stream)
    {
        if (samples == null || samples.Count == 0)
        {
            throw new ArgumentException("Audio samples cannot be empty.", nameof(samples));
        }

        channels = Mathf.Max(1, channels);
        int frameCount = samples.Count / channels;
        if (frameCount <= 0)
        {
            throw new ArgumentException("The sample count is smaller than the channel count.", nameof(samples));
        }

        int firstFrame = 0;
        int lastFrame = frameCount - 1;
        bool containsAudio = false;

        for (int frame = 0; frame < frameCount; frame++)
        {
            bool frameHasAudio = false;
            for (int channel = 0; channel < channels; channel++)
            {
                if (Mathf.Abs(samples[frame * channels + channel]) > threshold)
                {
                    frameHasAudio = true;
                    containsAudio = true;
                    break;
                }
            }

            if (frameHasAudio)
            {
                firstFrame = frame;
                break;
            }
        }

        if (containsAudio)
        {
            for (int frame = frameCount - 1; frame >= firstFrame; frame--)
            {
                bool frameHasAudio = false;
                for (int channel = 0; channel < channels; channel++)
                {
                    if (Mathf.Abs(samples[frame * channels + channel]) > threshold)
                    {
                        frameHasAudio = true;
                        break;
                    }
                }

                if (frameHasAudio)
                {
                    lastFrame = frame;
                    break;
                }
            }
        }
        else
        {
            firstFrame = 0;
            lastFrame = frameCount - 1;
        }

        int firstSample = firstFrame * channels;
        int sampleLength = (lastFrame - firstFrame + 1) * channels;
        float[] trimmedSamples = samples.GetRange(firstSample, sampleLength).ToArray();
        int trimmedFrameCount = Mathf.Max(1, trimmedSamples.Length / channels);
        AudioClip trimmedClip = AudioClip.Create("TrimmedAudio", trimmedFrameCount, channels, sampleRate, is3D, stream);
        trimmedClip.SetData(trimmedSamples, 0);
        return trimmedClip;
    }

    private static void WritePcmData(Stream stream, AudioClip clip)
    {
        float[] samples = new float[clip.samples * clip.channels];
        if (!clip.GetData(samples, 0))
        {
            throw new InvalidOperationException("Unity could not read audio samples from the AudioClip.");
        }

        for (int i = 0; i < samples.Length; i++)
        {
            int pcmValue = Mathf.Clamp(Mathf.RoundToInt(samples[i] * 32767f), short.MinValue, short.MaxValue);
            short pcmSample = (short)pcmValue;
            stream.WriteByte((byte)(pcmSample & 0xff));
            stream.WriteByte((byte)((pcmSample >> 8) & 0xff));
        }
    }

    private static void WriteHeader(Stream stream, AudioClip clip, long dataLength)
    {
        WriteFourCc(stream, "RIFF");
        WriteInt32LE(stream, checked((int)(36 + dataLength)));
        WriteFourCc(stream, "WAVE");
        WriteFourCc(stream, "fmt ");
        WriteInt32LE(stream, 16);
        WriteInt16LE(stream, 1);
        WriteInt16LE(stream, (short)clip.channels);
        WriteInt32LE(stream, clip.frequency);
        WriteInt32LE(stream, checked(clip.frequency * clip.channels * 2));
        WriteInt16LE(stream, (short)(clip.channels * 2));
        WriteInt16LE(stream, 16);
        WriteFourCc(stream, "data");
        WriteInt32LE(stream, checked((int)dataLength));
    }

    private static void WriteFourCc(Stream stream, string value)
    {
        byte[] bytes = System.Text.Encoding.ASCII.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static void WriteInt16LE(Stream stream, short value)
    {
        stream.WriteByte((byte)(value & 0xff));
        stream.WriteByte((byte)((value >> 8) & 0xff));
    }

    private static void WriteInt32LE(Stream stream, int value)
    {
        stream.WriteByte((byte)(value & 0xff));
        stream.WriteByte((byte)((value >> 8) & 0xff));
        stream.WriteByte((byte)((value >> 16) & 0xff));
        stream.WriteByte((byte)((value >> 24) & 0xff));
    }
}
