using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace ProcessTestApp.Infrastructure
{
    public static class NetworkHelper
    {
        public static string GetActiveLanIPAddress()
        {
            try
            {
                // 1. Önce aktif Wi-Fi ve Ethernet fiziksel kartlarını tara
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                    string desc = (ni.Description ?? "").ToLower();
                    string name = (ni.Name ?? "").ToLower();

                    // Sanal adaptörleri ele (VirtualBox, VMware, Hyper-V, vEthernet, Docker, WSL, Npcap)
                    if (desc.Contains("virtual") || desc.Contains("vmware") || desc.Contains("hyper-v") ||
                        desc.Contains("vethernet") || desc.Contains("docker") || desc.Contains("wsl") ||
                        name.Contains("vbox") || name.Contains("vmnet") || name.Contains("vethernet"))
                    {
                        continue;
                    }

                    IPInterfaceProperties props = ni.GetIPProperties();
                    foreach (UnicastIPAddressInformation ip in props.UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            string ipStr = ip.Address.ToString();
                            if (!ipStr.StartsWith("127.") && !ipStr.StartsWith("169.254."))
                            {
                                return ipStr;
                            }
                        }
                    }
                }

                // 2. Yedek: Dns.GetHostEntry üzerinden gerçek IP'yi bul
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        string ipStr = ip.ToString();
                        if (!ipStr.StartsWith("127.") && !ipStr.StartsWith("169.254."))
                        {
                            return ipStr;
                        }
                    }
                }

                return "localhost";
            }
            catch
            {
                return "localhost";
            }
        }
    }
}
