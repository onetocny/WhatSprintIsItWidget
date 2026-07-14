using System;
using System.Collections.Concurrent;
using System.IO;
using Microsoft.Windows.Widgets;
using Microsoft.Windows.Widgets.Providers;

namespace WhatSprintIsItWidget
{
    /// <summary>
    /// Widget provider that renders the current Azure DevOps sprint and week,
    /// mirroring the layout of https://whatsprintis.it/.
    /// The Widgets host calls into this class through the IWidgetProvider interface.
    /// </summary>
    [System.Runtime.InteropServices.ComVisible(true)]
    [System.Runtime.InteropServices.Guid(WidgetProvider.ClsidString)]
    public sealed class WidgetProvider : IWidgetProvider
    {
        // Must match the CLSID declared in Package.appxmanifest and registered in Program.cs.
        public const string ClsidString = "6E3E1B58-1D8C-4F2A-9C4E-6A5B2F0E9D11";

        // Tracks the widgets currently pinned by the user so we can refresh them.
        private static readonly ConcurrentDictionary<string, WidgetState> RunningWidgets = new();

        private sealed class WidgetState
        {
            public bool IsActive { get; set; }
            public WidgetSize Size { get; set; } = WidgetSize.Medium;
        }

        public void CreateWidget(WidgetContext widgetContext)
        {
            Log($"CreateWidget id={widgetContext?.Id} def={widgetContext?.DefinitionId} size={widgetContext?.Size}");
            try
            {
                string widgetId = widgetContext!.Id;
                RunningWidgets[widgetId] = new WidgetState { IsActive = true, Size = widgetContext.Size };
                UpdateWidget(widgetId);
            }
            catch (Exception ex)
            {
                Log($"CreateWidget FAILED: {ex}");
            }
        }

        public void DeleteWidget(string widgetId, string customState)
        {
            Log($"DeleteWidget id={widgetId}");
            RunningWidgets.TryRemove(widgetId, out _);
        }

        public void OnActionInvoked(WidgetActionInvokedArgs actionInvokedArgs)
        {
            try
            {
                UpdateWidget(actionInvokedArgs.WidgetContext.Id);
            }
            catch (Exception ex)
            {
                Log($"OnActionInvoked FAILED: {ex}");
            }
        }

        public void OnWidgetContextChanged(WidgetContextChangedArgs contextChangedArgs)
        {
            try
            {
                WidgetContext ctx = contextChangedArgs.WidgetContext;
                if (RunningWidgets.TryGetValue(ctx.Id, out WidgetState? state))
                {
                    state.Size = ctx.Size;
                }
                Log($"OnWidgetContextChanged id={ctx.Id} size={ctx.Size}");
                UpdateWidget(ctx.Id);
            }
            catch (Exception ex)
            {
                Log($"OnWidgetContextChanged FAILED: {ex}");
            }
        }

        public void Activate(WidgetContext widgetContext)
        {
            Log($"Activate id={widgetContext?.Id} size={widgetContext?.Size}");
            try
            {
                if (RunningWidgets.TryGetValue(widgetContext!.Id, out WidgetState? state))
                {
                    state.IsActive = true;
                    state.Size = widgetContext.Size;
                }
                else
                {
                    RunningWidgets[widgetContext.Id] = new WidgetState { IsActive = true, Size = widgetContext.Size };
                }

                UpdateWidget(widgetContext.Id);
            }
            catch (Exception ex)
            {
                Log($"Activate FAILED: {ex}");
            }
        }

        public void Deactivate(string widgetId)
        {
            Log($"Deactivate id={widgetId}");
            if (RunningWidgets.TryGetValue(widgetId, out WidgetState? state))
            {
                state.IsActive = false;
            }
        }

        private static void UpdateWidget(string widgetId)
        {
            SprintCalculator.SprintInfo info = SprintCalculator.GetCurrent();
            WidgetSize size = RunningWidgets.TryGetValue(widgetId, out WidgetState? state)
                ? state.Size
                : WidgetSize.Medium;

            string card = BuildCard(info.Sprint, info.Week, size);

            var options = new WidgetUpdateRequestOptions(widgetId)
            {
                Template = card,
                Data = "{}",
                CustomState = widgetId,
            };

            WidgetManager.GetDefault().UpdateWidget(options);
            Log($"UpdateWidget id={widgetId} size={size} sprint={info.Sprint} week={info.Week} -> OK");
        }

        // Builds an Adaptive Card that shows "sprint N week M" as a single centered
        // line, matching https://whatsprintis.it/. A RichTextBlock is used so the small
        // lighter labels and the large numbers share one text baseline (separate columns
        // would bottom-align the text boxes, leaving the taller numbers a few pixels
        // higher than the labels). Font sizes scale with the widget size.
        private static string BuildCard(int sprint, int week, WidgetSize size)
        {
            string labelSize;
            string numberSize;
            string gap; // spacing between the sprint and week groups

            switch (size)
            {
                case WidgetSize.Small:
                    labelSize = "Small";
                    numberSize = "ExtraLarge";
                    gap = "  ";
                    break;
                case WidgetSize.Large:
                    labelSize = "Medium";
                    numberSize = "ExtraLarge";
                    gap = "      ";
                    break;
                case WidgetSize.Medium:
                default:
                    labelSize = "Small";
                    numberSize = "ExtraLarge";
                    gap = "    ";
                    break;
            }

            string Label(string text) =>
                $@"{{ ""type"": ""TextRun"", ""text"": ""{text}"", ""size"": ""{labelSize}"", ""weight"": ""Lighter"", ""isSubtle"": true }}";

            string Number(string text) =>
                $@"{{ ""type"": ""TextRun"", ""text"": ""{text}"", ""size"": ""{numberSize}"", ""weight"": ""Default"" }}";

            string inlines = string.Join(",\n        ", new[]
            {
                Label("sprint "),
                Number(sprint.ToString()),
                Label(gap + "week "),
                Number(week.ToString()),
            });

            return $@"{{
  ""$schema"": ""http://adaptivecards.io/schemas/adaptive-card.json"",
  ""type"": ""AdaptiveCard"",
  ""version"": ""1.5"",
  ""verticalContentAlignment"": ""Center"",
  ""body"": [
    {{
      ""type"": ""RichTextBlock"",
      ""horizontalAlignment"": ""Center"",
      ""inlines"": [
        {inlines}
      ]
    }}
  ]
}}";
        }

        private static void Log(string message)
        {
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "WhatSprintIsItWidget");
                Directory.CreateDirectory(dir);
                File.AppendAllText(
                    Path.Combine(dir, "provider.log"),
                    $"{DateTime.Now:O} [{Environment.ProcessId}] {message}{Environment.NewLine}");
            }
            catch
            {
                // Diagnostics only.
            }
        }
    }
}