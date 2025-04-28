// At top of Program.cs:
using System.Threading.Tasks;
...
internal class Program
{
    // Make this shared so both endpoints can see it:
    private static CancellationTokenSource? _blinkCts;

    private static void Main()
    {
        // … your device setup …

        var listener = new HttpListener();
        listener.Prefixes.Add("http://+:9123/");
        listener.Start();

        while (true)
        {
            var ctx = listener.GetContext();
            var req = ctx.Request;
            var res = ctx.Response;
            var path = req.Url.AbsolutePath;
            string body = new System.IO.StreamReader(req.InputStream).ReadToEnd();

            if (req.HttpMethod == "POST" && path == "/api/v1.5/command/blink")
            {
                // Cancel any existing blink first
                _blinkCts?.Cancel();

                var json = JsonDocument.Parse(body).RootElement;
                string color = json.GetProperty("color").GetString()!;
                int onMs      = json.GetProperty("onDuration").GetInt32();
                int offMs     = json.GetProperty("offDuration").GetInt32();
                int count     = json.GetProperty("count").GetInt32();

                // Start a new blink task
                _blinkCts = new CancellationTokenSource();
                var token = _blinkCts.Token;
                Task.Run(() => BlinkLoop(dev, color, onMs, offMs, count, token), token);

                res.StatusCode = 200;
                res.OutputStream.Write(Encoding.UTF8.GetBytes("OK"));
            }
            else if (req.HttpMethod == "POST" && path == "/api/v1.5/command/stop-blink")
            {
                _blinkCts?.Cancel();             // signal the blink to end
                SetColor(dev, "off");            // ensure light is off
                res.StatusCode = 200;
                res.OutputStream.Write(Encoding.UTF8.GetBytes("OK"));
            }
            else if (req.HttpMethod == "POST" && path == "/api/v1.5/command/color")
            {
                // also cancel blink if someone wants a steady color
                _blinkCts?.Cancel();
                string color = JsonDocument.Parse(body)
                                          .RootElement
                                          .GetProperty("color")
                                          .GetString()!;
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

    private static void BlinkLoop(HidDevice dev, string color, int onMs, int offMs, int count, CancellationToken token)
    {
        // If count <= 0, treat as infinite
        int i = 0;
        while (!token.IsCancellationRequested && (count <= 0 || i < count))
        {
            SetColor(dev, color);
            if (token.WaitHandle.WaitOne(onMs)) break;
            SetColor(dev, "off");
            if (token.WaitHandle.WaitOne(offMs)) break;
            i++;
        }
    }

    // SetColor stays the same…
}
