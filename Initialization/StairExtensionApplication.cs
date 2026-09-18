using System;
using System.Diagnostics;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using AutoCADApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using AutoCADException = Autodesk.AutoCAD.Runtime.Exception;

namespace StairDrawer.Initialization
{
    public sealed class StairExtensionApplication : IExtensionApplication
    {
        private bool _idleSubscribed;

        public void Initialize()
        {
            try
            {
                if (_idleSubscribed)
                {
                    return;
                }

                AutoCADApplication.Idle += OnIdle;
                _idleSubscribed = true;
            }
            catch (AutoCADException exception)
            {
                Trace.TraceError("StairDrawer initialization failed: {0}", exception);
            }
            catch (System.Exception exception)
            {
                Trace.TraceError("StairDrawer initialization failed: {0}", exception);
            }
        }

        public void Terminate()
        {
            try
            {
                UnsubscribeFromIdle();
            }
            catch (AutoCADException exception)
            {
                Trace.TraceError("StairDrawer termination failed: {0}", exception);
            }
            catch (System.Exception exception)
            {
                Trace.TraceError("StairDrawer termination failed: {0}", exception);
            }
        }

        private void OnIdle(object sender, EventArgs eventArgs)
        {
            try
            {
                Document document = AutoCADApplication.DocumentManager.MdiActiveDocument;
                if (document == null)
                {
                    return;
                }

                UnsubscribeFromIdle();
                document.Editor.WriteMessage(
                    "\nStairDrawer loaded. Command: STAIRDRAW.\n");
            }
            catch (AutoCADException exception)
            {
                Trace.TraceError("StairDrawer idle initialization failed: {0}", exception);
                TryUnsubscribeAfterFailure();
            }
            catch (System.Exception exception)
            {
                Trace.TraceError("StairDrawer idle initialization failed: {0}", exception);
                TryUnsubscribeAfterFailure();
            }
        }

        private void UnsubscribeFromIdle()
        {
            if (!_idleSubscribed)
            {
                return;
            }

            AutoCADApplication.Idle -= OnIdle;
            _idleSubscribed = false;
        }

        private void TryUnsubscribeAfterFailure()
        {
            try
            {
                UnsubscribeFromIdle();
            }
            catch (System.Exception exception)
            {
                Trace.TraceError("StairDrawer could not unsubscribe Idle after failure: {0}", exception);
            }
        }
    }
}
