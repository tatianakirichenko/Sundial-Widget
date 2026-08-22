# sundial_widget.rb
#!/usr/bin/env ruby
require 'json'
require 'date'
require 'optparse'

DEFAULT_LAT = 0.0
DEFAULT_LON = 0.0
CONFIG_FILE = 'sundial_config.json'
DEG2RAD = Math::PI / 180.0
RAD2DEG = 180.0 / Math::PI

def load_config
  if File.exist?(CONFIG_FILE)
    JSON.parse(File.read(CONFIG_FILE)) rescue {}
  else
    {}
  end
end

def save_config(cfg)
  File.write(CONFIG_FILE, JSON.pretty_generate(cfg))
end

def julian_day(dt)
  year = dt.year
  month = dt.month
  day = dt.day + dt.hour/24.0 + dt.min/1440.0 + dt.sec/86400.0
  if month <= 2
    year -= 1
    month += 12
  end
  a = (year / 100).to_i
  b = 2 - a + (a / 4).to_i
  (365.25 * (year + 4716)).to_i + (30.6001 * (month + 1)).to_i + day + b - 1524.5
end

def solar_declination(day_of_year)
  23.44 * DEG2RAD * Math.sin((284 + day_of_year) * 360 * DEG2RAD / 365)
end

def equation_of_time(day_of_year)
  b = (360.0 / 365) * (day_of_year - 81)
  b_rad = b * DEG2RAD
  9.87 * Math.sin(2 * b_rad) - 7.53 * Math.cos(b_rad) - 1.5 * Math.sin(b_rad)
end

def solar_position(dt, lat, lon)
  day_of_year = dt.yday
  dec_rad = solar_declination(day_of_year)
  eot = equation_of_time(day_of_year)
  hour_utc = dt.hour + dt.min/60.0 + dt.sec/3600.0
  local_mean_time = hour_utc # assume UTC for simplicity
  solar_time = local_mean_time + (4 * lon) / 60.0 + eot / 60.0
  ha_rad = (solar_time - 12) * 15 * DEG2RAD
  lat_rad = lat * DEG2RAD
  alt_rad = Math.asin(Math.sin(lat_rad) * Math.sin(dec_rad) + Math.cos(lat_rad) * Math.cos(dec_rad) * Math.cos(ha_rad))
  alt_deg = alt_rad * RAD2DEG
  azi_rad = Math.atan2(-Math.sin(ha_rad) * Math.cos(dec_rad),
                       Math.sin(dec_rad) * Math.cos(lat_rad) -
                       Math.cos(dec_rad) * Math.sin(lat_rad) * Math.cos(ha_rad))
  azi_deg = (azi_rad * RAD2DEG) % 360.0
  { altitude: alt_deg, azimuth: azi_deg, solar_time: solar_time, eot: eot }
end

def draw_sundial(azimuth_deg, dial_only)
  dir_names = ['N', 'NE', 'E', 'SE', 'S', 'SW', 'W', 'NW']
  idx = (azimuth_deg / 45).round % 8
  shadow_dir = dir_names[idx]
  lines = []
  if !dial_only
    lines << "      N"
    lines << "      |"
    lines << "  W---+---E"
    lines << "      |"
    lines << "      S"
    lines << "\nShadow direction: #{shadow_dir} (#{azimuth_deg.round(1)}°)"
  else
    size = 9; half = size / 2
    grid = Array.new(size) { Array.new(size, ' ') }
    grid[0][half] = 'N'
    grid[size-1][half] = 'S'
    grid[half][0] = 'W'
    grid[half][size-1] = 'E'
    grid[half][half] = '+'
    angle_rad = azimuth_deg * DEG2RAD
    end_r = half - 1
    dx = (end_r * Math.sin(angle_rad)).round
    dy = (-end_r * Math.cos(angle_rad)).round
    x2 = half + dx; y2 = half + dy
    x2 = [[x2, 0].max, size-1].min
    y2 = [[y2, 0].max, size-1].min
    x0, y0 = half, half
    steps = [ (x2-x0).abs, (y2-y0).abs ].max
    if steps > 0
      (1..steps).each do |i|
        x = (x0 + (x2 - x0) * i / steps.to_f).round
        y = (y0 + (y2 - y0) * i / steps.to_f).round
        if x >= 0 && x < size && y >= 0 && y < size
          grid[y][x] = '*' if grid[y][x] == ' ' || grid[y][x] == '+'
        end
      end
      if y2 >= 0 && y2 < size && x2 >= 0 && x2 < size
        grid[y2][x2] = 'X'
      end
    end
    lines = grid.map(&:join)
  end
  lines.join("\n")
end

def render(dt, lat, lon, dial_only)
  pos = solar_position(dt, lat, lon)
  alt = pos[:altitude]; azi = pos[:azimuth]
  solar_time = pos[:solar_time]; eot = pos[:eot]
  hours = solar_time.to_i
  minutes = ((solar_time - hours) * 60).to_i

  if dial_only
    puts draw_sundial(azi, true)
    return
  end
  lat_str = "#{lat.abs.round(2)}°#{lat >= 0 ? 'N' : 'S'}"
  lon_str = "#{lon.abs.round(2)}°#{lon >= 0 ? 'E' : 'W'}"
  puts "\n☀️ Sundial Widget"
  puts "Location: #{lat_str}, #{lon_str}"
  puts "Date: #{dt.strftime('%Y-%m-%d %H:%M')}"
  puts "\nSolar Time: #{sprintf('%02d:%02d', hours, minutes)} (Equation: #{eot.round(1)} min)"
  puts "Solar Altitude: #{alt.round(1)}°"
  puts "Solar Azimuth: #{azi.round(1)}°"
  puts "\n" + draw_sundial(azi, false)
end

options = {}
OptionParser.new do |opts|
  opts.banner = "Usage: sundial_widget.rb [options]"
  opts.on("--lat LAT", Float, "Latitude") { |v| options[:lat] = v }
  opts.on("--lon LON", Float, "Longitude") { |v| options[:lon] = v }
  opts.on("--date DATE", "YYYY-MM-DD") { |v| options[:date] = v }
  opts.on("--time TIME", "HH:MM") { |v| options[:time] = v }
  opts.on("--save-location NAME", "Save location") { |v| options[:save_location] = v }
  opts.on("--dial-only", "Only dial") { options[:dial_only] = true }
end.parse!

config = load_config
if options[:save_location]
  config["location"] = { "name" => options[:save_location], "lat" => options[:lat] || DEFAULT_LAT, "lon" => options[:lon] || DEFAULT_LON }
  save_config(config)
  puts "✅ Location '#{options[:save_location]}' saved."
end

lat = options[:lat] || config.dig("location", "lat") || DEFAULT_LAT
lon = options[:lon] || config.dig("location", "lon") || DEFAULT_LON

dt = DateTime.now
if options[:date]
  dt = DateTime.parse(options[:date])
end
if options[:time]
  h, m = options[:time].split(':').map(&:to_i)
  dt = DateTime.new(dt.year, dt.month, dt.day, h, m, 0, dt.zone)
end

render(dt, lat, lon, options[:dial_only])
