// sundial_widget.go
package main

import (
	"encoding/json"
	"flag"
	"fmt"
	"math"
	"os"
	"time"
)

const (
	DEFAULT_LAT = 0.0
	DEFAULT_LON = 0.0
	DEG2RAD     = math.Pi / 180.0
	RAD2DEG     = 180.0 / math.Pi
	CONFIG_FILE = "sundial_config.json"
)

type Config struct {
	Location struct {
		Name string  `json:"name"`
		Lat  float64 `json:"lat"`
		Lon  float64 `json:"lon"`
	} `json:"location"`
}

func loadConfig() Config {
	var cfg Config
	data, err := os.ReadFile(CONFIG_FILE)
	if err != nil {
		return cfg
	}
	json.Unmarshal(data, &cfg)
	return cfg
}

func saveConfig(cfg Config) {
	data, _ := json.MarshalIndent(cfg, "", "  ")
	os.WriteFile(CONFIG_FILE, data, 0644)
}

func julianDay(t time.Time) float64 {
	year := t.Year()
	month := int(t.Month())
	day := float64(t.Day()) + float64(t.Hour())/24.0 + float64(t.Minute())/1440.0 + float64(t.Second())/86400.0
	if month <= 2 {
		year--
		month += 12
	}
	A := year / 100
	B := 2 - A + A/4
	return float64(int(365.25*float64(year+4716))) + float64(int(30.6001*float64(month+1))) + day + float64(B) - 1524.5
}

func solarDeclination(dayOfYear int) float64 {
	return 23.44 * DEG2RAD * math.Sin((284+dayOfYear)*360*DEG2RAD/365)
}

func equationOfTime(dayOfYear int) float64 {
	B := (360.0 / 365) * float64(dayOfYear-81)
	B_rad := B * DEG2RAD
	return 9.87*math.Sin(2*B_rad) - 7.53*math.Cos(B_rad) - 1.5*math.Sin(B_rad)
}

type SolarPos struct {
	Altitude    float64
	Azimuth     float64
	SolarTime   float64
	EOT         float64
	Declination float64
}

func solarPosition(t time.Time, lat, lon float64) SolarPos {
	dayOfYear := t.YearDay()
	decRad := solarDeclination(dayOfYear)
	eot := equationOfTime(dayOfYear)
	_, offset := t.Zone()
	hourUTC := float64(t.Hour()) + float64(t.Minute())/60.0 + float64(t.Second())/3600.0
	localMeanTime := hourUTC + float64(offset)/3600.0
	solarTime := localMeanTime + (4*lon)/60.0 + eot/60.0
	haRad := (solarTime - 12) * 15 * DEG2RAD
	latRad := lat * DEG2RAD
	altRad := math.Asin(math.Sin(latRad)*math.Sin(decRad) + math.Cos(latRad)*math.Cos(decRad)*math.Cos(haRad))
	altDeg := altRad * RAD2DEG
	aziRad := math.Atan2(-math.Sin(haRad)*math.Cos(decRad),
		math.Sin(decRad)*math.Cos(latRad)-math.Cos(decRad)*math.Sin(latRad)*math.Cos(haRad))
	aziDeg := math.Mod(aziRad*RAD2DEG+360, 360)
	return SolarPos{altDeg, aziDeg, solarTime, eot, decRad * RAD2DEG}
}

