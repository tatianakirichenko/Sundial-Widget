// SundialWidget.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

class Config
{
    [JsonPropertyName("location")]
    public Location Location { get; set; } = new Location();
}

class Location
{
    [JsonPropertyName("name")] public string Name { get; set; } = "Default";
    [JsonPropertyName("lat")] public double Lat { get; set; } = 0.0;
    [JsonPropertyName("lon")] public double Lon { get; set; } = 0.0;
}

class SolarPos
{
    public double Altitude { get; set; }
    public double Azimuth { get; set; }
    public double SolarTime { get; set; }
    public double EOT { get; set; }
}

class SundialWidget
{
    private const double DEFAULT_LAT = 0.0;
    private const double DEFAULT_LON = 0.0;
    private const string CONFIG_FILE = "sundial_config.json";
    private const double DEG2RAD = Math.PI / 180.0;
    private const double RAD2DEG = 180.0 / Math.PI;
    private static readonly JsonSerializerOptions Options = new JsonSerializerOptions { WriteIndented = true };

    static double JulianDay(DateTime dt)
    {
        int year = dt.Year;
        int month = dt.Month;
        double day = dt.Day + dt.Hour/24.0 + dt.Minute/1440.0 + dt.Second/86400.0;
        if (month <= 2) { year--; month += 12; }
        int A = year / 100;
        int B = 2 - A + A / 4;
        return (int)(365.25 * (year + 4716)) + (int)(30.6001 * (month + 1)) + day + B - 1524.5;
    }

    static double SolarDeclination(int dayOfYear)
    {
        return 23.44 * DEG2RAD * Math.Sin((284 + dayOfYear) * 360 * DEG2RAD / 365);
    }

    static double EquationOfTime(int dayOfYear)
    {
        double B = (360.0 / 365) * (dayOfYear - 81);
        double B_rad = B * DEG2RAD;
        return 9.87 * Math.Sin(2 * B_rad) - 7.53 * Math.Cos(B_rad) - 1.5 * Math.Sin(B_rad);
    }

    static SolarPos SolarPosition(DateTime dt, double lat, double lon)
    {
        int dayOfYear = dt.DayOfYear;
        double decRad = SolarDeclination(dayOfYear);
        double eot = EquationOfTime(dayOfYear);
        double hourUTC = dt.Hour + dt.Minute/60.0 + dt.Second/3600.0;
        double localMeanTime = hourUTC; // UTC
        double solarTime = localMeanTime + (4 * lon) / 60.0 + eot / 60.0;
        double haRad = (solarTime - 12) * 15 * DEG2RAD;
        double latRad = lat * DEG2RAD;
        double altRad = Math.Asin(Math.Sin(latRad)*Math.Sin(decRad) + Math.Cos(latRad)*Math.Cos(decRad)*Math.Cos(haRad));
        double altDeg = altRad * RAD2DEG;
        double aziRad = Math.Atan2(-Math.Sin(haRad)*Math.Cos(decRad),
                                   Math.Sin(decRad)*Math.Cos(latRad) - Math.Cos(decRad)*Math.Sin(latRad)*Math.Cos(haRad));
        double aziDeg = (aziRad * RAD2DEG + 360) % 360;
        return new SolarPos { Altitude = altDeg, Azimuth = aziDeg, SolarTime = solarTime, EOT = eot };
    }

    static string DrawSundial(double azimuthDeg, bool dialOnly)
    {
        string[] dirNames = {"N", "NE", "E", "SE", "S", "SW", "W", "NW"};
        int idx = (int)Math.Round(azimuthDeg / 45) % 8;
        string shadowDir = dirNames[idx];
        var sb = new StringBuilder();
        if (!dialOnly)
        {
            sb.AppendLine("      N");
            sb.AppendLine("      |");
            sb.AppendLine("  W---+---E");
            sb.AppendLine("      |");
            sb.AppendLine("      S");
            sb.AppendLine($"\nShadow direction: {shadowDir} ({azimuthDeg:F1}°)");
        }
        else
        {
            int size = 9, half = size / 2;
            char[][] grid = new char[size][];
            for (int i=0; i<size; i++)
            {
                grid[i] = new char[size];
                Array.Fill(grid[i], ' ');
            }
            grid[0][half] = 'N';
            grid[size-1][half] = 'S';
            grid[half][0] = 'W';
            grid[half][size-1] = 'E';
            grid[half][half] = '+';
            double angleRad = azimuthDeg * DEG2RAD;
            int endR = half - 1;
            int dx = (int)Math.Round(endR * Math.Sin(angleRad));
            int dy = (int)Math.Round(-endR * Math.Cos(angleRad));
            int x2 = half + dx, y2 = half + dy;
            x2 = Math.Max(0, Math.Min(size-1, x2));
            y2 = Math.Max(0, Math.Min(size-1, y2));
            int x0 = half, y0 = half;
            int steps = Math.Max(Math.Abs(x2-x0), Math.Abs(y2-y0));
            if (steps > 0)
            {
                for (int i=1; i<=steps; i++)
                {
                    int x = (int)Math.Round(x0 + (x2-x0) * (double)i / steps);
                    int y = (int)Math.Round(y0 + (y2-y0) * (double)i / steps);
                    if (x >= 0 && x < size && y >= 0 && y < size)
                    {
                        if (grid[y][x] == ' ' || grid[y][x] == '+') grid[y][x] = '*';
                    }
                }
                if (y2 >= 0 && y2 < size && x2 >= 0 && x2 < size)
                {
                    grid[y2][x2] = 'X';
                }
            }
            foreach (var row in grid) sb.AppendLine(new string(row));
        }
        return sb.ToString();
    }

