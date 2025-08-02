# WebSockets Background Task Monitor

A full-stack application demonstrating asynchronous background task processing with real-time updates via WebSockets.

## Architecture

**Backend**: ASP.NET Core 8.0 Web API with WebSockets
**Frontend**: SvelteKit 2.x with Svelte 5 (Server-Side Rendered)

### Key Features

- ✅ Asynchronous background task queue using `System.Threading.Channels`
- ✅ Real-time progress updates via WebSockets (not SignalR)
- ✅ **Multi-client task isolation** - Each client only sees their own tasks
- ✅ **Client-specific messaging** - Progress updates sent only to originating client
- ✅ Server-side rendered SvelteKit frontend
- ✅ Task status tracking and monitoring
- ✅ Connection management with auto-reconnection
- ✅ Flexible client identification (Socket ID, Session ID, Custom ID)
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
- `taskQueued` - New task added to queue (sent only to originating client)
- `taskProgress` - Task progress update (sent only to originating client)
- `pong` - Response to ping

### Client Identification

The system supports multiple methods for identifying clients to ensure tasks are properly isolated:

**Priority Order:**
1. `clientId` in request body
2. `X-Socket-Id` header (for WebSocket clients)
3. `X-Session-Id` header (for session-based clients)
4. `X-Client-Id` header (generic identifier)
5. Auto-generated fallback ID

**Task Request with Client ID:**
```json
{
  "name": "Sample Task",
  "duration": 5000,
  "clientId": "your-client-id"
}
```

**HTTP Headers Example:**
```
POST /api/task/start
Content-Type: application/json
X-Socket-Id: abc123def456
```

## Client Isolation Architecture

### How It Works

The system implements **true multi-client isolation** where each WebSocket connection represents a unique client, and tasks are completely separated between clients:

1. **WebSocket Connection**: Each client receives a unique socket ID upon connection
2. **Task Association**: Tasks are tagged with the client ID who triggered them  
3. **Targeted Updates**: Progress messages are sent only to the originating client
4. **Zero Cross-Talk**: Clients cannot see or receive updates from other clients' tasks

### Implementation Details

**Backend Components:**
- `WebSocketManager`: Maintains client-to-socket mapping for targeted messaging
- `TaskController`: Extracts client ID from requests using multiple fallback methods
- `TaskProcessor`: Sends progress updates only to the task's originating client
- `TaskItem.ClientId`: Associates each task with its creator

**Frontend Integration:**
- Automatic socket ID capture and storage
- Client ID included in all task requests
- Only client-specific task updates displayed

### Benefits

✅ **Scalability**: Supports unlimited concurrent clients without interference  
✅ **Privacy**: Complete task isolation between users/sessions  
✅ **Performance**: Targeted messaging reduces unnecessary network traffic  
✅ **Flexibility**: Multiple client identification methods supported  
✅ **Real-time**: Instant updates only to relevant clients

### Visual Architecture

The diagram above illustrates how the system maintains complete client isolation while processing tasks efficiently through a shared queue system.

## Usage

1. **Open the frontend** at `http://localhost:5173`
2. **Connect to WebSocket** - Should auto-connect on page load with unique socket ID
3. **Start a task:**
   - Enter task name
   - Set duration (1000-30000ms)
   - Click "Start Task" (automatically uses your socket ID)
4. **Monitor progress** in real-time via WebSocket updates (only your tasks)
5. **View logs** for connection and task events

### Multi-Client Testing

To test client isolation:
1. **Open multiple browser tabs/windows** at `http://localhost:5173`
2. **Each tab gets a unique socket ID** shown in connection status
3. **Start tasks in different tabs** - each tab only shows its own tasks
4. **Verify isolation** - tasks in one tab don't appear in others

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
- ✅ **Multi-client task isolation**
- ✅ **Client-specific progress updates**
- ✅ Connection interruption/reconnection
- ✅ Page refresh with task persistence
- ✅ Long-running tasks
- ✅ Task failure handling
- ✅ **Cross-client interference prevention**

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