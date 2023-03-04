using System;
using System.Collections;
using System.Collections.Generic;

using Rhino;
using Rhino.Geometry;


using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;


// <Custom "using" statements>
using StructuralDesignKitLibrary;
using System.Linq;
using StructuralDesignKitLibrary.Materials;
using StructuralDesignKitLibrary.CrossSections;

using System.IO;
using System.Data;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;
using System.Runtime.InteropServices;

using Rhino.DocObjects;
using Rhino.Collections;
using GH_IO;
using GH_IO.Serialization;


// </Custom "using" statements>


#region padding (this ensures the line number of this file match with those in the code editor of the C# Script component
















#endregion

public partial class MyExternalScript : GH_ScriptInstance
{
    #region Do_not_modify_this_region
    private void Print(string text) { }
    private void Print(string format, params object[] args) { }
    private void Reflect(object obj) { }
    private void Reflect(object obj, string methodName) { }
    public override void InvokeRunScript(IGH_Component owner, object rhinoDocument, int iteration, List<object> inputs, IGH_DataAccess DA) { }
    public RhinoDoc RhinoDocument;
    public GH_Document GrasshopperDocument;
    public IGH_Component Component;
    public int Iteration;
    #endregion


    private void RunScript(Polyline iOutline, double iJoistSpacing, double AreaLoad, List<int> iMaxDeflections, double iMaxfloorHeight, int iResultCount, ref object A, ref object B, ref object C)
    {
        // <Custom code>

        //Get single sides of the outline
        //Currently the outline has to be a rectangle
        var segmentsArray = iOutline.GetSegments();
        List<Line> segments = new List<Line>();
        segments.AddRange(segmentsArray);

        //Order sides by length
        var segmentSorted = segments.OrderBy(p => p.Length).ToList();

        //Get side lengths 
        double shortSideLength = segmentSorted.First().Length;
        double longSideLength = segmentSorted.Last().Length;

        //Create lists of sides
        List<Line> shortSides = new List<Line>() { segmentSorted[0], segmentSorted[1] };
        List<Line> longSides = new List<Line>() { segmentSorted[2], segmentSorted[3] };




        //Define library of cross sections to use
        List<int> widths = new List<int>() { 60, 80, 115, 135, 160, 200, 240 };

        List<CrossSectionCatalog> crossSectionCatalogs = new List<CrossSectionCatalog>();

        foreach (int width in widths)
        {
            crossSectionCatalogs.Add(new CrossSectionCatalog(width, new StructuralDesignKitLibrary.Materials.MaterialTimberGlulam(MaterialTimberGlulam.Grades.GL24h), 120, 6 * width, 40));
        }



        //Define boundary conditions
        double minJoistLength = 2000;//[mm]
        if (iMaxDeflections.Count != 2) throw new Exception("The input IMaxDEflections must have 2 entries");
        int maxDeflectionJoists = iMaxDeflections[0];
        int maxDeflectionBeams = iMaxDeflections[1];


        //Initial calculation

        List<ConfigurationResult> results = new List<ConfigurationResult>();

        double span = shortSideLength;
        double Kmod = 0.9;

        int nbJoist = (Int32)Math.Ceiling(longSideLength / (iJoistSpacing / 1000));
        double LinearLoad = (AreaLoad * iJoistSpacing * 0.001); //[KN/m]
        var CrossSections = ComputeSuitableCrossSections(crossSectionCatalogs, LinearLoad, span, Kmod, maxDeflectionJoists, iMaxfloorHeight);


        results.Add(new ConfigurationResult(CrossSections, null, 0, nbJoist, shortSides[0], longSides[0]));


        //With subdivision of the layout
        int nbDivision = (Int16)Math.Ceiling(longSideLength / shortSideLength);


        while (longSideLength / nbDivision > minJoistLength / 1000)
        {
            //Define Cross Section Joists
            double JoistSpan = longSideLength / (double)nbDivision;
            double BeamSpan = span;


            var CSJoists = ComputeSuitableCrossSections(crossSectionCatalogs, LinearLoad, JoistSpan, Kmod, maxDeflectionJoists, iMaxfloorHeight);


            //Define Cross Section Beams
            double LinearLoadBeams = LinearLoad;
            List<CrossSectionRectangular> CSBeams = new List<CrossSectionRectangular>();
            if (CSJoists.Count > 0)
            {
                LinearLoadBeams = (AreaLoad + CSJoists[0].B / 1000 * CSJoists[0].H / 1000 * CSJoists[0].Material.Density / 100 / iJoistSpacing / 1000) * longSideLength / nbDivision;
                CSBeams = ComputeSuitableCrossSections(crossSectionCatalogs, LinearLoadBeams, BeamSpan, Kmod, maxDeflectionBeams, iMaxfloorHeight);
            }

            if (CSJoists.Count > 0 && CSBeams.Count > 0)
            {
                nbJoist = (Int32)Math.Ceiling(shortSideLength / (iJoistSpacing / 1000));
                results.Add(new ConfigurationResult(CSJoists, CSBeams, (nbDivision + 1), nbJoist, longSides[0], shortSides[0]));
            }
            //Print("Joists:");
            //foreach (var CS in CSJoists)
            //{
            //    Print(string.Format("{0}x{1}mm", CS.B, CS.H));
            //}

            //Print("Beams:");
            //foreach (var CS in CSBeams)
            //{
            //    Print(string.Format("{0}x{1}mm", CS.B, CS.H));
            //}
            //Print("\n\n");

            nbDivision += 1; ;
        }


        //Sort the results per the given Criteria

        var sortedResults = results.OrderBy(p => p.MinVolume).ToList();







        //Display results

        //Display Joists
        List<Line> GlobalJoists = new List<Line>();
        List<Line> GlobalBeams = new List<Line>();
        List<Brep> Volumes = new List<Brep>();
        List<Point3d> BaseTextPoints = new List<Point3d>();
        List<string> texts = new List<string>();
        List<double> textSizes = new List<double>();



        int count = 0;
        if (iResultCount > count) count = iResultCount - 1;
        if (count > sortedResults.Count) count = sortedResults.Count - 1;

        for (int i = 0; i <= count; i++)
        {

            var currentResult = sortedResults[i];

            List<Line> joists = new List<Line>();
            List<Line> Beams = new List<Line>();


            Line shortSide = new Line(shortSides[0].From, shortSides[0].To);
            Line longSide = new Line(longSides[0].From, longSides[0].To);
            
            Vector3d VectorSortSide = new Vector3d(shortSides[0].From- shortSides[0].To);

            VectorSortSide *= (i * 1.2) ;

            Transform transform =  Transform.Translation(VectorSortSide);
            


            Line JoistDirection = currentResult.JoistsDirection;


            if (currentResult.JoistsDirection.Direction == shortSides[0].Direction)
            {
                //Divide long side
                joists = GenerateBeamAxes(JoistDirection, longSide, currentResult.JoistsCount);
            }
            else
            {
                //Divide short side
                joists = GenerateBeamAxes(JoistDirection, shortSide, currentResult.JoistsCount);


            }
            foreach (Line axis in joists)
            {
                Brep joist = DrawBeam(axis, currentResult.CrossSectionJoists[0].B, currentResult.CrossSectionJoists[0].H);

                var deepCopy = joist.DuplicateBrep();
                deepCopy.Transform(transform);
                Volumes.Add(deepCopy);
            }


            //Display Beams
            if (currentResult.BeamsCount > 0)
            {
                Line BeamDirection = currentResult.BeamsDirection;

                if (currentResult.BeamsDirection.Direction == shortSides[0].Direction)
                {
                    //Divide long side
                    Beams = GenerateBeamAxes(BeamDirection, longSide, currentResult.BeamsCount);
                }
                else
                {
                    //Divide short side
                    Beams = GenerateBeamAxes(BeamDirection, shortSide, currentResult.JoistsCount);

                }
                foreach (Line axis in Beams)
                {
                    Brep beam = DrawBeam(axis, currentResult.CrossSectionBeams[0].B, currentResult.CrossSectionBeams[0].H);
                    var deepCopy = beam.DuplicateBrep();
                    deepCopy.Transform(transform);
                    Volumes.Add(deepCopy);
                    
                }
            }




            var textVector = VectorSortSide - new Vector3d(shortSides[0].From - shortSides[0].To)*0.5 + new Vector3d(longSides[0].To -longSides[0].From)*0.2;

            Point3d pt = new Point3d(shortSides[0].From);
            Transform pointTransform = Transform.Translation(textVector);
            pt.Transform(pointTransform);
            BaseTextPoints.Add(pt);

            string text = string.Format("Joist {0}x{1}mm\n", currentResult.CrossSectionJoists[0].B, currentResult.CrossSectionJoists[0].H);

            if (currentResult.CrossSectionBeams != null && currentResult.CrossSectionBeams.Count > 0)
            {
                text += string.Format("Beam {0}x{1}mm\n", currentResult.CrossSectionBeams[0].B, currentResult.CrossSectionBeams[0].H);
            }

            text+=string.Format("Volume case = {0:0.00}m³", currentResult.MinVolume.ToString());
            texts.Add(text);
            textSizes.Add(Math.Max(shortSideLength, longSideLength) * 0.4);

        }



        //
        //
        //_size.Add(siz);





        string CSResults = "";

        //foreach (var result in sortedResults)
        //{


        //    if (result.CrossSectionBeams != null && result.CrossSectionBeams.Count > 0)
        //    {
        //        Print(string.Format("Beam {0}x{1}mm\n", result.CrossSectionBeams[0].B, result.CrossSectionBeams[0].H));
        //    }
        //    //Print(string.Format("Min volume Joist= {0:0.##}", result.VolumeJoists.Min().ToString()));
        //    //if (result.VolumeBeams != null && result.VolumeBeams.Count > 0) Print(string.Format("Min volume Beams = {0:0.##}", result.VolumeBeams.Min().ToString()));
        //    Print(string.Format("Min volume case = {0:0.##}", result.MinVolume.ToString()) + "\n\n");
        //}

        _point.AddRange(BaseTextPoints);
        _text.AddRange(texts);
        _size.AddRange(textSizes);






        A = Volumes;
        B = BaseTextPoints;
        B = texts;


        //C = boundingBox.ToBrep();


        // </Custom code>
    }

