☀️ Sundial Widget — Multi‑Language Solar Time Display
8 languages, one beautiful sundial widget – show the current time, shadow direction, and solar position on a compact ASCII sundial – right from your terminal.

✨ Features
🕐 Real‑time display – shows current time and solar position

🌍 Location support – set latitude/longitude for accurate shadow

📅 Custom date/time – see the sundial for any moment

🎨 ASCII dial – a compact, elegant representation of a horizontal sundial

🧭 Shadow direction – shows where the shadow falls based on solar azimuth

🌞 Solar altitude & azimuth – displayed alongside the dial

💾 Persistent location – save your favorite spot in a config file

🚀 Quick Start
All implementations share the same CLI interface:

bash
# Show current sundial (default location 0°,0°)
<command>

# Specify location
<command> --lat 40.7128 --lon -74.0060

# Show for a specific date and time
<command> --date 2026-08-22 --time 14:30

# Save a location for future use
<command> --save-location "Paris" --lat 48.8584 --lon 2.2945

# Show only the dial (no extra info)
<command> --dial-only
Arguments:

--lat – latitude in degrees (positive North)

--lon – longitude in degrees (positive East)

--date – YYYY-MM-DD (default: today)

--time – HH:MM (default: current time)

--save-location <name> – save current location

--dial-only – output only the ASCII sundial

📸 Example Output
text
☀️ Sundial Widget
Location: 48.86°N, 2.29°E
Date: 2026-08-22 14:30

Solar Time: 14:28 (Equation: -2.5 min)
Solar Altitude: 45.2°
Solar Azimuth: 210.7° (SW)

      N
      |
  W---+---E
      |
      S
Shadow direction: SW (210.7°)

   ████████████
  ███  ████  ███
 ████    ██    ████
 ████    ██    ████
  ███  ████  ███
   ████████████
    (shadow point)
(Actual ASCII dial shows the shadow as a line/point)

📁 Repository Structure
text
.
├── README.md
├── python/
│   └── sundial_widget.py
├── go/
│   └── sundial_widget.go
├── javascript/
│   └── sundial_widget.js
├── ruby/
│   └── sundial_widget.rb
├── php/
│   └── sundial_widget.php
├── java/
│   └── SundialWidget.java
├── csharp/
│   └── SundialWidget.cs
└── cpp/
    └── sundial_widget.cpp
