# sundial_widget.py
import math
import json
import os
import argparse
from datetime import datetime, timezone, timedelta

CONFIG_FILE = "sundial_config.json"
DEFAULT_LAT = 0.0
DEFAULT_LON = 0.0
DEG2RAD = math.pi / 180.0
RAD2DEG = 180.0 / math.pi

class SundialWidget:
    def __init__(self, lat=DEFAULT_LAT, lon=DEFAULT_LON):
        self.lat = lat
        self.lon = lon
        self.config = self.load_config()

    def load_config(self):
        if os.path.exists(CONFIG_FILE):
            with open(CONFIG_FILE, "r") as f:
                return json.load(f)
        return {"location": {"name": "Default", "lat": DEFAULT_LAT, "lon": DEFAULT_LON}}

    def save_config(self, name, lat, lon):
        self.config["location"] = {"name": name, "lat": lat, "lon": lon}
        with open(CONFIG_FILE, "w") as f:
            json.dump(self.config, f, indent=2)

    def julian_day(self, dt):
        year = dt.year
        month = dt.month
        day = dt.day + dt.hour/24.0 + dt.minute/1440.0 + dt.second/86400.0
        if month <= 2:
            year -= 1
            month += 12
        A = int(year / 100)
        B = 2 - A + int(A / 4)
        return int(365.25 * (year + 4716)) + int(30.6001 * (month + 1)) + day + B - 1524.5

    def solar_declination(self, day_of_year):
        return 23.44 * DEG2RAD * math.sin((284 + day_of_year) * 360 * DEG2RAD / 365)

    def equation_of_time(self, day_of_year):
        B = (360.0 / 365) * (day_of_year - 81)
        B_rad = B * DEG2RAD
        return 9.87 * math.sin(2 * B_rad) - 7.53 * math.cos(B_rad) - 1.5 * math.sin(B_rad)

    def solar_position(self, dt):
        day_of_year = dt.timetuple().tm_yday
        dec_rad = self.solar_declination(day_of_year)
        eot = self.equation_of_time(day_of_year)
        hour_utc = dt.hour + dt.minute/60.0 + dt.second/3600.0
        local_mean_time = hour_utc + -dt.utcoffset().total_seconds()/3600.0
        solar_time = local_mean_time + (4 * self.lon) / 60.0 + eot / 60.0
        ha_rad = (solar_time - 12) * 15 * DEG2RAD
        lat_rad = self.lat * DEG2RAD
        alt_rad = math.asin(math.sin(lat_rad) * math.sin(dec_rad) +
                            math.cos(lat_rad) * math.cos(dec_rad) * math.cos(ha_rad))
        alt_deg = alt_rad * RAD2DEG
        azi_rad = math.atan2(-math.sin(ha_rad) * math.cos(dec_rad),
                             math.sin(dec_rad) * math.cos(lat_rad) -
                             math.cos(dec_rad) * math.sin(lat_rad) * math.cos(ha_rad))
        azi_deg = (azi_rad * RAD2DEG) % 360.0
        return {
            "altitude": alt_deg,
            "azimuth": azi_deg,
            "solar_time": solar_time,
            "eot": eot,
            "declination": dec_rad * RAD2DEG,
        }

    def draw_sundial(self, azimuth_deg, dial_only=False):
        # Simple ASCII sundial: compass rose with a shadow direction
        dir_names = ['N', 'NE', 'E', 'SE', 'S', 'SW', 'W', 'NW']
        idx = int(round(azimuth_deg / 45)) % 8
        shadow_dir = dir_names[idx]
        lines = []
        if not dial_only:
            lines.append("      N")
            lines.append("      |")
            lines.append("  W---+---E")
            lines.append("      |")
            lines.append("      S")
            lines.append(f"\nShadow direction: {shadow_dir} ({azimuth_deg:.1f}°)")
        else:
            # Compact dial with shadow indicator
            size = 9
            half = size // 2
            grid = [[' ' for _ in range(size)] for _ in range(size)]
            # Draw compass
            grid[0][half] = 'N'
            grid[size-1][half] = 'S'
            grid[half][0] = 'W'
            grid[half][size-1] = 'E'
            grid[half][half] = '+'
            # Draw shadow line from center outward
            angle_rad = azimuth_deg * DEG2RAD
            end_r = half - 1
            dx = int(round(end_r * math.sin(angle_rad)))
            dy = int(round(-end_r * math.cos(angle_rad)))
            x2 = half + dx
            y2 = half + dy
            # Clamp
            x2 = max(0, min(size-1, x2))
            y2 = max(0, min(size-1, y2))
            # Use Bresenham to draw line
            x0, y0 = half, half
            steps = max(abs(x2-x0), abs(y2-y0))
            if steps > 0:
                for i in range(1, steps+1):
                    x = int(round(x0 + (x2-x0) * i / steps))
                    y = int(round(y0 + (y2-y0) * i / steps))
                    if 0 <= x < size and 0 <= y < size:
                        if grid[y][x] == ' ' or grid[y][x] == '+':
                            grid[y][x] = '*'
                # mark endpoint
                if 0 <= y2 < size and 0 <= x2 < size:
                    grid[y2][x2] = 'X'
            lines.extend([''.join(row) for row in grid])
        return '\n'.join(lines)

    def render(self, dt, dial_only=False):
        pos = self.solar_position(dt)
        alt = pos["altitude"]
        azi = pos["azimuth"]
        solar_time = pos["solar_time"]
        eot = pos["eot"]
        hours = int(solar_time)
        minutes = int((solar_time - hours) * 60)

        if dial_only:
            print(self.draw_sundial(azi, dial_only=True))
            return

        print("\n☀️ Sundial Widget")
        lat_str = f"{abs(self.lat):.2f}°{'N' if self.lat>=0 else 'S'}"
        lon_str = f"{abs(self.lon):.2f}°{'E' if self.lon>=0 else 'W'}"
        print(f"Location: {lat_str}, {lon_str}")
        print(f"Date: {dt.strftime('%Y-%m-%d %H:%M')}")
        print(f"\nSolar Time: {hours:02d}:{minutes:02d} (Equation: {eot:+.1f} min)")
        print(f"Solar Altitude: {alt:.1f}°")
        print(f"Solar Azimuth: {azi:.1f}°")
        print("\n" + self.draw_sundial(azi))

