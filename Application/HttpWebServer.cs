using System;
using System.Collections.Concurrent;
using System.Configuration;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using ProcessTestApp.Data;
using ProcessTestApp.Domain;
using ProcessTestApp.Infrastructure;

namespace ProcessTestApp.Application
{
    public class HttpWebServer
    {
        private sealed class WebSession
        {
            public string Username { get; set; }
            public DateTime ExpiresAtUtc { get; set; }
        }

        private readonly int _port;
        private readonly Func<string, string> _dashboardHtml;
        private readonly Func<string> _statsJson;
        private readonly Func<string, bool> _commandHandler;
        private readonly IUserRepository _users;
        private readonly IAuditLogRepository _audit;
        private readonly ConcurrentDictionary<string, WebSession> _sessions = new ConcurrentDictionary<string, WebSession>();
        private TcpListener _listener;
        private Thread _thread;
        private volatile bool _running;

        public event Action<string> OnServerStarted;
        public event Action<string> OnServerStopped;
        public event Action<string> OnServerError;

        public HttpWebServer(int port, Func<string, string> dashboardHtml, Func<string> statsJson,
            Func<string, bool> commandHandler, IDbConnectionFactory connectionFactory)
        {
            _port = port;
            _dashboardHtml = dashboardHtml;
            _statsJson = statsJson;
            _commandHandler = commandHandler;
            _users = new UserRepository(connectionFactory);
            _audit = new AuditLogRepository(connectionFactory);
        }

        public void Start()
        {
            if (_running) return;
            try
            {
                bool lanMode;
                bool.TryParse(ConfigurationManager.AppSettings["EnableLanMode"], out lanMode);
                _listener = new TcpListener(lanMode ? IPAddress.Any : IPAddress.Loopback, _port);
                _listener.Start();
                _running = true;
                _thread = new Thread(ServerLoop) { IsBackground = true, Name = "ProcessTestWebServer" };
                _thread.Start();
                string host = lanMode ? NetworkHelper.GetActiveLanIPAddress() : "127.0.0.1";
                if (OnServerStarted != null) OnServerStarted(host);
                FileLogger.Info("HttpWebServer", "Mobil panel başlatıldı: http://" + host + ":" + _port);
            }
            catch (Exception ex)
            {
                if (OnServerError != null) OnServerError(ex.Message);
                FileLogger.Error("HttpWebServer", ex.Message);
            }
        }

        public void Stop()
        {
            _running = false;
            _sessions.Clear();
            try { if (_listener != null) _listener.Stop(); } catch { }
            _listener = null;
            if (OnServerStopped != null) OnServerStopped("Durduruldu");
        }

        private void ServerLoop()
        {
            while (_running && _listener != null)
            {
                try
                {
                    TcpClient client = _listener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(delegate { ProcessClient(client); });
                }
                catch
                {
                    if (_running) Thread.Sleep(100);
                }
            }
        }

        private void ProcessClient(TcpClient client)
        {
            using (client)
            {
                try
                {
                    client.ReceiveTimeout = 5000;
                    client.SendTimeout = 5000;
                    string remote = (client.Client.RemoteEndPoint as IPEndPoint) == null ? "unknown" : ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
                    using (NetworkStream stream = client.GetStream())
                    using (var reader = new StreamReader(stream, Encoding.UTF8, false, 4096, true))
                    {
                        string requestLine = reader.ReadLine();
                        if (string.IsNullOrWhiteSpace(requestLine)) return;
                        string[] request = requestLine.Split(' ');
                        if (request.Length < 2) return;
                        string method = request[0].ToUpperInvariant();
                        string path = request[1].Split('?')[0].ToLowerInvariant();
                        int contentLength = 0;
                        string authorization = "";
                        string header;
                        while (!string.IsNullOrEmpty(header = reader.ReadLine()))
                        {
                            if (header.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) int.TryParse(header.Substring(15).Trim(), out contentLength);
                            if (header.StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase)) authorization = header.Substring(14).Trim();
                        }
                        string body = "";
                        if (contentLength > 0 && contentLength <= 8192)
                        {
                            char[] buffer = new char[contentLength];
                            int total = 0;
                            while (total < contentLength)
                            {
                                int read = reader.Read(buffer, total, contentLength - total);
                                if (read <= 0) break;
                                total += read;
                            }
                            body = new string(buffer, 0, total);
                        }

                        if (method == "OPTIONS")
                        {
                            WriteResponse(stream, 200, "text/plain", "");
                            return;
                        }

                        if (method == "GET" && (path == "/" || path == "/index.html"))
                        {
                            WriteResponse(stream, 200, "text/html; charset=utf-8", _dashboardHtml(NetworkHelper.GetActiveLanIPAddress()));
                            return;
                        }
                        if (method == "GET" && path == "/api/stats")
                        {
                            WriteResponse(stream, 200, "application/json; charset=utf-8", _statsJson());
                            return;
                        }
                        if (method == "POST" && path == "/api/login")
                        {
                            Login(stream, body, remote);
                            return;
                        }
                        if (method == "POST" && path == "/api/logout")
                        {
                            string logoutToken = ExtractToken(authorization, request[1], body);
                            WebSession removed;
                            _sessions.TryRemove(logoutToken, out removed);
                            WriteResponse(stream, 200, "application/json; charset=utf-8", "{\"status\":\"success\"}");
                            return;
                        }
                        if (method == "POST" && (path == "/api/start" || path == "/api/estop" || path == "/api/reset"))
                        {
                            string tokenCandidate = ExtractToken(authorization, request[1], body);
                            WebSession session;
                            if (!ValidateSession(tokenCandidate, out session))
                            {
                                WriteResponse(stream, 401, "application/json; charset=utf-8", "{\"status\":\"error\",\"message\":\"Yönetici oturumu gerekli.\"}");
                                return;
                            }
                            string command = path == "/api/estop" ? "E_STOP" : path == "/api/reset" ? "RESET" : "START";
                            bool executed = _commandHandler(command);
                            _audit.Add(new AuditLog(session.Username, "WEB_" + command,
                                "Mobil panel komutu " + (executed ? "SUCCESS" : "FAILED") + " / IP: " + remote, null, command));
                            if (!executed)
                            {
                                WriteResponse(stream, 409, "application/json; charset=utf-8", "{\"status\":\"error\",\"message\":\"Cihaz bağlı değil veya seri komut gönderilemedi.\"}");
                                return;
                            }
                            WriteResponse(stream, 200, "application/json; charset=utf-8", "{\"status\":\"success\"}");
                            return;
                        }

                        WriteResponse(stream, 404, "application/json; charset=utf-8", "{\"status\":\"error\",\"message\":\"Bulunamadı.\"}");
                    }
                }
                catch (Exception ex)
                {
                    FileLogger.Warning("HttpWebServer", "İstek işlenemedi: " + ex.Message);
                }
            }
        }

