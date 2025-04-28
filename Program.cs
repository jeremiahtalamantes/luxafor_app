using System;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HidSharp;

internal class Program
{
    // CancellationTokenSource for blink operations
    private static CancellationTokenSource? _blinkCts;

    private static void Main()
    {
        var devices = DeviceList.Local.GetHidDevices(0x04D8, 0xF372).ToList();
        if (!devices.Any())
        {
            Console.Error.WriteLine("[Luxafor] No Luxafor device found (VID:0x04D8, PID:0xF372).");
            return;
        }
        var dev = devices[0];
        Console.WriteLine($"[Luxafor] Using device: {dev.DevicePath}");

        var listener = new HttpListener();
        listener.Prefixes.Add("http://+:9123/");
        listener.Start();
        Console.WriteLine("[Luxafor] Listening on port 9123... (Ctrl+C to exit)");

        while (true)
        {
            var context = listener.GetContext();
            var req     = context.Request;
            var res     = context.Response;
            var body    = new System.IO.StreamReader(req.InputStream).ReadToEnd();

            if (req.HttpMethod == "POST" && req.Url.AbsolutePath == "/api/v1.5/command/blink")
            {
                // Cancel existing blink
                _blinkCts?.Cancel();

                var json     = JsonDocument.Parse(body).RootElement;
                var color    = json.GetProperty("color").GetString() ?? "off";
                var onMs     = json.GetProperty("onDuration").GetInt32();
                var offMs    = json.GetProperty("offDuration").GetInt32();
                var count    = json.GetProperty("count").GetInt32();
                var infinite = count <= 0;

                Console.WriteLine($"[Luxafor] Blink: {color}, on {onMs}ms/off {offMs}ms ×{(infinite ? "∞" : count.ToString())}");

                _blinkCts = new CancellationTokenSource();
                var token = _blinkCts.Token;
                Task.Run(() => BlinkLoop(dev, color, onMs, offMs, count, token), token);

                res.StatusCode = 200;
                res.OutputStream.Write(Encoding.UTF8.GetBytes("OK"));
            }
            else if (req.HttpMethod == "POST" && req.Url.AbsolutePath == "/api/v1.5/command/stop-blink")
            {
                // Stop any blinking
                _blinkCts?.Cancel();
                SetColor(dev, "off");
                res.StatusCode = 200;
                res.OutputStream.Write(Encoding.UTF8.GetBytes("OK"));
            }
            else if (req.HttpMethod == "POST" && req.Url.AbsolutePath == "/api/v1.5/command/color")
            {
                // Cancel blink on color change
                _blinkCts?.Cancel();

                var json  = JsonDocument.Parse(body).RootElement;
                var color = json.GetProperty("color").GetString() ?? "off";

                Console.WriteLine($"[Luxafor] Set color: {color}");
                SetColor(dev, color);

                res.StatusCode = 200;
                res.OutputStream.Write(Encoding.UTF8.GetBytes("OK"));
            }
            else
            {
                res.StatusCode = 404;
            }

            res.Close();
        }
    }

    // Blink loop supporting infinite and cancellation
    private static void BlinkLoop(HidDevice dev, string color, int onMs, int offMs, int count, CancellationToken token)
    {
        int i = 0;
        bool infinite = count <= 0;
        while (!token.IsCancellationRequested && (infinite || i < count))
        {
            SetColor(dev, color);
            if (token.WaitHandle.WaitOne(onMs)) break;
            SetColor(dev, "off");
            if (token.WaitHandle.WaitOne(offMs)) break;
            i++;
        }
    }

    // Send HID report to set color or off
    private static void SetColor(HidDevice dev, string color)
    {
        using var stream = dev.Open();
        byte r = 0, g = 0, b = 0;
        switch (color.ToLowerInvariant())
        {
            case "red":   r = 0xFF; break;
            case "green": g = 0xFF; break;
            case "blue":  b = 0xFF; break;
            case "off":   break;
            default:
                Console.Error.WriteLine($"[Luxafor] Unknown color '{color}', defaulting off.");
                break;
        }
        // HID report: [0x00, 0x01, 0xFF, R, G, B, 0x00, 0x00, 0x00]
        var report = new byte[] { 0x00, 0x01, 0xFF, r, g, b, 0x00, 0x00, 0x00 };
        stream.Write(report, 0, report.Length);
    }
}
