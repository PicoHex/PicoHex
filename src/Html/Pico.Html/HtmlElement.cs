namespace Pico.Html;

/// <summary>
/// Abstract base class for all HTML elements
/// </summary>
public abstract class HtmlElement
{
    public string? Id { get; init; }
    public string? Class { get; init; }
    public string? Style { get; init; }
    public string? Title { get; init; }
    public bool Hidden { get; init; }
    public string? Text { get; init; }

    // Support for data attributes
    public Dictionary<string, string> DataAttributes { get; init; } = new();

    protected Dictionary<string, string> Attributes { get; } = new();
    protected List<HtmlElement> Children { get; } = new();

    public abstract string TagName { get; }
    public virtual bool IsSelfClosing => false;

    public HtmlElement AddChild(HtmlElement child)
    {
        Children.Add(child);
        return this;
    }

    public HtmlElement SetAttribute(string name, string value)
    {
        Attributes[name] = value;
        return this;
    }

    public HtmlElement SetDataAttribute(string name, string value)
    {
        DataAttributes[name] = value;
        return this;
    }

    protected virtual void RenderAttributes(StringBuilder sb)
    {
        // Render standard attributes
        if (!string.IsNullOrEmpty(Id))
            sb.Append($" id=\"{Id}\"");
        if (!string.IsNullOrEmpty(Class))
            sb.Append($" class=\"{Class}\"");
        if (!string.IsNullOrEmpty(Style))
            sb.Append($" style=\"{Style}\"");
        if (!string.IsNullOrEmpty(Title))
            sb.Append($" title=\"{Title}\"");
        if (Hidden)
            sb.Append(" hidden");

        // Render data attributes
        foreach (var (key, value) in DataAttributes)
        {
            sb.Append($" data-{key}=\"{value}\"");
        }

        // Render custom attributes
        foreach (var (key, value) in Attributes)
        {
            sb.Append($" {key}=\"{value}\"");
        }
    }

    public virtual string Render()
    {
        var sb = new StringBuilder();
        sb.Append($"<{TagName}");

        RenderAttributes(sb);

        if (IsSelfClosing)
        {
            sb.Append(" />");
        }
        else
        {
            sb.Append('>');

            // Add text content
            if (!string.IsNullOrEmpty(Text))
            {
                sb.Append(Text);
            }

            // Render children
            foreach (var child in Children)
            {
                sb.Append(child.Render());
            }

            sb.Append($"</{TagName}>");
        }

        return sb.ToString();
    }
}

/// <summary>
/// HTML document root element
/// </summary>
public class HtmlDocument : HtmlElement
{
    public override string TagName => "html";
    public string Lang { get; init; } = "en";

    public Head Head { get; init; } = new();
    public Body Body { get; init; } = new();

    public HtmlDocument()
    {
        Attributes["lang"] = Lang;
    }

    public override string Render()
    {
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html>");
        sb.Append($"<{TagName}");

        foreach (var (key, value) in Attributes)
        {
            sb.Append($" {key}=\"{value}\"");
        }

        sb.Append('>');
        sb.Append(Head.Render());
        sb.Append(Body.Render());
        sb.Append($"</{TagName}>");

        return sb.ToString();
    }
}

/// <summary>
/// HTML head element
/// </summary>
public class Head : HtmlElement
{
    public override string TagName => "head";
    public string? TitleText { get; set; }
    public string Charset { get; init; } = "UTF-8";

    public List<Meta> MetaTags { get; init; } = new();
    public List<Link> Links { get; init; } = new();
    public List<Script> Scripts { get; init; } = new();
    public List<StyleElement> Styles { get; init; } = new();

    // Add external CSS support
    public Head AddExternalCss(string href, string? media = null)
    {
        var link = new Link
        {
            Rel = "stylesheet",
            Href = href,
            Media = media
        };
        Links.Add(link);
        return this;
    }

    // Add inline CSS support
    public Head AddInlineCss(string css)
    {
        Styles.Add(new StyleElement { Css = css });
        return this;
    }

