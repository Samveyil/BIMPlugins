using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Comparers;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.Windows;
using Microsoft.Win32;
using BIMPlugins.ClashViewer.Classes;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Xml.Linq;
using BIMPlugins.ExtStorage.Methods;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;

namespace BIMPlugins.ClashViewer.WPF
{
    public partial class ClashViewerViewModel : ObservableObject, IDisposable
    {
        [ObservableProperty] private string _xmlFilePath;
        [ObservableProperty] private string _reportDate;
        [ObservableProperty] private string _syncTime;
        [ObservableProperty] private bool _isWorkFile = false;

        [ObservableProperty] private ObservableCollection<string> _levels = [];
        [ObservableProperty] private ObservableCollection<string> _sections = [];
        [ObservableProperty] private ObservableCollection<string> _familyNames = [];
        [ObservableProperty] private string _selectedStatusToFilter;
        [ObservableProperty] private string _selectedLevelToFilter;
        [ObservableProperty] private string _selectedSectionToFilter;
        [ObservableProperty] private string _selectedFamilyNameToFilter;
        [ObservableProperty] private string _selectedUserFilterRules;
        [ObservableProperty] private bool _containsBothElements = false;

        [ObservableProperty] private string _clashTestFilter;
        [ObservableProperty][NotifyCanExecuteChangedFor(nameof(SyncCommand))] private ClashTest _selectedClashTest;
        [ObservableProperty] private ClashResult _selectedClashResult;
        [ObservableProperty] private ObservableCollection<ClashResult> _selectedClashResults = [];
        
        [ObservableProperty] private double _progressStatus = 0;
        [ObservableProperty] private double _padding = 2000;
        [ObservableProperty] private bool _isolateElements = false;
        
        [ObservableProperty] private string _leftSection;
        [ObservableProperty] private string _rightSection;
        [ObservableProperty] private bool _isLeftEqualRight;
        [ObservableProperty][NotifyCanExecuteChangedFor(nameof(WriteNewCommentCommand))] private string _newCommentText;

        private UserFilterViewModel _userFilterViewModel;
        private XDocument _xDoc;
        private string _xmlDirectory;
        private string _syncXDocPath;
        private readonly string _pattern = @"\d{6}|\d{8}";
        private readonly ObservableCollection<ClashTest> _clashTests = [];
        private List<RevitLinkInstance> _rvtLinks = [];

        private Mutex _mutex;

        partial void OnXmlFilePathChanged(string value)
        {
            SyncTime = null;
            SelectedClashTest = null;
            _userFilterViewModel = null;
            SelectedUserFilterRules = null;
            _clashTests.Clear();

            if (value == null)
            {
                ReportDate = null;
                return;
            }

            _xmlDirectory = Path.GetDirectoryName(value);
            ReportDate = File.GetLastWriteTime(value).ToString("dd-MM-yyyy H:mm");

            var commentDirPath = _xmlDirectory.AppendPath("Комментарии");
            _syncXDocPath = commentDirPath.AppendPath("SEPluginsJournal.xml");

            if (IsWorkFile)
            {
                Directory.CreateDirectory(commentDirPath);
                Directory.CreateDirectory(commentDirPath.AppendPath("Архив"));

                if (!File.Exists(_syncXDocPath))
                {
                    var xDoc = new XDocument(new XElement("clashTests"));
                    xDoc.Save(_syncXDocPath);
                }

                ArchivateHistory();
            }

            foreach (var xClashTest in _xDoc.Descendants("clashtest").Where(c => c.Descendants("clashresult").Count() != 0))
            {
                _clashTests.Add(new ClashTest(xClashTest.Attribute("name").Value));
            }
        }

        partial void OnSelectedStatusToFilterChanged(string value)
        {
            RefreshClashResults(SelectedClashTest);
        }
        partial void OnSelectedLevelToFilterChanged(string value)
        {
            RefreshClashResults(SelectedClashTest);
        }
        partial void OnSelectedSectionToFilterChanged(string value)
        {
            RefreshClashResults(SelectedClashTest);
        }
        partial void OnSelectedFamilyNameToFilterChanged(string value)
        {
            RefreshClashResults(SelectedClashTest);
        }
        partial void OnContainsBothElementsChanged(bool value)
        {
            RefreshClashResults(SelectedClashTest);
        }

        partial void OnClashTestFilterChanged(string value)
        {
            FilteredClashTests.Refresh();
        }
        partial void OnSelectedClashTestChanged(ClashTest value)
        {
            if (value == null)
            {
                ProgressStatus = 0;
                return;
            }

            if (value.ClashResults.Count == 0)
            {
                GetClashResults(value);
            }

            LeftSection = value.Name.Split('-')[0].Split('_')[0].Split('(')[0].Trim();
            RightSection = value.Name.Split('-')[1].Split('_')[0].Split('(')[0].Trim();
            IsLeftEqualRight = LeftSection == RightSection;

            GetClashHistories(value);
            GetOldTypeClashHistories(value);
            SetActiveClashesPercent();

            Levels = new(value.ClashResults
                .Select(cl => cl.LevelName)
                .ToHashSet()
                .OrderBy(l => l, new NaturalComparer()));

            Sections = new(new List<string>() { LeftSection, RightSection }.OrderBy(p => p));

            FamilyNames = new(value.ClashResults.SelectMany(c => c.ClashObjects)
                .Select(cl => cl.FamilyName)
                .ToHashSet()
                .OrderBy(l => l, new NaturalComparer()));

            _userFilterViewModel = null;

            RefreshClashResults(value);
        }

