﻿// Copyright (c) MudBlazor 2021
// MudBlazor licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace MudBlazor.Utilities.Background;

#nullable enable
/// <summary>
/// Base class for implementing a long-running background worker.
/// </summary>
/// <remarks>
/// This class provides a base implementation for executing asynchronous operations continuously or periodically
/// in a background worker. It simplifies the implementation of scenarios where you need to execute asynchronous
/// operations in the background. It also provides built-in support for graceful shutdown.
/// </remarks>
internal abstract class BackgroundWorkerBase : IAsyncDisposable
{
    private Task? _executeTask;
    private CancellationTokenSource? _stoppingCts;

    /// <summary>
    /// Gets the Task that executes the background operation.
    /// </summary>
    /// <remarks>
    /// Will return <see langword="null"/> if the background operation hasn't started.
    /// </remarks>
    public virtual Task? ExecuteTask => _executeTask;

    /// <summary>
    /// Starts the processing asynchronously. The implementation should return a task that represents
    /// the lifetime of the long running operation(s) being performed.
    /// </summary>
    /// <param name="stoppingToken">Indicates that the shutdown process should no longer be graceful.</param>
    /// <returns>A <see cref="Task"/> that represents the long running operations.</returns>
    protected abstract Task ExecuteAsync(CancellationToken stoppingToken);

    /// <summary>
    /// Triggered when the application host is ready to start the service.
    /// </summary>
    /// <param name="cancellationToken">Indicates that the start process has been aborted.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous Start operation.</returns>
    public virtual Task StartAsync(CancellationToken cancellationToken = default)
    {
        // Create linked token to allow cancelling executing task from provided token
        _stoppingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Store the task we're executing
        _executeTask = ExecuteAsync(_stoppingCts.Token);

        // If the task is completed then return it, this will bubble cancellation and failure to the caller
        if (_executeTask.IsCompleted)
        {
            return _executeTask;
        }

        // Otherwise it's running
        return Task.CompletedTask;
    }

    /// <summary>
    /// Triggered when the application host is performing a graceful shutdown.
    /// </summary>
    /// <param name="cancellationToken">Indicates that the shutdown process should no longer be graceful.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous Stop operation.</returns>
    public virtual async Task StopAsync(CancellationToken cancellationToken)
    {
        // Stop called without start
        if (_executeTask == null)
        {
            return;
        }

        try
        {
            // Signal cancellation to the executing method
            _stoppingCts!.Cancel();
        }
        finally
        {
            // Wait until the task completes or the stop token triggers
            var tcs = new TaskCompletionSource<object>();
            await using var registration = cancellationToken.Register(s => ((TaskCompletionSource<object>)s!).SetCanceled(CancellationToken.None), tcs);
            // Do not await the _executeTask because cancelling it will throw an OperationCanceledException which we are explicitly ignoring
            await Task.WhenAny(_executeTask, tcs.Task).ConfigureAwait(false);
        }

    }

    /// <inheritdoc/>
    public virtual ValueTask DisposeAsync()
    {
        _stoppingCts?.Cancel();
        
        return ValueTask.CompletedTask;
    }
}
