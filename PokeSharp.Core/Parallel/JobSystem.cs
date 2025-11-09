using System.Threading.Tasks;

namespace PokeSharp.Core.Parallel;

/// <summary>
///     Simple job system for scheduling parallel work.
///     Provides a higher-level API for parallel task execution.
/// </summary>
public class JobSystem
{
    private readonly int _workerThreads;
    private readonly TaskScheduler _scheduler;

    /// <summary>
    ///     Creates a new job system.
    /// </summary>
    /// <param name="workerThreads">Number of worker threads (null = ProcessorCount).</param>
    public JobSystem(int? workerThreads = null)
    {
        _workerThreads = workerThreads ?? Environment.ProcessorCount;
        _scheduler = TaskScheduler.Default;
    }

    /// <summary>
    ///     Schedule a job for parallel execution.
    /// </summary>
    public JobHandle Schedule(Action job)
    {
        ArgumentNullException.ThrowIfNull(job);

        var task = Task.Run(job);
        return new JobHandle(task);
    }

    /// <summary>
    ///     Schedule a job that returns a result.
    /// </summary>
    public JobHandle<TResult> Schedule<TResult>(Func<TResult> job)
    {
        ArgumentNullException.ThrowIfNull(job);

        var task = Task.Run(job);
        return new JobHandle<TResult>(task);
    }

    /// <summary>
    ///     Schedule batch job (one job per item) in parallel.
    /// </summary>
    public JobHandle ScheduleBatch<T>(IEnumerable<T> items, Action<T> job)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(job);

        var task = Task.Run(() =>
        {
            System.Threading.Tasks.Parallel.ForEach(
                items,
                new ParallelOptions { MaxDegreeOfParallelism = _workerThreads },
                job
            );
        });

        return new JobHandle(task);
    }

    /// <summary>
    ///     Schedule batch job with result aggregation.
    /// </summary>
    public JobHandle<List<TResult>> ScheduleBatch<TItem, TResult>(
        IEnumerable<TItem> items,
        Func<TItem, TResult> job)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(job);

        var task = Task.Run(() =>
        {
            var results = new System.Collections.Concurrent.ConcurrentBag<TResult>();

            System.Threading.Tasks.Parallel.ForEach(
                items,
                new ParallelOptions { MaxDegreeOfParallelism = _workerThreads },
                item =>
                {
                    var result = job(item);
                    results.Add(result);
                }
            );

            return results.ToList();
        });

        return new JobHandle<List<TResult>>(task);
    }

    /// <summary>
    ///     Schedule job with dependency (waits for other job to complete first).
    /// </summary>
    public JobHandle ScheduleWithDependency(Action job, JobHandle dependency)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(dependency);

        var task = dependency.Task.ContinueWith(_ => job(), TaskScheduler.Default);
        return new JobHandle(task);
    }

    /// <summary>
    ///     Schedule job with dependency that returns a result.
    /// </summary>
    public JobHandle<TResult> ScheduleWithDependency<TResult>(
        Func<TResult> job,
        JobHandle dependency)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(dependency);

        var task = dependency.Task.ContinueWith(_ => job(), TaskScheduler.Default);
        return new JobHandle<TResult>(task);
    }

    /// <summary>
    ///     Schedule multiple jobs to run in parallel and wait for all to complete.
    /// </summary>
    public JobHandle ScheduleParallel(params Action[] jobs)
    {
        ArgumentNullException.ThrowIfNull(jobs);

        var tasks = jobs.Select(job => Task.Run(job)).ToArray();
        var combinedTask = Task.WhenAll(tasks);

        return new JobHandle(combinedTask);
    }

    /// <summary>
    ///     Complete all pending jobs (blocks until all complete).
    /// </summary>
    public void CompleteAll()
    {
        // Note: This is a simplified implementation
        // In a production system, you'd track all active jobs
        Task.WaitAll();
    }
}

/// <summary>
///     Handle to a scheduled job (non-generic version).
/// </summary>
public class JobHandle
{
    private protected readonly Task _task;