        public List<string> Statuses { get; set; } = ["Активная", "Исправленная", "Не коллизия"];
        public ICollectionView FilteredClashTests { get; }

        private ExternalEvent ExEvent { get; set; }

        public ClashViewerViewModel()
        {
            var handler = new RevitAPI.MyEventHandler<ClashViewerViewModel>(
                this,
                vm => vm.SetSectionBox()
            );
            ExEvent = ExternalEvent.Create(handler);

            FilteredClashTests = CollectionViewSource.GetDefaultView(_clashTests);

            FilteredClashTests.Filter = item =>
            {
                if (ClashTestFilter.IsNullOrEmpty())
                    return true;

                var clashTest = (ClashTest)item;
                return clashTest.Name.Contains(ClashTestFilter);
            };
        }
        public void Dispose()
        {
            _mutex?.Dispose();
            _mutex = null;
        }


        [RelayCommand]
        private void SelectXMLFile()
        {
            var dialog = new OpenFileDialog();
            dialog.DefaultExt = "*.xml";
            dialog.Title = "Выберите файл отчета";
            dialog.Filter = "файл XML|*.xml";

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    _xDoc = XDocument.Load(dialog.FileName);

                    IsWorkFile = !Regex.IsMatch(dialog.SafeFileName, _pattern);

                    XmlFilePath = null;
                    XmlFilePath = dialog.FileName;
                }
                catch
                {
                    MessageBox.Show("Файл отчета не сформирован полностью! Повторите попытку позже.", "BIMPlugins", MessageBoxButton.OK, MessageBoxImage.Error);
                    XmlFilePath = null;
                }
            }
        }

        [RelayCommand(CanExecute = (nameof(IsUIDocumentActive)))]
        private void UserFilter()
        {
            if (_userFilterViewModel == null)
            {
                var doc = RevitAPI.Document;

                var ids = SelectedClashTest.ClashResults.SelectMany(c => c.ClashObjects)
                    .Select(cl => cl.Id)
                    .ToHashSet()
                    .ToList();

                var elements = new List<Element>();
                foreach (var id in ids)
                {
                    var element = new ElementId(id).ToElement(doc);
                    if (element == null || element.Category == null) continue;

                    elements.Add(element);
                }

                if (elements.Count == 0)
                {
                    MessageWindow.ShowMessage("В открытом проекте отсутствуют элементы из этой проверки!", MessageBoxImage.Warning);
                    return;
                }

                _userFilterViewModel = new UserFilterViewModel(elements);
            }

            var userFilterWindow = new UserFilterWindow(_userFilterViewModel);
            _userFilterViewModel.CloseRequest += (s, e) => userFilterWindow.Close();

            userFilterWindow.ShowDialog();

            if (_userFilterViewModel.IsCommandExecuted)
            {
                SelectedUserFilterRules = _userFilterViewModel.SelectedFilterRules;
                RefreshClashResults(SelectedClashTest);
            }
            else
            {
                _userFilterViewModel = null;
            }
        }

        [RelayCommand]
        private void ClearUserFilter()
        {
            _userFilterViewModel = null;
            SelectedUserFilterRules = null;
            RefreshClashResults(SelectedClashTest);
        }

        [RelayCommand]
        private void ClearFilter(string value)
        {
            if (SelectedStatusToFilter == value)
                SelectedStatusToFilter = null;
            else if (SelectedLevelToFilter == value)
                SelectedLevelToFilter = null;
            else if (SelectedSectionToFilter == value)
                SelectedSectionToFilter = null;
            else if (SelectedFamilyNameToFilter == value)
                SelectedFamilyNameToFilter = null;

            RefreshClashResults(SelectedClashTest);
        }

        [RelayCommand]
        private void SetStatus(string status)
        {
            foreach (var clashResult in SelectedClashResults)
            {
                var oldStatus = string.Empty;
                var historyItem = clashResult.HistoryItems.FirstOrDefault();
                if (historyItem != null && historyItem.Author == RevitAPI.Application.Username)
                {
                    oldStatus = historyItem.OldStatus;
                    if (oldStatus == status)
                    {
                        clashResult.HistoryItems.Remove(historyItem);
                        clashResult.IsModified = false;
                    }
                }

                if (oldStatus != status)
                {
                    clashResult.HistoryItems.Insert(0, new HistoryItem(clashResult.Status, status) { Author = RevitAPI.Application.Username });
                    clashResult.IsModified = true;
                }

                clashResult.Status = status;
            }

            SetActiveClashesPercent();

            RefreshClashResults(SelectedClashTest);
        }
        private void SetStatus(string status, ClashResult clashResult)
        {
            var oldStatus = string.Empty;
            var historyItem = clashResult.HistoryItems.FirstOrDefault();
            if (historyItem != null && historyItem.Author == RevitAPI.Application.Username)
            {
                oldStatus = historyItem.OldStatus;
                if (oldStatus == status)
                {
                    clashResult.HistoryItems.Remove(historyItem);
                    clashResult.IsModified = false;
                }
            }
            if (oldStatus != status)
            {
                clashResult.HistoryItems.Insert(0, new HistoryItem(clashResult.Status, status) { Author = RevitAPI.Application.Username });
                clashResult.IsModified = true;
            }

            clashResult.Status = status;

            SetActiveClashesPercent();

            RefreshClashResults(SelectedClashTest);
        }

        private void SetActiveClashesPercent()
        {
            var notActiveClashesCount = SelectedClashTest.ClashResults.Where(c => !c.IsActive).ToList().Count;
            ProgressStatus = ((double)notActiveClashesCount / SelectedClashTest.ClashResults.Count * 100);

            if (ProgressStatus != 100 && ProgressStatus.Round(0) == 100)
            {
                ProgressStatus = 99;
            }
            else if (ProgressStatus != 0 && ProgressStatus.Round(0) == 0)
            {
                ProgressStatus = 1;
            }
            else
            {
                ProgressStatus = ProgressStatus.Round(0);
            }
        }

        [RelayCommand]
        private void AssignRole(string role)
        {
            foreach (var clashResult in SelectedClashResults)
            {
                var oldRole = string.Empty;
                var historyItem = clashResult.HistoryItems.FirstOrDefault();
                if (historyItem != null && historyItem.Author == RevitAPI.Application.Username)
                {
                    oldRole = historyItem.OldRole;
                    if (oldRole == role || oldRole == string.Empty)
                    {
                        clashResult.HistoryItems.Remove(historyItem);
                        clashResult.IsModified = false;
                    }
                }

                if (oldRole != role)
                {
                    clashResult.HistoryItems.Insert(
                        0,
                        new HistoryItem($"Выбран новый ответственный: Отдел {role}") { Author = RevitAPI.Application.Username, OldRole = clashResult.AssignedTo ?? string.Empty }
                    );
                    clashResult.IsModified = true;
                }

                if (clashResult.HistoryItems.FirstOrDefault(h => h.Comment.StartsWith("Выбран новый ответственный:")) == null)
                {
                    clashResult.AssignedTo = null;
                    clashResult.IsLeftSelected = null;
                }    
                else
                {
                    clashResult.AssignedTo = role;
                    clashResult.IsLeftSelected = LeftSection == role;
                }
            }

            RefreshClashResults(SelectedClashTest);
        }

        [RelayCommand(CanExecute = nameof(IsWriteCommentEnabled))]
        private void WriteNewComment()
        {
            bool? userSelection = null;

            foreach (var clashResult in SelectedClashResults)
            {
                var historyItem = clashResult.HistoryItems.FirstOrDefault();
                if (historyItem != null && historyItem.Author == RevitAPI.Application.Username)
                {
                    if (!historyItem.Comment.StartsWith("Статус:") && !historyItem.Comment.StartsWith("Выбран новый ответственный:"))
                    {
                        var targetTime = DateTime.ParseExact(historyItem.Date, "dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None);
                        TimeSpan difference = DateTime.Now - targetTime;

                        if (difference.TotalMinutes <= 3 && clashResult.IsModified)
                        {
                            if (userSelection == null)
                            {
                                if (MessageWindow.ShowMessage("Перезаписать существующий комментарий?", MessageBoxImage.Question, false) == MessageBoxResult.Yes)
                                {
                                    userSelection = true;
                                    historyItem.Comment = NewCommentText;
                                }
                                else
                                {
                                    userSelection = false;
                                    clashResult.HistoryItems.Insert(0, new HistoryItem(NewCommentText) { Author = RevitAPI.Application.Username });
                                }
                            }
                            else if (userSelection == true)
                            {
                                historyItem.Comment = NewCommentText;
                            }
                            else
                            {
                                clashResult.HistoryItems.Insert(0, new HistoryItem(NewCommentText) { Author = RevitAPI.Application.Username });
                            }
                        }
                        else
                        {
                            clashResult.HistoryItems.Insert(0, new HistoryItem(NewCommentText) { Author = RevitAPI.Application.Username });
                        }
                    }
                    else if (historyItem.Comment.StartsWith("Выбран новый ответственный:"))
                    {
                        if (userSelection == null && clashResult.IsModified)
                        {
                            if (MessageWindow.ShowMessage("Добавить комментарий к ответственному?", MessageBoxImage.Question, false) == MessageBoxResult.Yes)
                            {
                                userSelection = true;
                                historyItem.Comment = historyItem.Comment.Split('\n').First() + "\n" + NewCommentText;
                            }
                            else
                            {
                                userSelection = false;
                                clashResult.HistoryItems.Insert(0, new HistoryItem(NewCommentText) { Author = RevitAPI.Application.Username });
                            }
                        }
                        else if (userSelection == true)
                        {
                            historyItem.Comment = historyItem.Comment.Split('\n').First() + "\n" + NewCommentText;
                        }
                        else
                        {
                            clashResult.HistoryItems.Insert(0, new HistoryItem(NewCommentText) { Author = RevitAPI.Application.Username });
                        }
                    }
                    else
                    {
                        clashResult.HistoryItems.Insert(0, new HistoryItem(NewCommentText) { Author = RevitAPI.Application.Username });
                    }
                }
                else
                {
                    clashResult.HistoryItems.Insert(0, new HistoryItem(NewCommentText) { Author = RevitAPI.Application.Username });
                }

                clashResult.LastComment = NewCommentText;
                clashResult.IsModified = true;
            }

            NewCommentText = null;
        }

        [RelayCommand]
        private void ExecuteEnterKey(KeyEventArgs args)
        {
            if (args?.Key == Key.Enter && !NewCommentText.IsNullOrEmpty())
            {
                WriteNewComment();
                Keyboard.ClearFocus();
            }
        }

        [RelayCommand]
        private void CopyId(ClashObject clashObject)
        {
            Clipboard.SetText(clashObject.Id.ToString());
        }

        [RelayCommand(CanExecute = nameof(IsUIDocumentActive))]
        private void SelectElements()
        {
            var ids = SelectedClashResult
                .ClashObjects
                .Select(x => new ElementId(x.Id))
                .ToList();

            RevitAPI.UIDocument.Selection.SetElementIds(ids);
        }

        [RelayCommand(CanExecute = nameof(IsUIDocumentActive))]
        private void SectionBox() => ExEvent.Raise();
        private void SetSectionBox()
        {
            var ids = SelectedClashResult
                .ClashObjects
                .Select(x => new ElementId(x.Id))
                .ToList();

            if (ids.Select(id => id.ToElement()).All(e => e == null))
            {
                MessageWindow.ShowMessage("В проекте отсутствуют элементы!", MessageBoxImage.Warning);
                return;
            }

            var view3D = RevitAPI.Document.GetView3D($"Просмотр коллизий - {RevitAPI.Application.Username}");

            double padding = UnitUtils.ConvertToInternalUnits(Padding / 2, ParameterMethods.GetUnitType());

            var clashPoint = GetRevitPoint(SelectedClashResult.ClashPoint);
            var bbox = new BoundingBoxXYZ
            {
                Min = clashPoint - new XYZ(padding, padding, padding),
                Max = clashPoint + new XYZ(padding, padding, padding)
            };

            using (Transaction t = new Transaction(RevitAPI.Document, "Обрезать 3D вид"))
            {
                t.Start();

                view3D.SetSectionBox(bbox);

                view3D.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
                if (IsolateElements)
                {
                    view3D.IsolateElementsTemporary(ids);
                }

                t.Commit();
            }

            var uiViews = RevitAPI.UIDocument.GetOpenUIViews();
            foreach (UIView uiView in uiViews)
            {
                if (uiView.ViewId == view3D.Id)
                {
                    uiView.ZoomToFit();
                    break;
                }
            }
        }

        [RelayCommand(CanExecute = nameof(IsUIDocumentActive))]
        private void CheckIntersection()
        {
            var elements = SelectedClashResult
                .ClashObjects
                .Select(x => new ElementId(x.Id).ToElement())
                .Where(e => e != null)
                .ToList();

            if (elements.Count == 0)
            {
                MessageWindow.ShowMessage("В проекте отсутствуют элементы для проверки!", MessageBoxImage.Warning);
                return;
            }    

            if (SelectedClashResult.ClashObjects.GroupBy(cl => cl.DocName).First().Count() == 2)
            {
                if (elements.Count == 2)
                {
                    try
                    {
                        var intersection = BooleanOperationsUtils.ExecuteBooleanOperation(elements[0].ToSolid(), elements[1].ToSolid(), BooleanOperationsType.Intersect);
                        if (intersection.Volume == 0)
                        {
                            if (SelectedClashResult.Status != "Исправленная")
                                SetStatus("Исправленная", SelectedClashResult);
                        }
                        else
                        {
                            if (SelectedClashResult.Status != "Активная")
                                SetStatus("Активная", SelectedClashResult);
                        }
                    }
                    catch (Autodesk.Revit.Exceptions.InvalidOperationException)
                    {
                        MessageWindow.ShowMessage("Невозможно проверить данные элементы на пересечение!\n" +
                            "Это может быть связано с геометрическими неточностями в твердых телах, такими как слегка неровные грани или ребра.\n" +
                            "Также проблема может возникать из-за сложной геометрии одного или обоих тел, например, когда более двух граней сходятся вдоль одного ребра, или присутствуют совпадающие ребра и т. д.",
                            MessageBoxImage.Warning);

                        return;
                    }
                }
                else
                {
                    MessageWindow.ShowMessage("В проекте отсутствует один из элементов конфликта", MessageBoxImage.Warning);

                    return;
                }
            }
            else
            {
                var clashObject = SelectedClashResult
                    .ClashObjects
                    .FirstOrDefault(cl => new ElementId(cl.Id).ToElement() == null);

                if (_rvtLinks.Count == 0)
                {
                    _rvtLinks = new FilteredElementCollector(RevitAPI.Document)
                        .OfClass(typeof(RevitLinkInstance))
                        .Cast<RevitLinkInstance>()
                        .ToList();
                }

                var link = _rvtLinks.FirstOrDefault(link => link.Name.Contains(Path.GetFileNameWithoutExtension(clashObject.DocName)));
                var linkDoc = link?.GetLinkDocument();
                if (linkDoc == null)
                {
                    MessageBox.Show($"Связь {Path.GetFileNameWithoutExtension(clashObject.DocName)}.rvt не загружена в проект!", "BIMPlugins",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var transform = link.GetTotalTransform();

                var solid1 = SelectedClashResult
                    .ClashObjects
                    .Select(x => new ElementId(x.Id).ToElement())
                    .FirstOrDefault(e => e != null)
                    .ToSolid();

                var solid2 = new ElementId(clashObject.Id).ToElement(linkDoc).ToSolid();

                try
                {
                    var intersection = BooleanOperationsUtils.ExecuteBooleanOperation(
                        solid1,
                        SolidUtils.CreateTransformed(solid2, transform),
                        BooleanOperationsType.Intersect
                    );
                    
                    if (intersection.Volume == 0)
                    {
                        if (SelectedClashResult.Status != "Исправленная")
                            SetStatus("Исправленная");
                    }
                    else
                    {
                        if (SelectedClashResult.Status != "Активная")
                            SetStatus("Активная");
                    }
                }
                catch (Autodesk.Revit.Exceptions.InvalidOperationException)
                {
                    MessageBox.Show("Невозможно проверить данные элементы на пересечение!\n" +
                        "Это может быть связано с геометрическими неточностями в твердых телах, такими как слегка неровные грани или ребра.\n" +
                        "Также проблема может возникать из-за сложной геометрии одного или обоих тел, например, когда более двух граней сходятся вдоль одного ребра, или присутствуют совпадающие ребра и т. д.",
                        "BIMPlugins", MessageBoxButton.OK, MessageBoxImage.Warning);
                    
                    return;
                }
            }
        }

        [RelayCommand(CanExecute = nameof(IsClashTestSelected))]
        private void Sync()
        {
            var reportDate = File.GetLastWriteTime(XmlFilePath).ToString("dd-MM-yyyy H:mm");
            if (reportDate != ReportDate)
            {
                var selectedTestName = SelectedClashTest.Name;
                var selectedClashResultName = SelectedClashResult.Name;

                _xDoc = XDocument.Load(XmlFilePath);
                ReportDate = reportDate;

                _clashTests.Clear();
                foreach (var xClashTest in _xDoc.Descendants("clashtest").Where(c => c.Descendants("clashresult").Count() != 0))
                {
                    _clashTests.Add(new ClashTest(xClashTest.Attribute("name").Value));
                }

                SelectedClashTest = _clashTests.FirstOrDefault(t => t.Name == selectedTestName);
                if (SelectedClashTest != null)
                {
                    SelectedClashResult = SelectedClashTest.ClashResults.FirstOrDefault(r => r.Name == selectedClashResultName) ?? SelectedClashTest.ClashResults.First();
                }
                else
                {
                    SelectedClashTest = _clashTests.First();
                    SelectedClashResult = SelectedClashTest.ClashResults.First();
                }

                RefreshClashResults(SelectedClashTest);
            }

            GetClashHistories(SelectedClashTest);

            SerializeClashResults();
            SyncTime = DateTime.Now.ToString("H:mm");

            SetActiveClashesPercent();
        }

        private bool IsHistoryItemsEqual(HistoryItem historyItem1, HistoryItem historyItem2)
        {
            return historyItem1.Date == historyItem2.Date && historyItem1.Author == historyItem2.Author && historyItem1.Comment == historyItem2.Comment;
        }

        private XYZ GetRevitPoint(XYZ clashPoint)
        {
            var basePoint = new FilteredElementCollector(RevitAPI.Document)
                .OfCategory(BuiltInCategory.OST_ProjectBasePoint)
                .OfClass(typeof(BasePoint))
                .First() as BasePoint;

            double angle = basePoint.get_Parameter(BuiltInParameter.BASEPOINT_ANGLETON_PARAM).AsDouble();
            double baseX = basePoint.get_Parameter(BuiltInParameter.BASEPOINT_EASTWEST_PARAM).AsDouble();
            double baseY = basePoint.get_Parameter(BuiltInParameter.BASEPOINT_NORTHSOUTH_PARAM).AsDouble();
            double baseZ = basePoint.get_Parameter(BuiltInParameter.BASEPOINT_ELEVATION_PARAM).AsDouble();
            
            double pozitionX = basePoint.Position.X;
            double pozitionY = basePoint.Position.Y;
            double pozitionZ = basePoint.Position.Z;

            double clashX = UnitUtils.ConvertToInternalUnits(clashPoint.X * 1000, ParameterMethods.GetUnitType());
            double clashY = UnitUtils.ConvertToInternalUnits(clashPoint.Y * 1000, ParameterMethods.GetUnitType());
            double clashZ = UnitUtils.ConvertToInternalUnits(clashPoint.Z * 1000, ParameterMethods.GetUnitType());
            
            double resultX = clashX - baseX;
            double resultY = clashY - baseY;
            double resultZ = clashZ - baseZ;

            //rotation
            double centX = (resultX * Math.Cos(angle)) - (resultY * Math.Sin(angle));
            double centY = (resultX * Math.Sin(angle)) + (resultY * Math.Cos(angle));

            return new XYZ(centX + pozitionX, centY + pozitionY, resultZ + pozitionZ);
        }

        private void GetClashResults(ClashTest clashTest)
        {
            clashTest.ClashResults.Clear();

            var xClashTest = _xDoc.Descendants("clashtest").FirstOrDefault(r => r.Attribute("name").Value == clashTest.Name);

            var i = 1;
            foreach (var xClashResult in xClashTest.Descendants("clashresult").OrderBy(x => x.Attribute("name").Value, new NaturalComparer()).ToList())
            {
                var xPoint = xClashResult.Descendants("pos3f").FirstOrDefault();
                var clashResult = new ClashResult()
                {
                    Parent = clashTest,
                    Number = i,
                    Name = xClashResult.Attribute("name")?.Value,
                    ImagePath = _xmlDirectory.AppendPath(xClashResult.Attribute("href")?.Value.Replace(@"\", @"\\") ?? string.Empty),
                    ClashPoint = new XYZ
                    (
                        double.Parse(xPoint?.Attribute("x").Value, CultureInfo.InvariantCulture),
                        double.Parse(xPoint?.Attribute("y").Value, CultureInfo.InvariantCulture),
                        double.Parse(xPoint?.Attribute("z").Value, CultureInfo.InvariantCulture)
                    ),
                    LevelName = xClashResult.Element("gridlocation")?.Value.Split(':')[1].Trim(),
                    CreatedDate = GetCreatedDate(xClashResult.Element("createddate")?.Element("date"))
                };
                
                foreach (var xClashObject in xClashResult.Descendants("clashobject"))
                {
                    var nodes = xClashObject.Descendants("node").ToList();

                    var stringId = xClashObject.Element("smarttags")?
                        .Descendants("name").FirstOrDefault(t => t.Value == "Объект Id")?.Parent.Element("value").Value;

                    if (stringId.IsNullOrEmpty())
                        stringId = xClashObject.Element("objectattribute")?.Element("value").Value;

                    var clashObject = new ClashObject()
                    {
                        Id = stringId != null ? int.Parse(stringId, CultureInfo.InvariantCulture) : 0,
                        DocName = nodes[2].Value,
                        FamilyName = nodes[4].Value,
                        Type = nodes[5].Value,
                    };
                    
                    clashResult.ClashObjects.Add(clashObject);
                }

                i++;
                clashTest.ClashResults.Add(clashResult);
            }

            clashTest.FilteredClashResults = CollectionViewSource.GetDefaultView(clashTest.ClashResults);
            clashTest.FilteredClashResults.Filter = item =>
            {
                var clashResult = (ClashResult)item;

                bool statusFilter = SelectedStatusToFilter.IsNullOrEmpty() || clashResult.Status == SelectedStatusToFilter;
                bool levelFilter = SelectedLevelToFilter.IsNullOrEmpty() || clashResult.LevelName == SelectedLevelToFilter;
                bool sectionFilter = SelectedSectionToFilter.IsNullOrEmpty() || clashResult.AssignedTo == SelectedSectionToFilter || IsLeftEqualRight;
                bool familyNameFilter = SelectedFamilyNameToFilter.IsNullOrEmpty() || clashResult.ClashObjects.Select(cl => cl.FamilyName).Contains(SelectedFamilyNameToFilter);

                bool userFilter = _userFilterViewModel == null ||
                    (ContainsBothElements && clashResult.ClashObjects.Select(cl => cl.Id).All(id => _userFilterViewModel.FilteredIds.Contains(id))) ||
                    (!ContainsBothElements && clashResult.ClashObjects.Select(cl => cl.Id).Any(id => _userFilterViewModel.FilteredIds.Contains(id)));

                return statusFilter && levelFilter && sectionFilter && familyNameFilter && userFilter;
            };
        }
        private string GetCreatedDate(XElement element)
        {
            if (element == null)
                return null;

            var day = int.Parse(element.Attribute("day").Value).ToString("00");
            var month = int.Parse(element.Attribute("month").Value).ToString("00");

            return $"{day}-{month}-{element.Attribute("year").Value}";
        }

        private void RefreshClashResults(ClashTest clashTest)
        {
            clashTest.FilteredClashResults.Refresh();

            var clashResult = clashTest
                .FilteredClashResults.Cast<ClashResult>()
                .Where(c => c.Name == SelectedClashResult?.Name && c.Parent.Name == SelectedClashResult?.Parent.Name)
                .FirstOrDefault();

            SelectedClashResult = clashResult ?? clashTest.FilteredClashResults.Cast<ClashResult>().FirstOrDefault();
        }

        private void GetClashHistories(ClashTest clashTest)
        {
            if (!File.Exists(_syncXDocPath) || clashTest == null)
                return;

            var xDoc = XDocument.Load(_syncXDocPath);

            var xClashTest = xDoc.Descendants("clashTest").FirstOrDefault(r => r.Attribute("name").Value == clashTest.Name);
            if (xClashTest == null) return;

            bool isDocModified = false;
            foreach (var xClashResult in xClashTest.Descendants("clashResult").ToList())
            {
                var clashResult = clashTest.ClashResults.FirstOrDefault(cl => cl.Name == xClashResult.Attribute("name").Value);
                if (clashResult == null)
                {
                    xClashResult.Remove();
                    isDocModified = true;
                    continue;
                }

                foreach (var xComment in xClashResult.Elements("comment"))
                {
                    var historyItem = new HistoryItem(xComment.Element("text").Value)
                    {
                        Author = xComment.Attribute("author").Value,
                        Date = xComment.Attribute("date").Value
                    };

                    if (!clashResult.HistoryItems.Any(h => IsHistoryItemsEqual(h, historyItem)))
                    {
                        clashResult.HistoryItems.Add(historyItem);
                    }
                }

                clashResult.HistoryItems = new(clashResult.HistoryItems.OrderByDescending(c => c.Date));

                var lastStatusHistoryItem = clashResult.HistoryItems.FirstOrDefault(c => c.Comment.StartsWith("Статус:"));
                if (lastStatusHistoryItem != null)
                {
                    var index = lastStatusHistoryItem.Comment.IndexOf("=>");
                    clashResult.Status = lastStatusHistoryItem.Comment.Substring(index + 3);
                    clashResult.HasHistoryItems = true;
                }

                var lastRoleHistoryItem = clashResult.HistoryItems.FirstOrDefault(c => c.Comment.StartsWith("Выбран новый отв"));
                if (lastRoleHistoryItem != null)
                {
                    var index = lastRoleHistoryItem.Comment.IndexOf("Отдел");
                    clashResult.AssignedTo = lastRoleHistoryItem.Comment.Substring(index + 6).Split('\n')[0];
                    clashResult.IsLeftSelected = clashResult.AssignedTo == LeftSection;
                    clashResult.HasHistoryItems = true;

                    if (lastRoleHistoryItem.Comment.Split('\n').Count() == 2)
                        clashResult.LastComment = lastRoleHistoryItem.Comment.Split('\n')[1];
                }

                if (clashResult.LastComment.IsNullOrEmpty())
                {
                    var lastCommentItem = clashResult.HistoryItems.FirstOrDefault(c => !c.Comment.StartsWith("Статус:") && !c.Comment.StartsWith("Выбран новый отв"));
                    if (lastCommentItem != null)
                        clashResult.LastComment = lastCommentItem.Comment;
                }
                else
                {
                    var firstItem = clashResult.HistoryItems.FirstOrDefault(c => !c.Comment.StartsWith("Статус:"));
                    if (firstItem != null && !firstItem.Comment.StartsWith("Выбран новый отв"))
                    {
                        clashResult.LastComment = firstItem.Comment;
                    }
                }
            }

            if (isDocModified && IsWorkFile)
            {
                if (xClashTest.Descendants("clashResult").Count() == 0)
                {
                    xClashTest.Remove();
                }

                SaveHistory(xDoc);
            }
        }
        private void GetOldTypeClashHistories(ClashTest clashTest)
        {
            var path = Directory.GetFiles(_xmlDirectory, "*_Синхронизация.xml", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (path == null) return;

            var xDoc = XDocument.Load(path);

            var xClashTest = xDoc.Descendants("clahtest").FirstOrDefault(r => r.Attribute("name").Value == clashTest.Name);
            if (xClashTest == null) return;

            foreach (var xClashResult in xClashTest.Descendants("clash").ToList())
            {
                var clashResult = clashTest.ClashResults.FirstOrDefault(cl => cl.Name == xClashResult.Attribute("name").Value);
                if (clashResult == null)
                {
                    continue;
                }

                var status = xClashResult.Attribute("status").Value;
                if (status == "В работе")
                    status = "Активная";

                var comment = xClashResult.Attribute("coment").Value;
                var author = xClashResult.Attribute("autor").Value;

                var historyItem = comment.IsNullOrEmpty()
                    ? new HistoryItem("Активная", status)
                    {
                        Author = author,
                        Date = string.Empty
                    }
                    : new HistoryItem(comment)
                    {
                        Author = author,
                        Date = string.Empty
                    };

                if (!clashResult.HistoryItems.Any(h => IsHistoryItemsEqual(h, historyItem)) && !(comment.IsNullOrEmpty() && status == "Активная"))
                {
                    clashResult.HistoryItems.Add(historyItem);
                }

                clashResult.HistoryItems = new(clashResult.HistoryItems.OrderByDescending(c => c.Date));

                var lastComment = clashResult.HistoryItems.FirstOrDefault(c => !c.Comment.StartsWith("Статус:") && !c.Comment.StartsWith("Выбран новый отв"))?.Comment;
                if (lastComment != null && clashResult.LastComment.IsNullOrEmpty())
                    clashResult.LastComment = lastComment;

                var lastStatusHistoryItem = clashResult.HistoryItems.FirstOrDefault(c => c.Comment.StartsWith("Статус:"))?.Comment;
                if (lastStatusHistoryItem != null)
                {
                    var index = lastStatusHistoryItem.IndexOf("=>");
                    clashResult.Status = lastStatusHistoryItem.Substring(index + 3);
                }
                else
                {
                    clashResult.Status = status;
                }

                clashResult.IsModified = true;
                clashResult.HasHistoryItems = true;
            }

            if (IsWorkFile)
                SerializeClashResults();
        }
        private void SerializeClashResults()
        {
            var xDoc = XDocument.Load(_syncXDocPath);

            var root = xDoc.Element("clashTests");
            if (root == null)
            {
                root = new XElement("clashTests");
                xDoc.Add(root);
            }

            bool isDocModified = false;
            foreach (var clashTest in _clashTests.Where(cl => cl.ClashResults.Any(c => c.IsModified)))
            {
                isDocModified = true;

                var xClashTest = xDoc.Descendants("clashTest").FirstOrDefault(r => r.Attribute("name").Value == clashTest.Name);
                
                if (xClashTest == null)
                {
                    xClashTest = new XElement("clashTest",
                        new XAttribute("name", clashTest.Name)
                    );

                    root.Add(xClashTest);
                }

                var xClashResults = xClashTest.Element("clashResults");
                if (xClashResults == null)
                {
                    xClashResults = new XElement("clashResults");
                    xClashTest.Add(xClashResults);
                }

                foreach (var clashResult in clashTest.ClashResults.Where(r => r.IsModified).ToList())
                {
                    clashResult.IsModified = false;

                    var xClashResult = xClashTest.Descendants("clashResult").FirstOrDefault(r => r.Attribute("name").Value == clashResult.Name);
                    xClashResult?.Remove();

                    xClashResult = new XElement("clashResult",
                        new XAttribute("name", clashResult.Name),
                        new XAttribute("status", clashResult.Status),

                        from historyItem in clashResult.HistoryItems
                        select new XElement("comment",
                            new XAttribute("author", historyItem.Author),
                            new XAttribute("date", historyItem.Date),
                            new XElement("text", historyItem.Comment)
                        )
                    );

                    xClashResults.Add(xClashResult);

                    clashResult.HistoryItems.ToList().ForEach(hi => hi.OldStatus = null);
                }
            }

            if (isDocModified)
            {
                SaveHistory(xDoc);
            }
        }

        private void SaveHistory(XDocument xDoc)
        {
            if (_mutex == null)
                _mutex = new Mutex(false, "Global\\SEPlugins_SyncXml");

            var acquired = _mutex.WaitOne(TimeSpan.FromSeconds(5));

            if (!acquired)
            {
                MessageWindow.ShowMessage("Превышено время ожидания.\nПовторите попытку позже", MessageBoxImage.Warning);

                return;
            }

            try
            {
                xDoc.Save(_syncXDocPath);
            }
            catch { MessageWindow.ShowMessage("Ошибка при синхронизации!\nПовторите попытку позже", MessageBoxImage.Warning); }
            finally { _mutex.ReleaseMutex(); }
        }
        private void ArchivateHistory()
        {
            var newXmlFileName = $"{Path.GetFileNameWithoutExtension(_syncXDocPath)}_{DateTime.Today:ddMMyy}.xml";
            if (!File.Exists(Path.Combine(Path.GetDirectoryName(_syncXDocPath), "Архив", newXmlFileName)))
            {
                try
                {
                    File.Copy(_syncXDocPath, Path.Combine(Path.GetDirectoryName(_syncXDocPath), "Архив", newXmlFileName), true);
                }
                catch {}
            }
        }

        private bool IsUIDocumentActive() => RevitAPI.UIDocument != null;
        private bool IsWriteCommentEnabled() => !NewCommentText.IsNullOrEmpty();
        private bool IsClashTestSelected() => SelectedClashTest != null;
    }
}