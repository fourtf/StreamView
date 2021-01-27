using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Dynamic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace StreamView
{
    public partial class MainForm : Form
    {
        private IntPtr chatterinoHandle;
        private static Regex twitchUrlRegex = new Regex(@"^https?:\/\/[^\/]+\/(\w+)");
        private Process chatterinoProcess;

        public MainForm()
        {
            InitializeComponent();

            this.webview.SourceChanged += (s, e) =>
            {
                var match = twitchUrlRegex.Match(this.webview.Source.ToString());

                if (match.Success)
                {
                    var twitchChannel = match.Groups[1];

                    Console.WriteLine($"Switching to channel '{twitchChannel}'");
                    this.setChatterinoChannel(twitchChannel.Value);
                }
            };

            this.webview.CoreWebView2Ready += (s, e) =>
            {
                this.webview.CoreWebView2.ContainsFullScreenElementChanged += (s2, e2) =>
                {
                    this.setFullscreen(this.webview.CoreWebView2.ContainsFullScreenElement);
                };
            };

            this.webview.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.F1)
                {
                    // open settings
                    e.Handled = true;
                }
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
                PInvoke.COPYDATASTRUCT cps = (PInvoke.COPYDATASTRUCT)Marshal.PtrToStructure(m.LParam, typeof(PInvoke.COPYDATASTRUCT));
                byte[] buffer = new byte[cps.cbData];
                Marshal.Copy(cps.lpData, buffer, 0, cps.cbData);

                var data = Encoding.UTF8.GetString(buffer, 0, buffer[buffer.Length - 1] == 0 ? buffer.Length - 1 : buffer.Length);
                var root = JObject.Parse(data);
                if (root.Value<string>("type") == "created-window")
                {
                    if (long.TryParse(root.Value<string>("window-id"), out var hwnd))
                    {
                        this.chatterinoHandle = new IntPtr(hwnd);
                        this.updateChatterinoDpi();

                        Task.Run(async () =>
                        {
                            await Task.Delay(100);
                            this.BeginInvoke((MethodInvoker)delegate { this.resizeControls(); });
                        });
                    }
                }
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

            this.runChatterino();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            this.resizeControls();
        }

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);

            this.updateChatterinoDpi();
            this.resizeControls();
        }

        private void runChatterino()
        {
            Console.WriteLine(this.Handle);

            var chatterinoProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = System.IO.File.ReadAllText("exe").Trim(),
                    Arguments = $"--x-attach-split-to-window={this.Handle}",
                }
            };
            this.chatterinoProcess = chatterinoProcess;

            chatterinoProcess.Start();
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

            // Resize chatterino
            if (this.chatterinoHandle != IntPtr.Zero)
            {
                var rect = chatterinoRect;

                PInvoke.SetWindowPos(this.chatterinoHandle, IntPtr.Zero, rect.X, rect.Y, rect.Width, rect.Height,
                    PInvoke.SetWindowPosFlags.DoNotActivate |
                    PInvoke.SetWindowPosFlags.DoNotChangeOwnerZOrder |
                    PInvoke.SetWindowPosFlags.DoNotCopyBits);
            }
        }

        private void updateChatterinoDpi()
        {
            if (this.chatterinoHandle != IntPtr.Zero)
            {
                var hiword = this.DeviceDpi | this.DeviceDpi << 16;
                var rect = new PInvoke.RECT(this.chatterinoRect);

                var ptr = Marshal.AllocHGlobal(Marshal.SizeOf(rect));
                Marshal.StructureToPtr(rect, ptr, false);

                PInvoke.PostMessage(new HandleRef(new PInvoke.HGlobalOwner(ptr), this.chatterinoHandle), PInvoke.WM_DPICHANGED, hiword, ptr);
            }
        }

        private void setChatterinoChannel(string twitchChannelName)
        {
            // Currently only support twitch channels.
            dynamic payload = new Dictionary<string, string>
            {
                { "type" , "set-channel" },
                { "provider" , "twitch" },
                { "channel-name" , twitchChannelName },
            };

            var bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload));
            var payloadPtr = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, payloadPtr, bytes.Length);

            var data = new PInvoke.COPYDATASTRUCT
            {
                lpData = payloadPtr,
                cbData = bytes.Length,
            };

            var dataPtr = Marshal.AllocHGlobal(Marshal.SizeOf(data));
            Marshal.StructureToPtr(data, dataPtr, false);

            PInvoke.SendMessage(this.chatterinoHandle, PInvoke.WM_COPYDATA, 0, dataPtr);
            Marshal.FreeHGlobal(payloadPtr);
            Marshal.FreeHGlobal(dataPtr);
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
