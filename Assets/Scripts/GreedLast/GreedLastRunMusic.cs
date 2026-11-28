using System;
using UnityEngine;

namespace GreedLast
{
    internal sealed class GreedLastRunMusic : IDisposable
    {
        internal const float ReferenceBeatSeconds = 0.5f;
        internal const string ResourcePath = "GreedLast/RunAudio/";
        private const float MixGain = 0.43f;
        private const float SeekToleranceSeconds = 0.085f;
        private readonly AudioSource source;
        private readonly GreedLastRunMusicClock clock = new GreedLastRunMusicClock();
        private bool playing;

        internal GreedLastRunMusic(Transform host, AudioClip clip)
        {
            var player = new GameObject("Greed run music");
            player.transform.SetParent(host, false);
            source = player.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.clip = clip;
            source.spatialBlend = 0f;
        }

        internal bool Update(float distance, float beatSeconds, bool live, float volume, out int beat)
        {
            bool pulse = clock.Advance(distance, beatSeconds, live, out beat);
            if (!live || volume <= 0f || AudioListener.volume <= 0f)
            {
                Stop();
                return pulse;
            }

            AudioClip clip = source.clip;
            int targetSample = (int)(clock.ReferenceSeconds * clip.frequency % clip.samples);
            source.pitch = ReferenceBeatSeconds / beatSeconds;
            source.volume = volume * MixGain;
            if (!playing)
            {
                source.timeSamples = targetSample;
                source.Play();
                playing = true;
            }
            else
            {
                int difference = Mathf.Abs(targetSample - source.timeSamples);
                if (Mathf.Min(difference, clip.samples - difference) > SeekToleranceSeconds * clip.frequency)
                    source.timeSamples = targetSample;
            }
            return pulse;
        }

        internal void Stop()
        {
            if (!playing) return;
            source.Stop();
            playing = false;
        }

        internal void Reset()
        {
            Stop();
            clock.Reset();
        }

        public void Dispose()
        {
            Stop();
            if (Application.isPlaying) UnityEngine.Object.Destroy(source.gameObject);
            else UnityEngine.Object.DestroyImmediate(source.gameObject);
        }
    }

    // Distance accumulates only as the run advances. Pattern-local elapsed time starts over at each trap.
    internal sealed class GreedLastRunMusicClock
    {
        private float previousDistance;
        private double beats;
        private int lastBeat = -1;

        internal double ReferenceSeconds => beats * GreedLastRunMusic.ReferenceBeatSeconds;

        internal bool Advance(float distance, float beatSeconds, bool live, out int beat)
        {
            if (distance < previousDistance) Reset();
            if (live)
                beats += ((double)distance - previousDistance) / GreedLastRunCore.RunSpeedMetersPerSecond / beatSeconds;
            previousDistance = distance;
            beat = (int)Math.Floor(beats + 0.00001);
            if (!live || beat == lastBeat) return false;
            lastBeat = beat;
            return true;
        }

        internal void Reset()
        {
            previousDistance = 0f;
            beats = 0d;
            lastBeat = -1;
        }
    }
}
