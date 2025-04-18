using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;
using NLog;
using RLMod.Core.Extensions;
using RLMod.Core.Models.Map;

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
            Debug.Assert(csvFields.Length == 8, "CSV fields length is not 8");

            int provinceId = int.TryParse(csvFields[0], out int province) ? province : 0;
            var color = new Rgb(
                byte.TryParse(csvFields[1], out byte r) ? r : (byte)0,
                byte.TryParse(csvFields[2], out byte g) ? g : (byte)0,
                byte.TryParse(csvFields[3], out byte b) ? b : (byte)0
            );

            var provinceData = new Province
            {
                Id = provinceId,
                Color = color,
                Type = csvFields[4].EqualsIgnoreCase("land") ? ProvinceType.Land : ProvinceType.Sea,
                IsCoastal = bool.TryParse(csvFields[5], out bool isCoastal) && isCoastal,
                Terrain = csvFields[6],
                ContinentId = int.TryParse(csvFields[7], out int continentId) ? continentId : 0
            };

            provinces[provinceId] = provinceData;
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
