using OpenAgent.Core.Tools;

namespace OpenAgent.Tools;

/// <summary>
/// Simple calculator tool for basic arithmetic operations.
/// </summary>
public class CalculatorTool : ITool
{
    public string Name => "calculator";

    public string Description => "Perform basic arithmetic operations. Supports add, subtract, multiply, and divide.";

    public async Task<object> ExecuteAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask; // Synchronous operation

        if (!arguments.TryGetValue("operation", out var opObj) || opObj == null)
        {
            throw new ArgumentException("Missing 'operation' argument");
        }

        if (!arguments.TryGetValue("a", out var aObj) || aObj == null)
        {
            throw new ArgumentException("Missing 'a' argument");
        }

        if (!arguments.TryGetValue("b", out var bObj) || bObj == null)
        {
            throw new ArgumentException("Missing 'b' argument");
        }

        var operation = opObj.ToString()!.ToLower();
        var a = Convert.ToDouble(aObj);
        var b = Convert.ToDouble(bObj);

        var result = operation switch
        {
            "add" => a + b,
            "subtract" => a - b,
            "multiply" => a * b,
            "divide" => b != 0 ? a / b : throw new DivideByZeroException("Cannot divide by zero"),
            _ => throw new ArgumentException($"Unknown operation: {operation}")
        };

        return $"{a} {operation} {b} = {result}";
    }
}
