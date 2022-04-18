using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.ApplicationServices;

namespace CreationModelPlaginRev3
{
    [TransactionAttribute(TransactionMode.Manual)]
    public class CreationModel : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            List<Level> levels = GetListOfLevels(doc);

            Level level1 = GetLevel(levels, "Уровень 1");
            Level level2 = GetLevel(levels, "Уровень 2");

            List<Wall> walls = new List<Wall>();

            CreateWalls(doc, 10000, 5000, level1, level2, ref walls);

            AddDoor(doc, level1, walls[0]);

            for (int i = 1; i < walls.Count; i++)
                AddWindow(doc, level1, walls[i], 900);

            AddRoof(doc, level2, walls);

            return Result.Succeeded;
        }

        private void AddRoof(Document doc, Level level, List<Wall> walls)
        {
            RoofType roofType = new FilteredElementCollector(doc)
                .OfClass(typeof(RoofType))
                .OfType<RoofType>()
                .Where(x => x.Name.Equals("Типовой - 400мм"))
                .Where(x => x.FamilyName.Equals("Базовая крыша"))
                .FirstOrDefault();

            double wallWidth = walls[0].Width;
            double dt = wallWidth / 2;

            Transaction transaction = new Transaction(doc, "Создание кровли выдавливанием");
            transaction.Start();

            // задаем незамкнутый профиль, определяющий форму крыши (в плоскости короткой стены)
            double elevation = level.Elevation;
            LocationCurve curve = walls[1].Location as LocationCurve;
            XYZ p1 = curve.Curve.GetEndPoint(0);
            XYZ p2 = curve.Curve.GetEndPoint(1);
            p1 = new XYZ(p1.X, p1.Y - 2 * dt, p1.Z + elevation + 1.3); // крайняя точка на скате
            p2 = new XYZ(p2.X, p2.Y + 2 * dt, p2.Z + elevation + 1.3); // крайняя точка на скате
            XYZ p3 = (p1 + p2) / 2;
            p3 = new XYZ(p3.X, p3.Y, p3.Z + 3); // точка на коньке
            CurveArray curveArray = new CurveArray();
            curveArray.Append(Line.CreateBound(p1, p3));
            curveArray.Append(Line.CreateBound(p3, p2));

            // задаем плоскость для размещения профиля
            ReferencePlane plane = doc.Create.NewReferencePlane(new XYZ(0, 0, 0), new XYZ(0, 0, 1), new XYZ(0, 1, 0), doc.ActiveView);

            // задаем начало и конец выдавливания (в плоскости длинной стены)
            LocationCurve curve1 = walls[0].Location as LocationCurve;
            XYZ pnt1 = curve1.Curve.GetEndPoint(0);
            XYZ pnt2 = curve1.Curve.GetEndPoint(1);
            double extrusionStart = (pnt2.X - pnt1.X) / 2 + 2 * dt;
            double extrusionEnd = -1 * extrusionStart;

            doc.Create.NewExtrusionRoof(curveArray, plane, level, roofType, extrusionStart, extrusionEnd);

            transaction.Commit();
        }

        private void AddWindow(Document doc, Level level, Wall wall, double height)
        {
            FamilySymbol windowType = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_Windows)
                .OfType<FamilySymbol>()
                .Where(x => x.Name.Equals("0915 x 1830 мм"))
                .Where(x => x.FamilyName.Equals("Фиксированные"))
                .FirstOrDefault();

            LocationCurve hostCurve = wall.Location as LocationCurve;
            XYZ point1 = hostCurve.Curve.GetEndPoint(0);
            XYZ point2 = hostCurve.Curve.GetEndPoint(1);
            XYZ point = (point1 + point2) / 2;

            double convertedHeight = UnitUtils.ConvertToInternalUnits(height, UnitTypeId.Millimeters);
            point = new XYZ(point.X, point.Y, point.Z + convertedHeight);

            Transaction transaction = new Transaction(doc, "Вставка окна");
            transaction.Start();
            if (!windowType.IsActive)
                windowType.Activate();
            doc.Create.NewFamilyInstance(point, windowType, wall, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
            transaction.Commit();
        }

        private void AddDoor(Document doc, Level level, Wall wall)
        {
            FamilySymbol doorType = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_Doors)
                .OfType<FamilySymbol>()
                .Where(x => x.Name.Equals("0915 x 2134 мм"))
                .Where(x => x.FamilyName.Equals("Одиночные-Щитовые"))
                .FirstOrDefault();

            LocationCurve hostCurve = wall.Location as LocationCurve;
            XYZ point1 = hostCurve.Curve.GetEndPoint(0);
            XYZ point2 = hostCurve.Curve.GetEndPoint(1);
            XYZ point = (point1 + point2) / 2;

            Transaction transaction = new Transaction(doc, "Вставка двери");
            transaction.Start();
            if (!doorType.IsActive)
                doorType.Activate();
            doc.Create.NewFamilyInstance(point, doorType, wall, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
            transaction.Commit();
        }

        public List<Level> GetListOfLevels(Document doc)
        {
            List<Level> listlevel = new FilteredElementCollector(doc)
                                        .OfClass(typeof(Level))
                                        .OfType<Level>()
                                        .ToList();

            if (listlevel != null)
                return listlevel;
            else
                return null;
        }

        public Level GetLevel(List<Level> levels, string levelName)
        {
            Level level = levels
                .Where(x => x.Name.Equals(levelName))
                .FirstOrDefault();

            if (level != null)
                return level;
            else
                return null;
        }

        public static void CreateWalls(Document doc, double width, double depth, Level level1, Level level2, ref List<Wall> createdWalls)
        {
            double convertredWidth = UnitUtils.ConvertToInternalUnits(width, UnitTypeId.Millimeters);
            double convertredDepth = UnitUtils.ConvertToInternalUnits(depth, UnitTypeId.Millimeters);
            double dx = convertredWidth / 2;
            double dy = convertredDepth / 2;

            List<XYZ> points = new List<XYZ>();
            points.Add(new XYZ(-dx, -dy, 0));
            points.Add(new XYZ(dx, -dy, 0));
            points.Add(new XYZ(dx, dy, 0));
            points.Add(new XYZ(-dx, dy, 0));
            points.Add(new XYZ(-dx, -dy, 0));

            Transaction transaction = new Transaction(doc, "Построение стен");
            transaction.Start();
            for (int i = 0; i < 4; i++)
            {
                Line line = Line.CreateBound(points[i], points[i + 1]);
                Wall wall = Wall.Create(doc, line, level1.Id, false);
                wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).Set(level2.Id);
                createdWalls.Add(wall);
            }
            transaction.Commit();
        }

        //private void AddRoof(Document doc, Level level, List<Wall> walls)
        //{
        //    RoofType roofType = new FilteredElementCollector(doc)
        //        .OfClass(typeof(RoofType))
        //        .OfType<RoofType>()
        //        .Where(x => x.Name.Equals("Типовой - 400мм"))
        //        .Where(x => x.FamilyName.Equals("Базовая крыша"))
        //        .FirstOrDefault();

        //    double wallWidth = walls[0].Width;
        //    double dt = wallWidth / 2;

        //    List<XYZ> points = new List<XYZ>();
        //    points.Add(new XYZ(-dt, -dt, 0));
        //    points.Add(new XYZ(dt, -dt, 0));
        //    points.Add(new XYZ(dt, dt, 0));
        //    points.Add(new XYZ(-dt, dt, 0));
        //    points.Add(new XYZ(-dt, -dt, 0));

        //    Application application = doc.Application;
        //    CurveArray footprint = application.Create.NewCurveArray();
        //    for (int i = 0; i < walls.Count; i++)
        //    {
        //        LocationCurve curve = walls[i].Location as LocationCurve;
        //        XYZ p1 = curve.Curve.GetEndPoint(0);
        //        XYZ p2 = curve.Curve.GetEndPoint(1);
        //        Line line = Line.CreateBound(p1 + points[i], p2 + points[i + 1]);
        //        footprint.Append(line);
        //    }
        //    ModelCurveArray footPrintModelCurveMapping = new ModelCurveArray();

        //    Transaction transaction = new Transaction(doc, "Создание кровли");
        //    transaction.Start();
        //    FootPrintRoof footPrintRoof = doc.Create.NewFootPrintRoof(footprint, level, roofType, out footPrintModelCurveMapping);
        //    ModelCurveArrayIterator iterator = footPrintModelCurveMapping.ForwardIterator();
        //    iterator.Reset();
        //    while (iterator.MoveNext())
        //    {
        //        ModelCurve modelCurve = iterator.Current as ModelCurve;
        //        footPrintRoof.set_DefinesSlope(modelCurve, true);
        //        footPrintRoof.set_SlopeAngle(modelCurve, 0.5);
        //    }
        //    transaction.Commit();
        //}




    }
}
