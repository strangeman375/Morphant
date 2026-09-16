using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Morphant.Generator;

internal sealed class CodeWriter
{
    private const string NewLine = "\r\n";

    private readonly StringBuilder _builder = new();
    private int _indent;

    public void Line(string value = "")
    {
        if (value.IndexOf('$') >= 0 &&
            (value.IndexOf('\r') >= 0 || value.IndexOf('\n') >= 0) &&
            WriteInterpolatedLine(value)) return;

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

    private bool WriteInterpolatedLine(string value)
    {
        var strings = CSharpSyntaxTree.ParseText(value).GetRoot().DescendantNodes()
            .OfType<InterpolatedStringExpressionSyntax>()
            .Where(node => !node.Ancestors().OfType<InterpolatedStringExpressionSyntax>().Any())
            .Select(node => node.Span).ToArray();
        if (strings.Length == 0) return false;

        // Indentation and newline normalization inside an interpolation can
        // change literal text (and raw-string indentation). Keep that text as
        // written, including expressions that require verbatim strings in C# 9.
        var text = SourceText.From(value);
        foreach (var line in text.Lines)
        {
            if (line.Span.Length > 0)
            {
                if (!InsideString(line.Start)) _builder.Append(' ', _indent * 4);
                _builder.Append(line.ToString());
            }
            _builder.Append(InsideString(line.End)
                ? text.ToString(TextSpan.FromBounds(line.End, line.EndIncludingLineBreak))
                : NewLine);
        }
        return true;

        bool InsideString(int position) => strings.Any(span => span.Start < position && position < span.End);
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
