using System.ComponentModel;
using System.Text;
using System.Text.Json;
using MakingMcp.Tools;
using Microsoft.SemanticKernel;
using ModelContextProtocol.Server;

namespace MakingMcp.Shared.Tools;

public class ReadTool
{
    private const int DefaultMaxReadLines = 2000;

    [McpServerTool(Name = "Read"),
     
     Description(
         """
         Reads a file from the local filesystem. You can access any file directly by using this tool.
         Assume this tool is able to read all files on the machine. If the User provides a path to a file assume that path is valid. It is okay to read a file that does not exist; an error will be returned.

         Usage:
         - The file_path parameter must be an absolute path, not a relative path
         - By default, it reads up to 2000 lines starting from the beginning of the file
         - You can optionally specify a line offset and limit (especially handy for long files), but it's recommended to read the whole file by not providing these parameters
         - Any lines longer than 2000 characters will be truncated
         - Results are returned using cat -n format, with line numbers starting at 1
         - This tool allows Claude Code to read images (eg PNG, JPG, etc). When reading an image file the contents are presented visually as Claude Code is a multimodal LLM.
         - This tool can read PDF files (.pdf). PDFs are processed page by page, extracting both text and visual content for analysis.
         - This tool can read Jupyter notebooks (.ipynb files) and returns all cells with their outputs, combining code, text, and visualizations.
         - This tool can only read files, not directories. To read a directory, use an ls command via the Bash tool.
         - You have the capability to call multiple tools in a single response. It is always better to speculatively read multiple files as a batch that are potentially useful. 
         - You will regularly be asked to read screenshots. If the user provides a path to a screenshot ALWAYS use this tool to view the file at the path. This tool will work with all temporary file paths like /var/folders/123/abc/T/TemporaryItems/NSIRD_screencaptureui_ZfB1tD/Screenshot.png
         - If you read a file that exists but has empty contents you will receive a system reminder warning in place of file contents.
         """)]
    public static async Task<string> Read(
        string file_path,
        [Description("The number of lines to read. Only provide if the file is too large to read at once.")]
        int limit = DefaultMaxReadLines,
        [Description("The line number to start reading from. Only provide if the file is too large to read at once")]
        int offset = 0
    )
    {
        if (!EditTool.TryNormalizeAbsolutePath(file_path, out var normalizedPath, out var error))
        {
            return await Task.FromResult(EditTool.Error(error));
        }

        if (!File.Exists(normalizedPath))
        {
            return await Task.FromResult(EditTool.Error($"File not found: {normalizedPath}"));
        }

        if (limit <= 0)
        {
            return await Task.FromResult(EditTool.Error("limit must be greater than 0."));
        }

        if (offset < 0)
        {
            return await Task.FromResult(EditTool.Error("offset cannot be negative."));
        }

        try
        {
            var lines = (await File.ReadAllLinesAsync(normalizedPath));
            var totalLines = lines.Length;

            if (offset >= totalLines)
            {
                return "ERROR: offset exceeds total number of lines in the file.";
            }

            var slice = lines
                .Skip(offset)
                .Take(limit)
                .ToList();

            var sb = new StringBuilder();
            for (var index = 0; index < slice.Count; index++)
            {
                var lineNumber = offset + index + 1;
                sb.Append(lineNumber.ToString().PadLeft(6));
                sb.Append(' ');
                sb.AppendLine(slice[index]);
            }

            EditTool.MarkRead(normalizedPath);

            return JsonSerializer.Serialize(new
            {
                file_path = normalizedPath,
                content = sb.ToString(),
                total_lines = totalLines,
                lines_returned = slice.Count,
                offset,
                limit
            }, JsonSerializerOptions.Web);
        }
        catch (Exception ex)
        {
            return EditTool.Error($"Failed to read file: {ex.Message}");
        }
    }
}