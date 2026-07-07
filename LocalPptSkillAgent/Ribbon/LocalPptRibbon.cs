using System;
using System.Runtime.InteropServices;
using Office = Microsoft.Office.Core;

namespace LocalPptSkillAgent.Ribbon
{
    [ComVisible(true)]
    public class LocalPptRibbon : Office.IRibbonExtensibility
    {
        private readonly Action _openPanel;
        public LocalPptRibbon(Action openPanel) { _openPanel = openPanel; }

        public string GetCustomUI(string ribbonId) => @"
<customUI xmlns='http://schemas.microsoft.com/office/2009/07/customui'>
  <ribbon>
    <tabs>
      <tab id='LocalAiTab' label='Local AI'>
        <group id='LocalPptSkillAgentGroup' label='LocalPptSkillAgent'>
          <button id='OpenLocalPptSkillAgentPanel' label='Open Panel' size='large' imageMso='ResearchPane' onAction='OnOpenPanel'/>
        </group>
      </tab>
    </tabs>
  </ribbon>
</customUI>";

        public void OnOpenPanel(Office.IRibbonControl control) => _openPanel?.Invoke();
    }
}
