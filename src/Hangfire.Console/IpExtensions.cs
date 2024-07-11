using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Linq;

namespace Hangfire.Console;

internal static class IpExtensions
{
    public static string GetIp()
    {
        // 取得本地主機的 IP 地址列表
        IPAddress[] ipAddresses = Dns.GetHostAddresses(Dns.GetHostName());

        return ipAddresses.FirstOrDefault(ipAddress => ipAddress.AddressFamily == AddressFamily.InterNetwork)?.ToString();
    }
}