    static void Render(DateTime dt, double lat, double lon, bool dialOnly)
    {
        SolarPos pos = SolarPosition(dt, lat, lon);
        double alt = pos.Altitude, azi = pos.Azimuth;
        double solarTime = pos.SolarTime, eot = pos.EOT;
        int hours = (int)solarTime;
        int minutes = (int)((solarTime - hours) * 60);

        if (dialOnly)
        {
            Console.Write(DrawSundial(azi, true));
            return;
        }
        string latStr = $"{Math.Abs(lat):F2}°{(lat >= 0 ? 'N' : 'S')}";
        string lonStr = $"{Math.Abs(lon):F2}°{(lon >= 0 ? 'E' : 'W')}";
        Console.WriteLine($"\n☀️ Sundial Widget");
        Console.WriteLine($"Location: {latStr}, {lonStr}");
        Console.WriteLine($"Date: {dt:yyyy-MM-dd HH:mm}");
        Console.WriteLine($"\nSolar Time: {hours:D2}:{minutes:D2} (Equation: {eot:+0.0;-0.0} min)");
        Console.WriteLine($"Solar Altitude: {alt:F1}°");
        Console.WriteLine($"Solar Azimuth: {azi:F1}°");
        Console.WriteLine($"\n{DrawSundial(azi, false)}");
    }

    static void Main(string[] args)
    {
        var parsed = ParseArgs(args);
        Config config = new Config();
        if (File.Exists(CONFIG_FILE))
        {
            string json = File.ReadAllText(CONFIG_FILE);
            config = JsonSerializer.Deserialize<Config>(json) ?? new Config();
        }
        if (parsed.ContainsKey("save-location"))
        {
            config.Location.Name = parsed["save-location"];
            config.Location.Lat = parsed.ContainsKey("lat") ? double.Parse(parsed["lat"]) : DEFAULT_LAT;
            config.Location.Lon = parsed.ContainsKey("lon") ? double.Parse(parsed["lon"]) : DEFAULT_LON;
            File.WriteAllText(CONFIG_FILE, JsonSerializer.Serialize(config, Options));
            Console.WriteLine($"✅ Location '{config.Location.Name}' saved.");
        }
        double lat = parsed.ContainsKey("lat") ? double.Parse(parsed["lat"]) : config.Location.Lat;
        double lon = parsed.ContainsKey("lon") ? double.Parse(parsed["lon"]) : config.Location.Lon;

        DateTime dt = DateTime.UtcNow;
        if (parsed.ContainsKey("date"))
        {
            dt = DateTime.Parse(parsed["date"] + " 00:00:00").ToUniversalTime();
        }
        if (parsed.ContainsKey("time"))
        {
            var parts = parsed["time"].Split(':');
            dt = new DateTime(dt.Year, dt.Month, dt.Day, int.Parse(parts[0]), int.Parse(parts[1]), 0, DateTimeKind.Utc);
        }
        Render(dt, lat, lon, parsed.ContainsKey("dial-only"));
    }

    static Dictionary<string, string> ParseArgs(string[] args)
    {
        var dict = new Dictionary<string, string>();
        for (int i=0; i<args.Length; i++)
        {
            if (args[i].StartsWith("--"))
            {
                string key = args[i].Substring(2);
                if (i+1 < args.Length && !args[i+1].StartsWith("--"))
                    dict[key] = args[++i];
                else
                    dict[key] = "";
            }
        }
        return dict;
    }
}
