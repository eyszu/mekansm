using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Ganss.Xss;
using Markdig;

namespace Mekansm.Services;

/// <summary>Markdown to safe HTML. Raw HTML is disabled in the parser and the output is sanitized again.</summary>
public sealed partial class MarkdownService
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()   // tables, task lists, footnotes, autolinks, etc.
        .DisableHtml()
        .Build();

    public string ToHtml(string? markdown)
    {
        var html = Markdown.ToHtml(markdown ?? "", Pipeline);

        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Add("input");              // task list checkboxes
        sanitizer.AllowedAttributes.Add("type");
        sanitizer.AllowedAttributes.Add("checked");
        sanitizer.AllowedAttributes.Add("disabled");
        sanitizer.PostProcessNode += (_, e) =>
        {
            if (e.Node is IElement { LocalName: "a" } a)
                a.SetAttribute("rel", "nofollow ugc noopener");
        };
        return sanitizer.Sanitize(html);
    }

    public string ToExcerpt(string? markdown, int max = 180)
    {
        var text = Markdown.ToPlainText(markdown ?? "", Pipeline);
        text = Whitespace().Replace(text, " ").Trim();
        return text.Length <= max ? text : text[..max].TrimEnd() + "…";
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
