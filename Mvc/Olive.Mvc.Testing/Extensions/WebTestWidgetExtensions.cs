﻿namespace Olive.Mvc
{
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Html;
    using Microsoft.AspNetCore.Mvc.Rendering;
    using System;
    using System.Linq;
    using System.Text;

    public static class WebTestWidgetExtensions
    {
        internal static bool IsUITestExecutionMode;

        public static HtmlString WebTestWidget(this IHtmlHelper @this)
        {
            if (!Context.Current.Environment().IsDevelopment()) return null;

            if (IsUITestExecutionMode)
                @this.RunJavascript("loadModule('olive/plugins/sanityAdapter', m => m.default.enable());");

            var commands = Context.Current.GetServices<IDevCommand>().Where(x => x.IsEnabled()).ToArray();

            var r = new StringBuilder();

            r.Append("<div class='webtest-commands' ");

            var height = commands.Count() * 30;

            r.Append("style='position: fixed; left: 47%; bottom: 0; text-align: center; width: 150px;");
            r.Append($"margin-bottom:-{height}px; ");
            r.Append("transition: margin-bottom 0.25s ease; background: #2ea8eb; ");
            r.Append("color: #fff; font-size: 12px; font-family:Arial;' ");
            r.Append($"onmouseover='this.style.marginBottom=\"0\"' ");
            r.Append($"onmouseout='this.style.marginBottom=\"-{height}px\"'>");

            r.AppendLine(@"<div style='width: 100%; background-color:#1b648d; padding: 3px 0;font-size: 13px; font-weight: 700;'>Test...</div>");

            foreach (var command in commands)
            {
                if (command.Title.IsEmpty()) continue;
                var url = "/cmd/" + command.Name;
                r.AppendLine($@"<div style='width: 100%; height:30px; box-sizing:border-box; padding: 4px 0;'>
                                  <a href='{url}' style='color: #fff;'>{command.Title.HtmlEncode()}</a>
                                </div>");
            }

            r.AppendLine("</div>");

            return r.ToString().Raw();
        }
    }
}