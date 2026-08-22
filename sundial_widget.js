// sundial_widget.js
#!/usr/bin/env node
const fs = require('fs');
const { program } = require('commander');

const DEFAULT_LAT = 0.0;
const DEFAULT_LON = 0.0;
const CONFIG_FILE = 'sundial_config.json';
const DEG2RAD = Math.PI / 180;
const RAD2DEG = 180 / Math.PI;

function loadConfig() {
    if (fs.existsSync(CONFIG_FILE)) {
        try {
            return JSON.parse(fs.readFileSync(CONFIG_FILE));
        } catch (e) {}
    }
    return { location: { lat: DEFAULT_LAT, lon: DEFAULT_LON } };
}

function saveConfig(cfg) {
    fs.writeFileSync(CONFIG_FILE, JSON.stringify(cfg, null, 2));
}

function julianDay(dt) {
    const year = dt.getFullYear();
    const month = dt.getMonth() + 1;
    const day = dt.getDate() + dt.getHours()/24 + dt.getMinutes()/1440 + dt.getSeconds()/86400;
    let y = year, m = month;
    if (month <= 2) { y--; m += 12; }
    const A = Math.floor(y / 100);
    const B = 2 - A + Math.floor(A / 4);
    return Math.floor(365.25 * (y + 4716)) + Math.floor(30.6001 * (m + 1)) + day + B - 1524.5;
}

function solarDeclination(dayOfYear) {
    return 23.44 * DEG2RAD * Math.sin((284 + dayOfYear) * 360 * DEG2RAD / 365);
}

function equationOfTime(dayOfYear) {
    const B = (360.0 / 365) * (dayOfYear - 81);
    const B_rad = B * DEG2RAD;
    return 9.87 * Math.sin(2 * B_rad) - 7.53 * Math.cos(B_rad) - 1.5 * Math.sin(B_rad);
}

function solarPosition(dt, lat, lon) {
    const dayOfYear = Math.floor((dt - new Date(dt.getFullYear(), 0, 0)) / (1000*60*60*24));
    const decRad = solarDeclination(dayOfYear);
    const eot = equationOfTime(dayOfYear);
    const hourUTC = dt.getHours() + dt.getMinutes()/60 + dt.getSeconds()/3600;
    const localMeanTime = hourUTC; // assume UTC for simplicity
    const solarTime = localMeanTime + (4 * lon) / 60 + eot / 60;
    const haRad = (solarTime - 12) * 15 * DEG2RAD;
    const latRad = lat * DEG2RAD;
    const altRad = Math.asin(Math.sin(latRad)*Math.sin(decRad) + Math.cos(latRad)*Math.cos(decRad)*Math.cos(haRad));
    const altDeg = altRad * RAD2DEG;
    const aziRad = Math.atan2(-Math.sin(haRad)*Math.cos(decRad),
                              Math.sin(decRad)*Math.cos(latRad) - Math.cos(decRad)*Math.sin(latRad)*Math.cos(haRad));
    const aziDeg = ((aziRad * RAD2DEG) % 360 + 360) % 360;
    return { altitude: altDeg, azimuth: aziDeg, solarTime, eot };
}

