using System;
using System.Threading;
using CSCore;
using CSCore.DSP;
using CSCore.SoundIn;
using CSCore.Streams;
using CSCore.Utils;

namespace NoraBar.Services
{
    public class AudioVisualizerService : IDisposable
    {
        private WasapiLoopbackCapture? _capture;
        private SoundInSource? _soundInSource;
        private IWaveSource? _realtimeSource;
        private SingleBlockNotificationStream? _singleBlockNotificationStream;

        private Timer? _timer;
        private readonly int _barCount = 8;

        private readonly Complex[] _complexBuffer = new Complex[4096];
        private int _bufferIndex = 0;
        private readonly object _lock = new object();
        private bool _newDataAvailable = false;

        private readonly object _disposeLock = new object();
        private bool _isStopping = false;

        public event EventHandler<float[]>? SpectrumDataUpdated;

        public bool Start()
        {
            try
            {
                _capture = new WasapiLoopbackCapture();
                _capture.Initialize();

                _soundInSource = new SoundInSource(_capture);
                var sampleSource = _soundInSource.ToSampleSource();

                _singleBlockNotificationStream = new SingleBlockNotificationStream(sampleSource);
                _singleBlockNotificationStream.SingleBlockRead += (s, a) =>
                {
                    lock (_lock)
                    {
                        _complexBuffer[_bufferIndex].Real = (a.Left + a.Right) / 2f;
                        _complexBuffer[_bufferIndex].Imaginary = 0f;
                        _bufferIndex++;

                        if (_bufferIndex >= _complexBuffer.Length)
                        {
                            _bufferIndex = 0;
                        }
                        _newDataAvailable = true;
                    }
                };

                _realtimeSource = _singleBlockNotificationStream.ToWaveSource();

                // Read data continuously
                byte[] buffer = new byte[_realtimeSource.WaveFormat.BytesPerSecond / 2];
                _isStopping = false;
                _soundInSource.DataAvailable += (s, e) =>
                {
                    lock (_disposeLock)
                    {
                        if (_isStopping || _realtimeSource == null) return;
                        int read;
                        while (_realtimeSource != null && (read = _realtimeSource.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            // Pump data
                        }
                    }
                };

                _capture.Start();

                _timer = new Timer(UpdateSpectrum, null, 0, 50); // 20 FPS
                return true;
            }
            catch (Exception)
            {
                Stop();
                return false;
            }
        }

        private void UpdateSpectrum(object? state)
        {
            bool hasNewData;
            Complex[] fftData = new Complex[4096];

            lock (_lock)
            {
                hasNewData = _newDataAvailable;
                _newDataAvailable = false;

                if (hasNewData)
                {
                    // Copy circularly
                    Array.Copy(_complexBuffer, _bufferIndex, fftData, 0, 4096 - _bufferIndex);
                    Array.Copy(_complexBuffer, 0, fftData, 4096 - _bufferIndex, _bufferIndex);
                }
            }

            if (!hasNewData) return;

            // Apply Hamming window
            for (int i = 0; i < fftData.Length; i++)
            {
                fftData[i].Real *= (float)(0.54 - 0.46 * Math.Cos(2 * Math.PI * i / (fftData.Length - 1)));
            }

            // Execute FFT
            FastFourierTransformation.Fft(fftData, 12, FftMode.Forward);

            float[] spectrumData = new float[_barCount];

            // Use only the lower frequencies (first quarter of the FFT data)
            int maxBands = fftData.Length / 4;
            int bandSize = maxBands / _barCount;

            for (int i = 0; i < _barCount; i++)
            {
                float max = 0;
                for (int j = 0; j < bandSize; j++)
                {
                    int index = i * bandSize + j;
                    if (index < fftData.Length)
                    {
                        float val = (float)fftData[index].Value;
                        if (val > max) max = val;
                    }
                }

                // Convert magnitude to decibels for a more natural response
                if (max < 1e-5f) max = 1e-5f;
                float db = (float)(10 * Math.Log10(max));

                // Map -40dB .. 0dB to 0.0 .. 1.0
                float scaled = (db + 40.0f) / 40.0f;

                // Emphasize the visualization slightly
                scaled *= 1.2f;

                if (scaled > 1.0f) scaled = 1.0f;
                if (scaled < 0.0f) scaled = 0.0f;

                spectrumData[i] = scaled;
            }

            SpectrumDataUpdated?.Invoke(this, spectrumData);
        }

        public void Stop()
        {
            _isStopping = true;

            lock (_disposeLock)
            {
                DisposeResource(ref _timer);

                WasapiLoopbackCapture? capture = _capture;
                _capture = null;
                if (capture != null)
                {
                    try
                    {
                        capture.Stop();
                    }
                    catch (Exception)
                    {
                    }
                    finally
                    {
                        DisposeResource(capture);
                    }
                }

                DisposeResource(ref _soundInSource);
                DisposeResource(ref _realtimeSource);
                DisposeResource(ref _singleBlockNotificationStream);
            }

            lock (_lock)
            {
                Array.Clear(_complexBuffer);
                _bufferIndex = 0;
                _newDataAvailable = false;
            }
        }

        private static void DisposeResource<T>(ref T? resource) where T : class, IDisposable
        {
            T? resourceToDispose = resource;
            resource = null;
            DisposeResource(resourceToDispose);
        }

        private static void DisposeResource(IDisposable? resource)
        {
            try
            {
                resource?.Dispose();
            }
            catch (Exception)
            {
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
