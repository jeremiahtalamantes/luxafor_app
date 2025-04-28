# Luxafor Console Service

A minimal .NET 9 console application that exposes an HTTP API to control the [Luxafor Flag light](https://luxafor.com/product/flag/) without relying on the official Luxafor desktop app.  

In multi-user or lab environments, the Luxafor desktop application ties each light to the individual user’s profile—making the device ID unpredictable as different students log in. This tool sidesteps that by binding the USB light at the machine level and providing a simple HTTP interface. The light stays permanently attached to the PC itself, ensuring consistent behavior regardless of who’s signed in.

It supports:

```
luxafor_app/
├── global.json              # Locks .NET SDK to 9.0.203
├── luxafor_console.csproj   # Project file
├── Program.cs               # Main application logic
└── README.md                # This documentation
```

---

## Prerequisites

- Windows 10/11 (for running/publishing console app)
- [.NET 9.0 SDK (9.0.203)](https://dotnet.microsoft.com/download/dotnet/9.0)
- Luxafor USB light attached

---

## Building & Publishing

1. **Clone the repo** on your build machine:
   ```bash
   git clone https://github.com/jeremiahtalamantes/luxafor_app.git
   cd luxafor_app
   ```
2. **Restore & Run** interactively:
   ```bash
   dotnet restore
   dotnet run
   ```
3. **Publish a self-contained EXE** for deployment:
   ```bash
   dotnet publish -c Release \
     -r win-x64 \
     --self-contained true \
     --output publish/
   ```
   Output in `publish/` will include `luxafor_console.exe`, all runtime DLLs, and your HID library.

---

## HTTP API Endpoints

- **Set steady color**
  ```http
  POST http://<host>:9123/api/v1.5/command/color
  Content-Type: application/json

  { "color": "green" }
  ```

- **Blink**
  ```http
  POST http://<host>:9123/api/v1.5/command/blink
  Content-Type: application/json

  {
    "color": "blue",
    "onDuration": 300,
    "offDuration": 300,
    "count": 5    # 0 = infinite
  }
  ```

- **Stop blink**
  ```http
  POST http://<host>:9123/api/v1.5/command/stop-blink
  ```

---

## Testing Locally

Use **curl** or **PowerShell**:

```bash
# Set solid red
curl -X POST http://localhost:9123/api/v1.5/command/color \
     -H "Content-Type: application/json" \
     -d '{"color":"red"}'

# Blink green 10 times
curl -X POST http://localhost:9123/api/v1.5/command/blink \
     -H "Content-Type: application/json" \
     -d '{"color":"green","onDuration":500,"offDuration":500,"count":10}'

# Stop blink
curl -X POST http://localhost:9123/api/v1.5/command/stop-blink
```

---

## Running as a Windows Service

1. **Copy** published output to `C:\Program Files\luxafor_console\` and secure folder permissions.
2. **Install NSSM** (Non-Sucking Service Manager) to system PATH.
3. **Create service**:
   ```powershell
   nssm install LuxaforConsole "C:\Program Files\luxafor_console\luxafor_console.exe"
   nssm set LuxaforConsole AppDirectory "C:\Program Files\luxafor_console"
   nssm set LuxaforConsole Start SERVICE_AUTO_START
   nssm start LuxaforConsole
   ```
4. **Verify** listening on port 9123 with:
   ```powershell
   netsh advfirewall firewall add rule name="Luxafor HTTP Port 9123" dir=in action=allow protocol=TCP localport=9123
   netstat -ano | findstr :9123
   ```

---

## Firewall & Networking

- **Firewall rule** (via script or GPO) to allow inbound TCP 9123 on Domain/Private:
  ```powershell
  New-NetFirewallRule -DisplayName "Luxafor HTTP Port 9123" \
    -Direction Inbound -Action Allow -Protocol TCP -LocalPort 9123 -Profile Any
  ```

- **Group Policy**: Create a Computer Configuration → Windows Settings → Security Settings → Windows Defender Firewall → Inbound Rule for TCP 9123.

---