    // Add external JavaScript support
    public Head AddExternalScript(string src, bool async = false, bool defer = false)
    {
        var script = new Script { Src = src };
        if (async)
            script.Async = true;
        if (defer)
            script.Defer = true;
        Scripts.Add(script);
        return this;
    }

    public override string Render()
    {
        Children.Clear();

        // Add metadata
        Children.Add(new Meta().SetAttribute("charset", Charset));
        Children.AddRange(MetaTags);

        // Add title
        if (!string.IsNullOrEmpty(TitleText))
        {
            Children.Add(new TitleElement { Text = TitleText });
        }

        // Add links
        Children.AddRange(Links);

        // Add styles
        Children.AddRange(Styles);

        // Add scripts
        Children.AddRange(Scripts);

        return base.Render();
    }
}

/// <summary>
/// HTML body element
/// </summary>
public class Body : HtmlElement
{
    public override string TagName => "body";
    public List<Script> BottomScripts { get; init; } = [];

    // Add bottom script
    public Body AddBottomScript(Script script)
    {
        BottomScripts.Add(script);
        return this;
    }

    public override string Render()
    {
        var sb = new StringBuilder();
        sb.Append($"<{TagName}");

        RenderAttributes(sb);

        sb.Append('>');

        // Add text content
        if (!string.IsNullOrEmpty(Text))
        {
            sb.Append(Text);
        }

        // Render children
        foreach (var child in Children)
        {
            sb.Append(child.Render());
        }

        // Add bottom scripts
        foreach (var script in BottomScripts)
        {
            sb.Append(script.Render());
        }

        sb.Append($"</{TagName}>");

        return sb.ToString();
    }
}

// Heading elements
public class H1 : HtmlElement
{
    public override string TagName => "h1";
}

public class H2 : HtmlElement
{
    public override string TagName => "h2";
}

public class H3 : HtmlElement
{
    public override string TagName => "h3";
}

public class H4 : HtmlElement
{
    public override string TagName => "h4";
}

public class H5 : HtmlElement
{
    public override string TagName => "h5";
}

public class H6 : HtmlElement
{
    public override string TagName => "h6";
}

/// <summary>
/// Paragraph element
/// </summary>
public class Paragraph : HtmlElement
{
    public override string TagName => "p";
}

/// <summary>
/// Anchor (link) element
/// </summary>
public class Anchor : HtmlElement
{
    public override string TagName => "a";
    public string? Href { get; init; }
    public string? Target { get; init; }
    public string? Rel { get; init; }

    protected override void RenderAttributes(StringBuilder sb)
    {
        if (!string.IsNullOrEmpty(Href))
            Attributes["href"] = Href;
        if (!string.IsNullOrEmpty(Target))
            Attributes["target"] = Target;
        if (!string.IsNullOrEmpty(Rel))
            Attributes["rel"] = Rel;

        base.RenderAttributes(sb);
    }
}

/// <summary>
/// Image element
/// </summary>
public class Image : HtmlElement
{
    public override string TagName => "img";
    public override bool IsSelfClosing => true;

    public string? Src { get; init; }
    public string? Alt { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }

    protected override void RenderAttributes(StringBuilder sb)
    {
        if (!string.IsNullOrEmpty(Src))
            Attributes["src"] = Src;
        if (!string.IsNullOrEmpty(Alt))
            Attributes["alt"] = Alt;
        if (Width.HasValue)
            Attributes["width"] = Width.Value.ToString();
        if (Height.HasValue)
            Attributes["height"] = Height.Value.ToString();

        base.RenderAttributes(sb);
    }
}

// Container elements
public class Div : HtmlElement
{
    public override string TagName => "div";
}

public class Span : HtmlElement
{
    public override string TagName => "span";
}

/// <summary>
/// List elements
/// </summary>
public class UnorderedList : HtmlElement
{
    public override string TagName => "ul";
}

public class OrderedList : HtmlElement
{
    public override string TagName => "ol";
    public string? Type { get; init; }
    public int? Start { get; init; }