    // <Custom additional code>


    #region functions


    //Compute bending
    private double ComputeBending(double span, double load)
    {
        return load * Math.Pow(span, 2) / 8;
    }

    //Compute shear
    private double ComputeShear(double span, double load)
    {
        return load * span / 2;
    }


    //Compute deflection
    private double ComputeDeflection(double span, double load, StructuralDesignKitLibrary.CrossSections.CrossSectionRectangular CS)
    {
        return 5 * load * Math.Pow(span, 4) / (384 * CS.Material.E * CS.MomentOfInertia_Y);
    }

    //Compute minimum EI to span the distance given the load - Deflection based
    private int ComputeMinEI(double MaxDeflection, double load, double span)
    {
        return (Int32)(5 * load * Math.Pow(span, 4) / (384 * MaxDeflection) / 1e10);
    }


    //Compute minimum beam height to span the distance given the load - Bending stress based
    private int ComputeMinBeamHeight(double MaxBendingStress, double bendingMoment, double width)
    {
        double doubleHeight = Math.Sqrt(6 * bendingMoment * 1000000 / (width * MaxBendingStress));
        return (Int32)Math.Ceiling(doubleHeight);
    }

    //Compute the minimum cross section area to span the distance given the load - Shear stress based
    private int ComputeMinCrossSectionArea(double MaxShearStress, double shear)
    {
        return (Int32)(1500 * shear / MaxShearStress);
    }



