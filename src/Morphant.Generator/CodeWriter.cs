using System.Text;

namespace Morphant.Generator;

internal sealed class CodeWriter
{
    private const string NewLine = "\r\n";

    private readonly StringBuilder _builder = new();
    private int _indent;

    public void Line(string value = "")
    {
        if (value.Length > 0)
        {
            _builder.Append(' ', _indent * 4);
            _builder.Append(value);
        }

        _builder.Append(NewLine);
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
