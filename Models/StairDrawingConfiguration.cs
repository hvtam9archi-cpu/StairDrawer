using System;

namespace StairDrawer.Models
{
    internal enum StairDrawingMode
    {
        Plan,
        Section
    }

    internal sealed class StairDrawingConfiguration
    {
        private StairDrawingConfiguration(
            StairDrawingMode drawingMode,
            StairPlanParameters planParameters,
            StairSectionParameters sectionParameters)
        {
            DrawingMode = drawingMode;
            PlanParameters = planParameters;
            SectionParameters = sectionParameters;
        }

        public StairDrawingMode DrawingMode { get; }

        public StairPlanParameters PlanParameters { get; }

        public StairSectionParameters SectionParameters { get; }

        public static StairDrawingConfiguration CreatePlan(StairPlanParameters parameters)
        {
            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            return new StairDrawingConfiguration(StairDrawingMode.Plan, parameters, null);
        }

        public static StairDrawingConfiguration CreateSection(StairSectionParameters parameters)
        {
            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            return new StairDrawingConfiguration(StairDrawingMode.Section, null, parameters);
        }
    }
}
