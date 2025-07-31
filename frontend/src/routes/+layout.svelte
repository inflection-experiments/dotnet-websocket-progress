<script lang="ts">
	import { onMount } from 'svelte';
	import { wsManager } from '$lib/websocket';
</script>

<main>
	<slot />
</main>

<style>
	:global(body) {
		font-family: Arial, sans-serif;
		max-width: 1200px;
		margin: 0 auto;
		padding: 20px;
		background-color: #f5f5f5;
	}

	:global(h1, h2, h3) {
		margin-top: 0;
	}

	:global(.container) {
		background: white;
		padding: 20px;
		border-radius: 8px;
		box-shadow: 0 2px 10px rgba(0,0,0,0.1);
		margin-bottom: 20px;
	}

	:global(input, button) {
		padding: 8px;
		border: 1px solid #ddd;
		border-radius: 4px;
	}

	:global(button) {
		background-color: #007bff;
		color: white;
		cursor: pointer;
		font-weight: bold;
	}

	:global(button:hover) { 
		background-color: #0056b3; 
	}

	:global(button:disabled) { 
		background-color: #6c757d; 
		cursor: not-allowed; 
	}

	:global(label) {
		margin-bottom: 5px;
		font-weight: bold;
		display: block;
	}

	main {
		width: 100%;
	}
</style>