func drawSundial(azimuthDeg float64, dialOnly bool) string {
	dirNames := []string{"N", "NE", "E", "SE", "S", "SW", "W", "NW"}
	idx := int(math.Round(azimuthDeg/45)) % 8
	shadowDir := dirNames[idx]
	lines := []string{}
	if !dialOnly {
		lines = append(lines, "      N")
		lines = append(lines, "      |")
		lines = append(lines, "  W---+---E")
		lines = append(lines, "      |")
		lines = append(lines, "      S")
		lines = append(lines, fmt.Sprintf("\nShadow direction: %s (%.1f°)", shadowDir, azimuthDeg))
	} else {
		size := 9
		half := size / 2
		grid := make([][]byte, size)
		for i := range grid {
			grid[i] = make([]byte, size)
			for j := range grid[i] {
				grid[i][j] = ' '
			}
		}
		grid[0][half] = 'N'
		grid[size-1][half] = 'S'
		grid[half][0] = 'W'
		grid[half][size-1] = 'E'
		grid[half][half] = '+'
		angleRad := azimuthDeg * DEG2RAD
		endR := half - 1
		dx := int(math.Round(float64(endR) * math.Sin(angleRad)))
		dy := int(math.Round(-float64(endR) * math.Cos(angleRad)))
		x2 := half + dx
		y2 := half + dy
		if x2 < 0 {
			x2 = 0
		}
		if x2 >= size {
			x2 = size - 1
		}
		if y2 < 0 {
			y2 = 0
		}
		if y2 >= size {
			y2 = size - 1
		}
		x0, y0 := half, half
		steps := max(abs(x2-x0), abs(y2-y0))
		if steps > 0 {
			for i := 1; i <= steps; i++ {
				x := int(math.Round(float64(x0) + float64(x2-x0)*float64(i)/float64(steps)))
				y := int(math.Round(float64(y0) + float64(y2-y0)*float64(i)/float64(steps)))
				if x >= 0 && x < size && y >= 0 && y < size {
					if grid[y][x] == ' ' || grid[y][x] == '+' {
						grid[y][x] = '*'
					}
				}
			}
			if y2 >= 0 && y2 < size && x2 >= 0 && x2 < size {
				grid[y2][x2] = 'X'
			}
		}
		for _, row := range grid {
			lines = append(lines, string(row))
		}
	}
	return strings.Join(lines, "\n")
}

func max(a, b int) int {
	if a > b {
		return a
	}
	return b
}

func abs(a int) int {
	if a < 0 {
		return -a
	}
	return a
}

func render(t time.Time, lat, lon float64, dialOnly bool) {
	pos := solarPosition(t, lat, lon)
	alt := pos.Altitude
	azi := pos.Azimuth
	solarTime := pos.SolarTime
	eot := pos.EOT
	hours := int(solarTime)
	minutes := int((solarTime - float64(hours)) * 60)

	if dialOnly {
		fmt.Print(drawSundial(azi, true))
		return
	}
	latStr := fmt.Sprintf("%.2f°%c", math.Abs(lat), 'N')
	if lat < 0 {
		latStr = fmt.Sprintf("%.2f°%c", math.Abs(lat), 'S')
	}
	lonStr := fmt.Sprintf("%.2f°%c", math.Abs(lon), 'E')
	if lon < 0 {
		lonStr = fmt.Sprintf("%.2f°%c", math.Abs(lon), 'W')
	}
	fmt.Printf("\n☀️ Sundial Widget\n")
	fmt.Printf("Location: %s, %s\n", latStr, lonStr)
	fmt.Printf("Date: %s\n", t.Format("2006-01-02 15:04"))
	fmt.Printf("\nSolar Time: %02d:%02d (Equation: %+.1f min)\n", hours, minutes, eot)
	fmt.Printf("Solar Altitude: %.1f°\n", alt)
	fmt.Printf("Solar Azimuth: %.1f°\n", azi)
	fmt.Printf("\n%s\n", drawSundial(azi, false))
}

func main() {
	var (
		lat          = flag.Float64("lat", DEFAULT_LAT, "Latitude (positive North)")
		lon          = flag.Float64("lon", DEFAULT_LON, "Longitude (positive East)")
		dateStr      = flag.String("date", "", "YYYY-MM-DD")
		timeStr      = flag.String("time", "", "HH:MM")
		saveLocation = flag.String("save-location", "", "Save location with name")
		dialOnly     = flag.Bool("dial-only", false, "Show only the dial")
	)
	flag.Parse()

	cfg := loadConfig()
	if *saveLocation != "" {
		cfg.Location.Name = *saveLocation
		cfg.Location.Lat = *lat
		cfg.Location.Lon = *lon
		saveConfig(cfg)
		fmt.Printf("✅ Location '%s' saved.\n", *saveLocation)
	}
	useLat := *lat
	useLon := *lon
	if useLat == DEFAULT_LAT && useLon == DEFAULT_LON && cfg.Location.Lat != 0 {
		useLat = cfg.Location.Lat
		useLon = cfg.Location.Lon
	}

	now := time.Now()
	t := now
	if *dateStr != "" {
		d, _ := time.Parse("2006-01-02", *dateStr)
		t = time.Date(d.Year(), d.Month(), d.Day(), t.Hour(), t.Minute(), 0, 0, time.UTC)
	}
	if *timeStr != "" {
		parsed, _ := time.Parse("15:04", *timeStr)
		t = time.Date(t.Year(), t.Month(), t.Day(), parsed.Hour(), parsed.Minute(), 0, 0, time.UTC)
	}
	render(t, useLat, useLon, *dialOnly)
}
