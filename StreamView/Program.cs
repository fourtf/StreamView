using CommandLine;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace StreamView
{
    class Options
    {
        [Option("chat-handle", Required = false)]
        public long ChatHandle { get; set; }

        [Option("channel", Required = false)]
        public string Channel { get; set; }
    }

    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(async opt =>
                {
                    CoreWebView2Environment env;
                    try
                    {
                        string userDataDir = Environment.GetEnvironmentVariable("APPDATA") + "/Chatterino2/StreamView";

                        env = await CoreWebView2Environment.CreateAsync(null, userDataDir);
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show(e.Message, "Error initiating the WebView2 environment");
                        Environment.Exit(1);
                        return;
                    }

                    Application.Run(new MainForm(env, new IntPtr(opt.ChatHandle), opt.Channel));
                });
        }
    }
}
