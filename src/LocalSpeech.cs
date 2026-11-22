using System;
using System.Collections.Generic;
using System.Linq;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using System.Threading;

namespace Jarvis
{
    public sealed class LocalSpeech : IDisposable
    {
        private SpeechRecognitionEngine recognizer;
        private SpeechSynthesizer speaker;
        private bool disposed;
        private volatile bool speaking;
        private Prompt activePrompt;
        private readonly object speakerGate = new object();
        private long diagnosticTick;
        public event Action<string> Heard;
        public event Action<string> Error;
        public event Action<float> Level;
        public event Action<string> Diagnostic;
        public bool IsListening { get; private set; }

        public static string SpanishRecognizerName()
        {
            RecognizerInfo info = SpeechRecognitionEngine.InstalledRecognizers().FirstOrDefault(r => r.Culture.TwoLetterISOLanguageName == "es");
            return info == null ? null : info.Culture.DisplayName;
        }

        public void Start()
        {
            if (disposed) throw new ObjectDisposedException("LocalSpeech");
            if (IsListening) return;
            RecognizerInfo info = SpeechRecognitionEngine.InstalledRecognizers().FirstOrDefault(r => r.Culture.TwoLetterISOLanguageName == "es");
            if (info == null)
                throw new InvalidOperationException("Instala el reconocimiento de voz en español en Configuración de Windows > Hora e idioma > Voz.");
            try
            {
                recognizer = new SpeechRecognitionEngine(info);
                recognizer.LoadGrammar(SpeechGrammar.CreateCommandGrammar(info.Culture));
                recognizer.LoadGrammar(SpeechGrammar.CreateLauncherGrammar(info.Culture));
                recognizer.LoadGrammar(SpeechGrammar.CreateDictationGrammar());
                recognizer.SpeechRecognized += OnRecognized;
                recognizer.SpeechRecognitionRejected += OnRejected;
                recognizer.SpeechHypothesized += OnHypothesized;
                recognizer.SpeechDetected += OnSpeechDetected;
                recognizer.AudioSignalProblemOccurred += OnSignalProblem;
                recognizer.AudioLevelUpdated += OnLevel;
                recognizer.RecognizeCompleted += OnCompleted;
                recognizer.SetInputToDefaultAudioDevice();
                recognizer.EndSilenceTimeout = TimeSpan.FromMilliseconds(650);
                recognizer.EndSilenceTimeoutAmbiguous = TimeSpan.FromMilliseconds(950);
                IsListening = true;
                recognizer.RecognizeAsync(RecognizeMode.Multiple);
                Report("Motor de español listo. Di «Vale, me escuchas».", true);
            }
            catch
            {
                Stop();
                throw;
            }
        }

        private void OnRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            if (!IsListening || speaking || e.Result == null) return;
            string phrase = e.Result.Text ?? "";
            if (phrase.Length > 160) phrase = phrase.Substring(0, 160) + "…";
            float wakeConfidence = e.Result.Words.Count > 0 && String.Equals(e.Result.Words[0].Text, "vale", StringComparison.OrdinalIgnoreCase) ? e.Result.Words[0].Confidence : 0;
            bool accepted = SpeechGrammar.Accepts(e.Result);
            Report("Oí «" + phrase + "» · confianza " + (e.Result.Confidence * 100).ToString("F0") + "% · Vale " + (wakeConfidence * 100).ToString("F0") + "%" + (!accepted ? " · no ejecutado" : ""), true);
            if (!accepted) return;
            Action<string> handler = Heard;
            if (handler != null) handler(e.Result.Text);
        }

