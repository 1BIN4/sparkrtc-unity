using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Unity.WebRTC
{
    /// <summary>
    ///     Delegate to be called when new audio data is received.
    /// </summary>
    /// <remarks>
    ///     `AudioReadEventHandler` is a delegate to be called when new audio data is received.
    /// </remarks>
    /// <param name="data">Float array containing audio data samples.</param>
    /// <param name="channels">Number of audio channels.</param>
    /// <param name="sampleRate">Sample rate of the audio data</param>
    /// <example>
    ///     <code lang="cs"><![CDATA[
    ///         audioStreamTrack.onReceived += (data, channels, sampleRate) =>
    ///         {
    ///         }
    ///     ]]></code>
    /// </example>
    /// <seealso cref="AudioStreamTrack.OnReceived"/>
    public delegate void AudioReadEventHandler(float[] data, int channels, int sampleRate);

    /// <summary>
    /// 
    /// </summary>
    public static class AudioSourceExtension
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="source"></param>
        /// <param name="track"></param>
        public static void SetTrack(this AudioSource source, AudioStreamTrack track)
        {
            track._streamRenderer.Source = source;
        }
    }

    /// <summary>
    ///     Represents a single audio track within a stream.
    /// </summary>
    /// <remarks>
    ///     `AudioStreamTrack` is a `MediaStreamTrack` that represents a single audio track within a stream.
    /// </remarks>
    /// <example>
    ///     <code lang="cs"><![CDATA[
    ///         AudioStreamTrack audioStreamTrack = new AudioStreamTrack(texture);
    ///     ]]></code>
    /// </example>
    /// <seealso cref="MediaStreamTrack" />
    /// <seealso cref="WebRTC" />
    public class AudioStreamTrack : MediaStreamTrack
    {
        /// <summary>
        ///     AudioSource object.
        /// </summary>
        public AudioSource Source
        {
            get
            {
                if (_source != null)
                    return _source;
                return _streamRenderer?.Source;
            }
        }

        private AudioSource _source;

        /// <summary>
        /// This flag only works on sender side track.
        /// If True, Send audio input to remote and Play audio in local.
        /// If False, only send to remote. Not play audio in local.
        /// </summary>
        public bool Loopback
        {
            get
            {
                if (_audioCapturer != null)
                {
                    return _audioCapturer.loopback;
                }

                return false;
            }
            set
            {
                if (_audioCapturer != null)
                {
                    _audioCapturer.loopback = value;
                }
            }
        }

        internal class AudioStreamRenderer : IDisposable
        {
            private bool disposed;

            internal IntPtr self;

            private AudioSource _audioSource;
            private AudioCustomFilter _filter;
            private AudioStreamTrack _track;

            /// <summary>
            /// 
            /// </summary>
            public AudioSource Source
            {
                get
                {
                    return _audioSource;
                }
                set
                {
                    _audioSource = value;
                    AddFilter(_audioSource);
                }
            }

            private static T GetOrAddComponent<T>(GameObject go) where T : Component
            {
                T comp = go.GetComponent<T>();
                if (!comp)
                    comp = go.AddComponent<T>();
                return comp;
            }

            private void AddFilter(AudioSource source)
            {
                if (_filter != null)
                    return;
                _filter = GetOrAddComponent<AudioCustomFilter>(source.gameObject);
                _filter.hideFlags = HideFlags.HideInInspector;
                _filter.onAudioRead += SetData;
                _filter.sender = false;
                source.Play();
            }

            public AudioStreamRenderer(AudioStreamTrack track)
                : this(WebRTC.Context.CreateAudioTrackSink())
            {
                _track = track;
                _track?.AddSink(this);
            }

            public AudioStreamRenderer(IntPtr ptr)
            {
                self = ptr;
                WebRTC.Table.Add(self, this);
            }

            ~AudioStreamRenderer()
            {
                this.Dispose();
            }

            public void Dispose()
            {
                if (this.disposed)
                {
                    return;
                }

                if (_filter != null)
                {
                    _filter.onAudioRead -= SetData;
                    WebRTC.DestroyOnMainThread(_filter);
                }
                if (self != IntPtr.Zero && !WebRTC.Context.IsNull)
                {
                    if (_track != null && WebRTC.Table.ContainsKey(_track.self))
                        _track.RemoveSink(this);
                    WebRTC.Table.Remove(self);
                    WebRTC.Context.DeleteAudioTrackSink(self);
                    self = IntPtr.Zero;
                }
                this.disposed = true;
                GC.SuppressFinalize(this);
            }

            /// <summary>
            /// </summary>
            /// <note>
            /// This method is called on worker thread, not main thread.
            /// So almost Unity APIs are not able to use.
            /// </note>
            /// <param name="data"></param>
            internal void SetData(float[] data, int channels, int sampleRate)
            {
                NativeMethods.AudioTrackSinkProcessAudio(self, data, data.Length, channels, sampleRate);

                onReceived?.Invoke(data, channels, sampleRate);
            }
            internal event AudioReadEventHandler onReceived;
        }

        readonly AudioCustomFilter _audioCapturer;
        internal AudioStreamRenderer _streamRenderer;
        internal AudioTrackSource _trackSource;

        /// <summary>
        ///     Creates a new AudioStreamTrack object.
        /// </summary>
        /// <remarks>
        ///     `AudioStreamTrack` constructor creates an instance of `AudioStreamTrack`.
        /// </remarks>
        /// <example>
        ///     <code><![CDATA[
        ///         AudioStreamTrack audioStreamTrack = new AudioStreamTrack();
        ///     ]]></code>
        /// </example>
        public AudioStreamTrack()
            : this(Guid.NewGuid().ToString(), new AudioTrackSource())
        {
        }

        /// <summary>
        ///     Creates a new AudioStreamTrack object.
        /// </summary>
        /// <remarks>
        ///     `AudioStreamTrack` constructor creates an instance of `AudioStreamTrack` with a `source`.
        /// </remarks>
        /// <param name="source">`AudioSource` object.</param>
        /// <example>
        ///     <code><![CDATA[
        ///         AudioStreamTrack audioStreamTrack = new AudioStreamTrack(source);
        ///     ]]></code>
        /// </example>
        public AudioStreamTrack(AudioSource source)
            : this(Guid.NewGuid().ToString(), new AudioTrackSource())
        {
            if (source == null)
                throw new ArgumentNullException("source", "AudioSource argument is null.");
            _source = source;

            _audioCapturer = source.gameObject.AddComponent<AudioCustomFilter>();
            _audioCapturer.hideFlags = HideFlags.HideInInspector;
            _audioCapturer.onAudioRead += SetData;
            _audioCapturer.sender = true;
        }

        /// <summary>
        ///     Creates a new AudioStreamTrack object.
        /// </summary>
        /// <remarks>
        ///     `AudioStreamTrack` constructor creates an instance of `AudioStreamTrack` with a `listener`.
        /// </remarks>
        /// <param name="listener">`AudioListener` object.</param>
        /// <example>
        ///     <code><![CDATA[
        ///         AudioStreamTrack audioStreamTrack = new AudioStreamTrack(listener);
        ///     ]]></code>
        /// </example>
        public AudioStreamTrack(AudioListener listener)
            : this(Guid.NewGuid().ToString(), new AudioTrackSource())
        {
            if (listener == null)
                throw new ArgumentNullException("listener", "AudioListener argument is null.");

            _audioCapturer = listener.gameObject.AddComponent<AudioCustomFilter>();
            _audioCapturer.hideFlags = HideFlags.HideInInspector;
            _audioCapturer.onAudioRead += SetData;
            _audioCapturer.sender = true;
        }

        internal AudioStreamTrack(string label, AudioTrackSource source)
            : base(WebRTC.Context.CreateAudioTrack(label, source.self))
        {
            _trackSource = source;
        }

        internal AudioStreamTrack(IntPtr ptr) : base(ptr)
        {
            _streamRenderer = new AudioStreamRenderer(this);
        }

        internal void AddSink(AudioStreamRenderer renderer)
        {
            NativeMethods.AudioTrackAddSink(
                GetSelfOrThrow(), renderer.self);
        }
        internal void RemoveSink(AudioStreamRenderer renderer)
        {
            NativeMethods.AudioTrackRemoveSink(
                GetSelfOrThrow(), renderer.self);
        }

        /// <summary>
        ///     Disposes of AudioStreamTrack.
        /// </summary>
        /// <remarks>
        ///     `Dispose` method disposes of the `AudioStreamTrack` and releases the associated resources. 
        /// </remarks>
        /// <example>
        ///     <code lang="cs"><![CDATA[
        ///         audioStreamTrack.Dispose();
        ///     ]]></code>
        /// </example>
        public override void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            if (self != IntPtr.Zero && !WebRTC.Context.IsNull)
            {
                if (_audioCapturer != null)
                {
                    // Unity API must be called from main thread.
                    _audioCapturer.onAudioRead -= SetData;
                    WebRTC.DestroyOnMainThread(_audioCapturer);
                }
                if (_streamRenderer != null)
                {
                    _streamRenderer?.Dispose();
                    _streamRenderer = null;
                }
                _trackSource?.Dispose();
                _trackSource = null;
            }
            base.Dispose();
        }

        /// <summary>
        ///     Provides the audio data to the track.
        /// </summary>
        /// <remarks>
        ///     `SetData` method provides the audio data to the track.
        /// </remarks>
        /// <param name="nativeArray">`NativeArray<float>` containing audio data samples.</param>
        /// <param name="channels">Number of audio channels.</param>
        /// <param name="sampleRate">Sample rate of the audio data</param>
        /// <example>
        ///     <code lang="cs"><![CDATA[
        ///         audioStreamTrack.SetData(nativeArray, channels, sampleRate);
        ///     ]]></code>
        /// </example>
        public void SetData(NativeArray<float>.ReadOnly nativeArray, int channels, int sampleRate)
        {
            unsafe
            {
                void* ptr = nativeArray.GetUnsafeReadOnlyPtr();
                ProcessAudio(_trackSource, (IntPtr)ptr, sampleRate, channels, nativeArray.Length);
            }
        }

        /// <summary>
        ///     Provides the audio data to the track.
        /// </summary>
        /// <remarks>
        ///     `SetData` method provides the audio data to the track.
        /// </remarks>
        /// <param name="nativeSlice">`NativeSlice<float>` containing audio data samples.</param>
        /// <param name="channels">Number of audio channels.</param>
        /// <param name="sampleRate">Sample rate of the audio data</param>
        /// <example>
        ///     <code lang="cs"><![CDATA[
        ///         audioStreamTrack.SetData(nativeSlice, channels, sampleRate);
        ///     ]]></code>
        /// </example>
        public void SetData(NativeSlice<float> nativeSlice, int channels, int sampleRate)
        {
            unsafe
            {
                void* ptr = nativeSlice.GetUnsafeReadOnlyPtr();
                ProcessAudio(_trackSource, (IntPtr)ptr, sampleRate, channels, nativeSlice.Length);
            }
        }

        static void ProcessAudio(AudioTrackSource source, IntPtr array, int sampleRate, int channels, int frames)
        {
            if (sampleRate == 0 || channels == 0 || frames == 0)
                throw new ArgumentException($"arguments are invalid values " +
                    $"sampleRate={sampleRate}, " +
                    $"channels={channels}, " +
                    $"frames={frames}");
            source.Update(array, sampleRate, channels, frames);
        }

        /// <summary>
        ///     Provides the audio data to the track.
        /// </summary>
        /// <remarks>
        ///     `SetData` method provides the audio data to the track.
        /// </remarks>
        /// <param name="array">Float array containing audio data samples.</param>
        /// <param name="channels">Number of audio channels.</param>
        /// <param name="sampleRate">Sample rate of the audio data</param>
        /// <example>
        ///     <code lang="cs"><![CDATA[
        ///         audioStreamTrack.SetData(array, channels, sampleRate);
        ///     ]]></code>
        /// </example>
        public void SetData(float[] array, int channels, int sampleRate)
        {
            if (array == null)
                throw new ArgumentNullException("array is null");

            unsafe
            {
                fixed (float* ptr = array)
                {
                    ProcessAudio(_trackSource, (IntPtr)ptr, sampleRate, channels, array.Length);
                }
            }
        }

        // ReadOnlySpan<T> is supported since .NET Standard 2.1.