    private int ComputeCSHeight(CrossSectionCatalog CSCat, double span, double LinearLoad, double Kmod, double maxDeflectionJoists, double iMaxfloorHeight)
    {
        double bendingMoment = ComputeBending(span, LinearLoad);
        double ShearForce = ComputeShear(span, LinearLoad);


        int minHeight_Bending = ComputeMinBeamHeight(CSCat.Material.Fmyk * Kmod / 1.3, bendingMoment, CSCat.Width);
        int minArea_shear = ComputeMinCrossSectionArea(CSCat.Material.Fvk * Kmod / 1.3, ShearForce);
        int minEI_Stiffness = ComputeMinEI(span * 1000 / maxDeflectionJoists, LinearLoad, span * 1000);


        List<int> minHeights = new List<int>();
        minHeights.Add(FirstEqualOrAbove(CSCat.Heights, minHeight_Bending));
        minHeights.Add(FirstEqualOrAbove(CSCat.EIs, minEI_Stiffness));
        minHeights.Add(FirstEqualOrAbove(CSCat.Areas, minArea_shear));

        foreach (int height in minHeights)
        {
            if (height < 0) return -1;
        }

        minHeights.Sort();
        int minHeight = CSCat.Heights[minHeights.Last()];

        if (minHeight < iMaxfloorHeight && iMaxfloorHeight > 0 || iMaxfloorHeight < 0) return minHeight;
        else return -1;
    }


