using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ClashViewer;
using BIMPlugins.Common;
using BIMPlugins.Common.WPF;
using BIMPlugins.Docs;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.Families;
using BIMPlugins.Levels;
using BIMPlugins.Parameters;
using BIMPlugins.Sheets;
using BIMPlugins.UI;
using BIMPlugins.UI.Utils;
using BIMPlugins.Views;
using BIMPlugins.Windows.ViewModels;
using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace BIMPlugins
{
    public class BIMPlugins_Application : IExternalApplication
    {
        public static UIControlledApplication _uiControlApp;
        public static string tabName = "BIMPlugins";

        private static Autodesk.Windows.RibbonPanel _modifyPanel;
        private static Autodesk.Windows.RibbonButton _whoDidButton;
        private static Autodesk.Windows.RibbonButton _fastSelectButton;

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }

        public Result OnStartup(UIControlledApplication application)
        {
            _uiControlApp = application;

            _uiControlApp.FindOrCreateRibbonTab(tabName);
            InitializeDlls();
            
            try
            {
                var viewModel = new ViewSettingsViewModel();
                var window = new ViewSettingWindow(viewModel, _uiControlApp);

                _uiControlApp.RegisterDockablePane(ViewSettingWindow.PaneId, "Управление видимостью", window);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\n\n" + ex.StackTrace);
                return Result.Failed;
            }

            //Утилиты
            RibbonPanel commonPanel = application.CreateRibbonPanel(tabName, "Утилиты");

            var whoDidButton = commonPanel.CreatePushButton<WhoDidCmd, NotAvailableInFamilyEditor>("Кто сделал\nэто?").SetShowText()
                .SetLargeImage("angry-32.png")
                .SetToolTip("Позволяет узнать создателя, владельца и последнего редактора выбранного элемента")
                .SetContextualHelp(@"B:\00_Библиотека\1_Инструкции\Общие\BIMPlugins\Инструкция Кто сделал это.pdf");

            var projectPanelLargeStackedItems = new PushButton[]
            {
                whoDidButton,
                commonPanel.CreatePushButton<RotateElementsCmd, NotAvailableInFamilyEditor>("Повернуть").SetShowText()
                    .SetLargeImage("rotate-32.png")
                    .SetToolTip("Поворачивает элементы вокруг своей оси на заданный угол")
                    .SetContextualHelp(@"B:\00_Библиотека\1_Инструкции\Общие\BIMPlugins\Инструкция Повернуть.pdf"),
                commonPanel.CreatePushButton<SuperFilterCmd, NotAvailableInFamilyEditor>("Суперфильтр").SetShowText()
                    .SetLargeImage("superFilter.tiff")
                    .SetToolTip("Фильтр элементов по параметрам")
                    .SetContextualHelp(@"B:\00_Библиотека\1_Инструкции\Общие\BIMPlugins\Инструкция Суперфильтр.pdf"),
                commonPanel.CreatePushButton<SumCmd, NotAvailableInFamilyEditor>("Сумма").SetShowText()
                    .SetLargeImage("calculator-32.png")
                    .SetToolTip("Сумма параметров")
                    .SetContextualHelp(@"B:\00_Библиотека\1_Инструкции\Общие\BIMPlugins\Инструкция Сумма.pdf"),
            };
            commonPanel.RowLargeStackedItems(projectPanelLargeStackedItems);

            var projectPanelStackedItems = new PushButton[]
            {
                commonPanel.CreatePushButton<MirrorCmd, NotAvailableInFamilyEditor>("Зеркало").SetShowText()
                    .SetLargeImage("mirror-32.png")
                    .SetToolTip("Поиск отзеркаленных семейств"),
                commonPanel.CreatePushButton<ViewSettingsCmd, NotAvailableInFamilyEditor>("Настройка\nвидимости").SetShowText()
                    .SetLargeImage("bulb-32.png")
                    .SetToolTip("Настроить видимость фильтров и рабочих наборов")
                    .SetContextualHelp(@"B:\00_Библиотека\1_Инструкции\Общие\BIMPlugins\Инструкция Настройка видимости.pdf"),
                commonPanel.CreatePushButton<GetFaceAreaCmd, NotAvailableInFamilyEditor>("Площадь\nграней").SetShowText()
                    .SetLargeImage("areaSum-32.png")
                    .SetToolTip("Вычисляет суммарную площадь выбранных граней")
                    .SetContextualHelp(@"B:\00_Библиотека\1_Инструкции\Общие\BIMPlugins\Инструкция Площадь граней.pdf")
            };
            commonPanel.RowStackedItems(projectPanelStackedItems);

            //Изменить
            var tempPanel = _uiControlApp.CreateOrSelectPanel("Temp");

            _whoDidButton = whoDidButton.ToAWRibbonItem<Autodesk.Windows.RibbonButton>().Clone() as Autodesk.Windows.RibbonButton;
            _whoDidButton.ShowText = true;
            _whoDidButton.Orientation = System.Windows.Controls.Orientation.Vertical;
            
            _fastSelectButton = tempPanel.CreatePushButton<FastSelectCmd, NotAvailableInFamilyEditor>("Быстрый\nвыбор")
                .SetLargeImage("fastSelect-32.png")
                .SetToolTip("Позволяет выбрать элементы одиннаковой категории или все экземпляры одного семейства")
                .ToAWRibbonItem<Autodesk.Windows.RibbonButton>();

            tempPanel.Remove();

            _modifyPanel = RibbonModifyUtils.CreateRibbonPanel(tabName, [_whoDidButton, _fastSelectButton]);

            var modifyTab = RibbonModifyUtils.RibbonTab();
            modifyTab.Panels.CollectionChanged += new NotifyCollectionChangedEventHandler(OnCollectionChanged);

            //Коллизии
            RibbonPanel nwcPanel = application.CreateRibbonPanel(tabName, "Коллизии");
            nwcPanel.CreatePushButton<ClashViewerCmd, AlwaysAvailable>("Просмотр").SetShowText(true)
                .SetLargeImage("clashManager.tiff")
                .SetToolTip("Позволяет просматривать коллизии из отчета Navisworks")
                .SetContextualHelp(@"B:\00_Библиотека\1_Инструкции\Общие\BIMPlugins\Инструкция Просмотр коллизий_Новый.pdf");

            //Виды
            RibbonPanel viewsPanel = application.CreateRibbonPanel(tabName, "Виды");
            viewsPanel.CreatePushButton<ColourFilterCmd, NotAvailableInFamilyEditor>("Цветные\nфильтры").SetShowText(true)
                .SetLargeImage("colorFilter.tiff")
                .SetToolTip("Позволяет создавать цветные фильтры на виде по значениям выбранного параметра выбранных категорий")
                .SetContextualHelp(@"B:\00_Библиотека\1_Инструкции\Общие\BIMPlugins\Инструкция Цветные фильтры.pdf");
            viewsPanel.CreatePushButton<ImageFromLegendCmd, NotAvailableInFamilyEditor>("Изображения\nиз легенд").SetShowText(true)
                .SetLargeImage("imageFromLegend-32.png")
                .SetToolTip("Позволяет сохранить в проекте изображения из легенд")
                .SetContextualHelp(@"B:\00_Библиотека\1_Инструкции\Общие\BIMPlugins\Инструкция Изображения из легенд.pdf");
            viewsPanel.CreatePushButton<SectionBoxCmd, NotAvailableInFamilyEditor>("Обрезка\nпо уровням").SetShowText(true)
                .SetLargeImage(@"/BIMPlugins.Windows;component/Resources/sectionBox-32.png")
                .SetToolTip("Обрезка 3D-вида по выбранным уровням")
                .SetContextualHelp(@"B:\00_Библиотека\1_Инструкции\Общие\BIMPlugins\Инструкция Обрезка по уровням.pdf");
            viewsPanel.CreatePushButton<CropBoxOn3DCmd, NotAvailableInFamilyEditor>("Секущий\nдиапазон").SetShowText(true)
                .SetLargeImage("cropBox-32.png")
                .SetToolTip("Показ секущего диапазона видов в цвете на 3D")
                .SetContextualHelp(@"B:\00_Библиотека\1_Инструкции\Общие\BIMPlugins\Инструкция Секущий диапазон.pdf");
            viewsPanel.CreatePushButton<ActivateByIdCmd, NotAvailableInFamilyEditor>("Перейти\nпо Id").SetShowText(true)
                .SetLargeImage("getSheetById.tiff")
                .SetToolTip("Позволяет открыть вид по его Id или по Id элемента, привязанному к этому виду, из буфера обмена");

            //Листы
            RibbonPanel sheetsPanel = application.CreateRibbonPanel(tabName, "Листы");
            sheetsPanel.CreatePushButton<SpecificationScheduleCmd, NotAvailableInFamilyEditor>("Ведомость\nспецификаций").SetShowText(true)
                .SetLargeImage("specification.tiff")
                .SetToolTip("Позволяет создать ведомость спецификаций в Revit")
                .SetContextualHelp(@"B:\00_Библиотека\1_Инструкции\Общие\BIMPlugins\Инструкция Ведомость спецификаций.pdf");
            sheetsPanel.CreatePushButton<WhereIsViewCmd, NotAvailableInFamilyEditor>("Где вид?").SetShowText(true)
                .SetLargeImage("whereIsView-32.png")
                .SetToolTip("Позволяет быстро определять на каком листе располагается активный вид")
                .SetContextualHelp(@"B:\00_Библиотека\1_Инструкции\Общие\BIMPlugins\Инструкция Где вид.pdf");

            var sheetsPanelStackedItems = new PushButton[]
            {
                sheetsPanel.CreatePushButton<CopySheetsCmd, NotAvailableInFamilyEditor>("Дубликатор листов").SetShowText(true)
                    .SetLargeImage("copySheets-32.png")
                    .SetToolTip("Позволяет копировать листы с заданными настройками")
                    .SetContextualHelp(@"B:\00_Библиотека\1_Инструкции\Общие\BIMPlugins\Инструкция Дубликатор листов.pdf"),
                sheetsPanel.CreatePushButton<SheetsNumberingCmd, NotAvailableInFamilyEditor>("Нумератор листов").SetShowText(true)
                    .SetLargeImage("renumberSheets-32.png")
                    .SetToolTip("Позволяет производить перенумерацию листов в рамках одной группы листов")
                    .SetContextualHelp(@"B:\00_Библиотека\1_Инструкции\Общие\BIMPlugins\Инструкция Нумератор листов.pdf"),
                sheetsPanel.CreatePushButton<CopyStampCmd, NotAvailableInFamilyEditor>("Копировать штамп").SetShowText(true)
                    .SetLargeImage("copyStamp-32.png")
                    .SetToolTip("Позволяет скопировать заполнения штампа с выбранного листа на выбранные листы")
                    .SetContextualHelp(@"B:\00_Библиотека\1_Инструкции\Общие\BIMPlugins\Инструкция Копировать штамп.pdf"),
            };
            sheetsPanel.RowStackedItems(sheetsPanelStackedItems);

            //Параметры
            RibbonPanel parametersPanel = application.CreateRibbonPanel(tabName, "Параметры");
            parametersPanel.CreatePushButton<SetParameterValueCmd, NotAvailableInFamilyEditor>("Заполнятор").SetShowText(true)
                .SetLargeImage("setParameterValue.tiff")
                .SetToolTip("Позволяет заполнить параметр у выбранных элементов по выбранной логике работы")
                .SetContextualHelp(@"B:\00_Библиотека\1_Инструкции\Общие\BIMPlugins\Инструкция Заполнятор.pdf");
            parametersPanel.CreatePushButton<CopyPropertiesCmd, NotAvailableInFamilyEditor>("Копирование\nсвойств").SetShowText(true)
                .SetLargeImage("copyProperties.tiff")
                .SetToolTip("Позволяет скопировать свойства с одного элемента на другой")
                .SetContextualHelp(@"B:\00_Библиотека\1_Инструкции\Общие\BIMPlugins\Инструкция Копирование свойств.pdf");
            parametersPanel.CreatePushButton<NumerateCmd, NotAvailableInFamilyEditor>("Нумерация").SetShowText(true)
                .SetLargeImage("numerate.tiff")
                .SetToolTip("Позволяет добавить нумерацию в указанный параметр элементов")
                .SetContextualHelp(@"B:\00_Библиотека\1_Инструкции\Общие\BIMPlugins\Инструкция Нумерация.pdf");

            //Уровни
            RibbonPanel levelPanel = application.CreateRibbonPanel(tabName, "Уровни");
            levelPanel.CreatePushButton<MoveLevelCmd, NotAvailableInFamilyEditor>("Переместить\nуровень").SetShowText(true)
                .SetLargeImage("moveLevel.tiff")
                .SetToolTip("Позволяет переместить уровень на нужную отметку, не меняя положение элементов в модели")
                .SetContextualHelp(@"X:\03_BIM отдел\02_Инструкции\Инструкции к BIMPlugins\Инструкция Переместить уровень.pdf");
            levelPanel.CreatePushButton<SetNewLevelCmd, NotAvailableInFamilyEditor>("Назначить\nуровень").SetShowText(true)
                .SetLargeImage("setNewLevel.tiff")
                .SetToolTip("Позволяет изменить привязку элемента к уровню")
                .SetContextualHelp(@"X:\03_BIM отдел\02_Инструкции\Инструкции к BIMPlugins\Инструкция Назначить уровень.pdf");

            //Документы
            RibbonPanel docsPanel = application.CreateRibbonPanel(tabName, "Документы");
            var serverSplitBtn = docsPanel.CreateSplitButton("RevitServer");
            serverSplitBtn.CreatePushButton<RSNInfoCmd, AlwaysAvailable>("Найти\nпроект").SetShowText(true)
                .SetLargeImage("serverInfo.tiff")
                .SetToolTip("Позволяет найти и открыть файл с Revit Server");
            serverSplitBtn.CreatePushButton<MakeRSNCmd, AlwaysAvailable>("Создать\nструктуру").SetShowText(true)
                .SetLargeImage("serverCreate.tiff")
                .SetToolTip("Позволяет создать структуру RSN для использования в других плагинах");

            docsPanel.CreatePushButton<RelinquishCmd, AlwaysAvailable>("Освободить\nэлементы").SetShowText(true)
                .SetLargeImage("relinquish.tiff")
                .SetToolTip("Позволяет освободить все забранные элементы в проектах");
            docsPanel.CreatePushButton<CloseDocsCmd, AlwaysAvailable>("Закрыть").SetShowText(true)
                .SetLargeImage("closeDocks.tiff")
                .SetToolTip("При ошибке плагинов фоновые документы не закрываются и хранятся в памяти");

            //Семейства
            RibbonPanel familyPanel = application.CreateRibbonPanel(tabName, "Семейства");
            familyPanel.CreatePushButton<SaveFamilyCmd, AvailableInFamilyEditor>("Сохранить").SetShowText(true)
                .SetLargeImage("saveFamily.tiff")
                .SetToolTip("Позволяет пересохранить семейство без увеличения размера файла");
            familyPanel.CreatePushButton<BindParametersCmd, AvailableInFamilyEditor>("Связать\nпараметры").SetShowText(true)
                .SetLargeImage("bindFamilies.tiff")
                .SetToolTip("Позволяет связать параметры между родительским и вложенным семействами");

            return Result.Succeeded;
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null && RevitAPI.UIDocument?.ToSelectedElements().Count() == 1)
            {
                _modifyPanel.IsVisible = true;
                _fastSelectButton.IsVisible = true;
                _whoDidButton.IsVisible = RevitAPI.Document.IsWorkshared;
            }
            if (e.OldItems != null || RevitAPI.UIDocument?.ToSelectedElements().Count() != 1)
            {
                _modifyPanel.IsVisible = false;
                _whoDidButton.IsVisible = false;
                _fastSelectButton.IsVisible = false;
            }
        }

        private void InitializeDlls()
        {
            var card = new Card();
            var hue = new Hue("Dammy", Colors.Black, Colors.White);

            var vm = new MessageViewModel("1", MessageBoxImage.None, true);
        }
    }

    public class AlwaysAvailable : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories) => applicationData != null;
    }
    public class AvailableInFamilyEditor : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
        {
            var doc = applicationData.ActiveUIDocument?.Document;
            return doc != null && doc.IsFamilyDocument;
        }
    }
    public class NotAvailableInFamilyEditor : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
        {
            var doc = applicationData.ActiveUIDocument?.Document;
            return doc != null && !doc.IsFamilyDocument;
        }
    }
}