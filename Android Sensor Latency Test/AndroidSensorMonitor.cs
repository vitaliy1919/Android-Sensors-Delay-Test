using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Android_Sensor_Test
{
    enum SensorType
    {
        ACCELEROMETER = 1,
        ACCELEROMETER_UNCALIBRATED = 35,
        ALL = -1,
        AMBIENT_TEMPERATURE = 13,
        DEVICE_PRIVATE_BASE = 65536,
        GAME_ROTATION_VECTOR = 15,
        GEOMAGNETIC_ROTATION_VECTOR = 20,
        GRAVITY = 9,
        GYROSCOPE = 4,
        GYROSCOPE_UNCALIBRATED = 16,
        HEART_BEAT = 31,
        HEART_RATE = 21,
        LIGHT = 5,
        LINEAR_ACCELERATION = 10,
        LOW_LATENCY_OFFBODY_DETECT = 34,
        MAGNETIC_FIELD = 2,
        MAGNETIC_FIELD_UNCALIBRATED = 14,
        MOTION_DETECT = 30,
        ORIENTATION = 3,
        POSE_6DOF = 28,
        PRESSURE = 6,
        PROXIMITY = 8,
        RELATIVE_HUMIDITY = 12,
        ROTATION_VECTOR = 11,
        SIGNIFICANT_MOTION = 17,
        STATIONARY_DETECT = 29,
        STEP_COUNTER = 19,
        STEP_DETECTOR = 18
    }
    class SensorData
    {
        public SensorType type;
        public long timestamp;
        public float[] values;
    }
    delegate void SensorDataArrived(SensorData data);
    class AndroidSensorMonitor
    {
        private Thread monitoringThread;
        private Mutex mutex = new Mutex();
        public event SensorDataArrived sensorDataCallback;
        private volatile bool monitor = true;
        static public void StartADBForwarding(string pathToADB, int port1, int port2)
        {
            //C: \Users\19VD5\Downloads\tools_r29.0.2 - windows\adb
            var strCmdText = $"/C \"{pathToADB}\" forward tcp:{port1} tcp:{port2}";
            System.Diagnostics.Process.Start("CMD.exe", strCmdText);
        }
        public void StopMonitoring()
        {
            mutex.WaitOne();
            monitor = false;
            mutex.ReleaseMutex();

        }
        public void Wait()
        {
            monitoringThread.Join();
        }
        public void StartMonitoring(int port)
        {
            monitor = true;
            monitoringThread = new Thread(() =>
            {
                TcpClient clientSocket = new System.Net.Sockets.TcpClient();
                try
                {
                    clientSocket.Connect("127.0.0.1", port);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error happened when connecting to server: " + e.Message);
                    Console.WriteLine("Check that your server is up and that the adb forward command was executed properly");
                    Console.ReadKey();
                    return;
                }
                var stream = clientSocket.GetStream();
                var bytes = new byte[512];
                var jsonString = "";
                var offset = 0;
                var start = -1;
                var end = -1;
                var firstLineReceived = false;

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

                            var jsonObj = JsonConvert.DeserializeObject<SensorData>(curJsonString);
                            sensorDataCallback(jsonObj);
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
                    mutex.WaitOne();
                    if (!monitor)
                    {
                        mutex.ReleaseMutex();
                        break;
                    }
                    mutex.ReleaseMutex();
                } while (true);
                stream.Close();
                clientSocket.Close();
            });
            monitoringThread.Start();
        }
    }
}
