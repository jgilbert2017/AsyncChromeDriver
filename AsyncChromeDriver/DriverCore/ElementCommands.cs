﻿// Copyright (c) Oleg Zudov. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
// This file is based on or incorporates material from the Chromium Projects, licensed under the BSD-style license. More info in THIRD-PARTY-NOTICES file.

using System;
using System.Threading.Tasks;
using BaristaLabs.ChromeDevTools.Runtime.Runtime;
using Zu.WebBrowser.BasicTypes;

namespace Zu.Chrome.DriverCore
{
    public class ElementCommands
    {
        private WebView webView;
        private ElementUtils elementUtils;
        private AsyncChromeDriver asyncChromeDriver;

        public Session Session { get; private set; }

        public ElementCommands(WebView webView, Session session, ElementUtils elementUtils = null, AsyncChromeDriver asyncChromeDriver = null)
        {
            this.webView = webView;
            Session = session;
            this.elementUtils = elementUtils ?? new ElementUtils(webView, Session);
            this.asyncChromeDriver = asyncChromeDriver;
        }

        public string GetElementKey()
        {
            if (Session?.w3c_compliant == true)
                return ElementKeys.ElementKeyW3C;
            else
                return ElementKeys.ElementKey;
        }

        public async Task<string> ClickElement(string elementId)
        {
            await asyncChromeDriver.CheckConnected();
            var tag_name = await elementUtils.GetElementTagName(elementId);
            if (tag_name == "option")
            {
                bool is_toggleable = await elementUtils.IsOptionElementTogglable(elementId);

                if (is_toggleable)
                {
                    await elementUtils.ToggleOptionElement(elementId);
                    return "ToggleOptionElement";
                }
                else
                {
                    await elementUtils.SetOptionElementSelected(elementId);
                    return "SetOptionElementSelected";
                }
            }
            else
            {
                WebPoint location = await elementUtils.GetElementClickableLocation(elementId);
                var res = await webView.DevTools.Session.Input.DispatchMouseEvent(new BaristaLabs.ChromeDevTools.Runtime.Input.DispatchMouseEventCommand
                {
                    Type = ChromeDriverMouse.MovedMouseEventType,
                    Button = ChromeDriverMouse.NoneMouseButton,
                    X = location.X,
                    Y = location.Y,
                    Modifiers = Session.sticky_modifiers,
                    ClickCount = 0
                });
                res = await webView.DevTools.Session.Input.DispatchMouseEvent(new BaristaLabs.ChromeDevTools.Runtime.Input.DispatchMouseEventCommand
                {
                    Type = ChromeDriverMouse.PressedMouseEventType,
                    Button = ChromeDriverMouse.LeftMouseButton,
                    X = location.X,
                    Y = location.Y,
                    Modifiers = Session.sticky_modifiers,
                    ClickCount = 1
                });
                res = await webView.DevTools.Session.Input.DispatchMouseEvent(new BaristaLabs.ChromeDevTools.Runtime.Input.DispatchMouseEventCommand
                {
                    Type = ChromeDriverMouse.ReleasedMouseEventType,
                    Button = ChromeDriverMouse.LeftMouseButton,
                    X = location.X,
                    Y = location.Y,
                    Modifiers = Session.sticky_modifiers,
                    ClickCount = 1
                });
                Session.mouse_position = location;
                //await new ChromeDriverMouse(webView).Click(location);
                return "Click";
            }

        }
        public async Task<WebPoint> GetElementLocation(string elementId)
        {
            var res = await webView.CallFunction(atoms.GET_LOCATION, $"{{\"{GetElementKey()}\":\"{elementId}\"}}");
            return ResultValueConverter.ToWebPoint(res?.Result?.Value);
        }

        public async Task<EvaluateCommandResponse> FocusElement(string elementId)
        {
            var res = await webView.CallFunction(focus_js.JsSource, $"{{\"{GetElementKey()}\":\"{elementId}\"}}");
            return res;
        }

        public async Task<string> SendKeysToElement(string elementId, string keys)
        {
            var isInput = await elementUtils.IsElementAttributeEqualToIgnoreCase(elementId, "tagName", "input");
            var isFile = await elementUtils.IsElementAttributeEqualToIgnoreCase(elementId, "type", "file");
            if (isInput && isFile)
            {
                bool multiple = await elementUtils.IsElementAttributeEqualToIgnoreCase(elementId, "multiple", "true");
                return webView.SetFileInputFiles(elementId, keys);
            }
            else
            {
                var startTime = DateTime.Now;
                bool isDisplayed = false;
                bool isFocused = false;
                if (Session.implicitWait != default(TimeSpan))
                {
                    while (true)
                    {
                        isDisplayed = await elementUtils.IsElementDisplayed(elementId);
                        if (isDisplayed) break;
                        isFocused = await elementUtils.IsElementFocused(elementId);
                        if (isFocused) break;
                        if (Session.implicitWait == default(TimeSpan)) break;
                        if (DateTime.Now - startTime >= Session.implicitWait)
                        {
                            return null;
                        }
                        await Task.Delay(100);
                    }
                }
                bool isEnabled = await elementUtils.IsElementEnabled(elementId);
                if (!isEnabled) return null;
                if (!isFocused)
                {
                    await FocusElement(elementId);
                }
                //var res = SendKeysOnWindow(keys, true);
                await webView.DispatchKeyEvents(keys);
                return "ok";
            }
        }
    }
}
