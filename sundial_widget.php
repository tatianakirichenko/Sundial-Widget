# sundial_widget.php
#!/usr/bin/env php
<?php

define('DEFAULT_LAT', 0.0);
define('DEFAULT_LON', 0.0);
define('CONFIG_FILE', 'sundial_config.json');
define('DEG2RAD', M_PI / 180.0);
define('RAD2DEG', 180.0 / M_PI);

function loadConfig() {
    if (file_exists(CONFIG_FILE)) {
        return json_decode(file_get_contents(CONFIG_FILE), true) ?: [];
    }
    return [];
}

function saveConfig($cfg) {
    file_put_contents(CONFIG_FILE, json_encode($cfg, JSON_PRETTY_PRINT));
}

function julianDay($dt) {
    $year = (int)$dt->format('Y');
    $month = (int)$dt->format('m');
    $day = (float)$dt->format('d') + (float)$dt->format('H')/24 + (float)$dt->format('i')/1440 + (float)$dt->format('s')/86400;
    if ($month <= 2) { $year--; $month += 12; }
    $A = (int)($year / 100);
    $B = 2 - $A + (int)($A / 4);
    return (int)(365.25 * ($year + 4716)) + (int)(30.6001 * ($month + 1)) + $day + $B - 1524.5;
}

function solarDeclination($dayOfYear) {
    return 23.44 * DEG2RAD * sin((284 + $dayOfYear) * 360 * DEG2RAD / 365);
}

function equationOfTime($dayOfYear) {
    $B = (360.0 / 365) * ($dayOfYear - 81);
    $B_rad = $B * DEG2RAD;
    return 9.87 * sin(2 * $B_rad) - 7.53 * cos($B_rad) - 1.5 * sin($B_rad);
}

function solarPosition($dt, $lat, $lon) {
    $dayOfYear = (int)$dt->format('z') + 1;
    $decRad = solarDeclination($dayOfYear);
    $eot = equationOfTime($dayOfYear);
    $hourUTC = (float)$dt->format('G') + (float)$dt->format('i')/60 + (float)$dt->format('s')/3600;
    $localMeanTime = $hourUTC; // UTC
    $solarTime = $localMeanTime + (4 * $lon) / 60 + $eot / 60;
    $haRad = ($solarTime - 12) * 15 * DEG2RAD;
    $latRad = $lat * DEG2RAD;
    $altRad = asin(sin($latRad) * sin($decRad) + cos($latRad) * cos($decRad) * cos($haRad));
    $altDeg = $altRad * RAD2DEG;
    $aziRad = atan2(-sin($haRad) * cos($decRad),
                    sin($decRad) * cos($latRad) - cos($decRad) * sin($latRad) * cos($haRad));
    $aziDeg = fmod($aziRad * RAD2DEG + 360, 360);
    return ['altitude' => $altDeg, 'azimuth' => $aziDeg, 'solarTime' => $solarTime, 'eot' => $eot];
}

function drawSundial($azimuthDeg, $dialOnly) {
    $dirNames = ['N', 'NE', 'E', 'SE', 'S', 'SW', 'W', 'NW'];
    $idx = (int)round($azimuthDeg / 45) % 8;
    $shadowDir = $dirNames[$idx];
    $lines = [];
    if (!$dialOnly) {
        $lines[] = "      N";
        $lines[] = "      |";
        $lines[] = "  W---+---E";
        $lines[] = "      |";
        $lines[] = "      S";
        $lines[] = "\nShadow direction: $shadowDir (" . round($azimuthDeg, 1) . "°)";
    } else {
        $size = 9; $half = (int)($size/2);
        $grid = array_fill(0, $size, array_fill(0, $size, ' '));
        $grid[0][$half] = 'N';
        $grid[$size-1][$half] = 'S';
        $grid[$half][0] = 'W';
        $grid[$half][$size-1] = 'E';
        $grid[$half][$half] = '+';
        $angleRad = $azimuthDeg * DEG2RAD;
        $endR = $half - 1;
        $dx = (int)round($endR * sin($angleRad));
        $dy = (int)round(-$endR * cos($angleRad));
        $x2 = $half + $dx;
        $y2 = $half + $dy;
        $x2 = max(0, min($size-1, $x2));
        $y2 = max(0, min($size-1, $y2));
        $x0 = $half; $y0 = $half;
        $steps = max(abs($x2-$x0), abs($y2-$y0));
        if ($steps > 0) {
            for ($i=1; $i<=$steps; $i++) {
                $x = (int)round($x0 + ($x2-$x0) * $i / $steps);
                $y = (int)round($y0 + ($y2-$y0) * $i / $steps);
                if ($x >= 0 && $x < $size && $y >= 0 && $y < $size) {
                    if ($grid[$y][$x] == ' ' || $grid[$y][$x] == '+') {
                        $grid[$y][$x] = '*';
                    }
                }
            }
            if ($y2 >= 0 && $y2 < $size && $x2 >= 0 && $x2 < $size) {
                $grid[$y2][$x2] = 'X';
            }
        }
        foreach ($grid as $row) {
            $lines[] = implode('', $row);
        }
    }
    return implode("\n", $lines);
}

function render($dt, $lat, $lon, $dialOnly) {
    $pos = solarPosition($dt, $lat, $lon);
    $alt = $pos['altitude'];
    $azi = $pos['azimuth'];
    $solarTime = $pos['solarTime'];
    $eot = $pos['eot'];
    $hours = (int)$solarTime;
    $minutes = (int)(($solarTime - $hours) * 60);

    if ($dialOnly) {
        echo drawSundial($azi, true) . "\n";
        return;
    }
    $latStr = abs($lat) . "°" . ($lat >= 0 ? 'N' : 'S');
    $lonStr = abs($lon) . "°" . ($lon >= 0 ? 'E' : 'W');
    echo "\n☀️ Sundial Widget\n";
    echo "Location: $latStr, $lonStr\n";
    echo "Date: " . $dt->format('Y-m-d H:i') . "\n";
    printf("\nSolar Time: %02d:%02d (Equation: %+.1f min)\n", $hours, $minutes, $eot);
    printf("Solar Altitude: %.1f°\n", $alt);
    printf("Solar Azimuth: %.1f°\n", $azi);
    echo "\n" . drawSundial($azi, false) . "\n";
}

$opts = getopt("", ["lat:", "lon:", "date:", "time:", "save-location:", "dial-only"]);
$config = loadConfig();

if (isset($opts['save-location'])) {
    $lat = isset($opts['lat']) ? (float)$opts['lat'] : DEFAULT_LAT;
    $lon = isset($opts['lon']) ? (float)$opts['lon'] : DEFAULT_LON;
    $config['location'] = ['name' => $opts['save-location'], 'lat' => $lat, 'lon' => $lon];
    saveConfig($config);
    echo "✅ Location '" . $opts['save-location'] . "' saved.\n";
}

$lat = isset($opts['lat']) ? (float)$opts['lat'] : ($config['location']['lat'] ?? DEFAULT_LAT);
$lon = isset($opts['lon']) ? (float)$opts['lon'] : ($config['location']['lon'] ?? DEFAULT_LON);

$dt = new DateTime('now', new DateTimeZone('UTC'));
if (isset($opts['date'])) {
    $dt = new DateTime($opts['date'] . ' 00:00:00', new DateTimeZone('UTC'));
}
if (isset($opts['time'])) {
    list($h, $m) = explode(':', $opts['time']);
    $dt->setTime((int)$h, (int)$m, 0);
}
$dialOnly = isset($opts['dial-only']);
render($dt, $lat, $lon, $dialOnly);
?>
