var css = new CssStyleSheet();

// 添加基本样式
css.AddRule(
    "body",
    rule =>
        rule.AddProperty("font-family", "'Segoe UI', Tahoma, Geneva, Verdana, sans-serif")
            .AddProperty("margin", "0")
            .AddProperty("padding", "0")
            .AddProperty("background-color", "#f8f9fa")
            .AddProperty("color", "#333")
);

css.AddRule(
    ".container",
    rule =>
        rule.AddProperty("max-width", "1200px")
            .AddProperty("margin", "0 auto")
            .AddProperty("padding", "20px")
);

css.AddRule(
    ".header",
    rule =>
        rule.AddProperty("background-color", "#343a40")
            .AddProperty("color", "white")
            .AddProperty("padding", "20px 0")
            .AddProperty("margin-bottom", "30px")
);

css.AddRule(
    ".nav",
    rule =>
        rule.AddProperty("display", "flex")
            .AddProperty("justify-content", "center")
            .AddProperty("gap", "20px")
            .AddProperty("list-style", "none")
            .AddProperty("padding", "0")
);

css.AddRule(
    ".nav a",
    rule =>
        rule.AddProperty("color", "white")
            .AddProperty("text-decoration", "none")
            .AddProperty("font-weight", "500")
            .AddProperty("transition", "color 0.3s")
);

css.AddRule(".nav a:hover", rule => rule.AddProperty("color", "#17a2b8"));

css.AddRule(
    ".card",
    rule =>
        rule.AddProperty("background", "white")
            .AddProperty("border-radius", "8px")
            .AddProperty("box-shadow", "0 2px 10px rgba(0,0,0,0.1)")
            .AddProperty("padding", "25px")
            .AddProperty("margin-bottom", "20px")
);

css.AddRule(
    ".btn",
    rule =>
        rule.AddProperty("background-color", "#007bff")
            .AddProperty("color", "white")
            .AddProperty("border", "none")
            .AddProperty("padding", "10px 20px")
            .AddProperty("border-radius", "4px")
            .AddProperty("cursor", "pointer")
            .AddProperty("transition", "background-color 0.3s")
);

css.AddRule(".btn:hover", rule => rule.AddProperty("background-color", "#0056b3"));

css.AddRule(".form-group", rule => rule.AddProperty("margin-bottom", "15px"));

css.AddRule(
    "label",
    rule =>
        rule.AddProperty("display", "block")
            .AddProperty("margin-bottom", "5px")
            .AddProperty("font-weight", "500")
);

css.AddRule(
    "input[type='text'], input[type='email']",
    rule =>
        rule.AddProperty("width", "100%")
            .AddProperty("padding", "10px")
            .AddProperty("border", "1px solid #ddd")
            .AddProperty("border-radius", "4px")
            .AddProperty("box-sizing", "border-box")
);

// 添加媒体查询
css.AddMediaQuery(
    "(max-width: 768px)",
    mediaSheet =>
    {
        mediaSheet.AddRule(".container", rule => rule.AddProperty("padding", "10px"));

        mediaSheet.AddRule(
            ".nav",
            rule =>
                rule.AddProperty("flex-direction", "column")
                    .AddProperty("align-items", "center")
                    .AddProperty("gap", "10px")
        );

        mediaSheet.AddRule(".card", rule => rule.AddProperty("padding", "15px"));
    }
);

// 创建 JavaScript 代码
var js = new JavaScriptCode();

// 添加变量和函数
js.AddVariable("appName", "'我的网站'", true);
js.AddVariable("userCount", "0");

js.AddFunction(
    "showMessage",
    "message",
    @"
                const messageDiv = document.createElement('div');
                messageDiv.textContent = message;
                messageDiv.style.position = 'fixed';
                messageDiv.style.top = '20px';
                messageDiv.style.right = '20px';
                messageDiv.style.background = '#28a745';
                messageDiv.style.color = 'white';
                messageDiv.style.padding = '10px 15px';
                messageDiv.style.borderRadius = '4px';
                messageDiv.style.zIndex = '1000';
                document.body.appendChild(messageDiv);
                
                setTimeout(() => {
                    document.body.removeChild(messageDiv);
                }, 3000);
            "
);

js.AddArrowFunction(
    "handleFormSubmit",
    "e",
    @"
                e.preventDefault();
                const formData = new FormData(e.target);
                const name = formData.get('name');
                const email = formData.get('email');
                
                if (!name || !email) {
                    showMessage('请填写所有必填字段');
                    return;
                }
                
                // 模拟API调用
                setTimeout(() => {
                    showMessage(`感谢提交，${name}！`);
                    e.target.reset();
                    userCount++;
                    updateUserCount();
                }, 1000);
            ",
    true
);

js.AddFunction(
    "updateUserCount",
    "",
    @"
                const countElement = document.getElementById('userCount');
                if (countElement) {
                    countElement.textContent = userCount;
                }
            "
);

// 添加事件监听器
js.AddEventListener("contactForm", "submit", "handleFormSubmit(event)");
js.AddEventListener(
    "themeToggle",
    "click",
    @"
                document.body.classList.toggle('dark-mode');
                const isDarkMode = document.body.classList.contains('dark-mode');
                localStorage.setItem('darkMode', isDarkMode);
                showMessage(isDarkMode ? '已启用深色模式' : '已禁用深色模式');
            "
);

