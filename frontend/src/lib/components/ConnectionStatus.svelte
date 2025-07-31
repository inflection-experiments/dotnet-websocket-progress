<script lang="ts">
	import { connectionState, wsManager } from '$lib/websocket';

	let { connection } = $props<{ connection: typeof connectionState }>();

	function handleConnect() {
		wsManager.connect();
	}

	function handleDisconnect() {
		wsManager.disconnect();
	}

	function handlePing() {
		wsManager.ping();
	}

	function getStatusClass(state: any): string {
		if (state.connected) return 'connected';
		if (state.connecting) return 'connecting';
		return 'disconnected';
	}

	function getStatusText(state: any): string {
		if (state.connected) return 'Connected';
		if (state.connecting) return 'Connecting...';
		return 'Disconnected';
	}
</script>

<div class="connection-status {getStatusClass($connection)}">
	<span class="status-text">{getStatusText($connection)}</span>
	<div class="connection-controls">
		<button 
			onclick={handleConnect} 
			disabled={$connection.connected || $connection.connecting}
		>
			Connect
		</button>
		<button 
			onclick={handleDisconnect} 
			disabled={!$connection.connected && !$connection.connecting}
		>
			Disconnect
		</button>
		<button onclick={handlePing}>Ping</button>
	</div>
</div>

<style>
	.connection-status {
		padding: 10px;
		border-radius: 4px;
		font-weight: bold;
		margin-bottom: 20px;
		display: flex;
		justify-content: space-between;
		align-items: center;
	}

	.connected { 
		background-color: #d4edda; 
		color: #155724; 
	}

	.disconnected { 
		background-color: #f8d7da; 
		color: #721c24; 
	}

	.connecting { 
		background-color: #fff3cd; 
		color: #856404; 
	}

	.connection-controls {
		display: flex;
		gap: 10px;
	}

	.connection-controls button {
		padding: 5px 10px;
		font-size: 0.9em;
		border: 1px solid #ddd;
		border-radius: 4px;
		background-color: #007bff;
		color: white;
		cursor: pointer;
		font-weight: bold;
	}

	.connection-controls button:hover { 
		background-color: #0056b3; 
	}

	.connection-controls button:disabled { 
		background-color: #6c757d; 
		cursor: not-allowed; 
	}
</style>