    protected override void RenderAttributes(StringBuilder sb)
    {
        if (!string.IsNullOrEmpty(Type))
            Attributes["type"] = Type;
        if (Start.HasValue)
            Attributes["start"] = Start.Value.ToString();

        base.RenderAttributes(sb);
    }
}

public class ListItem : HtmlElement
{
    public override string TagName => "li";
    public string? Value { get; init; }

    protected override void RenderAttributes(StringBuilder sb)
    {
        if (!string.IsNullOrEmpty(Value))
            Attributes["value"] = Value;

        base.RenderAttributes(sb);
    }
}

/// <summary>
/// Table elements
/// </summary>
public class Table : HtmlElement
{
    public override string TagName => "table";
}

public class TableRow : HtmlElement
{
    public override string TagName => "tr";
}

public class TableCell : HtmlElement
{
    public override string TagName => "td";
    public int? ColSpan { get; init; }
    public int? RowSpan { get; init; }

    protected override void RenderAttributes(StringBuilder sb)
    {
        if (ColSpan.HasValue)
            Attributes["colspan"] = ColSpan.Value.ToString();
        if (RowSpan.HasValue)
            Attributes["rowspan"] = RowSpan.Value.ToString();

        base.RenderAttributes(sb);
    }
}

public class TableHeader : HtmlElement
{
    public override string TagName => "th";
    public int? ColSpan { get; init; }
    public int? RowSpan { get; init; }
    public string? Scope { get; init; }

    protected override void RenderAttributes(StringBuilder sb)
    {
        if (ColSpan.HasValue)
            Attributes["colspan"] = ColSpan.Value.ToString();
        if (RowSpan.HasValue)
            Attributes["rowspan"] = RowSpan.Value.ToString();
        if (!string.IsNullOrEmpty(Scope))
            Attributes["scope"] = Scope;

        base.RenderAttributes(sb);
    }
}

/// <summary>
/// Form element
/// </summary>
public class Form : HtmlElement
{
    public override string TagName => "form";
    public string? Action { get; init; }
    public string Method { get; init; } = "get";
    public string? Enctype { get; init; }

    protected override void RenderAttributes(StringBuilder sb)
    {
        if (!string.IsNullOrEmpty(Action))
            Attributes["action"] = Action;
        if (!string.IsNullOrEmpty(Method))
            Attributes["method"] = Method;
        if (!string.IsNullOrEmpty(Enctype))
            Attributes["enctype"] = Enctype;

        base.RenderAttributes(sb);
    }
}

/// <summary>
/// Input element
/// </summary>
public class Input : HtmlElement
{
    public override string TagName => "input";
    public override bool IsSelfClosing => true;

    public string Type { get; init; } = "text";
    public string? Name { get; init; }
    public string? Value { get; init; }
    public string? Placeholder { get; init; }
    public bool Required { get; init; }

    protected override void RenderAttributes(StringBuilder sb)
    {
        Attributes["type"] = Type;
        if (!string.IsNullOrEmpty(Name))
            Attributes["name"] = Name;
        if (!string.IsNullOrEmpty(Value))
            Attributes["value"] = Value;
        if (!string.IsNullOrEmpty(Placeholder))
            Attributes["placeholder"] = Placeholder;
        if (Required)
            Attributes["required"] = "required";

        base.RenderAttributes(sb);
    }
}

/// <summary>
/// Textarea element
/// </summary>
public class TextArea : HtmlElement
{
    public override string TagName => "textarea";
    public string? Name { get; init; }
    public int? Rows { get; init; }
    public int? Cols { get; init; }
    public string? Placeholder { get; init; }

    protected override void RenderAttributes(StringBuilder sb)
    {
        if (!string.IsNullOrEmpty(Name))
            Attributes["name"] = Name;
        if (Rows.HasValue)
            Attributes["rows"] = Rows.Value.ToString();
        if (Cols.HasValue)
            Attributes["cols"] = Cols.Value.ToString();
        if (!string.IsNullOrEmpty(Placeholder))
            Attributes["placeholder"] = Placeholder;

        base.RenderAttributes(sb);
    }

