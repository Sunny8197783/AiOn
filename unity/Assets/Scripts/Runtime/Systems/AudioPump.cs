// Plays the AudioGraph (AudioClip.Create PCM + one AudioSource, PORTING.md "Save, settings, audio"): a looping
// 2-second stereo clip is a ring buffer that Update fills a little ahead of the play head, on the main thread.
// The play head comes from AudioSettings.dspTime (the clip starts with PlayScheduled, at the output rate), so a
// stalled frame is detected and the graph clock skips the lost time like a real audio thread would. The ring is
// kept silent ahead of what was written, so a long stall plays silence instead of repeating old sound.
using System;
using UnityEngine;

namespace Gyeongbo.Systems
{
    internal sealed class AudioPump : MonoBehaviour
    {
        const float AHEAD = 0.12f;        // seconds rendered ahead of the play head (more on slow frames)
        ACtx ctx;
        AudioClip clip;
        AudioSource src;
        int len, sr;
        float[] buf = new float[0];
        double dspStart;
        long written, zeroed;
        bool suspended;

        public static AudioPump Create(ACtx ctx)
        {
            var go = new GameObject("gyeongbo-audio") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            var p = go.AddComponent<AudioPump>();
            p.ctx = ctx; p.sr = ctx.sampleRate; p.len = ctx.sampleRate * 2;
            p.clip = AudioClip.Create("gyeongbo-audio", p.len, 2, p.sr, false);
            p.src = go.AddComponent<AudioSource>();
            p.src.clip = p.clip; p.src.loop = true; p.src.playOnAwake = false; p.src.spatialBlend = 0; p.src.priority = 0;
            p.dspStart = AudioSettings.dspTime + 0.1;
            p.src.PlayScheduled(p.dspStart);
            return p;
        }

        long Played() { long v = (long)((AudioSettings.dspTime - dspStart) * sr); return v < 0 ? 0 : v; }

        void Update()
        {
            if (ctx == null) return;
            long played = Played();
            if (suspended || ctx.state != "running")
            {
                written = zeroed = played;   // the graph clock holds; the ring was cleared when it suspended
                return;
            }
            if (written < played) { ctx.Skip(played - written); written = played; }
            long target = played + (long)(Mathf.Max(AHEAD, Time.unscaledDeltaTime * 2.5f) * sr);
            int n = (int)Math.Min(target - written, len / 4);
            if (n > 0)
            {
                if (buf.Length < n * 2) buf = new float[n * 2];
                try { ctx.Render(buf, n); }
                catch (Exception e) { Debug.LogException(e); Array.Clear(buf, 0, n * 2); }
                Write(written, buf, n);
                written += n;
            }
            // Silence ahead of the written part (never reaching back into what is still to be heard).
            long from = Math.Max(zeroed, written), to = written + len - sr / 2;
            if (to > from)
            {
                int z = (int)Math.Min(to - from, len);
                var zeros = new float[z * 2];
                Write(from, zeros, z);
                zeroed = from + z;
            }
        }

        // Writes n frames (interleaved stereo) at absolute frame `at`, wrapping around the ring.
        void Write(long at, float[] data, int n)
        {
            int pos = (int)(at % len), first = Math.Min(n, len - pos);
            if (first == n && data.Length == n * 2) { clip.SetData(data, pos); return; }
            var a = new float[first * 2]; Array.Copy(data, 0, a, 0, first * 2); clip.SetData(a, pos);
            if (n > first) { var b = new float[(n - first) * 2]; Array.Copy(data, first * 2, b, 0, (n - first) * 2); clip.SetData(b, 0); }
        }

        // JS visibilitychange: suspend the context while the app is hidden, resume when it is back.
        public void Suspend(bool on)
        {
            if (on == suspended) return;
            suspended = on;
            if (ctx != null) ctx.state = on ? "suspended" : "running";
            if (on && clip != null) clip.SetData(new float[len * 2], 0);
            written = zeroed = Played();
        }

        void OnApplicationPause(bool paused) => Suspend(paused);
        void OnApplicationFocus(bool focus) { if (!Application.runInBackground) Suspend(!focus); }

        void OnDestroy()
        {
            if (src != null) src.Stop();
            if (clip != null) Destroy(clip);
        }
    }
}
