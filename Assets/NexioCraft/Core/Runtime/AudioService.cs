using System;
using System.Collections.Generic;
using UnityEngine;

namespace NexioCraft.Core
{
    // New effects go at the end: each clip's noise is seeded from its enum value.
    public enum Sfx
    {
        Click, Pop, DiceRoll, Step, Unlock, Capture, TokenHome, Six, YourTurn, NoMove, Win, Lose, PieceMove, PieceCapture, Check, Promote,
        Gunshot, BoltCycle, RobotHit, RobotDown, Explosion, ScopeIn, Alarm, Coin
    }

    /// <summary>Plays short synthesized effects through a small pool of voices. No audio files needed.</summary>
    public sealed class AudioService : MonoBehaviour
    {
        const int VoiceCount = 8;

        readonly Dictionary<Sfx, AudioClip> clips = new Dictionary<Sfx, AudioClip>();
        AudioSource[] voices;
        int nextVoice;

        void Awake()
        {
            voices = new AudioSource[VoiceCount];
            for (int i = 0; i < VoiceCount; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                voices[i] = source;
            }
            foreach (Sfx sfx in Enum.GetValues(typeof(Sfx))) clips[sfx] = SynthClips.Create(sfx);
        }

        public void Play(Sfx sfx, float volume = 1f, float pitch = 1f)
        {
            if (!Settings.Sound) return;
            var voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % VoiceCount;
            voice.Stop();
            voice.clip = clips[sfx];
            voice.volume = volume;
            voice.pitch = pitch;
            voice.Play();
        }
    }

    /// <summary>Tiny additive synthesizer that renders each effect into an AudioClip at startup.</summary>
    static class SynthClips
    {
        const int Rate = 44100;

        enum Wave { Sine, Triangle, Square }

