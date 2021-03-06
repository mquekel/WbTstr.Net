﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using FluentAutomation.Exceptions;
using FluentAutomation.Interfaces;
using FluentAutomation.Wrappers;

using OpenQA.Selenium;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Remote;
using OpenQA.Selenium.Support.UI;

using Polly;

namespace FluentAutomation
{
    public class CommandProvider : BaseCommandProvider, ICommandProvider, IDisposable
    {
        private readonly IFileStoreProvider _fileStoreProvider;
        private readonly IWebDriver _webDriver;
        private string _mainWindowHandle;

        public CommandProvider(Func<IWebDriver> webDriverFactory, IFileStoreProvider fileStoreProvider)
        {
            FluentTest.ProviderInstance = null;

            _webDriver = InitializeWebDriver(webDriverFactory);
            _fileStoreProvider = fileStoreProvider;
        }

        private IWebDriver InitializeWebDriver(Func<IWebDriver> webDriverFactory)
        {
            var policy = Policy.Handle<InvalidOperationException>().WaitAndRetry(5, i => TimeSpan.FromSeconds(5));
            return policy.Execute(() => WebDriverFactoryMethod(webDriverFactory));
        }

        private IWebDriver WebDriverFactoryMethod(Func<IWebDriver> webDriverFactory, IWebDriver reCreatedWebDriver = null)
        {
            try
            {
                var policy = Policy.Handle<InvalidOperationException>().WaitAndRetry(4, i => TimeSpan.FromSeconds(30));
                return policy.Execute(
                    () =>
                    {
                        var webDriver = reCreatedWebDriver ?? webDriverFactory();
                        if (!FluentTest.IsMultiBrowserTest && FluentTest.ProviderInstance == null)
                        {
                            FluentTest.ProviderInstance = webDriver;
                        }

                        webDriver.Manage().Cookies.DeleteAllCookies();
                        webDriver.Manage().Timeouts().ImplicitlyWait(TimeSpan.FromSeconds(10));

                        // If an alert is open, the world ends if we touch the size property. Ignore this and let it get set by the next command chain
                        try
                        {
                            if (this.Settings.WindowMaximized)
                            {
                                // store window size back before maximizing so we can 'undo' this action if necessary
                                var windowSize = webDriver.Manage().Window.Size;
                                if (!this.Settings.WindowWidth.HasValue) this.Settings.WindowWidth = windowSize.Width;

                                if (!this.Settings.WindowHeight.HasValue) this.Settings.WindowHeight = windowSize.Height;

                                webDriver.Manage().Window.Maximize();
                            }
                            else if (this.Settings.WindowHeight.HasValue && this.Settings.WindowWidth.HasValue)
                            {
                                webDriver.Manage().Window.Size = new Size(this.Settings.WindowWidth.Value, this.Settings.WindowHeight.Value);
                            }
                            else
                            {
                                var windowSize = webDriver.Manage().Window.Size;
                                this.Settings.WindowHeight = windowSize.Height;
                                this.Settings.WindowWidth = windowSize.Width;
                            }
                        }
                        catch (UnhandledAlertException e)
                        {
                            // TODO: handle error
                        }

                        _mainWindowHandle = webDriver.CurrentWindowHandle;
                        return webDriver;
                    });
            }
            catch (InvalidOperationException e)
            {
                if (reCreatedWebDriver != null)
                {
                    Console.WriteLine("Created a new remote WebDriver, but it still doens't seems to work.");
                    throw;
                }

                // Oke, we're going to create a new remote WebDriver (including a new session)
                // But first we try to terminate the current one. 
                Dispose();

                IWebDriver recreated = null;
                try
                {
                    // Get the remote WebDriver configuration
                    WbTstr wbTstr = WbTstr.Configure() as WbTstr;
                    if (wbTstr != null)
                    {
                        var capabilities = new DesiredCapabilities(wbTstr.Capabilities);
                        var remoteDriverUri = wbTstr.RemoteDriverUri;

                        // Create a new remote WebDriver
                        var policy = Policy.Handle<Exception>().WaitAndRetry(4, i => TimeSpan.FromSeconds(30));
                        recreated = policy.Execute(() => new EnhancedRemoteWebDriver(remoteDriverUri, capabilities, TimeSpan.FromSeconds(60)));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to create new remote WebDriver instance.");
                    throw;
                }

                return WebDriverFactoryMethod(webDriverFactory, recreated);
            }
        }

        public ICommandProvider WithConfig(FluentSettings settings)
        {
            // If an alert is open, the world ends if we touch the size property. Ignore this and let it get set by the next command chain
            try
            {
                if (settings.WindowMaximized)
                {
                    // store window size back before maximizing so we can 'undo' this action if necessary
                    // this code intentionally touches this.Settings before its been replaced with the local
                    // configuration code, so that when the With.___.Then block is completed, the outer settings
                    // object has the correct window size to work with.
                    var windowSize = _webDriver.Manage().Window.Size;
                    if (!this.Settings.WindowWidth.HasValue)
                        this.Settings.WindowWidth = windowSize.Width;

                    if (!this.Settings.WindowHeight.HasValue)
                        this.Settings.WindowHeight = windowSize.Height;

                    this._webDriver.Manage().Window.Maximize();
                }
                // If the browser size has changed since the last config change, update it
                else if (settings.WindowWidth.HasValue && settings.WindowHeight.HasValue)
                {
                    this._webDriver.Manage().Window.Size = new Size(settings.WindowWidth.Value, settings.WindowHeight.Value);
                }
            }
            catch (UnhandledAlertException) { }

            this.Settings = settings;

            return this;
        }

        public Uri Url
        {
            get
            {
                return new Uri(this._webDriver.Url, UriKind.Absolute);
            }
        }

        public string Source
        {
            get
            {
                return this._webDriver.PageSource;
            }
        }

        public void Navigate(Uri url)
        {
            this.Act(CommandType.Action, () =>
            {
                var currentUrl = new Uri(this._webDriver.Url);
                var baseUrl = currentUrl.GetLeftPart(System.UriPartial.Authority);

                if (!url.IsAbsoluteUri)
                {
                    url = new Uri(new Uri(baseUrl), url.ToString());
                }

                this._webDriver.Navigate().GoToUrl(url);
            });
        }

        public ElementProxy Find(string selector)
        {
            return new ElementProxy(this, () =>
            {
                try
                {
                    var webElement = this._webDriver.FindElement(Sizzle.Find(selector));
                    return new Element(webElement, selector);
                }
                catch (NoSuchElementException)
                {
                    throw new FluentElementNotFoundException("Unable to find element with selector [{0}]", selector);
                }
            });
        }

        public ElementProxy FindMultiple(string selector)
        {
            var finalResult = new ElementProxy();

            finalResult.Children.Add(new Func<ElementProxy>(() =>
            {
                var result = new ElementProxy();
                var webElements = this._webDriver.FindElements(Sizzle.Find(selector));
                if (webElements.Count == 0)
                    throw new FluentElementNotFoundException("Unable to find element with selector [{0}].", selector);

                foreach (var element in webElements)
                {
                    result.Elements.Add(new Tuple<ICommandProvider, Func<IElement>>(this, () => new Element(element, selector)));
                }

                return result;
            }));

            return finalResult;
        }

        public void Click(int x, int y)
        {
            this.Act(CommandType.Action, () =>
            {
                var bodyElement = this.Find("body").Element as Element;

                var xDiff = x - bodyElement.PosX;
                var yDiff = y - bodyElement.PosY; 

                new Actions(this._webDriver)
                    .MoveToElement(bodyElement.WebElement, xDiff, yDiff)
                    .Click()
                    .Perform();
            });
        }

        public void Click(ElementProxy element, int x, int y)
        {
            this.Act(CommandType.Action, () =>
            {
                var containerElement = element.Element as Element;
                new Actions(this._webDriver)
                    .MoveByOffset(x, y)
                    .Click()
                    .Perform();
            });
        }

        public void Click(ElementProxy element)
        {
            this.Act(CommandType.Action, () =>
            {
                var containerElement = element.Element as Element;
                new Actions(this._webDriver)
                    .Click(containerElement.WebElement)
                    .Perform();
            });
        }

        public void DoubleClick(int x, int y)
        {
            var bodyElement = this.Find("body").Element as Element;

            var xDiff = x - bodyElement.PosX;
            var yDiff = y - bodyElement.PosY; 
            this.Act(CommandType.Action, () =>
            {
                new Actions(this._webDriver)
                    .MoveToElement(bodyElement.WebElement, xDiff, yDiff)
                    .DoubleClick()
                    .Perform();
            });
        }

        public void DoubleClick(ElementProxy element, int x, int y)
        {
            this.Act(CommandType.Action, () =>
            {
                var containerElement = element.Element as Element;
                new Actions(this._webDriver)
                    .MoveToElement(containerElement.WebElement, x, y)
                    .DoubleClick()
                    .Perform();
            });
        }

        public void DoubleClick(ElementProxy element)
        {
            this.Act(CommandType.Action, () =>
            {
                var containerElement = element.Element as Element;
                new Actions(this._webDriver)
                    .DoubleClick(containerElement.WebElement)
                    .Perform();
            });
        }

        public void RightClick(int x, int y)
        {
            var bodyElement = this.Find("body").Element as Element;

            var xDiff = x - bodyElement.PosX;
            var yDiff = y - bodyElement.PosY;
            this.Act(CommandType.Action, () =>
            {
                new Actions(this._webDriver)
                    .MoveToElement(bodyElement.WebElement, xDiff, yDiff)
                    .ContextClick()
                    .Perform();
            });
        }

        public void RightClick(ElementProxy element, int x, int y)
        {
            this.Act(CommandType.Action, () =>
            {
                var containerElement = element.Element as Element;
                new Actions(this._webDriver)
                    .MoveToElement(containerElement.WebElement, x, y)
                    .ContextClick()
                    .Perform();
            });
        }

        public void RightClick(ElementProxy element)
        {
            this.Act(CommandType.Action, () =>
            {
                var containerElement = element.Element as Element;
                new Actions(this._webDriver)
                    .ContextClick(containerElement.WebElement)
                    .Perform();
            });
        }

        public void Hover(int x, int y)
        {
            var bodyElement = this.Find("body").Element as Element;

            var xDiff = x - bodyElement.PosX;
            var yDiff = y - bodyElement.PosY;
            this.Act(CommandType.Action, () =>
            {
                new Actions(this._webDriver)
                    .MoveToElement(bodyElement.WebElement, xDiff, yDiff)
                    .Perform();
            });
        }

        public void Hover(ElementProxy element, int x, int y)
        {
            this.Act(CommandType.Action, () =>
            {
                var containerElement = element.Element as Element;
                new Actions(this._webDriver)
                    .MoveToElement(containerElement.WebElement, x, y)
                    .Perform();
            });
        }

        public void Hover(ElementProxy element)
        {
            this.Act(CommandType.Action, () =>
            {
                var unwrappedElement = element.Element as Element;
                new Actions(this._webDriver)
                    .MoveToElement(unwrappedElement.WebElement)
                    .Perform();
            });
        }

        public void Focus(ElementProxy element)
        {
            this.Act(CommandType.Action, () =>
            {
                var unwrappedElement = element.Element as Element;

                switch (unwrappedElement.WebElement.TagName)
                {
                    case "input":
                    case "select":
                    case "textarea":
                    case "a":
                    case "iframe":
                    case "button":
                        var executor = (IJavaScriptExecutor)this._webDriver;
                        executor.ExecuteScript("arguments[0].focus();", unwrappedElement.WebElement);
                        break;
                }
            });
        }
        
        public void DragAndDrop(int sourceX, int sourceY, int destinationX, int destinationY)
        {
            var bodyElement = this.Find("body").Element as Element;

            var xSourceDiff = sourceX - bodyElement.PosX;
            var ySourceDiff = sourceY - bodyElement.PosY;

            var xdestinationDiff = destinationX - bodyElement.PosX;
            var ydestinationDiff = destinationY - bodyElement.PosY;

            this.Act(CommandType.Action, () =>
            {
                new Actions(this._webDriver)
                    .MoveToElement(bodyElement.WebElement, xSourceDiff, ySourceDiff)
                    .ClickAndHold()
                    .MoveToElement(bodyElement.WebElement, xdestinationDiff, ydestinationDiff)
                    .Release()
                    .Perform();
            });
        }

        public void DragAndDrop(ElementProxy source, int sourceOffsetX, int sourceOffsetY, ElementProxy target, int targetOffsetX, int targetOffsetY)
        {
            this.Act(CommandType.Action, () =>
            {
                var element = source.Element as Element;
                var targetElement = target.Element as Element;
                new Actions(this._webDriver)
                    .MoveToElement(element.WebElement, sourceOffsetX, sourceOffsetY)
                    .ClickAndHold()
                    .MoveToElement(targetElement.WebElement, targetOffsetX, targetOffsetY)
                    .Release()
                    .Perform();
            });
        }

        public void DragAndDrop(ElementProxy source, ElementProxy target)
        {
            this.Act(CommandType.Action, () =>
            {
                var unwrappedSource = source.Element as Element;
                var unwrappedTarget = target.Element as Element;

                new Actions(this._webDriver)
                    .DragAndDrop(unwrappedSource.WebElement, unwrappedTarget.WebElement)
                    .Perform();
            });
        }

        public void EnterText(ElementProxy element, string text)
        {
            this.Act(CommandType.Action, () =>
            {
                var unwrappedElement = element.Element as Element;

                unwrappedElement.WebElement.Clear();
                unwrappedElement.WebElement.SendKeys(text);
            });
        }

        public void EnterTextWithoutEvents(ElementProxy element, string text)
        {
            this.Act(CommandType.Action, () =>
            {
                var unwrappedElement = element.Element as Element;

                ((IJavaScriptExecutor)this._webDriver).ExecuteScript(string.Format("if (typeof fluentjQuery != 'undefined') {{ fluentjQuery(\"{0}\").val(\"{1}\").trigger('change'); }}", unwrappedElement.Selector.Replace("\"", ""), text.Replace("\"", "")));
            });
        }

        public void AppendText(ElementProxy element, string text)
        {
            this.Act(CommandType.Action, () =>
            {
                var unwrappedElement = element.Element as Element;
                unwrappedElement.WebElement.SendKeys(text);
            });
        }

        public void AppendTextWithoutEvents(ElementProxy element, string text)
        {
            this.Act(CommandType.Action, () =>
            {
                var unwrappedElement = element.Element as Element;
                ((IJavaScriptExecutor)this._webDriver).ExecuteScript(string.Format("if (typeof fluentjQuery != 'undefined') {{ fluentjQuery(\"{0}\").val(fluentjQuery(\"{0}\").val() + \"{1}\").trigger('change'); }}", unwrappedElement.Selector.Replace("\"", ""), text.Replace("\"", "")));
            });
        }

        public void SelectText(ElementProxy element, string optionText)
        {
            this.Act(CommandType.Action, () =>
            {
                var unwrappedElement = element.Element as Element;

                SelectElement selectElement = new SelectElement(unwrappedElement.WebElement);
                if (selectElement.IsMultiple) selectElement.DeselectAll();
                selectElement.SelectByText(optionText);
            });
        }

        public void MultiSelectValue(ElementProxy element, string[] optionValues)
        {
            this.Act(CommandType.Action, () =>
            {
                var unwrappedElement = element.Element as Element;

                SelectElement selectElement = new SelectElement(unwrappedElement.WebElement);
                if (selectElement.IsMultiple) selectElement.DeselectAll();

                foreach (var optionValue in optionValues)
                {
                    selectElement.SelectByValue(optionValue);
                }
            });
        }

        public void MultiSelectIndex(ElementProxy element, int[] optionIndices)
        {
            this.Act(CommandType.Action, () =>
            {
                var unwrappedElement = element.Element as Element;

                SelectElement selectElement = new SelectElement(unwrappedElement.WebElement);
                if (selectElement.IsMultiple) selectElement.DeselectAll();

                foreach (var optionIndex in optionIndices)
                {
                    selectElement.SelectByIndex(optionIndex);
                }
            });
        }

        public void MultiSelectText(ElementProxy element, string[] optionTextCollection)
        {
            this.Act(CommandType.Action, () =>
            {
                var unwrappedElement = element.Element as Element;

                SelectElement selectElement = new SelectElement(unwrappedElement.WebElement);
                if (selectElement.IsMultiple) selectElement.DeselectAll();

                foreach (var optionText in optionTextCollection)
                {
                    selectElement.SelectByText(optionText);
                }
            });
        }

        public void SelectValue(ElementProxy element, string optionValue)
        {
            this.Act(CommandType.Action, () =>
            {
                var unwrappedElement = element.Element as Element;

                SelectElement selectElement = new SelectElement(unwrappedElement.WebElement);
                if (selectElement.IsMultiple) selectElement.DeselectAll();
                selectElement.SelectByValue(optionValue);
            });
        }

        public void SelectIndex(ElementProxy element, int optionIndex)
        {
            this.Act(CommandType.Action, () =>
            {
                var unwrappedElement = element.Element as Element;

                SelectElement selectElement = new SelectElement(unwrappedElement.WebElement);
                if (selectElement.IsMultiple) selectElement.DeselectAll();
                selectElement.SelectByIndex(optionIndex);
            });
        }

        public override void TakeScreenshot(string screenshotName)
        {
            this.Act(CommandType.Action, () =>
            {
                // get raw screenshot
                var screenshotDriver = (ITakesScreenshot)this._webDriver;
                var tmpImagePath = Path.Combine(this.Settings.UserTempDirectory, screenshotName);
                screenshotDriver.GetScreenshot().SaveAsFile(tmpImagePath, ImageFormat.Png);

                // save to file store
                this._fileStoreProvider.SaveScreenshot(this.Settings, File.ReadAllBytes(tmpImagePath), screenshotName);
                File.Delete(tmpImagePath);
            });
        }

        public void UploadFile(ElementProxy element, int x, int y, string fileName)
        {
            this.Act(CommandType.Action, () =>
            {
                // wait before typing in the field
                var task = Task.Factory.StartNew(() =>
                {
                    this.Wait(TimeSpan.FromMilliseconds(1000));
                    this.Type(fileName);
                });

                if (x == 0 && y == 0)
                {
                    this.Click(element);
                }
                else
                {
                    this.Click(element, x, y);
                }

                task.Wait();
                this.Wait(TimeSpan.FromMilliseconds(1500));
            });
        }

        public void Press(string keys)
        {
            this.Act(CommandType.Action, () => System.Windows.Forms.SendKeys.SendWait(keys));
        }

        public void Type(string text)
        {
            this.Act(CommandType.Action, () =>
            {
                foreach (var character in text)
                {
                    System.Windows.Forms.SendKeys.SendWait(character.ToString());
                    this.Wait(TimeSpan.FromMilliseconds(20));
                }
            });
        }

        public void SwitchToWindow(string windowName)
        {
            this.Act(CommandType.Action, () =>
            {
                if (windowName == string.Empty)
                {
                    this._webDriver.SwitchTo().Window(this._mainWindowHandle);
                    return;
                }

                var matchFound = false;
                foreach (var windowHandle in this._webDriver.WindowHandles)
                {
                    this._webDriver.SwitchTo().Window(windowHandle);

                    if (this._webDriver.Title == windowName || this._webDriver.Url.EndsWith(windowName))
                    {
                        matchFound = true;
                        break;
                    }
                }

                if (!matchFound)
                {
                    throw new FluentException("No window with a title or URL matching [{0}] could be found.", windowName);
                }
            });
        }

        public void SwitchToFrame(string frameNameOrSelector)
        {
            this.Act(CommandType.Action, () =>
            {
                if (frameNameOrSelector == string.Empty)
                {
                    this._webDriver.SwitchTo().DefaultContent();
                    return;
                }

                // try to locate frame using argument as a selector, if that fails pass it into Frame so it can be
                // evaluated as a name by Selenium
                IWebElement frameBySelector = null;
                try
                {
                    frameBySelector = this._webDriver.FindElement(Sizzle.Find(frameNameOrSelector));
                }
                catch (NoSuchElementException)
                {
                }

                if (frameBySelector == null)
                    this._webDriver.SwitchTo().Frame(frameNameOrSelector);
                else
                    this._webDriver.SwitchTo().Frame(frameBySelector);
            });
        }

        public void SwitchToFrame(ElementProxy frameElement)
        {
            this.Act(CommandType.Action, () =>
            {
                this._webDriver.SwitchTo().Frame((frameElement.Element as Element).WebElement);
            });
        }

        public static IAlert ActiveAlert = null;
        private void SetActiveAlert()
        {
            if (ActiveAlert == null)
            {
                this.Act(CommandType.Action, () =>
                {
                    try
                    {
                        ActiveAlert = this._webDriver.SwitchTo().Alert();
                    }
                    catch (Exception ex)
                    {
                        throw new FluentException(ex.Message, ex);
                    }
                });
            }
        }

        public void AlertClick(Alert accessor)
        {
            this.SetActiveAlert();
            if (ActiveAlert == null)
                return;

            try
            {
                this.Act(CommandType.Action, () =>
                {
                    try
                    {
                        if (accessor == Alert.OK)
                            ActiveAlert.Accept();
                        else
                            ActiveAlert.Dismiss();
                    }
                    catch (NoAlertPresentException ex)
                    {
                        throw new FluentException(ex.Message, ex);
                    }
                });
            }
            finally
            {
                ActiveAlert = null;
            }
        }

        public void AlertText(Action<string> matchFunc)
        {
            this.SetActiveAlert();
            matchFunc(ActiveAlert.Text);
        }

        public void AlertEnterText(string text)
        {
            this.SetActiveAlert();
            ActiveAlert.SendKeys(text);

            try
            {
                // just do it - attempting to get behaviors between browsers to match
                ActiveAlert.Accept();
            }
            catch (Exception) { }
        }

        public void Visible(ElementProxy element, Action<bool> action)
        {
            this.Act(CommandType.Action, () =>
            {
                var isVisible = (element.Element as Element).WebElement.Displayed;
                action(isVisible);
            });
        }

        public void CssPropertyValue(ElementProxy element, string propertyName, Action<bool, string> action)
        {
            this.Act(CommandType.Action, () =>
            {
                var propertyValue = ((IJavaScriptExecutor)this._webDriver).ExecuteScript(string.Format("return fluentjQuery(\"{0}\").css(\"{1}\")", element.Element.Selector, propertyName));
                if (propertyValue == null)
                    action(false, string.Empty);
                else
                    action(true, propertyValue.ToString());
            });
        }

        public void Dispose()
        {
            try
            {
                this._webDriver.Manage().Cookies.DeleteAllCookies();
                this._webDriver.Quit();
                this._webDriver.Dispose();
            }
            catch (Exception) { }
        }
    }
}