        private void Login(NetworkStream stream, string body, string remote)
        {
            string username = JsonValue(body, "username");
            string password = JsonValue(body, "password");
            User user = _users.Authenticate(username, password);
            if (user == null)
            {
                _audit.Add(new AuditLog(username ?? "unknown", "WEB_LOGIN_FAIL", "Hatalı mobil giriş / IP: " + remote, null, null));
                WriteResponse(stream, 401, "application/json; charset=utf-8", "{\"status\":\"error\",\"message\":\"Kullanıcı adı veya parola hatalı.\"}");
                return;
            }
            if (RoleNames.NormalizeRoleName(user.Role) != RoleNames.Administrator)
            {
                WriteResponse(stream, 403, "application/json; charset=utf-8", "{\"status\":\"error\",\"message\":\"Mobil komutlar yalnızca Administrator rolüne açıktır.\"}");
                return;
            }

            string token = CreateToken();
            _sessions[token] = new WebSession { Username = user.Username, ExpiresAtUtc = DateTime.UtcNow.AddMinutes(30) };
            _audit.Add(new AuditLog(user.Username, "WEB_LOGIN_SUCCESS", "Mobil yönetici oturumu / IP: " + remote, null, null));
            WriteResponse(stream, 200, "application/json; charset=utf-8", "{\"status\":\"success\",\"token\":\"" + JsonEscape(token) + "\",\"username\":\"" + JsonEscape(user.Username) + "\"}");
        }

        private bool ValidateSession(string token, out WebSession session)
        {
            session = null;
            if (string.IsNullOrWhiteSpace(token) || !_sessions.TryGetValue(token, out session)) return false;
            if (session.ExpiresAtUtc <= DateTime.UtcNow)
            {
                WebSession removed;
                _sessions.TryRemove(token, out removed);
                session = null;
                return false;
            }
            return true;
        }

        private static string GetBearerToken(string header)
        {
            return header != null && header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header.Substring(7).Trim() : "";
        }

        private static string ExtractToken(string authorizationHeader, string rawPath, string body)
        {
            string token = GetBearerToken(authorizationHeader);
            if (!string.IsNullOrWhiteSpace(token)) return token;

            if (!string.IsNullOrWhiteSpace(rawPath) && rawPath.Contains("?"))
            {
                Match qMatch = Regex.Match(rawPath, @"[?&]token=([^&]+)", RegexOptions.IgnoreCase);
                if (qMatch.Success) return Uri.UnescapeDataString(qMatch.Groups[1].Value);
            }

            token = JsonValue(body, "token");
            if (!string.IsNullOrWhiteSpace(token)) return token;

            return "";
        }

        private static string JsonValue(string body, string name)
        {
            Match match = Regex.Match(body ?? "", "\\\"" + Regex.Escape(name) + "\\\"\\s*:\\s*\\\"(?<v>(?:\\\\.|[^\\\"])*)\\\"", RegexOptions.IgnoreCase);
            return match.Success ? Regex.Unescape(match.Groups["v"].Value) : "";
        }

        private static string CreateToken()
        {
            byte[] bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }

        private static void WriteResponse(NetworkStream stream, int status, string contentType, string body)
        {
            byte[] content = Encoding.UTF8.GetBytes(body ?? "");
            string reason = status == 200 ? "OK" : status == 401 ? "Unauthorized" : status == 403 ? "Forbidden" : status == 409 ? "Conflict" : "Not Found";
            string headers = "HTTP/1.1 " + status + " " + reason + "\r\n" +
                             "Content-Type: " + contentType + "\r\n" +
                             "Content-Length: " + content.Length + "\r\n" +
                             "Access-Control-Allow-Origin: *\r\n" +
                             "Access-Control-Allow-Headers: Authorization, Content-Type\r\n" +
                             "Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n" +
                             "Cache-Control: no-store\r\n" +
                             "X-Content-Type-Options: nosniff\r\n" +
                             "Connection: close\r\n\r\n";
            byte[] headerBytes = Encoding.ASCII.GetBytes(headers);
            stream.Write(headerBytes, 0, headerBytes.Length);
            stream.Write(content, 0, content.Length);
            stream.Flush();
        }

        private static string JsonEscape(string value)
        {
            return (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n");
        }
    }
}
