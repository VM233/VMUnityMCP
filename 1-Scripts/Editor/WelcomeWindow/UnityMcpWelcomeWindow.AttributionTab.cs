using UnityEngine.UIElements;

namespace UnityMCP.Editor.Welcome
{
    /// <summary>
    /// License and project-lineage attribution retained by the independent
    /// VM Unity MCP distribution.
    /// </summary>
    public sealed partial class UnityMcpWelcomeWindow
    {
        private void BuildAttributionTab()
        {
            AddHeading(_scroll, "Project lineage");
            AddBody(_scroll,
                "VM Unity MCP is an independently maintained continuation of AnkleBreaker " +
                "Studio's Unity MCP Plugin. The repositories are now separate, while the " +
                "original copyright and license terms remain in force.");

            VisualElement attribution =
                AddBox(_scroll, "Powered by AnkleBreaker MCP", UnityMcpWelcomeIcon.STAR);
            AddBoxBody(attribution,
                "Original project by <b>AnkleBreaker Consulting &amp; AnkleBreaker Studio</b>. " +
                "See the original source and the bundled AnkleBreaker Open License for the " +
                "required notices and distribution terms.");
            attribution.Add(MakeAccentButton("Open Original Project",
                UnityMcpWelcomeIcon.WEB, () => OpenUrl(ORIGINAL_PROJECT_URL)));

            VisualElement license =
                AddBox(_scroll, "License", UnityMcpWelcomeIcon.WEB);
            AddBoxBody(license,
                "VM Unity MCP keeps the original license unmodified and publishes VM233's " +
                "subsequent changes under those same terms.");
            license.Add(MakeAccentButton("Read License",
                UnityMcpWelcomeIcon.WEB, () => OpenUrl(LICENSE_URL)));
        }
    }
}
