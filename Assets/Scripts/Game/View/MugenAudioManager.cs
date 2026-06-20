using System;
using System.Collections.Generic;
using System.IO;
using Lockstep.Import.Snd;
using Lockstep.Math;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.Char;
using UnityEngine;

namespace Lockstep.View
{
    public sealed class MugenAudioManager : MonoBehaviour
    {
        sealed class SoundBank
        {
            public byte[] SndBytes;
            public SndFile Snd;
            public readonly Dictionary<long, AudioClip> Clips = new Dictionary<long, AudioClip>();
            public readonly HashSet<long> Missing = new HashSet<long>();
        }

        public float MasterVolume = 1f;
        public int PoolSize = 16;

        readonly Dictionary<MCharData, SoundBank> _banks = new Dictionary<MCharData, SoundBank>();
        readonly Dictionary<int, AudioSource> _channels = new Dictionary<int, AudioSource>();
        readonly List<AudioSource> _pool = new List<AudioSource>();
        int _poolCursor;

        public static MugenAudioManager Ensure()
        {
            MugenAudioManager existing = FindObjectOfType<MugenAudioManager>();
            if (existing != null)
            {
                return existing;
            }

            GameObject obj = new GameObject("MugenAudioManager");
            return obj.AddComponent<MugenAudioManager>();
        }

        void Awake()
        {
            EnsurePool();
        }

        public void Register(MCharData data, string sndPath)
        {
            if (data == null || _banks.ContainsKey(data) || string.IsNullOrEmpty(sndPath) || !File.Exists(sndPath))
            {
                return;
            }

            try
            {
                byte[] bytes = File.ReadAllBytes(sndPath);
                _banks[data] = new SoundBank
                {
                    SndBytes = bytes,
                    Snd = SndReader.Read(bytes),
                };
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MUGEN] failed to load sound bank: " + sndPath + " " + ex.Message);
            }
        }

        public void PlayFrameEvents(MBattleEngine engine)
        {
            if (engine == null || engine.World == null || engine.World.Events == null)
            {
                return;
            }

            List<MSoundEvent> sounds = engine.World.Events.Sounds;
            for (int i = 0; i < sounds.Count; i++)
            {
                HandleSound(engine, sounds[i]);
            }
        }

        void HandleSound(MBattleEngine engine, MSoundEvent sound)
        {
            int channelKey = ChannelKey(sound.OwnerId, sound.Channel);
            switch (sound.Type)
            {
                case MSoundEventType.Stop:
                    if (sound.Channel < 0)
                    {
                        StopOwnerChannels(sound.OwnerId);
                    }
                    else if (_channels.TryGetValue(channelKey, out AudioSource stopSource))
                    {
                        stopSource.Stop();
                    }
                    break;
                case MSoundEventType.SetPan:
                    if (_channels.TryGetValue(channelKey, out AudioSource panSource))
                    {
                        panSource.panStereo = NormalizePan(sound.Pan);
                    }
                    break;
                case MSoundEventType.Play:
                    Play(engine, sound, channelKey);
                    break;
            }
        }

        void Play(MBattleEngine engine, MSoundEvent sound, int channelKey)
        {
            SoundBank bank = BankFor(engine, sound);
            if (bank == null)
            {
                return;
            }

            AudioClip clip = ClipFor(bank, sound.Group, sound.Number);
            if (clip == null)
            {
                return;
            }

            AudioSource source = sound.Channel >= 0 ? ChannelSource(channelKey) : PoolSource();
            source.loop = sound.LoopCount != 0;
            source.pitch = Mathf.Clamp(sound.Frequency.ToFloat(), 0.25f, 4f);
            source.panStereo = NormalizePan(sound.Pan);
            source.volume = Mathf.Clamp01(sound.VolumeScale / 100f) * MasterVolume;
            source.priority = Mathf.Clamp(128 - sound.Priority, 0, 256);

            if (source.loop)
            {
                source.clip = clip;
                source.Play();
            }
            else
            {
                source.PlayOneShot(clip, source.volume);
            }
        }

        SoundBank BankFor(MBattleEngine engine, MSoundEvent sound)
        {
            if (sound.CommonBank)
            {
                return null;
            }

            MChar owner = FindOwner(engine, sound.OwnerId);
            MCharData data = owner != null ? owner.OwnData : null;
            return data != null && _banks.TryGetValue(data, out SoundBank bank) ? bank : null;
        }

        static MChar FindOwner(MBattleEngine engine, int ownerId)
        {
            for (int i = 0; i < engine.Chars.Count; i++)
            {
                if (engine.Chars[i].Id == ownerId) { return engine.Chars[i]; }
            }
            for (int i = 0; i < engine.Helpers.Count; i++)
            {
                if (engine.Helpers[i].Id == ownerId) { return engine.Helpers[i]; }
            }
            return null;
        }

