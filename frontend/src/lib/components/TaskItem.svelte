<script lang="ts">
	interface TaskItem {
		id: string;
		name: string;
		status: string;
		progress: number;
		result?: string;
		error?: string;
		createdAt: string;
		startedAt?: string;
		completedAt?: string;
	}

	let { task }: { task: TaskItem } = $props();

	function getStatusClass(status: string): string {
		const normalizedStatus = status.toLowerCase().replace(/\s+/g, '-');
		return `status-${normalizedStatus}`;
	}
</script>

<div class="task-item">
	<div class="task-header">
		<div>
			<strong>{task.name}</strong>
			<div class="task-id">ID: {task.id}</div>
		</div>
		<div class="task-status {getStatusClass(task.status)}">{task.status}</div>
	</div>
	
	<div class="progress-bar">
		<div class="progress-fill" style="width: {task.progress}%">
			{task.progress}%
		</div>
	</div>
	
	{#if task.result}
		<div class="task-result">{task.result}</div>
	{/if}
	
	{#if task.error}
		<div class="task-error">Error: {task.error}</div>
	{/if}
</div>

<style>
	.task-item {
		border: 1px solid #ddd;
		border-radius: 4px;
		padding: 15px;
		margin-bottom: 10px;
		background-color: #f8f9fa;
	}

	.task-header {
		display: flex;
		justify-content: space-between;
		align-items: center;
		margin-bottom: 10px;
	}

	.task-id {
		font-family: monospace;
		font-size: 0.9em;
		color: #666;
	}

	.task-status {
		padding: 4px 8px;
		border-radius: 4px;
		font-size: 0.85em;
		font-weight: bold;
	}

	.status-queued { background-color: #fff3cd; color: #856404; }
	:global(.status-running) { background-color: #cce5ff; color: #004085; }
	:global(.status-processing-step-1-10),
	:global(.status-processing-step-2-10),
	:global(.status-processing-step-3-10),  
	:global(.status-processing-step-4-10),
	:global(.status-processing-step-5-10),
	:global(.status-processing-step-6-10),
	:global(.status-processing-step-7-10),
	:global(.status-processing-step-8-10),
	:global(.status-processing-step-9-10),
	:global(.status-processing-step-10-10) { 
		background-color: #cce5ff; color: #004085; 
	}
	.status-completed { background-color: #d4edda; color: #155724; }
	.status-failed { background-color: #f8d7da; color: #721c24; }
	.status-cancelled { background-color: #e2e3e5; color: #383d41; }

	.progress-bar {
		width: 100%;
		height: 20px;
		background-color: #e9ecef;
		border-radius: 10px;
		overflow: hidden;
		margin: 10px 0;
	}

	.progress-fill {
		height: 100%;
		background-color: #28a745;
		transition: width 0.3s ease;
		display: flex;
		align-items: center;
		justify-content: center;
		color: white;
		font-size: 0.8em;
		font-weight: bold;
	}

	.task-result {
		margin-top: 10px;
		padding: 10px;
		background-color: #e8f5e8;
		border-radius: 4px;
		font-size: 0.9em;
	}

	.task-error {
		margin-top: 10px;
		padding: 10px;
		background-color: #ffe6e6;
		border-radius: 4px;
		color: #d63384;
		font-size: 0.9em;
	}
</style>