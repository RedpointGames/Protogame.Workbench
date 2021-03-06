﻿#if PLATFORM_WINDOWS
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Web;
using System.Windows.Forms;

namespace Horizon.Framework
{
    public class WindowsUIManager : IUIManager
    {
        private Form _activeForm;
        
        private readonly RuntimeServer _runtimeServer;
        private readonly IConfiguration _configuration;

        private readonly IAppHandlerManager _appHandlerManager;

        private readonly IBrandingEngine _brandingEngine;

        public static bool IsSubmitting;

        public static object SubmissionLock = new object();

        public WindowsUIManager(
            RuntimeServer server,
            IConfiguration configuration,
            IAppHandlerManager appHandlerManager,
            IBrandingEngine brandingEngine)
        {
            _runtimeServer = server;
            _configuration = configuration;
            _appHandlerManager = appHandlerManager;
            _brandingEngine = brandingEngine;
        }

        public void Run()
        {
            BrowserEmulation.EnableLatestIE();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var displaySize = Screen.PrimaryScreen.Bounds;

            int width, height;
            bool allowResizing;
            _configuration.GetWindowConfiguration(displaySize.Width, displaySize.Height, out width, out height, out allowResizing);

            var form = new Form();
            _activeForm = form;
            form.Text = _brandingEngine.ProductName;
            form.Icon = _brandingEngine.WindowsIcon;
            form.Width = width;
            form.Height = height;
            form.FormBorderStyle = allowResizing ? FormBorderStyle.Sizable : FormBorderStyle.FixedDialog;
            form.MaximizeBox = allowResizing;
            form.StartPosition = FormStartPosition.CenterScreen;

            var webBrowser = new WebBrowser();
            webBrowser.Dock = DockStyle.Fill;
            webBrowser.ObjectForScripting = new ScriptInterface();
            form.Controls.Add(webBrowser);

            _runtimeServer.RegisterRuntimeInjector(x =>
                SafeInvoke(form, () => ExecuteScript(webBrowser, x)));

            if (!Debugger.IsAttached)
            {
                webBrowser.ScriptErrorsSuppressed = true;
            }

            webBrowser.Navigate(_runtimeServer.BaseUri);

            webBrowser.Navigating += (o, a) => ExecuteAndCatch(
                () =>
                {
                    var uri = a.Url;

                    if (uri.Scheme != "app")
                    {
                        return;
                    }

                    _appHandlerManager.Handle(uri.AbsolutePath, HttpUtility.ParseQueryString(uri.Query));

                    a.Cancel = true;
                });

            webBrowser.Navigated += (sender, args) => ExecuteAndCatch(() =>
            {
                _runtimeServer.Flush();
            });

            form.Show();
            form.FormClosing += (o, a) =>
            {
                if (!IsSubmitting)
                {
                    Application.Exit();
                }
            };

            Application.Run();
        }

        [ComVisible(true)]
        public class ScriptInterface
        {
            public void ReportError(string errorMessage, string url, int lineNumber)
            {
                Console.WriteLine(url + ":" + lineNumber + ": " + errorMessage);
            }

            public void Log(string errorMessage)
            {
                Console.WriteLine(errorMessage);
            }
        }

        public void Quit()
        {
            Application.Exit();
        }

        private void ExecuteAndCatch(Action eventHandler)
        {
            if (Debugger.IsAttached)
            {
                eventHandler();
            }
            else
            {
                try
                {
                    eventHandler();
                }
                catch (Exception e)
                {
                    lock (SubmissionLock)
                    {
                        if (IsSubmitting)
                        {
                            return;
                        }

                        IsSubmitting = true;
                    }

                    if (_activeForm != null)
                    {
                        _activeForm.Close();
                    }

                    Application.DoEvents();

                    Application.Exit();
                }
            }
        }

        private void SafeInvoke(Form form, Action action)
        {
            try
            {
                form.Invoke(action);
            }
            catch (InvalidOperationException)
            {
            }
            catch (NullReferenceException)
            {
            }
        }

        [ComImport, ComVisible(true), Guid(@"3050f28b-98b5-11cf-bb82-00aa00bdce0b")]
        [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
        [TypeLibType(TypeLibTypeFlags.FDispatchable)]
        public interface IHTMLScriptElement
        {
            [DispId(1006)]
            string text { set; [return: MarshalAs(UnmanagedType.BStr)] get; }
        }

        private static int m_NameCount = 0;

        private static void ExecuteScript(WebBrowser browser, string script)
        {
            var uniqueName = "call" + m_NameCount++;
            var doc = browser.Document;
            HtmlElement head = null;
            if (doc != null)
            {
                var tags = doc.GetElementsByTagName("head");
                if (tags.Count == 1)
                {
                    head = tags[0];
                }
            };
            if (head == null)
            {
                Console.WriteLine("WARNING: Can't execute script yet; document not ready!");
                return;
            }
            var scriptEl = browser.Document.CreateElement("script");
            var element = (IHTMLScriptElement)scriptEl.DomElement;
            element.text = "function " + uniqueName + "() { " + script + " }";
            head.AppendChild(scriptEl);
            browser.Document.InvokeScript(uniqueName);
        }
    }
}
#endif