    public override string Render()
    {
        var sb = new StringBuilder();
        sb.Append($"<{TagName}");

        RenderAttributes(sb);

        sb.Append('>');
        if (!string.IsNullOrEmpty(Text))
            sb.Append(Text);
        sb.Append($"</{TagName}>");

        return sb.ToString();
    }
}

/// <summary>
/// Select element
/// </summary>
public class Select : HtmlElement
{
    public override string TagName => "select";
    public string? Name { get; init; }
    public List<Option> Options { get; init; } = new();

    protected override void RenderAttributes(StringBuilder sb)
    {
        if (!string.IsNullOrEmpty(Name))
            Attributes["name"] = Name;

        base.RenderAttributes(sb);
    }

    public override string Render()
    {
        var sb = new StringBuilder();
        sb.Append($"<{TagName}");

        RenderAttributes(sb);

        sb.Append('>');

        foreach (var option in Options)
        {
            sb.Append(option.Render());
        }

        sb.Append($"</{TagName}>");

        return sb.ToString();
    }
}

/// <summary>
/// Option element
/// </summary>
public class Option : HtmlElement
{
    public override string TagName => "option";
    public string? Value { get; init; }
    public bool Selected { get; init; }

    protected override void RenderAttributes(StringBuilder sb)
    {
        if (!string.IsNullOrEmpty(Value))
            Attributes["value"] = Value;
        if (Selected)
            Attributes["selected"] = "selected";

        base.RenderAttributes(sb);
    }

    public override string Render()
    {
        var sb = new StringBuilder();
        sb.Append($"<{TagName}");

        RenderAttributes(sb);

        sb.Append('>');
        if (!string.IsNullOrEmpty(Text))
            sb.Append(Text);
        sb.Append($"</{TagName}>");

        return sb.ToString();
    }
}

/// <summary>
/// Button element
/// </summary>
public class Button : HtmlElement
{
    public override string TagName => "button";
    public string Type { get; init; } = "button";
    public bool Disabled { get; init; }

    protected override void RenderAttributes(StringBuilder sb)
    {
        Attributes["type"] = Type;
        if (Disabled)
            Attributes["disabled"] = "disabled";

        base.RenderAttributes(sb);
    }

    public override string Render()
    {
        var sb = new StringBuilder();
        sb.Append($"<{TagName}");

        RenderAttributes(sb);

        sb.Append('>');
        if (!string.IsNullOrEmpty(Text))
            sb.Append(Text);
        sb.Append($"</{TagName}>");

        return sb.ToString();
    }
}

/// <summary>
/// Label element
/// </summary>
public class Label : HtmlElement
{
    public override string TagName => "label";
    public string? For { get; init; }

    protected override void RenderAttributes(StringBuilder sb)
    {
        if (!string.IsNullOrEmpty(For))
            Attributes["for"] = For;

        base.RenderAttributes(sb);
    }

    public override string Render()
    {
        var sb = new StringBuilder();
        sb.Append($"<{TagName}");

        RenderAttributes(sb);

        sb.Append('>');
        if (!string.IsNullOrEmpty(Text))
            sb.Append(Text);
        sb.Append($"</{TagName}>");

        return sb.ToString();
    }
}

/// <summary>
/// IFrame element
/// </summary>
public class IFrame : HtmlElement
{
    public override string TagName => "iframe";
    public string? Src { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
    public string FrameBorder { get; init; } = "0";

    protected override void RenderAttributes(StringBuilder sb)
    {
        if (!string.IsNullOrEmpty(Src))
            Attributes["src"] = Src;
        if (Width.HasValue)
            Attributes["width"] = Width.Value.ToString();
        if (Height.HasValue)
            Attributes["height"] = Height.Value.ToString();
        Attributes["frameborder"] = FrameBorder;

        base.RenderAttributes(sb);
    }
}

/// <summary>
/// Audio element
/// </summary>
public class Audio : HtmlElement
{
    public override string TagName => "audio";
    public string? Src { get; init; }
    public bool Controls { get; init; } = true;
    public bool AutoPlay { get; init; }
    public bool Loop { get; init; }