    //Binary search algorithm which find the first index returning a value equal or above the target
    public int FirstEqualOrAbove(List<int> array, int target)
    {
        int resultIndex = -1;
        int LeftIndex = 0;
        int RightIndex = array.Count - 1;
        int iteration = 0;


        //Before entering the binary search loop, verify if the value to look for is in the range of the array
        if (array[0] > target)
        {
            resultIndex = LeftIndex;
        }

        else if (array[array.Count - 1] < target)
        {
            resultIndex = -1;
        }

        else
        {
            //Binary Search
            while (RightIndex - LeftIndex >= 1)
            {
                int CurrentIndex = (Int32)Math.Floor((double)(LeftIndex + RightIndex) / 2);
                int CurrentValue = array[CurrentIndex];

                CurrentValue = array[CurrentIndex];

                if (CurrentValue < target) LeftIndex = CurrentIndex;

                else if (CurrentValue > target) RightIndex = CurrentIndex;

                else if (CurrentValue == target)
                {
                    RightIndex = CurrentIndex;
                    LeftIndex = CurrentIndex;
                }

                if (iteration > 100) break;
                iteration++;

            }
            resultIndex = RightIndex;
        }

        return resultIndex;
    }


    private List<CrossSectionRectangular> ComputeSuitableCrossSections(List<CrossSectionCatalog> CSCatalogue, double linearLoad, double span, double Kmod, int maxDeflectionJoists, double iMaxfloorHeight)
    {
        List<CrossSectionRectangular> CrossSections = new List<CrossSectionRectangular>();

        foreach (var CSCat in CSCatalogue)
        {
            //Initial calculation
            double InitialLinearLoad = linearLoad; //[KN/m]
            int initH = ComputeCSHeight(CSCat, span, InitialLinearLoad, Kmod, maxDeflectionJoists, iMaxfloorHeight);

            //Update 
            double UpdatedLinearLoad = InitialLinearLoad + (CSCat.Width * 0.001 * CSCat.Material.RhoMean * 0.01 * initH * 0.001);
            int updatedH = ComputeCSHeight(CSCat, span, UpdatedLinearLoad, Kmod, maxDeflectionJoists, iMaxfloorHeight);

            while (updatedH > initH)
            {
                initH = updatedH;
                UpdatedLinearLoad = InitialLinearLoad + (CSCat.Width * 0.001 * CSCat.Material.RhoMean * 0.01 * initH * 0.001);

                updatedH = ComputeCSHeight(CSCat, span, UpdatedLinearLoad, Kmod, maxDeflectionJoists, iMaxfloorHeight);
            }

            if (initH > 0 && updatedH > 0) CrossSections.Add(new CrossSectionRectangular(CSCat.Width, updatedH, CSCat.Material));

        }

        return CrossSections;
    }


    private List<Line> GenerateBeamAxes(Line lineToCopy, Line LineToDivide, int numberOfElement)
    {
        List<Line> BeamAxes = new List<Line>();
        var divideParam = new LineCurve(LineToDivide).DivideByCount(numberOfElement - 1, true);
        var divisionPoints = new List<Point3d>();

        foreach (double param in divideParam)
        {
            divisionPoints.Add(LineToDivide.PointAtLength(param));
        }


        Point3d firstVectorPoint = new Point3d();
        if (LineToDivide.MinimumDistanceTo(lineToCopy.From) == 0) firstVectorPoint = lineToCopy.From;
        else firstVectorPoint = lineToCopy.To;

        foreach (var pt in divisionPoints)
        {
            var transform = Transform.Translation(new Line(firstVectorPoint, pt).Direction);
            Line newLineToCopy = new Line();
            newLineToCopy = lineToCopy;
            newLineToCopy.Transform(transform);
            BeamAxes.Add(newLineToCopy);
        }
        return BeamAxes;
    }




    private Brep DrawBeam(Line baseLine, int width, int height)
    {

        var startFrame = new Plane();
        var endFrame = new Plane();
        var baseLineCurve = new LineCurve(baseLine);
        baseLineCurve.PerpendicularFrameAt(0, out startFrame);
        baseLineCurve.PerpendicularFrameAt(baseLine.Length, out endFrame);


        var intervalX = new Interval(-(double)width / 2000, (double)width / 2000);
        var intervalY = new Interval(-(double)height / 1000,0.1) ;
        var intervalZ = new Interval(0.0, baseLine.Length);



        var beam = new Box(startFrame,intervalX, intervalY, intervalZ);

        return beam.ToBrep();

    }


    #endregion


    #region additionalClasses

    public class CrossSectionCatalog
    {

