#!/bin/bash

# Quick start script for WebSockets Background Task Monitor
# This script will start both the backend and frontend in parallel

echo "🚀 Starting WebSockets Background Task Monitor..."

# Check if .NET is installed
if ! command -v dotnet &> /dev/null; then
    echo "❌ .NET SDK not found. Please install .NET 8.0 SDK first."
    exit 1
fi

# Check if Node.js is installed
if ! command -v node &> /dev/null; then
    echo "❌ Node.js not found. Please install Node.js 18+ first."
    exit 1
fi

echo "✅ Prerequisites check passed"

# Setup backend
echo "📦 Setting up backend..."
cd backend
if [ ! -d "bin" ]; then
    echo "🔧 Restoring backend packages..."
    dotnet restore
fi

# Setup frontend
echo "📦 Setting up frontend..."
cd ../frontend
if [ ! -d "node_modules" ]; then
    echo "🔧 Installing frontend dependencies..."
    npm install
fi

echo "🎉 Setup complete!"
echo ""
echo "🌐 Starting services..."
echo "   Backend: http://localhost:5000"
echo "   Frontend: http://localhost:5173"
echo ""
echo "📝 To stop the services, press Ctrl+C"
echo ""

# Start both services in parallel
cd ../backend && dotnet run &
BACKEND_PID=$!

cd ../frontend && npm run dev &
FRONTEND_PID=$!

# Wait for both processes
wait $BACKEND_PID $FRONTEND_PID