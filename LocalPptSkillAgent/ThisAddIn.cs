#if VSTO
using LocalPptSkillAgent.Ribbon;
using LocalPptSkillAgent.UI;
using Microsoft.Office.Tools;
using Office = Microsoft.Office.Core;

namespace LocalPptSkillAgent
{
    public partial class ThisAddIn
    {
        private CustomTaskPane _taskPane;

        private void ThisAddIn_Startup(object sender, System.EventArgs e)
        {
            // 注意: DPIコンテキストをスレッドで切り替えてからHWNDを作ると、PowerPointのペインへの
            // 親子付け(SetParent)で入力がハングするWindows既知問題を踏むため、コンテキストは触らない。
            var control = new MainTaskPaneControl(Application);
            _taskPane = CustomTaskPanes.Add(control, "LocalPptSkillAgent");
            _taskPane.DockPosition = Office.MsoCTPDockPosition.msoCTPDockPositionRight;
            _taskPane.Width = Utils.DpiUtils.Scale(control, 430);
            _taskPane.Visible = false;
        }

        private void ThisAddIn_Shutdown(object sender, System.EventArgs e) { }
        public void ShowPanel() { if (_taskPane != null) _taskPane.Visible = true; }
        protected override Office.IRibbonExtensibility CreateRibbonExtensibilityObject() => new LocalPptRibbon(ShowPanel);

        #region VSTO generated wiring
        private void InternalStartup()
        {
            Startup += ThisAddIn_Startup;
            Shutdown += ThisAddIn_Shutdown;
        }
        #endregion
    }
}
#else
namespace LocalPptSkillAgent
{
    // The VSTO host half is compiled by the PowerPoint VSTO project configuration.
    public sealed class ThisAddIn { }
}
#endif
