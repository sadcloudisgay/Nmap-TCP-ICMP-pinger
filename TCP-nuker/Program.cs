using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

class IcmpTcpPinger
{
    private static string targetIpAddress = "1.1.1.1";
    private static int icmpPingIntervalMilliseconds = 200;
    private static int NmapPingIntervalMilliseconds = 1; // 1 ms delay for Nmap-style scan (default)
    private static int tcpPingIntervalMilliseconds = 200; // 200 ms interval for TCP pings
    private static Timer pingTimer;
    private static int pingCount = 0;
    private static int failedPingCount = 0;
    private static long totalResponseTime = 0;
    private static int tcpPort = 80; // Default TCP port
    private static int nmapPort = 1; // Start from port 1 for Nmap-style scan
    private static StringBuilder successfulPorts = new StringBuilder();

    static async Task Main(string[] args)
    {
        Console.WriteLine("Enter which protocol you would like to use:");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("1. ICMP");
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine("2. TCP");
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("3. Nmap-style scan (ping all ports from 1 to 65535)");
        Console.ResetColor();

        string protocolChoice = Console.ReadLine();

        Console.Write("Enter the IP (or press Enter to ping the closest Cloudflare server to you): ");
        string inputIpAddress = Console.ReadLine();

        if (!string.IsNullOrWhiteSpace(inputIpAddress))
        {
            targetIpAddress = inputIpAddress;
        }

        if (protocolChoice == "2")
        {
            Console.WriteLine("Enter the port number to ping (or press Enter to randomize): ");
            string portInput = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(portInput))
            {
                if (int.TryParse(portInput, out int port))
                {
                    tcpPort = port;
                }
            }
        }
        else if (protocolChoice == "3")
        {
            // Start from port 1 for Nmap-style scan
            nmapPort = 1;
        }

        UpdateWindowTitle();

        pingTimer = new Timer(protocolChoice == "3" ? NmapPingIntervalMilliseconds : icmpPingIntervalMilliseconds);
        pingTimer.Elapsed += async (sender, e) => await SendPingAsync(protocolChoice);
        pingTimer.AutoReset = true;
        pingTimer.Enabled = true;

        Console.WriteLine("Press Enter to exit...");
        await Task.Run(() => Console.ReadLine());

        // Write successful ports to a file after exiting
        File.WriteAllText("successful_ports.txt", successfulPorts.ToString());
        Console.WriteLine("Successful ports written to 'successful_ports.txt'");
    }

    private static async Task SendPingAsync(string protocolChoice)
    {
        if (protocolChoice == "1")
        {
            await PerformIcmpPingAsync();
        }
        else if (protocolChoice == "2")
        {
            await SendTcpPingAsync();
        }
        else if (protocolChoice == "3")
        {
            await SendNmapStylePingAsync();
        }
    }

    private static async Task PerformIcmpPingAsync()
    {
        try
        {
            using (Ping pingSender = new Ping())
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();

                PingReply reply = await pingSender.SendPingAsync(targetIpAddress);

                stopwatch.Stop();

                if (stopwatch.ElapsedMilliseconds > 1000)
                {
                    failedPingCount++;
                    Console.WriteLine($"Failed to ping {targetIpAddress}");
                }
                else
                {
                    DisplayPingResult(targetIpAddress, "ICMP", stopwatch.ElapsedMilliseconds);
                }
            }
        }
        catch (PingException ex)
        {
            Console.WriteLine($"PingException: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    private static async Task SendTcpPingAsync()
    {
        try
        {
            using (TcpClient client = new TcpClient())
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();

                var connectTask = client.ConnectAsync(targetIpAddress, tcpPort);
                var timeoutTask = Task.Delay(1000); // Adjust the timeout duration (in milliseconds)

                await Task.WhenAny(connectTask, timeoutTask);

                stopwatch.Stop();

                if (!connectTask.IsCompleted)
                {
                    failedPingCount++;
                    Console.WriteLine($"Failed to ping {targetIpAddress}:{tcpPort}");
                }
                else
                {
                    DisplayPingResult(targetIpAddress, "TCP", stopwatch.ElapsedMilliseconds);
                    successfulPorts.AppendLine(tcpPort.ToString()); // Add successful port to the list
                }
            }
        }
        catch (SocketException ex)
        {
            Console.WriteLine($"SocketException: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }

        UpdateWindowTitle();
    }

    private static async Task SendNmapStylePingAsync()
    {
        try
        {
            using (TcpClient client = new TcpClient())
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();

                var connectTask = client.ConnectAsync(targetIpAddress, nmapPort);

                // Fixed delay of 1 millisecond
                var timeoutTask = Task.Delay(NmapPingIntervalMilliseconds);

                await Task.WhenAny(connectTask, timeoutTask);

                if (!connectTask.IsCompleted)
                {
                    failedPingCount++;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write($"Failed to ping {targetIpAddress} ");
                    Console.ResetColor();
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.Write($"| -----> {nmapPort}\n");
                    Console.ResetColor();
                }
                else
                {
                    DisplayPingResult(targetIpAddress, "NMAP", stopwatch.ElapsedMilliseconds);
                    successfulPorts.AppendLine(nmapPort.ToString()); // Add successful port to the list
                }
            }
        }
        catch (SocketException ex)
        {
            Console.WriteLine($"SocketException: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }

        nmapPort++; // Increment the NMAP port

        UpdateWindowTitle();
    }

    private static void DisplayPingResult(string target, string protocol, long responseTime)
    {
        var textColor = protocol == "ICMP" ? ConsoleColor.Cyan : (protocol == "TCP" ? ConsoleColor.Magenta : ConsoleColor.Blue);
        Console.ForegroundColor = textColor;
        Console.Write($"Sent {protocol} ping ");
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write("to ");
        Console.ForegroundColor = textColor;
        Console.Write(target);

        if (responseTime > 300)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write(" - High Latency");
        }
        else if (responseTime > 150)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write(" - Medium Latency");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(" - Low Latency");
        }

        Console.WriteLine($" - Response time: {responseTime} ms");

        totalResponseTime += responseTime;
        pingCount++;

        double averageDelay = (double)totalResponseTime / pingCount;
        UpdateWindowTitle();

        Console.ResetColor();

        Console.WriteLine();
    }

    private static void UpdateWindowTitle()
    {
        string title;
        if (tcpPort <= 65535)
        {
            title = $"Average Delay: {(double)totalResponseTime / pingCount:F2} ms - Pings reached: {pingCount} - Failed Pings: {failedPingCount} - Port: {tcpPort}";
        }
        else if (nmapPort <= 65535)
        {
            title = $"NMAP Scan - Pings Sent: {pingCount} - Failed Pings: {failedPingCount}";
        }
        else
        {
            title = $"Port scan completed - Failed Pings: {failedPingCount}";
        }
        Console.Title = title;
    }
}
