using System.Text;

namespace Morphant.Generator;

internal sealed class CodeWriter
{
    private const string NewLine = "\r\n";

    private readonly StringBuilder _builder = new();
    private int _indent;

    public void Line(string value = "")
    {
        var lines = value
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n');

        foreach (var line in lines)
        {
            if (line.Length > 0)
            {
                _builder.Append(' ', _indent * 4);
                _builder.Append(line);
            }

            _builder.Append(NewLine);
        }
    }

    public void OpenBlock(string declaration)
    {
        Line(declaration);
        Line("{");
        _indent++;
    }

    public void CloseBlock()
    {
        _indent--;
        Line("}");
    }

    public void EmptyBlock()
    {
        Line("{");
        Line("}");
    }

    public void Indent() => _indent++;

    public void Unindent() => _indent--;

    public override string ToString() => _builder.ToString();
}
