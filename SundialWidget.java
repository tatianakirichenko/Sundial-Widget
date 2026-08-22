// SundialWidget.java
import java.io.*;
import java.nio.file.*;
import java.time.*;
import java.time.format.*;
import java.util.*;
import com.google.gson.*;

class Config {
    public Location location = new Location();
}

class Location {
    public String name = "Default";
    public double lat = 0.0;
    public double lon = 0.0;
}

class SolarPos {
    double altitude, azimuth, solarTime, eot;
}

public class SundialWidget {
    private static final double DEFAULT_LAT = 0.0;
    private static final double DEFAULT_LON = 0.0;
    private static final String CONFIG_FILE = "sundial_config.json";
    private static final double DEG2RAD = Math.PI / 180.0;
    private static final double RAD2DEG = 180.0 / Math.PI;
    private static final Gson gson = new GsonBuilder().setPrettyPrinting().create();

    public static double julianDay(LocalDateTime dt) {
        int year = dt.getYear();
        int month = dt.getMonthValue();
        double day = dt.getDayOfMonth() + dt.getHour()/24.0 + dt.getMinute()/1440.0 + dt.getSecond()/86400.0;
        if (month <= 2) { year--; month += 12; }
        int A = year / 100;
        int B = 2 - A + A / 4;
        return (int)(365.25 * (year + 4716)) + (int)(30.6001 * (month + 1)) + day + B - 1524.5;
    }

    public static double solarDeclination(int dayOfYear) {
        return 23.44 * DEG2RAD * Math.sin((284 + dayOfYear) * 360 * DEG2RAD / 365);
    }

    public static double equationOfTime(int dayOfYear) {
        double B = (360.0 / 365) * (dayOfYear - 81);
        double B_rad = B * DEG2RAD;
        return 9.87 * Math.sin(2 * B_rad) - 7.53 * Math.cos(B_rad) - 1.5 * Math.sin(B_rad);
    }

    public static SolarPos solarPosition(LocalDateTime dt, double lat, double lon) {
        int dayOfYear = dt.getDayOfYear();
        double decRad = solarDeclination(dayOfYear);
        double eot = equationOfTime(dayOfYear);
        double hourUTC = dt.getHour() + dt.getMinute()/60.0 + dt.getSecond()/3600.0;
        double localMeanTime = hourUTC; // UTC
        double solarTime = localMeanTime + (4 * lon) / 60.0 + eot / 60.0;
        double haRad = (solarTime - 12) * 15 * DEG2RAD;
        double latRad = lat * DEG2RAD;
        double altRad = Math.asin(Math.sin(latRad)*Math.sin(decRad) + Math.cos(latRad)*Math.cos(decRad)*Math.cos(haRad));
        double altDeg = altRad * RAD2DEG;
        double aziRad = Math.atan2(-Math.sin(haRad)*Math.cos(decRad),
                                   Math.sin(decRad)*Math.cos(latRad) - Math.cos(decRad)*Math.sin(latRad)*Math.cos(haRad));
        double aziDeg = (aziRad * RAD2DEG + 360) % 360;
        SolarPos pos = new SolarPos();
        pos.altitude = altDeg;
        pos.azimuth = aziDeg;
        pos.solarTime = solarTime;
        pos.eot = eot;
        return pos;
    }

