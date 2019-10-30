using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using HTTPServer;
using System.Net.Sockets;
using System.Net;

namespace NativeVideoTest
{
    public partial class Form1 : Form
    {

        SimpleHTTP http = null;


        public Form1()
        {
            InitializeComponent();

        }

        public HTTPServer.HTTPResponse root(HTTPRequest r)
        {
            Console.WriteLine("Got root? {0}", r.HTTP_URI);

            String headerText = "";
            foreach (String k in r.Headers.Keys)
            {
                headerText += k + ": " + r.Headers[k] + "<BR/>";
            }

            return new HTTPResponse(
                ResponseBody: "<h1>Welcome</h1><hr/>This is a demo page.<br/>" + headerText,
                CustomHeaders:new Dictionary<string, string>() { { "Content-Type", "text/html"} }
            );
        }

        private HTTPServer.HTTPResponse teapot(HTTPRequest r)
        {
            return new HTTPResponse(
                StatusCode:418,
                ResponseBody:"I'm a teapot!"
            );
        }


        private HTTPServer.HTTPResponse upload(HTTPRequest r)
        {
            Console.WriteLine(r.HTTP_URI);
            return new HTTPResponse();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            http = new SimpleHTTP();
            http.EndPoints.Add("/", root);
            http.EndPoints.Add("/teapot", teapot);
            http.EndPoints.Add("/upload", upload);
        }

    }
}