        public static AudioClip Create(Sfx sfx)
        {
            var rng = new System.Random(977 + (int)sfx);
            float[] buffer;
            switch (sfx)
            {
                case Sfx.Click:
                    buffer = New(0.08f);
                    Tone(buffer, 0f, 0.06f, 1500f, 900f, 0.35f, Wave.Sine, 0.002f, 55f);
                    Noise(buffer, rng, 0f, 0.012f, 0.1f, 250f, 0.4f);
                    break;
                case Sfx.Pop:
                    buffer = New(0.12f);
                    Tone(buffer, 0f, 0.1f, 700f, 1400f, 0.4f, Wave.Sine, 0.003f, 30f);
                    break;
                case Sfx.DiceRoll:
                {
                    buffer = New(0.62f);
                    float t = 0f;
                    for (int i = 0; i < 9; i++)
                    {
                        float amp = 0.55f * (1f - i / 11f);
                        Noise(buffer, rng, t, 0.03f, amp, 140f, 0.55f);
                        Tone(buffer, t, 0.03f, 1800f + (float)rng.NextDouble() * 900f, 1500f, amp * 0.35f, Wave.Triangle, 0.001f, 120f);
                        t += 0.035f + (float)rng.NextDouble() * 0.035f;
                    }
                    break;
                }
                case Sfx.Step:
                    buffer = New(0.1f);
                    Tone(buffer, 0f, 0.08f, 520f, 760f, 0.4f, Wave.Sine, 0.002f, 38f);
                    Tone(buffer, 0f, 0.05f, 1040f, 1520f, 0.12f, Wave.Triangle, 0.002f, 60f);
                    break;
                case Sfx.Unlock:
                    buffer = New(0.32f);
                    Tone(buffer, 0f, 0.22f, 320f, 980f, 0.35f, Wave.Sine, 0.01f, 9f);
                    Tone(buffer, 0.16f, 0.14f, 1318.5f, 1318.5f, 0.25f, Wave.Sine, 0.002f, 30f);
                    break;
                case Sfx.Capture:
                    buffer = New(0.5f);
                    Tone(buffer, 0f, 0.4f, 820f, 110f, 0.2f, Wave.Square, 0.004f, 6f);
                    Noise(buffer, rng, 0f, 0.25f, 0.18f, 14f, 0.25f);
                    Tone(buffer, 0f, 0.24f, 150f, 55f, 0.6f, Wave.Sine, 0.003f, 14f);
                    break;
                case Sfx.TokenHome:
                {
                    buffer = New(0.7f);
                    float[] notes = { 1046.5f, 1318.5f, 1568f, 2093f };
                    for (int i = 0; i < notes.Length; i++)
                    {
                        Tone(buffer, i * 0.07f, 0.34f, notes[i], notes[i], 0.22f, Wave.Sine, 0.003f, 9f);
                        Tone(buffer, i * 0.07f, 0.2f, notes[i] * 2f, notes[i] * 2f, 0.05f, Wave.Triangle, 0.003f, 14f);
                    }
                    break;
                }
                case Sfx.Six:
                    buffer = New(0.42f);
                    Tone(buffer, 0f, 0.12f, 1760f, 2640f, 0.18f, Wave.Sine, 0.002f, 20f);
                    Tone(buffer, 0.06f, 0.34f, 2637f, 2637f, 0.2f, Wave.Sine, 0.002f, 10f);
                    break;
                case Sfx.YourTurn:
                    buffer = New(0.38f);
                    Tone(buffer, 0f, 0.18f, 880f, 880f, 0.28f, Wave.Sine, 0.004f, 14f);
                    Tone(buffer, 0.1f, 0.26f, 1318.5f, 1318.5f, 0.28f, Wave.Sine, 0.004f, 11f);
                    break;
                case Sfx.NoMove:
                    buffer = New(0.36f);
                    Tone(buffer, 0f, 0.14f, 247f, 233f, 0.4f, Wave.Triangle, 0.004f, 16f);
                    Tone(buffer, 0.13f, 0.22f, 185f, 175f, 0.4f, Wave.Triangle, 0.004f, 12f);
                    break;
                case Sfx.Win:
                {
                    buffer = New(1.5f);
                    float[] notes = { 784f, 1046.5f, 1318.5f, 1568f };
                    for (int i = 0; i < notes.Length; i++)
                    {
                        bool last = i == notes.Length - 1;
                        Tone(buffer, i * 0.12f, last ? 1.1f : 0.3f, notes[i], notes[i], 0.25f, Wave.Triangle, 0.004f, last ? 2.8f : 8f);
                    }
                    Tone(buffer, 0.36f, 1.1f, 523.25f, 523.25f, 0.12f, Wave.Sine, 0.03f, 3f);
                    Tone(buffer, 0.36f, 1.1f, 659.25f, 659.25f, 0.1f, Wave.Sine, 0.03f, 3f);
                    break;
                }
                case Sfx.Lose:
                {
                    buffer = New(0.95f);
                    float[] notes = { 523.25f, 466.2f, 415.3f, 349.2f };
                    for (int i = 0; i < notes.Length; i++)
                        Tone(buffer, i * 0.16f, i == 3 ? 0.5f : 0.2f, notes[i], notes[i], 0.3f, Wave.Triangle, 0.004f, i == 3 ? 4f : 10f);
                    break;
                }
                case Sfx.PieceMove:
                    // Wooden "tock" of a piece set down.
                    buffer = New(0.14f);
                    Noise(buffer, rng, 0f, 0.02f, 0.5f, 180f, 0.7f);
                    Tone(buffer, 0f, 0.1f, 240f, 170f, 0.55f, Wave.Sine, 0.001f, 45f);
                    Tone(buffer, 0f, 0.04f, 1250f, 1100f, 0.08f, Wave.Triangle, 0.001f, 90f);
                    break;
                case Sfx.PieceCapture:
                    buffer = New(0.22f);
                    Noise(buffer, rng, 0f, 0.03f, 0.6f, 120f, 0.6f);
                    Tone(buffer, 0f, 0.12f, 300f, 150f, 0.6f, Wave.Sine, 0.001f, 32f);
                    Noise(buffer, rng, 0.06f, 0.025f, 0.45f, 160f, 0.65f);
                    Tone(buffer, 0.06f, 0.1f, 210f, 160f, 0.45f, Wave.Sine, 0.001f, 40f);
                    break;
                case Sfx.Check:
                    buffer = New(0.4f);
                    Tone(buffer, 0f, 0.16f, 740f, 740f, 0.3f, Wave.Triangle, 0.003f, 16f);
                    Tone(buffer, 0.12f, 0.26f, 1109f, 1109f, 0.3f, Wave.Triangle, 0.003f, 12f);
                    break;
                case Sfx.Promote:
                {
                    buffer = New(0.55f);
                    float[] notes = { 784f, 988f, 1175f, 1568f };
                    for (int i = 0; i < notes.Length; i++)
                        Tone(buffer, i * 0.06f, 0.28f, notes[i], notes[i], 0.22f, Wave.Sine, 0.003f, 10f);
                    break;
                }
                case Sfx.Gunshot:
                    // Sharp crack, a heavy muzzle boom, then the echo rolling back off the buildings.
                    buffer = New(1.3f);
                    Noise(buffer, rng, 0f, 0.05f, 1f, 90f, 0.05f);
                    Tone(buffer, 0f, 0.4f, 120f, 40f, 0.9f, Wave.Sine, 0.001f, 8f);
                    Noise(buffer, rng, 0.004f, 0.35f, 0.75f, 10f, 0.82f);
                    Noise(buffer, rng, 0.17f, 1.05f, 0.24f, 4f, 0.9f);
                    Tone(buffer, 0.17f, 0.55f, 72f, 40f, 0.22f, Wave.Sine, 0.03f, 5f);
                    break;
                case Sfx.BoltCycle:
                    // Bolt lifted and pulled back, then pushed home and locked.
                    buffer = New(0.52f);
                    Noise(buffer, rng, 0f, 0.03f, 0.5f, 120f, 0.2f);
                    Tone(buffer, 0f, 0.05f, 2200f, 1800f, 0.2f, Wave.Triangle, 0.001f, 80f);
                    Noise(buffer, rng, 0.09f, 0.09f, 0.3f, 35f, 0.55f);
                    Noise(buffer, rng, 0.3f, 0.03f, 0.6f, 140f, 0.15f);
                    Tone(buffer, 0.3f, 0.06f, 1600f, 1300f, 0.28f, Wave.Triangle, 0.001f, 70f);
                    Tone(buffer, 0.3f, 0.09f, 380f, 300f, 0.3f, Wave.Sine, 0.001f, 40f);
                    break;
                case Sfx.RobotHit:
                    // Metal clang: inharmonic partials over a short noise click.
                    buffer = New(0.6f);
                    Noise(buffer, rng, 0f, 0.02f, 0.7f, 150f, 0.1f);
                    Tone(buffer, 0f, 0.5f, 523f, 515f, 0.3f, Wave.Sine, 0.001f, 9f);
                    Tone(buffer, 0f, 0.4f, 1287f, 1270f, 0.22f, Wave.Sine, 0.001f, 13f);
                    Tone(buffer, 0f, 0.3f, 2391f, 2360f, 0.16f, Wave.Sine, 0.001f, 18f);
                    Tone(buffer, 0f, 0.2f, 3710f, 3650f, 0.1f, Wave.Sine, 0.001f, 26f);
                    break;
                case Sfx.RobotDown:
                {
                    // Power-down whine, crackling sparks and falling parts.
                    buffer = New(1.1f);
                    Tone(buffer, 0f, 0.9f, 620f, 55f, 0.26f, Wave.Square, 0.005f, 3.2f);
                    Tone(buffer, 0f, 0.7f, 310f, 40f, 0.3f, Wave.Sine, 0.005f, 4f);
                    for (int i = 0; i < 7; i++)
                        Noise(buffer, rng, 0.05f + (float)rng.NextDouble() * 0.6f, 0.04f, 0.35f, 90f, 0.05f);
                    Noise(buffer, rng, 0.02f, 0.45f, 0.45f, 11f, 0.7f);
                    break;
                }
                case Sfx.Explosion:
                    buffer = New(1.8f);
                    Noise(buffer, rng, 0f, 0.08f, 0.8f, 40f, 0.3f);
                    Noise(buffer, rng, 0f, 1.75f, 1f, 2.6f, 0.93f);
                    Tone(buffer, 0f, 1.2f, 90f, 30f, 0.8f, Wave.Sine, 0.004f, 3f);
                    break;
                case Sfx.ScopeIn:
                    buffer = New(0.22f);
                    Noise(buffer, rng, 0f, 0.16f, 0.25f, 18f, 0.85f);
                    Tone(buffer, 0.12f, 0.05f, 1900f, 1700f, 0.15f, Wave.Triangle, 0.001f, 90f);
                    break;
                case Sfx.Alarm:
                    buffer = New(0.9f);
                    for (int i = 0; i < 3; i++)
                    {
                        float hz = i % 2 == 0 ? 932f : 698f;
                        Tone(buffer, i * 0.28f, 0.26f, hz, hz, 0.22f, Wave.Triangle, 0.01f, 3f);
                    }
                    break;
                case Sfx.Coin:
                    buffer = New(0.36f);
                    Tone(buffer, 0f, 0.08f, 1976f, 1976f, 0.22f, Wave.Square, 0.001f, 30f);
                    Tone(buffer, 0.07f, 0.28f, 2637f, 2637f, 0.22f, Wave.Square, 0.001f, 12f);
                    break;
                default:
                    buffer = New(0.05f);
                    break;
            }

            LimitPeak(buffer, 0.9f);
            var clip = AudioClip.Create(sfx.ToString(), buffer.Length, 1, Rate, false);
            clip.SetData(buffer, 0);
            return clip;
        }