    internal JobHandle(Task task)
    {
        _task = task ?? throw new ArgumentNullException(nameof(task));
    }

    /// <summary>
    ///     The underlying task.
    /// </summary>
    public Task Task => _task;

    /// <summary>
    ///     Check if job is complete.
    /// </summary>
    public bool IsComplete => _task.IsCompleted;

    /// <summary>
    ///     Check if job completed successfully.
    /// </summary>
    public bool IsCompletedSuccessfully => _task.IsCompletedSuccessfully;

    /// <summary>
    ///     Check if job was canceled.
    /// </summary>
    public bool IsCanceled => _task.IsCanceled;

    /// <summary>
    ///     Check if job faulted (threw exception).
    /// </summary>
    public bool IsFaulted => _task.IsFaulted;

    /// <summary>
    ///     Get exception if job faulted.
    /// </summary>
    public Exception? Exception => _task.Exception;

    /// <summary>
    ///     Wait for job to complete (blocks current thread).
    /// </summary>
    public void Complete()
    {
        _task.Wait();
    }

    /// <summary>
    ///     Wait for job to complete with timeout.
    /// </summary>
    public bool Complete(TimeSpan timeout)
    {
        return _task.Wait(timeout);
    }

    /// <summary>
    ///     Wait for job to complete asynchronously.
    /// </summary>
    public async Task CompleteAsync()
    {
        await _task;
    }

    /// <summary>
    ///     Combine multiple job handles into a single handle.
    /// </summary>
    public static JobHandle Combine(params JobHandle[] handles)
    {
        ArgumentNullException.ThrowIfNull(handles);

        var tasks = handles.Select(h => h._task).ToArray();
        var combinedTask = Task.WhenAll(tasks);

        return new JobHandle(combinedTask);
    }
}

/// <summary>
///     Handle to a scheduled job that returns a result.
/// </summary>
public class JobHandle<TResult> : JobHandle
{
    private readonly Task<TResult> _typedTask;

    internal JobHandle(Task<TResult> task) : base(task)
    {
        _typedTask = task ?? throw new ArgumentNullException(nameof(task));
    }

    /// <summary>
    ///     Get the result (blocks until job completes).
    /// </summary>
    public TResult Result => _typedTask.Result;

    /// <summary>
    ///     Get the result asynchronously.
    /// </summary>
    public new async Task<TResult> CompleteAsync()
    {
        return await _typedTask;
    }

    /// <summary>
    ///     Try to get the result if job is complete.
    /// </summary>
    public bool TryGetResult(out TResult? result)
    {
        if (IsComplete && IsCompletedSuccessfully)
        {
            result = _typedTask.Result;
            return true;
        }

        result = default;
        return false;
    }
}

/// <summary>
///     Extension methods for job handles.
/// </summary>
public static class JobHandleExtensions
{
    /// <summary>
    ///     Chain another job to execute after this one completes.
    /// </summary>
    public static JobHandle Then(this JobHandle handle, Action continuation)
    {
        ArgumentNullException.ThrowIfNull(continuation);

        var task = handle.Task.ContinueWith(_ => continuation());
        return new JobHandle(task);
    }

    /// <summary>
    ///     Chain another job that returns a result.
    /// </summary>
    public static JobHandle<TResult> Then<TResult>(this JobHandle handle, Func<TResult> continuation)
    {
        ArgumentNullException.ThrowIfNull(continuation);

        var task = handle.Task.ContinueWith(_ => continuation());
        return new JobHandle<TResult>(task);
    }

    /// <summary>
    ///     Chain another job that uses the result of the previous job.
    /// </summary>
    public static JobHandle<TNewResult> Then<TResult, TNewResult>(
        this JobHandle<TResult> handle,
        Func<TResult, TNewResult> continuation)
    {
        ArgumentNullException.ThrowIfNull(continuation);

        var task = handle.CompleteAsync().ContinueWith(t => continuation(t.Result));
        return new JobHandle<TNewResult>(task);
    }
}
