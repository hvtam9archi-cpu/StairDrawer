using Autodesk.AutoCAD.Runtime;

[assembly: CommandClass(typeof(StairDrawer.Commands.StairCommands))]
[assembly: ExtensionApplication(typeof(StairDrawer.Initialization.StairExtensionApplication))]
