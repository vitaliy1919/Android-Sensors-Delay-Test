using Android_Sensor_Test;
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
        Mutex monitorMutex = new Mutex();
        //Mutex monitor = new Mutex();
        AndroidSensorMonitor monitor;
        double layingZ = double.MinValue;
        volatile  bool calibrated = false;
        private volatile bool monotoringEnabled;
        System.IO.StreamWriter json = new System.IO.StreamWriter(@"json.txt");
        System.IO.StreamWriter file = new System.IO.StreamWriter(@"log.txt");

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
            monitor = new AndroidSensorMonitor();
            AndroidSensorMonitor.StartADBForwarding(@"C:\Users\19VD5\Downloads\tools_r29.0.2-windows\adb", 65000, 65000);
            monitor.sensorDataCallback += Monitor_sensorDataCallback;
            monitor.StartMonitoring(65000);
        }

        private void Monitor_sensorDataCallback(Android_Sensor_Test.SensorData jsonObj)
        {
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
            monitorMutex.WaitOne();
            if (monotoringEnabled)
            {
                file.Write($"{jsonObj.type}, {jsonObj.timestamp}, ");
                for (int i = 0; i < jsonObj.values.Length; i++)
                    if (i != jsonObj.values.Length - 1)
                        file.Write($"{jsonObj.values[i]}, ");
                    else
                        file.WriteLine($"{jsonObj.values[i]}");
            }
            monitorMutex.ReleaseMutex();
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            monitor.StopMonitoring();
            json.Close();
            file.Close();
            //sensorDataPoll.Abort();
        }

       
        private void CalibrateButton_Click(object sender, RoutedEventArgs e)
        {
            mutex.WaitOne();

            calibrated = true;
            mutex.ReleaseMutex();
        }

        private void StartRecordButton_Click(object sender, RoutedEventArgs e)
        {
            monitorMutex.WaitOne();
            monotoringEnabled = true;
            monitorMutex.ReleaseMutex();
        }

        private void StopRecordButton_Click(object sender, RoutedEventArgs e)
        {
            monitorMutex.WaitOne();
            monotoringEnabled = false;
            monitorMutex.ReleaseMutex();
        }
    }

}