        private void OnRejected(object sender, SpeechRecognitionRejectedEventArgs e)
        {
            if (IsListening && !speaking) Report("Detecté voz, pero no entendí la frase. Prueba «Vale, me escuchas».", false);
        }
        private void OnHypothesized(object sender, SpeechHypothesizedEventArgs e)
        {
            if (!IsListening || speaking || e.Result == null) return;
            string phrase = e.Result.Text ?? "";
            if (phrase.Length > 110) phrase = phrase.Substring(0, 110) + "…";
            Report("Reconociendo: «" + phrase + "»", false);
        }
        private void OnSpeechDetected(object sender, SpeechDetectedEventArgs e)
        {
            if (IsListening && !speaking) Report("Voz detectada. Reconociendo…", false);
        }
        private void OnSignalProblem(object sender, AudioSignalProblemOccurredEventArgs e)
        {
            if (!IsListening || speaking) return;
            string message;
            switch (e.AudioSignalProblem)
            {
                case AudioSignalProblem.TooLoud: message = "La señal satura. Aleja un poco el micrófono o baja su nivel."; break;
                case AudioSignalProblem.TooSoft: message = "La voz llega muy baja. Acércate al micrófono."; break;
                case AudioSignalProblem.TooNoisy: message = "Hay demasiado ruido para reconocer la frase."; break;
                case AudioSignalProblem.TooFast: message = "Habla un poco más despacio."; break;
                case AudioSignalProblem.TooSlow: message = "Di Vale y el comando en una sola frase."; break;
                default: message = "Windows detectó un problema de señal: " + e.AudioSignalProblem; break;
            }
            Report(message, false);
        }
        private void Report(string text, bool force)
        {
            long now = DateTime.UtcNow.Ticks;
            if (!force && now - Interlocked.Read(ref diagnosticTick) < TimeSpan.TicksPerSecond * 2) return;
            Interlocked.Exchange(ref diagnosticTick, now);
            Action<string> handler = Diagnostic;
            if (handler != null) handler(text);
        }

        private void OnLevel(object sender, AudioLevelUpdatedEventArgs e)
        {
            Action<float> handler = Level;
            if (handler != null && IsListening) handler(e.AudioLevel / 100f);
        }

        private void OnCompleted(object sender, RecognizeCompletedEventArgs e)
        {
            if (IsListening && !disposed)
            {
                IsListening = false;
                Action<string> handler = Error;
                if (handler != null) handler(e.Error == null ? "El micrófono se detuvo. Actívalo de nuevo para reintentar." : "No pude acceder al micrófono. Revisa los permisos y el dispositivo de entrada de Windows.");
            }
        }

        public void Say(string text)
        {
            if (disposed || String.IsNullOrWhiteSpace(text)) return;
            try
            {
                lock (speakerGate)
                {
                    if (speaker == null)
                    {
                        speaker = new SpeechSynthesizer();
                        InstalledVoice voice = speaker.GetInstalledVoices().FirstOrDefault(v => v.Enabled && v.VoiceInfo.Culture.TwoLetterISOLanguageName == "es");
                        if (voice != null) speaker.SelectVoice(voice.VoiceInfo.Name);
                        speaker.SpeakCompleted += delegate(object sender, SpeakCompletedEventArgs e) { lock (speakerGate) { if (e.Prompt == activePrompt) speaking = false; } };
                    }
                    speaker.SpeakAsyncCancelAll();
                    speaking = true;
                    activePrompt = speaker.SpeakAsync(text);
                }
            }
            catch (InvalidOperationException) { speaking = false; }
            catch (System.Runtime.InteropServices.COMException) { speaking = false; }
        }

        public void Stop()
        {
            IsListening = false;
            if (speaker != null) { speaker.SpeakAsyncCancelAll(); speaking = false; }
            SpeechRecognitionEngine old = recognizer;
            recognizer = null;
            if (old != null)
            {
                old.SpeechRecognized -= OnRecognized;
                old.SpeechRecognitionRejected -= OnRejected;
                old.SpeechHypothesized -= OnHypothesized;
                old.SpeechDetected -= OnSpeechDetected;
                old.AudioSignalProblemOccurred -= OnSignalProblem;
                old.AudioLevelUpdated -= OnLevel;
                old.RecognizeCompleted -= OnCompleted;
                try { old.RecognizeAsyncCancel(); } catch (InvalidOperationException) { }
                old.Dispose();
            }
        }

        public void Dispose()
        {
            disposed = true;
            Stop();
            if (speaker != null) { speaker.Dispose(); speaker = null; }
        }
    }
}
