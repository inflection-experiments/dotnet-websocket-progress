import { writable } from 'svelte/store';
import { browser } from '$app/environment';

interface WebSocketMessage {
	type: string;
	data?: any;
	timestamp: string;
}

interface TaskItem {
	id: string;
	name: string;
	status: string;
	progress: number;
	clientId?: string;
	result?: string;
	error?: string;
	createdAt: string;
	startedAt?: string;
	completedAt?: string;
}

interface ConnectionState {
	connected: boolean;
	connecting: boolean;
	socketId?: string;
}

// Stores
export const connectionState = writable<ConnectionState>({
	connected: false,
	connecting: false
});

export const tasks = writable<Map<string, TaskItem>>(new Map());
export const logs = writable<string[]>([]);
export const stats = writable({
	connections: 0,
	activeTasks: 0,
	completedTasks: 0
});

class WebSocketManager {
	private socket: WebSocket | null = null;
	private reconnectAttempts = 0;
	private maxReconnectAttempts = 5;
	private reconnectInterval = 1000;
	private isManualDisconnect = false;

	connect() {
		if (!browser) return;
		
		if (this.socket && this.socket.readyState === WebSocket.OPEN) {
			this.addLog('Already connected');
			return;
		}

		const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
		const wsUrl = `${protocol}//localhost:5000/ws`; // Backend URL
		
		connectionState.update(state => ({ ...state, connecting: true }));
		this.addLog(`Attempting to connect to ${wsUrl}`);
		this.isManualDisconnect = false;

		try {
			this.socket = new WebSocket(wsUrl);
			
			this.socket.onopen = (event) => {
				this.addLog('WebSocket connected successfully');
				connectionState.update(state => ({ 
					...state, 
					connected: true, 
					connecting: false 
				}));
				this.reconnectAttempts = 0;
			};

			this.socket.onmessage = (event) => {
				try {
					const message: WebSocketMessage = JSON.parse(event.data);
					this.handleWebSocketMessage(message);
				} catch (error) {
					this.addLog(`Error parsing message: ${error}`);
				}
			};

			this.socket.onclose = (event) => {
				this.addLog(`WebSocket closed: ${event.code} - ${event.reason || 'No reason provided'}`);
				connectionState.update(state => ({ 
					...state, 
					connected: false, 
					connecting: false 
				}));
				
				// Attempt to reconnect if not manually disconnected
				if (!this.isManualDisconnect && this.reconnectAttempts < this.maxReconnectAttempts) {
					setTimeout(() => this.attemptReconnect(), this.reconnectInterval * Math.pow(2, this.reconnectAttempts));
				}
			};

			this.socket.onerror = (event) => {
				this.addLog('WebSocket error occurred');
				connectionState.update(state => ({ 
					...state, 
					connected: false, 
					connecting: false 
				}));
			};

		} catch (error) {
			this.addLog(`Failed to create WebSocket: ${error}`);
			connectionState.update(state => ({ 
				...state, 
				connected: false, 
				connecting: false 
			}));
		}
	}

	disconnect() {
		this.isManualDisconnect = true;
		if (this.socket) {
			this.socket.close(1000, 'Manual disconnect');
			this.socket = null;
		}
		connectionState.update(state => ({ 
			...state, 
			connected: false, 
			connecting: false 
		}));
		this.addLog('Manually disconnected');
	}

	private attemptReconnect() {
		this.reconnectAttempts++;
		this.addLog(`Reconnection attempt ${this.reconnectAttempts}/${this.maxReconnectAttempts}`);
		this.connect();
	}

	ping() {
		if (this.socket && this.socket.readyState === WebSocket.OPEN) {
			const message = JSON.stringify({
				type: 'ping',
				timestamp: new Date().toISOString()
			});
			this.socket.send(message);
			this.addLog('Ping sent to server');
		} else {
			this.addLog('Cannot ping: WebSocket not connected');
		}
	}

	private handleWebSocketMessage(message: WebSocketMessage) {
		this.addLog(`Received: ${message.type}`);
		
		switch (message.type) {
			case 'connection':
				this.handleConnectionMessage(message.data);
				break;
			case 'taskQueued':
				this.handleTaskQueued(message.data);
				break;
			case 'taskProgress':
				this.handleTaskProgress(message.data);
				break;
			case 'pong':
				this.addLog('Pong received from server');
				break;
			default:
				this.addLog(`Unknown message type: ${message.type}`);
		}
	}

	private handleConnectionMessage(data: any) {
		this.addLog(`Connected with socket ID: ${data.socketId}`);
		connectionState.update(state => ({ 
			...state, 
			socketId: data.socketId 
		}));
		stats.update(s => ({ ...s, connections: 1 }));
	}

	private handleTaskQueued(taskData: TaskItem) {
		this.addLog(`Task queued: ${taskData.id}`);
		tasks.update(tasksMap => {
			tasksMap.set(taskData.id, taskData);
			return tasksMap;
		});
		stats.update(s => ({ ...s, activeTasks: s.activeTasks + 1 }));
	}

	private handleTaskProgress(taskData: TaskItem) {
		this.addLog(`Task ${taskData.id}: ${taskData.status} (${taskData.progress}%)`);
		
		tasks.update(tasksMap => {
			const existingTask = tasksMap.get(taskData.id);
			const wasCompleted = existingTask && (existingTask.status === 'Completed' || existingTask.status === 'Failed');
			const isNowCompleted = taskData.status === 'Completed' || taskData.status === 'Failed';
			
			tasksMap.set(taskData.id, taskData);
			
			if (!wasCompleted && isNowCompleted) {
				stats.update(s => ({
					...s,
					activeTasks: Math.max(0, s.activeTasks - 1),
					completedTasks: s.completedTasks + 1
				}));
			}
			
			return tasksMap;
		});
	}

	private addLog(message: string) {
		const timestamp = new Date().toLocaleTimeString();
		const logEntry = `[${timestamp}] ${message}`;
		logs.update(logList => [...logList, logEntry]);
	}

	clearLogs() {
		logs.set([]);
	}

	clearTasks() {
		tasks.set(new Map());
		stats.update(s => ({ ...s, activeTasks: 0, completedTasks: 0 }));
		this.addLog('Task list cleared');
	}

	getSocketId(): string | null {
		// Get the current socket ID from the connection state
		let currentSocketId: string | null = null;
		connectionState.subscribe(state => {
			currentSocketId = state.socketId || null;
		})();
		return currentSocketId;
	}
}

export const wsManager = new WebSocketManager();