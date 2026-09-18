using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Reflection;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using BatchPlotPlus.AutoCAD;
using App = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(SplitChecks))]
public class SplitChecks
{
    private static string Root => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
    private static string Result => Path.Combine(Root,"results");
    private static void Assert(bool value,string text) { if(!value) throw new InvalidOperationException(text); }
    private static ObjectId Add(Transaction tx, BlockTableRecord model, Entity entity)
    { var id=model.AppendEntity(entity); tx.AddNewlyCreatedDBObject(entity,true); return id; }
    private static Polyline Shape(params double[] xy)
    { var p=new Polyline(); for(int i=0;i<xy.Length;i+=2)p.AddVertexAt(i/2,new Point2d(xy[i],xy[i+1]),0,0,0);p.Closed=true;return p; }
    private static string[] Texts(string file)
    {
        using(var db=new Database(false,true))
        { db.ReadDwgFile(file,FileOpenMode.OpenForReadAndAllShare,true,"");db.CloseInput(true);
          using(var tx=db.TransactionManager.StartOpenCloseTransaction())
          { var bt=(BlockTable)tx.GetObject(db.BlockTableId,OpenMode.ForRead);var ms=(BlockTableRecord)tx.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForRead);
            return ms.Cast<ObjectId>().Select(id=>tx.GetObject(id,OpenMode.ForRead)).OfType<DBText>().Select(t=>t.TextString).ToArray(); }
        }
    }
    private static void AssertFullCrossingLine(string file)
    {
        using(var db=new Database(false,true))
        {
            db.ReadDwgFile(file,FileOpenMode.OpenForReadAndAllShare,true,"");db.CloseInput(true);
            using(var tx=db.TransactionManager.StartOpenCloseTransaction())
            {
                var bt=(BlockTable)tx.GetObject(db.BlockTableId,OpenMode.ForRead);var ms=(BlockTableRecord)tx.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForRead);
                var lines=ms.Cast<ObjectId>().Select(id=>tx.GetObject(id,OpenMode.ForRead)).OfType<Line>().ToArray();
                Assert(lines.Length==1 && Math.Abs(lines[0].Length-260)<1e-7,"WB preserves whole crossing line");
            }
        }
    }
    [CommandMethod("BPPSPLIT153")]
    public void Test()
    {
        Directory.CreateDirectory(Result);var log=Path.Combine(Result,"synthetic.txt");File.WriteAllText(log,"START\n");
        try
        {
            var doc=App.DocumentManager.MdiActiveDocument;var db=doc.Database;ObjectId first,second,concave,tagA;
            using(var tx=db.TransactionManager.StartTransaction())
            {
                var layers=(LayerTable)tx.GetObject(db.LayerTableId,OpenMode.ForWrite);var l=new LayerTableRecord{Name="BPP_TEST_FRAMES"};layers.Add(l);tx.AddNewlyCreatedDBObject(l,true);
                var bt=(BlockTable)tx.GetObject(db.BlockTableId,OpenMode.ForRead);var ms=(BlockTableRecord)tx.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForWrite);
                var a=Shape(0,0,100,0,100,80,0,80);a.Layer=l.Name;first=Add(tx,ms,a);
                var b=Shape(200,0,300,0,300,80,200,80);b.Layer=l.Name;second=Add(tx,ms,b);
                var c=Shape(400,0,500,0,500,40,440,40,440,100,400,100);c.Layer=l.Name;concave=Add(tx,ms,c);
                tagA=Add(tx,ms,new DBText{TextString="A_ONLY",Position=new Point3d(10,20,0),Height=3});
                Add(tx,ms,new DBText{TextString="B_ONLY",Position=new Point3d(210,20,0),Height=3});
                Add(tx,ms,new DBText{TextString="C_ONLY",Position=new Point3d(410,20,0),Height=3});
                Add(tx,ms,new DBText{TextString="NOT_IN_CONCAVE",Position=new Point3d(465,70,0),Height=2});tx.Commit();
            }
            var priorUcs=doc.Editor.CurrentUserCoordinateSystem;
            doc.Editor.CurrentUserCoordinateSystem=Matrix3d.Displacement(new Vector3d(31,42,0))*Matrix3d.Rotation(0.61,Vector3d.ZAxis,Point3d.Origin);
            var ucs=doc.Editor.CurrentUserCoordinateSystem;
            using(var view=doc.Editor.GetCurrentView())
            {
                view.ViewTwist=0.23;doc.Editor.SetCurrentView(view);
                using(var expectedView=doc.Editor.GetCurrentView())
                {
                    var state=new PluginState{FrameMode=FrameMode.Polyline,TemplateHandle=first.Handle.ToString(),AutoLayer=true,DwgOutputDirectory=Path.Combine(Result,"rect"),DwgFilePrefix="sheet",DwgTestFirstTwo=false};
                    DwgSplitService.Execute(doc,state);
                    Assert(Directory.GetFiles(state.DwgOutputDirectory,"*.dwg").Length==2,"rectangle count");
                    var sets=Directory.GetFiles(state.DwgOutputDirectory,"*.dwg").Select(Texts).ToArray();
                    Assert(sets.Any(t=>t.SequenceEqual(new[]{"A_ONLY"})) && sets.Any(t=>t.SequenceEqual(new[]{"B_ONLY"})),"one sheet per file");
                    Assert(doc.Editor.CurrentUserCoordinateSystem.IsEqualTo(ucs),"UCS restore");
                    using(var actual=doc.Editor.GetCurrentView())Assert(Math.Abs(actual.ViewTwist-expectedView.ViewTwist)<1e-9 && actual.Target.IsEqualTo(expectedView.Target),"view restore");
                    File.AppendAllText(log,"RECTANGLE_TWO_FILES_AND_ROTATED_UCS_PASS\n");
                    state.FrameMode=FrameMode.Custom;state.TemplateHandle=concave.Handle.ToString();state.ExplicitSheetHandles.Add(concave.Handle.ToString());state.DwgOutputDirectory=Path.Combine(Result,"concave");
                    DwgSplitService.Execute(doc,state);var texts=Texts(Directory.GetFiles(state.DwgOutputDirectory,"*.dwg").Single());
                    Assert(texts.SequenceEqual(new[]{"C_ONLY"}),"concave excludes bbox-only content");
                    File.AppendAllText(log,"CONCAVE_TRUE_CONTOUR_PASS\n");
                    using(var tx=db.TransactionManager.StartTransaction())
                    {
                        var bt=(BlockTable)tx.GetObject(db.BlockTableId,OpenMode.ForRead);var ms=(BlockTableRecord)tx.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForWrite);
                        Add(tx,ms,new Line(new Point3d(20,40,0),new Point3d(280,40,0)));tx.Commit();
                    }
                    state.FrameMode=FrameMode.Polyline;state.TemplateHandle=first.Handle.ToString();state.ExplicitSheetHandles.Clear();state.DwgOutputDirectory=Path.Combine(Result,"crossing");
                    DwgSplitService.Execute(doc,state);
                    var files=Directory.GetFiles(state.DwgOutputDirectory,"*.dwg");
                    Assert(files.Length==2,"cross-sheet still produces separate outputs");
                    foreach(var file in files)AssertFullCrossingLine(file);
                    Assert(doc.Editor.CurrentUserCoordinateSystem.IsEqualTo(ucs),"crossing UCS restore");
                    using(var actual=doc.Editor.GetCurrentView())Assert(Math.Abs(actual.ViewTwist-expectedView.ViewTwist)<1e-9 && actual.Target.IsEqualTo(expectedView.Target),"crossing view restore");
                    File.AppendAllText(log,"CROSS_SHEET_WHOLE_OBJECT_OUTPUT_PASS\nPASS\n");
                }
            }
            doc.Editor.CurrentUserCoordinateSystem=priorUcs;
        }
        catch(System.Exception e){File.AppendAllText(log,"FAIL "+e+"\n");}
    }
}
