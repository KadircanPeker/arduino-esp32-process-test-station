using System;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ProcessTestApp.Infrastructure;

namespace ProcessTestApp.Communication
{
    /// <summary>
    /// Arduino veya ESP32 ile tek bir COM port üzerinden satır tabanlı haberleşme sağlar.
    /// Birlikte verilen iki cihaz yazılımı da 9600 baud kullanır.
    /// </summary>
    public sealed class ArduinoSerialService : IDisposable
    {
        public const int CanonicalBaudRate = 9600;

        private static readonly Lazy<ArduinoSerialService> InstanceHolder =
            new Lazy<ArduinoSerialService>(() => new ArduinoSerialService());

        private readonly object _portLock = new object();
        private readonly StringBuilder _lineBuffer = new StringBuilder();
        private SerialPort _serialPort;
        private Thread _readThread;
        private volatile bool _isReading;

        public string DeviceTag { get; set; } = "ARDUINO";

        public static ArduinoSerialService Instance => InstanceHolder.Value;

        public bool IsConnected
        {
            get
            {
                lock (_portLock)
                {
                    return _serialPort != null && _serialPort.IsOpen;
                }
            }
        }

        public string ConnectedPortName { get; private set; } = "";
        public int BaudRate { get; private set; } = CanonicalBaudRate;

        public event Action<TestData> OnTestResultReceived;
        public event Action<string> OnErrorReceived;
        public event Action<bool> OnConnectionStatusChanged;

        public ArduinoSerialService(string deviceTag = "ARDUINO")
        {
            DeviceTag = deviceTag;
        }

        public bool Connect(string portName, int baudRate = CanonicalBaudRate)
        {
            lock (_portLock)
            {
                if (_serialPort != null && _serialPort.IsOpen)
                {
                    if (ConnectedPortName.Equals(portName, StringComparison.OrdinalIgnoreCase)) return true;
                    CloseInternal();
                }

                try
                {
                    _serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
                    {
                        ReadTimeout = 2000,
                        WriteTimeout = 2000,
                        DtrEnable = true,
                        RtsEnable = true
                    };
                    _serialPort.Open();

                    ConnectedPortName = portName;
                    BaudRate = baudRate;
                    _lineBuffer.Clear();
                    _isReading = true;
                    _readThread = new Thread(ReadLoop)
                    {
                        IsBackground = true,
                        Name = "SerialMeasurementReadThread"
                    };
                    _readThread.Start();

                    FileLogger.Info("ArduinoSerialService", "Port açıldı: " + portName + " @ " + baudRate);
                    OnConnectionStatusChanged?.Invoke(true);
                    return true;
                }
                catch (Exception ex)
                {
                    FileLogger.Error("ArduinoSerialService", "Port açılamadı: " + ex.Message);
                    OnConnectionStatusChanged?.Invoke(false);
                    return false;
                }
            }
        }

        public void Disconnect()
        {
            lock (_portLock)
            {
                if (_serialPort != null && _serialPort.IsOpen)
                {
                    // Bağlantı kesilirken Arduino rölesini güvenli durumda bırak.
                    TryWriteLine("E_STOP");
                }
                CloseInternal();
            }
        }

        public bool SendRaw(string command)
        {
            lock (_portLock)
            {
                if (_serialPort == null || !_serialPort.IsOpen)
                {
                    FileLogger.Warning("ArduinoSerialService", "Kapalı porta komut gönderilemedi: " + command);
                    return false;
                }
                return TryWriteLine(command);
            }
        }

        public Task<bool> SendEmergencyStopAsync()
        {
            return Task.FromResult(SendRaw("E_STOP"));
        }

        public Task<bool> SendResetAsync()
        {
            return Task.FromResult(SendRaw("RESET"));
        }

        public Task<bool> SendLimitsAsync(double minValue, double maxValue)
        {
            string command = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "LIMITS;{0:F2};{1:F2}", minValue, maxValue);
            return Task.FromResult(SendRaw(command));
        }

        private bool TryWriteLine(string command)
        {
            try
            {
                _serialPort.WriteLine(command);
                FileLogger.Trace("ArduinoSerialService", "[TX] " + command);
                return true;
            }
            catch (Exception ex)
            {
                FileLogger.Error("ArduinoSerialService", "Komut gönderilemedi: " + ex.Message);
                OnErrorReceived?.Invoke("Komut gönderilemedi: " + ex.Message);
                return false;
            }
        }

        private void ReadLoop()
        {
            var readBuffer = new byte[1024];
            while (_isReading)
            {
                try
                {
                    SerialPort port = _serialPort;
                    if (port == null || !port.IsOpen)
                    {
                        Thread.Sleep(100);
                        continue;
                    }

                    int bytesRead = port.Read(readBuffer, 0, readBuffer.Length);
                    if (bytesRead > 0)
                    {
                        ProcessIncomingChunk(Encoding.ASCII.GetString(readBuffer, 0, bytesRead));
                    }
                }
                catch (TimeoutException)
                {
                    // Normal seri port bekleme süreci.
                }
                catch (Exception ex)
                {
                    if (_isReading)
                    {
                        FileLogger.Warning("ArduinoSerialService", "Okuma kesildi: " + ex.Message);
                        OnErrorReceived?.Invoke("Seri bağlantı kesildi: " + ex.Message);
                    }
                    break;
                }
            }
        }

        public void ProcessIncomingChunk(string chunk)
        {
            if (string.IsNullOrEmpty(chunk)) return;

            lock (_lineBuffer)
            {
                _lineBuffer.Append(chunk);
                string pending = _lineBuffer.ToString();
                int newlineIndex;
                while ((newlineIndex = pending.IndexOf('\n')) >= 0)
                {
                    string line = pending.Substring(0, newlineIndex).Trim('\r', '\n', ' ');
                    pending = pending.Substring(newlineIndex + 1);
                    if (!string.IsNullOrEmpty(line)) DispatchLine(line);
                }

                _lineBuffer.Clear();
                _lineBuffer.Append(pending);
            }
        }

        public void DispatchLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            FileLogger.Trace("ArduinoSerialService", "[" + DeviceTag + " RX] " + line);

            int snIdx = line.IndexOf("SN", StringComparison.OrdinalIgnoreCase);
            if (snIdx >= 0)
            {
                string payload = line.Substring(snIdx).Trim();
                TestData data = PacketParser.ParseLegacyLine(payload);
                if (data != null)
                {
                    if (!string.IsNullOrWhiteSpace(DeviceTag)) data.StationName = DeviceTag;
                    OnTestResultReceived?.Invoke(data);
                }
            }
            else if (line.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase))
            {
                OnErrorReceived?.Invoke(line);
            }
        }

        private void CloseInternal()
        {
            _isReading = false;
            try
            {
                if (_serialPort != null)
                {
                    if (_serialPort.IsOpen) _serialPort.Close();
                    _serialPort.Dispose();
                }
            }
            catch (Exception ex)
            {
                FileLogger.Error("ArduinoSerialService", "Port kapatılırken hata: " + ex.Message);
            }
            finally
            {
                _serialPort = null;
                ConnectedPortName = "";
                OnConnectionStatusChanged?.Invoke(false);
            }
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
