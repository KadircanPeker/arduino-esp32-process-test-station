using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using ProcessTestApp.Application;
using ProcessTestApp.Communication;
using ProcessTestApp.Data;
using ProcessTestApp.Domain;
using ProcessTestApp.Infrastructure;

namespace ProcessTestApp
{
    public class Form1 : Form
    {
        private static readonly Color Bg = Color.FromArgb(8, 17, 31);
        private static readonly Color Panel = Color.FromArgb(16, 29, 45);
        private static readonly Color PanelAlt = Color.FromArgb(11, 23, 40);
        private static readonly Color Border = Color.FromArgb(38, 54, 75);
        private static readonly Color TextMain = Color.FromArgb(232, 238, 247);
        private static readonly Color TextMuted = Color.FromArgb(142, 160, 184);
        private static readonly Color Blue = Color.FromArgb(56, 189, 248);
        private static readonly Color Green = Color.FromArgb(34, 197, 94);
        private static readonly Color Red = Color.FromArgb(239, 68, 68);
        private static readonly Color Amber = Color.FromArgb(245, 158, 11);

        private readonly object _dataLock = new object();
        private readonly BindingList<TestData> _visibleLogs = new BindingList<TestData>();
        private readonly List<TestData> _allLogs = new List<TestData>();
        private readonly Dictionary<string, ProductThreshold> _thresholds = new Dictionary<string, ProductThreshold>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _errorCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly System.Collections.Concurrent.ConcurrentQueue<TestData> _uiDataQueue = new System.Collections.Concurrent.ConcurrentQueue<TestData>();
        private readonly System.Windows.Forms.Timer _uiThrottlingTimer = new System.Windows.Forms.Timer();

        private readonly ArduinoSerialService _serial1 = ArduinoSerialService.Instance;
        private readonly ArduinoSerialService _serial2 = new ArduinoSerialService("ESP32");

        private IDbConnectionFactory _connectionFactory;
        private TestLogRepository _testLogs;
        private IUserRepository _users;
        private IAuditLogRepository _audit;
        private HttpWebServer _webServer;

        private ComboBox _ports1;
        private ComboBox _baud1;
        private Button _connect1;
        private Button _disconnect1;
        private Label _connectionState1;

        private ComboBox _ports2;
        private ComboBox _baud2;
        private Button _connect2;
        private Button _disconnect2;
        private Label _connectionState2;

        private NumericUpDown _minLimit;
        private NumericUpDown _maxLimit;
        private NumericUpDown _esp32RssiLimit;
        private Button _sendLimits;
        private Label _operator;
        private LinkLabel _webUrl;
        private Label _liveStatus;
        private Label _liveDetail;
        private Label _totalValue;
        private Label _passValue;
        private Label _failValue;
        private Label _yieldValue;

        private string _activeChartViewMode = "ARDUINO";
        private Button _btnShowArduino;
        private Button _btnShowEsp32;
        private Button _btnShowAll;
        private Chart _trendChart;
        private Chart _paretoChart;
        private ListView _errorList;
        private DataGridView _grid;
        private Button _userManagementButton;
        private Button _auditButton;
        private ToolStripStatusLabel _footerState;
        private string _activeDevice = "Cihaz bekleniyor";
        private string _currentWebUrl = "http://127.0.0.1:5000";
        private int _simulationCounter = 9000;
        private bool _isEstopLocked = false;

        public Form1()
        {
            InitializeComponent();
            Load += Form1_Load;
            FormClosing += Form1_FormClosing;
        }

