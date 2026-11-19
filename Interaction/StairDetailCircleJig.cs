using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;

namespace StairDrawer.Interaction
{
    internal sealed class StairDetailCircleJig : DrawJig
    {
        private readonly double _radius;
        private readonly double? _elevation;
        private readonly Point3d? _source;
        private readonly double _sourceRadius;
        public Point3d Position { get; private set; }

        public StairDetailCircleJig(double radius, double? elevation, Point3d? source = null, double sourceRadius = 0)
        {
            _radius = radius; _elevation = elevation; _source = source; _sourceRadius = sourceRadius;
            Position = source ?? Point3d.Origin;
        }

        protected override SamplerStatus Sampler(JigPrompts prompts)
        {
            var options = new JigPromptPointOptions(_source.HasValue
                ? "\nChọn vị trí đặt tâm chi tiết phóng lớn: " : "\nChọn tâm vùng trích trên bậc thang: ")
                { UserInputControls = UserInputControls.Accept3dCoordinates };
            PromptPointResult result = prompts.AcquirePoint(options);
            if (result.Status != PromptStatus.OK) return SamplerStatus.Cancel;
            var point = new Point3d(result.Value.X, result.Value.Y, _elevation ?? result.Value.Z);
            if (Position.DistanceTo(point) < 1e-7) return SamplerStatus.NoChange;
            Position = point; return SamplerStatus.OK;
        }

        protected override bool WorldDraw(WorldDraw draw)
        {
            draw.SubEntityTraits.Color = 8;
            if (_source.HasValue) draw.Geometry.Circle(_source.Value, _sourceRadius, Vector3d.ZAxis);
            return draw.Geometry.Circle(Position, _radius, Vector3d.ZAxis);
        }
    }
}
