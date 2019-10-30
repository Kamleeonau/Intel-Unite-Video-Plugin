using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;

namespace HTTPServer
{
    public class SimpleHTTP
    {
        private const int BUFFER_SIZE = 4096;
        private const int MAX_UPLOAD = 1073741824; // 1GB
        private readonly String[] HTTP_VERSIONS = { "HTTP/1.0", "HTTP/1.1" };
        private readonly String[] ALLOWED_METHODS = { "GET", "POST" };
        private TcpListener listener = null;

        public Dictionary<String, Func<HTTPRequest, HTTPResponse>> EndPoints = new Dictionary<string, Func<HTTPRequest, HTTPResponse>>();

        public SimpleHTTP(int port = 8998, bool autoAccept = true)
        {
            listener = new TcpListener(new IPEndPoint(IPAddress.Any, port));
            listener.Start();
            if (autoAccept)
            {
                accept();
            }
        }

        public void accept()
        {
            listener.BeginAcceptTcpClient(new AsyncCallback(onConnection), listener);
        }

        private void closeWithResponse(TcpClient client, HTTPResponse response)
        {
            if (client.Connected)
            {
                sendResponse(client, response);
                NetworkStream ns = client.GetStream();
                Console.WriteLine("Closing socket");
                ns.Close();
                client.Close();
            }
        }
        private void sendResponse(TcpClient client, HTTPResponse response)
        {

            if (client.Connected)
            {
                NetworkStream ns = client.GetStream();
                if (ns.CanWrite)
                {
                    ns.Write(response.Bytes, 0, response.Bytes.Length);
                    ns.Flush();
                }
            }
        }



