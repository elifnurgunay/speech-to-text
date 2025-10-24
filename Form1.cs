using NAudio.Wave;
using Newtonsoft.Json; // JSON ayrıştırma için
using System;
using System.Windows.Forms;
using Vosk;

namespace speech_to_text
{
    public partial class Form1 : Form
    {
        // Vosk nesneleri
        private VoskRecognizer voskRecognizer;
        private Model voskModel;

        // NAudio nesneleri (Mikrofon girişi için)
        private WaveInEvent waveIn;

        // Ayarlar
        private const int SAMPLE_RATE = 16000;
        private const string TURKISH_MODEL_PATH = "vosk-model-small-tr-0.3"; // Model klasörünüzün adı

        public Form1()
        {
            InitializeComponent();
            VoskSisteminiHazirla();
        }

        private void VoskSisteminiHazirla()
        {
            try
            {
                // Vosk loglarını sessize al
                Vosk.Vosk.SetLogLevel(-1);

                // Model yolunu uygulama çalışma dizinine göre belirle
                string modelPath = System.IO.Path.Combine(Application.StartupPath, TURKISH_MODEL_PATH);

                // Çalışma dizinini göster (debug için)
                DurumGuncelle($"Çalışma dizini: {Application.StartupPath}");

                if (!System.IO.Directory.Exists(modelPath))
                {
                    DurumGuncelle($"HATA: Model klasörü bulunamadı: {modelPath}");
                    MessageBox.Show($"Türkçe Vosk modeli ({TURKISH_MODEL_PATH}) bulunamadı.\nBeklenen konum: {modelPath}\n\nÇözüm:\n1) Modeli indirin: https://alphacephei.com/vosk/models\n2) Klasörü proje çıkış dizinine (bin\\Debug veya bin\\Release) kopyalayın\n3) Veya proje köküne koyup __Project Properties > Build Events__ içine post-build kopyalama ekleyin.", "Model Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Modeli yükle
                voskModel = new Model(modelPath);
                voskRecognizer = new VoskRecognizer(voskModel, SAMPLE_RATE);

                // Mikrofonu hazırla
                waveIn = new WaveInEvent();
                waveIn.WaveFormat = new WaveFormat(SAMPLE_RATE, 1);
                waveIn.DataAvailable += WaveIn_DataAvailable;
                waveIn.RecordingStopped += WaveIn_RecordingStopped;

                DurumGuncelle("Vosk Konuşma Tanıma Sistemi Hazır. Model Yüklendi.");
                btnBaslat.Enabled = true;
                btnDurdur.Enabled = false;
            }
            catch (Exception ex)
            {
                DurumGuncelle($"Kritik Hata: {ex.Message}");
                MessageBox.Show($"Sistem hazırlanırken hata oluştu:\n{ex.Message}", "Hata",
                                 MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void WaveIn_DataAvailable(object sender, WaveInEventArgs e)
        {
            // Mikrofon verisini Vosk Recognizer'a gönder
            if (voskRecognizer.AcceptWaveform(e.Buffer, e.BytesRecorded))
            {
                // **Tamamlanmış bir cümle tanındı**
                string resultJson = voskRecognizer.Result();
                ProcessVoskResult(resultJson);
                DurumGuncelle("Tanındı: ✅ Dinliyorum...");
            }
            else
            {
                // **Parçalı (anlık) sonuçları almak için:**
                // string partialResultJson = voskRecognizer.PartialResult();
                // ProcessVoskPartialResult(partialResultJson); 
            }
        }

        private void ProcessVoskResult(string resultJson)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<string>(ProcessVoskResult), resultJson);
                return;
            }

            try
            {
                // JSON.NET ile sonucu ayrıştır
                dynamic result = JsonConvert.DeserializeObject(resultJson);
                string taninanMetin = result?.text != null ? result.text.ToString() : "[Boş Sonuç]";

                if (!string.IsNullOrWhiteSpace(taninanMetin))
                {
                    string zamanDamgasi = DateTime.Now.ToString("HH:mm:ss");
                    string yeniSatir = $"[{zamanDamgasi}] {taninanMetin}";

                    txtSonuc.AppendText(yeniSatir + Environment.NewLine);
                    txtSonuc.SelectionStart = txtSonuc.Text.Length;
                    txtSonuc.ScrollToCaret();
                }
            }
            catch (Exception ex)
            {
                // JSON ayrıştırma hatası, genelde çok nadir olur.
                DurumGuncelle($"JSON ayrıştırma hatası: {ex.Message}");
            }
        }

        private void WaveIn_RecordingStopped(object sender, StoppedEventArgs e)
        {
            if (e.Exception != null)
            {
                DurumGuncelle($"Kayıt durduruldu (Hata: {e.Exception.Message})");
            }
            else
            {
                DurumGuncelle("Durduruldu ⏸");
            }

            // Son kalan veriyi işle
            string finalResultJson = voskRecognizer.FinalResult();
            ProcessVoskResult(finalResultJson);
        }

        // --- Buton Olayları ---

        private void btnBaslat_Click(object sender, EventArgs e)
        {
            if (waveIn != null)
            {
                try
                {
                    waveIn.StartRecording();
                    if (WaveIn.DeviceCount < 1)
                    {
                        MessageBox.Show("Mikrofon bulunamadı. Lütfen bir mikrofon bağlayın.", "Donanım Hatası", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    DurumGuncelle("Dinliyorum... 🎤 Konuşun!");
                    btnBaslat.Enabled = false;
                    btnDurdur.Enabled = true;
                }
                catch (Exception ex)
                {
                    DurumGuncelle($"Başlatma Hatası: {ex.Message}");
                    MessageBox.Show($"Mikrofon başlatılamadı:\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnDurdur_Click(object sender, EventArgs e)
        {
            if (waveIn != null)
            {
                waveIn.StopRecording();
                btnBaslat.Enabled = true;
                btnDurdur.Enabled = false;
            }
        }

        private void btnTemizle_Click(object sender, EventArgs e)
        {
            txtSonuc.Clear();
        }

        private void DurumGuncelle(string mesaj)
        {
            if (lblDurum.InvokeRequired)
            {
                lblDurum.Invoke(new Action<string>(DurumGuncelle), mesaj);
                return;
            }

            lblDurum.Text = mesaj;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Uygulama kapanırken kaynakları serbest bırak
            if (waveIn != null)
            {
                waveIn.StopRecording();
                waveIn.Dispose();
            }

            if (voskRecognizer != null)
            {
                voskRecognizer.Dispose();
            }

            // Vosk Modelini de serbest bırakın (Vosk'un kendisi yönetebilir ancak iyi bir pratiktir)
            // if (voskModel != null)
            // {
            //     voskModel.Dispose();
            // }
        }
    }
}