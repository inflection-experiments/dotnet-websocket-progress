<script lang="ts">
	import { onMount } from 'svelte';
	import { connectionState, tasks, logs, stats, wsManager } from '$lib/websocket';
	import { apiClient } from '$lib/api';
	import ConnectionStatus from '$lib/components/ConnectionStatus.svelte';
	import TaskItem from '$lib/components/TaskItem.svelte';

	let taskName = $state('Sample Task');
	let taskDuration = $state(5000);
	let isStartingTask = $state(false);

	onMount(() => {
		// Connect to WebSocket when the component mounts
		wsManager.connect();

		// Handle page visibility change
		const handleVisibilityChange = () => {
			if (document.hidden) {
				console.log('Page hidden - maintaining connection');
			} else {
				console.log('Page visible - checking connection');
				if (!$connectionState.connected) {
					wsManager.connect();
				}
			}
		};

		document.addEventListener('visibilitychange', handleVisibilityChange);

		// Cleanup on component destroy
		return () => {
			document.removeEventListener('visibilitychange', handleVisibilityChange);
		};
	});

	async function startTask() {
		if (!taskName.trim()) {
			alert('Please enter a task name');
			return;
		}

		if (!$connectionState.connected) {
			alert('WebSocket is not connected');
			return;
		}

		isStartingTask = true;
		try {
			// Get the socket ID for this client
			const socketId = $connectionState.socketId;
			
			const result = await apiClient.startTask({
				name: taskName,
				duration: taskDuration
			}, socketId);

			console.log(`Task started: ${result.taskId} for client: ${socketId}`);
		} catch (error) {
			console.error('Error starting task:', error);
			alert(`Error starting task: ${error}`);
		} finally {
			isStartingTask = false;
		}
	}

	function clearTasks() {
		wsManager.clearTasks();
	}

	function clearLogs() {
		wsManager.clearLogs();
	}

	// Reactive computed values using Svelte 5 runes
	const sortedTasks = $derived(Array.from($tasks.values()).sort((a, b) => 
		new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()
	));
</script>

<svelte:head>
	<title>Background Task Monitor</title>
</svelte:head>

<h1>Background Task Monitor</h1>

<div class="container">
	<ConnectionStatus connection={connectionState} />
	
	<div class="stats">
		<div class="stat-item">
			<div class="stat-value">{$stats.connections}</div>
			<div class="stat-label">Connections</div>
		</div>
		<div class="stat-item">
			<div class="stat-value">{$stats.activeTasks}</div>
			<div class="stat-label">Active Tasks</div>
		</div>
		<div class="stat-item">
			<div class="stat-value">{$stats.completedTasks}</div>
			<div class="stat-label">Completed</div>
		</div>
	</div>
	
	<div class="task-form">
		<div class="form-group">
			<label for="taskName">Task Name:</label>
			<input 
				type="text" 
				id="taskName" 
				bind:value={taskName}
				placeholder="Enter task name"
			/>
		</div>
		<div class="form-group">
			<label for="taskDuration">Duration (ms):</label>
			<input 
				type="number" 
				id="taskDuration" 
				bind:value={taskDuration}
				min="1000" 
				max="30000"
			/>
		</div>
		<button 
			onclick={startTask} 
			disabled={!$connectionState.connected || isStartingTask}
		>
			{isStartingTask ? 'Starting...' : 'Start Task'}
		</button>
		<button onclick={clearTasks}>Clear All</button>
	</div>
</div>

<div class="container">
	<h3>Active Tasks</h3>
	<div class="task-list">
		{#if sortedTasks.length === 0}
			<p>No tasks running</p>
		{:else}
			{#each sortedTasks as task (task.id)}
				<TaskItem {task} />
			{/each}
		{/if}
	</div>
</div>

<div class="container">
	<h3>Connection Logs</h3>
	<button onclick={clearLogs} class="clear-logs-btn">Clear Logs</button>
	<div class="logs">
		{#each $logs as logEntry}
			<div class="log-entry">{logEntry}</div>
		{/each}
	</div>
</div>

<style>
	.stats {
		display: flex;
		gap: 20px;
		margin-bottom: 20px;
		flex-wrap: wrap;
	}

	.stat-item {
		background-color: #f8f9fa;
		padding: 10px;
		border-radius: 4px;
		text-align: center;
		min-width: 100px;
		flex: 1;
	}

	.stat-value {
		font-size: 1.5em;
		font-weight: bold;
		color: #007bff;
	}

	.stat-label {
		font-size: 0.9em;
		color: #666;
	}

	.task-form {
		display: flex;
		gap: 10px;
		margin-bottom: 20px;
		align-items: end;
		flex-wrap: wrap;
	}

	.form-group {
		display: flex;
		flex-direction: column;
		min-width: 200px;
	}

	.task-list {
		min-height: 50px;
	}

	.logs {
		max-height: 300px;
		overflow-y: auto;
		background-color: #f8f9fa;
		border: 1px solid #ddd;
		padding: 10px;
		font-family: monospace;
		font-size: 0.85em;
		border-radius: 4px;
	}

	.log-entry {
		margin-bottom: 5px;
		padding: 2px 0;
		word-break: break-word;
	}

	.clear-logs-btn {
		float: right;
		margin-bottom: 10px;
		font-size: 0.9em;
	}

	/* Responsive design */
	@media (max-width: 768px) {
		.task-form {
			flex-direction: column;
			align-items: stretch;
		}

		.form-group {
			min-width: auto;
		}

		.stats {
			flex-direction: column;
		}

		.stat-item {
			min-width: auto;
		}
	}
</style>