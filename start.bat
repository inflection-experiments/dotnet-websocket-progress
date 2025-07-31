@echo off

REM Quick start script for WebSockets Background Task Monitor (Windows)
REM This script will start both the backend and frontend

echo 🚀 Starting WebSockets Background Task Monitor...

REM Check if .NET is installed
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo ❌ .NET SDK not found. Please install .NET 8.0 SDK first.
    pause
    exit /b 1
)

REM Check if Node.js is installed
node --version >nul 2>&1
if errorlevel 1 (
    echo ❌ Node.js not found. Please install Node.js 18+ first.
    pause
    exit /b 1
)

echo ✅ Prerequisites check passed

REM Setup backend
echo 📦 Setting up backend...
cd backend
if not exist "bin" (
    echo 🔧 Restoring backend packages...
    dotnet restore
)

REM Setup frontend
echo 📦 Setting up frontend...
cd ..\frontend
if not exist "node_modules" (
    echo 🔧 Installing frontend dependencies...
    npm install
)

echo 🎉 Setup complete!
echo.
echo 🌐 Starting services...
echo    Backend: http://localhost:5000
echo    Frontend: http://localhost:5173
echo.
echo 📝 To stop the services, press Ctrl+C in both terminal windows
echo.

REM Start both services in separate windows
start "Backend Server" cmd /k "cd ..\backend && dotnet run"
start "Frontend Server" cmd /k "cd ..\frontend && npm run dev"

echo Both servers are starting in separate windows...
pause