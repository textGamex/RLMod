using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using NLog;
using RLMod.Core.Extensions;
using RLMod.Core.Models.Map;

namespace RLMod.Core.Infrastructure.Parser;

public sealed class ProvinceParser
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    public static bool TryParse(string gameRootPath, out IReadOnlyDictionary<int, Province> provinces)
    {
        return TryParse(
            Path.Combine(gameRootPath, "map", "provinces.bmp"),
            Path.Combine(gameRootPath, "map", "definition.csv"),
            Path.Combine(gameRootPath, "map", "adjacencies.csv"),
            out provinces
        );
    }

    private static bool TryParse(
        string provincesFilePath,
        string definitionFilePath,
        string adjacenciesFilePath,
        out IReadOnlyDictionary<int, Province> provinces
    )
    {
        Log.Info("Starting province parser...");

        try
        {
            string[] csvLines = File.ReadAllLines(definitionFilePath);
            var provincesMap = new Dictionary<int, Province>(csvLines.Length);
            var colorToProvinceId = new Dictionary<Rgb, int>(csvLines.Length);

            Log.Info("Parsering {Path}", definitionFilePath);

            ParseProvinceScv(csvLines, provincesMap, colorToProvinceId);
            ParseProvinceBmp(provincesFilePath, provincesMap, colorToProvinceId);
            ParseAdjacenciesFile(adjacenciesFilePath, provincesMap);

            Log.Info("Province parser finish.");
            provinces = provincesMap;
            return true;
        }
        catch (Exception e)
        {
            Log.Error(e, "解析 Province 文件失败");
            provinces = new Dictionary<int, Province>();
            return false;
        }
    }

    private static void ParseAdjacenciesFile(
        string adjacenciesFilePath,
        Dictionary<int, Province> provincesMap
    )
    {
        foreach (string line in File.ReadAllLines(adjacenciesFilePath))
        {
            string[] fields = line.Split(';');
            if (fields.Length < 2)
            {
                continue;
            }

            if (
                int.TryParse(fields[0], out int fromProvinceId)
                && int.TryParse(fields[1], out int toProvinceId)
            )
            {
                if (provincesMap.TryGetValue(fromProvinceId, out var fromProvince))
                {
                    fromProvince.Adjacencies.Add(toProvinceId);
                }
                if (provincesMap.TryGetValue(toProvinceId, out var toProvince))
                {
                    toProvince.Adjacencies.Add(fromProvinceId);
                }
            }
        }
    }

    private static void ParseProvinceScv(
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

    private static void ParseProvinceBmp(
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
}
