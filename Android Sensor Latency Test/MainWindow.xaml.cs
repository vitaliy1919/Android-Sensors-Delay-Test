using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Sensor_Test_Visual
{
    class SensorData
    {
        public string type;
        public long timestamp;
        public float[] values;
    }
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Thread sensorDataPoll;
        Line a;
        Mutex mutex = new Mutex();
        Mutex monitor = new Mutex();

        double layingZ = double.MinValue;
        volatile  bool calibrated = false;
        private volatile bool monotoringEnabled;

        public MainWindow()
        {
            Closing += MainWindow_Closing;
             a = new Line();
            a.X1 = 50; a.Y1 = 50;
            a.X2 = 100; a.Y2 = 50;
            a.Stroke = System.Windows.Media.Brushes.LightSteelBlue;
            a.StrokeThickness = 10;
            InitializeComponent();
            canvas.Children.Add(a);
            string strCmdText;
            strCmdText = @"/C ""C:\Users\19VD5\Downloads\tools_r29.0.2-windows\adb"" forward tcp:65000 tcp:65000";
            System.Diagnostics.Process.Start("CMD.exe", strCmdText);
            sensorDataPoll = new Thread(sensorDataLoop);          // Kick off a new thread
            sensorDataPoll.Start();
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            sensorDataPoll.Abort();
        }

        void sensorDataLoop()
        {
            TcpClient clientSocket = new System.Net.Sockets.TcpClient();
            clientSocket.Connect("127.0.0.1", 65000);
            var stream = clientSocket.GetStream();
            var bytes = new byte[512];
            System.IO.StreamWriter json = new System.IO.StreamWriter(@"json.txt");
            System.IO.StreamWriter file = new System.IO.StreamWriter(@"log.txt");

            var jsonString = "";
            var offset = 0;
            var start = -1;
            var end = -1;
            var firstLineReceived = false;
            var timer = new Stopwatch();
            var jsons = new List<String>();
            var difference = 0L;
            do
            {
               
               
                var len = stream.Read(bytes, 0, bytes.Length);
                var iter = offset;
                while (iter < len)
                {
                    if (start == -1 && jsonString == "")
                    {
                        if (bytes[iter] == 0)
                            start = iter;
                    }
                    else if (bytes[iter] == 1)
                    {
                        var curJsonString = (iter > start ? Encoding.ASCII.GetString(bytes, start + 1, iter - start - 1) : "");
                        if (jsonString != "")
                        {
                            curJsonString = jsonString + curJsonString;
                            jsonString = "";
                        }
                        //json.Write(curJsonString);
                        Console.WriteLine("JSON: " + curJsonString);
                        var jsonObj = JsonConvert.DeserializeObject<SensorData>(curJsonString);
                        mutex.WaitOne();
                        if (calibrated && jsonObj.type.Contains("Acc"))
                        {
                            if (layingZ == double.MinValue)
                                layingZ = jsonObj.values[2];
                            var alpha = Math.Acos(Math.Min(jsonObj.values[2] / layingZ, 1));
                            var cos = Math.Min(jsonObj.values[2] / layingZ, 1);
                            var sin = Math.Sqrt(1 - cos * cos);
                            this.Dispatcher.Invoke(() =>
                            {
                                a.X1 = 75 - 25 * cos;
                                a.Y1 = 50 + 25 * sin;
                                a.X2 = 75 + 25 * cos;
                                a.Y1 = 50 - 25 * sin;
                            });
                        }
                        mutex.ReleaseMutex();
                        monitor.WaitOne();
                        if (monotoringEnabled)
                        {
                            file.Write($"{jsonObj.type}, {jsonObj.timestamp}, ");
                            for (int i = 0; i < jsonObj.values.Length; i++)
                                if (i != jsonObj.values.Length - 1)
                                    file.Write($"{jsonObj.values[i]}, ");
                                else
                                    file.WriteLine($"{jsonObj.values[i]}");
                        }
                        monitor.ReleaseMutex();
                        //    //Console.WriteLine($"{Math.Asin(jsonObj.values[2] / 9.9)} degree");
                        //if (!firstLineReceived)
                        //{
                        //    firstLineReceived = true;
                        //    difference = jsonObj.timestamp;
                        //    timer.Start();
                        //    json.WriteLine();
                        //}
                        //else
                        //{
                        //    json.WriteLine($"  {(double)timer.ElapsedTicks / Stopwatch.Frequency * 1_000_000_000}ns, {difference}, {jsonObj.timestamp}");

                        //    //var diff = (double)timer.ElapsedTicks / Stopwatch.Frequency*1_000_000_000 + difference;
                        //    //json.WriteLine($"  {(diff - jsonObj.timestamp)/ 1_000_000_000d}s");
                        //    //Console.WriteLine($"  {(diff - jsonObj.timestamp) / 1_000_000_000}s");

                        //}
                        //Console.WriteLine(jsonObj.type);
                        jsons.Add(curJsonString);

                        //if (curJsonString.Contains("Gyro"))
                        start = -1;
                    }
                    ++iter;
                }
                if (start == -1)
                {
                    jsonString = "";
                }
                else
                {
                    jsonString += Encoding.ASCII.GetString(bytes, start + 1, len - start - 1);
                    start = -1;
                }
                //if (bytes[0] != '{')
                //{
                //    offset = 0;
                //    continue;
                //}
                //var str = Encoding.ASCII.GetString(bytes, 0, len);
                //if (str[0] == '{')
                //    Console.WriteLine("--");
                //file.WriteLine("#" + str + "#");
                //Console.WriteLine(str);
            } while (true);
            stream.Close();
            clientSocket.Close();
        }

        private void CalibrateButton_Click(object sender, RoutedEventArgs e)
        {
            mutex.WaitOne();

            calibrated = true;
            mutex.ReleaseMutex();
        }

        private void StartRecordButton_Click(object sender, RoutedEventArgs e)
        {
            mutex.WaitOne();
            monotoringEnabled = true;
            mutex.ReleaseMutex();
        }

        private void StopRecordButton_Click(object sender, RoutedEventArgs e)
        {
            mutex.WaitOne();
            monotoringEnabled = false;
            mutex.ReleaseMutex();
        }
    }

}
