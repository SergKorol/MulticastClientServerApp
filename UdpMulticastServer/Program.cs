using System.Net;
using System.Net.Sockets;
using System.Xml.Linq;

namespace UdpMulticastServer;

internal static class Server
{
    private static void Main()
    {
        var config = XDocument.Load("ServerConfig.xml");
        var multicastAddress = config.Root?.Element("MulticastAddress")?.Value;
        int.TryParse(config.Root?.Element("Port")?.Value, out int port);
        int.TryParse(config.Root?.Element("MinValue")?.Value, out int minValue);
        int.TryParse(config.Root?.Element("MaxValue")?.Value, out int maxValue);

        if (string.IsNullOrEmpty(multicastAddress) || port == 0 || minValue == 0 || maxValue == 0)
        {
            Console.WriteLine("Error: Check the correct of ServerConfig.xml file.");
            return;
        }

        using UdpClient udpClient = new UdpClient();
        if (!IPAddress.TryParse(multicastAddress, out IPAddress? multicastIpAddress))
        {
            Console.WriteLine("Error: Impossible to parse multicast address.");
            return;
        }

        try
        {
            udpClient.JoinMulticastGroup(multicastIpAddress);

            IPEndPoint remoteEndPoint = new IPEndPoint(multicastIpAddress, port);
            Random random = new Random();

            Console.WriteLine($"The Server is running. The Data sending to {multicastAddress}:{port}");

            while (true)
            {
                int randomValue = random.Next(minValue, maxValue);
                byte[] data = BitConverter.GetBytes(randomValue);

                udpClient.Send(data, data.Length, remoteEndPoint);

                Thread.Sleep(1000);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Internal Server Error: {ex.Message} {ex.StackTrace}");
        }
    }
}