using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Numerics;
using System.Windows.Forms.DataVisualization.Charting;

// This is the code for your desktop app.
// Press Ctrl+F5 (or go to Debug > Start Without Debugging) to run your app.

namespace VoiceActivtyDetector
{
    public partial class Form1 : Form
    {

        int channels;
        int sampleRate;
        int bitsPerSample;
        int dataLength;
        int sampleCount;
        int[] audioData;
        double [] loPassAudio;
        List<int> zeros = new List<int>();
        bool is16bit;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            openFileDialog1.RestoreDirectory = true;
            openFileDialog1.Title = "Browse Audio Files";
            openFileDialog1.DefaultExt = "wav";
            openFileDialog1.CheckFileExists = true;
            openFileDialog1.CheckPathExists = true;
            openFileDialog1.Filter = "Audio Files (.wav)|*.wav";
            openFileDialog1.FilterIndex = 2;
            openFileDialog1.Multiselect = false;


            // Set palette.
            chart1.Palette = ChartColorPalette.SeaGreen;
            
        }

        private void button1_Click(object sender, EventArgs e)
        {
            openFileDialog1 = new OpenFileDialog();

            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                textBox1.Text = openFileDialog1.FileName;
                Console.WriteLine(openFileDialog1.FileName);
                try
                {
                    if (openFileDialog1.OpenFile() != null)
                    {
                        Console.WriteLine("File " + openFileDialog1.FileName + " opened successfully!");
                        WaveReader(openFileDialog1.FileName);
                        chart1.Series.Clear();
                        chart1.ChartAreas[0].AxisY.Minimum = -10000;
                        chart1.ChartAreas[0].AxisY.Maximum = 10000;
                        PlotRawWAV();
                        Console.WriteLine(String.Format(" Channels: {0}, SR : {1}, BPS {2}, samples : {3}", channels, sampleRate, bitsPerSample, sampleCount));
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error: Could not read file from disk.  Original error: " + ex.Message);
                }
            }
        }


        private void button2_Click(object sender, EventArgs e)
        {
            //Filter loaded audio data
            if (audioData != null)
            {
                PlotFilteredWAV();
                ZeroCCounter();
                label2.Text = String.Format("Total Zero Crossings: {0}({0}/{1} = {2:F2}%)", zeros.Count, sampleCount, (double)zeros.Count*100 / sampleCount); 
            }
            else
            {
                MessageBox.Show("Error: Must load audio data first to apply filter.");
            }
        }


        private void button3_Click(object sender, EventArgs e)
        { 
            if (textBox2.Text !="")
            {
                WriteZCToFile(textBox2.Text);
            } else
            {
                MessageBox.Show("Error: Must enter a proper filename in text box.");
            }
        }


        private void WaveReader(string spath)
        {
            FileStream fs = new FileStream(spath, FileMode.Open, FileAccess.Read);
            BinaryReader br = new BinaryReader(fs);
            fs.Position = 22;
            channels = br.ReadInt16();
            fs.Position = 24;
            sampleRate = br.ReadInt32();
            fs.Position = 34;
            bitsPerSample = br.ReadInt16();
            if ( bitsPerSample != 16 && bitsPerSample != 32)
            {
                MessageBox.Show("Error: bits per sample does not equal 16 or 32 bits/s");
                return;
            }
            is16bit = bitsPerSample == 16;
            dataLength = (int)fs.Length - 44;
            sampleCount = dataLength / (bitsPerSample / 8);
            audioData = new int[sampleCount];
            fs.Position = 44;
            for (int i = 0; i < sampleCount; i++)
            {
                if (is16bit){audioData[i] = br.ReadInt16();}
                else {audioData[i] = br.ReadInt32();}
            }
            Console.WriteLine("WAV File read succesfully");
            br.Close();
            fs.Close();
        }

        private void PlotRawWAV()
        {
            //Plot Raw wave data to chart
            chart1.Series.Add("Raw Data");
            chart1.Series["Raw Data"].Points.DataBindY(audioData);
            chart1.Titles.Clear();
            chart1.Titles.Add(String.Format("Channels: {0}, SR : {1}, BPS {2}, samples : {3}", channels, sampleRate, bitsPerSample, sampleCount));
        }

        private void PlotFilteredWAV()
        {
            loPassAudio = new double[sampleCount];
            loPassAudio[0] = audioData[0];
            //Apply low pass filter to data
            for (int i = 1; i < sampleCount; i++)
            {
                loPassAudio[i] = audioData[i] - 0.95 * audioData[i - 1];
            }
            //Plot filtered audio data
            chart1.Series.Add("Low Pass Data");
            chart1.Series["Low Pass Data"].Points.DataBindY(loPassAudio);
        }

        private void PlayWAVFile(string spath)
        {
            FileStream fs = new FileStream(spath, FileMode.Open, FileAccess.Read);
            System.Media.SoundPlayer sp = new System.Media.SoundPlayer(fs);
            sp.Play();
            fs.Close();
        }


        private void ZeroCCounter()
        {
            //Count all zero crossings and record time sample in zeros list
            for (int i =1; i< sampleCount; i++)
            {
                if ((loPassAudio[i-1] > 0 && loPassAudio[i] <= 0) || (loPassAudio[i - 1] < 0 && loPassAudio[i] >= 0))
                {
                    zeros.Add(i);
                }
            }
        }

        private void WriteZCToFile(String fpath)
        {
            /****************************************************
             * Writing Zero crossing data in the following format
             * Sample rate
             * Bitspersample
             * sample count
             * zero Count
             * first zero crossing
             * second zero crossing
             * :
             * :
             * EOF
             * *************************************************/
            string filename = Path.GetFullPath(fpath +".txt");
            if (File.Exists(filename))
            {
                File.Delete(filename);
            }
            using (FileStream stream = new FileStream(filename, FileMode.CreateNew, FileAccess.Write))
            {
                Console.WriteLine("File: " + filename + " successfully created.");
                // Create a StreamWriter from FileStream  
                StreamWriter sr = new StreamWriter(stream);
                sr.WriteLine(sampleRate);
                sr.WriteLine(bitsPerSample);
                sr.WriteLine(sampleCount);
                sr.WriteLine(zeros.Count);
                foreach (int sample in zeros)
                {
                    sr.WriteLine(sample);
                }
                sr.Close();
            }

        }
    }      
}