function drawSundial(azimuthDeg, dialOnly) {
    const dirNames = ['N', 'NE', 'E', 'SE', 'S', 'SW', 'W', 'NW'];
    const idx = Math.round(azimuthDeg / 45) % 8;
    const shadowDir = dirNames[idx];
    let lines = [];
    if (!dialOnly) {
        lines.push('      N');
        lines.push('      |');
        lines.push('  W---+---E');
        lines.push('      |');
        lines.push('      S');
        lines.push(`\nShadow direction: ${shadowDir} (${azimuthDeg.toFixed(1)}°)`);
    } else {
        const size = 9, half = Math.floor(size/2);
        const grid = Array.from({length: size}, () => Array(size).fill(' '));
        grid[0][half] = 'N';
        grid[size-1][half] = 'S';
        grid[half][0] = 'W';
        grid[half][size-1] = 'E';
        grid[half][half] = '+';
        const angleRad = azimuthDeg * DEG2RAD;
        const endR = half - 1;
        const dx = Math.round(endR * Math.sin(angleRad));
        const dy = Math.round(-endR * Math.cos(angleRad));
        let x2 = half + dx, y2 = half + dy;
        x2 = Math.max(0, Math.min(size-1, x2));
        y2 = Math.max(0, Math.min(size-1, y2));
        const x0 = half, y0 = half;
        const steps = Math.max(Math.abs(x2-x0), Math.abs(y2-y0));
        if (steps > 0) {
            for (let i = 1; i <= steps; i++) {
                const x = Math.round(x0 + (x2-x0) * i / steps);
                const y = Math.round(y0 + (y2-y0) * i / steps);
                if (x >= 0 && x < size && y >= 0 && y < size) {
                    if (grid[y][x] === ' ' || grid[y][x] === '+') grid[y][x] = '*';
                }
            }
            if (y2 >= 0 && y2 < size && x2 >= 0 && x2 < size) {
                grid[y2][x2] = 'X';
            }
        }
        lines = grid.map(row => row.join(''));
    }
    return lines.join('\n');
}

function render(dt, lat, lon, dialOnly) {
    const pos = solarPosition(dt, lat, lon);
    const alt = pos.altitude, azi = pos.azimuth;
    const solarTime = pos.solarTime, eot = pos.eot;
    const hours = Math.floor(solarTime);
    const minutes = Math.floor((solarTime - hours) * 60);

    if (dialOnly) {
        console.log(drawSundial(azi, true));
        return;
    }
    const latStr = `${Math.abs(lat).toFixed(2)}°${lat >= 0 ? 'N' : 'S'}`;
    const lonStr = `${Math.abs(lon).toFixed(2)}°${lon >= 0 ? 'E' : 'W'}`;
    console.log(`\n☀️ Sundial Widget`);
    console.log(`Location: ${latStr}, ${lonStr}`);
    console.log(`Date: ${dt.toISOString().slice(0,16).replace('T',' ')}`);
    console.log(`\nSolar Time: ${String(hours).padStart(2,'0')}:${String(minutes).padStart(2,'0')} (Equation: ${eot >= 0 ? '+' : ''}${eot.toFixed(1)} min)`);
    console.log(`Solar Altitude: ${alt.toFixed(1)}°`);
    console.log(`Solar Azimuth: ${azi.toFixed(1)}°`);
    console.log(`\n${drawSundial(azi, false)}`);
}

program
    .option('--lat <lat>', 'Latitude (positive North)', parseFloat, DEFAULT_LAT)
    .option('--lon <lon>', 'Longitude (positive East)', parseFloat, DEFAULT_LON)
    .option('--date <date>', 'YYYY-MM-DD')
    .option('--time <time>', 'HH:MM')
    .option('--save-location <name>', 'Save location with name')
    .option('--dial-only', 'Show only the dial')
    .parse(process.argv);

const opts = program.opts();
const config = loadConfig();

if (opts.saveLocation) {
    config.location = { name: opts.saveLocation, lat: opts.lat, lon: opts.lon };
    saveConfig(config);
    console.log(`✅ Location '${opts.saveLocation}' saved.`);
}

let lat = opts.lat || config.location.lat || DEFAULT_LAT;
let lon = opts.lon || config.location.lon || DEFAULT_LON;

let dt = new Date();
if (opts.date) {
    const parts = opts.date.split('-').map(Number);
    dt.setFullYear(parts[0], parts[1]-1, parts[2]);
}
if (opts.time) {
    const [h, m] = opts.time.split(':').map(Number);
    dt.setHours(h, m, 0, 0);
}
// Use UTC
dt = new Date(dt.toISOString());

render(dt, lat, lon, opts.dialOnly);
