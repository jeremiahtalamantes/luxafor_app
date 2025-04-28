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
    // Used to cancel an ongoing blink operation
    private static CancellationTokenSource? _blinkCts;

    private static void Main()
    {
        // Discover the first attached Luxafor device
        var devices = DeviceList.Local.GetHidDevices(0x04D8, 0xF372).ToList();
        if (!devices.Any())
        {
            Console.Error.WriteLine("[Luxafor] No Luxafor device found (VID:0x04D8, PID:0xF372).");
            return;
        }
        var dev = devices[0];
        Console.WriteLine($"[Luxafor] Using device: {dev.DevicePath}");

        // Start HTTP listener
        var listener = new HttpListener();
        listener.Prefixes.Add("http://+:9123/");
        listener.Start();
        Console.WriteLine("[Luxafor] Listening on port 9123... (Ctrl+C to exit)");

        while (true)
        {
            var context = listener.GetContext();
            var req     = context.Request;
            var res     = context.Response;

            // --- CORS support ---
            res.Headers.Add("Access-Control-Allow-Origin", "*");
            res.Headers.Add("Access-Control-Allow-Methods", "POST, OPTIONS");
            res.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

            // Handle preflight
            if (req.HttpMethod == "OPTIONS")
            {
                res.StatusCode = 204; // No Content
                res.Close();
                continue;
            }

            // Read request body
            var body = new System.IO.StreamReader(req.InputStream).ReadToEnd();

            // Blink endpoint
            if (req.HttpMethod == "POST" && req.Url.AbsolutePath == "/api/v1.5/command/blink")
            {
                _blinkCts?.Cancel();  // stop any existing blink

                var json     = JsonDocument.Parse(body).RootElement;
                var color    = json.GetProperty("color").GetString() ?? "off";
                var onMs     = json.GetProperty("onDuration").GetInt32();
                var offMs    = json.GetProperty("offDuration").GetInt32();
                var count    = json.GetProperty("count").GetInt32();

                Console.WriteLine($"[Luxafor] Blink: {color}, on {onMs}ms/off {offMs}ms ×{(count <= 0 ? \"∞\" : count.ToString())}");

                _blinkCts = new CancellationTokenSource();
                Task.Run(() => BlinkLoop(dev, color, onMs, offMs, count, _blinkCts.Token), _blinkCts.Token);

                res.StatusCode = 200;
                res.OutputStream.Write(Encoding.UTF8.GetBytes("OK"));
            }
            // Stop-blink endpoint
            else if (req.HttpMethod == "POST" && req.Url.AbsolutePath == "/api/v1.5/command/stop-blink")
            {
                _blinkCts?.Cancel();
                SetColor(dev, "off");

                res.StatusCode = 200;
                res.OutputStream.Write(Encoding.UTF8.GetBytes("OK"));
            }
            // Steady color endpoint
            else if (req.HttpMethod == "POST" && req.Url.AbsolutePath == "/api/v1.5/command/color")
            {
                _blinkCts?.Cancel();

                var color = JsonDocument.Parse(body)
                                       .RootElement
                                       .GetProperty("color")
                                       .GetString() ?? "off";

                Console.WriteLine($"[Luxafor] Set color: {color}");
                SetColor(dev, color);

                res.StatusCode = 200;
                res.OutputStream.Write(Encoding.UTF8.GetBytes("OK"));
            }
            // Not found
            else
            {
                res.StatusCode = 404;
            }

            res.Close();
        }
    }

    // Runs the blink loop, respects cancellation and infinite count
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

    // Sends the 9-byte HID report to set the color
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
        // HID report: [ReportID=0x00, Mode=0x01, LED mask=0xFF, R, G, B, 0x00, 0x00, 0x00]
        var report = new byte[] { 0x00, 0x01, 0xFF, r, g, b, 0x00, 0x00, 0x00 };
        stream.Write(report, 0, report.Length);
    }
}
