using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Xml.Linq;

namespace UdpMulticastClient;

internal static class Client
{
    private static readonly ConcurrentQueue<int> Quotes = new();
    static long _lostPackets;
    static long _totalReceivedPackets;
    static bool _isReceiving = true;
    static readonly int MaxQuotesSize = 1000;
    private static readonly object LockObject = new();

    static void Main()
    {
        var config = XDocument.Load("ClientConfig.xml");
        var multicastAddress = config.Root?.Element("MulticastAddress")?.Value;
        int.TryParse(config.Root?.Element("Port")?.Value, out var port);

        Thread receiveThread = new Thread(() => ReceiveQuotes(multicastAddress, port));
        Thread processThread = new Thread(ProcessData);
        Thread processControlThread = new Thread(ProcessStatistics);

        receiveThread.Start();
        processThread.Start();
        processControlThread.Start();

        receiveThread.Join();
        processThread.Join();
        processControlThread.Join();
    }

    private static void ReceiveQuotes(string? multicastAddress, int port)
    {
        try
        {
            UdpClient udpClient = new UdpClient();
            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, port);
            udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udpClient.ExclusiveAddressUse = false;
            udpClient.Client.Bind(localEndPoint);

            Console.WriteLine("Joining to multicast group...");
            IPAddress.TryParse(multicastAddress, out IPAddress? multicastIpAddress);
            if (multicastIpAddress != null) udpClient.JoinMulticastGroup(multicastIpAddress);
            Console.WriteLine("Successfully joined multicast group!");

            while (true)
            {
                if (!_isReceiving)
                {
                    Thread.Sleep(100);
                    continue;
                }

                try
                {
                    byte[] data = udpClient.Receive(ref localEndPoint);
                    Console.WriteLine($"Received packet size: {data.Length} bytes");
                    int message = BitConverter.ToInt32(data, 0);
                    Console.WriteLine($"Received data: {message}");

                    if (Quotes.Count >= MaxQuotesSize)
                    {
                        Quotes.TryDequeue(out _);
                    }

                    Quotes.Enqueue(message);
                    Interlocked.Increment(ref _totalReceivedPackets);
                }
                catch (SocketException ex)
                {
                    Console.WriteLine($"SocketException: {ex.Message}");
                    Interlocked.Increment(ref _lostPackets);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in ReceiveQuotes: {ex.Message}");
        }
    }

    private static void ProcessData()
    {
        while (true)
        {
            Thread.Sleep(500);
        }
    }

    private static void ProcessStatistics()
    {
        while (true)
        {
            var key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.Enter)
            {
                lock (LockObject)
                {
                    if (_isReceiving)
                    {
                        Console.WriteLine("\nSuspending receive packets...");
                        _isReceiving = false;
                    }
                    else
                    {
                        Console.WriteLine("\nResuming receive packets...");
                        _isReceiving = true;
                    }
                }

                if (Quotes.Count > 0)
                {
                    var quotesArray = Quotes.ToArray();
                    double mean = quotesArray.Average();
                    double stdDev = Math.Sqrt(quotesArray.Average(v => Math.Pow(v - mean, 2)));
                    var mode = quotesArray.GroupBy(x => x)
                        .OrderByDescending(g => g.Count())
                        .Select(g => g.Key)
                        .FirstOrDefault();
                    double median = GetMedian(quotesArray);

                    Console.Clear();
                    Console.WriteLine($"Average: {mean}");
                    Console.WriteLine($"Standard deviation: {stdDev}");
                    Console.WriteLine($"Mode: {mode}");
                    Console.WriteLine($"Median: {median}");
                    Console.WriteLine($"Lost packets: {_lostPackets}");
                    Console.WriteLine($"Total received packets: {_totalReceivedPackets}");
                }
                else
                {
                    Console.Clear();
                    Console.WriteLine("Data did not receive any packets!");
                }
            }

            Thread.Sleep(500); 
        }
    }

    private static double GetMedian(int[] numbers)
    {
        Array.Sort(numbers);
        int count = numbers.Length;
        if (count % 2 == 0)
        {
            return (numbers[count / 2 - 1] + numbers[count / 2]) / 2.0;
        }
        else
        {
            return numbers[count / 2];
        }
    }
}