// 添加页面加载时执行的代码
js.AddRawCode(
    @"
                // 检查是否启用了深色模式
                if (localStorage.getItem('darkMode') === 'true') {
                    document.body.classList.add('dark-mode');
                }
                
                // 初始化用户计数
                updateUserCount();
                
                console.log(`${appName} 已加载`);
            "
);

// 创建 HTML 文档
var doc = new HtmlDocument
{
    Lang = "zh-CN",
    Head =
    {
        // 设置头部
        TitleText = "我的网站 - 欢迎"
    }
};

doc.Head.MetaTags.Add(
    new Meta { Name = "viewport", Content = "width=device-width, initial-scale=1.0" }
);
doc.Head.MetaTags.Add(new Meta { Name = "description", Content = "这是一个使用C# HTML构建器创建的示例网站" });

// 添加外部CSS
doc.Head.AddExternalCss(
    "https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.0.0/css/all.min.css"
);

// 添加内联CSS
doc.Head.AddInlineCss(css.ToString());

// 添加外部JavaScript库
doc.Head.AddExternalScript(
    "https://cdnjs.cloudflare.com/ajax/libs/jquery/3.6.0/jquery.min.js",
    true,
    true
);

// 创建主体内容
var header = new Header { Class = "header" };
var headerContainer = new Div { Class = "container" };
headerContainer.AddChild(new H1 { Text = "我的网站", Style = "text-align: center; margin: 0;" });
header.AddChild(headerContainer);

// 创建导航
var nav = new Nav();
var ul = new UnorderedList { Class = "nav" };

var homeItem = new ListItem();
homeItem.AddChild(new Anchor { Href = "#home", Text = "首页" });
ul.AddChild(homeItem);

var aboutItem = new ListItem();
aboutItem.AddChild(new Anchor { Href = "#about", Text = "关于" });
ul.AddChild(aboutItem);

var contactItem = new ListItem();
contactItem.AddChild(new Anchor { Href = "#contact", Text = "联系我们" });
ul.AddChild(contactItem);

nav.AddChild(ul);

// 创建主内容区
var main = new Main();
var container = new Div { Class = "container" };

// 欢迎卡片
var welcomeCard = new Div { Class = "card" };
welcomeCard.AddChild(new H2 { Text = "欢迎访问我的网站" });
welcomeCard.AddChild(
    new Paragraph { Text = "这是一个使用C# HTML构建器创建的示例网站。它展示了如何以面向对象的方式构建包含CSS和JavaScript的完整网页。" }
);
welcomeCard.AddChild(new Paragraph { Text = $"已注册用户: <span id=\"userCount\">0</span>" });

// 添加主题切换按钮
var themeToggle = new Button
{
    Class = "btn",
    Id = "themeToggle",
    Text = "切换深色模式",
    Style = "margin-top: 15px;"
};
welcomeCard.AddChild(themeToggle);

container.AddChild(welcomeCard);

// 联系表单卡片
var formCard = new Div { Class = "card" };
formCard.AddChild(new H2 { Text = "联系我们" });

var form = new Form { Id = "contactForm" };

var nameGroup = new Div { Class = "form-group" };
nameGroup.AddChild(new Label { For = "name", Text = "姓名:" });
nameGroup.AddChild(
    new Input
    {
        Type = "text",
        Id = "name",
        Name = "name",
        Required = true
    }
);
form.AddChild(nameGroup);

var emailGroup = new Div { Class = "form-group" };
emailGroup.AddChild(new Label { For = "email", Text = "邮箱:" });
emailGroup.AddChild(
    new Input
    {
        Type = "email",
        Id = "email",
        Name = "email",
        Required = true
    }
);
form.AddChild(emailGroup);

var messageGroup = new Div { Class = "form-group" };
messageGroup.AddChild(new Label { For = "message", Text = "留言:" });
messageGroup.AddChild(
    new TextArea
    {
        Id = "message",
        Name = "message",
        Rows = 4
    }
);
form.AddChild(messageGroup);

var submitButton = new Button
{
    Type = "submit",
    Class = "btn",
    Text = "提交"
};
form.AddChild(submitButton);

formCard.AddChild(form);
container.AddChild(formCard);

main.AddChild(container);

// 创建页脚
var footer = new Footer { Class = "header", Style = "margin-top: 50px;" };
var footerContainer = new Div { Class = "container", Style = "text-align: center;" };
footerContainer.AddChild(new Paragraph { Text = "© 2023 我的网站 - 使用C# HTML构建器创建" });
footer.AddChild(footerContainer);

// 将各部分添加到 body
doc.Body.AddChild(header);
doc.Body.AddChild(nav);
doc.Body.AddChild(main);
doc.Body.AddChild(footer);

// 添加底部JavaScript
var bottomScript = new Script();
bottomScript.SetCode(js);
doc.Body.AddBottomScript(bottomScript);

// 输出 HTML
Console.WriteLine("<!DOCTYPE html>");
Console.WriteLine(doc.Render());
Console.ReadLine();
