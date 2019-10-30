using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Web.Script.Serialization;
using System.IO;
using System.Text;
using System.Net;
using System.Net.Sockets;

namespace UniteVideoBrowserExtensionHelper
{
    class Program
    {


        static void Main(string[] args)
        {
            const int MAX_LENGTH = 10240;

            Stream stdin = Console.OpenStandardInput();
            Stream stdout = Console.OpenStandardOutput();

            // socket to send for debugging
            var s = new System.Net.Sockets.UdpClient();
            

            // buffer
            byte[] messageLengthRaw = new byte[4];

            String messageString = "";

            

            while (true)
            {
                try
                {
                    int readBytes = stdin.Read(messageLengthRaw, 0, 4);
                    //unpack bytes to a 32-bit unsigned integer
                    int length = BitConverter.ToInt32(messageLengthRaw, 0);
                    if (readBytes == 0)
                    {
                        // we read zero bytes. this means we should be ending
                        return;
                    }
                    Console.Error.WriteLine("Read {0} bytes from stream. Message length is {1}.", readBytes, length);

                    // need an appropriate sized buffer for the incoming message
                    if (length > MAX_LENGTH)
                    {
                        length = MAX_LENGTH;
                    }
                    byte[] message = new byte[length];

                    stdin.Read(message, 0, length);
                    messageString = Encoding.UTF8.GetString(message);

                    Console.Error.WriteLine("Length: {0}, Message: {1}", messageString.Length, messageString);
                    if (messageString.Length == 0)
                    {
                        //abort because we have a null string
                        continue;
                    }

                    String HubAddress = Util.FindUniteHub();
                    String Response = "";
                    if (HubAddress.Length > 0)
                    {
                        // We appear to be connected to a hub, try to send the URL through for playback
                        Response = "OK";
                        TcpClient client = new TcpClient();
                        try
                        {
                            client.Connect(HubAddress, 5050);
                            Stream stream = client.GetStream();
                            stream.Write(message,0,length);
                        }
                        catch (Exception)
                        {
                            Console.Error.WriteLine("Exception happened trying to connect :(");
                            Response = "ERR:CONNECTERROR";
                        }
                        

                    }
                    else
                    {
                        Response = "ERR:NOTCONNECT";
                    }

                    byte[] m = Util.EncodeNative(Response);
                    stdout.Write(m, 0, m.Length);
                    stdout.Flush();

                    // send the data to localhost for debugging
                    //s.Send(m, m.Length, "10.30.0.1", 5050);


                                        
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine("Exception happened. {0}", e.Message);
                    continue;
                }

            }
        }

    }
}
