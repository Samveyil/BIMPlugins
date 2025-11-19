using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Windows;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using ricaun.Revit.UI;
using BIMPlugins.ClashViewer;
using BIMPlugins.Common;
using BIMPlugins.Common.WPF;
using BIMPlugins.Levels;
using BIMPlugins.Parameters;
using BIMPlugins.Sheets;
using BIMPlugins.Views;
using System.Collections.Specialized;
using System.Windows;
using RibbonPanel = Autodesk.Revit.UI.RibbonPanel;
using BIMPlugins.ExtStorage.Methods;
using System;
using System.Linq;

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

            UIMethods.FindTab(_uiControlApp, tabName);
            RevitAPI.InitializeMaterialDesign();
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

            //Изменить
            _modifyPanel = UIMethods.CreateAWPanel();
            
            var modifyTab = ComponentManager.Ribbon.Tabs.FirstOrDefault(t => t.Id == "Modify");
            modifyTab.Panels.Add(_modifyPanel);
            modifyTab.Panels.CollectionChanged += new NotifyCollectionChangedEventHandler(OnCollectionChanged);

            //Утилиты
            RibbonPanel commonPanel = application.CreateRibbonPanel(tabName, "Утилиты");

            var whoDidButton = commonPanel.CreatePushButton<WhoDidCommand, NotAvailableInFamilyEditor>("Кто сделал\nэто?").SetShowText()
                .SetLargeImage(UIMethods.GetImagePath("BIMPlugins", "angry-32.png"))
                .SetToolTip("Позволяет узнать создателя, владельца и последнего редактора выбранного элемента")
                .SetContextualHelp(@"B:\00_Библиотека\1_Инструкции\Общие\BIMPlugins\Инструкция Кто сделал это.pdf");

            _whoDidButton = whoDidButton.GetRibbonItem<Autodesk.Windows.RibbonButton>().Clone() as Autodesk.Windows.RibbonButton;
            _whoDidButton.ShowText = true;

            var fastButton = commonPanel.CreatePushButton<FastSelectCommand, NotAvailableInFamilyEditor>("Быстрый\nвыбор").SetShowText()
                .SetLargeImage(UIMethods.GetImagePath("BIMPlugins", "fastSelect-32.png"))
                .SetToolTip("Позволяет выбрать элементы одиннаковой категории или все экземпляры одного семейства");

            _fastSelectButton = fastButton.GetRibbonItem<Autodesk.Windows.RibbonButton>().Clone() as Autodesk.Windows.RibbonButton;
            _fastSelectButton.ShowText = true;

            commonPanel.Remove(fastButton);

            _modifyPanel.Source.Items.Add(_whoDidButton);
            _modifyPanel.Source.Items.Add(_fastSelectButton);

            var projectPanelLargeStackedItems = new PushButton[]
            {
                whoDidButton,
                commonPanel.CreatePushButton<RotateElementsCommand, NotAvailableInFamilyEditor>("Повернуть").SetShowText()
                    .SetLargeImage(UIMethods.GetImagePath("BIMPlugins", "rotate-32.png"))
                    .SetToolTip("Поворачивает элементы вокруг своей оси на заданный угол")
                    .SetContextualHelp(@"B:\00_Библиотека\1_Инструкции\Общие\BIMPlugins\Инструкция Повернуть.pdf"),
                commonPanel.CreatePushButton<SuperFilterCommand, NotAvailableInFamilyEditor>("Суперфильтр").SetShowText()
                    .SetLargeImage(UIMethods.GetImagePath("BIMPlugins", "superFilter.tiff"))
                    .SetToolTip("Фильтр элементов по параметрам")
                    .SetContextualHelp(@"B:\00_Библиотека\1_Инструкции\Общие\BIMPlugins\Инструкция Суперфильтр.pdf"),
                commonPanel.CreatePushButton<SumCommand, NotAvailableInFamilyEditor>("Сумма").SetShowText()
                    .SetLargeImage(UIMethods.GetImagePath("BIMPlugins", "calculator-32.png"))
                    .SetToolTip("Сумма параметров")
                    .SetContextualHelp(@"B:\00_Библиотека\1_Инструкции\Общие\BIMPlugins\Инструкция Сумма.pdf"),
            };
            commonPanel.RowLargeStackedItems(projectPanelLargeStackedItems);

            var projectPanelStackedItems = new PushButton[]
            {
                commonPanel.CreatePushButton<MirrorCommand, NotAvailableInFamilyEditor>("Зеркало").SetShowText()
                    .SetLargeImage(UIMethods.GetImagePath("BIMPlugins", "mirror-32.png"))
                    .SetToolTip("Поиск отзеркаленных семейств"),
                commonPanel.CreatePushButton<ViewSettingsCommand, NotAvailableInFamilyEditor>("Настройка\nвидимости").SetShowText()
                    .SetLargeImage(UIMethods.GetImagePath("BIMPlugins", "bulb-32.png"))
                    .SetToolTip("Настроить видимость фильтров и рабочих наборов")
                    .SetContextualHelp(@"B:\00_Библиотека\1_Инструкции\Общие\BIMPlugins\Инструкция Настройка видимости.pdf"),
                commonPanel.CreatePushButton<GetFaceAreaCommand, NotAvailableInFamilyEditor>("Площадь\nграней").SetShowText()
                    .SetLargeImage(UIMethods.GetImagePath("BIMPlugins", "areaSum-32.png"))
                    .SetToolTip("Вычисляет суммарную площадь выбранных граней")
                    .SetContextualHelp(@"B:\00_Библиотека\1_Инструкции\Общие\BIMPlugins\Инструкция Площадь граней.pdf")
            };
            commonPanel.RowStackedItems(projectPanelStackedItems);

            //Коллизии
            RibbonPanel nwcPanel = application.CreateRibbonPanel(tabName, "Коллизии");
            nwcPanel.CreatePushButton<ClashViewerCommand, AlwaysAvailability>("Просмотр").SetShowText(true)
                .SetLargeImage(UIMethods.GetImagePath("BIMPlugins", "clashManager.tiff"))
                .SetToolTip("Позволяет просматривать коллизии из отчета Navisworks")
                .SetContextualHelp(@"B:\00_Библиотека\1_Инструкции\Общие\BIMPlugins\Инструкция Просмотр коллизий_Новый.pdf");

            //Виды
            RibbonPanel viewsPanel = application.CreateRibbonPanel(tabName, "Виды");
            viewsPanel.CreatePushButton<ColourFilterCommand, NotAvailableInFamilyEditor>("Цветные\nфильтры").SetShowText(true)
                .SetLargeImage(UIMethods.GetImagePath("BIMPlugins", "colorFilter.tiff"))
                .SetToolTip("Позволяет создавать цветные фильтры на виде по значениям выбранного параметра выбранных категорий")
                .SetContextualHelp(@"B:\00_Библиотека\1_Инструкции\Общие\BIMPlugins\Инструкция Цветные фильтры.pdf");
            viewsPanel.CreatePushButton<ImageFromLegendCommand, NotAvailableInFamilyEditor>("Изображения\nиз легенд").SetShowText(true)
                .SetLargeImage(UIMethods.GetImagePath("BIMPlugins", "imageFromLegend-32.png"))
                .SetToolTip("Позволяет сохранить в проекте изображения из легенд")
                .SetContextualHelp(@"B:\00_Библиотека\1_Инструкции\Общие\BIMPlugins\Инструкция Изображения из легенд.pdf");
            viewsPanel.CreatePushButton<SectionBoxCommand, NotAvailableInFamilyEditor>("Обрезка\nпо уровням").SetShowText(true)
                .SetLargeImage(@"/BIMPlugins.ExtStorage;component/Resources/sectionBox-32.png")
                .SetToolTip("Обрезка 3D-вида по выбранным уровням")
                .SetContextualHelp(@"B:\00_Библиотека\1_Инструкции\Общие\BIMPlugins\Инструкция Обрезка по уровням.pdf");
            viewsPanel.CreatePushButton<CropBoxOn3DCommand, NotAvailableInFamilyEditor>("Секущий\nдиапазон").SetShowText(true)
                .SetLargeImage(UIMethods.GetImagePath("BIMPlugins", "cropBox-32.png"))
                .SetToolTip("Показ секущего диапазона видов в цвете на 3D")
                .SetContextualHelp(@"B:\00_Библиотека\1_Инструкции\Общие\BIMPlugins\Инструкция Секущий диапазон.pdf");
            viewsPanel.CreatePushButton<ActivateByIdCommand, NotAvailableInFamilyEditor>("Перейти\nпо Id").SetShowText(true)
                .SetLargeImage(UIMethods.GetImagePath("BIMPlugins", "view-32.png"))
                .SetToolTip("Позволяет открыть вид по его Id из буфера обмена");

            //Листы
            RibbonPanel sheetsPanel = application.CreateRibbonPanel(tabName, "Листы");
            sheetsPanel.CreatePushButton<SpecificationScheduleCommand, NotAvailableInFamilyEditor>("Ведомость\nспецификаций").SetShowText(true)
                .SetLargeImage(UIMethods.GetImagePath("BIMPlugins", "specification.tiff"))
                .SetToolTip("Позволяет создать ведомость спецификаций в Revit")
                .SetContextualHelp(@"B:\00_Библиотека\1_Инструкции\Общие\BIMPlugins\Инструкция Ведомость спецификаций.pdf");
            sheetsPanel.CreatePushButton<WhereIsViewCommand, NotAvailableInFamilyEditor>("Где вид?").SetShowText(true)
                .SetLargeImage(UIMethods.GetImagePath("BIMPlugins", "whereIsView-32.png"))
                .SetToolTip("Позволяет быстро определять на каком листе располагается активный вид")
                .SetContextualHelp(@"B:\00_Библиотека\1_Инструкции\Общие\BIMPlugins\Инструкция Где вид.pdf");

            var sheetsPanelStackedItems = new PushButton[]
            {
                sheetsPanel.CreatePushButton<CopySheetsCommand, NotAvailableInFamilyEditor>("Дубликатор листов").SetShowText(true)
                    .SetLargeImage(UIMethods.GetImagePath("BIMPlugins", "copySheets-32.png"))
                    .SetToolTip("Позволяет копировать листы с заданными настройками")
                    .SetContextualHelp(@"B:\00_Библиотека\1_Инструкции\Общие\BIMPlugins\Инструкция Дубликатор листов.pdf"),
                sheetsPanel.CreatePushButton<SheetsNumberingCommand, NotAvailableInFamilyEditor>("Нумератор листов").SetShowText(true)
                    .SetLargeImage(UIMethods.GetImagePath("BIMPlugins", "renumberSheets-32.png"))
                    .SetToolTip("Позволяет производить перенумерацию листов в рамках одной группы листов")
                    .SetContextualHelp(@"B:\00_Библиотека\1_Инструкции\Общие\BIMPlugins\Инструкция Нумератор листов.pdf"),
                sheetsPanel.CreatePushButton<CopyStampCommand, NotAvailableInFamilyEditor>("Копировать штамп").SetShowText(true)
                    .SetLargeImage(UIMethods.GetImagePath("BIMPlugins", "copyStamp-32.png"))
                    .SetToolTip("Позволяет скопировать заполнения штампа с выбранного листа на выбранные листы")
                    .SetContextualHelp(@"B:\00_Библиотека\1_Инструкции\Общие\BIMPlugins\Инструкция Копировать штамп.pdf"),
            };
            sheetsPanel.RowStackedItems(sheetsPanelStackedItems);

            //Параметры
            RibbonPanel parametersPanel = application.CreateRibbonPanel(tabName, "Параметры");
            parametersPanel.CreatePushButton<SetParameterValueCommand, NotAvailableInFamilyEditor>("Заполнятор").SetShowText(true)
                .SetLargeImage(UIMethods.GetImagePath("BIMPlugins", "setParameterValue.tiff"))
                .SetToolTip("Позволяет заполнить параметр у выбранных элементов по выбранной логике работы")
                .SetContextualHelp(@"B:\00_Библиотека\1_Инструкции\Общие\BIMPlugins\Инструкция Заполнятор.pdf");
            parametersPanel.CreatePushButton<CopyPropertiesCommand, NotAvailableInFamilyEditor>("Копирование\nсвойств").SetShowText(true)
                .SetLargeImage(UIMethods.GetImagePath("BIMPlugins", "copyProperties.tiff"))
                .SetToolTip("Позволяет скопировать свойства с одного элемента на другой")
                .SetContextualHelp(@"B:\00_Библиотека\1_Инструкции\Общие\BIMPlugins\Инструкция Копирование свойств.pdf");
            parametersPanel.CreatePushButton<NumerateCommand, NotAvailableInFamilyEditor>("Нумерация").SetShowText(true)
                .SetLargeImage(UIMethods.GetImagePath("BIMPlugins", "numerate.tiff"))
                .SetToolTip("Позволяет добавить нумерацию в указанный параметр элементов")
                .SetContextualHelp(@"B:\00_Библиотека\1_Инструкции\Общие\BIMPlugins\Инструкция Нумерация.pdf");

            //Уровни
            RibbonPanel levelPanel = application.CreateRibbonPanel(tabName, "Уровни");
            levelPanel.CreatePushButton<MoveLevelCommand, NotAvailableInFamilyEditor>("Переместить\nуровень").SetShowText(true)
                .SetLargeImage(UIMethods.GetImagePath("BIMPlugins", "moveLevel.tiff"))
                .SetToolTip("Позволяет переместить уровень на нужную отметку, не меняя положение элементов в модели")
                .SetContextualHelp(@"X:\03_BIM отдел\02_Инструкции\Инструкции к BIMPlugins\Инструкция Переместить уровень.pdf");
            levelPanel.CreatePushButton<SetNewLevelCommand, NotAvailableInFamilyEditor>("Назначить\nуровень").SetShowText(true)
                .SetLargeImage(UIMethods.GetImagePath("BIMPlugins", "setNewLevel.tiff"))
                .SetToolTip("Позволяет изменить привязку элемента к уровню")
                .SetContextualHelp(@"X:\03_BIM отдел\02_Инструкции\Инструкции к BIMPlugins\Инструкция Назначить уровень.pdf");

            return Result.Succeeded;
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null && RevitAPI.UIDocument?.ToSelectedElements().Count == 1)
            {
                _modifyPanel.IsVisible = true;
                _fastSelectButton.IsVisible = true;
                _whoDidButton.IsVisible = RevitAPI.Document.IsWorkshared;
            }
            if (e.OldItems != null || RevitAPI.UIDocument?.ToSelectedElements().Count != 1)
            {
                _modifyPanel.IsVisible = false;
                _whoDidButton.IsVisible = false;
                _fastSelectButton.IsVisible = false;
            }
        }

        private class AlwaysAvailability : IExternalCommandAvailability
        {
            public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
            {
                if (applicationData != null)
                {
                    return true;
                }
                else { return false; }
            }
        }
        private class NotAvailableInFamilyEditor : IExternalCommandAvailability
        {
            public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
            {
                if (applicationData.ActiveUIDocument != null)
                {
                    return !applicationData.ActiveUIDocument.Document.IsFamilyDocument;
                }
                else { return false; }
            }
        }
    }
}