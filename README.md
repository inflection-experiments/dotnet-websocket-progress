# WebSockets Background Task Monitor

A full-stack application demonstrating asynchronous background task processing with real-time updates via WebSockets.

## Architecture

**Backend**: ASP.NET Core 8.0 Web API with WebSockets
**Frontend**: SvelteKit 2.x with Svelte 5 (Server-Side Rendered)

### Key Features

- ✅ Asynchronous background task queue using `System.Threading.Channels`
- ✅ Real-time progress updates via WebSockets (not SignalR)
- ✅ Server-side rendered SvelteKit frontend
- ✅ Task status tracking and monitoring
- ✅ Connection management with auto-reconnection
- ✅ Responsive UI with connection logs

## Project Structure

```
websockets-progress/
├── backend/                    # ASP.NET Core Web API
│   ├── Controllers/
│   │   └── TaskController.cs   # API endpoints
│   ├── Models/                 # Data models
│   │   ├── TaskItem.cs
│   │   ├── TaskRequest.cs
│   │   ├── TaskResponse.cs
│   │   └── WebSocketMessage.cs
│   ├── Services/               # Business logic
│   │   ├── ITaskQueue.cs
│   │   ├── TaskQueue.cs
│   │   ├── IWebSocketManager.cs
│   │   ├── WebSocketManager.cs
│   │   ├── ITaskProcessor.cs
│   │   ├── TaskProcessor.cs
│   │   └── BackgroundTaskService.cs
│   ├── Program.cs              # Application configuration
│   └── websockets-backend.csproj
├── frontend/                   # SvelteKit Application
│   ├── src/
│   │   ├── lib/
│   │   │   ├── components/     # Svelte components
│   │   │   ├── websocket.ts    # WebSocket client
│   │   │   └── api.ts          # HTTP client
│   │   ├── routes/
│   │   │   ├── +layout.svelte  # Layout component
│   │   │   └── +page.svelte    # Main page
│   │   └── app.html
│   ├── package.json
│   ├── svelte.config.js
│   ├── vite.config.js
│   └── tsconfig.json
└── README.md
```

## Setup Instructions

### Prerequisites

- .NET 8.0 SDK
- Node.js 18+ with npm/pnpm
- A code editor (VS Code recommended)

### Backend Setup

1. **Navigate to the backend directory:**
   ```bash
   cd backend
   ```

2. **Restore packages:**
   ```bash
   dotnet restore
   ```

3. **Build the project:**
   ```bash
   dotnet build
   ```

4. **Run the backend (Development):**
   ```bash
   dotnet run
   ```
   
   The backend will be available at: `http://localhost:5000`

### Frontend Setup

1. **Navigate to the frontend directory:**
   ```bash
   cd frontend
   ```

2. **Install dependencies:**
   ```bash
   npm install
   # or
   pnpm install
   ```

3. **Run the development server:**
   ```bash
   npm run dev
   # or
   pnpm dev
   ```
   
   The frontend will be available at: `http://localhost:5173`

## Running the Application

### Method 1: Two Terminal Windows

**Terminal 1 (Backend):**
```bash
cd backend
dotnet run
```

**Terminal 2 (Frontend):**
```bash
cd frontend
npm run dev
```

### Method 2: Production Build

**Build Frontend:**
```bash
cd frontend
npm run build
```

**Run Backend (serves both API and frontend):**
```bash
cd backend
dotnet run --environment Production
```

## API Endpoints

### Backend API (Port 5000)

- `GET /` - API information and available endpoints
- `GET /health` - Health check endpoint
- `GET /api/task/status` - Service status with connection info
- `POST /api/task/start` - Start a new background task
- `WebSocket /ws` - WebSocket connection for real-time updates

### WebSocket Messages

**Client to Server:**
- `ping` - Ping the server for connectivity test

**Server to Client:**
- `connection` - Connection established with socket ID
- `taskQueued` - New task added to queue
- `taskProgress` - Task progress update
- `pong` - Response to ping

## Usage

1. **Open the frontend** at `http://localhost:5173`
2. **Connect to WebSocket** - Should auto-connect on page load
3. **Start a task:**
   - Enter task name
   - Set duration (1000-30000ms)
   - Click "Start Task"
4. **Monitor progress** in real-time via WebSocket updates
5. **View logs** for connection and task events

## Development

### Backend Development

**Watch mode (auto-restart on changes):**
```bash
cd backend
dotnet watch run
```

**View logs:**
- Console output shows detailed logging
- Check WebSocket connection status
- Monitor task queue and processing

### Frontend Development

**Development server with hot reload:**
```bash
cd frontend
npm run dev
```

**Type checking:**
```bash
cd frontend
npm run check
```

**Build for production:**
```bash
cd frontend
npm run build
npm run preview
```

## Configuration

### Backend Configuration

The backend uses environment-based configuration:

**Development (appsettings.Development.json):**
- Detailed logging enabled
- CORS allows localhost origins
- WebSocket keep-alive: 30 seconds

**Production (appsettings.json):**
- Reduced logging
- Configure allowed origins
- Performance optimizations

### Frontend Configuration

**Environment Variables:**
- API base URL configurable via environment
- WebSocket URL auto-detected from location

## Testing

### Manual Testing Workflow

1. **Start both backend and frontend**
2. **Test WebSocket connection:**
   - Verify connection status shows "Connected"
   - Test ping functionality
   - Check connection logs
3. **Test task processing:**
   - Start multiple tasks with different durations
   - Verify real-time progress updates
   - Check task completion status
4. **Test reconnection:**
   - Stop backend, verify disconnect
   - Restart backend, verify auto-reconnection

### Test Scenarios

- ✅ Single task processing
- ✅ Multiple concurrent tasks
- ✅ Connection interruption/reconnection
- ✅ Page refresh with task persistence
- ✅ Long-running tasks
- ✅ Task failure handling

## Troubleshooting

### Common Issues

**WebSocket Connection Failed:**
- Ensure backend is running on port 5000
- Check CORS configuration
- Verify WebSocket endpoint `/ws`

**Tasks Not Starting:**
- Check API endpoint `/api/task/start`
- Verify request payload format
- Check backend logs for errors

**Frontend Build Errors:**
- Clear `node_modules` and reinstall
- Check Node.js version (18+)
- Verify TypeScript configuration

### Debug Commands

**Backend:**
```bash
cd backend
dotnet run --verbosity detailed
```

**Frontend:**
```bash
cd frontend
npm run dev -- --debug
```

## Production Deployment

### Backend Deployment

```bash
cd backend
dotnet publish -c Release -o ./publish
```

### Frontend Deployment

```bash
cd frontend
npm run build
```

The built frontend files will be in the `build` directory and can be served by any static file server or integrated with the backend.

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Test thoroughly
5. Submit a pull request

## License

MIT License - See LICENSE file for details.