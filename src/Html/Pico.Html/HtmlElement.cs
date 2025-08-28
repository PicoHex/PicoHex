namespace Pico.Html;

public abstract class HtmlElement
{
    public string Id { get; set; }
    public string Class { get; set; }
    public string Style { get; set; }
    public string Title { get; set; }
    public bool Hidden { get; set; }
    public string Text { get; set; }

    protected Dictionary<string, string> Attributes { get; set; } = new();
    protected List<HtmlElement> Children { get; set; } = [];

    public abstract string TagName { get; }
    public virtual bool IsSelfClosing { get; } = false;

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

    public virtual string Render()
    {
        var sb = new StringBuilder();
        sb.Append($"<{TagName}");

        // 添加标准属性
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

        // 添加自定义属性
        foreach (var attr in Attributes)
        {
            sb.Append($" {attr.Key}=\"{attr.Value}\"");
        }

        if (IsSelfClosing)
        {
            sb.Append(" />");
        }
        else
        {
            sb.Append(">");

            // 添加文本内容
            if (!string.IsNullOrEmpty(Text))
            {
                sb.Append(Text);
            }

            // 渲染子元素
            foreach (var child in Children)
            {
                sb.Append(child.Render());
            }

            sb.Append($"</{TagName}>");
        }

        return sb.ToString();
    }
}

public class HtmlDocument : HtmlElement
{
    public override string TagName => "html";
    public string Lang { get; set; } = "en";

    public Head Head { get; set; } = new();
    public Body Body { get; set; } = new();

    public HtmlDocument()
    {
        Attributes["lang"] = Lang;
    }

    public override string Render()
    {
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html>");
        sb.Append($"<{TagName}");

        foreach (var attr in Attributes)
        {
            sb.Append($" {attr.Key}=\"{attr.Value}\"");
        }

        sb.Append(">");
        sb.Append(Head.Render());
        sb.Append(Body.Render());
        sb.Append($"</{TagName}>");

        return sb.ToString();
    }
}

public class Head : HtmlElement
{
    public override string TagName => "head";
    public string TitleText { get; set; } = string.Empty;
    public string Charset { get; set; } = "UTF-8";

    public List<Meta> MetaTags { get; set; } = [];
    public List<Link> Links { get; set; } = [];
    public List<Script> Scripts { get; set; } = [];
    public StyleElement? Style { get; set; }

    public override string Render()
    {
        Children.Clear();

        Children.Add(new Meta().SetAttribute("charset", Charset));
        Children.AddRange(MetaTags);

        if (!string.IsNullOrEmpty(TitleText))
        {
            Children.Add(new TitleElement { Text = TitleText });
        }

        Children.AddRange(Links);

        if (Style != null)
        {
            Children.Add(Style);
        }

        Children.AddRange(Scripts);

        return base.Render();
    }
}

public class Body : HtmlElement
{
    public override string TagName => "body";
}

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

public class Paragraph : HtmlElement
{
    public override string TagName => "p";
}

public class Anchor : HtmlElement
{
    public override string TagName => "a";
    public string Href { get; set; }
    public string Target { get; set; }
    public string Rel { get; set; }

    public override string Render()
    {
        if (!string.IsNullOrEmpty(Href))
            Attributes["href"] = Href;
        if (!string.IsNullOrEmpty(Target))
            Attributes["target"] = Target;
        if (!string.IsNullOrEmpty(Rel))
            Attributes["rel"] = Rel;

        return base.Render();
    }
}

public class Image : HtmlElement
{
    public override string TagName => "img";
    public override bool IsSelfClosing => true;

    public string Src { get; set; }
    public string Alt { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }

    public override string Render()
    {
        if (!string.IsNullOrEmpty(Src))
            Attributes["src"] = Src;
        if (!string.IsNullOrEmpty(Alt))
            Attributes["alt"] = Alt;
        if (Width.HasValue)
            Attributes["width"] = Width.Value.ToString();
        if (Height.HasValue)
            Attributes["height"] = Height.Value.ToString();

        return base.Render();
    }
}

public class Div : HtmlElement
{
    public override string TagName => "div";
}

public class Span : HtmlElement
{
    public override string TagName => "span";
}

public class UnorderedList : HtmlElement
{
    public override string TagName => "ul";
}

public class OrderedList : HtmlElement
{
    public override string TagName => "ol";
    public string Type { get; set; }
    public int? Start { get; set; }

    public override string Render()
    {
        if (!string.IsNullOrEmpty(Type))
            Attributes["type"] = Type;
        if (Start.HasValue)
            Attributes["start"] = Start.Value.ToString();

        return base.Render();
    }
}

public class ListItem : HtmlElement
{
    public override string TagName => "li";
    public string Value { get; set; }

    public override string Render()
    {
        if (!string.IsNullOrEmpty(Value))
            Attributes["value"] = Value;
        return base.Render();
    }
}

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
    public int? ColSpan { get; set; }
    public int? RowSpan { get; set; }

    public override string Render()
    {
        if (ColSpan.HasValue)
            Attributes["colspan"] = ColSpan.Value.ToString();
        if (RowSpan.HasValue)
            Attributes["rowspan"] = RowSpan.Value.ToString();

        return base.Render();
    }
}

