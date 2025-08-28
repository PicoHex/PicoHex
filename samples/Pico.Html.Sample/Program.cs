var doc = new HtmlDocument { Lang = "en-us", Head = { Title = "My Html" } };

doc.Head.MetaTags.Add(
    new Meta { Name = "viewport", Content = "width=device-width, initial-scale=1.0" }
);
doc.Head.Links.Add(
    new Link
    {
        Rel = "stylesheet",
        Href = "styles.css",
        Type = "text/css"
    }
);

var header = new Header();
header.AddChild(new H1 { Text = "Welcome" });

var nav = new Nav();
var ul = new UnorderedList();

var homeItem = new ListItem();
homeItem.AddChild(new Anchor { Href = "#home", Text = "Home" });
ul.AddChild(homeItem);

var aboutItem = new ListItem();
aboutItem.AddChild(new Anchor { Href = "#about", Text = "About" });
ul.AddChild(aboutItem);

var contactItem = new ListItem();
contactItem.AddChild(new Anchor { Href = "#contact", Text = "Contact" });
ul.AddChild(contactItem);

nav.AddChild(ul);

var main = new Main();
var article = new Article();
article.AddChild(new H2 { Text = "Title" });
article.AddChild(new Paragraph { Text = "Here are something" });

var form = new Form { Action = "/submit", Method = "post" };

var nameLabel = new Label { For = "name", Text = "name:" };
form.AddChild(nameLabel);

var nameInput = new Input
{
    Type = "text",
    Id = "name",
    Name = "name",
    Required = true
};
form.AddChild(nameInput);

var submitButton = new Button { Type = "submit", Text = "submit" };
form.AddChild(submitButton);

article.AddChild(form);
main.AddChild(article);

var footer = new Footer();
footer.AddChild(new Paragraph { Text = "MyPage" });

doc.Body.AddChild(header);
doc.Body.AddChild(nav);
doc.Body.AddChild(main);
doc.Body.AddChild(footer);

Console.WriteLine(doc.Render());
Console.ReadLine();
