using System;
using System.Speech.Synthesis;

namespace OmniPdf.App.Core
{
    public class SpeechReaderService : IDisposable
    {
        private SpeechSynthesizer? _synth;
        private bool _isPaused = false;

        public event Action<string>? StateChanged;

        public SpeechReaderService()
        {
            try
            {
                _synth = new SpeechSynthesizer();
                _synth.StateChanged += (s, e) =>
                {
                    StateChanged?.Invoke(e.State.ToString());
                };
            }
            catch
            {
                _synth = null;
            }
        }

        public bool IsAvailable => _synth != null;
        public bool IsSpeaking => _synth?.State == SynthesizerState.Speaking;
        public bool IsPaused => _isPaused;

        public void SpeakText(string text, int rate = 0, int volume = 100)
        {
            if (_synth == null || string.IsNullOrWhiteSpace(text)) return;

            try
            {
                Stop();
                _synth.Rate = Math.Clamp(rate, -10, 10);
                _synth.Volume = Math.Clamp(volume, 0, 100);
                _isPaused = false;
                _synth.SpeakAsync(text);
                StateChanged?.Invoke("Speaking");
            }
            catch { }
        }

        public void Pause()
        {
            if (_synth != null && _synth.State == SynthesizerState.Speaking && !_isPaused)
            {
                _synth.Pause();
                _isPaused = true;
                StateChanged?.Invoke("Paused");
            }
        }

        public void Resume()
        {
            if (_synth != null && _isPaused)
            {
                _synth.Resume();
                _isPaused = false;
                StateChanged?.Invoke("Speaking");
            }
        }

        public void Stop()
        {
            if (_synth != null)
            {
                try
                {
                    _synth.SpeakAsyncCancelAll();
                    _isPaused = false;
                    StateChanged?.Invoke("Ready");
                }
                catch { }
            }
        }

        public void Dispose()
        {
            Stop();
            _synth?.Dispose();
            _synth = null;
        }
    }
}
