using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StreamView
{
    public partial class MainForm : Form
    {
        private IntPtr chatterinoHandle;
        private static Regex twitchUrlRegex = new Regex(@"^https?:\/\/[^\/]+\/(\w+)");
        private Process chatterinoProcess;

        public MainForm(CoreWebView2Environment env, IntPtr chatterinoHandle, string initialChannel)
        {
            InitializeComponent();

            this.webview.EnsureCoreWebView2Async(env);

            this.chatterinoHandle = chatterinoHandle;

            if (initialChannel != null)
            {
                this.webview.Source = new Uri($"https://twitch.tv/{initialChannel}");
            }
            else
            {
                this.webview.Source = new Uri("https://twitch.tv");
            }

            this.webview.SourceChanged += (s, e) =>
            {
                var match = twitchUrlRegex.Match(this.webview.Source.ToString());

                if (match.Success)
                {
                    var twitchChannel = match.Groups[1];

                    Console.WriteLine($"Switching to channel '{twitchChannel}'");
                    ChatterinoInterop.SetChatterinoChannel(this.chatterinoHandle, twitchChannel.Value);
                }
            };

            this.webview.CoreWebView2Ready += (s, e) =>
            {
                this.webview.CoreWebView2.ContainsFullScreenElementChanged += (s2, e2) =>
                {
                    this.setFullscreen(this.webview.CoreWebView2.ContainsFullScreenElement);
                };
            };
        }


        private void setFullscreen(bool value)
        {
            if (value)
            {
                this.TopMost = true;
                this.FormBorderStyle = FormBorderStyle.None;
                this.WindowState = FormWindowState.Maximized;
            }
            else
            {
                this.TopMost = false;
                this.FormBorderStyle = FormBorderStyle.Sizable;
                this.WindowState = FormWindowState.Normal;
            }
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            if (m.Msg == PInvoke.WM_COPYDATA)
            {
                var chatterinoMessage = ChatterinoInterop.DecodeChatterinoMessage(m);

                if (chatterinoMessage is CreatedWindow msg)
                {
                    this.chatterinoHandle = msg.Handle;
                    ChatterinoInterop.SetChatterinoDpi(this.chatterinoHandle, this.chatterinoRect, this.DeviceDpi);

                    Task.Run(async () =>
                    {
                        await Task.Delay(100);
                        this.Invoke((MethodInvoker)delegate { this.resizeControls(); });
                    });
                }
                else if (chatterinoMessage is WindowShown)
                {
                    ChatterinoInterop.SetChatterinoDpi(this.chatterinoHandle, this.chatterinoRect, this.DeviceDpi);
                    this.resizeControls();
                }
                else if (chatterinoMessage is FixBounds)
                {
                    Task.Run(async () =>
                    {
                        await Task.Delay(100);
                        ChatterinoInterop.ResizeChatterino(this.chatterinoHandle, this.chatterinoRect);
                        await Task.Delay(500);
                        this.Invoke((MethodInvoker)delegate { this.resizeControls(); });
                    });
                }
            }
            else if (m.Msg == PInvoke.WM_DPICHANGED)
            {
                ChatterinoInterop.SetChatterinoDpi(this.chatterinoHandle, this.chatterinoRect, m.WParam.ToInt32() & 0xff);
                this.resizeControls();
            }
        }

        protected override void OnCreateControl()
        {
            base.OnCreateControl();

            PInvoke.UseImmersiveDarkMode(this.Handle, true);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            if (this.chatterinoHandle == IntPtr.Zero)
            {
                this.chatterinoProcess = ChatterinoInterop.RunChatterino(this.Handle);
                ChatterinoInterop.SetChatterinoDpi(this.chatterinoHandle, this.chatterinoRect, this.DeviceDpi);
                this.resizeControls();
            }
            else
            {
                PInvoke.SetParent(this.chatterinoHandle, this.Handle);
                ChatterinoInterop.SetReadyToShow(this.chatterinoHandle, this.Handle);
                ChatterinoInterop.SetChatterinoDpi(this.chatterinoHandle, this.chatterinoRect, this.DeviceDpi);
                this.resizeControls();
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            this.resizeControls();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            try
            {
                this.chatterinoProcess?.Kill();
            }
            catch { }

            base.OnClosing(e);
        }

        private void resizeControls()
        {
            // Resize webview
            this.webview.Width = this.ClientSize.Width - this.chatterinoRect.Width;
            this.webview.Height = this.ClientSize.Height;

            // Resize chatterino
            ChatterinoInterop.ResizeChatterino(this.chatterinoHandle, this.chatterinoRect);
        }

        private Rectangle chatterinoRect
        {
            get
            {
                var width = (int)(340 * (this.DeviceDpi / 96f));
                return new Rectangle(this.ClientSize.Width - width, 0, width, this.ClientSize.Height);
            }
        }
    }
}