        public int Width { get; set; }

        public IMaterialTimber Material { get; set; }

        public List<int> Heights { get; set; }

        public List<int> EIs { get; set; }
        public List<int> Areas { get; set; }

        //Price

        //CO2 per Cubic


        public CrossSectionCatalog(int width, IMaterialTimber material, List<int> heights)
        {
            Width = width;
            Material = material;
            Heights = heights;
            ComputeEI();
            ComputeAreas();

        }

        public CrossSectionCatalog(int width, IMaterialTimber material, int heightStart, int heightEnd, int step)
        {
            Width = width;
            Material = material;
            Heights = new List<int>();

            int height = heightStart;
            while (height < heightEnd)
            {
                height += step;
                Heights.Add(height);
            }

            ComputeEI();
            ComputeAreas();
        }


        private void ComputeEI()
        {
            EIs = new List<int>();
            foreach (int height in Heights)
            {
                EIs.Add((Int32)((Width * Math.Pow(height, 3) / 12 * Material.E0mean) / 1e10));
            }
        }

        private void ComputeAreas()
        {
            Areas = new List<int>();
            foreach (int height in Heights)
            {
                Areas.Add(Width * height);
            }
        }
    }


    public class ConfigurationResult
    {
        //Properties
        public List<CrossSectionRectangular> CrossSectionJoists { get; set; }
        public List<CrossSectionRectangular> CrossSectionBeams { get; set; }
        public int BeamsCount { get; set; }
        public int JoistsCount { get; set; }
        public Line JoistsDirection { get; set; }
        public Line BeamsDirection { get; set; }
        public List<double> VolumeJoists { get; set; }
        public List<double> VolumeBeams { get; set; }
        public double MinVolume { get; set; }


        public ConfigurationResult(List<CrossSectionRectangular> crossSectionJoists, List<CrossSectionRectangular> crossSectionBeams, int beamsCount, int joistsCount, Line joistsDirection, Line beamsDirection)
        {
            CrossSectionJoists = crossSectionJoists;
            CrossSectionBeams = crossSectionBeams;
            BeamsCount = beamsCount;
            JoistsCount = joistsCount;
            JoistsDirection = joistsDirection;
            BeamsDirection = beamsDirection;
            MinVolume = 0;

            ComputeVolume();
        }

        private void ComputeVolume()
        {

            VolumeJoists = new List<double>();
            VolumeBeams = new List<double>();

            if (CrossSectionJoists != null && CrossSectionJoists.Count > 0)
            {
                foreach (var CS in CrossSectionJoists)
                {
                    VolumeJoists.Add((double)CS.B / 1000 * (double)CS.H / 1000 * (double)JoistsDirection.Length * (double)JoistsCount);
                }
                MinVolume += VolumeJoists.Min();
            }


            if (CrossSectionBeams != null && CrossSectionBeams.Count > 0)
            {
                foreach (var CS in CrossSectionBeams)
                {
                    VolumeBeams.Add((double)CS.B / 1000 * (double)CS.H / 1000 * (double)JoistsDirection.Length * (double)BeamsCount);
                }
                MinVolume += VolumeBeams.Min();
            }
        }
    }




    #endregion


    #region display Text

    private readonly List<string> _text = new List<string>();
    private readonly List<Point3d> _point = new List<Point3d>();
    private readonly List<double> _size = new List<double>();

    public override void BeforeRunScript()
    {
        _text.Clear();
        _point.Clear();
        _size.Clear();
    }

    public override BoundingBox ClippingBox
    {
        get
        {
            return BoundingBox.Empty;
        }
    }
    public override void DrawViewportWires(IGH_PreviewArgs args)
    {
        if (_text.Count == 0)
            return;

        Plane plane;
        args.Viewport.GetFrustumFarPlane(out plane);

        for (int i = 0; i < _text.Count; i++)
        {
            string text = _text[i];
            double size = _size[i];
            Point3d point = _point[i];
            plane.Origin = point;

            // Figure out the size. This means measuring the visible size in the viewport AT the current location.
            double pixPerUnit;
            Rhino.Display.RhinoViewport viewport = args.Viewport;
            viewport.GetWorldToScreenScale(point, out pixPerUnit);

            size = size / pixPerUnit;

            Rhino.Display.Text3d drawText = new Rhino.Display.Text3d(text, plane, size);
            args.Display.Draw3dText(drawText, Color.Black);
            drawText.Dispose();
        }
    }
    #endregion



    // </Custom additional code>
}
