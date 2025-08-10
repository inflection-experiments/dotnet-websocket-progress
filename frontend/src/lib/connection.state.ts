import { writable } from 'svelte/store';

export interface WebSocketMessage {
	type: string;
	data?: any;
	timestamp: string;
}
export interface TaskItem {
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
