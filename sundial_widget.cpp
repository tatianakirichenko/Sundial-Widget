// sundial_widget.cpp
#include <iostream>
#include <fstream>
#include <string>
#include <vector>
#include <cmath>
#include <ctime>
#include <iomanip>
#include <sstream>
#include <nlohmann/json.hpp>
#include <getopt.h>

using namespace std;
using json = nlohmann::json;

const double DEFAULT_LAT = 0.0;
const double DEFAULT_LON = 0.0;
const string CONFIG_FILE = "sundial_config.json";
const double DEG2RAD = M_PI / 180.0;
const double RAD2DEG = 180.0 / M_PI;

double julianDay(const tm& dt) {
    int year = dt.tm_year + 1900;
    int month = dt.tm_mon + 1;
    double day = dt.tm_mday + dt.tm_hour/24.0 + dt.tm_min/1440.0 + dt.tm_sec/86400.0;
    if (month <= 2) { year--; month += 12; }
    int A = year / 100;
    int B = 2 - A + A / 4;
    return (int)(365.25 * (year + 4716)) + (int)(30.6001 * (month + 1)) + day + B - 1524.5;
}

double solarDeclination(int dayOfYear) {
    return 23.44 * DEG2RAD * sin((284 + dayOfYear) * 360 * DEG2RAD / 365);
}

double equationOfTime(int dayOfYear) {
    double B = (360.0 / 365) * (dayOfYear - 81);
    double B_rad = B * DEG2RAD;
    return 9.87 * sin(2 * B_rad) - 7.53 * cos(B_rad) - 1.5 * sin(B_rad);
}

struct SolarPos {
    double altitude, azimuth, solarTime, eot;
};

SolarPos solarPosition(const tm& dt, double lat, double lon) {
    int dayOfYear = dt.tm_yday + 1;
    double decRad = solarDeclination(dayOfYear);
    double eot = equationOfTime(dayOfYear);
    double hourUTC = dt.tm_hour + dt.tm_min/60.0 + dt.tm_sec/3600.0;
    double localMeanTime = hourUTC; // UTC
    double solarTime = localMeanTime + (4 * lon) / 60.0 + eot / 60.0;
    double haRad = (solarTime - 12) * 15 * DEG2RAD;
    double latRad = lat * DEG2RAD;
    double altRad = asin(sin(latRad)*sin(decRad) + cos(latRad)*cos(decRad)*cos(haRad));
    double altDeg = altRad * RAD2DEG;
    double aziRad = atan2(-sin(haRad)*cos(decRad),
                          sin(decRad)*cos(latRad) - cos(decRad)*sin(latRad)*cos(haRad));
    double aziDeg = fmod(aziRad * RAD2DEG + 360, 360);
    return {altDeg, aziDeg, solarTime, eot};
}

string drawSundial(double azimuthDeg, bool dialOnly) {
    string dirNames[] = {"N", "NE", "E", "SE", "S", "SW", "W", "NW"};
    int idx = (int)round(azimuthDeg / 45) % 8;
    string shadowDir = dirNames[idx];
    vector<string> lines;
    if (!dialOnly) {
        lines.push_back("      N");
        lines.push_back("      |");
        lines.push_back("  W---+---E");
        lines.push_back("      |");
        lines.push_back("      S");
        char buf[100];
        snprintf(buf, sizeof(buf), "\nShadow direction: %s (%.1f°)", shadowDir.c_str(), azimuthDeg);
        lines.push_back(buf);
    } else {
        int size = 9, half = size / 2;
        vector<vector<char>> grid(size, vector<char>(size, ' '));
        grid[0][half] = 'N';
        grid[size-1][half] = 'S';
        grid[half][0] = 'W';
        grid[half][size-1] = 'E';
        grid[half][half] = '+';
        double angleRad = azimuthDeg * DEG2RAD;
        int endR = half - 1;
        int dx = (int)round(endR * sin(angleRad));
        int dy = (int)round(-endR * cos(angleRad));
        int x2 = half + dx, y2 = half + dy;
        x2 = max(0, min(size-1, x2));
        y2 = max(0, min(size-1, y2));
        int x0 = half, y0 = half;
        int steps = max(abs(x2-x0), abs(y2-y0));
        if (steps > 0) {
            for (int i=1; i<=steps; i++) {
                int x = (int)round(x0 + (x2-x0) * (double)i / steps);
                int y = (int)round(y0 + (y2-y0) * (double)i / steps);
                if (x >= 0 && x < size && y >= 0 && y < size) {
                    if (grid[y][x] == ' ' || grid[y][x] == '+') grid[y][x] = '*';
                }
            }
            if (y2 >= 0 && y2 < size && x2 >= 0 && x2 < size) {
                grid[y2][x2] = 'X';
            }
        }
        for (auto& row : grid) {
            string line(row.begin(), row.end());
            lines.push_back(line);
        }
    }
    string result;
    for (auto& line : lines) result += line + "\n";
    return result;
}