public class TableHeader : HtmlElement
{
    public override string TagName => "th";
    public int? ColSpan { get; set; }
    public int? RowSpan { get; set; }
    public string Scope { get; set; }

    public override string Render()
    {
        if (ColSpan.HasValue)
            Attributes["colspan"] = ColSpan.Value.ToString();
        if (RowSpan.HasValue)
            Attributes["rowspan"] = RowSpan.Value.ToString();
        if (!string.IsNullOrEmpty(Scope))
            Attributes["scope"] = Scope;

        return base.Render();
    }
}

public class Form : HtmlElement
{
    public override string TagName => "form";
    public string Action { get; set; }
    public string Method { get; set; } = "get";
    public string Enctype { get; set; }

    public override string Render()
    {
        if (!string.IsNullOrEmpty(Action))
            Attributes["action"] = Action;
        if (!string.IsNullOrEmpty(Method))
            Attributes["method"] = Method;
        if (!string.IsNullOrEmpty(Enctype))
            Attributes["enctype"] = Enctype;

        return base.Render();
    }
}

public class Input : HtmlElement
{
    public override string TagName => "input";
    public override bool IsSelfClosing => true;

    public string Type { get; set; } = "text";
    public string Name { get; set; }
    public string Value { get; set; }
    public string Placeholder { get; set; }
    public bool Required { get; set; }

    public override string Render()
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

        return base.Render();
    }
}

public class TextArea : HtmlElement
{
    public override string TagName => "textarea";
    public string Name { get; set; }
    public int? Rows { get; set; }
    public int? Cols { get; set; }
    public string Placeholder { get; set; }

    public override string Render()
    {
        if (!string.IsNullOrEmpty(Name))
            Attributes["name"] = Name;
        if (Rows.HasValue)
            Attributes["rows"] = Rows.Value.ToString();
        if (Cols.HasValue)
            Attributes["cols"] = Cols.Value.ToString();
        if (!string.IsNullOrEmpty(Placeholder))
            Attributes["placeholder"] = Placeholder;

        var sb = new StringBuilder();
        sb.Append($"<{TagName}");

        foreach (var attr in Attributes)
        {
            sb.Append($" {attr.Key}=\"{attr.Value}\"");
        }

        sb.Append(">");
        if (!string.IsNullOrEmpty(Text))
            sb.Append(Text);
        sb.Append($"</{TagName}>");

        return sb.ToString();
    }
}

public class Select : HtmlElement
{
    public override string TagName => "select";
    public string Name { get; set; }

    public List<Option> Options { get; set; } = new List<Option>();

    public override string Render()
    {
        if (!string.IsNullOrEmpty(Name))
            Attributes["name"] = Name;

        var sb = new StringBuilder();
        sb.Append($"<{TagName}");

        foreach (var attr in Attributes)
        {
            sb.Append($" {attr.Key}=\"{attr.Value}\"");
        }

        sb.Append(">");

        foreach (var option in Options)
        {
            sb.Append(option.Render());
        }

        sb.Append($"</{TagName}>");

        return sb.ToString();
    }
}

public class Option : HtmlElement
{
    public override string TagName => "option";
    public string Value { get; set; }
    public bool Selected { get; set; }

    public override string Render()
    {
        if (!string.IsNullOrEmpty(Value))
            Attributes["value"] = Value;
        if (Selected)
            Attributes["selected"] = "selected";

        var sb = new StringBuilder();
        sb.Append($"<{TagName}");

        foreach (var attr in Attributes)
        {
            sb.Append($" {attr.Key}=\"{attr.Value}\"");
        }

        sb.Append(">");
        if (!string.IsNullOrEmpty(Text))
            sb.Append(Text);
        sb.Append($"</{TagName}>");

        return sb.ToString();
    }
}

public class Button : HtmlElement
{
    public override string TagName => "button";
    public string Type { get; set; } = "button";
    public bool Disabled { get; set; }

    public override string Render()
    {
        Attributes["type"] = Type;
        if (Disabled)
            Attributes["disabled"] = "disabled";

        var sb = new StringBuilder();
        sb.Append($"<{TagName}");

        foreach (var attr in Attributes)
        {
            sb.Append($" {attr.Key}=\"{attr.Value}\"");
        }

        sb.Append(">");
        if (!string.IsNullOrEmpty(Text))
            sb.Append(Text);
        sb.Append($"</{TagName}>");

        return sb.ToString();
    }
}

public class Label : HtmlElement
{
    public override string TagName => "label";
    public string For { get; set; }

    public override string Render()
    {
        if (!string.IsNullOrEmpty(For))
            Attributes["for"] = For;

        var sb = new StringBuilder();
        sb.Append($"<{TagName}");

        foreach (var attr in Attributes)
        {
            sb.Append($" {attr.Key}=\"{attr.Value}\"");
        }

        sb.Append(">");
        if (!string.IsNullOrEmpty(Text))
            sb.Append(Text);
        sb.Append($"</{TagName}>");

        return sb.ToString();
    }
}

