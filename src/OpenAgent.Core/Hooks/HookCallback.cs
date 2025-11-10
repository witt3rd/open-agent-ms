namespace OpenAgent.Core.Hooks;

/// <summary>
/// Callback function for hooks.
/// </summary>
/// <param name="data">Data associated with the hook event.</param>
/// <param name="context">Execution context.</param>
/// <returns>Result indicating whether to allow the operation.</returns>
public delegate Task<HookResult> HookCallback(object? data, HookContext context);
