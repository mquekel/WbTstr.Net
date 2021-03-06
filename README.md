__DEPRECATED__ in favor of WbTstr ([wbtstr/wbtstr](https://github.com/wbtstr/wbtstr)).

# WbTstr.Net

## Overview
<!-- start -->
WbTstr.Net is a tool which makes testing the web easy. It's based on Selenium WebDriver but with an much easier syntax.
It allows teams to focus on Test Automation with as little hassle as possible. 

<!-- end -->


<!-- start -->
## Background
We believe that Test Automation is a team effort. So coding your tests is the only right way of doing it.
But we don't want to be distracted by syntax or timings. That's why we started WbTstr.Net, based on
[FluentAutomation](http://fluent.stirno.com).

WbTstr.Net is a simple tool which let you create your tests in a fluent syntax within minutes.
We built it on top of WebDriver which allows you to test in all major browsers.

So let's get started [here](/guide.html)
 
<!-- end -->


<!-- start -->
## Contributors

WbTstr.Net is an open source project by [Mirabeau](https://www.mirabeau.nl).

We have the following contributors (A-Z):
* Maarten Groeneweg
* Onno Valkering
* Marcel de Wit
<!-- end -->

<!-- start -->
## Guide

### Install
There are 2 ways of installing WbTstr.Net. 
Unless you would like a specific project structure we advise to start with the Visual Studio extension.

#### Use the Visual Studio extension
The quickest way of getting started with WbTstr.Net is by using the Visual Studio extension.
It helps you with a Visual Studio project which has everything in place to get started with testing right away.

The easiest way to get started with WbTstr.Net is to install our Visual Studio 2013 Extension from the Visual Studio Gallery.

1. Download and install the extension from the [Visual Studio Market Place](https://marketplace.visualstudio.com/items?itemName=Mirabeaunl.WbTstrNet)
2. Simply create a new project of the type "Mirabeau WbTstr.Net"
3. Build the project to retrieve the dependencies

#### Use the NuGet package
If you would like to fit WbTstr.Net into your own project structure that's possible as well.
Just download the NuGet package.
```csharp
Install-Package WbTstr.Net.SeleniumWebDriver
```

### Example test

```csharp   
    namespace MyTest.Examples 
    {
      public class MyFirstTest : FluentTest
      {
        public MyFirstTest()
        {
          WbTstr.Configure()
          .PreferedBrowser().IsChrome();
          WbTstr.Bootstrap();
        }

        [TestCase]
        public void Test1()
        {
           I.Open("http://www.mirabeau.nl");
           I.Assert.Exists("H1");
        }
      }  
    }
```	

<!-- end -->


<!-- start -->
## API


### Configuration
The WbTstr class provides a fluent interface to configure WbTstr.Net

#### Basic usage
In the most basic setup, the WbTstr class can be used to specify the webdriver that needs to be used.
```csharp
    WbTstr.Configure()
        .UseWebDriver(SeleniumWebDriver.Browser.Chrome)
        .BootstrapInstance();
```

Calling the _.Configure_ method initializes the WbTstr class for configuration, the _.BootstrapInstance_ and _.Bootstrap_ methods must be called to finalize the configuration. Recommended is to configure WbTstr from a shared base test class. 

It is also possible to configure WbTstr using a configuration file (app.config). When using only file based configuration it is no longer required to call the _.Configure_ method. However, you can still use it to override file based configuration. It is still required to call the _.Bootstrap_ method, as in the example below. 
```csharp
    <appSettings>
        <add key="WbTstr:UseWebDriver" value="Chrome" />
    </appSettings>

    WbTstr.Boostrap();
```


#### Configure BrowserStack
The WbTstr class can also be used to configure BrowserStack, instead of calling the _.UseWebDriver_ method the _.UseBrowserStackAsRemoteDriver_ method is called. An example is shown below.
```csharp
    WbTstr.Configure()
        .UseBrowserStackAsRemoteDriver()
        .SetBrowserStackCredentials("usr", "pwd")    // Recommendation: put credentials in a config file.
        .EnableBrowserStackLocal()                   
        .BootstrapInstance();
```
When calling the _.EnableBrowserStackLocal_ method, WbTstr will setup [BrowserStack Local](https://www.browserstack.com/local-testing) for testing on private networks.

A few handy methods are added to set BrowserStack capabilities:
```csharp
    WbTstr.Configure()
        .UseBrowserStackAsRemoteDriver()
        .PreferedBrowser().IsChrome()
        .PreferedOperatingSystem().IsWindows()
        .PreferedScreenResolution().IsAny()
        .EnableBrowserStackProjectGrouping("projectName")
        .SetBrowserStackBuildIdentifier("buildIdentification")
        .BootstrapInstance();
```
For more information about these capabilities, see the BrowserStack [capabilities documentation](https://www.browserstack.com/automate/capabilities).

#### Adding custom capabilities
Additional selenium, BrowserStack and/or custom capabilities can be added with the ._SetCapability_ method.
```csharp
    WbTstr.Configure()
        .SetCapability("deviceOrientation", "landscape")
        .BootstrapInstance();
```
#### File based configuration
The following tables shows what can be configured using file based configuration (more will be added).

| Key  |      Options |
|----------|-------------|
| EnableDebug |  false / true | 
| UseWebDriver |    Chrome / InternetExplorer / InternetExplorer64 / Firefox / PhantomJs / Safari  |
| EnableDryRun | true / false |
| EnableBrowserStackLocal | true / false |
| BrowserStackProject | non-empty string |

WbTstr.Net will first look for environment variables before looking in the configuration file.

### Page Objects
Anyone who has done any serious amount of automated testing will tell you that the largest challenge we all face is fragile, hard to maintain tests.

To combat this, it can be useful to use `PageObject` to group your actions/expects/asserts. This gives you functional code that can be reused in any number of tests.

With the new first-class `PageObject` support, we provide some simple but useful built-in functions such as `Go` and `Switch`. Also included, a validation function tied to the `this.At` property.

Any time a `PageObject` navigation is triggered, or any time `Switch` is used, the included `At` function will execute providing an easy hook to make sure all tests execute with the underlying browser and application in the same state, every time.

If you have any ideas for or comments on the new <code>PageObject</code> functionality, let us know! It's a new feature and we'd love some feedback.


```csharp
public class HomePage : PageObject<HomePage>
{
    public HomePage(FluentTest test)
        : base(test)
    {
        Url = "http://www.mirabeau.nl";
        At = () =>; I.Expect.Exists("H1");
    }

    public HomePage OpenContactPage()
    {
        I.Click("#nav a[href='/nl-nl/contact']");
        return this.Switch<ContactPage>();
    }
}

public class ContactPage : PageObject<ContactPage>
{
    public ContactPage(FluentTest test)
        : base(test)
    {
        At = () => I.Expect.Exists(".locations");
    }

    public ContactPage AssertHasPhone()
    {
        I.Expect.Exists(".icon-telephone");
        return this;
    }
}

public class SampleTest : FluentTest
{
    public SampleTest()
    {
         WbTstr.Configure()
        .UseBrowserStackAsRemoteDriver()
        .PreferedBrowser().IsChrome()
        .BootstrapInstance();
    }

    [Fact]
    public void CheckForPhoneIcon()
    {
        new HomePage(this)
            .Go()
            .OpenContactPage()
            .AssertHasPhone();
    }
}
```

### Actions
We interchangeable use the terms `Action` and `Command` to refer to the same thing. This is because of terminology changes throughout the lifetime of the project.
Actions are the units of work which manipulate state of the browser or the page.

#### I.Append
Append text to the current values of inputs and textareas. Automatically calls <code>ToString()</code> on non-string values to simplify tests.
Using `WithoutEvents()` will cause the value to be set via JavaScript and will not trigger keyup/keydown events in the browser.

```csharp
// Append text onto element value
I.Append("Test!").In("#searchBox");

// Append text without keyup/keydown events
I.Append("Test!").WithoutEvents().In("#searchBox");

// Append an integer onto element value
I.Append(6).In("#quantity");

// Append text using cached reference to element
var element = I.Find("#searchBox");
I.Append("Test!").In(element);
```

#### I.Click
Click an element by selector, coordinates or cached reference. Optionally provide an offset to click relative to the item.

```csharp
// Click by element selector
I.Click("#searchBox");

// Click by x, y coordinates
I.Click(10, 100);

// Click by relative offset from element selector
I.Click("#searchBox", 10, 100);

// Click using cached reference to element.
var element = I.Find("#searchBox");
I.Click(element);
```

#### I.DoubleClick
Double-click an element by selector, coordinates or cached reference. Optionally provide an offset to click relative to the item.

```csharp
// DoubleClick by element selector
I.DoubleClick("#searchBox");

// DoubleClick by x, y coordinates
I.DoubleClick(10, 100);

// DoubleClick by relative offset from element selector
I.DoubleClick("#searchBox", 10, 100);

// DoubleClick using cached reference to element.
var element = I.Find("#searchBox");
I.DoubleClick(element);
```

#### I.Drag
Drag &amp; drop works with elements, coordinates and offsets. In the next version, the offset functionality will look a bit less... stupid. Sorry about that.

```csharp
// Drag one element to another
I.Drag("#drag").To("#drop");

// Drag one coordinate to another
I.Drag(100, 100).To(500, 500);

// Drag from element offset to another element
I.Drag(I.Find("#drag"), 50, 50).To("#drop");

// Drag from element to another elements offset
I.Drag("#drag").To(I.Find("#drop", 100, 30));
```

#### I.Enter
Primary method of entering text into inputs and textareas. Automatically calls `ToString()` on non-string values to simplify tests.

Using `WithoutEvents()` will cause the value to be set via JavaScript and will not trigger keyup/keydown events in the browser.

```csharp
// Enter text into element
I.Enter("Test!").In("#searchBox");

// Enter text without keyup/keydown events
I.Enter("Test!").WithoutEvents().In("#searchBox");

// Enter an integer into element
I.Enter(6).In("#quantity");

// Enter text using cached reference to element
var element = I.Find("#searchBox");
I.Enter("Test!").In(element);
```

#### I.Find
Get a factory reference to an element. Returns a function that can be evaluated to return access to the underlying element. Used internally by all functions that target elements.

A second method, `I.FindMultiple`, exists to retrieve a collection of elements at once. It provides the same factory function but returns an `IEnumerable` instead.

Often this function is used to break through the abstraction and get direct access to the providers element representation. This can be necessary in some cases but using `I.Find` in this way is discouraged.

**Warning:** If you intend to cache an element, cache this function not its result. The result is not kept up to date with the current state of the page.

```csharp
// Find element by selector
var element = I.Find("#searchBox");

// Get reference to underlying IWebElement (Selenium)
var webElement = element() as OpenQA.Selenium.IWebElement;

// Get reference to underlying WatiN.Core.Element (WatiN)
var webElement = element() as WatiN.Core.Element;

// Find a collection of elements matching selector
var listItems = I.FindMultiple("li");
```

#### I.Focus
Set the browser's current focus to a specified element or cached reference to an element.

```csharp
// Set browser focus by selector
I.Focus("#searchBox");

// Set browser focus using cached reference to element
var element = I.Find("#searchBox");
```

#### I.Hover
Cause the mouse to hover over a specified element, coordinates or position relative to an element.

```csharp
// Hover over element
I.Hover("#searchBox");

// Hover over x, y coordinates
I.Hover(10, 100);

// Hover over relative offset from element selector
I.Hover("#searchBox", 10, 100);

// Hover using cached reference to element.
var element = I.Find("#searchBox");
I.Hover(element);
```

#### I.Open
Open and navigate the web browser to the specified URL or <a href="http://msdn.microsoft.com/en-us/library/system.uri(v=vs.110).aspx" target="_blank">Uri</a>. Using a Uri can be valuable to validate your URI fragment before using it in a test.

```csharp
// Open browser via string/URL
I.Open("http://google.com");

// Open browser via URI
I.Open(new Uri("http://google.com"));
```

#### I.Press
Triggers a single OS level keypress event. This method will send events to whatever the active window is at the time its trigger, currently **not guaranteed to be the actual browser window. Use with caution.**

The intended use is for interactive with elements that steal focus or are not a part of the DOM such as Flash.

Several keys require special values to be used. Refer to the <a href="http://msdn.microsoft.com/en-us/library/system.windows.forms.sendkeys.aspx" target="_blank">Windows Forms SendKeys documentation</a> for valid values.

```csharp
// Press Tab key
I.Press("{TAB}");
```

#### I.RightClick
Right-click an element by selector, coordinates or cached reference. Optionally provide an offset to click relative to the item.

```csharp
// RightClick by element selector
I.RightClick("#searchBox");

// RightClick by x, y coordinates
I.RightClick(10, 100);

// RightClick by relative offset from element selector
I.RightClick("#searchBox", 10, 100);

// RightClick using cached reference to element.
var element = I.Find("#searchBox");
I.RightClick(element);
```

#### I.Select
Primary method of selecting items in `<SELECT>` elements found via selector or cached reference. Selection can be done using `<OPTION>` value, text or index.

Selecting via Text/string will fall back to Value matching if no match is found. This simplifies the API for most users but may be confusing. If you need to guarantee the selection was based on value use the `Option` overload.

```csharp
// Select option by Text
I.Select("MN").From("#states");

// Select option by index
I.Select(12).From("#states");

// Select option by value
I.Select(Option.Value, 9999).From("#numbers");

// Select multiple options from a multiselect by text, index, or value
I.Select("ND", "MN").From("#states");
I.Select(10, 11, 12).From("#states");
I.Select(Option.Value, 9998, 9999).From("#numbers");
```

#### I.TakeScreenshot
Grab a quick screenshot of the current browser window and save it to disk. The screenshot path is configurable via `Settings.ScreenshotPath`.

```csharp
// Take Screenshot
I.TakeScreenshot("LoginScreen");
```

#### I.Type
Type a string, one character at a time using OS level keypress events. This functionality will send keypress events to whatever the active window is at the time its trigger, currently **not guaranteed to be the actual browser window. Use with caution.**

The intended use is for interactive with elements that steal focus or are not a part of the DOM such as Flash.

Type does not support the use of special key values.

```csharp
// Type string
I.Type("Test!");
```

#### I.Upload
Upload a file using an `<input type="file">` on the current page. The provided path must be absolute and point to the file you want to upload.

This has been used with several Flash uploaders without issue. Your mileage may vary.

```csharp
// Upload LoginScreen.jpg
I.Upload("input[type='file'].uploader", @"C:\LoginScreen");
```

#### I.Wait

Wait for a specified period of time before continuing the test. Method accepts a number of seconds or a <a href="http://msdn.microsoft.com/en-us/library/system.timespan(v=vs.110).aspx" target="_blank">TimeSpan</a>. Not guaranteed to be exact.

In most cases, your tests will be less fragile if you can utilize <a href="#i-waituntil">I.WaitUntil</a> instead.

```csharp
// Wait for 10 seconds
I.Wait(10);

// Wait for 500 milliseconds
I.Wait(TimeSpan.FromMilliseconds(500));
```

#### I.WaitUntil
Recommended method of waiting in tests. Conditional wait using anonymous functions that either return `true` / `false` or throw an `Exception` when the condition has not been met. Useful when content on the page is loaded dynamically or changes state during interactions.

If the condition has not been met, a timed wait will be executed before testing the condition again. The duration if this wait is set in `Settings.DefaultWaitUntilThreadSleep`.

The condition must succeed within the time set in `Settings.DefaultWaitUntilTimeout` or the test will fail.

Important Note: Most actions have implicit WaitUntil functionality built-in. Before adding, be sure your test needs it.

```csharp
// WaitUntil element exists
I.WaitUntil(() => I.Assert.Exists("#searchBar"));

// WaitUntil element has attribute 'data-loaded'
I.WaitUntil(() =>
    I.Find("#searchBar")()
        .Attributes
        .Get("data-loaded") == "true"
);
```

### Assertions

#### I.Assert.Class
Assert that an element matching selector has the specified class.

```csharp
// Has btn-primary class
I.Assert.Class("btn-primary").Of("header");
```

#### I.Assert.Count
Assert that we have a certain count of items matching a selector.

```csharp
// 1 search bar on page.
I.Assert.Count(1).Of("#searchBar");
```

#### I.Assert.Exists
Assert that an element is on the page.

```csharp
// Element on page
I.Assert.Exists("#searchBar");
```

#### I.Assert.False
Assert that an anonymous function should return false. Use with `I.Find` to fail tests properly if conditions are not met.

```csharp
// Element is not a select box
var element = I.Find("input");
I.Assert.False(() => element().IsSelect);
```

#### I.Assert.Text
Assert that an element matching selector has the specified text. Works with any DOM element that has `innerHTML` or can provides its contents/value via text.

Supports anonymous functions that return `true` or `false`.

```csharp
// Header tag set to Test!
I.Assert.Text("Test!").In("header");

// Content longer than 50 characters
I.Assert.Text((text) => text.Length > 50).In("#content");
```
#### I.Assert.Throws
Assert that an `Exception` should be thrown by the anonymous function. Useful for negative assertions such as testing that something is not present.

```csharp
// Page has no errors
I.Assert.Throws(() => I.Assert.Exists(".error"));
```

#### I.Assert.True
Assert that an anonymous function should return true. Use with `I.Find` to fail tests properly if conditions are not met.

```csharp
// Element is a select box
var element = I.Find("select");
I.Assert.True(() => element().IsSelect);
```

#### I.Assert.Url
Assert that the browser has the specified Url. If using the string or Uri overloads, the match must be exact.

Supports anonymous functions that return `true` or `false`. Particularly useful on pages that modify the URL via hashtags or other mechanisms.

```csharp
// At #assert-url on docs
I.Assert.Url("http://fluent.stirno.com/docs/#assert-url");

// Verify we're on SSL
I.Assert.Url((uri) => uri.Scheme == "https");
```

#### I.Assert.Value
Assert that an element matching selector has the specified value. Works with `<INPUT>`, `<TEXTAREA>` and `<SELECT>`

Supports anonymous functions that return `true` or `false`.

```csharp
// Dropdown has value of 10.
I.Assert.Value(10).In("#quantity");

// Value starts with 'M'
I.Assert.Value((value) => value.StartsWith("M")).In("#states");
```

<!-- end -->