#if UNITY_2021_2_OR_NEWER
        /// <summary>
        ///     Provides the audio data to the track.
        /// </summary>
        /// <remarks>
        ///     `SetData` method provides the audio data to the track.
        /// </remarks>
        /// <param name="span">`ReadOnlySpan<float>` containing audio data samples.</param>
        /// <param name="channels">Number of audio channels.</param>
        /// <param name="sampleRate">Sample rate of the audio data</param>
        /// <example>
        ///     <code lang="cs"><![CDATA[
        ///         audioStreamTrack.SetData(span, channels, sampleRate);
        ///     ]]></code>
        /// </example>
        public void SetData(ReadOnlySpan<float> span, int channels, int sampleRate)
        {
            unsafe
            {
                fixed (float* ptr = span)
                {
                    ProcessAudio(_trackSource, (IntPtr)ptr, sampleRate, channels, span.Length);
                }
            }
        }
#endif

        /// <summary>
        ///     Event to be fired when received new audio data.
        /// </summary>
        /// <remarks>
        ///     `onReceived` event is fired when received new audio data.
        /// </remarks>
        /// <example>
        ///     <code lang="cs"><![CDATA[
        ///         audioStreamTrack.onReceived += (data, channels, sampleRate) =>
        ///         {
        ///         }
        ///     ]]></code>
        /// </example>
        /// <seealso cref="AudioReadEventHandler"/>
        public event AudioReadEventHandler onReceived
        {
            add
            {
                if (_streamRenderer == null)
                    throw new InvalidOperationException("AudioStreamTrack is not receiver side.");
                _streamRenderer.onReceived += value;
            }
            remove
            {
                if (_streamRenderer == null)
                    throw new InvalidOperationException("AudioStreamTrack is not receiver side.");
                _streamRenderer.onReceived -= value;
            }
        }
    }

    internal class AudioTrackSource : RefCountedObject
    {
        public AudioTrackSource() : base(WebRTC.Context.CreateAudioTrackSource())
        {
            WebRTC.Table.Add(self, this);
        }

        ~AudioTrackSource()
        {
            this.Dispose();
        }

        public void Update(IntPtr array, int sampleRate, int channels, int frames)
        {
            NativeMethods.AudioSourceProcessLocalAudio(GetSelfOrThrow(), array, sampleRate, channels, frames);
        }

        public override void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            if (self != IntPtr.Zero && !WebRTC.Context.IsNull)
            {
                WebRTC.Table.Remove(self);
            }
            base.Dispose();
        }
    }
}
