using System;
using System.Collections.Generic;
using Trace = System.Diagnostics.Trace;
using System.IO;
using System.Linq;
using System.Reflection;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace StairDrawer.Services
{
    // Use the installed Callout public API; stair extraction also works without that optional plugin.
    internal static class StairCalloutBridge
    {
        public static bool TryCreateLeader(IReadOnlyList<Point3d> boundary, Matrix3d transform,
            double diameter, Database database, out List<Entity> entities)
        {
            entities = null;
            try
            {
                Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Callout");
                if (assembly == null)
                {
                    string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "Autodesk", "ApplicationPlugins", "Callout.bundle", "Contents", "Callout.dll");
                    if (!File.Exists(path)) return false;
                    assembly = Assembly.LoadFrom(path);
                }

                MethodInfo method = assembly.GetType("Callout.Services.CalloutGeometryService")?.GetMethod("CreateSmartLeader");
                if (method == null) return false;
                entities = (List<Entity>)method.Invoke(null, new object[] { boundary, transform, diameter, database });
                return true;
            }
            catch (Exception error)
            {
                Trace.TraceWarning("Callout leader API unavailable: {0}", error.GetBaseException().Message);
                return false;
            }
        }
    }
}
