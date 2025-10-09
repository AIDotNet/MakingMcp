using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace MakingMcp.Tools;

public class CompleteTool(Func<string, Task> completeTaskFunc)
{
    [KernelFunction("complete_task"),
     Description(
         "Use this tool when you have completed the task to provide your final answer")]
    public async Task<string> CompleteTask(
        [Description(
            "The final result of the task")]
        string result)
    {
        await completeTaskFunc.Invoke(result);

        return "Task completed.";
    }
}