void render(const tm& dt, double lat, double lon, bool dialOnly) {
    SolarPos pos = solarPosition(dt, lat, lon);
    double alt = pos.altitude, azi = pos.azimuth;
    double solarTime = pos.solarTime, eot = pos.eot;
    int hours = (int)solarTime;
    int minutes = (int)((solarTime - hours) * 60);

    if (dialOnly) {
        cout << drawSundial(azi, true);
        return;
    }
    char dateBuf[20];
    strftime(dateBuf, sizeof(dateBuf), "%Y-%m-%d %H:%M", &dt);
    string latStr = to_string(abs(lat)).substr(0,5) + "°" + (lat >= 0 ? "N" : "S");
    string lonStr = to_string(abs(lon)).substr(0,5) + "°" + (lon >= 0 ? "E" : "W");
    cout << "\n☀️ Sundial Widget\n";
    cout << "Location: " << latStr << ", " << lonStr << "\n";
    cout << "Date: " << dateBuf << "\n";
    printf("Solar Time: %02d:%02d (Equation: %+.1f min)\n", hours, minutes, eot);
    printf("Solar Altitude: %.1f°\n", alt);
    printf("Solar Azimuth: %.1f°\n", azi);
    cout << "\n" << drawSundial(azi, false);
}

int main(int argc, char* argv[]) {
    static struct option long_options[] = {
        {"lat", required_argument, 0, 'a'},
        {"lon", required_argument, 0, 'o'},
        {"date", required_argument, 0, 'd'},
        {"time", required_argument, 0, 't'},
        {"save-location", required_argument, 0, 's'},
        {"dial-only", no_argument, 0, 'p'},
        {0,0,0,0}
    };
    int opt;
    string dateStr, timeStr, saveLocation;
    double lat = DEFAULT_LAT, lon = DEFAULT_LON;
    bool dialOnly = false;

    while ((opt = getopt_long(argc, argv, "a:o:d:t:s:p", long_options, nullptr)) != -1) {
        switch (opt) {
            case 'a': lat = stod(optarg); break;
            case 'o': lon = stod(optarg); break;
            case 'd': dateStr = optarg; break;
            case 't': timeStr = optarg; break;
            case 's': saveLocation = optarg; break;
            case 'p': dialOnly = true; break;
            default:
                cerr << "Usage: sundial_widget --lat LAT --lon LON --date YYYY-MM-DD --time HH:MM --save-location NAME --dial-only\n";
                return 1;
        }
    }

    // Load config
    json config;
    ifstream f(CONFIG_FILE);
    if (f.is_open()) f >> config;

    if (!saveLocation.empty()) {
        config["location"] = {{"name", saveLocation}, {"lat", lat}, {"lon", lon}};
        ofstream out(CONFIG_FILE);
        out << setw(2) << config << endl;
        cout << "✅ Location '" << saveLocation << "' saved.\n";
    }

    if (lat == DEFAULT_LAT && lon == DEFAULT_LON && config.contains("location")) {
        lat = config["location"]["lat"];
        lon = config["location"]["lon"];
    }

    time_t now = time(nullptr);
    tm dt = *gmtime(&now);
    if (!dateStr.empty()) {
        strptime(dateStr.c_str(), "%Y-%m-%d", &dt);
    }
    if (!timeStr.empty()) {
        strptime(timeStr.c_str(), "%H:%M", &dt);
    }

    render(dt, lat, lon, dialOnly);
    return 0;
}
