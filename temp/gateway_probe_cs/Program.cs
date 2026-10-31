using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        var port = 51006;
        var gatewayScript = @"D:\YASIN\Gateway\LocalWhatsApp\src\server.mjs";

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "node",
                Arguments = $"\"{gatewayScript}\"",
                WorkingDirectory = System.IO.Path.GetDirectoryName(gatewayScript)!,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        process.StartInfo.Environment["LUMAR_LOCAL_GATEWAY_PORT"] = port.ToString();
        process.StartInfo.Environment["LUMAR_LOCAL_TEST_RECIPIENT"] = "967775106545";
        process.StartInfo.Environment["LUMAR_LOCAL_MEDIA_ORIGIN"] = "https://public-backend.example.com";

        Console.WriteLine("START");
        process.Start();

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (!timeout.IsCancellationRequested)
        {
            try
            {
                using var response = await client.GetAsync($"http://127.0.0.1:{port}/health", timeout.Token);
                Console.WriteLine($"STATUS:{(int)response.StatusCode}");
                if (response.StatusCode == System.Net.HttpStatusCode.OK || response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERR:{ex.GetType().Name}:{ex.Message}");
            }

            await Task.Delay(200, timeout.Token);
        }

        Console.WriteLine("PROCESSLIFETIME:" + (!process.HasExited ? "ALIVE" : $"EXITED:{process.ExitCode}"));
        Console.WriteLine("STDOUT:\n" + process.StandardOutput.ReadToEnd());
        Console.WriteLine("STDERR:\n" + process.StandardError.ReadToEnd());

        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(5000);
        }
    }
}
