/*
* Copyright (c) 2016 Samsung Electronics Co., Ltd All Rights Reserved
*
* Licensed under the Apache License, Version 2.0 (the License);
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
*
* http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an AS IS BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*/

using System;
using System.Runtime.InteropServices;

namespace Tizen.Multimedia
{
    /// <summary>
    /// Provides the ability to directly manage the system audio input devices.
    /// </summary>
    /// <remarks>The recorder privilege(http://tizen.org/privilege/recorder) is required.</remarks>
    public abstract class AudioCaptureBase : IDisposable
    {
        /// <summary>
        /// Specifies the minimum value allowed for the audio capture.
        /// </summary>
        public static readonly int MinSampleRate = 8000;

        /// <summary>
        /// Specifies the maximum value allowed for the audio capture.
        /// </summary>
        public static readonly int MaxSampleRate = 48000;

        internal IntPtr _handle = IntPtr.Zero;

        private AudioIOState _state = AudioIOState.Idle;

        internal AudioCaptureBase(int sampleRate, AudioChannel channel, AudioSampleType sampleType)
        {
            if (sampleRate < MinSampleRate || MaxSampleRate < sampleRate)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleRate), sampleRate,
                    $"Valid sampleRate range is { MinSampleRate } <= x <= { MaxSampleRate }.");
            }

            ValidationUtil.ValidateEnum(typeof(AudioChannel), channel, nameof(channel));
            ValidationUtil.ValidateEnum(typeof(AudioSampleType), sampleType, nameof(sampleType));

            SampleRate = sampleRate;
            Channel = channel;
            SampleType = sampleType;

            AudioIOUtil.ThrowIfError(
                Interop.AudioIO.AudioInput.Create(SampleRate, (int)Channel, (int)SampleType, out _handle));

            RegisterStateChangedCallback();
        }

        ~AudioCaptureBase()
        {
            Dispose(false);
        }

        /// <summary>
        /// Occurs when the state of the AudioCapture is changed.
        /// </summary>
        public event EventHandler<AudioIOStateChangedEventArgs> StateChanged;

        private Interop.AudioIO.AudioStateChangedCallback _stateChangedCallback;

        private void RegisterStateChangedCallback()
        {
            _stateChangedCallback = (IntPtr handle, int previous, int current, bool byPolicy, IntPtr _) =>
            {
                _state = (AudioIOState)current;

                StateChanged?.Invoke(this,
                    new AudioIOStateChangedEventArgs((AudioIOState)previous, _state, byPolicy));
            };

            AudioIOUtil.ThrowIfError(
                Interop.AudioIO.AudioInput.SetStateChangedCallback(_handle, _stateChangedCallback, IntPtr.Zero));
        }

        #region Dispose support
        private bool _isDisposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed)
            {
                return;
            }

            if (_handle != IntPtr.Zero)
            {
                if (_state != AudioIOState.Idle)
                {
                    try
                    {
                        Unprepare();
                    }
                    catch (Exception)
                    {
                    }
                }

                Interop.AudioIO.AudioInput.Destroy(_handle);
                _handle = IntPtr.Zero;
                _isDisposed = true;
            }
        }

        internal void ValidateNotDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }
        #endregion

        internal void ValidateState(params AudioIOState[] desiredStates)
        {
            ValidateNotDisposed();

            AudioIOUtil.ValidateState(_state, desiredStates);
        }

        /// <summary>
        /// Gets the sample rate of the audio input data stream.
        /// </summary>
        public int SampleRate { get; }

        /// <summary>
        /// Gets the channel type of the audio input data stream.
        /// </summary>
        public AudioChannel Channel { get; }

        /// <summary>
        /// Gets the sample type of the audio input data stream.
        /// </summary>
        public AudioSampleType SampleType { get; }

        /// <summary>
        /// Gets the size allocated for the audio input buffer.
        /// </summary>
        /// <exception cref="ObjectDisposedException">The AudioPlayback has already been disposed.</exception>
        public int GetBufferSize()
        {
            AudioIOUtil.ThrowIfError(Interop.AudioIO.AudioInput.GetBufferSize(_handle, out var size));
            return size;
        }

        /// <summary>
        /// Prepares the AudioCapture for reading audio data by starting buffering of audio data from the device.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        ///     Operation failed due to internal error.\n
        ///     -or-\n
        ///     The current state is not <see cref="AudioIOState.Idle"/>.
        /// </exception>
        /// <seealso cref="Unprepare"/>
        public void Prepare()
        {
            ValidateState(AudioIOState.Idle);

            AudioIOUtil.ThrowIfError(Interop.AudioIO.AudioInput.Prepare(_handle),
                "Failed to prepare the AudioCapture");
        }

        /// <summary>
        /// Unprepares the AudioCapture.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        ///     Operation failed due to internal error.\n
        ///     \n
        ///     The current state is <see cref="AudioIOState.Idle"/>.
        /// </exception>
        /// <seealso cref="Prepare"/>
        public void Unprepare()
        {
            ValidateState(AudioIOState.Running, AudioIOState.Paused);

            AudioIOUtil.ThrowIfError(Interop.AudioIO.AudioInput.Unprepare(_handle),
                "Failed to unprepare the AudioCapture");
        }

        /// <summary>
        /// Pauses buffering of audio data from the device.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        ///     The current state is <see cref="AudioState.Idle"/>.\n
        ///     -or-\n
        ///     The method is called in the <see cref="AsyncAudioCapture.DataAvailable"/> event handler.
        /// </exception>
        /// <seealso cref="Resume"/>
        public void Pause()
        {
            if (_state == AudioIOState.Paused)
            {
                return;
            }
            ValidateState(AudioIOState.Running);

            AudioIOUtil.ThrowIfError(Interop.AudioIO.AudioInput.Pause(_handle));
        }
        /// <summary>
        /// Resumes buffering audio data from the device.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        ///     The current state is <see cref="AudioState.Idle"/>.\n
        ///     -or-\n
        ///     The method is called in the <see cref="AsyncAudioCapture.DataAvailable"/> event handler.
        /// </exception>
        /// <seealso cref="Pause"/>
        public void Resume()
        {
            if (_state == AudioIOState.Running)
            {
                return;
            }
            ValidateState(AudioIOState.Paused);

            AudioIOUtil.ThrowIfError(Interop.AudioIO.AudioInput.Resume(_handle));
        }
        /// <summary>
        /// Flushes and discards buffered audio data from the input stream.
        /// </summary>
        /// <exception cref="InvalidOperationException">The current state is <see cref="AudioState.Idle"/>.</exception>
        public void Flush()
        {
            ValidateState(AudioIOState.Running, AudioIOState.Paused);

            int ret = Interop.AudioIO.AudioInput.Flush(_handle);

            MultimediaDebug.AssertNoError(ret);
        }

        /// <summary>
        /// Sets the sound stream information to the audio input.
        /// </summary>
        /// <param name="streamPolicy">The <see cref="AudioStreamPolicy"/> to apply for the AudioCapture.</param>
        /// <exception cref="ArgumentNullException"><paramref name="streamPolicy"/> is null.</exception>
        /// <exception cref="ObjectDisposedException"><paramref name="streamPolicy"/> has already been disposed.</exception>
        /// <exception cref="NotSupportedException"><paramref name="streamPolicy"/> is not supported.</exception>
        /// <exception cref="ArgumentException">Not able to retrieve information from <paramref name="streamPolicy"/>.</exception>
        public void ApplyStreamPolicy(AudioStreamPolicy streamPolicy)
        {
            if (streamPolicy == null)
            {
                throw new ArgumentNullException(nameof(streamPolicy));
            }

            if (streamPolicy.Handle == IntPtr.Zero)
            {
                throw new ObjectDisposedException(nameof(streamPolicy));
            }

            ValidateNotDisposed();

            AudioIOUtil.ThrowIfError(Interop.AudioIO.AudioInput.SetStreamInfo(_handle, streamPolicy.Handle));
        }
    }

    /// <summary>
    /// Provides the ability to record audio from system audio input devices in synchronous way.
    /// </summary>
    /// <privilege>http://tizen.org/privilege/recorder</privilege>
    public class AudioCapture : AudioCaptureBase
    {
        /// <summary>
        /// Initializes a new instance of the AudioCapture class with the specified sample rate, channel and sampleType.
        /// </summary>
        /// <param name="sampleRate">The audio sample rate.(8000 ~ 48000Hz)</param>
        /// <param name="channel">The audio channel type.</param>
        /// <param name="sampleType">The audio sample type.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     <paramref name="sampleRate"/> is less than <see cref="MinSampleRate"/>.\n
        ///     -or-\n
        ///     <paramref name="sampleRate"/> is greater than <see cref="MaxSampleRate"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///     <paramref name="channel"/> is invalid.\n
        ///     -or-\n
        ///     <paramref name="sampleType"/> is invalid.
        /// </exception>
        /// <exception cref="InvalidOperationException">The required privilege is not specified.</exception>
        /// <exception cref="NotSupportedException">The system does not support microphone.</exception>
        public AudioCapture(int sampleRate, AudioChannel channel, AudioSampleType sampleType)
            : base(sampleRate, channel, sampleType)
        {
        }

        /// <summary>
        /// Reads audio data from the audio input buffer.
        /// </summary>
        /// <param name="count">The number of bytes to be read.</param>
        /// <returns>The buffer of audio data captured.</returns>
        /// <exception cref="InvalidOperationException">The current state is not <see cref="AudioIOState.Running"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is equal to or less than zero.</exception>
        public byte[] Read(int count)
        {
            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count,
                    $"{ nameof(count) } can't be equal to or less than zero.");
            }
            ValidateState(AudioIOState.Running);

            byte[] buffer = new byte[count];

            AudioIOUtil.ThrowIfError(Interop.AudioIO.AudioInput.Read(_handle, buffer, count),
                "Failed to read");

            return buffer;
        }
    }

    /// <summary>
    /// Provides the ability to record audio from system audio input devices in asynchronous way.
    /// </summary>
    /// <privilege>http://tizen.org/privilege/recorder</privilege>
    public class AsyncAudioCapture : AudioCaptureBase
    {

        /// <summary>
        /// Occurs when audio data is available.
        /// </summary>
        public event EventHandler<AudioDataAvailableEventArgs> DataAvailable;

        /// <summary>
        /// Initializes a new instance of the AsyncAudioCapture class with the specified sample rate, channel and sampleType.
        /// </summary>
        /// <param name="sampleRate">The audio sample rate.(8000 ~ 48000Hz)</param>
        /// <param name="channel">The audio channel type.</param>
        /// <param name="sampleType">The audio sample type.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     <paramref name="sampleRate"/> is less than <see cref="MinSampleRate"/>.\n
        ///     -or-\n
        ///     <paramref name="sampleRate"/> is greater than <see cref="MaxSampleRate"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///     <paramref name="channel"/> is invalid.\n
        ///     -or-\n
        ///     <paramref name="sampleType"/> is invalid.
        /// </exception>
        /// <exception cref="InvalidOperationException">The required privilege is not specified.</exception>
        /// <exception cref="NotSupportedException">The system does not support microphone.</exception>
        public AsyncAudioCapture(int sampleRate, AudioChannel channel, AudioSampleType sampleType)
            : base(sampleRate, channel, sampleType)
        {
            _streamCallback = (IntPtr handle, uint length, IntPtr _) => { OnInputDataAvailable(handle, length); };

            AudioIOUtil.ThrowIfError(
                Interop.AudioIO.AudioInput.SetStreamCallback(_handle, _streamCallback, IntPtr.Zero),
                $"Failed to initialize a { nameof(AsyncAudioCapture) }");
        }

        private Interop.AudioIO.AudioStreamCallback _streamCallback;

        private void OnInputDataAvailable(IntPtr handle, uint length)
        {
            if (length == 0U)
            {
                return;
            }

            IntPtr ptr = IntPtr.Zero;
            try
            {
                AudioIOUtil.ThrowIfError(Interop.AudioIO.AudioInput.Peek(_handle, out ptr, ref length));

                byte[] buffer = new byte[length];
                Marshal.Copy(ptr, buffer, 0, (int)length);

                Interop.AudioIO.AudioInput.Drop(_handle);

                DataAvailable?.Invoke(this, new AudioDataAvailableEventArgs(buffer));
            }
            catch (Exception e)
            {
                Log.Error(nameof(AsyncAudioCapture), e.Message);
            }
        }
    }
}