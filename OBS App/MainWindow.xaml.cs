using Bluegrams.Application;
using System;
using System.Diagnostics;
using System.Net;
using System.IO;
using System.Timers;
using System.Windows;
using System.Text;
using System.Threading.Tasks;

namespace ObsHeartRateMonitor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly Ant.Dongle antDongle;
        private readonly Ant.Device.HeartRateMonitor heartRateMonitor;
        private readonly Timer logTimer;
        private int lastLogValue;
        private static readonly HttpListener httpListener = new HttpListener();
        private string httpHtml;
        private string httpJs;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public MainWindow()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {
            PortableSettingsProvider.SettingsFileName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name + ".config";
            PortableSettingsProvider.SettingsDirectory = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule!.FileName);
            PortableSettingsProvider.ApplyProvider(Properties.Settings.Default);

            InitializeComponent();

            Application.Current.DispatcherUnhandledException += UnhandledException;

            int sensor = Properties.Settings.Default.Sensor;
            if (sensor != 0) TextBoxSensorId.Text = sensor.ToString();
            CheckBoxLogEnabled.IsChecked = Properties.Settings.Default.IsLogEnabled;
            ComboBoxLogRate.SelectedIndex = Properties.Settings.Default.LogRefreshRate;
            CheckBoxHttpEnabled.IsChecked = Properties.Settings.Default.IsHttpEnabled;
            TextBoxHttpHost.Text = Properties.Settings.Default.HttpHost;
            TextBoxHttpPort.Text = Properties.Settings.Default.HttpPort.ToString();
            CheckBoxReconnect.IsChecked = Properties.Settings.Default.AutoReconnect;

            try
            {
                antDongle = new Ant.Dongle();
                antDongle.Initialize();

                heartRateMonitor = new Ant.Device.HeartRateMonitor(antDongle.Channels[0]);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }

            logTimer = new Timer
            {
                AutoReset = true
            };
            logTimer.Elapsed += LogTimer_Elapsed;
        }

        private void UnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show("An unhandled exception occurred: " + e.Exception.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }

        private void ButtonSearch_Click(object sender, RoutedEventArgs e)
        {
            var searchWindow = new SearchWindow(heartRateMonitor);
            searchWindow.ShowDialog();
        }

        private void ButtonConnectDisconnect_Click(object sender, RoutedEventArgs e)
        {
            if ((string)ButtonConnectDisconnect.Content == "Connect")
            {
                Connect();
            }
            else
            {
                Disconnect();
            }
        }

        private void HeartRateMonitor_NewSensorDataReceived(object? sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() => { TextBlockHeartRate.Text = heartRateMonitor.HeartRate.ToString(); }));
        }
        private void HeartRateMonitor_SensorNotFound(object? sender, EventArgs e)
        {
            MessageBox.Show("Sensor not found or disconnected", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Dispatcher.BeginInvoke(new Action(() => { Disconnect(); }));
        }

        private void LogTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            int heartRate = heartRateMonitor.HeartRate;
            if ((heartRate > 0) && (lastLogValue != heartRate))
            {
                File.WriteAllText(System.Reflection.Assembly.GetExecutingAssembly().GetName().Name + ".txt", heartRate.ToString());
                lastLogValue = heartRate;
            }
        }

        private void Connect()
        {
            if (!int.TryParse(TextBoxSensorId.Text, out int sensorId))
            {
                MessageBox.Show("Sensor ID must be a number");
                return;
            }
            if (sensorId < 0 || sensorId > 65535)
            {
                MessageBox.Show("Sensor ID must be between 0 (=Any) and 65535");
                return;
            }

            if (!int.TryParse(TextBoxHttpPort.Text, out int httpPort))
            {
                MessageBox.Show("HTTP port must be a number");
                return;
            }
            if (httpPort < 1024 || httpPort > 65535)
            {
                MessageBox.Show("HTTP port must be between 1024 and 65535");
                return;
            }

            ButtonConnectDisconnect.Content = "Disconnect";
            TextBoxSensorId.IsEnabled = false;
            ButtonSearch.IsEnabled = false;
            GroupBoxObs.IsEnabled = false;
            GroupBoxHttp.IsEnabled = false;
            CheckBoxReconnect.IsEnabled = false;
            heartRateMonitor.NewSensorDataReceived += HeartRateMonitor_NewSensorDataReceived;

            bool reconnect = CheckBoxReconnect.IsChecked ?? false;
            if (!reconnect)
                heartRateMonitor.SensorNotFound += HeartRateMonitor_SensorNotFound;
            heartRateMonitor.Start((ushort)sensorId, reconnect);

            if (sensorId > 0)
            {
                Properties.Settings.Default.Sensor = sensorId;
            }
            Properties.Settings.Default.IsLogEnabled = CheckBoxLogEnabled.IsChecked ?? false;
            Properties.Settings.Default.LogRefreshRate = ComboBoxLogRate.SelectedIndex;
            Properties.Settings.Default.IsHttpEnabled = CheckBoxHttpEnabled.IsChecked ?? false;
            Properties.Settings.Default.HttpHost = TextBoxHttpHost.Text ?? "localhost";
            Properties.Settings.Default.HttpPort = httpPort;
            Properties.Settings.Default.AutoReconnect = reconnect;
            Properties.Settings.Default.Save();

            if (CheckBoxLogEnabled.IsChecked ?? false)
            {
                logTimer.Interval = (ComboBoxLogRate.SelectedIndex + 1) * 1000;
                logTimer.Start();
            }

            if (CheckBoxHttpEnabled.IsChecked ?? false)
            {
                var baseName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
                var htmlFileName = baseName + ".html";
                var jsFileName = baseName + ".js";
                if (!File.Exists(htmlFileName)) File.WriteAllText(htmlFileName, Properties.Resources.HtmlTemplate);
                if (!File.Exists(jsFileName)) File.WriteAllText(jsFileName, Properties.Resources.HtmlClientJs);

                var httpHost = Properties.Settings.Default.HttpHost;
                httpHtml = File.ReadAllText(htmlFileName)
                    .Replace("%heart_rate_value%", "<span id=\"heart-rate-value\"></span>")
                    .Replace("%heart_rate_js%", jsFileName)
                    .Replace("%heart_rate_url%", $"http://{httpHost}:{httpPort}/data");
                httpJs = File.ReadAllText(jsFileName);

                httpListener.Prefixes.Clear();
                httpListener.Prefixes.Add($"http://{httpHost}:{httpPort}/");
                httpListener.Start();
                HttpListen();
            }
        }

        private async void HttpListen()
        {
            while (httpListener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = await httpListener.GetContextAsync();
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    Task.Factory.StartNew(() => HttpProcess(context));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                }
                catch (HttpListenerException)
                {
                    // Presumably the listener was stopped, ignore
                }
            }
        }

        private void HttpProcess(HttpListenerContext context)
        {
            var req = context.Request;
            var resp = context.Response;
            switch (req.Url?.AbsolutePath ?? "")
            {
                case "/":
                    HttpSend(resp, "text/html", httpHtml);
                    break;
                case "/client.js":
                    HttpSend(resp, "application/javascript", httpJs);
                    break;
                case "/data":
                    string heartRate = heartRateMonitor.HeartRate.ToString();
                    if (heartRate == "-1") heartRate = "\"?\"";
                    HttpSend(resp, "application/json", "{\"value\":" + heartRate + "}");
                    break;
                default:
                    context.Response.StatusCode = 404;
                    HttpSend(resp, "text/plain", "Not Found");
                    break;
            }
        }

        private static void HttpSend(HttpListenerResponse response, string contentType, string data)
        {
            byte[] output = Encoding.UTF8.GetBytes(data);
            response.AddHeader("content-type", contentType);
            response.ContentLength64 = data.Length;
            response.OutputStream.Write(output, 0, output.Length);
            response.OutputStream.Close();
        }

        private void Disconnect()
        {
            ButtonConnectDisconnect.Content = "Connect";
            TextBoxSensorId.IsEnabled = true;
            ButtonSearch.IsEnabled = true;
            GroupBoxObs.IsEnabled = true;
            GroupBoxHttp.IsEnabled = true;
            CheckBoxReconnect.IsEnabled = true;
            heartRateMonitor.NewSensorDataReceived -= HeartRateMonitor_NewSensorDataReceived;
            heartRateMonitor.SensorNotFound -= HeartRateMonitor_SensorNotFound;
            heartRateMonitor.Stop();
            logTimer.Stop();
            httpListener.Stop();
        }

    }
}