    protected override void RenderAttributes(StringBuilder sb)
    {
        if (!string.IsNullOrEmpty(Src))
            Attributes["src"] = Src;
        if (Controls)
            Attributes["controls"] = "controls";
        if (AutoPlay)
            Attributes["autoplay"] = "autoplay";
        if (Loop)
            Attributes["loop"] = "loop";

        base.RenderAttributes(sb);
    }
}

/// <summary>
/// Video element
/// </summary>
public class Video : HtmlElement
{
    public override string TagName => "video";
    public string? Src { get; init; }
    public bool Controls { get; init; } = true;
    public bool AutoPlay { get; init; }
    public bool Loop { get; init; }
    public string? Poster { get; init; }

    protected override void RenderAttributes(StringBuilder sb)
    {
        if (!string.IsNullOrEmpty(Src))
            Attributes["src"] = Src;
        if (Controls)
            Attributes["controls"] = "controls";
        if (AutoPlay)
            Attributes["autoplay"] = "autoplay";
        if (Loop)
            Attributes["loop"] = "loop";
        if (!string.IsNullOrEmpty(Poster))
            Attributes["poster"] = Poster;

        base.RenderAttributes(sb);
    }
}

/// <summary>
/// Meta element
/// </summary>
public class Meta : HtmlElement
{
    public override string TagName => "meta";
    public override bool IsSelfClosing => true;

    public string? Charset { get; init; }
    public string? Name { get; init; }
    public string? Content { get; init; }

    protected override void RenderAttributes(StringBuilder sb)
    {
        if (!string.IsNullOrEmpty(Charset))
            Attributes["charset"] = Charset;
        if (!string.IsNullOrEmpty(Name))
            Attributes["name"] = Name;
        if (!string.IsNullOrEmpty(Content))
            Attributes["content"] = Content;

        base.RenderAttributes(sb);
    }
}

/// <summary>
/// Link element
/// </summary>
public class Link : HtmlElement
{
    public override string TagName => "link";
    public override bool IsSelfClosing => true;

    public string? Rel { get; init; }
    public string? Href { get; init; }
    public string? Type { get; init; }
    public string? Media { get; init; }

    protected override void RenderAttributes(StringBuilder sb)
    {
        if (!string.IsNullOrEmpty(Rel))
            Attributes["rel"] = Rel;
        if (!string.IsNullOrEmpty(Href))
            Attributes["href"] = Href;
        if (!string.IsNullOrEmpty(Type))
            Attributes["type"] = Type;
        if (!string.IsNullOrEmpty(Media))
            Attributes["media"] = Media;

        base.RenderAttributes(sb);
    }
}

/// <summary>
/// Script element
/// </summary>
public class Script : HtmlElement
{
    public override string TagName => "script";
    public string? Src { get; init; }
    public string Type { get; set; } = "text/javascript";
    public bool Async { get; set; }
    public bool Defer { get; set; }
    public string? Code { get; set; }
    public bool IsModule { get; set; }

    public Script SetCode(string code)
    {
        Code = code;
        return this;
    }

    public Script SetAsModule(bool isModule = true)
    {
        IsModule = isModule;
        Type = isModule ? "module" : "text/javascript";
        return this;
    }

    protected override void RenderAttributes(StringBuilder sb)
    {
        if (!string.IsNullOrEmpty(Src))
            Attributes["src"] = Src;
        Attributes["type"] = Type;
        if (Async)
            Attributes["async"] = "async";
        if (Defer)
            Attributes["defer"] = "defer";

        base.RenderAttributes(sb);
    }

