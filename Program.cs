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
        var listener = new HttpListener();
        listener.Prefixes.Add("http://+:9123/");
        listener.Start();
        Console.WriteLine("[Luxafor] Listening on port 9123...");

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
                    SetColor(colEl.GetString());
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

    private static void SetColor(string? color)
    {
        var dev = DeviceList.Local
                    .GetHidDevices(0x04D8, 0xF372)
                    .FirstOrDefault();
        if (dev is null || string.IsNullOrEmpty(color)) return;

        using var stream = dev.Open();
        byte r = 0, g = 0, b = 0;
        switch (color.ToLowerInvariant())
        {
            case "red":   r = 0xFF; break;
            case "green": g = 0xFF; break;
            case "blue":  b = 0xFF; break;
            case "off":   break;
        }

        // Report: [ReportID=0x00, R, G, B]
        stream.Write(new byte[] { 0x00, r, g, b }, 0, 4);
        Console.WriteLine($"[Luxafor] Set color to {color}");
    }
}