        private void InitializeComponent()
        {
            Text = "Arduino / ESP32 Proses Test ve İzlenebilirlik İstasyonu";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1180, 760);
            ClientSize = new Size(1400, 860);
            BackColor = Bg;
            ForeColor = TextMain;
            Font = new Font("Segoe UI", 9F);
            Icon = File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_icon.ico")) ? new Icon(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_icon.ico")) : null;

            var header = new Panel { Dock = DockStyle.Top, Height = 76, BackColor = Panel, Padding = new Padding(22, 12, 22, 8) };
            var title = new Label { Text = "ARDUINO / ESP32 PROSES TEST İSTASYONU", AutoSize = true, Font = new Font("Segoe UI Semibold", 15F, FontStyle.Bold), ForeColor = TextMain, Location = new Point(22, 13) };
            var subtitle = new Label { Text = "Ölçüm · Limit değerlendirmesi · Röle güvenliği · SQL izlenebilirlik · Mobil kontrol", AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = TextMuted, Location = new Point(24, 46) };
            _operator = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Right, Width = 420, ForeColor = TextMuted, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            header.Controls.AddRange(new Control[] { title, subtitle, _operator });
            Controls.Add(header);

            var tabs = new TabControl { Dock = DockStyle.Fill, Appearance = TabAppearance.FlatButtons, ItemSize = new Size(180, 36), SizeMode = TabSizeMode.Fixed, Padding = new Point(14, 5) };
            tabs.TabPages.Add(BuildLiveTab());
            tabs.TabPages.Add(BuildErrorTab());
            tabs.TabPages.Add(BuildManagementTab());
            Controls.Add(tabs);
            tabs.BringToFront();

            var status = new StatusStrip { BackColor = PanelAlt, ForeColor = TextMuted, SizingGrip = false };
            _footerState = new ToolStripStatusLabel("Sistem başlatılıyor...") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            status.Items.Add(_footerState);
            status.Items.Add(new ToolStripStatusLabel("Yerel prototip · Yazılımsal E-Stop fiziksel emniyet rölesi değildir"));
            Controls.Add(status);
        }

        private TabPage BuildLiveTab()
        {
            var tab = CreateTab("Canlı Test");
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(6), BackColor = Bg };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            tab.Controls.Add(layout);

            var connection = CreatePanel();
            var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(8, 6, 8, 6), WrapContents = true, AutoScroll = true };
            connection.Controls.Add(flow);

            // PORT 1 (ARDUINO)
            flow.Controls.Add(CreateCaption("ARDUINO PORT"));
            _ports1 = CreateCombo(75);
            flow.Controls.Add(_ports1);
            _baud1 = CreateCombo(70);
            _baud1.Items.AddRange(new object[] { "9600", "115200" });
            _baud1.SelectedIndex = 0;
            flow.Controls.Add(_baud1);
            _connect1 = CreateButton("Bağlan", Green, Connect1_Click, 62);
            _disconnect1 = CreateButton("Ayır", Color.FromArgb(71, 85, 105), Disconnect1_Click, 48);
            _disconnect1.Enabled = false;
            flow.Controls.AddRange(new Control[] { _connect1, _disconnect1 });
            _connectionState1 = new Label { Text = "● P1 KAPALI", Width = 75, Height = 30, TextAlign = ContentAlignment.MiddleCenter, ForeColor = TextMuted, Font = new Font("Segoe UI", 7.5F, FontStyle.Bold), Margin = new Padding(2) };
            flow.Controls.Add(_connectionState1);

            flow.Controls.Add(CreateSeparator());

            // PORT 2 (ESP32)
            flow.Controls.Add(CreateCaption("ESP32 PORT"));
            _ports2 = CreateCombo(75);
            flow.Controls.Add(_ports2);
            _baud2 = CreateCombo(70);
            _baud2.Items.AddRange(new object[] { "9600", "115200" });
            _baud2.SelectedIndex = 0;
            flow.Controls.Add(_baud2);
            _connect2 = CreateButton("Bağlan", Green, Connect2_Click, 62);
            _disconnect2 = CreateButton("Ayır", Color.FromArgb(71, 85, 105), Disconnect2_Click, 48);
            _disconnect2.Enabled = false;
            flow.Controls.AddRange(new Control[] { _connect2, _disconnect2 });
            _connectionState2 = new Label { Text = "● P2 KAPALI", Width = 75, Height = 30, TextAlign = ContentAlignment.MiddleCenter, ForeColor = TextMuted, Font = new Font("Segoe UI", 7.5F, FontStyle.Bold), Margin = new Padding(2) };
            flow.Controls.Add(_connectionState2);

            flow.Controls.Add(CreateButton("Yenile", Blue, delegate { LoadPorts(); }, 60));

            // LİMİTLER & KOMUTLAR
            flow.Controls.Add(CreateSeparator());
            flow.Controls.Add(CreateCaption("ARD V-LİMİT"));
            _minLimit = CreateNumeric(1.00M);
            _maxLimit = CreateNumeric(4.50M);
            flow.Controls.AddRange(new Control[] { _minLimit, _maxLimit });

            flow.Controls.Add(CreateCaption("ESP RSSI LİMİT"));
            _esp32RssiLimit = CreateNumeric(75.00M);
            _esp32RssiLimit.Maximum = 150.00M;
            flow.Controls.Add(_esp32RssiLimit);

            _sendLimits = CreateButton("Limit Gönder", Color.FromArgb(14, 116, 144), SendLimits_Click, 88);
            flow.Controls.Add(_sendLimits);
            Button enable = CreateButton("Etkinleştir", Green, LocalCommand_Click, 72);
            enable.Tag = "START";
            Button estop = CreateButton("E-STOP", Red, LocalCommand_Click, 65);
            estop.Tag = "E_STOP";
            Button reset = CreateButton("Reset", Color.FromArgb(71, 85, 105), LocalCommand_Click, 58);
            reset.Tag = "RESET";
            flow.Controls.AddRange(new Control[] { enable, estop, reset });
            flow.Controls.Add(CreateButton("Demo Veri", Color.FromArgb(124, 58, 237), Demo_Click, 75));
            flow.Controls.Add(CreateSeparator());
            flow.Controls.Add(CreateButton("Geçmişi Yükle", Color.FromArgb(3, 105, 161), LoadHistory_Click, 92));
            flow.Controls.Add(CreateButton("Ekranı Temizle", Color.FromArgb(71, 85, 105), ClearScreen_Click, 95));
            layout.Controls.Add(connection, 0, 0);

            var summary = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, Padding = new Padding(0, 2, 0, 2), BackColor = Bg };
            summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
            for (int i = 0; i < 4; i++) summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16));
            var liveCard = CreatePanel();
            liveCard.Padding = new Padding(10, 5, 8, 4);
            _liveStatus = new Label { Text = "VERİ BEKLENİYOR", Dock = DockStyle.Top, Height = 28, Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold), ForeColor = TextMain, TextAlign = ContentAlignment.TopLeft, Padding = new Padding(0) };
            _liveDetail = new Label { Text = "Bir Arduino veya ESP32 seri portuna bağlanın.", Dock = DockStyle.Top, Height = 36, ForeColor = TextMuted, Padding = new Padding(0, 2, 0, 0), Font = new Font("Segoe UI", 8F) };
            liveCard.Controls.Add(_liveDetail);
            liveCard.Controls.Add(_liveStatus);
            summary.Controls.Add(liveCard, 0, 0);
            summary.Controls.Add(CreateKpiCard("TOPLAM TEST", out _totalValue, Blue), 1, 0);
            summary.Controls.Add(CreateKpiCard("PASS", out _passValue, Green), 2, 0);
            summary.Controls.Add(CreateKpiCard("FAIL", out _failValue, Red), 3, 0);
            summary.Controls.Add(CreateKpiCard("YIELD", out _yieldValue, Amber), 4, 0);
            layout.Controls.Add(summary, 0, 1);

            var contentSplit = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Bg, Padding = new Padding(0, 4, 0, 0) };
            contentSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            contentSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            var chartPanel = CreatePanel();
            chartPanel.Padding = new Padding(6);
            chartPanel.MinimumSize = new Size(100, 100);

            var chartToolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 36,
                BackColor = Panel,
                Padding = new Padding(2, 2, 2, 2),
                WrapContents = false
            };

            _btnShowArduino = CreateButton("⚡ Arduino (V / A)", Blue, (s, e) => SetChartViewMode("ARDUINO"), 135);
            _btnShowEsp32 = CreateButton("📶 ESP32 (RSSI / Ağ)", Color.FromArgb(30, 41, 59), (s, e) => SetChartViewMode("ESP32"), 145);
            _btnShowAll = CreateButton("📊 Tümü (Çift Eksen)", Color.FromArgb(30, 41, 59), (s, e) => SetChartViewMode("ALL"), 135);

            chartToolbar.Controls.Add(_btnShowArduino);
            chartToolbar.Controls.Add(_btnShowEsp32);
            chartToolbar.Controls.Add(_btnShowAll);

            _trendChart = CreateTrendChart();
            chartPanel.Controls.Add(_trendChart);
            chartPanel.Controls.Add(chartToolbar);
            SetChartViewMode("ARDUINO");
            contentSplit.Controls.Add(chartPanel, 0, 0);

            var gridPanel = CreatePanel();
            gridPanel.Padding = new Padding(6);
            gridPanel.MinimumSize = new Size(100, 100);
            _grid = CreateGrid();
            gridPanel.Controls.Add(_grid);
            contentSplit.Controls.Add(gridPanel, 1, 0);

            layout.Controls.Add(contentSplit, 0, 2);
            return tab;
        }

        private TabPage BuildErrorTab()
        {
            var tab = CreateTab("Hata Analizi");
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Bg, Padding = new Padding(10) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            tab.Controls.Add(layout);

            var chartPanel = CreatePanel();
            chartPanel.Padding = new Padding(10);
            chartPanel.MinimumSize = new Size(100, 100);
            _paretoChart = CreateParetoChart();
            chartPanel.Controls.Add(_paretoChart);
            layout.Controls.Add(chartPanel, 0, 0);

            var listPanel = CreatePanel();
            listPanel.Padding = new Padding(10);
            var title = new Label { Text = "HATA KODU AÇIKLAMALARI", Dock = DockStyle.Top, Height = 30, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = Blue };
            _errorList = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true, BackColor = PanelAlt, ForeColor = TextMain, BorderStyle = BorderStyle.None };
            _errorList.Columns.Add("Kod", 85);
            _errorList.Columns.Add("Açıklama", 360);
            _errorList.Columns.Add("Adet", 65);
            listPanel.Controls.Add(_errorList);
            listPanel.Controls.Add(title);
            layout.Controls.Add(listPanel, 1, 0);
            return tab;
        }

        private TabPage BuildManagementTab()
        {
            var tab = CreateTab("Raporlama ve Yönetim");
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(18), BackColor = Bg };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 250));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            tab.Controls.Add(root);

            var report = CreateGroup("RAPORLAMA VE ARŞİV");
            var reportFlow = CreateVerticalFlow();
            report.Controls.Add(reportFlow);
            reportFlow.Controls.Add(CreateWideButton("Seçili Test İçin PDF Raporu", Color.FromArgb(14, 116, 144), Pdf_Click));
            reportFlow.Controls.Add(CreateWideButton("Tüm Kayıtları CSV Olarak Dışa Aktar", Color.FromArgb(37, 99, 235), Csv_Click));
            reportFlow.Controls.Add(CreateWideButton("PDF Arşiv Klasörünü Aç", Color.FromArgb(71, 85, 105), OpenPdfArchive_Click));
            reportFlow.Controls.Add(CreateWideButton("SQL Geçmişini Yenile", Color.FromArgb(71, 85, 105), delegate { LoadHistory(); }));
            root.Controls.Add(report, 0, 0);

            var manage = CreateGroup("OPERATÖR VE DENETİM");
            var manageFlow = CreateVerticalFlow();
            manage.Controls.Add(manageFlow);
            _userManagementButton = CreateWideButton("Kullanıcı / Operatör Yönetimi", Color.FromArgb(124, 58, 237), UserManagement_Click);
            _auditButton = CreateWideButton("Giriş ve Komut Denetim Kayıtları", Color.FromArgb(14, 116, 144), Audit_Click);
            manageFlow.Controls.Add(_userManagementButton);
            manageFlow.Controls.Add(_auditButton);
            manageFlow.Controls.Add(CreateWideButton("Günlük Sistem Loglarını Aç", Color.FromArgb(71, 85, 105), OpenLogs_Click));
            root.Controls.Add(manage, 1, 0);

            var mobile = CreateGroup("MOBİL İZLEME VE KONTROL");
            var mobileInner = new Panel { Dock = DockStyle.Fill, Padding = new Padding(22) };
            _webUrl = new LinkLabel { Text = _currentWebUrl, LinkColor = Blue, ActiveLinkColor = Color.White, AutoSize = true, Font = new Font("Segoe UI", 16F, FontStyle.Bold), Location = new Point(24, 45) };
            _webUrl.LinkClicked += delegate { OpenUrl(_currentWebUrl); };
            var mobileText = new Label { Text = "Aynı Wi-Fi ağına bağlı telefondan canlı testleri izleyin. START / E-STOP / RESET komutları yalnız Administrator girişiyle kullanılabilir.\n\nApp.config içinde EnableLanMode=true olmalıdır.", Location = new Point(24, 95), Size = new Size(560, 100), ForeColor = TextMuted, Font = new Font("Segoe UI", 10F) };
            mobileInner.Controls.AddRange(new Control[] { _webUrl, mobileText });
            mobile.Controls.Add(mobileInner);
            root.SetColumnSpan(mobile, 2);
            root.Controls.Add(mobile, 0, 1);
            return tab;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            _connectionFactory = new SqlConnectionFactory(null);
            _testLogs = new TestLogRepository(_connectionFactory);
            _users = new UserRepository(_connectionFactory);
            _audit = new AuditLogRepository(_connectionFactory);
            LoadDefaultThresholds();
            LoadPorts();
            SubscribeSerialEvents();
            InitThrottlingTimer();
            ClearLiveScreen();
            StartWebServer();
            ApplyRolePermissions();
            SetStatus("HAZIR", "Temiz oturum başlatıldı. Tüm canlı ölçümler SQL veritabanına kaydedilir.", Color.FromArgb(51, 65, 85));
        }

        private void InitThrottlingTimer()
        {
            _uiThrottlingTimer.Interval = 150; // 150 ms UI sampling throttle
            _uiThrottlingTimer.Tick += UiThrottlingTimer_Tick;
            _uiThrottlingTimer.Start();
        }

        private void SubscribeSerialEvents()
        {
            _serial1.OnTestResultReceived += Serial1_OnTestResultReceived;
            _serial1.OnConnectionStatusChanged += Serial1_OnConnectionStatusChanged;
            _serial1.OnErrorReceived += Serial1_OnErrorReceived;

            _serial2.OnTestResultReceived += Serial2_OnTestResultReceived;
            _serial2.OnConnectionStatusChanged += Serial2_OnConnectionStatusChanged;
            _serial2.OnErrorReceived += Serial2_OnErrorReceived;
        }

        private void UnsubscribeSerialEvents()
        {
            _serial1.OnTestResultReceived -= Serial1_OnTestResultReceived;
            _serial1.OnConnectionStatusChanged -= Serial1_OnConnectionStatusChanged;
            _serial1.OnErrorReceived -= Serial1_OnErrorReceived;

            _serial2.OnTestResultReceived -= Serial2_OnTestResultReceived;
            _serial2.OnConnectionStatusChanged -= Serial2_OnConnectionStatusChanged;
            _serial2.OnErrorReceived -= Serial2_OnErrorReceived;
        }

        private void LoadDefaultThresholds()
        {
            _thresholds["VOLTAGE_RELAY_TESTER"] = new ProductThreshold { ProductType = "VOLTAGE_RELAY_TESTER", MinVoltage = 1.0, MaxVoltage = 4.5, MinCurrent = 0, MaxCurrent = 2.5, IpcClass = "ARDUINO" };
            _thresholds["WIFI_TESTER"] = new ProductThreshold { ProductType = "WIFI_TESTER", MinVoltage = 0, MaxVoltage = 75, MinCurrent = 0, MaxCurrent = 100, IpcClass = "ESP32" };
        }

        private void LoadPorts()
        {
            string selected1 = Convert.ToString(_ports1.SelectedItem);
            string selected2 = Convert.ToString(_ports2.SelectedItem);
            _ports1.Items.Clear();
            _ports2.Items.Clear();
            string[] ports = SerialPort.GetPortNames().OrderBy(x => x).ToArray();
            _ports1.Items.AddRange(ports);
            _ports2.Items.AddRange(ports);

            if (!string.IsNullOrEmpty(selected1) && _ports1.Items.Contains(selected1)) _ports1.SelectedItem = selected1;
            else if (_ports1.Items.Count > 0) _ports1.SelectedIndex = 0;

            if (!string.IsNullOrEmpty(selected2) && _ports2.Items.Contains(selected2)) _ports2.SelectedItem = selected2;
            else if (_ports2.Items.Count > 1) _ports2.SelectedIndex = 1;
            else if (_ports2.Items.Count > 0) _ports2.SelectedIndex = 0;

            _footerState.Text = ports.Length == 0 ? "COM port bulunamadı; Demo Veri ile arayüz test edilebilir." : ports.Length + " COM port bulundu.";
        }

        private void Connect1_Click(object sender, EventArgs e)
        {
            if (_ports1.SelectedItem == null)
            {
                MessageBox.Show("Önce Arduino için bir COM port seçin.", "Bağlantı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            int baudRate;
            if (!int.TryParse(Convert.ToString(_baud1.SelectedItem), out baudRate)) baudRate = 9600;
            string port = Convert.ToString(_ports1.SelectedItem);
            if (_serial1.Connect(port, baudRate))
            {
                _activeDevice = "Arduino (" + port + " @ " + baudRate + ")";
                _serial1.SendLimitsAsync((double)_minLimit.Value, (double)_maxLimit.Value);
                WriteAudit("SERIAL_CONNECT_P1", _activeDevice);
                SetStatus("ARDUINO BAĞLANDI", _activeDevice + " üzerinden telemetri bekleniyor.", Color.FromArgb(20, 83, 45));
            }
            else
            {
                MessageBox.Show("Arduino seri portu (P1) açılamadı. Portun açık olmadığını kontrol edin.", "Bağlantı Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Disconnect1_Click(object sender, EventArgs e)
        {
            _serial1.Disconnect();
            WriteAudit("SERIAL_DISCONNECT_P1", "Arduino kapandı");
        }

        private void Connect2_Click(object sender, EventArgs e)
        {
            if (_ports2.SelectedItem == null)
            {
                MessageBox.Show("Önce ESP32 için bir COM port seçin.", "Bağlantı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            int baudRate;
            if (!int.TryParse(Convert.ToString(_baud2.SelectedItem), out baudRate)) baudRate = 9600;
            string port = Convert.ToString(_ports2.SelectedItem);
            if (_serial2.Connect(port, baudRate))
            {
                string dev = "ESP32 (" + port + " @ " + baudRate + ")";
                WriteAudit("SERIAL_CONNECT_P2", dev);
                SetStatus("ESP32 BAĞLANDI", dev + " üzerinden telemetri bekleniyor.", Color.FromArgb(20, 83, 45));
            }
            else
            {
                MessageBox.Show("ESP32 seri portu (P2) açılamadı. Portun açık olmadığını kontrol edin.", "Bağlantı Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Disconnect2_Click(object sender, EventArgs e)
        {
            _serial2.Disconnect();
            WriteAudit("SERIAL_DISCONNECT_P2", "ESP32 kapandı");
        }

        private async void SendLimits_Click(object sender, EventArgs e)
        {
            if (_minLimit.Value >= _maxLimit.Value)
            {
                MessageBox.Show("Alt limit, üst limitten küçük olmalıdır.", "Limit Hatası", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            bool p1Sent = _serial1.IsConnected && await _serial1.SendLimitsAsync((double)_minLimit.Value, (double)_maxLimit.Value);
            bool p2Sent = _serial2.IsConnected && await _serial2.SendLimitsAsync(0, (double)_esp32RssiLimit.Value);
            _thresholds["VOLTAGE_RELAY_TESTER"].MinVoltage = (double)_minLimit.Value;
            _thresholds["VOLTAGE_RELAY_TESTER"].MaxVoltage = (double)_maxLimit.Value;
            _thresholds["WIFI_TESTER"].MaxVoltage = (double)_esp32RssiLimit.Value;
            WriteAudit("LIMITS_SENT", "Arduino: " + _minLimit.Value + "-" + _maxLimit.Value + "V | ESP32: " + _esp32RssiLimit.Value + "dBm");
            _footerState.Text = "Limitler kaydedildi (Arduino & ESP32).";
        }

        private void Demo_Click(object sender, EventArgs e)
        {
            _simulationCounter++;
            var random = new Random();
            bool wifi = _simulationCounter % 3 == 0;
            TestData demo;
            if (wifi)
            {
                double rssi = random.Next(48, 89);
                demo = new TestData { SerialNumber = "SN" + _simulationCounter, ProductType = "WIFI_TESTER", Voltage = rssi, Current = random.Next(1, 15), Result = rssi <= (double)_esp32RssiLimit.Value ? "PASS" : "FAIL", ErrorCode = rssi <= (double)_esp32RssiLimit.Value ? "E00" : "E05", LogTime = DateTime.Now, SourceType = "SIMULATION" };
            }
            else
            {
                double voltage = Math.Round(random.NextDouble() * 5.0, 2);
                string result = voltage >= (double)_minLimit.Value && voltage <= (double)_maxLimit.Value ? "PASS" : "FAIL";
                demo = new TestData { SerialNumber = "SN" + _simulationCounter, ProductType = "VOLTAGE_RELAY_TESTER", Voltage = voltage, Current = Math.Round(voltage / 2.0, 2), Result = result, ErrorCode = result == "PASS" ? "E00" : voltage < (double)_minLimit.Value ? "E01" : "E02", LogTime = DateTime.Now, SourceType = "SIMULATION" };
            }
            ProcessTest(demo, false);
        }

        private void Serial1_OnTestResultReceived(TestData data)
        {
            if (data != null) { data.StationName = "ARDUINO"; ProcessTest(data, true); }
        }

        private void Serial2_OnTestResultReceived(TestData data)
        {
            if (data != null) { data.StationName = "ESP32"; ProcessTest(data, true); }
        }

        private void Serial1_OnConnectionStatusChanged(bool connected)
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired) { BeginInvoke(new Action<bool>(Serial1_OnConnectionStatusChanged), connected); return; }
            _connect1.Enabled = !connected;
            _disconnect1.Enabled = connected;
            _ports1.Enabled = !connected;
            _baud1.Enabled = !connected;
            _connectionState1.Text = connected ? "● P1 BAĞLI" : "● P1 KAPALI";
            _connectionState1.ForeColor = connected ? Green : TextMuted;
        }

        private void Serial2_OnConnectionStatusChanged(bool connected)
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired) { BeginInvoke(new Action<bool>(Serial2_OnConnectionStatusChanged), connected); return; }
            _connect2.Enabled = !connected;
            _disconnect2.Enabled = connected;
            _ports2.Enabled = !connected;
            _baud2.Enabled = !connected;
            _connectionState2.Text = connected ? "● P2 BAĞLI" : "● P2 KAPALI";
            _connectionState2.ForeColor = connected ? Green : TextMuted;
        }

        private void Serial1_OnErrorReceived(string error)
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired) { BeginInvoke(new Action<string>(Serial1_OnErrorReceived), error); return; }
            _footerState.Text = "Arduino haberleşme hatası: " + error;
        }

        private void Serial2_OnErrorReceived(string error)
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired) { BeginInvoke(new Action<string>(Serial2_OnErrorReceived), error); return; }
            _footerState.Text = "ESP32 haberleşme hatası: " + error;
        }

        private void ProcessTest(TestData data, bool persist)
        {
            if (data == null) return;
            bool wifi = string.Equals(data.ProductType, "WIFI_TESTER", StringComparison.OrdinalIgnoreCase) || string.Equals(data.StationName, "ESP32", StringComparison.OrdinalIgnoreCase);
            data.LogTime = data.LogTime == default(DateTime) ? DateTime.Now : data.LogTime;
            data.SourceType = data.SourceType == "SIMULATION" ? "SIMULATION" : wifi ? "ESP32" : "ARDUINO";
            data.StationName = wifi ? "ESP32" : "ARDUINO";
            data.OperatorName = SessionManager.LoggedInUserFullName ?? "Bilinmeyen Operatör";
            data.BatchNo = "BATCH-" + data.LogTime.ToString("yyyyMMdd");
            data.TestAttemptNo = data.TestAttemptNo < 1 ? 1 : data.TestAttemptNo;

            if (wifi)
            {
                data.MinLimit = 0;
                data.MaxLimit = (double)_esp32RssiLimit.Value;
                if (data.Current <= 0 || data.Voltage > data.MaxLimit) { data.Result = "FAIL"; data.ErrorCode = "E05"; }
                else if (!string.Equals(data.Result, "FAIL", StringComparison.OrdinalIgnoreCase)) { data.Result = "PASS"; data.ErrorCode = "E00"; }
            }
            else
            {
                double min = (double)_minLimit.Value;
                double max = (double)_maxLimit.Value;
                data.MinLimit = min;
                data.MaxLimit = max;
                if (!string.Equals(data.ErrorCode, "E99", StringComparison.OrdinalIgnoreCase))
                {
                    if (data.Voltage < min) { data.Result = "FAIL"; data.ErrorCode = "E01"; }
                    else if (data.Voltage > max) { data.Result = "FAIL"; data.ErrorCode = "E02"; }
                    else { data.Result = "PASS"; data.ErrorCode = "E00"; }
                }
            }

            if (persist && _testLogs != null)
            {
                ThreadPool.QueueUserWorkItem(delegate
                {
                    if (!_testLogs.Add(data)) FileLogger.Warning("Form1", "Ölçüm arayüzde gösterildi ancak SQL'e kaydedilemedi: " + data.SerialNumber);
                });
            }

            _uiDataQueue.Enqueue(data);
        }

        private void UiThrottlingTimer_Tick(object sender, EventArgs e)
        {
            if (IsDisposed || Disposing || _uiDataQueue.IsEmpty) return;

            TestData lastItem = null;
            while (_uiDataQueue.TryDequeue(out TestData data))
            {
                lastItem = data;
                lock (_dataLock)
                {
                    _allLogs.Insert(0, data);
                    if (_allLogs.Count > 2000) _allLogs.RemoveAt(_allLogs.Count - 1);
                }
                _visibleLogs.Insert(0, data);
                if (_visibleLogs.Count > 500) _visibleLogs.RemoveAt(_visibleLogs.Count - 1);
            }

            if (lastItem != null)
            {
                UpdateDashboard(lastItem);
            }
        }

        private void UpdateDashboard(TestData last)
        {
            RecalculateCounters();
            AddTrendPoint(last);
            UpdateErrorViews();
            _grid.Refresh();
            _grid.ClearSelection();
            if (_grid.Rows.Count > 0)
            {
                _grid.Rows[0].Selected = true;
                try { _grid.FirstDisplayedScrollingRowIndex = 0; } catch { }
            }

            bool wifi = IsWifi(last);
            string primary = wifi ? (-Math.Abs(last.Voltage)).ToString("F0") + " dBm" : last.Voltage.ToString("F2") + " V";
            string secondary = wifi ? last.Current.ToString("F0") + " ağ" : last.Current.ToString("F2") + " A";
            string detail = last.SerialNumber + " · " + primary + " · " + secondary + " · " + last.ErrorCode + " / " + ErrorDescription(last.ErrorCode);
            SetStatus(last.Result + " · " + last.SerialNumber, detail, last.Result == "PASS" ? Color.FromArgb(20, 83, 45) : Color.FromArgb(127, 29, 29));
        }

        private void RecalculateCounters()
        {
            List<TestData> snapshot;
            lock (_dataLock) snapshot = _allLogs.ToList();
            int total = snapshot.Count;
            int pass = snapshot.Count(x => string.Equals(x.Result, "PASS", StringComparison.OrdinalIgnoreCase));
            int fail = total - pass;
            double yieldRate = total == 0 ? 0 : 100.0 * pass / total;
            _totalValue.Text = total.ToString(CultureInfo.InvariantCulture);
            _passValue.Text = pass.ToString(CultureInfo.InvariantCulture);
            _failValue.Text = fail.ToString(CultureInfo.InvariantCulture);
            _yieldValue.Text = "% " + yieldRate.ToString("F1", CultureInfo.InvariantCulture);
        }

        private void SetChartViewMode(string mode)
        {
            _activeChartViewMode = mode;
            if (_btnShowArduino != null && _btnShowEsp32 != null && _btnShowAll != null)
            {
                _btnShowArduino.BackColor = mode == "ARDUINO" ? Blue : Color.FromArgb(30, 41, 59);
                _btnShowEsp32.BackColor = mode == "ESP32" ? Green : Color.FromArgb(30, 41, 59);
                _btnShowAll.BackColor = mode == "ALL" ? Color.FromArgb(124, 58, 237) : Color.FromArgb(30, 41, 59);
            }
            UpdateChartAxisAndVisibility();
        }

        private void UpdateChartAxisAndVisibility()
        {
            if (_trendChart == null || _trendChart.ChartAreas.Count == 0) return;
            ChartArea area = _trendChart.ChartAreas["Live"];

            if (_activeChartViewMode == "ARDUINO")
            {
                _trendChart.Series["Arduino Gerilim (V)"].Enabled = true;
                _trendChart.Series["Arduino Akım (A)"].Enabled = true;
                _trendChart.Series["ESP32 RSSI (dBm)"].Enabled = false;
                _trendChart.Series["ESP32 Ağlar"].Enabled = false;

                _trendChart.Series["Arduino Gerilim (V)"].YAxisType = AxisType.Primary;
                _trendChart.Series["Arduino Akım (A)"].YAxisType = AxisType.Primary;

                area.AxisY2.Enabled = AxisEnabled.False;
                area.AxisY.Minimum = 0;
                area.AxisY.Maximum = 5.5;
                area.AxisY.Title = "Gerilim (V) / Akım (A)";
                area.AxisY.TitleForeColor = Blue;

                if (_trendChart.Titles.Count > 0) _trendChart.Titles[0].Text = "⚡ Arduino Canlı İzleme Grafiği (Gerilim & Akım)";
            }
            else if (_activeChartViewMode == "ESP32")
            {
                _trendChart.Series["Arduino Gerilim (V)"].Enabled = false;
                _trendChart.Series["Arduino Akım (A)"].Enabled = false;
                _trendChart.Series["ESP32 RSSI (dBm)"].Enabled = true;
                _trendChart.Series["ESP32 Ağlar"].Enabled = true;

                _trendChart.Series["ESP32 RSSI (dBm)"].YAxisType = AxisType.Primary;
                _trendChart.Series["ESP32 Ağlar"].YAxisType = AxisType.Primary;

                area.AxisY2.Enabled = AxisEnabled.False;
                area.AxisY.Minimum = 0;
                area.AxisY.Maximum = 100;
                area.AxisY.Title = "RSSI Sinyal (dBm) / Ağ Sayısı";
                area.AxisY.TitleForeColor = Green;

                if (_trendChart.Titles.Count > 0) _trendChart.Titles[0].Text = "📶 ESP32 Wi-Fi Canlı İzleme Grafiği (RSSI & Ağlar)";
            }
            else // ALL
            {
                _trendChart.Series["Arduino Gerilim (V)"].Enabled = true;
                _trendChart.Series["Arduino Akım (A)"].Enabled = true;
                _trendChart.Series["ESP32 RSSI (dBm)"].Enabled = true;
                _trendChart.Series["ESP32 Ağlar"].Enabled = true;

                _trendChart.Series["Arduino Gerilim (V)"].YAxisType = AxisType.Primary;
                _trendChart.Series["Arduino Akım (A)"].YAxisType = AxisType.Primary;
                _trendChart.Series["ESP32 RSSI (dBm)"].YAxisType = AxisType.Secondary;
                _trendChart.Series["ESP32 Ağlar"].YAxisType = AxisType.Secondary;

                area.AxisY.Minimum = 0;
                area.AxisY.Maximum = 5.5;
                area.AxisY.Title = "Arduino (V / A)";
                area.AxisY.TitleForeColor = Blue;

                area.AxisY2.Enabled = AxisEnabled.True;
                area.AxisY2.Minimum = 0;
                area.AxisY2.Maximum = 100;
                area.AxisY2.Title = "ESP32 (dBm / Ağ)";
                area.AxisY2.TitleForeColor = Green;

                if (_trendChart.Titles.Count > 0) _trendChart.Titles[0].Text = "📊 Arduino & ESP32 Birleşik İzleme (Çift Eksen)";
            }
        }

        private void AddTrendPoint(TestData data)
        {
            string time = data.LogTime.ToString("HH:mm:ss");
            bool wifi = IsWifi(data);
            if (wifi)
            {
                _trendChart.Series[2].Points.AddXY(time, Math.Abs(data.Voltage));
                _trendChart.Series[3].Points.AddXY(time, data.Current);
            }
            else
            {
                _trendChart.Series[0].Points.AddXY(time, data.Voltage);
                _trendChart.Series[1].Points.AddXY(time, data.Current);
            }
            foreach (Series series in _trendChart.Series)
                while (series.Points.Count > 35) series.Points.RemoveAt(0);
        }

        private void UpdateErrorViews()
        {
            _errorCounts.Clear();
            List<TestData> snapshot;
            lock (_dataLock) snapshot = _allLogs.ToList();
            foreach (TestData item in snapshot.Where(x => !string.Equals(x.Result, "PASS", StringComparison.OrdinalIgnoreCase)))
            {
                string code = string.IsNullOrWhiteSpace(item.ErrorCode) ? "FORMAT_ERR" : item.ErrorCode;
                _errorCounts[code] = _errorCounts.ContainsKey(code) ? _errorCounts[code] + 1 : 1;
            }

            string[] codes = { "E01", "E02", "E05", "E99", "FORMAT_ERR", "SQL_ERR" };
            _errorList.Items.Clear();
            foreach (string code in codes)
            {
                int count = _errorCounts.ContainsKey(code) ? _errorCounts[code] : 0;
                _errorList.Items.Add(new ListViewItem(new[] { code, ErrorDescription(code), count.ToString() }));
            }

            Series series = _paretoChart.Series[0];
            series.Points.Clear();
            foreach (var pair in _errorCounts.OrderByDescending(x => x.Value)) series.Points.AddXY(pair.Key, pair.Value);
            if (series.Points.Count == 0) series.Points.AddXY("Hata yok", 0);
        }

        private void LoadHistory_Click(object sender, EventArgs e)
        {
            LoadHistory();
        }

        private void ClearScreen_Click(object sender, EventArgs e)
        {
            ClearLiveScreen();
        }

        private void ClearLiveScreen()
        {
            lock (_dataLock)
            {
                _allLogs.Clear();
            }
            _visibleLogs.RaiseListChangedEvents = false;
            _visibleLogs.Clear();
            _visibleLogs.RaiseListChangedEvents = true;
            _visibleLogs.ResetBindings();

            if (_trendChart != null)
            {
                foreach (Series series in _trendChart.Series) series.Points.Clear();
            }
            RecalculateCounters();
            UpdateErrorViews();
            if (_grid != null) _grid.ClearSelection();
            if (_footerState != null) _footerState.Text = "Canlı ekran temizlendi (Tüm geçmiş ölçümler SQL veritabanında saklanır).";
        }

        private void LoadHistory()
        {
            if (_testLogs == null) return;
            List<TestData> history = _testLogs.GetRecent(500);
            lock (_dataLock)
            {
                _allLogs.Clear();
                _allLogs.AddRange(history);
            }
            _visibleLogs.RaiseListChangedEvents = false;
            _visibleLogs.Clear();
            foreach (var item in history) _visibleLogs.Add(item);
            _visibleLogs.RaiseListChangedEvents = true;
            _visibleLogs.ResetBindings();
            _grid.ClearSelection();
            if (_grid.Rows.Count > 0)
            {
                _grid.Rows[0].Selected = true;
                try { _grid.FirstDisplayedScrollingRowIndex = 0; } catch { }
            }
            RebuildChartsFromHistory(history);
            RecalculateCounters();
            UpdateErrorViews();
            _footerState.Text = history.Count + " test kaydı SQL geçmişinden yüklendi.";
        }

        private void RebuildChartsFromHistory(List<TestData> history)
        {
            foreach (Series series in _trendChart.Series) series.Points.Clear();
            foreach (TestData item in history.Take(35).Reverse())
            {
                AddTrendPoint(item);
            }
        }

        private void LocalCommand_Click(object sender, EventArgs e)
        {
            string command = Convert.ToString(((Button)sender).Tag);
            ExecuteCommand(command, true);
        }

        private bool ExecuteCommand(string command, bool showMessage)
        {
            bool ok = true;
            if (command == "E_STOP")
            {
                _isEstopLocked = true;
                bool p1Ok = _serial1.IsConnected && _serial1.SendRaw("E_STOP");
                bool p2Ok = _serial2.IsConnected && _serial2.SendRaw("E_STOP");
                SetStatus("ACİL STOP KİLİTLENDİ", "Sistem acil durdurma durumuna alındı.", Color.FromArgb(127, 29, 29));
            }
            else if (command == "RESET")
            {
                _isEstopLocked = false;
                if (_serial1.IsConnected)
                {
                    _serial1.SendRaw("RESET");
                    _serial1.SendRaw(string.Format(CultureInfo.InvariantCulture, "LIMITS;{0:F2};{1:F2}", (double)_minLimit.Value, (double)_maxLimit.Value));
                }
                if (_serial2.IsConnected)
                {
                    _serial2.SendRaw("RESET");
                    _serial2.SendRaw(string.Format(CultureInfo.InvariantCulture, "LIMITS;{0:F2};{1:F2}", (double)_minLimit.Value, (double)_maxLimit.Value));
                }
                SetStatus("SİSTEM RESETLENDİ / ETKİN", "Sistem sıfırlandı, yeni ölçümlere hazır.", Color.FromArgb(20, 83, 45));
            }
            else // START
            {
                _isEstopLocked = false;
                if (_serial1.IsConnected)
                {
                    _serial1.SendRaw("RESET");
                    _serial1.SendRaw(string.Format(CultureInfo.InvariantCulture, "LIMITS;{0:F2};{1:F2}", (double)_minLimit.Value, (double)_maxLimit.Value));
                }
                if (_serial2.IsConnected)
                {
                    _serial2.SendRaw("RESET");
                    _serial2.SendRaw(string.Format(CultureInfo.InvariantCulture, "LIMITS;{0:F2};{1:F2}", (double)_minLimit.Value, (double)_maxLimit.Value));
                }
                SetStatus("SİSTEM ETKİN", "Sistem aktif çalışıyor.", Color.FromArgb(20, 83, 45));
            }

            if (showMessage) WriteAudit("LOCAL_" + command, ok ? "SUCCESS" : "FAILED");
            return ok;
        }

        private void StartWebServer()
        {
            try
            {
                _webServer = new HttpWebServer(5000, DashboardHtmlBuilder.GetDashboardHtml, GetStatsJson, HandleWebCommand, _connectionFactory);
                _webServer.OnServerStarted += delegate(string ip)
                {
                    Action update = delegate
                    {
                        _currentWebUrl = "http://" + ip + ":5000";
                        _webUrl.Text = _currentWebUrl;
                        _footerState.Text = "Mobil panel aktif: " + _currentWebUrl;
                    };
                    if (InvokeRequired) BeginInvoke(update); else update();
                };
                _webServer.OnServerError += delegate(string error) { FileLogger.Error("Form1", "Mobil panel: " + error); };
                _webServer.Start();
            }
            catch (Exception ex)
            {
                _footerState.Text = "Mobil panel başlatılamadı: " + ex.Message;
            }
        }

        private bool HandleWebCommand(string command)
        {
            if (IsDisposed || Disposing) return false;
            if (InvokeRequired)
            {
                return (bool)Invoke(new Func<string, bool>(HandleWebCommand), command);
            }
            return ExecuteCommand(command, false);
        }

        private string GetStatsJson()
        {
            List<TestData> snapshot;
            lock (_dataLock) snapshot = _allLogs.ToList();
            int total = snapshot.Count;
            int pass = snapshot.Count(x => string.Equals(x.Result, "PASS", StringComparison.OrdinalIgnoreCase));
            int fail = total - pass;
            double yieldRate = total == 0 ? 0 : 100.0 * pass / total;
            TestData last = snapshot.FirstOrDefault();

            var recent = new StringBuilder("[");
            foreach (TestData item in snapshot.Take(20))
            {
                if (recent.Length > 1) recent.Append(',');
                recent.Append(TestJson(item));
            }
            recent.Append(']');

            var errors = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (TestData item in snapshot.Where(x => !string.Equals(x.Result, "PASS", StringComparison.OrdinalIgnoreCase)))
            {
                string code = string.IsNullOrWhiteSpace(item.ErrorCode) ? "FORMAT_ERR" : item.ErrorCode;
                errors[code] = errors.ContainsKey(code) ? errors[code] + 1 : 1;
            }
            var errorJson = new StringBuilder("[");
            foreach (var pair in errors.OrderByDescending(x => x.Value))
            {
                if (errorJson.Length > 1) errorJson.Append(',');
                errorJson.Append("{\"code\":\"").Append(Json(pair.Key)).Append("\",\"description\":\"").Append(Json(ErrorDescription(pair.Key))).Append("\",\"count\":").Append(pair.Value).Append('}');
            }
            errorJson.Append(']');

            bool deviceConnected = (_serial1 != null && _serial1.IsConnected) || (_serial2 != null && _serial2.IsConnected);
            string deviceNames = GetConnectedDeviceNames();

            return string.Format(CultureInfo.InvariantCulture,
                "{{\"total\":{0},\"pass\":{1},\"fail\":{2},\"yield\":{3:F1},\"serverTime\":\"{4:yyyy-MM-dd HH:mm:ss}\",\"deviceConnected\":{5},\"deviceName\":\"{6}\",\"lastTest\":{7},\"recentTests\":{8},\"errorSummary\":{9}}}",
                total, pass, fail, yieldRate, DateTime.Now, deviceConnected ? "true" : "false", Json(deviceNames), last == null ? "null" : TestJson(last), recent, errorJson);
        }

        private string GetConnectedDeviceNames()
        {
            bool p1 = _serial1 != null && _serial1.IsConnected;
            bool p2 = _serial2 != null && _serial2.IsConnected;
            if (p1 && p2) return "Arduino (" + _serial1.ConnectedPortName + ") & ESP32 (" + _serial2.ConnectedPortName + ")";
            if (p1) return "Arduino (" + _serial1.ConnectedPortName + ")";
            if (p2) return "ESP32 (" + _serial2.ConnectedPortName + ")";
            return "Cihaz bağlı değil";
        }

        private string TestJson(TestData item)
        {
            bool wifi = IsWifi(item);
            string primaryLabel = wifi ? "En güçlü Wi-Fi sinyali" : "Potansiyometre gerilimi";
            string secondaryLabel = wifi ? "Bulunan ağ sayısı" : "Hesaplanan akım";
            string primaryDisplay = wifi ? (-Math.Abs(item.Voltage)).ToString("F0", CultureInfo.InvariantCulture) + " dBm" : item.Voltage.ToString("F2", CultureInfo.InvariantCulture) + " V";
            string secondaryDisplay = wifi ? item.Current.ToString("F0", CultureInfo.InvariantCulture) + " ağ" : item.Current.ToString("F2", CultureInfo.InvariantCulture) + " A";
            return "{\"serial\":\"" + Json(item.SerialNumber) + "\",\"product\":\"" + Json(item.ProductType) + "\",\"primaryLabel\":\"" + Json(primaryLabel) + "\",\"secondaryLabel\":\"" + Json(secondaryLabel) + "\",\"primaryDisplay\":\"" + Json(primaryDisplay) + "\",\"secondaryDisplay\":\"" + Json(secondaryDisplay) + "\",\"result\":\"" + Json(item.Result) + "\",\"errorCode\":\"" + Json(item.ErrorCode) + "\",\"errorDescription\":\"" + Json(ErrorDescription(item.ErrorCode)) + "\",\"time\":\"" + item.LogTime.ToString("yyyy-MM-dd HH:mm:ss") + "\"}";
        }

        private void Pdf_Click(object sender, EventArgs e)
        {
            TestData selected = SelectedTest();
            if (selected == null)
            {
                MessageBox.Show("Önce Canlı Test sekmesinden bir kayıt seçin.", "PDF Raporu", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (var dialog = new SaveFileDialog { Filter = "PDF Dosyası (*.pdf)|*.pdf", FileName = "TestReport_" + selected.SerialNumber + ".pdf" })
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                string error;
                var report = new ReportService(ErrorDescription, _thresholds);
                if (report.GeneratePdfReport(selected, dialog.FileName, _currentWebUrl, out error))
                {
                    WriteAudit("PDF_REPORT", selected.SerialNumber);
                    OpenUrl(dialog.FileName);
                }
                else MessageBox.Show(error, "Rapor Oluşturulamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void Csv_Click(object sender, EventArgs e)
        {
            using (var dialog = new SaveFileDialog { Filter = "CSV Dosyası (*.csv)|*.csv", FileName = "ProcessTestLogs_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".csv" })
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                List<TestData> snapshot;
                lock (_dataLock) snapshot = _allLogs.ToList();
                string error;
                if (new ReportService(ErrorDescription, _thresholds).ExportToCsv(dialog.FileName, snapshot, out error))
                {
                    WriteAudit("CSV_EXPORT", snapshot.Count + " kayıt");
                    MessageBox.Show("CSV dışa aktarma tamamlandı.", "Raporlama", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else MessageBox.Show(error, "CSV Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UserManagement_Click(object sender, EventArgs e)
        {
            using (var form = new UserManagementForm(_users)) form.ShowDialog(this);
        }

        private void Audit_Click(object sender, EventArgs e)
        {
            using (var form = new AuditLogForm()) form.ShowDialog(this);
        }

        private void OpenPdfArchive_Click(object sender, EventArgs e)
        {
            string directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PDF_Reports");
            Directory.CreateDirectory(directory);
            Process.Start("explorer.exe", directory);
        }

        private void OpenLogs_Click(object sender, EventArgs e)
        {
            string directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            Directory.CreateDirectory(directory);
            Process.Start("explorer.exe", directory);
        }

        private void ApplyRolePermissions()
        {
            string role = RoleNames.NormalizeRoleName(SessionManager.LoggedInUserRole) ?? RoleNames.Operator;
            _operator.Text = (SessionManager.LoggedInUserFullName ?? "Bilinmeyen Kullanıcı") + "  ·  " + role;
            bool admin = role == RoleNames.Administrator;
            bool engineer = admin || role == RoleNames.ProcessEngineer;
            bool auditAccess = engineer || role == RoleNames.QualityEngineer;
            _userManagementButton.Enabled = admin;
            _auditButton.Enabled = auditAccess;
            _minLimit.Enabled = engineer;
            _maxLimit.Enabled = engineer;
            _sendLimits.Enabled = engineer;
        }

        private void WriteAudit(string action, string description)
        {
            if (_audit == null) return;
            _audit.Add(new AuditLog(SessionManager.LoggedInUsername ?? "system", action, description ?? "", null, null));
        }

        private void SetStatus(string title, string detail, Color color)
        {
            _liveStatus.Text = title;
            _liveDetail.Text = detail;
            _liveStatus.Parent.BackColor = color;
        }

        private TestData SelectedTest()
        {
            return _grid.CurrentRow == null ? null : _grid.CurrentRow.DataBoundItem as TestData;
        }

        private static bool IsWifi(TestData data)
        {
            return data != null && string.Equals(data.ProductType, "WIFI_TESTER", StringComparison.OrdinalIgnoreCase);
        }

        private static string ErrorDescription(string code)
        {
            switch ((code ?? "").ToUpperInvariant())
            {
                case "E00": return "Test başarılı / hata yok";
                case "E01": return "Alt gerilim limitinin altında";
                case "E02": return "Üst gerilim limitinin üzerinde";
                case "E05": return "Wi-Fi ağı bulunamadı veya sinyal -75 dBm sınırından zayıf";
                case "E99": return "Yazılımsal acil durdurma kilidi aktif";
                case "FORMAT_ERR": return "Seri veri biçimi beklenen altı alanla eşleşmedi";
                case "SQL_ERR": return "Test sonucu SQL veritabanına kaydedilemedi";
                default: return "Tanımsız hata kodu";
            }
        }

        private static string Json(string value)
        {
            return (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n");
        }

        private static void OpenUrl(string target)
        {
            try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
            catch { }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            try { if (_uiThrottlingTimer != null) _uiThrottlingTimer.Stop(); } catch { }
            try
            {
                if (_serial1.IsConnected) _serial1.SendEmergencyStopAsync().Wait(500);
                if (_serial2.IsConnected) _serial2.SendEmergencyStopAsync().Wait(500);
            }
            catch { }
            UnsubscribeSerialEvents();
            try { _serial1.Disconnect(); } catch { }
            try { _serial2.Disconnect(); } catch { }
            try { if (_webServer != null) _webServer.Stop(); } catch { }
            SessionManager.ClearSession();
        }

        private TabPage CreateTab(string title)
        {
            return new TabPage(title) { BackColor = Bg, ForeColor = TextMain, Padding = new Padding(4) };
        }

        private Panel CreatePanel()
        {
            return new Panel { Dock = DockStyle.Fill, BackColor = Panel, Margin = new Padding(5), BorderStyle = BorderStyle.FixedSingle };
        }

        private GroupBox CreateGroup(string text)
        {
            return new GroupBox { Text = text, Dock = DockStyle.Fill, ForeColor = Blue, BackColor = Panel, Font = new Font("Segoe UI", 10F, FontStyle.Bold), Margin = new Padding(7), Padding = new Padding(14) };
        }

        private FlowLayoutPanel CreateVerticalFlow()
        {
            return new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(8, 12, 8, 8) };
        }

        private Label CreateCaption(string text)
        {
            return new Label { Text = text, AutoSize = true, Height = 30, TextAlign = ContentAlignment.MiddleCenter, ForeColor = TextMuted, Font = new Font("Segoe UI", 7.5F, FontStyle.Bold), Margin = new Padding(4, 5, 2, 2) };
        }

        private Label CreateSeparator()
        {
            return new Label { Text = "│", Width = 12, Height = 30, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Border, Margin = new Padding(2, 2, 2, 2) };
        }

        private ComboBox CreateCombo(int width)
        {
            return new ComboBox { Width = width, Height = 28, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = PanelAlt, ForeColor = TextMain, FlatStyle = FlatStyle.Flat, Margin = new Padding(2, 3, 4, 2) };
        }

        private NumericUpDown CreateNumeric(decimal value)
        {
            return new NumericUpDown { Width = 56, DecimalPlaces = 2, Increment = 0.10M, Minimum = 0, Maximum = 99, Value = value, BackColor = PanelAlt, ForeColor = TextMain, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(2, 4, 2, 2) };
        }

        private Button CreateButton(string text, Color color, EventHandler handler, int width)
        {
            var button = new Button { Text = text, Width = width, Height = 30, BackColor = color, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Margin = new Padding(2, 2, 2, 2), Font = new Font("Segoe UI", 8F, FontStyle.Bold), Cursor = Cursors.Hand };
            button.FlatAppearance.BorderSize = 0;
            button.Click += handler;
            return button;
        }

        private Button CreateWideButton(string text, Color color, EventHandler handler)
        {
            var button = CreateButton(text, color, handler, 420);
            button.Height = 38;
            button.Margin = new Padding(5, 5, 5, 4);
            return button;
        }

        private Panel CreateKpiCard(string title, out Label value, Color accent)
        {
            var panel = CreatePanel();
            panel.Padding = new Padding(10, 5, 8, 4);
            var caption = new Label { Text = title, Dock = DockStyle.Top, Height = 18, ForeColor = TextMuted, Font = new Font("Segoe UI", 7.5F, FontStyle.Bold), Margin = new Padding(0) };
            value = new Label { Text = "0", Dock = DockStyle.Top, Height = 45, ForeColor = accent, Font = new Font("Segoe UI Semibold", 19F, FontStyle.Bold), TextAlign = ContentAlignment.TopLeft, Margin = new Padding(0) };
            panel.Controls.Add(value);
            panel.Controls.Add(caption);
            return panel;
        }

        private Chart CreateTrendChart()
        {
            var chart = new Chart { Dock = DockStyle.Fill, BackColor = Panel, Palette = ChartColorPalette.None, MinimumSize = new Size(100, 100) };
            var area = new ChartArea("Live") { BackColor = PanelAlt };
            area.Position = new ElementPosition(2, 10, 96, 82);
            area.InnerPlotPosition = new ElementPosition(8, 5, 84, 85);
            
            // Sol Eksen (Primary Y-Axis) -> Arduino Gerilim (V) & Akım (A) (0 - 5.5 Scale)
            area.AxisX.LabelStyle.ForeColor = TextMuted;
            area.AxisY.LabelStyle.ForeColor = TextMuted;
            area.AxisX.MajorGrid.LineColor = Border;
            area.AxisY.MajorGrid.LineColor = Border;
            area.AxisX.LineColor = Border;
            area.AxisY.LineColor = Border;
            area.AxisY.IsStartedFromZero = true;
            area.AxisY.Minimum = 0;
            area.AxisY.Maximum = 5.5;
            area.AxisY.Title = "Arduino (V / A)";
            area.AxisY.TitleFont = new Font("Segoe UI", 8F, FontStyle.Bold);
            area.AxisY.TitleForeColor = Blue;

            // Sağ Eksen (Secondary Y2-Axis) -> ESP32 RSSI (dBm) & Ağ Sayısı (0 - 100 Scale)
            area.AxisY2.Enabled = AxisEnabled.True;
            area.AxisY2.LabelStyle.ForeColor = TextMuted;
            area.AxisY2.MajorGrid.Enabled = false;
            area.AxisY2.LineColor = Border;
            area.AxisY2.IsStartedFromZero = true;
            area.AxisY2.Minimum = 0;
            area.AxisY2.Maximum = 100;
            area.AxisY2.Title = "ESP32 (dBm / Ağ)";
            area.AxisY2.TitleFont = new Font("Segoe UI", 8F, FontStyle.Bold);
            area.AxisY2.TitleForeColor = Green;

            chart.ChartAreas.Add(area);

            var title = new Title("Canlı Ölçüm Eğrisi (Sol: Arduino V/A  |  Sağ: ESP32 dBm/Ağ)", Docking.Top, new Font("Segoe UI", 9.5F, FontStyle.Bold), TextMain);
            chart.Titles.Add(title);

            chart.Series.Add(new Series("Arduino Gerilim (V)") { ChartType = SeriesChartType.Spline, BorderWidth = 3, Color = Blue, ChartArea = "Live" });
            chart.Series.Add(new Series("Arduino Akım (A)") { ChartType = SeriesChartType.Spline, BorderWidth = 2, Color = Amber, ChartArea = "Live" });
            chart.Series.Add(new Series("ESP32 RSSI (dBm)") { ChartType = SeriesChartType.Spline, BorderWidth = 3, Color = Green, ChartArea = "Live", YAxisType = AxisType.Secondary });
            chart.Series.Add(new Series("ESP32 Ağlar") { ChartType = SeriesChartType.Spline, BorderWidth = 2, Color = Color.FromArgb(168, 85, 247), ChartArea = "Live", YAxisType = AxisType.Secondary });

            var legend = new Legend { BackColor = Panel, ForeColor = TextMuted, Docking = Docking.Bottom, Font = new Font("Segoe UI", 8F) };
            chart.Legends.Add(legend);
            return chart;
        }

        private Chart CreateParetoChart()
        {
            var chart = new Chart { Dock = DockStyle.Fill, BackColor = Panel, MinimumSize = new Size(100, 100) };
            var area = new ChartArea("Errors") { BackColor = PanelAlt };
            area.AxisX.LabelStyle.ForeColor = TextMuted;
            area.AxisY.LabelStyle.ForeColor = TextMuted;
            area.AxisX.MajorGrid.Enabled = false;
            area.AxisY.MajorGrid.LineColor = Border;
            chart.ChartAreas.Add(area);
            chart.Titles.Add(new Title("Hata Kodu Dağılımı", Docking.Top, new Font("Segoe UI", 11F, FontStyle.Bold), TextMain));
            chart.Series.Add(new Series("Hata Adedi") { ChartType = SeriesChartType.Column, Color = Red, ChartArea = "Errors", IsValueShownAsLabel = true, LabelForeColor = TextMain });
            return chart;
        }

        private DataGridView CreateGrid()
        {
            var grid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false, DataSource = _visibleLogs, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, BackgroundColor = PanelAlt, BorderStyle = BorderStyle.None, GridColor = Border, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, EnableHeadersVisualStyles = false };
            grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(30, 45, 64), ForeColor = TextMain, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), SelectionBackColor = Color.FromArgb(30, 45, 64) };
            grid.DefaultCellStyle = new DataGridViewCellStyle { BackColor = PanelAlt, ForeColor = TextMain, SelectionBackColor = Color.FromArgb(30, 64, 97), SelectionForeColor = Color.White, Padding = new Padding(4) };
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(14, 28, 47);
            grid.RowTemplate.Height = 32;
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "SerialNumber", Name = "SerialNumber", HeaderText = "Seri No", FillWeight = 85 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ProductType", Name = "ProductType", HeaderText = "Test Tipi", FillWeight = 105 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Voltage", Name = "Primary", HeaderText = "Gerilim / RSSI", FillWeight = 90 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Current", Name = "Secondary", HeaderText = "Akım / Ağ", FillWeight = 85 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Result", Name = "Result", HeaderText = "Sonuç", FillWeight = 65 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ErrorCode", Name = "ErrorCode", HeaderText = "Hata", FillWeight = 55 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "LogTime", Name = "LogTime", HeaderText = "Zaman", FillWeight = 95, DefaultCellStyle = new DataGridViewCellStyle { Format = "HH:mm:ss" } });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "SourceType", Name = "SourceType", HeaderText = "Kaynak", FillWeight = 70 });
            grid.CellFormatting += Grid_CellFormatting;
            grid.CellDoubleClick += Grid_CellDoubleClick;
            return grid;
        }

        private void Grid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            TestData item = _grid.Rows[e.RowIndex].DataBoundItem as TestData;
            if (item == null) return;
            string name = _grid.Columns[e.ColumnIndex].Name;
            if (name == "Primary") { e.Value = IsWifi(item) ? (-Math.Abs(item.Voltage)).ToString("F0") + " dBm" : item.Voltage.ToString("F2") + " V"; e.FormattingApplied = true; }
            if (name == "Secondary") { e.Value = IsWifi(item) ? item.Current.ToString("F0") + " ağ" : item.Current.ToString("F2") + " A"; e.FormattingApplied = true; }
            if (name == "Result")
            {
                e.CellStyle.ForeColor = item.Result == "PASS" ? Green : Red;
                e.CellStyle.Font = new Font(_grid.Font, FontStyle.Bold);
            }
        }

        private void Grid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            TestData item = SelectedTest();
            if (item == null) return;
            bool wifi = IsWifi(item);
            string details = "Seri No: " + item.SerialNumber + "\nTest: " + item.ProductType + "\n" +
                (wifi ? "Wi-Fi sinyali: " + (-Math.Abs(item.Voltage)).ToString("F0") + " dBm\nAğ sayısı: " + item.Current.ToString("F0") : "Gerilim: " + item.Voltage.ToString("F2") + " V\nAkım: " + item.Current.ToString("F2") + " A") +
                "\nSonuç: " + item.Result + "\nHata: " + item.ErrorCode + " — " + ErrorDescription(item.ErrorCode) + "\nOperatör: " + item.OperatorName + "\nZaman: " + item.LogTime.ToString("dd.MM.yyyy HH:mm:ss");
            MessageBox.Show(details, "Test İzlenebilirlik Kaydı", MessageBoxButtons.OK, item.Result == "PASS" ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
    }
}