    public static String drawSundial(double azimuthDeg, boolean dialOnly) {
        String[] dirNames = {"N", "NE", "E", "SE", "S", "SW", "W", "NW"};
        int idx = (int)Math.round(azimuthDeg / 45) % 8;
        String shadowDir = dirNames[idx];
        StringBuilder sb = new StringBuilder();
        if (!dialOnly) {
            sb.append("      N\n");
            sb.append("      |\n");
            sb.append("  W---+---E\n");
            sb.append("      |\n");
            sb.append("      S\n");
            sb.append(String.format("\nShadow direction: %s (%.1f°)\n", shadowDir, azimuthDeg));
        } else {
            int size = 9, half = size / 2;
            char[][] grid = new char[size][size];
            for (int i=0; i<size; i++) Arrays.fill(grid[i], ' ');
            grid[0][half] = 'N';
            grid[size-1][half] = 'S';
            grid[half][0] = 'W';
            grid[half][size-1] = 'E';
            grid[half][half] = '+';
            double angleRad = azimuthDeg * DEG2RAD;
            int endR = half - 1;
            int dx = (int)Math.round(endR * Math.sin(angleRad));
            int dy = (int)Math.round(-endR * Math.cos(angleRad));
            int x2 = half + dx, y2 = half + dy;
            x2 = Math.max(0, Math.min(size-1, x2));
            y2 = Math.max(0, Math.min(size-1, y2));
            int x0 = half, y0 = half;
            int steps = Math.max(Math.abs(x2-x0), Math.abs(y2-y0));
            if (steps > 0) {
                for (int i=1; i<=steps; i++) {
                    int x = (int)Math.round(x0 + (x2-x0) * (double)i / steps);
                    int y = (int)Math.round(y0 + (y2-y0) * (double)i / steps);
                    if (x >= 0 && x < size && y >= 0 && y < size) {
                        if (grid[y][x] == ' ' || grid[y][x] == '+') grid[y][x] = '*';
                    }
                }
                if (y2 >= 0 && y2 < size && x2 >= 0 && x2 < size) {
                    grid[y2][x2] = 'X';
                }
            }
            for (char[] row : grid) {
                sb.append(new String(row)).append('\n');
            }
        }
        return sb.toString();
    }

    public static void render(LocalDateTime dt, double lat, double lon, boolean dialOnly) {
        SolarPos pos = solarPosition(dt, lat, lon);
        double alt = pos.altitude, azi = pos.azimuth;
        double solarTime = pos.solarTime, eot = pos.eot;
        int hours = (int)solarTime;
        int minutes = (int)((solarTime - hours) * 60);

        if (dialOnly) {
            System.out.print(drawSundial(azi, true));
            return;
        }
        String latStr = String.format("%.2f°%c", Math.abs(lat), lat >= 0 ? 'N' : 'S');
        String lonStr = String.format("%.2f°%c", Math.abs(lon), lon >= 0 ? 'E' : 'W');
        System.out.printf("\n☀️ Sundial Widget\n");
        System.out.printf("Location: %s, %s\n", latStr, lonStr);
        System.out.printf("Date: %s\n", dt.format(DateTimeFormatter.ofPattern("yyyy-MM-dd HH:mm")));
        System.out.printf("\nSolar Time: %02d:%02d (Equation: %+.1f min)\n", hours, minutes, eot);
        System.out.printf("Solar Altitude: %.1f°\n", alt);
        System.out.printf("Solar Azimuth: %.1f°\n", azi);
        System.out.printf("\n%s\n", drawSundial(azi, false));
    }

    public static void main(String[] args) throws Exception {
        Map<String, String> params = new HashMap<>();
        for (int i=0; i<args.length; i++) {
            if (args[i].startsWith("--")) {
                String key = args[i].substring(2);
                if (i+1 < args.length && !args[i+1].startsWith("--")) {
                    params.put(key, args[++i]);
                } else {
                    params.put(key, "");
                }
            }
        }
        Config config = new Config();
        if (Files.exists(Paths.get(CONFIG_FILE))) {
            String json = new String(Files.readAllBytes(Paths.get(CONFIG_FILE)));
            config = gson.fromJson(json, Config.class);
        }
        if (params.containsKey("save-location")) {
            config.location.name = params.get("save-location");
            config.location.lat = params.containsKey("lat") ? Double.parseDouble(params.get("lat")) : DEFAULT_LAT;
            config.location.lon = params.containsKey("lon") ? Double.parseDouble(params.get("lon")) : DEFAULT_LON;
            Files.write(Paths.get(CONFIG_FILE), gson.toJson(config).getBytes());
            System.out.printf("✅ Location '%s' saved.\n", params.get("save-location"));
        }
        double lat = params.containsKey("lat") ? Double.parseDouble(params.get("lat")) : config.location.lat;
        double lon = params.containsKey("lon") ? Double.parseDouble(params.get("lon")) : config.location.lon;

        LocalDateTime dt = LocalDateTime.now(ZoneOffset.UTC);
        if (params.containsKey("date")) {
            LocalDate date = LocalDate.parse(params.get("date"));
            dt = LocalDateTime.of(date, dt.toLocalTime());
        }
        if (params.containsKey("time")) {
            LocalTime time = LocalTime.parse(params.get("time") + ":00");
            dt = LocalDateTime.of(dt.toLocalDate(), time);
        }
        render(dt, lat, lon, params.containsKey("dial-only"));
    }
}
