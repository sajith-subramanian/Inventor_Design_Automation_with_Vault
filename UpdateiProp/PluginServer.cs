using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Inventor;

namespace UpdateiProp
{
    [Guid("A8CF6DF3-11FF-40A7-86E4-B80ECBF8D5E8")]
    public class PluginServer : ApplicationAddInServer
    {
        public PluginServer()
        {
        }

        // Inventor application object.
        InventorServer m_inventorServer;
        SampleAutomation m_automation;

        public dynamic Automation
        {
            get
            {
                return m_automation;
            }
        }

        public void Activate(ApplicationAddInSite AddInSiteObject, bool FirstTime)
        {
            Trace.TraceInformation("Add Param Plugin: initializing... ");

            // Initialize AddIn members.
            m_inventorServer = AddInSiteObject.InventorServer;
            m_automation = new SampleAutomation(m_inventorServer);
        }

        public void Deactivate()
        {
            Trace.TraceInformation("Add Param Plugin: deactivating... ");

            // Release objects.
            Marshal.ReleaseComObject(m_inventorServer);
            m_inventorServer = null;

            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        public void ExecuteCommand(int CommandID)
        {
            // obsolete
        }
    }
}