        AudioClip ClipFor(SoundBank bank, int group, int number)
        {
            long key = SndFileKey(group, number);
            if (bank.Clips.TryGetValue(key, out AudioClip cached))
            {
                return cached;
            }
            if (bank.Missing.Contains(key) || !bank.Snd.TryGet(group, number, out SndEntry entry))
            {
                bank.Missing.Add(key);
                return null;
            }

            try
            {
                AudioClip clip = DecodeWav(SndReader.CopyWave(bank.SndBytes, entry), "snd_" + group + "_" + number);
                if (clip != null)
                {
                    bank.Clips[key] = clip;
                }
                else
                {
                    bank.Missing.Add(key);
                }
                return clip;
            }
            catch (Exception ex)
            {
                bank.Missing.Add(key);
                Debug.LogWarning("[MUGEN] failed to decode sound " + group + "," + number + ": " + ex.Message);
                return null;
            }
        }

        static AudioClip DecodeWav(byte[] wav, string name)
        {
            if (wav == null || wav.Length < 44 || Ascii(wav, 0, 4) != "RIFF" || Ascii(wav, 8, 4) != "WAVE")
            {
                return null;
            }

            int offset = 12;
            int format = 0;
            int channels = 0;
            int sampleRate = 0;
            int bits = 0;
            int dataOffset = -1;
            int dataSize = 0;
            while (offset + 8 <= wav.Length)
            {
                string id = Ascii(wav, offset, 4);
                int size = I32(wav, offset + 4);
                int chunkData = offset + 8;
                if (size < 0 || chunkData + size > wav.Length)
                {
                    return null;
                }

                if (id == "fmt " && size >= 16)
                {
                    format = U16(wav, chunkData);
                    channels = U16(wav, chunkData + 2);
                    sampleRate = I32(wav, chunkData + 4);
                    bits = U16(wav, chunkData + 14);
                }
                else if (id == "data")
                {
                    dataOffset = chunkData;
                    dataSize = size;
                }

                offset = chunkData + size + (size & 1);
            }

            if (dataOffset < 0 || channels <= 0 || sampleRate <= 0 || bits <= 0)
            {
                return null;
            }

            int bytesPerSample = bits / 8;
            if (bytesPerSample <= 0)
            {
                return null;
            }
            int sampleCount = dataSize / bytesPerSample;
            int frames = sampleCount / channels;
            float[] samples = new float[frames * channels];
            for (int i = 0; i < samples.Length; i++)
            {
                int p = dataOffset + i * bytesPerSample;
                samples[i] = Sample(wav, p, bits, format);
            }

            AudioClip clip = AudioClip.Create(name, frames, channels, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        static float Sample(byte[] data, int offset, int bits, int format)
        {
            if (format == 3 && bits == 32)
            {
                return BitConverter.ToSingle(data, offset);
            }
            switch (bits)
            {
                case 8:
                    return (data[offset] - 128) / 128f;
                case 16:
                    return (short)(data[offset] | data[offset + 1] << 8) / 32768f;
                case 24:
                {
                    int v = data[offset] | data[offset + 1] << 8 | data[offset + 2] << 16;
                    if ((v & 0x800000) != 0) { v |= unchecked((int)0xFF000000); }
                    return v / 8388608f;
                }
                case 32:
                    return I32(data, offset) / 2147483648f;
                default:
                    return 0f;
            }
        }

        AudioSource PoolSource()
        {
            EnsurePool();
            AudioSource source = _pool[_poolCursor];
            _poolCursor = (_poolCursor + 1) % _pool.Count;
            source.loop = false;
            source.clip = null;
            return source;
        }

        AudioSource ChannelSource(int channelKey)
        {
            if (_channels.TryGetValue(channelKey, out AudioSource source))
            {
                return source;
            }
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            _channels[channelKey] = source;
            return source;
        }

        void EnsurePool()
        {
            int wanted = Mathf.Max(1, PoolSize);
            while (_pool.Count < wanted)
            {
                AudioSource source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                _pool.Add(source);
            }
        }

        void StopOwnerChannels(int ownerId)
        {
            foreach (KeyValuePair<int, AudioSource> pair in _channels)
            {
                if (OwnerFromChannelKey(pair.Key) == ownerId)
                {
                    pair.Value.Stop();
                }
            }
        }

        static float NormalizePan(FFloat value)
        {
            float pan = value.ToFloat();
            if (Mathf.Abs(pan) > 1f)
            {
                pan /= 127f;
            }
            return Mathf.Clamp(pan, -1f, 1f);
        }

        static int ChannelKey(int ownerId, int channel)
        {
            return ownerId << 16 ^ (channel & 0xFFFF);
        }

        static int OwnerFromChannelKey(int key)
        {
            return key >> 16;
        }

        static long SndFileKey(int group, int number)
        {
            return ((long)group << 32) ^ (uint)number;
        }

        static string Ascii(byte[] data, int offset, int length)
        {
            return System.Text.Encoding.ASCII.GetString(data, offset, length);
        }

        static ushort U16(byte[] data, int offset)
        {
            return (ushort)(data[offset] | data[offset + 1] << 8);
        }

        static int I32(byte[] data, int offset)
        {
            return data[offset] | data[offset + 1] << 8 | data[offset + 2] << 16 | data[offset + 3] << 24;
        }
    }
}
