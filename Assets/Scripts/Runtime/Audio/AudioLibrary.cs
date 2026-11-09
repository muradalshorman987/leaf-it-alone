using System;
using System.Collections.Generic;
using UnityEngine;

namespace FallenYard.Audio
{
    /// <summary>
    /// Optional overrides: drop recorded or CC0 clips here to replace any procedural cue.
    /// Cues without an override use <see cref="GeneratedClips"/>. Create one with
    /// Assets > Create > Fallen Yard > Audio Library and assign it to the AudioManager.
    /// Log the source and licence of every clip you add in docs/AUDIO.md.
    /// </summary>
    [CreateAssetMenu(menuName = "Fallen Yard/Audio Library", fileName = "AudioLibrary")]
    public class AudioLibrary : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public SoundCue cue;
            [Tooltip("One or more takes; a random one plays each time.")]
            public AudioClip[] clips = Array.Empty<AudioClip>();
            [Tooltip("Extra gain for this cue (1 = as recorded).")]
            [Range(0f, 2f)] public float gain = 1f;
        }

        public List<Entry> entries = new List<Entry>();

        public Entry Find(SoundCue cue)
        {
            if (entries == null) return null;
            foreach (var e in entries)
                if (e != null && e.cue == cue && e.clips != null && e.clips.Length > 0)
                    return e;
            return null;
        }

        /// <summary>Override clip for a cue, or null if none is set.</summary>
        public AudioClip GetOverride(SoundCue cue, int variant)
        {
            var e = Find(cue);
            if (e == null) return null;
            int i = ((variant % e.clips.Length) + e.clips.Length) % e.clips.Length;
            return e.clips[i];
        }

        public float GainFor(SoundCue cue)
        {
            var e = Find(cue);
            return e != null ? e.gain : 1f;
        }

        /// <summary>Override from <paramref name="library"/> if any, else the generated clip.</summary>
        public static AudioClip Resolve(AudioLibrary library, SoundCue cue, int variant)
        {
            var clip = library != null ? library.GetOverride(cue, variant) : null;
            return clip != null ? clip : GeneratedClips.Get(cue, variant);
        }

        /// <summary>How many takes a cue has: the override count, or the procedural variant count.</summary>
        public static int Variants(AudioLibrary library, SoundCue cue)
        {
            var e = library != null ? library.Find(cue) : null;
            return e != null ? e.clips.Length : ProceduralSounds.VariantCount(cue);
        }
    }

    /// <summary>
    /// Builds AudioClips from <see cref="ProceduralSounds"/> on first use and caches them for
    /// the session. Clips are created with HideFlags.DontSave so they never end up in scenes.
    /// </summary>
    public static class GeneratedClips
    {
        static readonly Dictionary<int, AudioClip> Cache = new Dictionary<int, AudioClip>();

        static int Key(SoundCue cue, int variant) => (int)cue * 64 + variant;

        public static bool IsCached(SoundCue cue, int variant = 0)
        {
            variant = Wrap(cue, variant);
            return Cache.TryGetValue(Key(cue, variant), out var c) && c != null;
        }

        public static AudioClip Get(SoundCue cue, int variant = 0)
        {
            variant = Wrap(cue, variant);
            int key = Key(cue, variant);
            if (Cache.TryGetValue(key, out var clip) && clip != null) return clip;
            clip = ToClip(ProceduralSounds.Generate(cue, variant), $"FY_{cue}_{variant}");
            Cache[key] = clip;
            return clip;
        }

        /// <summary>Caches a buffer that was rendered elsewhere (e.g. on a worker thread). Main thread only.</summary>
        public static AudioClip Store(SoundCue cue, int variant, SoundBuffer buffer)
        {
            variant = Wrap(cue, variant);
            int key = Key(cue, variant);
            if (Cache.TryGetValue(key, out var existing) && existing != null) return existing;
            var clip = ToClip(buffer, $"FY_{cue}_{variant}");
            Cache[key] = clip;
            return clip;
        }

        public static AudioClip ToClip(SoundBuffer buffer, string name)
        {
            var clip = AudioClip.Create(name, buffer.samples.Length, 1, buffer.sampleRate, false);
            clip.SetData(buffer.samples, 0);
            clip.hideFlags = HideFlags.DontSave;
            return clip;
        }

        /// <summary>Destroys every cached clip (tests, or to free memory between scenes).</summary>
        public static void Clear()
        {
            foreach (var c in Cache.Values)
            {
                if (c == null) continue;
                if (Application.isPlaying) UnityEngine.Object.Destroy(c);
                else UnityEngine.Object.DestroyImmediate(c);
            }
            Cache.Clear();
        }

        static int Wrap(SoundCue cue, int variant)
        {
            int n = ProceduralSounds.VariantCount(cue);
            return ((variant % n) + n) % n;
        }
    }
}
