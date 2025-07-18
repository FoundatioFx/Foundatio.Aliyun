﻿using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Foundatio.Extensions;

internal static class TaskExtensions
{
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ConfiguredTaskAwaitable<TResult> AnyContext<TResult>(this Task<TResult> task)
    {
        return task.ConfigureAwait(false);
    }

    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ConfiguredTaskAwaitable AnyContext(this Task task)
    {
        return task.ConfigureAwait(false);
    }
}