public class IFrame : HtmlElement
{
    public override string TagName => "iframe";
    public string Src { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public string FrameBorder { get; set; } = "0";

    public override string Render()
    {
        if (!string.IsNullOrEmpty(Src))
            Attributes["src"] = Src;
        if (Width.HasValue)
            Attributes["width"] = Width.Value.ToString();
        if (Height.HasValue)
            Attributes["height"] = Height.Value.ToString();
        Attributes["frameborder"] = FrameBorder;

        return base.Render();
    }
}

public class Audio : HtmlElement
{
    public override string TagName => "audio";
    public string Src { get; set; }
    public bool Controls { get; set; } = true;
    public bool AutoPlay { get; set; }
    public bool Loop { get; set; }

    public override string Render()
    {
        if (!string.IsNullOrEmpty(Src))
            Attributes["src"] = Src;
        if (Controls)
            Attributes["controls"] = "controls";
        if (AutoPlay)
            Attributes["autoplay"] = "autoplay";
        if (Loop)
            Attributes["loop"] = "loop";

        return base.Render();
    }
}

public class Video : HtmlElement
{
    public override string TagName => "video";
    public string Src { get; set; }
    public bool Controls { get; set; } = true;
    public bool AutoPlay { get; set; }
    public bool Loop { get; set; }
    public string Poster { get; set; }

    public override string Render()
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

        return base.Render();
    }
}

public class Meta : HtmlElement
{
    public override string TagName => "meta";
    public override bool IsSelfClosing => true;

    public string Charset { get; set; }
    public string Name { get; set; }
    public string Content { get; set; }

    public override string Render()
    {
        if (!string.IsNullOrEmpty(Charset))
            Attributes["charset"] = Charset;
        if (!string.IsNullOrEmpty(Name))
            Attributes["name"] = Name;
        if (!string.IsNullOrEmpty(Content))
            Attributes["content"] = Content;

        return base.Render();
    }
}

public class Link : HtmlElement
{
    public override string TagName => "link";
    public override bool IsSelfClosing => true;

    public string Rel { get; set; }
    public string Href { get; set; }
    public string Type { get; set; }

    public override string Render()
    {
        if (!string.IsNullOrEmpty(Rel))
            Attributes["rel"] = Rel;
        if (!string.IsNullOrEmpty(Href))
            Attributes["href"] = Href;
        if (!string.IsNullOrEmpty(Type))
            Attributes["type"] = Type;

        return base.Render();
    }
}

public class Script : HtmlElement
{
    public override string TagName => "script";
    public string Src { get; set; }
    public string Type { get; set; } = "text/javascript";
    public bool Async { get; set; }
    public bool Defer { get; set; }
    public string Code { get; set; }

    public override string Render()
    {
        if (!string.IsNullOrEmpty(Src))
            Attributes["src"] = Src;
        Attributes["type"] = Type;
        if (Async)
            Attributes["async"] = "async";
        if (Defer)
            Attributes["defer"] = "defer";

        var sb = new StringBuilder();
        sb.Append($"<{TagName}");

        foreach (var attr in Attributes)
        {
            sb.Append($" {attr.Key}=\"{attr.Value}\"");
        }

        if (string.IsNullOrEmpty(Src) && !string.IsNullOrEmpty(Code))
        {
            sb.Append(">");
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

public class StyleElement : HtmlElement
{
    public override string TagName => "style";
    public string Type { get; set; } = "text/css";
    public string Media { get; set; }
    public string Css { get; set; }

    public override string Render()
    {
        Attributes["type"] = Type;
        if (!string.IsNullOrEmpty(Media))
            Attributes["media"] = Media;

        var sb = new StringBuilder();
        sb.Append($"<{TagName}");

        foreach (var attr in Attributes)
        {
            sb.Append($" {attr.Key}=\"{attr.Value}\"");
        }

        sb.Append(">");
        if (!string.IsNullOrEmpty(Css))
            sb.Append(Css);
        sb.Append($"</{TagName}>");

        return sb.ToString();
    }
}

public class TitleElement : HtmlElement
{
    public override string TagName => "title";

    public override string Render()
    {
        return $"<{TagName}>{Text}</{TagName}>";
    }
}

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

public class Blockquote : HtmlElement
{
    public override string TagName => "blockquote";
    public string Cite { get; set; }

    public override string Render()
    {
        if (!string.IsNullOrEmpty(Cite))
            Attributes["cite"] = Cite;

        var sb = new StringBuilder();
        sb.Append($"<{TagName}");

        foreach (var attr in Attributes)
        {
            sb.Append($" {attr.Key}=\"{attr.Value}\"");
        }

        sb.Append(">");
        if (!string.IsNullOrEmpty(Text))
            sb.Append(Text);
        sb.Append($"</{TagName}>");

        return sb.ToString();
    }
}
