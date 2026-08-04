using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace ProcessTestApp.Application
{
    public class ReportService
    {
        private readonly Func<string, string> _errorDescription;
        private readonly Dictionary<string, ProductThreshold> _thresholds;

        public ReportService(Func<string, string> errorDescription, Dictionary<string, ProductThreshold> thresholds)
        {
            _errorDescription = errorDescription;
            _thresholds = thresholds ?? new Dictionary<string, ProductThreshold>();
        }

        public bool ExportToCsv(string filePath, IEnumerable<TestData> logs, out string errorMessage)
        {
            errorMessage = "";
            try
            {
                using (var writer = new StreamWriter(filePath, false, new UTF8Encoding(true)))
                {
                    writer.WriteLine("Seri No;Cihaz/Test Tipi;Birincil Ölçüm;İkincil Ölçüm;Sonuç;Hata Kodu;Kayıt Zamanı;Kaynak;Operatör");
                    foreach (var item in logs ?? Enumerable.Empty<TestData>())
                    {
                        writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                            "{0};{1};{2:F2};{3:F2};{4};{5};{6:yyyy-MM-dd HH:mm:ss};{7};{8}",
                            SafeCsv(item.SerialNumber), SafeCsv(item.ProductType), item.Voltage, item.Current,
                            SafeCsv(item.Result), SafeCsv(item.ErrorCode), item.LogTime,
                            SafeCsv(item.SourceType), SafeCsv(item.OperatorName)));
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        public bool GeneratePdfReport(TestData data, string pdfPath, string liveWebUrl, out string errorMessage)
        {
            errorMessage = "";
            if (data == null)
            {
                errorMessage = "Raporlanacak test kaydı seçilmedi.";
                return false;
            }

            string tempHtml = null;
            try
            {
                bool wifi = string.Equals(data.ProductType, "WIFI_TESTER", StringComparison.OrdinalIgnoreCase);
                ProductThreshold threshold;
                _thresholds.TryGetValue(data.ProductType ?? "", out threshold);
                double min = threshold == null ? (wifi ? 0.0 : 1.0) : threshold.MinVoltage;
                double max = threshold == null ? (wifi ? 75.0 : 4.5) : threshold.MaxVoltage;

                string primaryName = wifi ? "En Güçlü Wi-Fi Sinyali (RSSI)" : "Potansiyometre Gerilimi";
                string secondaryName = wifi ? "Bulunan Ağ Sayısı" : "Hesaplanan Akım";
                string primaryValue = wifi ? (-Math.Abs(data.Voltage)).ToString("F0", CultureInfo.InvariantCulture) + " dBm" : data.Voltage.ToString("F2", CultureInfo.InvariantCulture) + " V";
                string secondaryValue = wifi ? data.Current.ToString("F0", CultureInfo.InvariantCulture) + " ağ" : data.Current.ToString("F2", CultureInfo.InvariantCulture) + " A";
                string limitText = wifi ? string.Format(CultureInfo.InvariantCulture, "RSSI ≥ -{0:F0} dBm", Math.Abs(max)) : string.Format(CultureInfo.InvariantCulture, "{0:F2} V – {1:F2} V", min, max);

                bool isPass = string.Equals(data.Result, "PASS", StringComparison.OrdinalIgnoreCase);
                string badgeColor = isPass ? "#059669" : "#dc2626";
                string badgeIcon = isPass ? "✔" : "✖";
                string statusText = isPass ? "TEST BAŞARILI (PASS)" : "TEST BAŞARISIZ (FAIL)";

                string targetWebUrl = string.IsNullOrWhiteSpace(liveWebUrl) ? "http://127.0.0.1:5000" : liveWebUrl;
                string qrCodeUrl = "https://api.qrserver.com/v1/create-qr-code/?size=150x150&data=" + WebUtility.UrlEncode(targetWebUrl);

                string errorDesc = _errorDescription == null ? "" : _errorDescription(data.ErrorCode);
                if (string.IsNullOrWhiteSpace(errorDesc)) errorDesc = isPass ? "Tüm parametreler kalite toleransları dahilindedir." : "Tolerans dışı ölçüm tespiti.";

                string html = @"<!doctype html><html lang='tr'><head><meta charset='utf-8'>
                <style>
                    @page { size: A4; margin: 15mm 18mm 18mm 18mm; }
                    * { box-sizing: border-box; }
                    body { font-family: 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #0f172a; margin: 0; background: #ffffff; line-height: 1.5; }
                    
                    .header-table { width: 100%; border-collapse: collapse; margin-bottom: 22px; padding-bottom: 12px; border-bottom: 2px solid #0f172a; }
                    .header-left { vertical-align: bottom; }
                    .header-right { text-align: right; vertical-align: middle; width: 110px; }
                    .kicker { font-size: 10px; font-weight: 700; letter-spacing: 1.5px; color: #0284c7; text-transform: uppercase; margin-bottom: 3px; }
                    .title { font-size: 22px; font-weight: 800; color: #0f172a; margin: 0; letter-spacing: -0.3px; }
                    .subtitle { font-size: 11px; color: #475569; margin-top: 4px; font-weight: 500; }
                    
                    .qr-box { display: inline-block; text-align: center; border: 1px solid #cbd5e1; border-radius: 6px; padding: 4px; background: #ffffff; }
                    .qr-img { display: block; border-radius: 4px; }
                    .qr-caption { font-size: 8px; color: #475569; text-align: center; margin-top: 3px; font-weight: 700; text-transform: uppercase; }

                    .badge-container { text-align: center; margin: 20px 0 25px 0; }
                    .badge { display: inline-block; padding: 10px 36px; background: #ffffff; color: __BADGE_COLOR__; border: 2px solid __BADGE_COLOR__; border-radius: 30px; font-size: 17px; font-weight: 800; letter-spacing: 0.5px; }

                    .section-title { font-size: 11px; font-weight: 800; color: #0f172a; text-transform: uppercase; letter-spacing: 1px; margin-bottom: 10px; border-bottom: 1px solid #e2e8f0; padding-bottom: 4px; }

                    .info-grid { width: 100%; border-collapse: collapse; margin-bottom: 22px; }
                    .info-cell { width: 50%; padding: 5px; vertical-align: top; }
                    .info-card { border: 1px solid #cbd5e1; border-radius: 6px; padding: 12px 16px; background: #ffffff; }
                    .info-label { font-size: 9.5px; font-weight: 700; color: #64748b; text-transform: uppercase; letter-spacing: 0.5px; }
                    .info-value { font-size: 15px; font-weight: 700; color: #0f172a; margin-top: 3px; }

                    .table-main { width: 100%; border-collapse: collapse; margin-bottom: 22px; font-size: 12px; }
                    .table-main th { background: #ffffff; color: #0f172a; padding: 10px 12px; font-weight: 700; text-align: left; border-top: 1px solid #0f172a; border-bottom: 2px solid #0f172a; text-transform: uppercase; font-size: 10.5px; letter-spacing: 0.5px; }
                    .table-main td { padding: 12px; border-bottom: 1px solid #e2e8f0; color: #334155; }

                    .alert-box { padding: 14px 18px; border-radius: 6px; margin-bottom: 30px; font-size: 12px; border: 1px solid #cbd5e1; border-left: 5px solid __BADGE_COLOR__; background: #ffffff; color: #0f172a; line-height: 1.6; }

                    .signature-table { width: 100%; border-collapse: collapse; margin-top: 40px; }
                    .sig-cell { width: 50%; padding: 0 25px; text-align: center; }
                    .sig-line { border-bottom: 1px solid #0f172a; height: 50px; margin-bottom: 8px; }
                    .sig-title { font-size: 11px; font-weight: 700; color: #0f172a; text-transform: uppercase; letter-spacing: 0.5px; }
                    .sig-name { font-size: 11px; color: #64748b; margin-top: 3px; }

                    .footer-bar { margin-top: 45px; padding-top: 12px; border-top: 1px solid #e2e8f0; font-size: 9px; color: #64748b; display: flex; justify-content: space-between; }
                </style>
                </head><body>
                    <table class='header-table'>
                        <tr>
                            <td class='header-left'>
                                <div class='kicker'>ARDUINO / ESP32 PROSES TEST VE İZLENEBİLİRLİK İSTASYONU</div>
                                <div class='title'>Elektriksel ve Kablosuz Test Raporu</div>
                                <div class='subtitle'>Seri No: <strong>__SERIAL__</strong> &nbsp;|&nbsp; Rapor Tarihi: __DATE__</div>
                            </td>
                            <td class='header-right'>
                                <div class='qr-box'>
                                    <img src='__QR_CODE_URL__' class='qr-img' width='70' height='70' alt='QR' />
                                    <div class='qr-caption'>📱 CANLI İZLEME</div>
                                </div>
                            </td>
                        </tr>
                    </table>

                    <div class='badge-container'>
                        <div class='badge'>__BADGE_ICON__ __STATUS_TEXT__</div>
                    </div>

                    <div class='section-title'>İzlenebilirlik ve Test Parametreleri</div>
                    <table class='info-grid'>
                        <tr>
                            <td class='info-cell'>
                                <div class='info-card'>
                                    <div class='info-label'>Test Edilen Ürün / Tipi</div>
                                    <div class='info-value'>__PRODUCT__</div>
                                </div>
                            </td>
                            <td class='info-cell'>
                                <div class='info-card'>
                                    <div class='info-label'>Ölçüm Kaynağı & İstasyon</div>
                                    <div class='info-value'>__SOURCE__ (__STATION__)</div>
                                </div>
                            </td>
                        </tr>
                        <tr>
                            <td class='info-cell'>
                                <div class='info-card'>
                                    <div class='info-label'>Birincil Ölçüm Değeri</div>
                                    <div class='info-value'>__PRIMARY__</div>
                                </div>
                            </td>
                            <td class='info-cell'>
                                <div class='info-card'>
                                    <div class='info-label'>İkincil Ölçüm Değeri</div>
                                    <div class='info-value'>__SECONDARY__</div>
                                </div>
                            </td>
                        </tr>
                    </table>

                    <div class='section-title'>Detaylı Ölçüm Değerlendirmesi</div>
                    <table class='table-main'>
                        <thead>
                            <tr>
                                <th>Parametre / Test Adı</th>
                                <th>Ölçülen Değer</th>
                                <th>Kabul Toleransı / Limit</th>
                                <th>Değerlendirme</th>
                            </tr>
                        </thead>
                        <tbody>
                            <tr>
                                <td><strong>__PRIMARY_NAME__</strong></td>
                                <td><strong>__PRIMARY__</strong></td>
                                <td>__LIMIT__</td>
                                <td><span style='color:__BADGE_COLOR__; font-weight:bold;'>__RESULT__</span></td>
                            </tr>
                            <tr>
                                <td><strong>__SECONDARY_NAME__</strong></td>
                                <td>__SECONDARY__</td>
                                <td>Sistem Bilgilendirme Ölçümü</td>
                                <td><span style='color:#059669; font-weight:bold;'>KAYIT EDİLDİ</span></td>
                            </tr>
                        </tbody>
                    </table>

                    <div class='alert-box'>
                        <strong>Teşhis ve Kalite Notu:</strong> __ERROR__ — __ERROR_DESC__<br>
                        <strong>Sorumlu Operatör:</strong> __OPERATOR__ &nbsp;|&nbsp; <strong>Parti / BATCH No:</strong> __BATCH__
                    </div>

                    <table class='signature-table'>
                        <tr>
                            <td class='sig-cell'>
                                <div class='sig-line'></div>
                                <div class='sig-title'>Testi Yapan Operatör</div>
                                <div class='sig-name'>__OPERATOR__</div>
                            </td>
                            <td class='sig-cell'>
                                <div class='sig-line'></div>
                                <div class='sig-title'>Kalite Güvence Onayı</div>
                                <div class='sig-name'>İzlenebilirlik İstasyon Sorumlusu</div>
                            </td>
                        </tr>
                    </table>

                    <div class='footer-bar'>
                        <span>Belge No: REF-__SERIAL__-__DATE_COMPACT__</span>
                        <span>Mobil Canlı İzleme Adresi: __LIVE_WEB_URL__</span>
                        <span>Prototip Test ve İzlenebilirlik Sistemi</span>
                    </div>
                </body></html>";

                html = html.Replace("__BADGE_COLOR__", badgeColor)
                    .Replace("__BADGE_ICON__", badgeIcon)
                    .Replace("__STATUS_TEXT__", statusText)
                    .Replace("__QR_CODE_URL__", qrCodeUrl)
                    .Replace("__SERIAL__", Html(data.SerialNumber))
                    .Replace("__DATE__", data.LogTime.ToString("dd.MM.yyyy HH:mm:ss"))
                    .Replace("__DATE_COMPACT__", data.LogTime.ToString("yyyyMMddHHmmss"))
                    .Replace("__RESULT__", Html(data.Result))
                    .Replace("__PRODUCT__", Html(data.ProductType))
                    .Replace("__SOURCE__", Html(data.SourceType))
                    .Replace("__STATION__", Html(data.StationName))
                    .Replace("__PRIMARY__", Html(primaryValue))
                    .Replace("__SECONDARY__", Html(secondaryValue))
                    .Replace("__PRIMARY_NAME__", Html(primaryName))
                    .Replace("__SECONDARY_NAME__", Html(secondaryName))
                    .Replace("__LIMIT__", Html(limitText))
                    .Replace("__ERROR__", Html(data.ErrorCode))
                    .Replace("__ERROR_DESC__", Html(errorDesc))
                    .Replace("__OPERATOR__", Html(data.OperatorName))
                    .Replace("__BATCH__", Html(data.BatchNo))
                    .Replace("__LIVE_WEB_URL__", Html(targetWebUrl));

                tempHtml = Path.Combine(Path.GetTempPath(), "process_test_" + Guid.NewGuid().ToString("N") + ".html");
                File.WriteAllText(tempHtml, html, Encoding.UTF8);
                if (!ConvertWithEdge(tempHtml, pdfPath, out errorMessage))
                {
                    string fallback = Path.ChangeExtension(pdfPath, ".html");
                    File.Copy(tempHtml, fallback, true);
                    errorMessage += " HTML raporu oluşturuldu: " + fallback;
                    return false;
                }

                string archive = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PDF_Reports");
                Directory.CreateDirectory(archive);
                File.Copy(pdfPath, Path.Combine(archive, "TestReport_" + SafeFileName(data.SerialNumber) + "_" + data.LogTime.ToString("yyyyMMdd_HHmmss") + ".pdf"), true);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
            finally
            {
                if (!string.IsNullOrEmpty(tempHtml))
                {
                    try { File.Delete(tempHtml); } catch { }
                }
            }
        }

        private static bool ConvertWithEdge(string htmlPath, string pdfPath, out string error)
        {
            error = "PDF dönüştürme tespiti başarısız oldu.";
            string[] candidates =
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Edge", "Application", "msedge.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Edge", "Application", "msedge.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome", "Application", "chrome.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Google", "Chrome", "Application", "chrome.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "Application", "chrome.exe")
            };

            var existingBrowsers = candidates.Where(File.Exists).ToList();
            if (existingBrowsers.Count == 0)
            {
                error = "Sistemde Microsoft Edge veya Google Chrome tarayıcısı bulunamadı.";
                return false;
            }

            string tempPdf = Path.Combine(Path.GetTempPath(), "report_" + Guid.NewGuid().ToString("N") + ".pdf");
            string userProfileDir = Path.Combine(Path.GetTempPath(), "browser_pdf_profile_" + Guid.NewGuid().ToString("N"));
            string fullHtmlPath = Path.GetFullPath(htmlPath);
            string fileUri = new Uri(fullHtmlPath).AbsoluteUri;

            try
            {
                foreach (string browserPath in existingBrowsers)
                {
                    string[] argVariants = new string[]
                    {
                        string.Format(CultureInfo.InvariantCulture, "--headless=new --user-data-dir=\"{0}\" --disable-gpu --no-sandbox --disable-web-security --allow-file-access-from-files --no-pdf-header-footer --print-to-pdf=\"{1}\" \"{2}\"", userProfileDir, tempPdf, fileUri),
                        string.Format(CultureInfo.InvariantCulture, "--headless --user-data-dir=\"{0}\" --disable-gpu --no-sandbox --disable-web-security --allow-file-access-from-files --no-pdf-header-footer --print-to-pdf=\"{1}\" \"{2}\"", userProfileDir, tempPdf, fileUri),
                        string.Format(CultureInfo.InvariantCulture, "--headless=new --user-data-dir=\"{0}\" --disable-gpu --no-sandbox --no-pdf-header-footer --print-to-pdf=\"{1}\" \"{2}\"", userProfileDir, tempPdf, fullHtmlPath),
                        string.Format(CultureInfo.InvariantCulture, "--headless --user-data-dir=\"{0}\" --disable-gpu --no-sandbox --no-pdf-header-footer --print-to-pdf=\"{1}\" \"{2}\"", userProfileDir, tempPdf, fullHtmlPath)
                    };

                    foreach (string args in argVariants)
                    {
                        try
                        {
                            using (var process = Process.Start(new ProcessStartInfo(browserPath, args) { UseShellExecute = false, CreateNoWindow = true }))
                            {
                                if (process != null)
                                {
                                    process.WaitForExit(10000);
                                }
                            }

                            for (int i = 0; i < 30; i++)
                            {
                                if (File.Exists(tempPdf) && new FileInfo(tempPdf).Length > 0)
                                {
                                    string targetDir = Path.GetDirectoryName(Path.GetFullPath(pdfPath));
                                    if (!string.IsNullOrEmpty(targetDir)) Directory.CreateDirectory(targetDir);
                                    File.Copy(tempPdf, pdfPath, true);
                                    error = "";
                                    return true;
                                }
                                System.Threading.Thread.Sleep(100);
                            }
                        }
                        catch { }
                    }
                }
            }
            finally
            {
                if (File.Exists(tempPdf))
                {
                    try { File.Delete(tempPdf); } catch { }
                }
                if (Directory.Exists(userProfileDir))
                {
                    try { Directory.Delete(userProfileDir, true); } catch { }
                }
            }

            error = "PDF oluşturulamadı. (Tarayıcı headless çıktısı alınamadı).";
            return false;
        }

        private static string Html(string value)
        {
            return WebUtility.HtmlEncode(value ?? "");
        }

        private static string SafeCsv(string value)
        {
            return (value ?? "").Replace(";", ",").Replace("\r", " ").Replace("\n", " ");
        }

        private static string SafeFileName(string value)
        {
            string result = value ?? "UNKNOWN";
            foreach (char invalid in Path.GetInvalidFileNameChars()) result = result.Replace(invalid, '_');
            return result;
        }
    }
}
