using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text.Json;
using NLog;

namespace RLMod.Core.Infrastructure.Parser;

public sealed class ProvinceParser
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    public static void DoParser()
    {
        Log.Info("Starting province parser...");

        string provincesFilePath = @"D:\SteamLibrary\steamapps\common\Hearts of Iron IV\map\provinces.bmp";
        string definitionFilePath = @"D:\SteamLibrary\steamapps\common\Hearts of Iron IV\map\definition.csv";
        string provincesJsonFilePath = @"D:\Worktable\hoi4_map_reader\State_reader\out\provinces.json";

        string[] csvLines = File.ReadAllLines(definitionFilePath);
        var provinces = new Dictionary<int, Province>(csvLines.Length);
        var colorToProvinceId = new Dictionary<Rgb, int>(csvLines.Length);

        Log.Info("Parsering {Path}", definitionFilePath);
        ProvinceScvParser(csvLines, provinces, colorToProvinceId);

        ProvinceBmpParser(provincesFilePath, provinces, colorToProvinceId);

        ProvinceJsonSerialization(provincesJsonFilePath, provinces);
        Log.Info("Province parser finish.");
    }

    private static void ProvinceScvParser(
        string[] csvLines,
        Dictionary<int, Province> provinces,
        Dictionary<Rgb, int> colorToProvinceId
    )
    {
        for (int i = 1; i < csvLines.Length; i++)
        {
            string[] csvFields = csvLines[i].Split(';');
            int provinceId = int.Parse(csvFields[0]);
            Rgb color = new(byte.Parse(csvFields[1]), byte.Parse(csvFields[2]), byte.Parse(csvFields[3]));

            Province province =
                new()
                {
                    Id = provinceId,
                    Color = color,
                    ProvinceType = csvFields[4],
                    IsCoastal = bool.Parse(csvFields[5]),
                    Terrain = csvFields[6],
                    ContinentId = int.Parse(csvFields[7]),
                };

            provinces[provinceId] = province;
            colorToProvinceId[color] = provinceId;
        }
    }

    private static void ProvinceBmpParser(
        string provincesFilePath,
        Dictionary<int, Province> provinces,
        Dictionary<Rgb, int> colorToProvinceId
    )
    {
        Log.Info("Parsering {provincesFilePath}", provincesFilePath);
        ConcurrentBag<(int provinceA, int provinceB)> edgePairs = [];

        using (Bitmap bmpProvinces = new(provincesFilePath))
        {
            int width = bmpProvinces.Width;
            int height = bmpProvinces.Height;
            Rectangle rect = new(0, 0, width, height);
            var data = bmpProvinces.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

            unsafe
            {
                byte* scan0 = (byte*)data.Scan0;
                int stride = data.Stride;

                Parallel.For(
                    0,
                    height,
                    i =>
                    {
                        byte* rowPtr = scan0 + i * stride;
                        for (int j = 0; j < width; j++)
                        {
                            byte* pixelPtr = rowPtr + j * 3;

                            Rgb currentColor = new(pixelPtr[2], pixelPtr[1], pixelPtr[0]);
                            int currentProvinceId = colorToProvinceId[currentColor];

                            if (j < width - 1)
                            {
                                byte* rightPtr = pixelPtr + 3;
                                Rgb rightColor = new(rightPtr[2], rightPtr[1], rightPtr[0]);
                                int rightProvinceId = colorToProvinceId[rightColor];
                                if (currentProvinceId != rightProvinceId)
                                {
                                    edgePairs.Add((currentProvinceId, rightProvinceId));
                                }
                            }

                            if (i < height - 1)
                            {
                                byte* downPtr = scan0 + (i + 1) * stride + j * 3;
                                Rgb downColor = new(downPtr[2], downPtr[1], downPtr[0]);
                                int downProvinceId = colorToProvinceId[downColor];
                                if (currentProvinceId != downProvinceId)
                                {
                                    edgePairs.Add((currentProvinceId, downProvinceId));
                                }
                            }
                        }
                    }
                );
            }
            bmpProvinces.UnlockBits(data);
        }

        foreach (var (provinceA, provinceB) in edgePairs)
        {
            provinces[provinceA].Adjacencies.Add(provinceB);
            provinces[provinceB].Adjacencies.Add(provinceA);
        }
    }

    private static void ProvinceJsonSerialization(
        string provincesJsonFilePath,
        Dictionary<int, Province> provinces
    )
    {
        string jsonString = JsonSerializer.Serialize(
            provinces,
            new JsonSerializerOptions { WriteIndented = true }
        );
        File.WriteAllText(provincesJsonFilePath, jsonString);
        Log.Info("Provinces data has been written to {provincesJsonFilePath}", provincesJsonFilePath);
    }
}