    public override string Render()
    {
        var sb = new StringBuilder();
        sb.Append($"<{TagName}");

        RenderAttributes(sb);

        if (string.IsNullOrEmpty(Src) && !string.IsNullOrEmpty(Code))
        {
            sb.Append('>');
            sb.Append(Code);
            sb.Append($"</{TagName}>");
        }
        else
        {
            sb.Append("></script>");
        }

        return sb.ToString();
    }
}

/// <summary>
/// Style element
/// </summary>
public class StyleElement : HtmlElement
{
    public override string TagName => "style";
    public string Type { get; init; } = "text/css";
    public string? Media { get; init; }
    public string? Css { get; init; }

    protected override void RenderAttributes(StringBuilder sb)
    {
        Attributes["type"] = Type;
        if (!string.IsNullOrEmpty(Media))
            Attributes["media"] = Media;

        base.RenderAttributes(sb);
    }

    public override string Render()
    {
        var sb = new StringBuilder();
        sb.Append($"<{TagName}");

        RenderAttributes(sb);

        sb.Append('>');
        if (!string.IsNullOrEmpty(Css))
            sb.Append(Css);
        sb.Append($"</{TagName}>");

        return sb.ToString();
    }
}

/// <summary>
/// Title element
/// </summary>
public class TitleElement : HtmlElement
{
    public override string TagName => "title";

    public override string Render()
    {
        return $"<{TagName}>{Text}</{TagName}>";
    }
}

// Semantic HTML5 elements
public class Nav : HtmlElement
{
    public override string TagName => "nav";
}

public class Header : HtmlElement
{
    public override string TagName => "header";
}

public class Footer : HtmlElement
{
    public override string TagName => "footer";
}

public class Main : HtmlElement
{
    public override string TagName => "main";
}

public class Article : HtmlElement
{
    public override string TagName => "article";
}

public class Section : HtmlElement
{
    public override string TagName => "section";
}

public class Aside : HtmlElement
{
    public override string TagName => "aside";
}

// Self-closing elements
public class Break : HtmlElement
{
    public override string TagName => "br";
    public override bool IsSelfClosing => true;
}

public class HorizontalRule : HtmlElement
{
    public override string TagName => "hr";
    public override bool IsSelfClosing => true;
}

// Text formatting elements
public class Strong : HtmlElement
{
    public override string TagName => "strong";
}

public class Emphasis : HtmlElement
{
    public override string TagName => "em";
}

public class Code : HtmlElement
{
    public override string TagName => "code";
}

public class Preformatted : HtmlElement
{
    public override string TagName => "pre";
}

/// <summary>
/// Blockquote element
/// </summary>
public class Blockquote : HtmlElement
{
    public override string TagName => "blockquote";
    public string? Cite { get; init; }

    protected override void RenderAttributes(StringBuilder sb)
    {
        if (!string.IsNullOrEmpty(Cite))
            Attributes["cite"] = Cite;

        base.RenderAttributes(sb);
    }

    public override string Render()
    {
        var sb = new StringBuilder();
        sb.Append($"<{TagName}");

        RenderAttributes(sb);

        sb.Append('>');
        if (!string.IsNullOrEmpty(Text))
            sb.Append(Text);
        sb.Append($"</{TagName}>");

        return sb.ToString();
    }
}

/// <summary>
/// CSS rule representation
/// </summary>
public class CssRule
{
    public string Selector { get; init; }
    public Dictionary<string, string> Properties { get; init; } = new();
    public List<CssRule> NestedRules { get; init; } = new();

    public CssRule(string selector)
    {
        Selector = selector;
    }

    public CssRule AddProperty(string property, string value)
    {
        Properties[property] = value;
        return this;
    }

    public CssRule AddNestedRule(string selector, Action<CssRule>? configure = null)
    {
        var rule = new CssRule(selector);
        configure?.Invoke(rule);
        NestedRules.Add(rule);
        return this;
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append($"{Selector} {{ ");

        foreach (var (key, value) in Properties)
        {
            sb.Append($"{key}: {value}; ");
        }

        sb.Append('}');

        // Add nested rules
        foreach (var nestedRule in NestedRules)
        {
            sb.AppendLine();
            sb.Append(nestedRule.ToString());
        }

        return sb.ToString();
    }
}

/// <summary>
/// CSS stylesheet representation
/// </summary>
public class CssStyleSheet
{
    public List<CssRule> Rules { get; init; } = new();