def main():
    parser = argparse.ArgumentParser(description="Sundial Widget")
    parser.add_argument("--lat", type=float, help="Latitude (positive North)")
    parser.add_argument("--lon", type=float, help="Longitude (positive East)")
    parser.add_argument("--date", help="YYYY-MM-DD")
    parser.add_argument("--time", help="HH:MM")
    parser.add_argument("--save-location", help="Save location with name")
    parser.add_argument("--dial-only", action="store_true", help="Show only the dial")
    args = parser.parse_args()

    # Load config
    config_file = "sundial_config.json"
    config = {}
    if os.path.exists(config_file):
        with open(config_file, "r") as f:
            config = json.load(f)

    lat = args.lat if args.lat is not None else DEFAULT_LAT
    lon = args.lon if args.lon is not None else DEFAULT_LON

    if args.save_location:
        config["location"] = {"name": args.save_location, "lat": lat, "lon": lon}
        with open(config_file, "w") as f:
            json.dump(config, f, indent=2)
        print(f"✅ Location '{args.save_location}' saved.")

    # Use saved location if no lat/lon given
    if args.lat is None and args.lon is None and config.get("location"):
        lat = config["location"]["lat"]
        lon = config["location"]["lon"]

    dt = datetime.now(timezone.utc)
    if args.date:
        dt = datetime.strptime(args.date, "%Y-%m-%d").replace(tzinfo=timezone.utc)
    if args.time:
        t = datetime.strptime(args.time, "%H:%M").time()
        dt = dt.replace(hour=t.hour, minute=t.minute, second=0)

    widget = SundialWidget(lat, lon)
    widget.render(dt, args.dial_only)

if __name__ == "__main__":
    main()