        private void onConnection(IAsyncResult result)
        {

            byte[] buffer = new byte[BUFFER_SIZE];

            Console.WriteLine("Got incoming connection");
            // accept the next connection
            accept();
            TcpClient client = listener.EndAcceptTcpClient(result);
            NetworkStream ns = client.GetStream();

            // read request from client. we need to be able to time this out
            ns.ReadTimeout = 10000; // 10 second timeout waiting for new data

            // as this is a new connection, we expect HTTP headers.
            int bytesRead = 0;
            int bytesReadThisTime = 0;
            bool headersParsed = false;

            do
            {
                if (ns.CanRead)
                {
                    try
                    {
                        bytesReadThisTime = ns.Read(buffer, bytesRead, BUFFER_SIZE - bytesRead);
                        string s1 = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesReadThisTime);
                        string s2 = System.Text.Encoding.UTF8.GetString(buffer, bytesRead, bytesReadThisTime);
                        Console.WriteLine("Read this: {0}", s2);
                    }
                    
                    catch (Exception)
                    {
                        Console.WriteLine("Exception reading data from client");
                        ns.Close();
                        client.Close();
                        return;
                        
                    }
                    bytesRead += bytesReadThisTime;
                    Console.WriteLine("Read {0} bytes from stream", bytesRead);
                    string s = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    if (s.Contains("\r\n\r\n"))
                    {
                        // should have received complete request headers - try to create an HTTPRequest object
                        byte[] trim_buffer = new byte[bytesRead];
                        Array.Copy(buffer, trim_buffer, bytesRead);
                        HTTPRequest request = new HTTPRequest(trim_buffer);

                        // Version number
                        if (!HTTP_VERSIONS.Contains(request.HTTP_VERSION))
                        {
                            closeWithResponse(client, new HTTPResponse(
                                StatusCode: 505,
                                CustomHeaders: new Dictionary<string, string>() { { "Content-type", "text/html" } },
                                ResponseBody: "Method not allowed\r\n")
                                );
                            return;
                        }

                        // Method
                        if (!ALLOWED_METHODS.Contains(request.HTTP_METHOD))
                        {
                            closeWithResponse(client, new HTTPResponse(
                                StatusCode: 405,
                                CustomHeaders: new Dictionary<string, string>() { { "Content-type", "text/html" } },
                                ResponseBody: "Method not allowed\r\n")
                                );
                            return;
                        }


                        // Valid URI?
                        if (EndPoints.Keys.Contains(request.HTTP_URI))
                        {
                            // we have an endpoint!

                            // deal with post
                            if (request.HTTP_METHOD == "POST")
                            {
                                // obtain the body
                                if (!request.Headers.Keys.Contains("content-length"))
                                {
                                    // no content length header. this is pretty invalid.
                                    closeWithResponse(client, new HTTPResponse(StatusCode: 400));
                                    return;
                                }
                                int contentLength = 0;
                                Int32.TryParse(request.Headers["content-length"], out contentLength);

                                // did the client send an expect?
                                if (request.Headers.ContainsKey("expect") && request.Headers["expect"].ToLower() == "100-continue")
                                {
                                    Console.WriteLine("We are expected to continue");
                                    if (contentLength > MAX_UPLOAD)
                                    {
                                        closeWithResponse(client, new HTTPResponse(
                                            StatusCode: 417,
                                            ResponseBody:"Request exceeds maximum size\r\n"
                                            )
                                        );
                                        return;
                                    }
                                    // we need to let the client know that it is ok to proceed
                                    sendResponse(client, new HTTPResponse(
                                        StatusCode: 100
                                        )
                                    );
                                    Console.WriteLine("Sent continue");
                                }

                                // somebody is POSTing
                                // create a buffer to hold the entire post
                                byte[] bodyBuffer = new byte[contentLength];
                                // do we have body data already? Prefill that
                                
                                if (request.BodyRaw.Length > 0)
                                {
                                    Console.WriteLine("Found {0} bytes of existing body data", request.BodyRaw.Length);
                                    Array.Copy(request.BodyRaw, bodyBuffer, request.BodyRaw.Length);
                                }

                                // try to read up until content-length
                                bytesRead = request.BodyRaw.Length;
                                int bytesRemaining = contentLength - bytesRead;
                                while (bytesRemaining > 0)
                                {
                                    try
                                    {
                                        bytesReadThisTime = ns.Read(bodyBuffer, bytesRead, Math.Min(BUFFER_SIZE, bytesRemaining));
                                        bytesRead += bytesReadThisTime;
                                        bytesRemaining = contentLength - bytesRead;
                                        if (bytesReadThisTime == 0)
                                        {
                                            throw new System.IO.IOException();
                                        }
                                    }
                                    catch (Exception)
                                    {
                                        Console.WriteLine("Exception reading content");
                                        // try to close gracefully, on the off chance we are still connected
                                        closeWithResponse(client, new HTTPResponse(
                                        StatusCode: 400
                                        ));
                                        return;
                                    }
                                    Console.WriteLine("Read {0} bytes", bytesRead);
                                }

                                // sanity checking
                                if (contentLength != bodyBuffer.Length)
                                {
                                    closeWithResponse(client, new HTTPResponse(
                                        StatusCode:400
                                        ));
                                    return;
                                }

                                // update the HTTPRequest with the complete body
                                request.BodyRaw = bodyBuffer;

                            }

                            // call the endpoint
                            HTTPResponse a = EndPoints[request.HTTP_URI](request);
                            closeWithResponse(client, a);
                            return;
                        }
                        // no end point for the requested URI. Return a 404.
                        closeWithResponse(client, new HTTPResponse(
                                StatusCode:404,
                                CustomHeaders: new Dictionary<string, string>() { { "Content-type", "text/html" } },
                                ResponseBody: "File not found\r\n")
                                );
                        return;
                    }
                }
                else
                {
                    Console.WriteLine("Cannot read from socket - terminating");
                }
                bytesReadThisTime = 0;

            } while (bytesReadThisTime > 0);

            closeWithResponse(client, new HTTPResponse(400));

        }

        public static String URLDecode(String input)
        {
            String output = "";

            // iterate over the string
            for (int i = 0; i < input.Length; i++)
            {
                String s = input.Substring(i, 1);
                // basic space substitute

                switch (s)
                {
                    case "+":
                        // basic space substitute
                        s = " ";
                        break;
                    case "%":
                        if (input.Length > i + 2)
                        {
                            String hex = input.Substring(i + 1, 2);
                            Console.WriteLine("Hex code: {0}", hex);
                            int c = 0;
                            try
                            {
                                c = Convert.ToUInt16(hex, 16);
                            }
                            catch (Exception)
                            {
                                // fail silently
                                continue;
                            }
                            s = new string((char)c, 1);
                            i += 2;
                        }
                        break;
                    default:
                        break;
                }
                output += s;

            }

            return output;
        }
    }

}
