using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StreamView
{
    abstract class ChatterinoMessage { }

    class CreatedWindow : ChatterinoMessage
    {
        public IntPtr Handle { get; set; }
    }

    class WindowShown : ChatterinoMessage { }

    class FixBounds : ChatterinoMessage { }

    static class ChatterinoInterop
    {
        public static Process RunChatterino(IntPtr parentHandle)
        {
            try {
                var chatterinoProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "/../chatterino.exe",


                        Arguments = $"--x-attach-split-to-window={parentHandle}",
                    }
                };
                chatterinoProcess.Start();
                return chatterinoProcess;
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Error starting chatterino", MessageBoxButtons.OK, MessageBoxIcon.Error);

                Environment.Exit(1);
                return null;
            }
        }

        public static void SetChatterinoDpi(IntPtr handle, Rectangle rectangle, int deviceDpi)
        {
            if (handle != IntPtr.Zero)
            {
                var hiword = deviceDpi | deviceDpi << 16;
                var rect = new PInvoke.RECT(rectangle);

                var ptr = Marshal.AllocHGlobal(Marshal.SizeOf(rect));
                Marshal.StructureToPtr(rect, ptr, false);

                PInvoke.SendMessage(handle, PInvoke.WM_DPICHANGED, hiword, ptr);
                Marshal.FreeHGlobal(ptr);
            }
        }

        public static void ResizeChatterino(IntPtr handle, Rectangle rect)
        {
            if (handle != IntPtr.Zero)
            {
                PInvoke.SetWindowPos(handle, IntPtr.Zero, rect.X, rect.Y, rect.Width, rect.Height,
                    PInvoke.SetWindowPosFlags.DoNotActivate |
                    PInvoke.SetWindowPosFlags.DoNotChangeOwnerZOrder |
                    PInvoke.SetWindowPosFlags.DoNotCopyBits);
            }
        }

        public static void SetChatterinoChannel(IntPtr handle, string twitchChannelName)
        {
            sendToChatterino(handle, new Dictionary<string, string>
            {
                { "type" , "set-channel" },
                { "provider" , "twitch" },
                { "channel-name" , twitchChannelName },
            });
        }

        public static void SetReadyToShow(IntPtr handle, IntPtr hostHandle)
        {
            dynamic payload = new Dictionary<string, string>
            {
                { "type" , "request-show" },
                { "callback-handle" , hostHandle.ToInt64().ToString() },
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

            PInvoke.SendMessage(handle, PInvoke.WM_COPYDATA, 0, dataPtr);
            Marshal.FreeHGlobal(payloadPtr);
            Marshal.FreeHGlobal(dataPtr);
        }

        public static ChatterinoMessage DecodeChatterinoMessage(Message m)
        {
            PInvoke.COPYDATASTRUCT cps = (PInvoke.COPYDATASTRUCT)Marshal.PtrToStructure(m.LParam, typeof(PInvoke.COPYDATASTRUCT));
            byte[] buffer = new byte[cps.cbData];
            Marshal.Copy(cps.lpData, buffer, 0, cps.cbData);

            var data = Encoding.UTF8.GetString(buffer, 0, buffer[buffer.Length - 1] == 0 ? buffer.Length - 1 : buffer.Length);
            var root = JObject.Parse(data);
            switch (root.Value<string>("type"))
            {
                case "created-window":
                    if (long.TryParse(root.Value<string>("window-id"), out var hwnd))
                    {
                        return new CreatedWindow
                        {
                            Handle = new IntPtr(hwnd)
                        };
                    }
                    break;
                case "shown":
                    return new WindowShown();
                case "fix-bounds":
                    return new FixBounds();
            }

            return null;
        }

        static void sendToChatterino(IntPtr handle, object payload)
        {
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

            PInvoke.SendMessage(handle, PInvoke.WM_COPYDATA, 0, dataPtr);
            Marshal.FreeHGlobal(payloadPtr);
            Marshal.FreeHGlobal(dataPtr);
        }
    }
}