    public CssStyleSheet AddRule(CssRule rule)
    {
        Rules.Add(rule);
        return this;
    }

    public CssStyleSheet AddRule(string selector, Action<CssRule> configure)
    {
        var rule = new CssRule(selector);
        configure(rule);
        Rules.Add(rule);
        return this;
    }

    // Add media query support
    public CssStyleSheet AddMediaQuery(string query, Action<CssStyleSheet> configure)
    {
        var mediaSheet = new CssStyleSheet();
        configure(mediaSheet);

        var rule = new CssRule($"@media {query}");
        foreach (var mediaRule in mediaSheet.Rules)
        {
            rule.AddNestedRule(
                mediaRule.Selector,
                r =>
                {
                    foreach (var (key, value) in mediaRule.Properties)
                    {
                        r.AddProperty(key, value);
                    }
                }
            );
        }

        Rules.Add(rule);
        return this;
    }

    public override string ToString()
    {
        var sb = new StringBuilder();

        foreach (var rule in Rules)
        {
            sb.AppendLine(rule.ToString());
        }

        return sb.ToString();
    }
}

/// <summary>
/// JavaScript code representation
/// </summary>
public record JavaScriptCode
{
    public string? Code { get; init; }
    public List<string> Functions { get; init; } = new();
    public Dictionary<string, string> Variables { get; init; } = new();
    public List<string> EventListeners { get; init; } = new();
    public List<string> Imports { get; init; } = new();

    public JavaScriptCode AddFunction(
        string functionName,
        string parameters,
        string body,
        bool isAsync = false
    )
    {
        var asyncKeyword = isAsync ? "async " : "";
        Functions.Add($"{asyncKeyword}function {functionName}({parameters}) {{ {body} }}");
        return this;
    }

    public JavaScriptCode AddArrowFunction(
        string functionName,
        string parameters,
        string body,
        bool isConst = true
    )
    {
        var constKeyword = isConst ? "const " : "";
        Functions.Add($"{constKeyword}{functionName} = ({parameters}) => {{ {body} }};");
        return this;
    }

    public JavaScriptCode AddVariable(
        string name,
        string value,
        bool isConst = false,
        string? type = null
    )
    {
        var declaration = isConst ? "const" : "let";
        var typeAnnotation = !string.IsNullOrEmpty(type) ? $": {type}" : "";
        Variables[name] = $"{declaration} {name}{typeAnnotation} = {value};";
        return this;
    }

    public JavaScriptCode AddEventListener(
        string elementId,
        string eventName,
        string handlerCode,
        bool useCapture = false
    )
    {
        var capture = useCapture ? ", true" : "";
        EventListeners.Add(
            $"document.getElementById('{elementId}').addEventListener('{eventName}', function(e) {{ {handlerCode} }}{capture});"
        );
        return this;
    }

    public JavaScriptCode AddImport(string module, string? imports = null)
    {
        var importStatement = string.IsNullOrEmpty(imports)
            ? $"import '{module}';"
            : $"import {imports} from '{module}';";
        Imports.Add(importStatement);
        return this;
    }

    public JavaScriptCode AddRawCode(string code)
    {
        return this with { Code = (Code ?? "") + code + Environment.NewLine };
    }

    public override string ToString()
    {
        var sb = new StringBuilder();

        // Add import statements
        foreach (var import in Imports)
        {
            sb.AppendLine(import);
        }

        // Add variable declarations
        foreach (var variable in Variables.Values)
        {
            sb.AppendLine(variable);
        }

        // Add function definitions
        foreach (var function in Functions)
        {
            sb.AppendLine(function);
        }

        // Add event listeners
        foreach (var listener in EventListeners)
        {
            sb.AppendLine(listener);
        }

        // Add raw code
        if (!string.IsNullOrEmpty(Code))
        {
            sb.AppendLine(Code);
        }

        return sb.ToString();
    }
}
