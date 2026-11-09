Windows desktop client of VoltDesk: one self-contained `.exe`, no .NET install needed.

### Run it

1. Start the server with demo data (Docker required):

   ```bash
   git clone https://github.com/kyrylo-papuk/VoltDesk.git
   cd VoltDesk
   docker compose up -d
   ```

2. Unzip the attached `win-x64` archive and run `VoltDesk.Desktop.exe`.
3. Sign in with one of the demo account buttons.

The app is not code-signed, so Windows SmartScreen may ask: choose *More info → Run anyway*.

The API image is also published to GitHub Container Registry as `ghcr.io/kyrylo-papuk/voltdesk-api`.
See the [README](https://github.com/kyrylo-papuk/VoltDesk#readme) for screenshots, architecture and details.
