using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    public class HomeStorageTests
    {
        HomeDesigner designer;
        HomeStorage storage;
        string directory;
        [SetUp] public void SetUp()
        {
            GameConfig.Use(null);
            designer = new HomeDesigner(GameConfig.Current.Houses,GameConfig.Current.Items);
            directory = Path.Combine(Path.GetTempPath(),"wreckabulary-home-test-"+Guid.NewGuid().ToString("N"));
            storage = new HomeStorage(designer,directory);
        }
        [TearDown] public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory,true);
            GameConfig.Use(null);
        }
        HomeLayout Furnished(string map)
        {
            var house = GameConfig.Current.HouseFor(map);
            var batch = designer.AddWords(designer.CreateLayout(map,"Saved Test Home"),"TABLE PLANT",house.Rooms[0].Name);
            Assert.IsTrue(batch.Ok,string.Join("\n",batch.Errors)); Assert.AreEqual(2,batch.Added.Count);
            batch.Layout.Props[0].Yaw = (batch.Layout.Props[0].Yaw+180)%360;
            batch.Layout.Props[0].Skin = "Candy";
            Assert.IsTrue(designer.Validate(batch.Layout).Ok);
            return batch.Layout;
        }
        [TestCase("pinwheel")]
        [TestCase("courtyard")]
        public void SavedHomesRoundTripEveryPortableField(string map)
        {
            var layout = Furnished(map);
            Assert.IsTrue(storage.TrySave(layout,out var error),error);
            Assert.IsTrue(storage.TryLoad(map,out var loaded,out error),error);
            Assert.AreEqual(layout.ToJson(),loaded.ToJson());
            loaded.Props[0].X += .5;
            Assert.IsTrue(storage.TryLoad(map,out var again,out error),error);
            Assert.AreEqual(layout.ToJson(),again.ToJson(),"caller mutation cannot alter the file");
        }
        [Test] public void RejectedSaveLeavesPreviousFileExactlyIntact()
        {
            var layout = Furnished("pinwheel");
            Assert.IsTrue(storage.TrySave(layout,out var error),error);
            string path = Path.Combine(directory,"pinwheel.json"); byte[] before = File.ReadAllBytes(path);
            layout.Props[0].X = double.NaN;
            Assert.IsFalse(storage.TrySave(layout,out error)); Assert.IsNotEmpty(error);
            CollectionAssert.AreEqual(before,File.ReadAllBytes(path));
            Assert.AreEqual(1,Directory.GetFiles(directory).Length,"no temporary file is leaked");
        }
        [Test] public void ASecondValidSaveReplacesTheSameMapWithoutChangingTheOtherMap()
        {
            var first = Furnished("pinwheel"); var other = Furnished("courtyard");
            Assert.IsTrue(storage.TrySave(first,out var error),error); Assert.IsTrue(storage.TrySave(other,out error),error);
            first.Name = "A New Name"; Assert.IsTrue(storage.TrySave(first,out error),error);
            Assert.IsTrue(storage.TryLoad("pinwheel",out var restored,out error),error); Assert.AreEqual(first.ToJson(),restored.ToJson());
            Assert.IsTrue(storage.TryLoad("courtyard",out restored,out error),error); Assert.AreEqual(other.ToJson(),restored.ToJson());
        }
        [Test] public void MalformedOrMismatchedFilesNeverReturnAPartialLayout()
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory,"pinwheel.json"); File.WriteAllText(path,"{\"schema\":1,");
            Assert.IsFalse(storage.TryLoad("pinwheel",out var layout,out var error)); Assert.IsNull(layout); Assert.IsNotEmpty(error);
            File.WriteAllText(path,designer.CreateLayout("courtyard").ToJson());
            Assert.IsFalse(storage.TryLoad("pinwheel",out layout,out error)); Assert.IsNull(layout); StringAssert.Contains("different map",error);
        }
        [Test] public void UnsupportedMapCannotEscapeTheStorageDirectory()
        {
            Assert.IsFalse(storage.TryLoad("../../outside",out var layout,out var error));
            Assert.IsNull(layout); Assert.IsNotEmpty(error); Assert.IsFalse(Directory.Exists(directory));
        }
        [Test] public void InvalidEditIsAtomicAndDoesNotConsumeUndoOrRedo()
        {
            var history = new HomeHistory(designer,Furnished("pinwheel")); var initial = history.Current.ToJson();
            var bad = history.Current; bad.Props.Add(bad.Props[0].Clone());
            Assert.IsFalse(history.TryApply(bad,out var error)); Assert.IsNotEmpty(error);
            Assert.AreEqual(initial,history.Current.ToJson()); Assert.IsFalse(history.CanUndo); Assert.IsFalse(history.CanRedo);
            var detached = history.Current; detached.Name = "Outside mutation";
            Assert.AreEqual(initial,history.Current.ToJson());
        }
        [Test] public void UndoRedoRestoresExactSnapshotsAndNewEditClearsTheRedoBranch()
        {
            var initial = Furnished("pinwheel"); var history = new HomeHistory(designer,initial);
            var second = history.Current; second.Name = "Second"; Assert.IsTrue(history.TryApply(second,out var error),error);
            var third = history.Current; third.Name = "Third"; Assert.IsTrue(history.TryApply(third,out error),error);
            Assert.IsTrue(history.Undo()); Assert.AreEqual(second.ToJson(),history.Current.ToJson());
            Assert.IsTrue(history.Redo()); Assert.AreEqual(third.ToJson(),history.Current.ToJson());
            Assert.IsTrue(history.Undo()); var branch = history.Current; branch.Name = "Branch";
            Assert.IsTrue(history.TryApply(branch,out error),error); Assert.IsFalse(history.CanRedo);
            Assert.IsTrue(history.Undo()); Assert.AreEqual(second.ToJson(),history.Current.ToJson());
            Assert.IsTrue(history.Undo()); Assert.AreEqual(initial.ToJson(),history.Current.ToJson());
        }
        [Test] public void AValidatedDifferentMapCannotReplaceAnExistingHistory()
        {
            var history = new HomeHistory(designer,Furnished("pinwheel")); string before = history.Current.ToJson();
            var imported = designer.Import(designer.CreateLayout("courtyard").ToJson());
            Assert.IsTrue(imported.Ok);
            Assert.IsFalse(history.TryApply(imported.Layout,out var error)); StringAssert.Contains("map",error);
            Assert.AreEqual(before,history.Current.ToJson()); Assert.IsFalse(history.CanUndo);
        }
        [Test] public void AnUnchangedEditDoesNotDiscardTheRedoBranch()
        {
            var history = new HomeHistory(designer,Furnished("pinwheel"));
            var changed = history.Current; changed.Name = "Second"; Assert.IsTrue(history.TryApply(changed,out var error),error);
            Assert.IsTrue(history.Undo()); Assert.IsTrue(history.CanRedo);
            Assert.IsTrue(history.TryApply(history.Current,out error),error);
            Assert.IsFalse(history.CanUndo); Assert.IsTrue(history.CanRedo);
            Assert.IsTrue(history.Redo()); Assert.AreEqual(changed.ToJson(),history.Current.ToJson());
        }
        [Test] public void ExportFileUsesTheValidatedPortableDocument()
        {
            var layout = Furnished("pinwheel"); Assert.IsTrue(storage.TryExportFile(layout,out var path,out var error),error);
            Assert.AreEqual(Path.Combine(directory,"pinwheel-export.json"),path);
            Assert.AreEqual(layout.ToJson(),File.ReadAllText(path));
        }
    }
}
