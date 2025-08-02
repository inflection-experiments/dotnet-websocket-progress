const API_BASE_URL = 'http://localhost:5000/api';

interface TaskRequest {
	name: string;
	duration: number;
	clientId?: string;
	parameters?: Record<string, any>;
}

interface TaskResponse {
	taskId: string;
	message: string;
	status: string;
	timestamp: string;
}

interface ServiceStatus {
	message: string;
	timestamp: string;
	activeWebSocketConnections: number;
	queuedTasks: number;
	status: string;
}

export class ApiClient {
	async startTask(request: TaskRequest, socketId?: string): Promise<TaskResponse> {
		const headers: Record<string, string> = {
			'Content-Type': 'application/json',
		};

		// Add socket ID to headers if provided
		if (socketId) {
			headers['X-Socket-Id'] = socketId;
		}

		// Include clientId in request body if not already set
		const requestBody = {
			...request,
			clientId: request.clientId || socketId
		};

		const response = await fetch(`${API_BASE_URL}/task/start`, {
			method: 'POST',
			headers,
			body: JSON.stringify(requestBody)
		});

		if (!response.ok) {
			const errorData = await response.json().catch(() => ({ message: 'Unknown error' }));
			throw new Error(errorData.message || `HTTP ${response.status}`);
		}

		return await response.json();
	}

	async getServiceStatus(): Promise<ServiceStatus> {
		const response = await fetch(`${API_BASE_URL}/task/status`);

		if (!response.ok) {
			throw new Error(`HTTP ${response.status}`);
		}

		return await response.json();
	}

	async checkHealth(): Promise<any> {
		const response = await fetch(`${API_BASE_URL}/task/health`);

		if (!response.ok) {
			throw new Error(`HTTP ${response.status}`);
		}

		return await response.json();
	}
}

export const apiClient = new ApiClient();