using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text;

namespace UniteVideoBrowserExtensionHelper
{
    class Util
    {
        public static String FindUniteHub()
        {
            int UnitePID = 0;
            String host = "";

            Process[] uniteProcesses = Process.GetProcessesByName("Intel Unite");

            if (uniteProcesses.Length == 0)
            {
                return "";
            }

            // Unite cannot (normally) run multiple times so we will only care about the first instance
            UnitePID = uniteProcesses[0].Id;

            foreach (TcpRow tcpRow in ManagedIpHelper.GetExtendedTcpTable(true))
            {
                // look for established connections owned by the unite process to ports > 1024. In most cases this should eliminate the pin server and LDAP servers.
                if (tcpRow.ProcessId == UnitePID && tcpRow.State == TcpState.Established && tcpRow.RemoteEndPoint.Port > 1024 && tcpRow.RemoteEndPoint.Port != 5050)
                {
                    host = tcpRow.RemoteEndPoint.Address.ToString();
                    break;
                }

            }

            return host;
        }

        public static byte[] EncodeNative(String message)
        {
            message = "\"" + message + "\"";
            byte[] retVal = new byte[message.Length + 4];
            Buffer.BlockCopy(BitConverter.GetBytes(message.Length), 0, retVal, 0, 4);
            Buffer.BlockCopy(Encoding.UTF8.GetBytes(message), 0, retVal, 4, message.Length);

            return retVal;
        }
    }
}
