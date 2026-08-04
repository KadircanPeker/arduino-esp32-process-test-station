using System;
using System.Globalization;
using ProcessTestApp.Infrastructure;

namespace ProcessTestApp.Communication
{
    public static class PacketParser
    {
        // Seri satır formatı: SN7001;VOLTAGE_RELAY_TESTER;2.50;1.25;PASS;E00
        public static TestData ParseLegacyLine(string rawLine)
        {
            if (string.IsNullOrEmpty(rawLine)) return null;

            string clean = rawLine.Trim('\r', '\n', ' ');
            string[] parts = clean.Split(';');
            
            if (parts.Length < 6)
            {
                FileLogger.Warning("PacketParser", "Eksik seri satır formatı: " + clean);
                return null;
            }

            try
            {
                string vStr = parts[2].Trim().Replace(',', '.');
                string cStr = parts[3].Trim().Replace(',', '.');

                double voltage = 0.0, current = 0.0;
                if (!double.TryParse(vStr, NumberStyles.Float, CultureInfo.InvariantCulture, out voltage))
                {
                    double.TryParse(vStr, NumberStyles.Any, CultureInfo.InvariantCulture, out voltage);
                }
                if (!double.TryParse(cStr, NumberStyles.Float, CultureInfo.InvariantCulture, out current))
                {
                    double.TryParse(cStr, NumberStyles.Any, CultureInfo.InvariantCulture, out current);
                }

                var data = new TestData
                {
                    SerialNumber = parts[0].Trim(),
                    ProductType = parts[1].Trim(),
                    Voltage = voltage,
                    Current = current,
                    Result = parts[4].Trim().ToUpper(),
                    ErrorCode = parts[5].Trim(),
                    LogTime = DateTime.Now,
                    TestAttemptNo = 1,
                    StationName = "İstasyon",
                    OperatorName = "Operatör",
                    BatchNo = "BATCH-" + DateTime.Now.ToString("yyyyMM"),
                    SourceType = "SERIAL"
                };
                return data;
            }
            catch (Exception ex)
            {
                FileLogger.Error("PacketParser", "Seri satır ayrıştırma hatası (" + clean + "): " + ex.Message);
                return null;
            }
        }
    }
}