        static float[] New(float seconds) => new float[Mathf.CeilToInt(seconds * Rate)];

        static void Tone(float[] buffer, float start, float duration, float fromHz, float toHz, float amplitude, Wave wave, float attack, float decay)
        {
            int offset = (int)(start * Rate);
            int count = (int)(duration * Rate);
            double phase = 0.0;
            for (int i = 0; i < count; i++)
            {
                int index = offset + i;
                if (index >= buffer.Length) break;
                float time = i / (float)Rate;
                float frequency = fromHz * Mathf.Pow(toHz / fromHz, i / (float)count);
                phase += frequency / Rate;
                phase -= Math.Floor(phase);
                float p = (float)phase;
                float sample = wave switch
                {
                    Wave.Triangle => 4f * Mathf.Abs(p - 0.5f) - 1f,
                    Wave.Square => p < 0.5f ? 0.7f : -0.7f,
                    _ => Mathf.Sin(p * 2f * Mathf.PI)
                };
                float envelope = Mathf.Min(1f, time / Mathf.Max(attack, 0.0001f)) * Mathf.Exp(-decay * time);
                float tail = Mathf.Clamp01((count - i) / (Rate * 0.004f));
                buffer[index] += sample * amplitude * envelope * tail;
            }
        }

        static void Noise(float[] buffer, System.Random rng, float start, float duration, float amplitude, float decay, float smoothing)
        {
            int offset = (int)(start * Rate);
            int count = (int)(duration * Rate);
            float last = 0f;
            for (int i = 0; i < count; i++)
            {
                int index = offset + i;
                if (index >= buffer.Length) break;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                last = Mathf.Lerp(white, last, smoothing);
                float time = i / (float)Rate;
                float tail = Mathf.Clamp01((count - i) / (Rate * 0.003f));
                buffer[index] += last * amplitude * Mathf.Exp(-decay * time) * tail;
            }
        }

        static void LimitPeak(float[] buffer, float target)
        {
            float peak = 0f;
            foreach (float s in buffer) peak = Mathf.Max(peak, Mathf.Abs(s));
            if (peak <= target) return;
            float gain = target / peak;
            for (int i = 0; i < buffer.Length; i++) buffer[i] *= gain;
        }
    }
}
