using System;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using HidSharp;

internal class Program
{
    private static void Main()
    {
        var devices = DeviceList.Local.GetHidDevices(0x04D8, 0xF372).ToList();
        if (!devices.Any())
        {
            Console.Error.WriteLine("[Luxafor] No Luxafor device found (VID:0x04D8, PID:0xF372).\n");
            return;
        }
        Console.WriteLine($"[Luxafor] Found {devices.Count} device(s). Using device path: {devices[0].DevicePath}");

        var listener = new HttpListener();
        listener.Prefixes.Add("http://+:9123/");
        listener.Start();
        Console.WriteLine("[Luxafor] Listening on port 9123... (press Ctrl+C to exit)");

        while (true)
        {
            var context = listener.GetContext();
            var req     = context.Request;
            var res     = context.Response;

            if (req.HttpMethod == "POST" &&
                req.Url.AbsolutePath == "/api/v1.5/command/color")
            {
                using var reader = new System.IO.StreamReader(req.InputStream);
                var body = reader.ReadToEnd();
                var doc  = JsonDocument.Parse(body);

                if (doc.RootElement.TryGetProperty("color", out var colEl))
                {
                    var color = colEl.GetString();
                    Console.WriteLine($"[Luxafor] Received color command: {color}");
                    SetColor(devices[0], color);
                    res.StatusCode = 200;
                    var msg = Encoding.UTF8.GetBytes("OK");
                    res.OutputStream.Write(msg, 0, msg.Length);
                }
                else
                {
                    res.StatusCode = 400;
                }
            }
            else
            {
                res.StatusCode = 404;
            }

            res.Close();
        }
    }

    private static void SetColor(HidDevice dev, string? color)
    {
        if (string.IsNullOrEmpty(color)) return;
        try
        {
            using var stream = dev.Open();
            // 9-byte report: [ReportID=0x00, Mode=0x01, LED=0xFF (all), R, G, B, 0x00, 0x00, 0x00]
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
            var report = new byte[] { 0x00, 0x01, 0xFF, r, g, b, 0x00, 0x00, 0x00 };
            stream.Write(report, 0, report.Length);
            Console.WriteLine($"[Luxafor] Set color to {color} (R={r},G={g},B={b})");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Luxafor] Error writing to device: {ex.Message}");
        }
    }
}
