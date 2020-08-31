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
        int winLength;
        double lambda;
        double variance;
        double windowms;
        double intervalms;
        int windowsCount;
        int intervalLength;
        int sampleCount;
        int[] audioData;
        double [] hiPassAudio;
        double[] zcDistribution;
        List<int> zeros = new List<int>();
        List<int> speechSamples = new List<int>();
        List<int> noiseSamples = new List<int>();
        bool is16bit;
        StripLine thresholdLine = new StripLine();

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


            // Set chart 1 properties and.
            chart1.Palette = ChartColorPalette.SeaGreen;
            chart1.ChartAreas[0].AxisX.ScaleView.Zoomable = true;

            //set chart2 controls
            chart2.Palette = ChartColorPalette.Bright;

            //set textbox initial values
            textBox5.Text = "15";
            textBox6.Text = "0";

            // set numeric initla values
            numericUpDown1.Value = 100;
            numericUpDown2.Value = 4;

            
        }

        private void AddThresholdStripLine()
        {
            LegendItem legendItem = new LegendItem();
            legendItem.Name = "Voiced Threshold";
            legendItem.ImageStyle = LegendImageStyle.Line;
            legendItem.Color = Color.Red;
            chart1.Legends["Legend1"].CustomItems.Add(legendItem);
            thresholdLine.Interval = 0;
            thresholdLine.IntervalOffset = 1000;
            thresholdLine.BackColor = chart1.Legends["Legend1"].CustomItems[0].Color;
            thresholdLine.StripWidth = 0.25;
            thresholdLine.ForeColor = Color.Black;
            chart1.ChartAreas[0].AxisY.StripLines.Add(thresholdLine);            
        }


        private void chart1_MouseWheel(object sender, MouseEventArgs e)
        {
            //Control X-Axis scrolling on chart using mouse Whee;
            var chart = (Chart)sender;
            var xAxis = chart.ChartAreas[0].AxisX;

            try
            {
                if (e.Delta < 0) // Scrolled down.
                {
                    xAxis.ScaleView.ZoomReset();
                }
                else if (e.Delta >= 0)
                {
                    var xMin = xAxis.ScaleView.ViewMinimum;
                    var xMax = xAxis.ScaleView.ViewMaximum;
                    var posXStart = xAxis.PixelPositionToValue(e.Location.X) - (xMax - xMin) / 4;
                    var posXFinish = xAxis.PixelPositionToValue(e.Location.X) + (xMax - xMin) / 4;
                    xAxis.ScaleView.Zoom(posXStart, posXFinish);
                }
            }
            catch{ }
        }


        private void button1_Click(object sender, EventArgs e)
        {
            //Load new audio data
            speechSamples.Clear();
            noiseSamples.Clear();
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
                        chart1.ChartAreas[0].AxisY.Minimum = -8000;
                        chart1.ChartAreas[0].AxisY.Maximum = 8000;
                        zeros.Clear();
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
            //Filter loaded audio data then count and plot zero crossings
            if (audioData != null)
            {
                PlotFilteredWAV();
                ZeroCCounter();
                PlotZeroCrossings();
                label2.Text = String.Format("Total Zero Crossings: {0}({0}/{1} = {2:F2}%)", zeros.Count, sampleCount, (double)zeros.Count*100 / sampleCount); 
            }
            else
            {
                MessageBox.Show("Error: Must load audio data first to apply filter.");
            }
        }


        private void button3_Click(object sender, EventArgs e)
        { 
            //Write zero croosing data to a file when button is clicked
            if (textBox2.Text !="")
            {
                //WriteZCToFile(textBox2.Text);
                WriteZCDistToFile(textBox2.Text);
            } else
            {
                MessageBox.Show("Error: Must enter a proper filename in text box.");
            }
        }


        private void button4_Click_1(object sender, EventArgs e)
        {
            Console.WriteLine("button4 clicked");
            // Calculate lambda and poisson distribution from window input
            winLength = (int)numericUpDown2.Value;
            if (winLength <= 2 || winLength > sampleCount)
            {
                MessageBox.Show("Window length must be between 2 and " + sampleCount + ". Please re-enter.", "Confirm", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            else if (audioData == null)
            {
                MessageBox.Show("Error: Must load audio data first to calculate lambda and poissson values.");
                return;
            }
            //Find interval length
            intervalLength = (int)numericUpDown1.Value;
            if (intervalLength < 2 * winLength)
            {
                MessageBox.Show("Error: Interval length must be at least twice sample window length.");
                return;
            }
            if (intervalLength > sampleCount / 10)
            {
                MessageBox.Show("Error: Interval length must be less than 1/10th sample count.");
                return;
            }
            //Calculate window size in ms
            windowms = (double)winLength * 1000 / sampleRate;
            intervalms = (double)intervalLength * 1000 / sampleRate;
            textBox4.Text = String.Format(windowms.ToString() + " ms");
            textBox3.Text = String.Format(intervalms.ToString() + " ms");
            //Calculate lambda
            Console.WriteLine("Finding Zero crossing distribution.");
            FindZCDist();
            //Plot histogram
            PlotZCDist();
            //Write to file
            //WriteZCDistToFile("ZCdata_" + textBox1.Text);
            
        }

        private void button5_Click(object sender, EventArgs e)
        {
            //Adjust that X-axis range values 
            int xRange, xStart;
            if(!Int32.TryParse(textBox5.Text, out xRange))
            {
                MessageBox.Show("Error: Xrange value is not an integer");
                textBox5.Text = "";
                return;
            }
            if (xRange <=0 || xRange >= sampleCount)
            {
                MessageBox.Show("Error: Xrange needs to be less than total samples: " + sampleCount);
                textBox5.Text = "";
                return;
            }

            if (!Int32.TryParse(textBox6.Text, out xStart))
            {
                MessageBox.Show("Error: Xstart value is not an integer");
                textBox6.Text = "";
                return;
            }
            if (xStart < 0 || xStart >= sampleCount-xRange)
            {
                MessageBox.Show("Error: Xstart needs to be nonnegative but less than total samples - xRange: " + (sampleCount - xRange).ToString());
                textBox6.Text = "";
                return;
            }
            var xAxis = chart1.ChartAreas[0].AxisX;
            var posXStart = xStart;
            var posXFinish = xStart + xRange;
            xAxis.ScaleView.Zoom(posXStart, posXFinish);
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
            double yrange = (int)audioData.Max() * 2;
            int yinterval = (int)Math.Round(yrange / 8);
            Console.WriteLine(String.Format("Raw: Ymax:{0}, Yrange: {1}, Yinterval: {2}", (int)audioData.Max(), yrange, yinterval));
            chart1.ChartAreas[0].AxisY.Maximum = 4*yinterval;
            chart1.ChartAreas[0].AxisY.Minimum = -4*yinterval;
            chart1.ChartAreas[0].AxisY.Interval = yinterval;

            //Plot Raw wave data to chart
            chart1.Series.Add("Raw Data");
            chart1.Series["Raw Data"].ChartType = SeriesChartType.Line;
            chart1.Series["Raw Data"].Points.DataBindY(audioData);
            chart1.Titles.Clear();
            chart1.Titles.Add(String.Format("Channels: {0}, SR : {1}, BPS {2}, samples : {3}", channels, sampleRate, bitsPerSample, sampleCount));
        }

        private void PlotFilteredWAV()
        {
            
            hiPassAudio = new double[sampleCount];
            hiPassAudio[0] = 0;
            //Apply low pass filter to data
            for (int i = 1; i < sampleCount; i++)
            {
                hiPassAudio[i] = audioData[i] - 0.95 * audioData[i - 1];
            }

            // divided the Y-axis into integer intervals
            double yrange = (int)hiPassAudio.Max() * 2;
            int yinterval = (int)Math.Round(yrange / 6);
            Console.WriteLine(String.Format("Filtered: Ymax:{0}, Yrange: {1}, Yinterval: {2}", (int)hiPassAudio.Max(), yrange, yinterval));
            chart1.ChartAreas[0].AxisY.Maximum = 4 * yinterval;
            chart1.ChartAreas[0].AxisY.Minimum = -4 * yinterval;
            chart1.ChartAreas[0].AxisY.Interval = yinterval;

            //Plot filtered audio data
            chart1.Series["Raw Data"].Enabled = false;
            chart1.Series.Add("Hi Pass Data");
            chart1.Series["Hi Pass Data"].ChartType = SeriesChartType.Line;
            chart1.Series["Hi Pass Data"].Points.DataBindY(hiPassAudio);
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
                if ((hiPassAudio[i-1] > 0 && hiPassAudio[i] <= 0) || (hiPassAudio[i - 1] < 0 && hiPassAudio[i] >= 0))
                {
                    zeros.Add(i+1);
                    Console.WriteLine(String.Format("Writing ZC at {0}. Hipass[i-1] = {1}, Hi Pass[i]= {2}", i, hiPassAudio[i - 1], hiPassAudio[i]));
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
        
        private void FindZCDist()
        {
            //Calculate Lambda for window size
            
            zcDistribution = new double[winLength+1];
            windowsCount = sampleCount / winLength;
            lambda = (double)zeros.Count / windowsCount;
            Console.WriteLine("Lambda = " + lambda);
            double sumSquares = 0;
            int windowSum;
            int zeroIndex = 0;
            for (int i = winLength-1; i< sampleCount; i+= winLength)
            {
                windowSum = 0;
                while(zeros[zeroIndex] <= i)
                {
                    windowSum++;
                    if (zeroIndex < zeros.Count - 1)
                        zeroIndex++;
                    else break;
                }
                //Console.WriteLine(String.Format("Window Sample Limit {0}, windowSum {1}, Zero index: {2}", i, windowSum,zeroIndex));
                sumSquares += Math.Pow(windowSum - lambda, 2);
                zcDistribution[windowSum]++;                 
            }
            variance = sumSquares / windowsCount;
        }

        private void PlotZCDist()
        {
            int[] xValues = new int[winLength + 1];
            double[] pValues = new double[winLength + 1];   //Poisson values
            Console.WriteLine("Beginning ZeroCount Calculation");
            for ( int i = 0; i<= winLength; i++)
            {
                xValues[i] = i;
                pValues[i] = FindPoisson(lambda, i)*windowsCount;
                Console.WriteLine(String.Format("{0}: pValues: {1}", i, pValues[i]));
            }
            chart2.Series.Clear();
            chart2.Series.Add("Zero Crossings");
            chart2.Series["Zero Crossings"].Points.DataBindXY(xValues,zcDistribution);
            chart2.Titles.Clear();
            chart2.Titles.Add(String.Format("Total samples : {0}, total zeros {1}\nWindows count : {2}, \u03BB : {3:F4}, Var : {4:F4}", sampleCount, zeros.Count,windowsCount, lambda, variance));
            //chart2.Series.Add("Poisson");
            //chart2.Series["Poisson"].Points.DataBindXY(xValues, pValues);
        }

        private void WriteZCDistToFile(String fpath)
        {
            string filename = Path.GetFullPath(fpath + ".txt");
            if (File.Exists(filename))
            {
                File.Delete(filename);
            }
            using (FileStream stream = new FileStream(filename, FileMode.CreateNew, FileAccess.Write))
            {
                Console.WriteLine("File: " + filename + " successfully created.");
                // Create a StreamWriter from FileStream  
                StreamWriter sr = new StreamWriter(stream);
                sr.WriteLine(sampleCount);
                sr.WriteLine(zeros.Count);
                sr.WriteLine(windowsCount);
                sr.WriteLine(lambda);
                sr.WriteLine(variance);
                foreach (int val in zcDistribution)
                {
                    sr.WriteLine(val);
                }
                sr.Close();
            }
            Console.WriteLine("Successfully wrote ZC distribtion to file: "+ filename);
        }


        private double FindPoisson(double l,int k)
        {
            //Return the value of Poisson with X=k and lambda = l
            int factorial;
            if (k > 1)
            {
                factorial = k;
                for (int i = k - 1; i > 1; i--)
                {
                    factorial *= i;
                }
            }
            else
            {
                factorial = 1;
            }

            return Math.Pow(l, k) * Math.Exp(-l) / factorial;
        }

        private double FindVar(double [] values, double m)
        {
            //Find standard deviation of an array of values with mean, m.
            double sumSquares =0;
            int N = values.Length;
            foreach (double value in values){
                sumSquares += Math.Pow(value - m, 2);
            }
            return sumSquares / N   ;
        }

        private void PlotZeroCrossings()
        {
            //Add a new fiedl in the chart plotting the zero crossings calculated but not yet visible
            chart1.Series.Add("Zero Crossings");
            chart1.Series["Zero Crossings"].ChartType = SeriesChartType.Point;
            chart1.Series["Zero Crossings"].MarkerColor = Color.Red;
            chart1.Series["Zero Crossings"].MarkerStyle = MarkerStyle.Circle;
            foreach (int zero in zeros)
            {
                chart1.Series["Zero Crossings"].Points.AddXY(zero, 0.0);
            }

        }
        
    }      
}

