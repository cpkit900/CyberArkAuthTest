# CyberArk Auth App

This is a WinForms application that demonstrates authentication with CyberArk using OIDC and SAML.

## Prerequisites

- .NET 6.0 SDK or higher
- WebView2 Runtime (usually installed on Windows 10/11)

## How to Run

### Command Line
1. Open a terminal (PowerShell or Command Prompt).
2. Navigate to the project directory:
   ```powershell
   cd c:\Users\Junnu\CyberArkTest\CyberArkAuthApp
   ```
3. Run the application:
   ```powershell
   dotnet run
   ```

### Visual Studio to Run
1. Open `C:\Users\Junnu\CyberArkTest\CyberArkAuthApp\CyberArkAuthApp.csproj` in Visual Studio.
2. Press `F5` or click the "Start" button.

## Usage
1. Enter your **Tenant ID** (subdomain of `id.cyberark.cloud`).
2. Enter your **Email**.
3. Select **Mode** (OIDC or SAML).
4. Click **Start**.
5. Authenticate in the embedded browser.
6. Once authenticated, the app will attempt to fetch accounts and display them in the grid.
