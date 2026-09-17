using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace StairDrawer.Interaction
{
    internal sealed class StairPlacement
    {
        public StairPlacement(Point3d origin, Vector3d xAxis)
        {
            Origin = origin;
            XAxis = xAxis;
        }

        public Point3d Origin { get; }

        public Vector3d XAxis { get; }
    }

    internal static class StairInteraction
    {
        public static bool TryGetPlacement(Editor editor, out StairPlacement placement)
        {
            placement = null;
            PromptPointResult originResult = editor.GetPoint(new PromptPointOptions("\nChon diem chen (WCS XY): "));
            if (originResult.Status != PromptStatus.OK)
            {
                return false;
            }

            Point3d origin = originResult.Value;
            var directionOptions = new PromptPointOptions("\nChon diem xac dinh huong truc X <WCS +X>: ")
            {
                AllowNone = true,
                BasePoint = origin,
                UseBasePoint = true,
                UseDashedLine = true
            };

            PromptPointResult directionResult = editor.GetPoint(directionOptions);
            if (directionResult.Status == PromptStatus.Cancel)
            {
                return false;
            }

            Vector3d xAxis = Vector3d.XAxis;
            if (directionResult.Status == PromptStatus.OK)
            {
                Vector3d direction = directionResult.Value - origin;
                xAxis = new Vector3d(direction.X, direction.Y, 0.0);
                if (xAxis.Length <= 1e-9)
                {
                    editor.WriteMessage("\nHuong ve phai co do dai khac 0 tren mat phang WCS XY.");
                    return false;
                }

                xAxis = xAxis.GetNormal();
            }
            else if (directionResult.Status != PromptStatus.None)
            {
                return false;
            }

            placement = new StairPlacement(new Point3d(origin.X, origin.Y, origin.Z), xAxis);
            return true;
        }
    }
}
