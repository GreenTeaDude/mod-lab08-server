using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Lab08
{
    public class Client
    {
        public int Id { get; }
        public double ArrivalTime { get; }

        public Client(int id, double arrivalTime)
        {
            Id = id;
            ArrivalTime = arrivalTime;
        }
    }

    public class Server
    {
        private readonly int channelsCount;
        private readonly double serviceRate;
        private readonly Random random;
        private readonly double[] busyUntil;

        public int TotalRequests { get; private set; }
        public int ServedRequests { get; private set; }
        public int RejectedRequests { get; private set; }
        public double BusyTimeSum { get; private set; }

        public Server(int channelsCount, double serviceRate, int seed = 1)
        {
            this.channelsCount = channelsCount;
            this.serviceRate = serviceRate;
            random = new Random(seed);
            busyUntil = new double[channelsCount];
        }

        public void Process(Client client)
        {
            TotalRequests++;

            int freeChannel = FindFreeChannel(client.ArrivalTime);

            if (freeChannel == -1)
            {
                RejectedRequests++;
                return;
            }

            double serviceTime = GetExponential(serviceRate);
            busyUntil[freeChannel] = client.ArrivalTime + serviceTime;
            BusyTimeSum += serviceTime;
            ServedRequests++;
        }

        private int FindFreeChannel(double currentTime)
        {
            for (int i = 0; i < channelsCount; i++)
            {
                if (busyUntil[i] <= currentTime)
                {
                    return i;
                }
            }

            return -1;
        }

        private double GetExponential(double rate)
        {
            double value = random.NextDouble();

            if (value == 0)
            {
                value = 0.000001;
            }

            return -Math.Log(value) / rate;
        }
    }

    public class ExperimentResult
    {
        public double Lambda { get; set; }
        public double ServiceRate { get; set; }
        public int Channels { get; set; }

        public int TotalRequests { get; set; }
        public int ServedRequests { get; set; }
        public int RejectedRequests { get; set; }

        public double RefusalProbability { get; set; }
        public double RelativeThroughput { get; set; }
        public double AbsoluteThroughput { get; set; }
        public double AverageBusyChannels { get; set; }
        public double IdleProbability { get; set; }

        public double TheoreticalRefusalProbability { get; set; }
        public double TheoreticalRelativeThroughput { get; set; }
        public double TheoreticalAbsoluteThroughput { get; set; }
        public double TheoreticalAverageBusyChannels { get; set; }
        public double TheoreticalIdleProbability { get; set; }
    }

    public class Program
    {
        private const int ChannelsCount = 4;
        private const double ServiceRate = 1.0;
        private const double SimulationTime = 10000.0;

        public static void Main(string[] args)
        {
            Directory.CreateDirectory("result");

            List<ExperimentResult> results = new List<ExperimentResult>();

            for (double lambda = 0.2; lambda <= 4.0; lambda += 0.2)
            {
                ExperimentResult result = RunExperiment(lambda, ServiceRate, ChannelsCount);
                results.Add(result);
            }

            SaveReport(results, "result/results.txt");

            CreateChart(results, "result/p-1.png", "Вероятность отказа", x => x.RefusalProbability);
            CreateChart(results, "result/p-2.png", "Относительная пропускная способность", x => x.RelativeThroughput);
            CreateChart(results, "result/p-3.png", "Абсолютная пропускная способность", x => x.AbsoluteThroughput);
            CreateChart(results, "result/p-4.png", "Среднее число занятых каналов", x => x.AverageBusyChannels);
            CreateChart(results, "result/p-5.png", "Вероятность простоя системы", x => x.IdleProbability);

            Console.WriteLine("Simulation finished.");
            Console.WriteLine("Results saved to result/results.txt");
            Console.WriteLine("Charts saved to result/p-1.png ... result/p-5.png");
        }

        private static ExperimentResult RunExperiment(double lambda, double serviceRate, int channelsCount)
        {
            Random random = new Random((int)(lambda * 1000));
            Server server = new Server(channelsCount, serviceRate, (int)(lambda * 2000));

            double currentTime = 0.0;
            int clientId = 0;

            while (currentTime < SimulationTime)
            {
                currentTime += GetExponential(random, lambda);

                Client client = new Client(clientId, currentTime);
                server.Process(client);

                clientId++;
            }

            double refusalProbability = (double)server.RejectedRequests / server.TotalRequests;
            double relativeThroughput = (double)server.ServedRequests / server.TotalRequests;
            double absoluteThroughput = server.ServedRequests / SimulationTime;
            double averageBusyChannels = server.BusyTimeSum / SimulationTime;
            double idleProbability = 1.0 - averageBusyChannels / channelsCount;

            double traffic = lambda / serviceRate;
            double theoreticalRefusal = ErlangB(channelsCount, traffic);
            double theoreticalRelative = 1.0 - theoreticalRefusal;
            double theoreticalAbsolute = lambda * theoreticalRelative;
            double theoreticalBusy = traffic * theoreticalRelative;
            double theoreticalIdle = IdleProbability(channelsCount, traffic);

            return new ExperimentResult
            {
                Lambda = lambda,
                ServiceRate = serviceRate,
                Channels = channelsCount,
                TotalRequests = server.TotalRequests,
                ServedRequests = server.ServedRequests,
                RejectedRequests = server.RejectedRequests,
                RefusalProbability = refusalProbability,
                RelativeThroughput = relativeThroughput,
                AbsoluteThroughput = absoluteThroughput,
                AverageBusyChannels = averageBusyChannels,
                IdleProbability = idleProbability,
                TheoreticalRefusalProbability = theoreticalRefusal,
                TheoreticalRelativeThroughput = theoreticalRelative,
                TheoreticalAbsoluteThroughput = theoreticalAbsolute,
                TheoreticalAverageBusyChannels = theoreticalBusy,
                TheoreticalIdleProbability = theoreticalIdle
            };
        }

        private static double GetExponential(Random random, double rate)
        {
            double value = random.NextDouble();

            if (value == 0)
            {
                value = 0.000001;
            }

            return -Math.Log(value) / rate;
        }

        private static double ErlangB(int channels, double traffic)
        {
            double sum = 0.0;

            for (int k = 0; k <= channels; k++)
            {
                sum += Math.Pow(traffic, k) / Factorial(k);
            }

            double numerator = Math.Pow(traffic, channels) / Factorial(channels);

            return numerator / sum;
        }

        private static double IdleProbability(int channels, double traffic)
        {
            double sum = 0.0;

            for (int k = 0; k <= channels; k++)
            {
                sum += Math.Pow(traffic, k) / Factorial(k);
            }

            return 1.0 / sum;
        }

        private static double Factorial(int number)
        {
            double result = 1.0;

            for (int i = 2; i <= number; i++)
            {
                result *= i;
            }

            return result;
        }

        private static void SaveReport(List<ExperimentResult> results, string path)
        {
            StringBuilder builder = new StringBuilder();

            builder.AppendLine("Lab08. Моделирование СМО клиент-сервер");
            builder.AppendLine("Многоканальная система массового обслуживания с отказами");
            builder.AppendLine();
            builder.AppendLine($"Количество каналов обслуживания: {ChannelsCount}");
            builder.AppendLine($"Интенсивность обслуживания: {ServiceRate}");
            builder.AppendLine($"Время моделирования: {SimulationTime}");
            builder.AppendLine();
            builder.AppendLine("Показатели:");
            builder.AppendLine("Pотк - вероятность отказа");
            builder.AppendLine("Q - относительная пропускная способность");
            builder.AppendLine("A - абсолютная пропускная способность");
            builder.AppendLine("K - среднее число занятых каналов");
            builder.AppendLine("P0 - вероятность простоя системы");
            builder.AppendLine();
            builder.AppendLine("lambda; total; served; rejected; Pотк; Q; A; K; P0; Pотк_теор; Q_теор; A_теор; K_теор; P0_теор");

            foreach (ExperimentResult result in results)
            {
                builder.AppendLine(
                    $"{Format(result.Lambda)}; " +
                    $"{result.TotalRequests}; " +
                    $"{result.ServedRequests}; " +
                    $"{result.RejectedRequests}; " +
                    $"{Format(result.RefusalProbability)}; " +
                    $"{Format(result.RelativeThroughput)}; " +
                    $"{Format(result.AbsoluteThroughput)}; " +
                    $"{Format(result.AverageBusyChannels)}; " +
                    $"{Format(result.IdleProbability)}; " +
                    $"{Format(result.TheoreticalRefusalProbability)}; " +
                    $"{Format(result.TheoreticalRelativeThroughput)}; " +
                    $"{Format(result.TheoreticalAbsoluteThroughput)}; " +
                    $"{Format(result.TheoreticalAverageBusyChannels)}; " +
                    $"{Format(result.TheoreticalIdleProbability)}");
            }

            File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
        }

        private static string Format(double value)
        {
            return value.ToString("0.0000", CultureInfo.InvariantCulture);
        }

        private static void CreateChart(
            List<ExperimentResult> results,
            string path,
            string title,
            Func<ExperimentResult, double> selector)
        {
            int width = 900;
            int height = 600;

            byte[] image = new byte[width * height * 3];

            for (int i = 0; i < image.Length; i++)
            {
                image[i] = 255;
            }

            int left = 80;
            int right = 40;
            int top = 50;
            int bottom = 70;

            DrawLine(image, width, height, left, top, left, height - bottom, 0, 0, 0);
            DrawLine(image, width, height, left, height - bottom, width - right, height - bottom, 0, 0, 0);

            double maxX = results[results.Count - 1].Lambda;
            double maxY = 0.0;

            foreach (ExperimentResult result in results)
            {
                double value = selector(result);

                if (value > maxY)
                {
                    maxY = value;
                }
            }

            if (maxY <= 0.0)
            {
                maxY = 1.0;
            }

            int previousX = 0;
            int previousY = 0;
            bool hasPrevious = false;

            foreach (ExperimentResult result in results)
            {
                double xValue = result.Lambda;
                double yValue = selector(result);

                int x = left + (int)((xValue / maxX) * (width - left - right));
                int y = height - bottom - (int)((yValue / maxY) * (height - top - bottom));

                DrawCircle(image, width, height, x, y, 4, 220, 0, 0);

                if (hasPrevious)
                {
                    DrawLine(image, width, height, previousX, previousY, x, y, 220, 0, 0);
                }

                previousX = x;
                previousY = y;
                hasPrevious = true;
            }

            SavePpmAsPngName(image, width, height, path);
        }

        private static void DrawCircle(
            byte[] image,
            int width,
            int height,
            int centerX,
            int centerY,
            int radius,
            byte red,
            byte green,
            byte blue)
        {
            for (int y = centerY - radius; y <= centerY + radius; y++)
            {
                for (int x = centerX - radius; x <= centerX + radius; x++)
                {
                    int dx = x - centerX;
                    int dy = y - centerY;

                    if (dx * dx + dy * dy <= radius * radius)
                    {
                        SetPixel(image, width, height, x, y, red, green, blue);
                    }
                }
            }
        }

        private static void DrawLine(
            byte[] image,
            int width,
            int height,
            int x0,
            int y0,
            int x1,
            int y1,
            byte red,
            byte green,
            byte blue)
        {
            int dx = Math.Abs(x1 - x0);
            int dy = Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int error = dx - dy;

            while (true)
            {
                SetPixel(image, width, height, x0, y0, red, green, blue);

                if (x0 == x1 && y0 == y1)
                {
                    break;
                }

                int e2 = 2 * error;

                if (e2 > -dy)
                {
                    error -= dy;
                    x0 += sx;
                }

                if (e2 < dx)
                {
                    error += dx;
                    y0 += sy;
                }
            }
        }

        private static void SetPixel(
            byte[] image,
            int width,
            int height,
            int x,
            int y,
            byte red,
            byte green,
            byte blue)
        {
            if (x < 0 || x >= width || y < 0 || y >= height)
            {
                return;
            }

            int index = (y * width + x) * 3;
            image[index] = red;
            image[index + 1] = green;
            image[index + 2] = blue;
        }

        private static void SavePpmAsPngName(byte[] image, int width, int height, string path)
        {
            using FileStream stream = new FileStream(path, FileMode.Create);
            using BinaryWriter writer = new BinaryWriter(stream);

            writer.Write(Encoding.ASCII.GetBytes($"P6\n{width} {height}\n255\n"));
            writer.Write(image);
        }
    }
}
