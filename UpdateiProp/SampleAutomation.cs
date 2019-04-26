using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Inventor;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Text;
using System.IO;


namespace UpdateiProp
{
    [ComVisible(true)]
    public class SampleAutomation
    {
        InventorServer m_inventorServer;

        public SampleAutomation(InventorServer inventorServer)
        {
            Trace.TraceInformation("Starting sample plugin.");
            m_inventorServer = inventorServer;
        }

        public void Run(Document doc)
        {

            Trace.TraceInformation("Running with no Args.");
            NameValueMap map = m_inventorServer.TransientObjects.CreateNameValueMap();
            RunWithArguments(doc, map);

        }

        public void RunWithArguments(Document doc, NameValueMap map)
        {
            try
            {
                StringBuilder traceInfo = new StringBuilder("RunWithArguments called with ");
                traceInfo.Append(doc.DisplayName);
                Trace.TraceInformation(map.Count.ToString());
                // values in map are keyed on _1, _2, etc
                for (int i = 0; i < map.Count; i++)
                {
                    traceInfo.Append(" and ");
                    traceInfo.Append(map.Value["_" + (i + 1)]);
                }
                Trace.TraceInformation(traceInfo.ToString());

                #region Update iProperty

                Trace.TraceInformation("Updating iProperty.");
                PropertySet invPropSet = doc.PropertySets["Inventor Summary Information"];
                // Get the 'Comments' property and set a value.
                Property invCommentsProperty = invPropSet["Comments"];
                invCommentsProperty.Value = "Comments added using Design Automation";
                doc.Update();
                doc.Save();

               #endregion
            }
            catch(Exception ex)
            { Trace.TraceInformation(ex.Message); }
        }

    }
}
