using System;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using HidSharp;

internal class Program
{
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
        Console.WriteLine("[Luxafor] Listening on port 9123...");

        while (true)
        {
            var context = listener.GetContext();
            var req     = context.Request;
            var res     = context.Response;

            using var reader = new System.IO.StreamReader(req.InputStream);
            var body = reader.ReadToEnd();

            if (req.HttpMethod == "POST" &&
                req.Url.AbsolutePath == "/api/v1.5/command/color")
            {
                var color = JsonDocument.Parse(body)
                                        .RootElement
                                        .GetProperty("color")
                                        .GetString();
                Console.WriteLine($"[Luxafor] Set color: {color}");
                SetColor(dev, color);
                res.StatusCode = 200;
                res.OutputStream.Write(Encoding.UTF8.GetBytes("OK"));
            }
            else if (req.HttpMethod == "POST" &&
                     req.Url.AbsolutePath == "/api/v1.5/command/blink")
            {
                var json = JsonDocument.Parse(body).RootElement;
                var color     = json.GetProperty("color").GetString();
                var onMs      = json.GetProperty("onDuration").GetInt32();
                var offMs     = json.GetProperty("offDuration").GetInt32();
                var count     = json.GetProperty("count").GetInt32();
                Console.WriteLine($"[Luxafor] Blink: {color}, on {onMs}ms/off {offMs}ms ×{count}");
                Blink(dev, color, onMs, offMs, count);
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

    private static void SetColor(HidDevice dev, string? color)
    {
        if (string.IsNullOrEmpty(color)) return;
        using var stream = dev.Open();
        byte r = 0, g = 0, b = 0;
        switch (color.ToLowerInvariant())
        {
            case "red":   r = 0xFF; break;
            case "green": g = 0xFF; break;
            case "blue":  b = 0xFF; break;
        }
        var report = new byte[] { 0x00, 0x01, 0xFF, r, g, b, 0, 0, 0 };
        stream.Write(report, 0, report.Length);
    }

    private static void Blink(HidDevice dev, string? color, int onMs, int offMs, int count)
    {
        for (int i = 0; i < count; i++)
        {
            SetColor(dev, color);
            Thread.Sleep(onMs);
            SetColor(dev, "off");
            Thread.Sleep(offMs);
        }
    }
}
